using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    // A model shader that draws a mesh of positions alone white wherever they put it.
    private const string FlatWhite = """
        import modelpass;

        [shader("vertex")]
        ModelVertexOutput vertexMain(float3 position : POSITION, ModelInstance instance)
        {
            return transformModelVertex(position, float3(0, 1, 0), float2(0, 0), instance);
        }

        [shader("fragment")]
        float4 fragmentMain(ModelVertexOutput input) : SV_Target
        {
            return float4(1, 1, 1, 1);
        }
        """;

    [NeedsVulkanFact]
    public void Two_Sections_Packed_On_Their_Grid_Draw_The_Pixels_Their_Float_Positions_Do_And_Meet_With_No_Seam()
    {
        // Two sections of a floor sixteen blocks wide side by side, their vertices on a grid of
        // sixteenths, seen from above through a shader that draws them white, uploaded from their
        // positions alone, which the device packs as 16-bit integers, and as whole vertices of
        // floats, which it does not. Packed, every position comes out of the vertex stage as the
        // float it was, so the frames are the same pixel for pixel and the seam at x 16 shows none
        // of the black behind.
        Open(96, 48);
        var shader = LoadShaderFromMemory(FlatWhite);
        // A fan of triangles about a middle in sixteenths, its rim on the section's edges.
        var rim = Enumerable.Range(0, 64).Select(i => i switch
        {
            < 16 => new Vector3(i, 0, 0),
            < 32 => new Vector3(16, 0, i - 16),
            < 48 => new Vector3(48 - i, 0, 16),
            _ => new Vector3(0, 0, 64 - i),
        }).ToArray();
        Vector3[] positions = [new Vector3(129, 0, 131) / 16f, .. rim];
        var indices = Enumerable.Range(0, 64).SelectMany(i => new[] { 0u, (uint)(1 + i), (uint)(1 + (i + 1) % 64) }).ToArray();
        var packed = UploadMesh(positions, indices, null, null);
        var floats = UploadMesh([.. positions.Select(p => new ModelVertex { Position = p, Normal = Vector3.UnitY })], indices);
        var camera = new Camera3D(new Vector3(16, 40, 8), new Vector3(16, 0, 8), -Vector3.UnitZ, 17, CameraProjection.Orthographic);
        Image Floor(ModelMesh mesh, string name) => Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawMesh(mesh, new ModelMaterial(Color.White) { Shader = shader, DoubleSided = true }, Matrix4x4.Identity);
            DrawMesh(mesh, new ModelMaterial(Color.White) { Shader = shader, DoubleSided = true }, Matrix4x4.CreateTranslation(16, 0, 0));
            EndMode3D();
        }, name);
        var (fromPacked, fromFloats) = (Floor(packed, "packed"), Floor(floats, "floats"));

        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        var entry = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GpuMeshes>()!.Get(packed.Id)!;
        if (device.PackedPositions is { } format)
        {
            entry.PositionFormat.Should().Be(format, "the positions lie on a grid of sixteenths, which 16-bit integers hold");
            entry.Quantum.Should().Be(MathF.Pow(2, -10));
        }
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 96; x++)
                GetImageColor(fromPacked, x, y).Should().Be(GetImageColor(fromFloats, x, y), $"pixel {x}, {y} is drawn from the same positions");
        var inside = GetImageColor(fromPacked, 24, 24);
        (inside.R + inside.G + inside.B).Should().BeGreaterThan(300, "the sections are drawn");
        for (int y = 2; y < 46; y++)
            GetImageColor(fromPacked, 48, y).Should().Be(inside, $"the seam between the sections at row {y} shows nothing behind");
        UnloadMesh(packed);
        UnloadMesh(floats);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Packed_Mesh_Written_Off_Its_Grid_Takes_Floats_And_Written_Back_On_It_Packs_Again()
    {
        // A quad of whole blocks seen from above, packed, its positions written in place four thirds
        // of a block to the right, which no power of two divides, and then back, drawn each time
        // where its positions put it, as integers, floats and integers again.
        Open(64, 32);
        var shader = LoadShaderFromMemory(FlatWhite);
        Vector3[] quad = [new(-1, 0, -1), new(1, 0, -1), new(1, 0, 1), new(-1, 0, 1)];
        var mesh = UploadMesh(quad, [0, 2, 1, 0, 3, 2], null, null);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var packed = ((GraphicsDevice)renderer.Context.Graphics!).PackedPositions;
        // The mesh's format and quantum on the GPU after a frame, and the brightness left and right of
        // the middle where it is drawn.
        (VertexFormat Format, float Quantum, int Left, int Right) Drawn(string name)
        {
            var image = Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(new Camera3D(new Vector3(0, 10, 0), Vector3.Zero, -Vector3.UnitZ, 4, CameraProjection.Orthographic));
                DrawMesh(mesh, new ModelMaterial(Color.White) { Shader = shader, DoubleSided = true }, Matrix4x4.Identity);
                EndMode3D();
            }, name);
            var entry = renderer.RenderWorld.TryGet<GpuMeshes>()!.Get(mesh.Id)!;
            var (left, right) = (GetImageColor(image, 28, 16), GetImageColor(image, 46, 16));
            return (entry.PositionFormat, entry.Quantum, left.R + left.G + left.B, right.R + right.G + right.B);
        }

        var onGrid = Drawn("on-grid");
        UpdateMeshBuffer<Vector3>(mesh, 0, [.. quad.Select(p => p + new Vector3(4 / 3f, 0, 0))], 0);
        var offGrid = Drawn("off-grid");
        UpdateMeshBuffer<Vector3>(mesh, 0, quad, 0);
        var back = Drawn("back");

        if (packed is { } format)
        {
            onGrid.Format.Should().Be(format, "whole blocks lie on a grid");
            back.Format.Should().Be(format, "the positions written back lie on it again");
            back.Quantum.Should().Be(onGrid.Quantum);
        }
        offGrid.Format.Should().Be(VertexFormat.Float3, "a third of a block lies on no grid of a power of two");
        offGrid.Quantum.Should().Be(1, "floats are drawn through the rows as they are");
        (onGrid.Left > 300, onGrid.Right).Should().Be((true, 0), "the quad spans x 24 to 40");
        (offGrid.Left, offGrid.Right > 300).Should().Be((0, true), "the quad moved spans x 35 to 51");
        (back.Left > 300, back.Right).Should().Be((true, 0), "the quad is back where it was");
        UnloadMesh(mesh);
        UnloadShader(shader);
    }
}
