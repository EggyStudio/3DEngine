using System.Runtime.CompilerServices;
using SDL3;

namespace Engine;

/// <summary>How a window covers its screen, as the <c>window.mode</c> setting takes it.</summary>
internal enum WindowMode
{
    /// <summary>A window with its frame, of its own size among the desktop's others.</summary>
    Windowed,

    /// <summary>
    /// A window with no frame of the size set, in the middle of its screen with the desktop round
    /// it, which covers the screen where the size is as large as the screen's both ways.
    /// </summary>
    Borderless,

    /// <summary>
    /// A window with no frame always covering its screen, the screen's mode unchanged.
    /// </summary>
    BorderlessFullscreen,

    /// <summary>The screen taken over at the screen's own mode.</summary>
    Fullscreen,
}

/// <summary>
/// Reads and sets how a window covers its screen, moves it to another screen, and keeps the size it
/// has as a window while it covers one, which the window's settings and the flat API share.
/// </summary>
internal static class WindowModes
{
    // What each window keeps of itself as a window while it is another mode, and the mode chosen.
    private static readonly ConditionalWeakTable<AppWindow, Box> Kept = [];

    private sealed class Box
    {
        // The size set for the window, which a borderless one takes as well, none before one is
        // known, and its place and whether it had a frame as a window.
        public (int Width, int Height)? Size;
        public (int X, int Y)? Place;
        public bool Framed = true;
        public WindowMode Chosen;
        // The size window.resolution set, none for native.
        public (int Width, int Height)? Resolution;
    }

    /// <summary>How the window covers its screen, as SDL holds it and as it was chosen.</summary>
    /// <remarks>
    /// SDL holds no mode for a window covering its screen without a frame and the screen's mode, or
    /// one set, for a fullscreen one. A borderless window as large as its screen covers it as
    /// borderless fullscreen does, so which of the two it is was kept as it was chosen.
    /// </remarks>
    internal static WindowMode Of(AppWindow window)
    {
        var w = window.Sdl.Window;
        var chosen = Kept.TryGetValue(window, out var box) ? box.Chosen : WindowMode.Windowed;
        if ((SDL.GetWindowFlags(w) & SDL.WindowFlags.Fullscreen) == 0)
            return chosen == WindowMode.Borderless ? WindowMode.Borderless : WindowMode.Windowed;
        if (SDL.GetWindowFullscreenMode(w) is not null) return WindowMode.Fullscreen;
        return chosen == WindowMode.Borderless ? WindowMode.Borderless : WindowMode.BorderlessFullscreen;
    }

    /// <summary>
    /// Sets how the window covers its screen and waits for the system to have done it, or says why
    /// the system refused it, the mode before kept.
    /// </summary>
    /// <remarks>
    /// A window going back to being a window takes the size, the place and the frame kept for it.
    /// A borderless window is sized and placed again though it is borderless already, as the size
    /// set may have changed. A window not yet shown takes the mode as it is shown, so only SDL's
    /// own answer is read for it. A size given is set for the window first, as the flat API's
    /// borderless window takes its monitor's.
    /// </remarks>
    internal static string? Set(AppWindow window, WindowMode mode, (int Width, int Height)? size = null) => Change(window, mode, size);

    private static string? Change(AppWindow window, WindowMode mode, (int Width, int Height)? size)
    {
        var w = window.Sdl.Window;
        var before = Of(window);
        if (mode == before && mode != WindowMode.Borderless && size is null) return null;
        if (before == WindowMode.Windowed) Keep(window);
        var box = Kept.GetOrCreateValue(window);
        box.Size = size ?? box.Size;
        box.Chosen = mode;
        if (Request(window, mode, before) is { } refused)
        {
            box.Chosen = before;
            Request(window, before, mode);
            return refused;
        }
        if ((SDL.GetWindowFlags(w) & SDL.WindowFlags.Hidden) != 0) return null;
        SDL.SyncWindow(w);
        if (Of(window) == mode) return null;
        var error = SDL.GetError();
        box.Chosen = before;
        Request(window, before, mode);
        SDL.SyncWindow(w);
        return $"The system kept the window {Said(Of(window))}{(error.Length > 0 ? $". {error}" : "")}.";
    }

