using SDL3;

namespace Engine;

/// <summary>A size and refresh rate a monitor can be driven at in fullscreen.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="RefreshRate">The refresh rate in hertz, rounded, or 0 when it is not known.</param>
public readonly record struct MonitorMode(int Width, int Height, int RefreshRate);

public static partial class Engine3D
{
    // -- Window state. With no window (a headless or offscreen run) these do nothing and the
    // queries answer as a window of the size asked for would.

    private static bool _resized;
    private static (int Width, int Height) _sizeBeforeEvents;

    private static nint WindowHandle => _app?.World.TryGetResource<AppWindow>(out var window) == true ? window.Sdl.Window : 0;

    private static bool HasWindowFlag(SDL.WindowFlags flag) => WindowHandle is not 0 and var w && (SDL.GetWindowFlags(w) & flag) != 0;

    /// <summary>Whether the window's size changed in the events of this frame.</summary>
    public static bool IsWindowResized() => _resized;

    /// <summary>
    /// Whether the window covers its monitor in fullscreen, by <see cref="ToggleFullscreen"/> or
    /// the flag for it, rather than as <see cref="ToggleBorderlessWindowed"/> covers it, as raylib's
    /// tells the two apart.
    /// </summary>
    public static bool IsWindowFullscreen() => HasWindowFlag(SDL.WindowFlags.Fullscreen) && !_borderless;

    /// <summary>Whether the window is minimized.</summary>
    public static bool IsWindowMinimized() => HasWindowFlag(SDL.WindowFlags.Minimized);

    /// <summary>Whether the window is maximized.</summary>
    public static bool IsWindowMaximized() => HasWindowFlag(SDL.WindowFlags.Maximized);

    /// <summary>Whether the window has the keyboard focus.</summary>
    public static bool IsWindowFocused() => HasWindowFlag(SDL.WindowFlags.InputFocus);

    /// <summary>Whether the window is hidden, as it is in a <c>--hidden</c> run.</summary>
    public static bool IsWindowHidden() => WindowHandle == 0 || HasWindowFlag(SDL.WindowFlags.Hidden);

    /// <summary>Switches the window between fullscreen on its monitor and a window.</summary>
    /// <remarks>
    /// Fullscreen keeps the monitor's desktop mode, unless <see cref="SetWindowFullscreenMode"/>
    /// has set one of its own.
    /// </remarks>
    public static void ToggleFullscreen()
    {
        if (WindowHandle is not 0 and var w)
        {
            var on = !IsWindowFullscreen();
            _borderless = false;
            SDL.SetWindowFullscreen(w, on);
        }
    }

    /// <summary>Maximizes the window.</summary>
    public static void MaximizeWindow()
    {
        if (WindowHandle is not 0 and var w) SDL.MaximizeWindow(w);
    }

    /// <summary>Minimizes the window.</summary>
    public static void MinimizeWindow()
    {
        if (WindowHandle is not 0 and var w) SDL.MinimizeWindow(w);
    }

    /// <summary>Restores a minimized or maximized window to its size before.</summary>
    public static void RestoreWindow()
    {
        if (WindowHandle is not 0 and var w) SDL.RestoreWindow(w);
    }

    /// <summary>Resizes the window, or the images an offscreen run draws into. The frames after it are drawn at the new size.</summary>
    public static void SetWindowSize(int width, int height)
    {
        if (WindowHandle is not 0 and var w) SDL.SetWindowSize(w, Math.Max(1, width), Math.Max(1, height));
        else if (TryRes<OffscreenSurface>(out var surface))
        {
            surface.Size = ((uint)Math.Max(1, width), (uint)Math.Max(1, height));
            if (TryRes<SurfaceResize>(out var resize)) resize.Request(Math.Max(1, width), Math.Max(1, height));
        }
    }

    /// <summary>Sets the smallest size the window can be resized to.</summary>
    public static void SetWindowMinSize(int width, int height)
    {
        if (WindowHandle is not 0 and var w) SDL.SetWindowMinimumSize(w, Math.Max(0, width), Math.Max(0, height));
    }

    /// <summary>Sets the largest size the window can be resized to.</summary>
    public static void SetWindowMaxSize(int width, int height)
    {
        if (WindowHandle is not 0 and var w) SDL.SetWindowMaximumSize(w, Math.Max(0, width), Math.Max(0, height));
    }

