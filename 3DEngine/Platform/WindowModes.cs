using System.Runtime.CompilerServices;
using SDL3;

namespace Engine;

/// <summary>How a window covers its screen, as the <c>window.mode</c> setting takes it.</summary>
internal enum WindowMode
{
    /// <summary>A window of its own size among the desktop's others.</summary>
    Windowed,

    /// <summary>A window with no border covering its screen, the screen's mode unchanged.</summary>
    Borderless,

    /// <summary>The screen taken over at the screen's own mode.</summary>
    Fullscreen,
}

/// <summary>
/// Reads and sets how a window covers its screen, moves it to another screen, and keeps the size it
/// has as a window while it covers one, which the window's settings and the flat API share.
/// </summary>
internal static class WindowModes
{
    // The size each window has as a window, kept while it covers its screen.
    private static readonly ConditionalWeakTable<AppWindow, Box> Sizes = [];

    private sealed class Box
    {
        public (int Width, int Height) Size;
    }

    /// <summary>How the window covers its screen, as SDL holds it.</summary>
    /// <remarks>
    /// SDL holds no mode for a borderless window and the screen's mode, or one set, for a
    /// fullscreen one.
    /// </remarks>
    internal static WindowMode Of(IntPtr window)
    {
        if ((SDL.GetWindowFlags(window) & SDL.WindowFlags.Fullscreen) == 0) return WindowMode.Windowed;
        return SDL.GetWindowFullscreenMode(window) is null ? WindowMode.Borderless : WindowMode.Fullscreen;
    }

    /// <summary>
    /// Sets how the window covers its screen and waits for the system to have done it, or says why
    /// the system refused it, the mode before kept.
    /// </summary>
    /// <remarks>
    /// A window going back to being a window takes the size kept for it. A window not yet shown
    /// takes the mode as it is shown, so only SDL's own answer is read for it.
    /// </remarks>
    internal static string? Set(AppWindow window, WindowMode mode)
    {
        var w = window.Sdl.Window;
        var before = Of(w);
        if (mode == before) return null;
        if (before == WindowMode.Windowed) Keep(window);
        if (Request(w, mode) is { } refused)
        {
            Request(w, before);
            return refused;
        }
        if (mode == WindowMode.Windowed && Sizes.TryGetValue(window, out var kept)) SDL.SetWindowSize(w, kept.Size.Width, kept.Size.Height);
        if ((SDL.GetWindowFlags(w) & SDL.WindowFlags.Hidden) != 0) return null;
        SDL.SyncWindow(w);
        if (Of(w) == mode) return null;
        var error = SDL.GetError();
        Request(w, before);
        SDL.SyncWindow(w);
        return $"The system kept the window {Word(Of(w))}{(error.Length > 0 ? $". {error}" : "")}.";
    }

    // Asks SDL for the mode, or says why it refused at once.
    private static string? Request(IntPtr w, WindowMode mode)
    {
        switch (mode)
        {
            case WindowMode.Windowed:
                return SDL.SetWindowFullscreen(w, false) ? null : Refusal(mode);
            case WindowMode.Borderless:
                return SDL.SetWindowFullscreenMode(w, IntPtr.Zero) && SDL.SetWindowFullscreen(w, true) ? null : Refusal(mode);
            default:
                // A mode the program chose for the window (E3D.SetWindowFullscreenMode) is kept.
                if (SDL.GetWindowFullscreenMode(w) is not null) return SDL.SetWindowFullscreen(w, true) ? null : Refusal(mode);
                if (FullscreenMode(w) is not { } screen) return $"The system refused the fullscreen window, the screen offering no mode for it. {SDL.GetError()}".TrimEnd();
                return SDL.SetWindowFullscreenMode(w, screen) && SDL.SetWindowFullscreen(w, true) ? null : Refusal(mode);
        }
    }

    private static string Refusal(WindowMode mode) => $"The system refused the {Word(mode)} window. {SDL.GetError()}".TrimEnd();

    /// <summary>The mode's name as the setting writes it, in lower case.</summary>
    internal static string Word(WindowMode mode) => mode.ToString().ToLowerInvariant();

