using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The light of a pixel at the edge of a wall seen nearly edge on at four samples a pixel, its colors
/// given at its corners, dark at one end and light at the other, read for a pixel white where no
/// color the walls hold under the scene's light comes near.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class EdgeLightTests : IDisposable
{
    private const int Width = 640, Height = 360;
    private readonly TestFolder _folder = new("engine-edge-light-");
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // Walls in planes that pass a hair beside the eye, at the origin looking down -z, each seen
    // nearly edge on as a sliver a tenth of a pixel or so across, its color dark where it is near the
    // eye and light where it is far, and its coordinate across it 0 near the eye and 1 far.
    private static ModelMesh Walls()
    {
        var vertices = new List<ModelVertex>();
        var colors = new List<Color>();
        var indices = new List<uint>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color near, Color far)
        {
            var n = Vector3.Normalize(Vector3.Cross(b - a, d - a));
            var first = (uint)vertices.Count;
            foreach (var (v, color, u) in new[] { (a, near, 0f), (b, near, 0f), (c, far, 1f), (d, far, 1f) })
            {
                vertices.Add(new ModelVertex(v, n, new Vector2(u, 0)));
                colors.Add(color);
            }
            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3, first, first + 2, first + 1, first, first + 3, first + 2]);
        }
        var (dark, light) = (new Color(24, 20, 18), new Color(200, 190, 180));
        for (int i = 0; i < 60; i++)
        {
            // Across the view, and a hair to one side of the eye's line, a different hair each.
            var k = -0.6f + 1.2f * i / 59;
            var hair = 0.004f * (1 + i % 7);
            Vector3 At(float z, float y) => new(k * -z + hair, y, z);
            Quad(At(-40, -3), At(-40, 3), At(-160, 3), At(-160, -3), dark, light);
        }
        return UploadMesh([.. vertices], [.. indices], [.. colors], null);
    }

    // The walls at the samples a pixel given under a low sun, drawn with the shader given where one
    // is, how many pixels of the frame are as light as the level given or more, white unless given,
    // which no wall's colors under this light come near, and how many the walls drew in.
    private (int White, int Drawn) White(int samples, string? shader = null, int white = 250)
    {
        var config = Config.Default.WithWindow("edge light", Width, Height) with { Headless = true, Offscreen = true, Samples = samples };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetBloom(0);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -0.5f, -0.6f)), new Color(255, 245, 230), 0.8f);
        SetAmbientLight(new Color(150, 180, 255), 0.3f);
        var walls = LoadModelFromMesh(Walls());
        Shader? own = shader is null ? null : LoadShaderFromMemory(shader, "edge-walls.slang");
        if (own is { } drawnWith) walls.Materials[0].Shader = drawnWith;
        var view = new Camera3D(Vector3.Zero, -Vector3.UnitZ, Vector3.UnitY, 70);
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(new Color(60, 70, 80));
            BeginMode3D(view);
            DrawModel(walls, Vector3.Zero, 1, Color.White);
            EndMode3D();
            if (frame == 2) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        var image = LoadImage(path);
        var (over, drawn) = (0, 0);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var c = GetImageColor(image, x, y);
                if (c.R >= white && c.G >= white && c.B >= white) over++;
                if (Math.Abs(c.R - 60) > 2 || Math.Abs(c.G - 70) > 2 || Math.Abs(c.B - 80) > 2) drawn++;
            }
        UnloadModel(walls);
        if (own is { } made) UnloadShader(made);
        CloseWindow();
        UseApp(null);
        return (over, drawn);
    }

    [NeedsVulkanFact]
    public void A_Pixel_At_A_Wall_Seen_Edge_On_Takes_No_More_Light_Than_Its_Colors_Can()
    {
        // At four samples a pixel a sliver covers a sample in pixels whose middle lies past it, and
        // its colors are read where it covers its samples, so none draws past them; at one sample
        // no pixel's middle lies past its triangle.
        var (one, drawnAtOne) = White(1);
        var (four, drawnAtFour) = White(4);
        drawnAtOne.Should().BeGreaterThan(50, "the walls are seen as slivers at one sample a pixel");
        drawnAtFour.Should().BeGreaterThan(200, "and at four, where a sliver covers a sample in many more pixels");
        one.Should().Be(0, "at one sample no wall draws white");
        four.Should().Be(0, "at four samples a sliver's colors are read where it covers its samples, so no wall draws white");
    }

    [NeedsVulkanFact]
    public void A_Walls_Point_Stays_On_It_At_Its_Edge()
    {
        // Its depth drawn as a gray from 0 to 0.8 across the walls, 204 of 255 at its lightest, so a
        // pixel lighter than 210 holds a point taken past its triangle.
        const string shade = "(-input.world.z - 40.0) / 150.0";
        var (four, drawn) = White(4, $$"""
            import modelpass;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                float v = {{shade}};
                return float4(v, v, v, 1.0);
            }
            """, 210);
        drawn.Should().BeGreaterThan(200, "the walls are seen as slivers at four samples a pixel");
        four.Should().Be(0, "the point is read where a sliver covers its samples, on the wall");
    }
}
