using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The window resized and minimized while it draws, through the commands <c>./e3d</c> sends, on an
/// offscreen run, whose images are made again at each size as a window's swapchain is.
/// </summary>
[Collection("E3D")]
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
        // A stepped clock with frames unpaced, so the resize's debounce of 0.15 s is ten frames of a
        // sixtieth whatever the machine's pace.
        var config = Config.Default.WithWindow("resize test", 160, 120) with
        {
            Headless = true, Offscreen = true, Samples = 4, FrameSeconds = 1.0 / 60, HeadlessFps = 1000,
        };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
    }

    private static string Command(string line)
    {
        var app = GetApp();
        using (ConsoleHost.Lend(app.World, app)) return ConsoleHost.Run(line).Text;
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
        // Frames past the resize's debounce, ten of a sixtieth, and the frames in flight at the old
        // size finished, before the one captured.
        for (int i = 0; i < 12; i++) Frame();
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
    public void A_Size_Set_Before_The_First_Frame_Is_The_Size_That_Frame_Is_Drawn_At()
    {
        // A program sizing its window from its settings before it draws, whose first frame, a
        // loading screen held while a world loads, kept the size the window opened at until a
        // present failed, where the resize waited out its debounce in frames that did not come.
        Open();
        SetWindowSize(240, 135);
        var path = Path.Combine(_folder.Path, "first.png");
        Frame(path);
        for (int i = 0; i < 10 && !File.Exists(path); i++) Frame();
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        var first = LoadImage(path);
        (first.Width, first.Height).Should().Be((240, 135), "the first frame is drawn at the size set before it");
        GetScreenWidth().Should().Be(240);
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty();
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
        Command("window.vsync true").Should().Be("window.vsync is on.");
        GetApp().World.Resource<SurfaceResize>().Vsync.Should().BeTrue("the setting asks for it as the flag does");
        Command("window.vsync off");

        // A resolution picked from a list, as the flat call does it in an offscreen run too.
        SetWindowSize(240, 135);
        var resized = Capture();
        (resized.Width, resized.Height).Should().Be((240, 135));
        GetScreenWidth().Should().Be(240);

        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty();
    }

    [NeedsVulkanFact]
    public void A_Settings_Screen_Changes_The_Samples_While_Frames_Are_Drawn()
    {
        // The window opened at 4 samples a pixel, set to 1 and to 2 as a settings screen sets
        // them, the swapchain, the window's depth and the scene's frame made again at each count
        // on the next frame. The cube is drawn unlit, so at 1 sample no pixel of its edges lies
        // between its color and the background's, and at 2 and 4 some do. Vulkan asks every
        // device for 1 and 4 alone, and lavapipe offers no more, so there 2 is drawn at 1.
        Open();
        var device = (GraphicsDevice)GetApp().World.Resource<Engine.Renderer>().Context.Graphics!;
        int Mixed(Image image)
        {
            var (cube, background) = (GetImageColor(image, 80, 60), GetImageColor(image, 150, 110));
            static int Apart(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            int mixed = 0;
            // Below the line of text, whose glyphs are smoothed whatever the count.
            for (int y = 20; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var c = GetImageColor(image, x, y);
                    if (Apart(c, cube) > 24 && Apart(c, background) > 24) mixed++;
                }
            return mixed;
        }

        var four = Mixed(Capture());
        device.Samples.Should().Be(4);
        Command("window.samples 1").Should().Be("window.samples is 1.");
        var one = Mixed(Capture());
        device.Samples.Should().Be(1, "the swapchain and the frame are made again at the count set");
        Command("window.samples 2").Should().Be("window.samples is 2.");
        var two = Mixed(Capture());
        var twoHeld = device.CanSample(2);
        device.Samples.Should().Be(twoHeld ? 2 : 1, "a count the device lacks is rounded down to one it has");

        one.Should().Be(0, $"at 1 sample no pixel of the cube's edges is mixed, where at 2 and 4 {two} and {four} are");
        if (twoHeld) two.Should().BeGreaterThan(0);
        else two.Should().Be(0, "2 is drawn at 1 where the device lacks it");
        four.Should().BeGreaterThan(0);
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty();
    }

    [NeedsVulkanFact]
    public void The_Probes_Faces_Are_Made_Again_At_The_Samples_Set_And_Each_Probe_Captured_At_Them()
    {
        // A probe in a lit room drawn as a model, captured at the 4 samples the window opened with,
        // both its passes, and left. Set to 1, its faces are made again at 1 and it is captured
        // again, where a probe that is never refreshed is otherwise captured no more.
        Open();
        CreatePointLight(new Vector3(0, 2, 2), Color.White, 20);
        var room = LoadModelFromMesh(GenMeshCube(8, 6, 8));
        room.Materials[0].DoubleSided = true; // seen from inside, where a single-sided cube shows nothing
        var camera = new Camera3D(new Vector3(0, 0, 3), Vector3.Zero, Vector3.UnitY, 60);
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModel(room, Vector3.Zero, 1, new Color(220, 40, 40));
                EndMode3D();
                EndDrawing();
            }
        }
        var handle = CreateReflectionProbe(Vector3.Zero, new Vector3(8, 6, 8));
        Frames(1);
        var probe = GetApp().World.Resource<ReflectionProbes>().ByEntity.Values.Single();
        for (int frame = 0; frame < 120 && probe.Passes < ReflectionProbes.Passes; frame++) Frames(1);
        probe.Passes.Should().BeGreaterThanOrEqualTo(ReflectionProbes.Passes, "the probe is captured twice within a few frames");
        Frames(30);
        var models = GetApp().World.Resource<Engine.Renderer>().RenderWorld.Get<ModelRenderer>();
        models.ProbeFacesSamples.Should().Be(4);
        var captured = probe.CapturedAt;

        Command("window.samples 1");
        // The frame that makes the swapchain again, and a capture of six faces, a face a frame.
        Frames(30);
        models.ProbeFacesSamples.Should().Be(1, "the faces are made again at the count set");
        probe.CapturedAt.Should().BeGreaterThan(captured, "the probe is captured again at the count set");
        IsReflectionProbeReady(handle).Should().BeTrue();
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty();
        UnloadReflectionProbe(handle);
        UnloadModel(room);
        // The setting is the process's, so its count set here would be written into the next
        // app's file of settings.
        Command("reset window.samples");
    }
}
