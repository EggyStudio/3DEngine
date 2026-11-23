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
            // Each frame a tenth of a second, so a resize is carried out two frames after it is
            // asked.
            Run(app, "time.frame 0.1");
            Frames(app, 1);
            SDL.GetDisplayBounds(SDL.GetDisplayForWindow(window.Sdl.Window), out var screen);
            Run(app, "window.size 300 200");
            Run(app, "window.mode borderlessfullscreen").Should().Be("window.mode is BorderlessFullscreen.");
            Frames(app, 3);
            (window.Sdl.Width, window.Sdl.Height).Should().Be((300, 200), "the program is given the size the frames are drawn at");

            var drawn = Take(app, shown: false);
            (drawn.Width, drawn.Height).Should().Be((300, 200), "the frame is drawn at the size set");
            Black(drawn, 0, 0).Should().BeFalse("the frame is the clear color to its corner");

            var fit = Math.Min(screen.W / 300.0, screen.H / 200.0);
            var (width, height) = ((int)Math.Round(300 * fit), (int)Math.Round(200 * fit));
            foreach (var scaling in new[] { "linear", "nearest", "sharp" })
            {
                Run(app, $"window.scaling {scaling}").Should().StartWith("window.scaling is");
                var shown = Take(app, shown: true);
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
            Take(app, shown: true).Width.Should().Be(300, "the window shows its frames as they are drawn");
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

    // The frame drawn, or the window's image it is shown in, as the next frame leaves it.
    private static (byte[] Pixels, int Width, int Height) Take(App app, bool shown)
    {
        var device = (GraphicsDevice)app.World.Resource<Engine.Renderer>().Context.Graphics!;
        (byte[], int, int)? taken = null;
        if (shown) device.RequestShownCapture((rgba, width, height) => taken = (rgba, width, height));
        else device.RequestCapture((rgba, width, height) => taken = (rgba, width, height));
        Frames(app, 1);
        return taken ?? throw new InvalidOperationException("No frame was captured.");
    }

    private static bool Black((byte[] Pixels, int Width, int Height) frame, int x, int y)
    {
        var i = (y * frame.Width + x) * 4;
        return frame.Pixels[i] + frame.Pixels[i + 1] + frame.Pixels[i + 2] == 0;
    }

    // Draws, each frame, columns a pixel wide over the whole frame and rectangles of one to three
    // pixels over them, each of its own color and none black, so a pixel of the frame most often
    // differs from those beside it.
    private static void Pattern(App app)
    {
        var random = new Random(314);
        var columns = Enumerable.Range(0, 4096).Select(_ => Hue(random)).ToArray();
        var rectangles = Enumerable.Range(0, 20000)
            .Select(_ => (X: random.NextDouble(), Y: random.NextDouble(), Width: random.Next(1, 4), Height: random.Next(1, 4), Color: Hue(random)))
            .ToArray();
        app.AddSystem(Stage.Update, world =>
        {
            var sdl = world.Resource<AppWindow>().Sdl;
            var (width, height) = (sdl.Width, sdl.Height);
            var list = world.Resource<DrawList>();
            list.SetTransform(System.Numerics.Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1), false);
            for (var x = 0; x < width; x++) Rectangle(list, x, 0, 1, height, columns[x % columns.Length]);
            foreach (var r in rectangles)
                Rectangle(list, (int)(r.X * width), (int)(r.Y * height), r.Width, r.Height, r.Color);
        });

        static Color Hue(Random random) => new((byte)random.Next(32, 224), (byte)random.Next(32, 224), (byte)random.Next(32, 224));
        static void Rectangle(DrawList list, int x, int y, int width, int height, Color color) =>
            list.Quad(new(x, y, 0), new(x, y + height, 0), new(x + width, y + height, 0), new(x + width, y, 0), color);
    }

    // The app's window in borderless fullscreen drawing frames of the size the screen's size gives,
    // and the screen.
    private static (AppWindow Window, SDL.Rect Screen) Fullscreen(App app, Func<int, int> width, Func<int, int> height)
    {
        var window = app.World.Resource<AppWindow>();
        Run(app, "time.frame 0.1");
        Frames(app, 1);
        SDL.GetDisplayBounds(SDL.GetDisplayForWindow(window.Sdl.Window), out var screen);
        Run(app, $"window.size {width(screen.W)} {height(screen.H)}");
        Run(app, "window.mode borderlessfullscreen").Should().Be("window.mode is BorderlessFullscreen.");
        Frames(app, 3);
        (window.Sdl.Width, window.Sdl.Height).Should().Be((width(screen.W), height(screen.H)));
        return (window, screen);
    }

    // The settings back at their defaults, which the settings of a later window in the process
    // would otherwise write into its file.
    private static void Reset(App app)
    {
        Run(app, "window.mode windowed");
        Frames(app, 3);
        Run(app, "reset window.scaling");
        Run(app, "reset time.frame");
        Run(app, "window.size 600 400");
    }

    // A frame pixel and those round it, the nearest first.
    private static readonly (int X, int Y)[] Round = [(0, 0), (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1)];

    // The corners of the four squares of two by two pixels that hold a pixel.
    private static readonly (int X, int Y)[] Squares = [(-1, -1), (0, -1), (-1, 0), (0, 0)];

    private static int Channel((byte[] Pixels, int Width, int Height) image, int x, int y, int c) =>
        image.Pixels[(Math.Clamp(y, 0, image.Height - 1) * image.Width + Math.Clamp(x, 0, image.Width - 1)) * 4 + c];

    private static bool Same((byte[] Pixels, int Width, int Height) a, int ax, int ay, (byte[] Pixels, int Width, int Height) b, int bx, int by) =>
        Channel(a, ax, ay, 0) == Channel(b, bx, by, 0) && Channel(a, ax, ay, 1) == Channel(b, bx, by, 1) && Channel(a, ax, ay, 2) == Channel(b, bx, by, 2);

    // Whether each channel of a pixel of the image lies among those of the square of two by two
    // pixels of the frame from its corner at x, y, a step over either way.
    private static bool Within((byte[] Pixels, int Width, int Height) image, int ix, int iy, (byte[] Pixels, int Width, int Height) frame, int x, int y)
    {
        for (var c = 0; c < 3; c++)
        {
            var (a, b, d, e) = (Channel(frame, x, y, c), Channel(frame, x + 1, y, c), Channel(frame, x, y + 1, c), Channel(frame, x + 1, y + 1, c));
            var value = Channel(image, ix, iy, c);
            if (value < Math.Min(Math.Min(a, b), Math.Min(d, e)) - 1 || value > Math.Max(Math.Max(a, b), Math.Max(d, e)) + 1) return false;
        }
        return true;
    }

    // Whether each channel of a pixel of the image is the mean of those of two pixels of the frame,
    // a step over either way.
    private static bool Even((byte[] Pixels, int Width, int Height) image, int ix, int iy, (byte[] Pixels, int Width, int Height) frame, int ax, int ay, int bx, int by)
    {
        for (var c = 0; c < 3; c++)
            if (Math.Abs(2 * Channel(image, ix, iy, c) - Channel(frame, ax, ay, c) - Channel(frame, bx, by, c)) > 2) return false;
        return true;
    }

    // The first pixel the two captures differ at, or null where they are the same to the bit.
    private static (int X, int Y)? Differs((byte[] Pixels, int Width, int Height) a, (byte[] Pixels, int Width, int Height) b)
    {
        var at = a.Pixels.AsSpan().CommonPrefixLength(b.Pixels);
        return at == a.Pixels.Length && a.Pixels.Length == b.Pixels.Length ? null : (at / 4 % a.Width, at / 4 / a.Width);
    }

    [NeedsHeadlessWindowFact]
    public void Sharp_Scaling_At_A_Whole_Ratio_Is_Nearest_To_The_Bit()
    {
        using var folder = new TestFolder("window-sharp-whole-");
        var app = Open(Path.Combine(folder.Path, "settings.txt"));
        Pattern(app);
        try
        {
            var (_, screen) = Fullscreen(app, width => width / 2, height => height / 2);
            Run(app, "window.scaling nearest");
            var nearest = Take(app, shown: true);
            Run(app, "window.scaling sharp");
            var sharp = Take(app, shown: true);
            (sharp.Width, sharp.Height).Should().Be((screen.W, screen.H));
            Black(sharp, screen.W / 2, screen.H / 2).Should().BeFalse("the frame is drawn onto the window");
            Differs(sharp, nearest).Should().BeNull("at a ratio of two each frame pixel is a square of four screen pixels whole, as Nearest draws it");
        }
        finally
        {
            Reset(app);
            Close(app);
        }
    }

    [NeedsHeadlessWindowFact]
    public void Sharp_Scaling_At_One_And_A_Half_Covers_The_Screen_Within_A_Blend_Of_Nearest_And_Its_Neighbors()
    {
        using var folder = new TestFolder("window-sharp-half-");
        var app = Open(Path.Combine(folder.Path, "settings.txt"));
        Pattern(app);
        try
        {
            var (_, screen) = Fullscreen(app, width => width / 3 * 2, height => height / 3 * 2);
            var frame = Take(app, shown: false);
            Run(app, "window.scaling nearest");
            var nearest = Take(app, shown: true);
            Run(app, "window.scaling sharp");
            var sharp = Take(app, shown: true);
            var (x, y, width, height) = Placement.Of(frame.Width, frame.Height, screen.W, screen.H);
            (width == screen.W || height == screen.H).Should().BeTrue("the frame is as large as fits, {0} by {1} on a screen of {2} by {3}", width, height, screen.W, screen.H);
            (width / (double)frame.Width).Should().BeApproximately(1.5, 0.01);

            // The first screen pixel not as the scaling draws it, said, or null where none is.
            string? Wrong()
            {
                for (var sy = 0; sy < screen.H; sy++)
                    for (var sx = 0; sx < screen.W; sx++)
                    {
                        var inside = sx >= x && sx < x + width && sy >= y && sy < y + height;
                        if (Black(sharp, sx, sy) == inside) return $"the screen at {sx},{sy} is not {(inside ? "the frame's" : "black")}";
                        if (!inside) continue;
                        var (u, v) = ((sx + 0.5 - x) * frame.Width / width, (sy + 0.5 - y) * frame.Height / height);
                        var (tx, ty) = ((int)u, (int)v);
                        // A screen pixel whose middle is no nearer an edge of its frame pixel than
                        // half a screen pixel takes it whole.
                        if (Math.Abs(u - tx - 0.5) <= 0.5 - 0.5 * frame.Width / width + 1e-3 && Math.Abs(v - ty - 0.5) <= 0.5 - 0.5 * frame.Height / height + 1e-3
                            && !Same(frame, tx, ty, sharp, sx, sy))
                            return $"Sharp at {sx},{sy} is not the frame pixel {tx},{ty} whole, though its middle is inside it";
                        // A screen pixel whose middle is on the edge between two frame pixels
                        // along one axis and inside them along the other is an even blend of the
                        // two, so the blocks either side of it are of one width.
                        var (edgeX, edgeY) = (Math.Abs(u - Math.Round(u)) < 1e-3, Math.Abs(v - Math.Round(v)) < 1e-3);
                        var (ex, ey) = ((int)Math.Round(u), (int)Math.Round(v));
                        if (edgeX && !edgeY && !Even(sharp, sx, sy, frame, ex - 1, ty, ex, ty)
                            || edgeY && !edgeX && !Even(sharp, sx, sy, frame, tx, ey - 1, tx, ey))
                            return $"Sharp at {sx},{sy} is not an even blend of the frame pixels either side of the edge its middle is on";
                        // The frame pixel Nearest shows is the one under the screen pixel's middle
                        // or, where that falls on an edge, one beside it, and Sharp's pixel lies
                        // within the colors of a square of two by two frame pixels holding it, in
                        // each channel, a step over for the blend's rounding.
                        var (shown, held) = (false, false);
                        foreach (var (ox, oy) in Round)
                        {
                            if (!Same(frame, tx + ox, ty + oy, nearest, sx, sy)) continue;
                            shown = true;
                            foreach (var (cx, cy) in Squares) held |= Within(sharp, sx, sy, frame, tx + ox + cx, ty + oy + cy);
                            if (held) break;
                        }
                        if (!shown) return $"Nearest at {sx},{sy} shows none of the frame pixels round {tx},{ty}";
                        if (!held) return $"Sharp at {sx},{sy} is not a blend of the frame pixel Nearest shows and those beside it, round {tx},{ty}";
                    }
                return null;
            }
            Wrong().Should().BeNull();
        }
        finally
        {
            Reset(app);
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