    // Asks SDL for the mode, from the one before, or says why it refused at once.
    private static string? Request(AppWindow window, WindowMode mode, WindowMode before)
    {
        var w = window.Sdl.Window;
        var box = Kept.GetOrCreateValue(window);
        switch (mode)
        {
            case WindowMode.Windowed:
                // The frame and the place a borderless window gave up, which SDL keeps of a window
                // only across its own fullscreen.
                if (!SDL.SetWindowFullscreen(w, false)) return Refusal(mode);
                SDL.SetWindowBordered(w, box.Framed);
                if (box.Size is { } size) SDL.SetWindowSize(w, size.Width, size.Height);
                if (box.Place is { } place) SDL.SetWindowPosition(w, place.X, place.Y);
                return null;
            case WindowMode.Borderless:
                // Covering where the size set is as large as the screen, and otherwise a window
                // with no frame of the size in the middle of the screen.
                var (width, height) = WindowedSize(window);
                if (Covers(w, width, height)) return Request(window, WindowMode.BorderlessFullscreen, before);
                if (!SDL.SetWindowFullscreen(w, false)) return Refusal(mode);
                SDL.SetWindowBordered(w, false);
                SDL.SetWindowSize(w, width, height);
                var middle = (int)(SDL.WindowPosCenteredMask | SDL.GetDisplayForWindow(w));
                SDL.SetWindowPosition(w, middle, middle);
                return null;
            case WindowMode.BorderlessFullscreen:
                return SDL.SetWindowFullscreenMode(w, IntPtr.Zero) && SDL.SetWindowFullscreen(w, true) ? null : Refusal(mode);
            default:
                // The mode of the size window.resolution set where the screen lists it, and
                // otherwise a mode the program chose for the window (E3D.SetWindowFullscreenMode).
                if (box.Resolution is { } resolution && ListedMode(w, resolution) is { } listed)
                    return SDL.SetWindowFullscreenMode(w, listed) && SDL.SetWindowFullscreen(w, true) ? null : Refusal(mode);
                if (SDL.GetWindowFullscreenMode(w) is not null) return SDL.SetWindowFullscreen(w, true) ? null : Refusal(mode);
                if (FullscreenMode(w) is not { } screen) return $"The system refused the fullscreen window, the screen offering no mode for it. {SDL.GetError()}".TrimEnd();
                return SDL.SetWindowFullscreenMode(w, screen) && SDL.SetWindowFullscreen(w, true) ? null : Refusal(mode);
        }
    }

    /// <summary>
    /// The sizes of the fullscreen modes the window's screen lists, largest first, each once.
    /// </summary>
    internal static (int Width, int Height)[] ListedSizes(IntPtr w) =>
        SDL.GetFullscreenDisplayModes(SDL.GetDisplayForWindow(w), out _) is { } modes ? modes.Select(m => (m.W, m.H)).Distinct().ToArray() : [];

    // The fullscreen mode of the size closest to the screen's own rate, or none where the screen
    // lists no mode of it.
    private static SDL.DisplayMode? ListedMode(IntPtr w, (int Width, int Height) size)
    {
        var display = SDL.GetDisplayForWindow(w);
        var rate = SDL.GetDesktopDisplayMode(display) is { } screen ? screen.RefreshRate : 0f;
        return SDL.GetClosestFullscreenDisplayMode(display, size.Width, size.Height, rate, true, out var closest)
            && (closest.W, closest.H) == size ? closest : null;
    }

    /// <summary>
    /// The window's resolution, the size of its mode in fullscreen and its size as a window
    /// otherwise, or none for native, the screen's own mode and the size the program made the
    /// window at.
    /// </summary>
    internal static (int Width, int Height)? ResolutionOf(AppWindow window)
    {
        if (Of(window) == WindowMode.Fullscreen)
            return Kept.TryGetValue(window, out var box) ? box.Resolution : null;
        var size = WindowedSize(window);
        return size == window.Sdl.Made ? null : size;
    }

    /// <summary>
    /// Sets the picture the scene is drawn as, and has the frames drawn again at its size.
    /// </summary>
    internal static void SetPicture(AppWindow window, Picture picture)
    {
        window.Sdl.Picture = picture;
        window.Repicture();
    }

