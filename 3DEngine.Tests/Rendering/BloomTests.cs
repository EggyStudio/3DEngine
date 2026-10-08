using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws frames offscreen with bloom on and off and reads chosen pixels. Light past the threshold
/// spreads into the dark around it, what is drawn after the 3D scene keeps its exact color, bloom
/// turned off draws the frame it drew before it was on, and a shader of the program's own draws
/// alike either way.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class BloomTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-bloom-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private readonly Camera3D _camera = new(new Vector3(0, 0, 6), Vector3.Zero, Vector3.UnitY, 45);

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("bloom test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames until the first one's capture has been written, as ReferenceFrameTests does.
    private Image Capture(Action draw, string name)
    {
        var path = Path.Combine(_folder.Path, name + ".png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            draw();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        return LoadImage(path);
    }

    // A sphere giving off four times white's light in the middle of a black frame.
    private Action GlowingSphere(Model sphere, Action? after = null) => () =>
    {
        ClearBackground(Color.Black);
        BeginMode3D(_camera);
        sphere.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 140, 60), EmissiveIntensity = 4 };
        DrawModel(sphere, Vector3.Zero, 1, Color.White);
        EndMode3D();
        after?.Invoke();
    };

    private static int Brightness(Image image, int x, int y)
    {
        var c = GetImageColor(image, x, y);
        return Math.Max(c.R, Math.Max(c.G, c.B));
    }

    [NeedsVulkanFact]
    public void Light_Past_The_Threshold_Spreads_Past_The_Edge_Of_What_Gives_It_Off()
    {
        Open();
        var sphere = LoadModelFromMesh(GenMeshSphere(0.6f, 24, 24));

        var off = Capture(GlowingSphere(sphere), "off");
        SetBloom(1);
        var on = Capture(GlowingSphere(sphere), "on");

        // The sphere is about 28 pixels across, so 20 from its middle is a little past its edge, in the black.
        Brightness(off, 80, 60).Should().BeGreaterThan(200, "the sphere is drawn in the middle");
        Brightness(off, 100, 60).Should().BeLessThan(4, "with bloom off nothing lights the frame past the sphere");
        Brightness(on, 100, 60).Should().BeGreaterThan(20, "with bloom on its light spreads past its edge");
        UnloadModel(sphere);
    }

    [NeedsVulkanFact]
    public void What_Is_Drawn_After_The_Scene_Keeps_Its_Exact_Color()
    {
        Open();
        SetBloom(1);
        var sphere = LoadModelFromMesh(GenMeshSphere(0.6f, 24, 24));

        var frame = Capture(GlowingSphere(sphere, () =>
        {
            DrawRectangle(90, 50, 30, 20, new Color(200, 100, 50));
            DrawText("UI", 4, 4, 20, new Color(250, 250, 250));
        }), "ui");

        GetImageColor(frame, 105, 60).Should().Be(new Color(200, 100, 50), "a rectangle drawn after EndMode3D covers the bloom, untouched by the tonemap");
        Brightness(frame, 100, 90).Should().BeGreaterThan(5, "the bloom is still drawn where nothing covers it");
        UnloadModel(sphere);
    }

    [NeedsVulkanFact]
    public void A_Shape_In_3D_Keeps_Its_Color_Below_The_Threshold()
    {
        Open();
        void Cube()
        {
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawCube(Vector3.Zero, 2, 2, 2, new Color(60, 120, 180));
            EndMode3D();
        }

        var off = Capture(Cube, "off");
        SetBloom(1);
        var on = Capture(Cube, "on");

        var (a, b) = (GetImageColor(off, 80, 60), GetImageColor(on, 80, 60));
        a.Should().Be(new Color(60, 120, 180), "a shape is drawn in its color");
        Math.Abs(a.R - b.R).Should().BeLessThanOrEqualTo(1, "decoded to linear and encoded again it comes back as it went in");
        Math.Abs(a.G - b.G).Should().BeLessThanOrEqualTo(1);
        Math.Abs(a.B - b.B).Should().BeLessThanOrEqualTo(1);
    }

    [NeedsVulkanFact]
    public void Bloom_Turned_Off_Draws_The_Frame_It_Drew_Before()
    {
        Open();
        CreateDirectionalLight(Vector3.Normalize(new Vector3(0.4f, -1, -0.5f)), Color.White, 1, castsShadows: true);
        var sphere = LoadModelFromMesh(GenMeshSphere(0.6f, 24, 24));
        var draw = GlowingSphere(sphere, () => DrawText("UI", 4, 4, 20, Color.RayWhite));

        var before = Capture(draw, "before");
        SetBloom(1);
        Capture(draw, "on");
        SetBloom(0);
        var after = Capture(draw, "after");

        for (int y = 0; y < before.Height; y++)
            for (int x = 0; x < before.Width; x++)
                GetImageColor(after, x, y).Should().Be(GetImageColor(before, x, y), $"the pixel at {x}, {y} is drawn as it was with bloom never on");
        UnloadModel(sphere);
    }

    [NeedsVulkanFact]
    public void Shapes_Drawn_With_Depth_Meet_The_Scenes_Depth()
    {
        // A model and shapes drawn with depth in one scene, at four samples, so the HDR frame draws
        // into the window's own multisampled depth: a bar behind the model hidden where the model
        // stands before it and seen past its sides, and a cube before the model covering it.
        Open();
        var block = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        var frame = Capture(() =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawModel(block, Vector3.Zero, 1, new Color(40, 200, 40));
            DrawCube(new Vector3(0, 0, -2), 6, 1, 1, Color.Red);
            DrawCube(new Vector3(0.6f, 0, 2), 0.6f, 0.6f, 0.6f, Color.Blue);
            EndMode3D();
        }, "depth");

        GetImageColor(frame, 70, 60).Should().Be(new Color(40, 200, 40), "the model stands before the bar there");
        GetImageColor(frame, 35, 60).Should().Be(Color.Red, "the bar is seen past the model's side");
        GetImageColor(frame, 102, 60).Should().Be(Color.Blue, "the cube stands before the model");
        UnloadModel(block);
    }

    [NeedsVulkanFact]
    public void A_Model_Shader_Of_The_Programs_Own_Draws_Alike_With_Bloom_On_Or_Off()
    {
        // A color returned as raylib's shaders return one, sRGB-encoded, which the window's scene
        // reads the same whichever effects are on, its one pass to the window drawn every frame.
        Open();
        var shader = LoadShaderFromMemory("""
            import modelpass;

            [shader("fragment")]
            float4 fragmentMain(ModelVertexOutput input) : SV_Target
            {
                return float4(0.2, 0.6, 0.8, 1.0);
            }
            """, "flat.slang");
        var cube = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        cube.Materials[0].Shader = shader;
        void Cube()
        {
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawModel(cube, Vector3.Zero, 1, Color.White);
            EndMode3D();
        }

        var off = Capture(Cube, "off");
        SetBloom(1);
        var on = Capture(Cube, "on");

        foreach (var (frame, what) in new[] { (off, "with bloom off"), (on, "with bloom on, the color under its threshold") })
        {
            var c = GetImageColor(frame, 80, 60);
            Math.Abs(c.R - 51).Should().BeLessThanOrEqualTo(1, $"the color returned is taken as encoded, as a 2D color is, {what}");
            Math.Abs(c.G - 153).Should().BeLessThanOrEqualTo(1, what);
            Math.Abs(c.B - 204).Should().BeLessThanOrEqualTo(1, what);
        }
        UnloadModel(cube);
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void A_Shader_Of_The_Programs_Own_Blends_As_In_An_Eight_Bit_Frame()
    {
        // An alpha past 1, as raylib's shaders give where their gamma correction is raised to the
        // alpha too, is held at 1 as an eight-bit frame holds it, so the color is laid on as it is
        // rather than pushing what is under it below 0.
        Open();
        var shader = LoadShaderFromMemory("""
            import engine;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                return float4(0.5, 0.5, 0.5, 1.5);
            }
            """, "past.slang");
        var frame = Capture(() =>
        {
            ClearBackground(Color.White);
            BeginMode3D(_camera);
            BeginShaderMode(shader);
            DrawCube(Vector3.Zero, 2, 2, 2, Color.White);
            EndShaderMode();
            EndMode3D();
        }, "past");

        Math.Abs(GetImageColor(frame, 80, 60).R - 128).Should().BeLessThanOrEqualTo(1,
            "the alpha is held at 1, so 0.5 is laid over the white as it is, where 1.5 would leave 0.25");
        UnloadShader(shader);
    }

    [NeedsVulkanFact]
    public void White_Is_Bent_Only_Where_Light_Past_White_Can_Come_About()
    {
        // With no light, no sky and no effect, the scene is drawn as raylib draws one, white as
        // white, and once a light can carry the scene past white the engine's curve bends white too.
        Open();
        void Cube()
        {
            ClearBackground(Color.Black);
            BeginMode3D(_camera);
            DrawCube(Vector3.Zero, 2, 2, 2, Color.White);
            EndMode3D();
        }

        var unlit = Capture(Cube, "unlit");
        CreatePointLight(new Vector3(0, 3, 3), Color.White, 1);
        var lit = Capture(Cube, "lit");

        GetImageColor(unlit, 80, 60).Should().Be(Color.White, "a frame no light reaches clamps its light, which leaves white as it is");
        GetImageColor(lit, 80, 60).R.Should().BeInRange(249, 252, "the curve brings 1 to about 0.963, 251 encoded, once a light is made");
    }
}
