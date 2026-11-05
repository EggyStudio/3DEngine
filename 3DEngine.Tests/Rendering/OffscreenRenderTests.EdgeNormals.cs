using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The normal a fragment reads where a triangle covers some of a pixel's four samples.
public sealed partial class OffscreenRenderTests
{
    [NeedsVulkanFact]
    public void A_Fragment_At_A_Triangles_Edge_Reads_A_Normal_Its_Vertices_Turn_Between()
    {
        // A strip three pixels tall facing the eye, a little aslant, its normals turned 75 degrees
        // up along its top and 75 down along its bottom, drawn by a shader that lights a pixel
        // whose normal turns further down than the bottom's. Read at the middle of a pixel the strip
        // covers some samples of, past its edge, the normal went on turning, which along the foot of
        // Sponza's doors, their bevels turning down, had global illumination read light for a
        // surface facing the floor, a row of white pixels along each foot, 80 at the atrium view's
        // two doors where 45 with the normal read where the samples lie.
        Open(96, 64);
        const string source = """
            import modelpass;

            uniform float4 lowest;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                float3 n = normalize(input.normal);
                return atan2(n.y, n.z) < lowest.x - 0.005 ? float4(1.0) : float4(0.0, 0.0, 0.0, 1.0);
            }
            """;
        var shader = LoadShaderFromMemory(source, "edge_normals.slang");
        var tilt = 75 * MathF.PI / 180;
        var (up, down) = (new Vector3(0, MathF.Sin(tilt), MathF.Cos(tilt)), new Vector3(0, -MathF.Sin(tilt), MathF.Cos(tilt)));
        ModelVertex[] vertices =
        [
            new(new Vector3(-1, -0.06f, 0), down, Vector2.Zero), new(new Vector3(1, -0.06f, 0), down, Vector2.UnitX),
            new(new Vector3(1, 0.06f, 0), up, Vector2.One), new(new Vector3(-1, 0.06f, 0), up, Vector2.UnitY),
        ];
        var strip = LoadModelFromMesh(UploadMesh(vertices, [0, 1, 2, 0, 2, 3]));
        strip.Materials[0].Shader = shader;
        SetShaderValue(shader, GetShaderLocation(shader, "lowest"), new Vector4(-tilt, 0, 0, 0));
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 45);
        var image = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(strip, Vector3.Zero, Vector3.UnitZ, 3, Vector3.One, Color.White);
            EndMode3D();
        });

        var past = 0;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                if (GetImageColor(image, x, y).R > 0) past++;
        past.Should().Be(0, "a fragment's normal lies between its triangle's vertices' normals wherever the triangle covers a sample");
        UnloadModel(strip);
        UnloadShader(shader);
    }
}
