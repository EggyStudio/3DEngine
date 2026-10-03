using SDL3;

namespace Engine;

/// <summary>
/// Thin wrapper over SDL window and event loop with hooks for resize, quit, and raw event forwarding.
/// </summary>
/// <remarks>
/// Resize events are coalesced: if multiple <c>WindowResized</c> events arrive in
/// a single poll batch, only the final dimensions are dispatched - once - after the
/// batch drains, collapsing N resize callbacks per frame to at most 1.
/// </remarks>
/// <seealso cref="SdlWindow"/>
/// <seealso cref="WindowData"/>
public sealed class AppWindow
{
    /// <summary>The underlying SDL window/renderer pair wrapper.</summary>
    public SdlWindow Sdl { get; private set; }

    /// <summary>Delegate for window resize events.</summary>
    /// <param name="width">New window width in pixels.</param>
    /// <param name="height">New window height in pixels.</param>
    public delegate void ResizeEventHandler(int width, int height);
    /// <summary>Raised when this window is resized.</summary>
    public event ResizeEventHandler? ResizeEvent;

    /// <summary>Raised when an SDL Quit or this window is closed.</summary>
    public event Action? QuitEvent;

    /// <summary>Raised for every SDL event polled; allows input systems to consume events.</summary>
    public event Action<SDL.Event>? SDLEvent;

    private volatile bool _shouldClose;

    /// <summary>Creates a new window with the specified properties and graphics backend.</summary>
    /// <param name="windowData">Window title and size.</param>
    /// <param name="backend">Graphics backend to use.</param>
    public AppWindow(WindowData windowData, GraphicsBackend backend)
    {
        var useVulkan = backend == GraphicsBackend.Vulkan;
        Sdl = new(windowData.Title, windowData.Width, windowData.Height, useVulkan);
    }

    /// <summary>Creates a new window with the specified properties using the SDL software renderer.</summary>
    /// <param name="windowData">Window title and size.</param>
    public AppWindow(WindowData windowData) : this(windowData, GraphicsBackend.Sdl) {}

    /// <summary>Returns true if this window has keyboard focus.</summary>
    public bool IsFocused()
    {
        return SDL.GetKeyboardFocus() == Sdl.Window;
    }

    /// <summary>Shows the window and applies an initial window command (maximize, minimize, etc.).</summary>
    /// <param name="command">The window command to apply after showing.</param>
    public void Show(WindowCommand command = WindowCommand.Normal)
    {
        SDL.ShowWindow(Sdl.Window);
        switch (command)
        {
            case WindowCommand.Maximize:
                SDL.MaximizeWindow(Sdl.Window);
                break;
            case WindowCommand.Minimize:
                SDL.MinimizeWindow(Sdl.Window);
                break;
            case WindowCommand.Restore:
                SDL.RestoreWindow(Sdl.Window);
                break;
            case WindowCommand.Hide:
                SDL.HideWindow(Sdl.Window);
                break;
            default:
                break;
        }
    }

    /// <summary>Requests the main loop to exit after the current iteration.</summary>
    public void RequestClose() => _shouldClose = true;

    /// <summary>Pumps SDL events and calls the supplied per-frame delegates until quit is requested.</summary>
    /// <param name="onFrame">Delegates invoked once each frame iteration.</param>
    /// <remarks>
    /// Resize events are coalesced: if multiple <c>WindowResized</c> events arrive in
    /// a single poll batch, only the final dimensions are dispatched - once - after the
    /// batch drains.  This collapses N resize callbacks per frame to at most 1.
    /// </remarks>
    public void Looping(params Delegate[] onFrame)
    {
        bool running = true;
        while (running)
        {
            if (_shouldClose) running = false;

            // -- Coalesced resize state for this poll batch --
            bool resizedThisBatch = false;
            int coalescedW = 0, coalescedH = 0;

            while (SDL.PollEvent(out var e))
            {
                SDLEvent?.Invoke(e);

                var evtType = (SDL.EventType)e.Type;

                if (evtType == SDL.EventType.Quit)
                {
                    QuitEvent?.Invoke();
                    running = false;
                }
                if (evtType == SDL.EventType.WindowCloseRequested
                    && e.Window.WindowID == SDL.GetWindowID(Sdl.Window))
                {
                    QuitEvent?.Invoke();
                    running = false;
                }
                if (evtType == SDL.EventType.WindowResized
                    && e.Window.WindowID == SDL.GetWindowID(Sdl.Window))
                {
                    // Update display scale (may change if window moved between monitors).
                    float resizeScale = SDL.GetWindowDisplayScale(Sdl.Window);
                    if (resizeScale <= 0f) resizeScale = 1f;
                    Sdl.DisplayScale = resizeScale;

                    // GetWindowSize returns logical coordinates on native Wayland,
                    // macOS, and iOS - but physical pixels on Windows, X11 (XWayland),
                    // and Android.
                    SDL.GetWindowSize(Sdl.Window, out int rawW, out int rawH);
                    if (Sdl.NeedsManualHiDpiScaling && resizeScale > 1.001f)
                    {
                        coalescedW = (int)(rawW / resizeScale);
                        coalescedH = (int)(rawH / resizeScale);
                    }
                    else
                    {
                        coalescedW = rawW;
                        coalescedH = rawH;
                    }
                    resizedThisBatch = true;
                }
            }

            // -- Dispatch the single coalesced resize (if any) --
            if (resizedThisBatch && coalescedW > 0 && coalescedH > 0)
            {
                Sdl.Width = coalescedW;
                Sdl.Height = coalescedH;
                ResizeEvent?.Invoke(coalescedW, coalescedH);
            }

            foreach (var frame in onFrame)
                frame?.DynamicInvoke();
        }
    }

    /// <summary>Disposes the underlying SDL resources, optionally invoking a callback before return.</summary>
    /// <param name="onDispose">Optional callback invoked after SDL resources are destroyed.</param>
    public void Dispose(Action? onDispose)
    {
        Sdl.Destroy();
        onDispose?.Invoke();
    }
}