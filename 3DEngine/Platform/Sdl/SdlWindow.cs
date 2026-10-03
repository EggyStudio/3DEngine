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
public sealed class SdlWindow
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

    /// <summary>Window size as a (Width, Height) tuple.</summary>
    public (int W, int H) Size => (Width, Height);

    /// <summary>Creates a new SDL window. If useVulkan is true, no SDL renderer is created and the window uses the Vulkan flag.</summary>
    public SdlWindow(string title, int width, int height, bool useVulkan = false)
    {
        var logger = Log.Category("Engine.Application");
        Title = title;
        Width = width;
        Height = height;

        logger.Info($"Initializing SDL3 (Video + Gamepad subsystems)...");
        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad))
        {
            var err = SDL.GetError();
            logger.Error($"SDL initialization failed: {err}");
            SDL.LogError(SDL.LogCategory.System, $"SDL could not initialize: {err}");
            throw new InvalidOperationException($"SDL init failed: {err}");
        }
        logger.Info("SDL3 initialized successfully.");

        if (useVulkan)
        {
            logger.Info($"Creating SDL Vulkan window: \"{title}\" ({width}x{height})...");
            var flags = SDL.WindowFlags.Resizable | SDL.WindowFlags.Vulkan | SDL.WindowFlags.Hidden;
            var window = SDL.CreateWindow(title, width, height, flags);
            if (window == IntPtr.Zero)
            {
                var err = SDL.GetError();
                logger.Error($"Vulkan window creation failed: {err}");
                SDL.LogError(SDL.LogCategory.Application, $"Error creating Vulkan window: {err}");
                throw new InvalidOperationException($"SDL Vulkan window creation failed: {err}");
            }
            Window = window;
            Renderer = IntPtr.Zero; // no SDL renderer in Vulkan mode
            logger.Info($"SDL Vulkan window created (handle=0x{window:X}).");
        }
        else
        {
            logger.Info($"Creating SDL window + software renderer: \"{title}\" ({width}x{height})...");
            if (!SDL.CreateWindowAndRenderer(title, width, height, SDL.WindowFlags.Resizable | SDL.WindowFlags.Hidden, out var window, out var renderer))
            {
                var err = SDL.GetError();
                logger.Error($"Window/renderer creation failed: {err}");
                SDL.LogError(SDL.LogCategory.Application, $"Error creating window and renderer: {err}");
                throw new InvalidOperationException($"SDL window/renderer creation failed: {err}");
            }
            Window = window;
            Renderer = renderer;
            logger.Info($"SDL window created (handle=0x{window:X}), renderer (handle=0x{renderer:X}).");
        }

        // -- HiDPI handling --
        // SDL3 spec: CreateWindow / GetWindowSize / SetWindowSize use *logical*
        // coordinates on native Wayland, macOS, and iOS.  Windows, X11 (including
        // XWayland), and Android are the exceptions - they work in physical pixels.
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