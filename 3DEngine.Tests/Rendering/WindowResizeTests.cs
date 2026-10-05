using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The window resized and minimized while it draws, through the commands <c>./e3d</c> sends, on an
/// offscreen run, whose images are made again at each size as a window's swapchain is.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class WindowResizeTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-resize-");
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
        var config = Config.Default.WithWindow("resize test", 160, 120) with { Headless = true, Offscreen = true, Samples = 4 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    private static string Command(string line)
    {
        var app = GetApp();
        using (ConsoleHost.Lend(app.World, app)) return ConsoleCommands.Run(line) ?? "";
    }

    // A lit scene with bloom, so the HDR frame, the bloom chain and the shadow are made again at
    // each size too, and a line of text over it.
    private static void Frame(string? shot = null)
    {
        BeginDrawing();
        if (shot is not null) TakeScreenshot(shot);
        ClearBackground(new Color(20, 30, 50));
        BeginMode3D(new Camera3D(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, 45));
        DrawCube(Vector3.Zero, 1, 1, 1, Color.Orange);
        EndMode3D();
        DrawText("resize", 4, 4, 10, Color.White);
        EndDrawing();
    }

    private Image Capture()
    {
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        // Past the resize's settling time, so the frame captured is drawn at the new size.
        for (int i = 0; i < 20; i++)
        {
            Frame();
            Thread.Sleep(10);
        }
        for (int i = 0; i < 10 && !File.Exists(path); i++) Frame(i == 0 ? path : null);
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        return LoadImage(path);
    }

    [NeedsVulkanFact]
    public void Frames_Follow_A_Storm_Of_Resizes_And_Come_Back_From_A_Minimize()
    {
        Open();
        SetBloom(0.5f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, -2, -1)), Color.White, 1, castsShadows: true);
        Capture().Width.Should().Be(160);

        // Sizes one after another, a frame or two apart, as a window dragged larger and smaller.
        foreach (var (w, h) in new[] { (300, 200), (90, 300), (640, 360), (33, 17), (200, 150) })
        {
            Command($"window.size {w} {h}");
            Frame();
            Frame();
        }
        var resized = Capture();
        (resized.Width, resized.Height).Should().Be((200, 150), "frames are drawn at the size the last resize asked for");
        GetScreenWidth().Should().Be(200, "the program sees the size it is drawn at");

        Command("window.minimize");
        for (int i = 0; i < 30; i++) Frame();
        Command("window.restore");
        var restored = Capture();
        (restored.Width, restored.Height).Should().Be((200, 150), "a restored run is drawn at the size it had");
        GetImageColor(restored, 100, 75).Should().NotBe(new Color(20, 30, 50), "the scene is drawn again after the minimize");

        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong through the storm");
    }

    [NeedsVulkanFact]
    public void A_Settings_Screen_Changes_Vsync_And_The_Size_While_Frames_Are_Drawn()
    {
        Open();
        Capture();

        // Vsync on and off again, each making the images again on the next frame.
        SetWindowState(ConfigFlags.VsyncHint);
        Frame();
        GetApp().World.Resource<SurfaceResize>().Vsync.Should().BeTrue("the request is kept as the state asked for");
        ClearWindowState(ConfigFlags.VsyncHint);
        Frame();
        GetApp().World.Resource<SurfaceResize>().Vsync.Should().BeFalse();
        Command("window.vsync true").Should().Be("vsync on");

        // A resolution picked from a list, as the flat call does it in an offscreen run too.
        SetWindowSize(240, 135);
        var resized = Capture();
        (resized.Width, resized.Height).Should().Be((240, 135));
        GetScreenWidth().Should().Be(240);

        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty();
    }
}
