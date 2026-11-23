using Diagnostics;
using FluentAssertions;
using SDL3;

namespace Engine.Tests.Platform;

/// <summary>
/// The window's page of settings on a window of SDL's offscreen driver, each setting's round
/// through the settings file, given to the window before it is shown and written back as it is
/// set, a mode the system refuses answered with the reason and the mode before kept, a borderless
/// window of the size set in the middle of its screen, borderless fullscreen drawing at the size
/// set and scaling it onto its screen, and the page only reading while the window is hidden.
/// </summary>
/// <remarks>
/// A window of the offscreen driver draws through VK_EXT_headless_surface, and closing it quits
/// SDL for the whole process, so these run alone in the window collection. The offscreen driver
/// places no window and draws no frame, so the borderless window's place and frame are held where
/// the driver keeps them, as X11's does in a headless mutter, which <c>E3D_TEST_VIDEO_DRIVER</c>
/// names for that test.
/// </remarks>
[Collection("Window")]
[Trait("Category", "Render")]
public sealed class WindowSettingsTests
{
    private const string Title = "window settings test";

    // A line run as the e3d client runs it, with the world lent.
    private static string Run(App app, string line)
    {
        using (ConsoleHost.Lend(app.World, app)) return ConsoleHost.Run(line).Text;
    }

    private static void Frames(App app, int count)
    {
        for (var i = 0; i < count; i++)
        {
            app.BeginFrame();
            app.EndFrame();
        }
    }

    // A program of the title opened on the offscreen driver, or the driver named, its settings file
    // at the path.
    private static App Open(string path, bool hidden = false, string driver = "offscreen")
    {
        var given = DiagnosticsBinding.SettingsPath;
        DiagnosticsBinding.SettingsPath = config => config.WindowData.Title == Title ? path : given(config);
        Restore = () => DiagnosticsBinding.SettingsPath = given;
        SDL.SetHint(SDL.Hints.VideoDriver, driver);
        return new App(Config.Default.WithWindow(Title, 320, 240) with { Hidden = hidden }).AddPlugin(new DefaultPlugins());
    }

    private static Action Restore = () => { };

    private static string Driver => Environment.GetEnvironmentVariable("E3D_TEST_VIDEO_DRIVER") is { Length: > 0 } named ? named : "offscreen";

    // The window's size and whether it covers its screen, and where the driver keeps them, its
    // place on its screen and whether it has a frame.
    private static string Standing(IntPtr window)
    {
        SDL.GetWindowSize(window, out var width, out var height);
        var flags = SDL.GetWindowFlags(window);
        var size = $"{width}x{height}{((flags & SDL.WindowFlags.Fullscreen) != 0 ? " covering" : "")}";
        if (Driver == "offscreen") return size;
        SDL.GetWindowPosition(window, out var x, out var y);
        SDL.GetDisplayBounds(SDL.GetDisplayForWindow(window), out var screen);
        return $"{size} at {x - screen.X},{y - screen.Y}{((flags & SDL.WindowFlags.Borderless) != 0 ? " frameless" : "")}";
    }

    // The standing a window of the size has, as Standing reads it.
    private static string Stands(int width, int height, bool covering, (int X, int Y) place, bool frameless)
    {
        var size = $"{width}x{height}{(covering ? " covering" : "")}";
        return Driver == "offscreen" ? size : $"{size} at {place.X},{place.Y}{(frameless ? " frameless" : "")}";
    }

    private static void Close(App app)
    {
        app.Shutdown();
        SDL.ResetHint(SDL.Hints.VideoDriver);
        Restore();
    }

    private static (int Width, int Height) Size(IntPtr window)
    {
        SDL.GetWindowSize(window, out var width, out var height);
        return (width, height);
    }