    /// <summary>
    /// The mode fullscreen takes, the one of the screen's fullscreen modes closest to the screen's
    /// own, or none where the screen lists none (SDL's offscreen driver).
    /// </summary>
    internal static SDL.DisplayMode? FullscreenMode(IntPtr w)
    {
        var display = SDL.GetDisplayForWindow(w);
        return SDL.GetDesktopDisplayMode(display) is { } screen
            && SDL.GetClosestFullscreenDisplayMode(display, screen.W, screen.H, screen.RefreshRate, true, out var closest) ? closest : null;
    }

    /// <summary>The screens there are by SDL's numbers, none before SDL's video starts.</summary>
    internal static uint[] Screens() => SDL.WasInit(SDL.InitFlags.Video) != 0 ? SDL.GetDisplays(out _) ?? [] : [];

    /// <summary>The number from 0 of the window's screen, or 0 where SDL knows none.</summary>
    internal static int ScreenOf(IntPtr w) => Math.Max(0, Array.IndexOf(Screens(), SDL.GetDisplayForWindow(w)));

    /// <summary>
    /// Moves the window to a screen by its number from 0, centered on it as a window, covering it
    /// otherwise, or says why it cannot.
    /// </summary>
    internal static string? MoveTo(AppWindow window, int screen)
    {
        var w = window.Sdl.Window;
        var screens = Screens();
        if (screen < 0 || screen >= screens.Length)
            return $"There is no screen {screen}, the screens being numbered from 0 to {screens.Length - 1}.";
        if (ScreenOf(w) == screen) return null;
        var mode = Of(w);
        // A fullscreen window is tied to its screen's mode, so it leaves it, moves and takes the
        // other's.
        if (mode == WindowMode.Fullscreen && Request(w, WindowMode.Borderless) is { } refused) return refused;
        var centered = (int)(SDL.WindowPosCenteredMask | screens[screen]);
        var moved = SDL.SetWindowPosition(w, centered, centered);
        if ((SDL.GetWindowFlags(w) & SDL.WindowFlags.Hidden) == 0) SDL.SyncWindow(w);
        if (mode == WindowMode.Fullscreen) Request(w, WindowMode.Fullscreen);
        if (moved && ((SDL.GetWindowFlags(w) & SDL.WindowFlags.Hidden) != 0 || ScreenOf(w) == screen)) return null;
        return $"The system kept the window on screen {ScreenOf(w)}, placing windows itself. {SDL.GetError()}".TrimEnd();
    }

    /// <summary>The size the window has as a window, its own while it is one and the one kept
    /// while it covers its screen.</summary>
    internal static (int Width, int Height) WindowedSize(AppWindow window)
    {
        if (Of(window.Sdl.Window) == WindowMode.Windowed) Keep(window);
        if (Sizes.TryGetValue(window, out var box)) return box.Size;
        SDL.GetWindowSize(window.Sdl.Window, out var width, out var height);
        return (width, height);
    }

    /// <summary>Sets the size the window has as a window, which a window covering its screen
    /// takes on leaving it.</summary>
    internal static void SetWindowedSize(AppWindow window, int width, int height)
    {
        Sizes.GetOrCreateValue(window).Size = (width, height);
        if (Of(window.Sdl.Window) == WindowMode.Windowed) SDL.SetWindowSize(window.Sdl.Window, width, height);
    }

    // The window's size as SDL holds it kept as its windowed one, where it is shown and not
    // minimized or maximized. Before it is shown the size set for it stands, which a system may
    // take only as the window is shown.
    private static void Keep(AppWindow window)
    {
        var w = window.Sdl.Window;
        var flags = SDL.GetWindowFlags(w);
        if ((flags & (SDL.WindowFlags.Hidden | SDL.WindowFlags.Minimized | SDL.WindowFlags.Maximized | SDL.WindowFlags.Fullscreen)) != 0) return;
        SDL.GetWindowSize(w, out var width, out var height);
        Sizes.GetOrCreateValue(window).Size = (width, height);
    }
}
