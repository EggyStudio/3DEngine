using SDL3;

namespace Engine;

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