    [NeedsHeadlessWindowFact]
    public void Each_Setting_Of_The_Window_Goes_Round_Through_The_File_Given_Before_The_Window_Is_Shown()
    {
        using var folder = new TestFolder("window-settings-");
        var path = Path.Combine(folder.Path, "settings.txt");
        File.WriteAllText(path, "window.mode borderlessfullscreen\nwindow.resizable off\nwindow.width 480\nwindow.height 272\nwindow.scaling nearest\nwindow.vsync on\n");
        var app = Open(path);
        try
        {
            var window = app.World.Resource<AppWindow>();
            var sdl = window.Sdl.Window;
            WindowModes.Of(window).Should().Be(WindowMode.BorderlessFullscreen, "the file's mode is given before the window is shown");
            WindowModes.WindowedSize(window).Should().Be((480, 272), "the size as a window is kept while the window covers its screen");

            Frames(app, 2);
            Run(app, "window.mode").Should().StartWith("window.mode is BorderlessFullscreen");
            Run(app, "window.resizable").Should().StartWith("window.resizable is on",
                "SDL's offscreen driver gives a window no edges to fix, so the file's line is refused and leaves the file");
            Run(app, "window.width").Should().StartWith("window.width is 480");
            Run(app, "window.height").Should().StartWith("window.height is 272");
            Run(app, "window.scaling").Should().StartWith("window.scaling is Nearest");
            Run(app, "window.vsync").Should().StartWith("window.vsync is on");

            Run(app, "window.mode windowed").Should().Be("window.mode is Windowed.");
            Size(sdl).Should().Be((480, 272), "leaving the mode the window takes the size it has as a window");
            Run(app, "window.mode borderlessfullscreen").Should().Be("window.mode is BorderlessFullscreen.");
            Run(app, "window.width 512").Should().Be("window.width is 512.", "the size as a window is set while the window covers its screen");
            WindowModes.WindowedSize(window).Should().Be((512, 272), "the size is kept for the window to take as it leaves the mode");
            Run(app, "window.width").Should().StartWith("window.width is 512");
            Run(app, "window.mode windowed");
            Size(sdl).Should().Be((512, 272));
            Run(app, "window.resizable off").Should().Be("The system kept the window resizable.",
                "a driver that answers yes and leaves the window is caught");
            (SDL.GetWindowFlags(sdl) & SDL.WindowFlags.Resizable).Should().NotBe((SDL.WindowFlags)0);
            Run(app, "window.mode borderlessfullscreen");

            File.ReadAllLines(path).Should().BeEquivalentTo(["window.mode BorderlessFullscreen", "window.width 512", "window.height 272", "window.scaling Nearest", "window.vsync on"],
                "each setting set is written, and one back at its default leaves the file");
            Run(app, "window.scaling linear");
            Run(app, "window.monitor 1").Should().Be("There is one screen, so the window stays on it.",
                "the offscreen driver has one screen, so the window's screen only reads");
            Run(app, "window.monitor").Should().StartWith("window.monitor is 0");
        }
        finally
        {
            Close(app);
        }

        File.ReadAllLines(path).Should().BeEquivalentTo(["window.mode BorderlessFullscreen", "window.width 512", "window.height 272", "window.vsync on"],
            "the file written as the program ends holds what it held");
    }

    [NeedsHeadlessWindowFact]
    public void A_Mode_The_System_Refuses_Answers_With_The_Reason_And_Keeps_The_Mode_Before()
    {
        using var folder = new TestFolder("window-refused-");
        var path = Path.Combine(folder.Path, "settings.txt");
        File.WriteAllText(path, "window.mode fullscreen\n");
        var app = Open(path);
        try
        {
            var window = app.World.Resource<AppWindow>();
            WindowModes.Of(window).Should().Be(WindowMode.Windowed, "the screen of SDL's offscreen driver lists no mode for a fullscreen window, so the file's line is refused");
            Frames(app, 1);
            Run(app, "window.mode borderless").Should().Be("window.mode is Borderless.");
            Run(app, "window.mode fullscreen").Should().StartWith("The system refused the fullscreen window, the screen offering no mode for it.");
            Run(app, "window.mode").Should().StartWith("window.mode is Borderless", "the mode before is kept");
            WindowModes.Of(window).Should().Be(WindowMode.Borderless);
            File.ReadAllLines(path).Should().Equal(["window.mode Borderless"], "the refused line is not written again, nor the refused mode");
        }
        finally
        {
            Close(app);
        }
    }

