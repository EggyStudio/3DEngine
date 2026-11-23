using SDL3;

namespace Engine;

/// <summary>
/// Owns an SDL window and optional software renderer, providing lifecycle management and HiDPI support.
/// </summary>
/// <remarks>
/// When <c>useVulkan</c> is <c>true</c> on construction, no SDL renderer is created -
/// only the window handle is available for Vulkan surface creation.
/// HiDPI coordinate-mode detection is performed at startup to determine whether manual scaling is needed.
/// </remarks>
/// <seealso cref="AppWindow"/>
/// <seealso cref="SdlSurfaceSource"/>
internal sealed class SdlWindow
{
    /// <summary>Window title.</summary>
    public string Title { get; private set; }

    /// <summary>Window width in logical pixels.</summary>
    public int Width { get; internal set; }

    /// <summary>Window height in logical pixels.</summary>
    public int Height { get; internal set; }

    /// <summary>Native SDL window handle.</summary>
    public nint Window { get; private set; }

    /// <summary>Native SDL renderer handle (<c>IntPtr.Zero</c> in Vulkan mode).</summary>
    public nint Renderer { get; private set; }

    /// <summary>Display scale factor (e.g. 1.25 for 125% HiDPI). Always ≥ 1.</summary>
    public float DisplayScale { get; internal set; } = 1f;

    /// <summary>
    /// True when the SDL video backend uses physical-pixel coordinates for
    /// <c>CreateWindow</c>/<c>GetWindowSize</c>/<c>SetWindowSize</c> instead of logical
    /// coordinates.  Windows, X11 (including XWayland), and Android use physical
    /// pixels; native Wayland, macOS, and iOS use logical coordinates.
    /// Detected at runtime by comparing window size to backing pixel size.
    /// </summary>
    public bool NeedsManualHiDpiScaling { get; private set; }

    /// <summary>
    /// The size in the window's units the program made the window at, which a native
    /// <c>window.resolution</c> gives it as a window.
    /// </summary>
    public (int Width, int Height) Made { get; }

    /// <summary>Window size as a (Width, Height) tuple.</summary>
    public (int W, int H) Size => (Width, Height);

    /// <summary>
    /// The picture the scene is drawn as and how it stands on the window, which the render settings
    /// and <c>E3D.SetPicture</c> set, the window made again (<see cref="AppWindow.Repicture"/>)
    /// as it changes.
    /// </summary>
    public Picture Picture { get; internal set; } = new();

    /// <summary>The window's size in its pixels, which the images it shows are made at.</summary>
    public (int Width, int Height) Pixels
    {
        get
        {
            if (SDL.GetWindowSizeInPixels(Window, out var width, out var height) && width > 0 && height > 0) return (width, height);
            SDL.GetWindowSize(Window, out width, out height);
            return (Math.Max(1, width), Math.Max(1, height));
        }
    }

    /// <summary>
    /// The size in pixels the scene is drawn at, the picture's for the window's pixels.
    /// </summary>
    public (int Width, int Height) FramePixels => PictureSize.Of(Picture, Pixels);

    /// <summary>Where the picture stands on the window in its pixels.</summary>
    public (int X, int Y, int Width, int Height) Placed => PictureSize.Placed(PictureSize.Bars(Picture), FramePixels, Pixels);

    /// <summary>
    /// A point of the window in its own units, as SDL's events and the interface give it, as the
    /// share of the picture across and down at it, from 0 to 1 over the picture and outside that
    /// over the black round it.
    /// </summary>
    public (float X, float Y) ToPicture(float x, float y)
    {
        var (density, placed) = (Density, Placed);
        return ((x * density - placed.X) / placed.Width, (y * density - placed.Y) / placed.Height);
    }

    /// <summary>
    /// A share of the picture across and down as the point of the window over it, in its own units.
    /// </summary>
    public (float X, float Y) FromPicture(float x, float y)
    {
        var (density, placed) = (Density, Placed);
        return ((x * placed.Width + placed.X) / density, (y * placed.Height + placed.Y) / density);
    }

