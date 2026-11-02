using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The fog <c>SetVolumetricFog</c> lays over the window's scene, read from frames drawn offscreen
/// with it and without, as <see cref="FrameEffectsTests"/> reads the effects over the frame.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class VolumetricFogTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-fog-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    private static void Open()
    {
        var config = Config.Default.WithWindow("fog test", 160, 120) with { Headless = true, Offscreen = true };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    // Draws frames until the first one's capture has been written, as FrameEffectsTests does.
    private Image Capture(Action draw)
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
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

    private static float Change(Image a, Image b, int x, int y)
    {
        var (p, q) = (GetImageColor(a, x, y), GetImageColor(b, x, y));
        return (Math.Abs(p.R - q.R) + Math.Abs(p.G - q.G) + Math.Abs(p.B - q.B)) / 3f;
    }

    [NeedsVulkanFact]
    public void Fog_Hides_What_Is_Far_More_Than_What_Is_Near()
    {
        Open();
        CreateDirectionalLight(new Vector3(0.3f, -1, 0.2f), Color.White);
        var camera = new Camera3D(new Vector3(0, 1, 0), new Vector3(0, 1, -10), Vector3.UnitY, 45);
        Action draw = () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            // A gray block near on the left and a large one far on the right, each a quarter of the frame across.
            DrawCube(new Vector3(-0.9f, 1, -4), 1.2f, 1.2f, 0.2f, Color.Gray);
            DrawCube(new Vector3(18, 1, -80), 24, 24, 1, Color.Gray);
            EndMode3D();
        };
        var clear = Capture(draw);
        SetVolumetricFog(0.02f, 0, 0, 0);
        var fogged = Capture(draw);

        // Through 4 units of fog 92% of the block's light reaches the eye, and through 80 a fifth.
        var near = Change(clear, fogged, 40, 60);
        var far = Change(clear, fogged, 130, 60);
        far.Should().BeGreaterThan(near * 3, "the fog between the eye and a pixel grows with its distance");
        far.Should().BeGreaterThan(20, "80 units of fog take most of the far block's light");
        GetVolumetricFog().Should().Be((0.02f, 0f, 0f, 0f));
    }

    [NeedsVulkanFact]
    public void Fog_That_Thins_With_Height_Leaves_The_Top_Of_A_Wall_Clearer()
    {
        Open();
        CreateDirectionalLight(new Vector3(0.3f, -1, 0.2f), Color.White);
        var camera = new Camera3D(new Vector3(0, 1, 0), new Vector3(0, 6, -30), Vector3.UnitY, 60);
        Action draw = () =>
        {
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawCube(new Vector3(0, 15, -40), 80, 30, 1, Color.Gray);
            EndMode3D();
        };
        var clear = Capture(draw);
        SetVolumetricFog(0.03f, 1, 0.5f, 0);
        var fogged = Capture(draw);

        // The wall's foot lies in the fog's full density along the way to it, and its top above
        // 20 units, where the fog is e to the tenth thinner.
        var foot = Change(clear, fogged, 80, 78);
        var top = Change(clear, fogged, 80, 12);
        foot.Should().BeGreaterThan(top * 2, "the fog thins above its height, so the way up to the wall's top crosses less of it");
    }
}