    [NeedsHeadlessWindowFact]
    public void A_Borderless_Window_Takes_The_Size_Set_In_The_Middle_Of_Its_Screen_And_Covers_It_At_The_Screens_Size()
    {
        using var folder = new TestFolder("window-borderless-");
        var app = Open(Path.Combine(folder.Path, "settings.txt"), driver: Driver);
        try
        {
            var window = app.World.Resource<AppWindow>();
            var sdl = window.Sdl.Window;
            Frames(app, 1);
            SDL.GetDisplayBounds(SDL.GetDisplayForWindow(sdl), out var screen);
            (int, int) Middle(int width, int height) => ((screen.W - width) / 2, (screen.H - height) / 2);
            var asWindow = Standing(sdl);

            Run(app, "window.mode borderless").Should().Be("window.mode is Borderless.");
            Standing(sdl).Should().Be(Stands(320, 240, false, Middle(320, 240), frameless: true),
                "a size under the screen's is a window of that size with no frame in the screen's middle");
            Run(app, "window.width 400").Should().Be("window.width is 400.");
            Standing(sdl).Should().Be(Stands(400, 240, false, Middle(400, 240), frameless: true),
                "setting the width resizes the borderless window at once and keeps it in the middle");

            Run(app, $"window.size {screen.W} {screen.H}");
            Standing(sdl).Should().Be(Stands(screen.W, screen.H, true, (0, 0), frameless: true), "a size as large as the screen covers it");
            WindowModes.Of(window).Should().Be(WindowMode.Borderless, "it covers as borderless and is not borderless fullscreen");
            Run(app, "window.size 320 240");
            Standing(sdl).Should().Be(Stands(320, 240, false, Middle(320, 240), frameless: true), "a smaller size leaves the screen again");

            Run(app, "window.mode borderlessfullscreen").Should().Be("window.mode is BorderlessFullscreen.");
            Standing(sdl).Should().Be(Stands(screen.W, screen.H, true, (0, 0), frameless: true), "borderless fullscreen covers the screen at any size set");
            Run(app, "window.width").Should().StartWith("window.width is 320", "the size set is kept while the window covers its screen");
            Run(app, "window.mode windowed").Should().Be("window.mode is Windowed.");
            Standing(sdl).Should().Be(asWindow, "the window takes its frame, its size and its place back");
            // The width back at its default, which the settings of a later window in the process
            // would otherwise write into its file.
            Run(app, "window.width 600");
        }
        finally
        {
            Close(app);
        }
    }

    [NeedsHeadlessWindowFact]
    public void Borderless_Fullscreen_Draws_At_The_Size_Set_And_Scales_It_Onto_Its_Screen_As_Window_Scaling_Says()
    {
        using var folder = new TestFolder("window-scaling-");
        var app = Open(Path.Combine(folder.Path, "settings.txt"));
        try
        {
            var window = app.World.Resource<AppWindow>();
            var device = (GraphicsDevice)app.World.Resource<Engine.Renderer>().Context.Graphics!;
            // Each frame a tenth of a second, so a resize is carried out two frames after it is
            // asked.
            Run(app, "time.frame 0.1");
            Frames(app, 1);
            SDL.GetDisplayBounds(SDL.GetDisplayForWindow(window.Sdl.Window), out var screen);
            Run(app, "window.size 300 200");
            Run(app, "window.mode borderlessfullscreen").Should().Be("window.mode is BorderlessFullscreen.");
            Frames(app, 3);
            (window.Sdl.Width, window.Sdl.Height).Should().Be((300, 200), "the program is given the size the frames are drawn at");

            (byte[] Pixels, int Width, int Height) Take(bool shown)
            {
                (byte[], int, int)? taken = null;
                if (shown) device.RequestShownCapture((rgba, width, height) => taken = (rgba, width, height));
                else device.RequestCapture((rgba, width, height) => taken = (rgba, width, height));
                Frames(app, 1);
                return taken ?? throw new InvalidOperationException("No frame was captured.");
            }
            static bool Black((byte[] Pixels, int Width, int Height) frame, int x, int y)
            {
                var i = (y * frame.Width + x) * 4;
                return frame.Pixels[i] + frame.Pixels[i + 1] + frame.Pixels[i + 2] == 0;
            }

            var drawn = Take(shown: false);
            (drawn.Width, drawn.Height).Should().Be((300, 200), "the frame is drawn at the size set");
            Black(drawn, 0, 0).Should().BeFalse("the frame is the clear color to its corner");

            var fit = Math.Min(screen.W / 300.0, screen.H / 200.0);
            var whole = Math.Floor(fit);
            foreach (var (scaling, width, height) in new[]
                     {
                         ("linear", (int)Math.Round(300 * fit), (int)Math.Round(200 * fit)),
                         ("nearest", (int)Math.Round(300 * fit), (int)Math.Round(200 * fit)),
                         ("integer", (int)(300 * whole), (int)(200 * whole)),
                     })
            {
                Run(app, $"window.scaling {scaling}").Should().StartWith("window.scaling is");
                var shown = Take(shown: true);
                (shown.Width, shown.Height).Should().Be((screen.W, screen.H), "the window covers the screen as {0}", scaling);
                var (x, y) = ((screen.W - width) / 2, (screen.H - height) / 2);
                Black(shown, x, y).Should().BeFalse("the frame's corner is at {0},{1} as {2}", x, y, scaling);
                Black(shown, x + width - 1, y + height - 1).Should().BeFalse("the frame's far corner is {0} by {1} on as {2}", width, height, scaling);
                if (x > 0) Black(shown, x - 1, y).Should().BeTrue("the screen left of the frame is black as {0}", scaling);
                if (y > 0) Black(shown, x, y - 1).Should().BeTrue("the screen over the frame is black as {0}", scaling);
                if (x + width < screen.W) Black(shown, x + width, y).Should().BeTrue("the screen right of the frame is black as {0}", scaling);
                if (y + height < screen.H) Black(shown, x, y + height).Should().BeTrue("the screen under the frame is black as {0}", scaling);
                window.Sdl.ToFrame(x, y).Should().Be((0f, 0f), "the pointer at the frame's corner is at its first pixel as {0}", scaling);
                window.Sdl.ToFrame(x + width / 2f, y + height / 2f).Should().Be((150f, 100f), "the pointer at the frame's middle is at its middle as {0}", scaling);
            }

            Run(app, "window.mode windowed");
            Frames(app, 3);
            window.Sdl.Picture.Should().BeNull("a window draws at its own size");
            (window.Sdl.Width, window.Sdl.Height).Should().Be((300, 200));
            Take(shown: true).Width.Should().Be(300, "the window shows its frames as they are drawn");
            // The settings back at their defaults, which the settings of a later window in the
            // process would otherwise write into its file.
            Run(app, "reset window.scaling");
            Run(app, "reset time.frame");
            Run(app, "window.size 600 400");
        }
        finally
        {
            Close(app);
        }
    }