    /// <summary>
    /// Where the picture stands on the window in its own units, as SDL's events and the interface
    /// give them.
    /// </summary>
    public (float X, float Y, float Width, float Height) PictureArea
    {
        get
        {
            var (x, y) = FromPicture(0, 0);
            var (right, bottom) = FromPicture(1, 1);
            return (x, y, right - x, bottom - y);
        }
    }

    // The window's pixels to one of its units.
    private float Density
    {
        get
        {
            SDL.GetWindowSize(Window, out var width, out _);
            return width > 0 ? Pixels.Width / (float)width : 1f;
        }
    }

    /// <summary>
    /// Starts SDL's video through <paramref name="start"/>, on Linux asking for X11 first through
    /// <paramref name="hint"/>, and where that start fails, asking again with the hint cleared
    /// through <paramref name="clear"/>, which <paramref name="fellBack"/> then says. A driver
    /// <paramref name="named"/> already, through the hint or <c>SDL_VIDEO_DRIVER</c>, is started on
    /// as it is named, with nothing asked.
    /// </summary>
    /// <remarks>
    /// Under a Wayland session SDL draws the window's frame itself through libdecor, whose GTK
    /// plugin, where it is installed, draws a frame of its own inside the window, its top corners
    /// rounded, its bottom ones square and its shadow its own, unlike the frames the desktop draws
    /// for its other windows. Through X11, under XWayland, the desktop draws the frame. A user who
    /// wants Wayland names it in <c>SDL_VIDEO_DRIVER</c>, and a session without XWayland, where X11
    /// cannot start, starts on what SDL finds. A driver already named, as SDL's offscreen driver
    /// named through the hint for a test, is started with nothing asked, since asking for X11
    /// overwrites the name, and on a machine with no display, where X11 does not start, clearing
    /// the hint then leaves no driver named, so SDL finds none.
    /// </remarks>
    internal static bool StartVideo(bool linux, string? named, Action<string> hint, Action clear, Func<bool> start, out bool fellBack)
    {
        fellBack = false;
        if (!string.IsNullOrEmpty(named)) return start();
        if (linux) hint("x11");
        if (start()) return true;
        if (!linux) return false;
        clear();
        fellBack = true;
        return start();
    }

    /// <summary>Creates a new SDL window. If useVulkan is true, no SDL renderer is created and the window uses the Vulkan flag.</summary>
    /// <param name="title">The window's title.</param>
    /// <param name="width">The window's width.</param>
    /// <param name="height">The window's height.</param>
    /// <param name="useVulkan">Whether the window is drawn into through Vulkan rather than SDL's renderer.</param>
    /// <param name="flags">SDL's window flags asked beside those the engine sets, as high pixel density or transparency.</param>
    public SdlWindow(string title, int width, int height, bool useVulkan = false, SDL.WindowFlags flags = 0)
    {
        var logger = Log.Category("Engine.Application");
        Title = title;
        Width = width;
        Height = height;
        Made = (width, height);

        logger.Info($"Initializing SDL3 (Video + Gamepad subsystems)...");
        if (!StartVideo(OperatingSystem.IsLinux(), SDL.GetHint(SDL.Hints.VideoDriver), driver => SDL.SetHint(SDL.Hints.VideoDriver, driver), () => SDL.ResetHint(SDL.Hints.VideoDriver),
                () => SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad), out var fellBack))
        {
            var err = SDL.GetError();
            logger.Error($"SDL initialization failed. {err}");
            SDL.LogError(SDL.LogCategory.System, $"SDL could not initialize: {err}");
            throw new InvalidOperationException($"SDL init failed. {err}");
        }
        logger.Info($"SDL3 initialized successfully, its video driver {SDL.GetCurrentVideoDriver()}{(fellBack ? ", since X11 did not start" : "")}.");

