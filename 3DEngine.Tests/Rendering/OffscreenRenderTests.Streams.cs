using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Stream_Of_The_Programs_Own_Feeds_Its_Vertex_Stage_And_A_Mesh_Without_One_Reads_Zero()
    {
        // One shader whose vertex stage colors each vertex from four packed bytes, red, green and
        // blue a byte each, through two planes seen from above: the left carries them, and the right
        // has no stream and reads zero, black over the white it is drawn on. The vertex color is
        // taken as sRGB and decoded, as a mesh's colors are, so the fragment stage encodes it back.
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("vertex")]
            ModelStreamsOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, uint packed : CUSTOM0, ModelInstance instance)
            {
                float4 color = float4(packed & 0xff, (packed >> 8) & 0xff, (packed >> 16) & 0xff, 255) / 255.0;
                return transformModelVertex(position, normal, uv, color, float2(0, 0), instance);
            }

            [shader("fragment")]
            float4 fragmentMain(ModelStreamsOutput input) : SV_Target
            {
                return float4(linearToSrgb(input.vertexColor.rgb), 1);
            }
            """, "packed.slang");
        var plain = GenMeshPlane(1.8f, 1.8f, 1, 1);
        GetApp().World.Resource<MeshStore>().TryGetData(plain.Id, out var vertices, out var indices).Should().BeTrue();
        const uint color = 40 | 160 << 8 | 220 << 16;
        var streamed = UploadMesh(vertices, indices, null, null, MeshStream.From(MeshStreamFormat.UInt, Enumerable.Repeat(color, vertices.Length).ToArray().AsSpan()));
        var material = new ModelMaterial(Color.White) { Shader = shader };

        var image = Capture(() =>
        {
            ClearBackground(Color.White);
            BeginMode3D(new Camera3D(new Vector3(0, 10, 0), Vector3.Zero, -Vector3.UnitZ, 2, CameraProjection.Orthographic));
            DrawMesh(streamed, material, Matrix4x4.CreateTranslation(-1, 0, 0));
            DrawMesh(plain, material, Matrix4x4.CreateTranslation(1, 0, 0));
            EndMode3D();
        }, "streams");

        GetImageColor(image, 16, 16).Should().Be(new Color(40, 160, 220), "the left plane's packed color reaches its vertex stage whole");
        GetImageColor(image, 48, 16).Should().Be(new Color(0, 0, 0), "the right plane has no stream and its stage reads zero");
        UnloadMesh(streamed);
        UnloadMesh(plain);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Mesh_Of_Positions_Alone_Is_Drawn_From_Its_Stream_And_Casts_Its_Shadow()
    {
        // A quad of positions alone, its color packed in four bytes a vertex, floating over a white
        // floor under a light from the side, seen from above: the quad shows its packed color, the
        // floor under its shadow is darker than beside it, so its shadow is drawn from twelve bytes
        // a vertex, and the GPU holds no more of it than those.
        Open(64, 32);
        var shader = LoadShaderFromMemory("""
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
            """, "positions.slang");
        var plane = GenMeshPlane(0.8f, 0.8f, 1, 1);
        GetApp().World.Resource<MeshStore>().TryGetData(plane.Id, out var vertices, out var indices).Should().BeTrue();
        var positions = vertices.Select(v => v.Position + new Vector3(-1.2f, 1, 0)).ToArray();
        const uint color = 40 | 160 << 8 | 220 << 16;
        var quad = UploadMesh(positions, indices, MeshStream.From(MeshStreamFormat.UInt, Enumerable.Repeat(color, positions.Length).ToArray().AsSpan()), null);
        var floor = GenMeshPlane(8, 8, 1, 1);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(1, -1, 0)), Color.White, 1, castsShadows: true);

        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(new Vector3(0, 10, 0), Vector3.Zero, -Vector3.UnitZ, 2, CameraProjection.Orthographic));
            DrawMesh(floor, new ModelMaterial(Color.White), Matrix4x4.Identity);
            DrawMesh(quad, new ModelMaterial(Color.White) { Shader = shader }, Matrix4x4.Identity);
            EndMode3D();
        }, "positions");

        GetImageColor(image, 13, 16).Should().Be(new Color(40, 160, 220), "the quad is drawn from its positions and its packed color");
        var (shaded, lit) = (GetImageColor(image, 29, 16), GetImageColor(image, 51, 16));
        (shaded.R + shaded.G + shaded.B).Should().BeLessThan((lit.R + lit.G + lit.B) / 2, "the floor a block across from the quad lies in its shadow");
        var meshes = GetApp().World.Resource<Engine.Renderer>().RenderWorld.TryGet<GpuMeshes>()!;
        meshes.Get(quad.Id)!.Vertices.Description.Size.Should().Be((ulong)(12 * positions.Length), "the GPU holds its positions alone");
        UnloadMesh(quad);
        UnloadMesh(plane);
        UnloadMesh(floor);
        UnloadShader(shader);
    }
}