    /// <summary>Moves the window's top left corner to a position on the desktop.</summary>
    public static void SetWindowPosition(int x, int y)
    {
        if (WindowHandle is not 0 and var w) SDL.SetWindowPosition(w, x, y);
    }

    /// <summary>The window's top left corner on the desktop, or zero with no window.</summary>
    public static System.Numerics.Vector2 GetWindowPosition()
    {
        if (WindowHandle is not 0 and var w && SDL.GetWindowPosition(w, out var x, out var y)) return new(x, y);
        return System.Numerics.Vector2.Zero;
    }

    // -- Monitors

    /// <summary>How many monitors are connected, or 0 with no video.</summary>
    public static int GetMonitorCount() => Displays().Length;

    /// <summary>The index of the monitor the window is on, or 0.</summary>
    public static int GetCurrentMonitor()
    {
        if (WindowHandle is not 0 and var w)
            return Math.Max(0, Array.IndexOf(Displays(), SDL.GetDisplayForWindow(w)));
        return 0;
    }

    /// <summary>A monitor's width in pixels, in its current mode, or 0 when there is no such monitor.</summary>
    public static int GetMonitorWidth(int monitor) => Mode(monitor) is { } mode ? mode.W : 0;

    /// <summary>A monitor's height in pixels, in its current mode, or 0 when there is no such monitor.</summary>
    public static int GetMonitorHeight(int monitor) => Mode(monitor) is { } mode ? mode.H : 0;

    /// <summary>A monitor's width in millimeters, as raylib's SDL backend works it out, or 0 when there is no such monitor.</summary>
    /// <remarks>
    /// SDL3 gives no monitor's own size, so it is the width in pixels at 96 pixels an inch times
    /// the window's display scale, as raylib's SDL3 backend reckons it.
    /// </remarks>
    public static int GetMonitorPhysicalWidth(int monitor) => Mode(monitor) is { } mode ? Millimeters(mode.W, GetWindowScaleDPI().X) : 0;

    /// <summary>A monitor's height in millimeters, as <see cref="GetMonitorPhysicalWidth"/> works it out.</summary>
    public static int GetMonitorPhysicalHeight(int monitor) => Mode(monitor) is { } mode ? Millimeters(mode.H, GetWindowScaleDPI().X) : 0;

    // Pixels in millimeters at a display scale times 96 pixels an inch.
    internal static int Millimeters(int pixels, float scale) => (int)(pixels / (scale * 96.0f) * 25.4f);

    /// <summary>A monitor's refresh rate in hertz, rounded, or 0 when it is not known.</summary>
    public static int GetMonitorRefreshRate(int monitor) => Mode(monitor) is { } mode ? (int)MathF.Round(mode.RefreshRate) : 0;

    /// <summary>A monitor's name, or an empty string when there is no such monitor.</summary>
    public static string GetMonitorName(int monitor) =>
        Displays() is var displays && monitor >= 0 && monitor < displays.Length ? SDL.GetDisplayName(displays[monitor]) ?? "" : "";

    /// <summary>The modes a monitor can be set to in fullscreen, largest first, or none when there is no such monitor.</summary>
    /// <remarks>Modes that differ only in pixel format or density are listed once.</remarks>
    public static MonitorMode[] GetMonitorModes(int monitor)
    {
        var displays = Displays();
        if (monitor < 0 || monitor >= displays.Length) return [];
        var modes = SDL.GetFullscreenDisplayModes(displays[monitor], out _);
        return modes is null ? [] : DistinctModes(modes.Select(m => (m.W, m.H, m.RefreshRate)));
    }

    /// <summary>Modes rounded to whole hertz, once each, in the order given.</summary>
    internal static MonitorMode[] DistinctModes(IEnumerable<(int Width, int Height, float RefreshRate)> modes) =>
        modes.Select(m => new MonitorMode(m.Width, m.Height, (int)MathF.Round(m.RefreshRate))).Distinct().ToArray();