    [NeedsHeadlessWindowFact]
    public void A_Hidden_Window_Has_Its_Page_Only_Read_With_The_Reason()
    {
        using var folder = new TestFolder("window-hidden-");
        var app = Open(Path.Combine(folder.Path, "settings.txt"), hidden: true);
        try
        {
            Frames(app, 1);
            const string hidden = "The window is hidden, as --hidden or E3D_HIDDEN asks, so its settings only read.";
            foreach (var name in new[] { "window.mode", "window.scaling", "window.resizable", "window.width", "window.height", "window.monitor", "window.vsync" })
            {
                using (ConsoleHost.Lend(app.World, app)) ConsoleHost.Registry.FindSetting(name)!.Locked.Should().Be(hidden, "{0} only reads while the window is hidden", name);
                Run(app, $"reset {name}").Should().Be(hidden);
            }
            Run(app, "window.mode fullscreen").Should().Be(hidden);
            WindowModes.Of(app.World.Resource<AppWindow>()).Should().Be(WindowMode.Windowed);
        }
        finally
        {
            Close(app);
        }
    }

    [NeedsHeadlessWindowFact]
    public void A_Hidden_Window_Keeps_The_Saved_Lines_Of_Its_Page_In_The_File_It_Writes()
    {
        using var folder = new TestFolder("window-hidden-kept-");
        var path = Path.Combine(folder.Path, "settings.txt");
        File.WriteAllText(path, "window.mode borderless\nwindow.width 480\n");
        var app = Open(path, hidden: true);
        try
        {
            Frames(app, 1);
            WindowModes.Of(app.World.Resource<AppWindow>()).Should().Be(WindowMode.Windowed, "the page only reads while the window is hidden, so the file's mode is not taken");
            Run(app, "keys.console F7");
            File.ReadAllLines(path).Should().BeEquivalentTo(["keys.console F7", "window.mode borderless", "window.width 480"],
                "the file written as another setting is set keeps the lines of the page as they stand");
            Run(app, "reset keys.console");
            File.ReadAllLines(path).Should().BeEquivalentTo(["window.mode borderless", "window.width 480"]);
        }
        finally
        {
            Close(app);
        }

        File.ReadAllLines(path).Should().BeEquivalentTo(["window.mode borderless", "window.width 480"], "the file written as the program ends keeps them too");
    }
}
