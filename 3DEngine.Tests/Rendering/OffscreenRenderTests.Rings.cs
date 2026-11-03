using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    // A model shader that draws a mesh of positions alone in the color its stream packs, a byte a
    // channel.
    private const string StreamColor = """
        import modelpass;

        [shader("vertex")]
        ModelStreamsOutput vertexMain(float3 position : POSITION, uint packed : CUSTOM0, ModelInstance instance)
        {
            float4 color = float4(packed & 0xff, (packed >> 8) & 0xff, (packed >> 16) & 0xff, 255) / 255.0;
            return transformModelVertex(position, float3(0, 1, 0), float2(0, 0), color, float2(0, 0), instance);
        }

        [shader("fragment")]
        float4 fragmentMain(ModelStreamsOutput input) : SV_Target
        {
            return float4(linearToSrgb(input.vertexColor.rgb), 1);
        }
        """;

    [NeedsVulkanFact]
    public void A_Mesh_Written_In_Place_Gives_Its_Rings_Back_Once_The_Writes_Stop_And_Draws_Its_Last_Writes()
    {
        // A quad of positions alone seen from above, its color packed in its stream, red. Written
        // green in place, its stream moves into a ring of buffers the CPU writes, and after frames
        // with no write the ring is given back for one buffer that still draws green. Written blue
        // it takes a ring again, and its positions written a block to the right do the same.
        Open(64, 32);
        var shader = LoadShaderFromMemory(StreamColor, "rings.slang");
        Vector3[] corners = [new(-0.5f, 0, -0.5f), new(0.5f, 0, -0.5f), new(0.5f, 0, 0.5f), new(-0.5f, 0, 0.5f)];
        const uint red = 200, green = 200 << 8, blue = 200 << 16;
        var quad = UploadMesh(corners, [0, 2, 1, 0, 3, 2], MeshStream.From(MeshStreamFormat.UInt, new[] { red, red, red, red }.AsSpan()), null);
        // The mesh's buffers, which the render world holds once a frame has been drawn.
        GpuMeshes.Entry Buffers() => GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GpuMeshes>()!.Get(quad.Id)!;
        // The colors in the middle and a block to the right after a frame.
        (Color Middle, Color Right) Drawn(string name)
        {
            var image = Capture(() =>
            {
                ClearBackground(Color.Black);
                BeginMode3D(new Camera3D(new Vector3(0, 10, 0), Vector3.Zero, -Vector3.UnitZ, 2, CameraProjection.Orthographic));
                DrawMesh(quad, new ModelMaterial(Color.White) { Shader = shader, DoubleSided = true }, Matrix4x4.Identity);
                EndMode3D();
            }, name);
            return (GetImageColor(image, 32, 16), GetImageColor(image, 48, 16));
        }
        void Quiet()
        {
            for (int i = 0; i <= GpuTextures.RetireFrames; i++) Drawn($"quiet {i}");
        }

        Drawn("red").Middle.Should().Be(new Color(200, 0, 0));
        UpdateMeshBuffer<uint>(quad, MeshStreamBuffer, [green, green, green, green], 0);
        Drawn("green").Middle.Should().Be(new Color(0, 200, 0));
        var ring = Buffers().CustomRing;
        ring.Should().NotBeNull("a stream written in place moves into a ring");
        Quiet();
        var settled = Buffers();
        settled.CustomRing.Should().BeNull("the ring is given back once the writes stop");
        ring!.Should().NotContain(settled.Custom!.Value.Buffer, "the stream lies in one buffer of its own");
        Drawn("green settled").Middle.Should().Be(new Color(0, 200, 0), "the buffer given for the ring holds its last write");
        UpdateMeshBuffer<uint>(quad, MeshStreamBuffer, [blue, blue, blue, blue], 0);
        Drawn("blue").Middle.Should().Be(new Color(0, 0, 200));
        Buffers().CustomRing.Should().NotBeNull("a write after makes the ring again");

        UpdateMeshBuffer<Vector3>(quad, 0, [.. corners.Select(c => c + Vector3.UnitX)], 0);
        Drawn("moved").Right.Should().Be(new Color(0, 0, 200));
        Buffers().Ring.Should().NotBeNull("positions written in place move into a ring");
        Quiet();
        var moved = Buffers();
        (moved.Ring, moved.CustomRing).Should().Be((null, null), "both rings are given back once the writes stop");
        var still = Drawn("moved settled");
        (still.Middle, still.Right).Should().Be((Color.Black, new Color(0, 0, 200)), "the buffers given back hold the last positions and colors");
        UnloadMesh(quad);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Relight_Of_A_Mesh_Of_Positions_Alone_Writes_Its_Stream_In_Place_And_Keeps_Its_Colors_Off_The_GPU()
    {
        // A quad of positions alone with a stream and colors, relit as a voxel game's section is,
        // its stream written green and then its colors. The GPU holds none of its colors, so the
        // colors' write leaves its positions and indices as they were and the stream's write stays
        // in place, the colors kept for the light field.
        Open(64, 32);
        var shader = LoadShaderFromMemory(StreamColor, "relight.slang");
        Vector3[] corners = [new(-0.5f, 0, -0.5f), new(0.5f, 0, -0.5f), new(0.5f, 0, 0.5f), new(-0.5f, 0, 0.5f)];
        const uint red = 200, green = 200 << 8;
        var quad = UploadMesh(corners, [0, 2, 1, 0, 3, 2], MeshStream.From(MeshStreamFormat.UInt, new[] { red, red, red, red }.AsSpan()), [.. Enumerable.Repeat(Color.Red, 4)]);
        GpuMeshes.Entry Buffers() => GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GpuMeshes>()!.Get(quad.Id)!;
        Color Drawn(string name) => GetImageColor(Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 10, 0), Vector3.Zero, -Vector3.UnitZ, 2, CameraProjection.Orthographic));
            DrawMesh(quad, new ModelMaterial(Color.White) { Shader = shader, DoubleSided = true }, Matrix4x4.Identity);
            EndMode3D();
        }, name), 32, 16);

        Drawn("red").Should().Be(new Color(200, 0, 0));
        var before = Buffers();
        UpdateMeshBuffer<uint>(quad, MeshStreamBuffer, [green, green, green, green], 0);
        UpdateMeshBuffer<Color>(quad, 3, [Color.Green, Color.Green, Color.Green, Color.Green], 0);
        Drawn("relit").Should().Be(new Color(0, 200, 0), "the stream written in place is drawn");

        var after = Buffers();
        after.Vertices.Should().BeSameAs(before.Vertices, "the colors' write uploads none of the positions again");
        after.Indices.Should().BeSameAs(before.Indices, "nor the indices");
        after.CustomRing.Should().NotBeNull("the stream's write stays a write in place");
        after.Colors.Should().BeNull("the GPU holds none of the mesh's colors");
        GetApp().World.Resource<MeshStore>().StreamsOf(quad.Id)!.Colors.Should().Equal(Enumerable.Repeat(Color.Green, 4), "the colors are kept for the light field");
        UnloadMesh(quad);
        UnloadShader(shader);
    }
}