    /// <summary>
    /// Sets the window's size as a window and as a borderless one, and its mode's in fullscreen, or
    /// for none the size the program made it at and the screen's own mode, or says why it cannot,
    /// the size before kept. A window takes any size up to its screen's, and fullscreen only a mode
    /// the screen lists.
    /// </summary>
    internal static string? SetResolution(AppWindow window, (int Width, int Height)? size)
    {
        var w = window.Sdl.Window;
        var mode = Of(window);
        if (size is { } asked)
        {
            if (mode == WindowMode.Fullscreen && ListedMode(w, asked) is null)
                return $"The screen lists no {asked.Width}x{asked.Height} mode for fullscreen, its modes being {string.Join(", ", ListedSizes(w).Select(s => $"{s.Width}x{s.Height}"))}.";
            if (mode != WindowMode.Fullscreen && SDL.GetDisplayBounds(SDL.GetDisplayForWindow(w), out var screen)
                && (asked.Width > screen.W || asked.Height > screen.H))
                return $"A window takes a size up to its screen's own {screen.W}x{screen.H}, not {asked.Width}x{asked.Height}.";
        }
        var box = Kept.GetOrCreateValue(window);
        var before = box.Resolution;
        box.Resolution = size;
        if (mode == WindowMode.Fullscreen)
        {
            // The window takes the size as a window too on leaving the mode.
            if (Request(window, mode, mode) is not { } refused)
            {
                box.Size = size ?? window.Sdl.Made;
                return null;
            }
            box.Resolution = before;
            Request(window, mode, mode);
            return refused;
        }
        var (width, height) = size ?? window.Sdl.Made;
        SetWindowedSize(window, width, height);
        return null;
    }

    private static string Refusal(WindowMode mode) => $"The system refused the {Said(mode)} window. {SDL.GetError()}".TrimEnd();

    /// <summary>
    /// Whether a window of the size covers the window's screen, as large as it both ways.
    /// </summary>
    internal static bool Covers(IntPtr w, int width, int height) =>
        SDL.GetDisplayBounds(SDL.GetDisplayForWindow(w), out var screen) && width >= screen.W && height >= screen.H;

    /// <summary>The mode's name as the setting writes it, in lower case.</summary>
    internal static string Word(WindowMode mode) => mode.ToString().ToLowerInvariant();

    // The mode's name in a sentence, its words apart.
    private static string Said(WindowMode mode) => mode == WindowMode.BorderlessFullscreen ? "borderless fullscreen" : Word(mode);

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
        var mode = Of(window);
        // A fullscreen window is tied to its screen's mode, so it leaves it, moves and takes the
        // other's.
        if (mode == WindowMode.Fullscreen && Request(window, WindowMode.BorderlessFullscreen, mode) is { } refused) return refused;
        var centered = (int)(SDL.WindowPosCenteredMask | screens[screen]);
        var moved = SDL.SetWindowPosition(w, centered, centered);
        if ((SDL.GetWindowFlags(w) & SDL.WindowFlags.Hidden) == 0) SDL.SyncWindow(w);
        // A fullscreen window takes the other screen's mode, and a borderless one covers it or not
        // by the other screen's size.
        if (mode is WindowMode.Fullscreen or WindowMode.Borderless) Request(window, mode, mode);
        if (moved && ((SDL.GetWindowFlags(w) & SDL.WindowFlags.Hidden) != 0 || ScreenOf(w) == screen)) return null;
        return $"The system kept the window on screen {ScreenOf(w)}, placing windows itself. {SDL.GetError()}".TrimEnd();
    }

    /// <summary>The size set for the window, its own while it is a window, the one a borderless
    /// window has, and the one kept while it covers its screen.</summary>
    internal static (int Width, int Height) WindowedSize(AppWindow window)
    {
        if (Of(window) == WindowMode.Windowed) Keep(window);
        if (Kept.TryGetValue(window, out var box) && box.Size is { } size) return size;
        SDL.GetWindowSize(window.Sdl.Window, out var width, out var height);
        return (width, height);
    }

    /// <summary>Sets the size set for the window, which a window takes at once, a borderless one
    /// at once in the middle of its screen, and one covering its screen on leaving it.</summary>
    internal static void SetWindowedSize(AppWindow window, int width, int height)
    {
        Kept.GetOrCreateValue(window).Size = (width, height);
        switch (Of(window))
        {
            case WindowMode.Windowed:
                SDL.SetWindowSize(window.Sdl.Window, width, height);
                break;
            case WindowMode.Borderless:
                Set(window, WindowMode.Borderless);
                break;
        }
    }

    // The window's size, place and frame as SDL holds them kept as its windowed ones, where it is
    // shown and not minimized or maximized. Before it is shown the size set for it stands, which a
    // system may take only as the window is shown.
    private static void Keep(AppWindow window)
    {
        var w = window.Sdl.Window;
        var flags = SDL.GetWindowFlags(w);
        var box = Kept.GetOrCreateValue(window);
        box.Framed = (flags & SDL.WindowFlags.Borderless) == 0;
        if ((flags & (SDL.WindowFlags.Hidden | SDL.WindowFlags.Minimized | SDL.WindowFlags.Maximized | SDL.WindowFlags.Fullscreen)) != 0) return;
        SDL.GetWindowSize(w, out var width, out var height);
        box.Size = (width, height);
        if (SDL.GetWindowPosition(w, out var x, out var y)) box.Place = (x, y);
    }
}