    /// <summary>
    /// Makes the window fullscreen on its monitor in the mode closest to <paramref name="mode"/>,
    /// changing the monitor's resolution, or at the desktop's mode when it is the default.
    /// </summary>
    /// <remarks>
    /// The frames after it are drawn at the mode's size. A mode is kept for later calls to
    /// <see cref="ToggleFullscreen"/>, and the default one returns them to the desktop's mode.
    /// </remarks>
    public static void SetWindowFullscreenMode(MonitorMode mode)
    {
        if (WindowHandle is not (not 0 and var w)) return;
        if (mode == default)
        {
            SDL.SetWindowFullscreenMode(w, IntPtr.Zero);
        }
        else
        {
            var display = SDL.GetDisplayForWindow(w);
            if (!SDL.GetClosestFullscreenDisplayMode(display, mode.Width, mode.Height, mode.RefreshRate, false, out var closest))
            {
                ApiLogger.Warn($"SetWindowFullscreenMode: the monitor has no mode near {mode.Width}x{mode.Height}: {SDL.GetError()}");
                return;
            }
            SDL.SetWindowFullscreenMode(w, closest);
        }
        SDL.SetWindowFullscreen(w, true);
    }

    /// <summary>Moves the window to a monitor, centered on it.</summary>
    /// <remarks>
    /// A window fullscreen at the desktop's mode goes fullscreen on the other monitor. One in a mode
    /// of its own stays, since SDL ties that mode to its monitor.
    /// </remarks>
    public static void SetWindowMonitor(int monitor)
    {
        var displays = Displays();
        if (WindowHandle is not (not 0 and var w) || monitor < 0 || monitor >= displays.Length) return;
        var centered = (int)(SDL.WindowPosCenteredMask | displays[monitor]);
        SDL.SetWindowPosition(w, centered, centered);
    }

    private static uint[] Displays() => SDL.WasInit(SDL.InitFlags.Video) != 0 ? SDL.GetDisplays(out _) ?? [] : [];

    private static SDL.DisplayMode? Mode(int monitor)
    {
        var displays = Displays();
        if (monitor < 0 || monitor >= displays.Length) return null;
        return SDL.GetCurrentDisplayMode(displays[monitor]);
    }

    // -- Clipboard

    /// <summary>Sets how opaque the window is, from 0 (clear) to 1, where the desktop allows it.</summary>
    public static void SetWindowOpacity(float opacity)
    {
        if (WindowHandle is not 0 and var w) SDL.SetWindowOpacity(w, Math.Clamp(opacity, 0f, 1f));
    }

    /// <summary>Raises the window and asks for the keyboard focus, which the desktop may refuse.</summary>
    public static void SetWindowFocused()
    {
        if (WindowHandle is not 0 and var w) SDL.RaiseWindow(w);
    }

    /// <summary>The width in pixels the window's content is drawn at, which on a doubled monitor is twice <see cref="GetScreenWidth"/>.</summary>
    public static int GetRenderWidth() => RenderSize().Width;

    /// <summary>The height in pixels the window's content is drawn at.</summary>
    public static int GetRenderHeight() => RenderSize().Height;

    private static (int Width, int Height) RenderSize()
    {
        if (WindowHandle is not 0 and var w && SDL.GetWindowSizeInPixels(w, out var width, out var height)) return (width, height);
        return (GetScreenWidth(), GetScreenHeight());
    }

    /// <summary>A monitor's top left corner on the desktop, or zero when there is no such monitor.</summary>
    public static System.Numerics.Vector2 GetMonitorPosition(int monitor)
    {
        var displays = Displays();
        if (monitor < 0 || monitor >= displays.Length || !SDL.GetDisplayBounds(displays[monitor], out var bounds)) return System.Numerics.Vector2.Zero;
        return new System.Numerics.Vector2(bounds.X, bounds.Y);
    }

    /// <summary>
    /// Switches between a window and a borderless one covering the monitor at its desktop mode,
    /// which changes no display mode and so switches at once.
    /// </summary>
    public static void ToggleBorderlessWindowed()
    {
        if (WindowHandle is not 0 and var w)
        {
            // From fullscreen it goes to borderless, as raylib's leaves fullscreen first.
            if (_borderless && HasWindowFlag(SDL.WindowFlags.Fullscreen))
            {
                SDL.SetWindowFullscreen(w, false);
                _borderless = false;
            }
            else
            {
                SDL.SetWindowFullscreenMode(w, IntPtr.Zero);
                SDL.SetWindowFullscreen(w, true);
                _borderless = true;
            }
        }
    }

