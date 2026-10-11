using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

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
}
