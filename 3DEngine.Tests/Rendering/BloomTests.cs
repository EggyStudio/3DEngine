using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws frames offscreen with bloom on and off and reads chosen pixels. Light past the threshold
/// spreads into the dark around it, what is drawn after the 3D scene keeps its exact color, and
/// bloom turned off draws the frame it drew before it was on.
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
}