        if (useVulkan)
        {
            logger.Info($"Creating SDL Vulkan window: \"{title}\" ({width}x{height})...");
            var window = SDL.CreateWindow(title, width, height, flags | SDL.WindowFlags.Resizable | SDL.WindowFlags.Vulkan | SDL.WindowFlags.Hidden);
            if (window == IntPtr.Zero)
            {
                var err = SDL.GetError();
                logger.Error($"Vulkan window creation failed. {err}");
                SDL.LogError(SDL.LogCategory.Application, $"Error creating Vulkan window: {err}");
                throw new InvalidOperationException($"SDL Vulkan window creation failed. {err}");
            }
            Window = window;
            Renderer = IntPtr.Zero; // no SDL renderer in Vulkan mode
            logger.Info($"SDL Vulkan window created (handle=0x{window:X}).");
        }
        else
        {
            logger.Info($"Creating SDL window + software renderer: \"{title}\" ({width}x{height})...");
            if (!SDL.CreateWindowAndRenderer(title, width, height, flags | SDL.WindowFlags.Resizable | SDL.WindowFlags.Hidden, out var window, out var renderer))
            {
                var err = SDL.GetError();
                logger.Error($"Window/renderer creation failed. {err}");
                SDL.LogError(SDL.LogCategory.Application, $"Error creating window and renderer: {err}");
                throw new InvalidOperationException($"SDL window/renderer creation failed. {err}");
            }
            Window = window;
            Renderer = renderer;
            logger.Info($"SDL window created (handle=0x{window:X}), renderer (handle=0x{renderer:X}).");
        }

        // -- HiDPI handling
        // By the SDL3 spec, CreateWindow / GetWindowSize / SetWindowSize use *logical*
        // coordinates on native Wayland, macOS, and iOS.  Windows, X11 (including
        // XWayland) and Android are the exceptions, working in physical pixels.
        //
        // Rather than matching on video-driver name strings (brittle), we detect
        // the coordinate mode at runtime by comparing the window size against the
        // backing pixel size.  When the two differ the platform uses logical
        // coordinates and no manual workaround is needed; when they are equal the
        // platform uses physical pixels and we must scale the window ourselves.
        string videoDriver = SDL.GetCurrentVideoDriver() ?? "";

        float scale = SDL.GetWindowDisplayScale(Window);
        if (scale <= 0f) scale = 1f;
        DisplayScale = scale;

        SDL.GetWindowSize(Window, out int winW, out int winH);
        SDL.GetWindowSizeInPixels(Window, out int pxW, out int pxH);
        NeedsManualHiDpiScaling = (winW == pxW && winH == pxH) && scale > 1.001f;

        logger.Info($"SDL video driver: \"{videoDriver}\", display scale: {scale:F2}, " +
                    $"window size: {winW}x{winH}, pixel size: {pxW}x{pxH}, " +
                    $"manual HiDPI scaling: {NeedsManualHiDpiScaling}");

        if (NeedsManualHiDpiScaling && scale > 1.001f)
        {
            // CreateWindow used physical pixels, so a 1280×720 window
            // appears small on a 2× display.  Scale it up so the window
            // fills the intended logical area on screen.
            int scaledW = (int)(width * scale);
            int scaledH = (int)(height * scale);
            SDL.SetWindowSize(Window, scaledW, scaledH);
            logger.Info($"Manual HiDPI Scaling: window scaled to {scaledW}x{scaledH}, " +
                        $"content resolution: {width}x{height}");
        }
        else
        {
            logger.Info($"Window created: {width}x{height}");
        }
    }

    /// <summary>Destroys the SDL renderer and window and quits SDL.</summary>
    public void Destroy()
    {
        var logger = Log.Category("Engine.Application");
        logger.Info("Destroying SDL window and renderer...");
        if (Renderer != IntPtr.Zero)
        {
            SDL.DestroyRenderer(Renderer);
            Renderer = IntPtr.Zero;
        }
        if (Window != IntPtr.Zero)
        {
            SDL.DestroyWindow(Window);
            Window = IntPtr.Zero;
        }
        SDL.Quit();
        logger.Info("SDL window destroyed and SDL subsystems shut down.");
    }
}