    /// <summary>How many pixels the window's content has for each unit of its size, 1 on most monitors and 2 on a doubled one.</summary>
    public static System.Numerics.Vector2 GetWindowScaleDPI()
    {
        var scale = WindowHandle is not 0 and var w ? SDL.GetWindowDisplayScale(w) : 1f;
        return new System.Numerics.Vector2(scale > 0 ? scale : 1f);
    }

    /// <summary>Sets the window's icon from an image, which the desktop scales to what it shows.</summary>
    public static unsafe void SetWindowIcon(Image image)
    {
        if (WindowHandle is not (not 0 and var w) || !image.IsValid) return;
        fixed (byte* pixels = image.Data)
        {
            // ABGR8888 is the bytes R, G, B and A in order on a little-endian machine, as an image holds them.
            var surface = SDL.CreateSurfaceFrom(image.Width, image.Height, SDL.PixelFormat.ABGR8888, (IntPtr)pixels, image.Width * 4);
            if (surface == IntPtr.Zero)
            {
                ApiLogger.Warn($"SetWindowIcon: the image could not be made a surface: {SDL.GetError()}");
                return;
            }
            SDL.SetWindowIcon(w, surface);
            SDL.DestroySurface(surface);
        }
    }

    /// <summary>
    /// Sets the window's icon from several sizes of one picture, the first the size it is drawn at
    /// most and the rest alternates the desktop picks from where it draws the icon larger or smaller.
    /// </summary>
    public static unsafe void SetWindowIcons(Image[] images)
    {
        if (WindowHandle is not (not 0 and var w) || images is not { Length: > 0 } || !images[0].IsValid) return;
        var surfaces = new List<IntPtr>();
        var pins = new List<System.Runtime.InteropServices.GCHandle>();
        try
        {
            foreach (var image in images.Where(i => i.IsValid))
            {
                var pin = System.Runtime.InteropServices.GCHandle.Alloc(image.Data, System.Runtime.InteropServices.GCHandleType.Pinned);
                pins.Add(pin);
                var surface = SDL.CreateSurfaceFrom(image.Width, image.Height, SDL.PixelFormat.ABGR8888, pin.AddrOfPinnedObject(), image.Width * 4);
                if (surface == IntPtr.Zero) continue;
                surfaces.Add(surface);
            }
            if (surfaces.Count == 0)
            {
                ApiLogger.Warn($"SetWindowIcons: no image could be made a surface: {SDL.GetError()}");
                return;
            }
            for (int i = 1; i < surfaces.Count; i++) SDL.AddSurfaceAlternateImage(surfaces[0], surfaces[i]);
            SDL.SetWindowIcon(w, surfaces[0]);
        }
        finally
        {
            foreach (var surface in surfaces) SDL.DestroySurface(surface);
            foreach (var pin in pins) pin.Free();
        }
    }

    /// <summary>The window's SDL window, for a library of the program's own that needs it, or zero with no window.</summary>
    public static nint GetWindowHandle() => WindowHandle;

    /// <summary>An image on the system clipboard, as a PNG, JPEG or BMP copied there, or an invalid image when it holds none.</summary>
    public static unsafe Image GetClipboardImage()
    {
        if (SDL.WasInit(SDL.InitFlags.Video) == 0) return default;
        foreach (var type in new[] { "image/png", "image/jpeg", "image/bmp" })
        {
            if (!SDL.HasClipboardData(type)) continue;
            var data = SDL.GetClipboardData(type, out var size);
            if (data == IntPtr.Zero || size == 0) continue;
            var bytes = new ReadOnlySpan<byte>((void*)data, checked((int)size)).ToArray();
            SDL.Free(data);
            return LoadImageFromMemory(type, bytes);
        }
        return default;
    }

    /// <summary>Puts text on the system clipboard.</summary>
    public static void SetClipboardText(string text)
    {
        if (SDL.WasInit(SDL.InitFlags.Video) != 0) SDL.SetClipboardText(text);
    }

    /// <summary>The text on the system clipboard, or an empty string.</summary>
    public static string GetClipboardText() => SDL.WasInit(SDL.InitFlags.Video) != 0 ? SDL.GetClipboardText() ?? "" : "";
}
