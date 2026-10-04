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

    /// <summary>Whether the window covers its monitor in fullscreen.</summary>
    public static bool IsWindowFullscreen() => HasWindowFlag(SDL.WindowFlags.Fullscreen);

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
        if (WindowHandle is not 0 and var w) SDL.SetWindowFullscreen(w, !IsWindowFullscreen());
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

    /// <summary>Resizes the window. The frames after it are drawn at the new size.</summary>
    public static void SetWindowSize(int width, int height)
    {
        if (WindowHandle is not 0 and var w) SDL.SetWindowSize(w, Math.Max(1, width), Math.Max(1, height));
    }

    /// <summary>Sets the smallest size the window can be resized to.</summary>
    public static void SetWindowMinSize(int width, int height)
    {
        if (WindowHandle is not 0 and var w) SDL.SetWindowMinimumSize(w, Math.Max(0, width), Math.Max(0, height));
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

    /// <summary>Puts text on the system clipboard.</summary>
    public static void SetClipboardText(string text)
    {
        if (SDL.WasInit(SDL.InitFlags.Video) != 0) SDL.SetClipboardText(text);
    }

    /// <summary>The text on the system clipboard, or an empty string.</summary>
    public static string GetClipboardText() => SDL.WasInit(SDL.InitFlags.Video) != 0 ? SDL.GetClipboardText() ?? "" : "";
}
