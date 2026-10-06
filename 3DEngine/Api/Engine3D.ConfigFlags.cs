using SDL3;

namespace Engine;

public static partial class Engine3D
{
    private static ConfigFlags _configFlags;
    private static int? _configSamples;

    /// <summary>Asks the next <see cref="InitWindow"/> for these flags, added to any asked before, as raylib's does.</summary>
    /// <remarks>Called before <see cref="InitWindow"/>. <see cref="CloseWindow"/> forgets them.</remarks>
    public static void SetConfigFlags(ConfigFlags flags) => _configFlags |= flags;

    /// <summary>
    /// How many samples a pixel of the next window is drawn with: 1 for none, or 2, 4 or 8, rounded
    /// down to what the device can do. The engine draws with 4 unless asked.
    /// </summary>
    public static void SetConfigSamples(int samples) => _configSamples = Math.Clamp(samples, 1, 8);

    // The config InitWindow opens its window with, from the flags asked for.
    internal static Config ConfigFor(int width, int height, string title)
    {
        var flags = _configFlags;
        var config = Config.Default.WithWindow(title, width, height);
        _alwaysRun = flags.HasFlag(ConfigFlags.WindowAlwaysRun);
        return config with
        {
            HighPixelDensity = flags.HasFlag(ConfigFlags.WindowHighdpi),
            Transparent = flags.HasFlag(ConfigFlags.WindowTransparent),
            Unfocused = flags.HasFlag(ConfigFlags.WindowUnfocused),
            Samples = _configSamples ?? (flags.HasFlag(ConfigFlags.Msaa4xHint) ? 4 : RunMode.Samples(config.Samples)),
            Vsync = flags.HasFlag(ConfigFlags.VsyncHint),
            Fullscreen = flags.HasFlag(ConfigFlags.FullscreenMode),
            Undecorated = flags.HasFlag(ConfigFlags.WindowUndecorated),
            Topmost = flags.HasFlag(ConfigFlags.WindowTopmost),
            Resizable = flags.HasFlag(ConfigFlags.WindowResizable),
            Hidden = config.Hidden || flags.HasFlag(ConfigFlags.WindowHidden),
            WindowCommand = flags.HasFlag(ConfigFlags.WindowMaximized) ? WindowCommand.Maximize
                : flags.HasFlag(ConfigFlags.WindowMinimized) ? WindowCommand.Minimize
                : config.WindowCommand,
        };
    }

    /// <summary>
    /// Turns flags on for the open window: fullscreen, borderless windowed, resizable, undecorated,
    /// hidden, minimized, maximized, topmost, unfocused, always run or vsync.
    /// </summary>
    /// <remarks>
    /// Vsync changes with the next frame, which makes the swapchain again with the present mode for
    /// it, as a settings screen needs. MSAA, high density and transparency are chosen as the window
    /// opens and are left as they are, which the log says. Unfocused leaves the focus where it is
    /// the next time the window is shown, rather than taking it away now.
    /// </remarks>
    public static void SetWindowState(ConfigFlags flags) => ChangeWindowState(flags, on: true);

    /// <summary>Turns flags off for the open window, as <see cref="SetWindowState"/> turns them on.</summary>
    public static void ClearWindowState(ConfigFlags flags) => ChangeWindowState(flags, on: false);

    /// <summary>Whether the open window has every one of the flags, its vsync as last set and its MSAA as it opened with.</summary>
    public static bool IsWindowState(ConfigFlags flags)
    {
        if (WindowHandle is not (not 0 and var w)) return false;
        var sdl = SDL.GetWindowFlags(w);
        var config = TryRes<Config>(out var c) ? c : null;
        bool Has(ConfigFlags flag) => flag switch
        {
            ConfigFlags.FullscreenMode => IsWindowFullscreen(),
            ConfigFlags.BorderlessWindowedMode => (sdl & SDL.WindowFlags.Fullscreen) != 0 && _borderless,
            ConfigFlags.WindowResizable => (sdl & SDL.WindowFlags.Resizable) != 0,
            ConfigFlags.WindowUndecorated => (sdl & SDL.WindowFlags.Borderless) != 0,
            ConfigFlags.WindowHidden => (sdl & SDL.WindowFlags.Hidden) != 0,
            ConfigFlags.WindowMinimized => (sdl & SDL.WindowFlags.Minimized) != 0,
            ConfigFlags.WindowMaximized => (sdl & SDL.WindowFlags.Maximized) != 0,
            ConfigFlags.WindowTopmost => (sdl & SDL.WindowFlags.AlwaysOnTop) != 0,
            ConfigFlags.VsyncHint => TryRes<SurfaceResize>(out var surface) ? surface.Vsync : config?.Vsync == true,
            ConfigFlags.Msaa4xHint => config?.Samples > 1,
            ConfigFlags.WindowHighdpi => (sdl & SDL.WindowFlags.HighPixelDensity) != 0,
            ConfigFlags.WindowTransparent => (sdl & SDL.WindowFlags.Transparent) != 0,
            ConfigFlags.WindowUnfocused => (sdl & SDL.WindowFlags.InputFocus) == 0,
            ConfigFlags.WindowAlwaysRun => _alwaysRun,
            _ => false,
        };
        foreach (var flag in Enum.GetValues<ConfigFlags>())
            if (flag != ConfigFlags.None && flags.HasFlag(flag) && !Has(flag)) return false;
        return true;
    }

    private static void ChangeWindowState(ConfigFlags flags, bool on)
    {
        // An offscreen run makes its images again too, with no window to change.
        if (flags.HasFlag(ConfigFlags.VsyncHint) && TryRes<SurfaceResize>(out var surface)) surface.RequestVsync(on);
        if (flags.HasFlag(ConfigFlags.WindowAlwaysRun)) _alwaysRun = on;
        // A window shown after this takes the focus or leaves it, as the hint says when it is shown.
        if (flags.HasFlag(ConfigFlags.WindowUnfocused)) SDL.SetHint(SDL.Hints.WindowActivateWhenShown, on ? "0" : "1");
        if (WindowHandle is not (not 0 and var w)) return;
        if (flags.HasFlag(ConfigFlags.FullscreenMode) && IsWindowFullscreen() != on) ToggleFullscreen();
        if (flags.HasFlag(ConfigFlags.BorderlessWindowedMode) && IsWindowState(ConfigFlags.BorderlessWindowedMode) != on) ToggleBorderlessWindowed();
        if (flags.HasFlag(ConfigFlags.WindowResizable)) SDL.SetWindowResizable(w, on);
        if (flags.HasFlag(ConfigFlags.WindowUndecorated)) SDL.SetWindowBordered(w, !on);
        if (flags.HasFlag(ConfigFlags.WindowTopmost)) SDL.SetWindowAlwaysOnTop(w, on);
        if (flags.HasFlag(ConfigFlags.WindowHidden))
        {
            if (on) SDL.HideWindow(w);
            // A --hidden run's window is never shown, whatever the program asks.
            else if (!(TryRes<Config>(out var running) && running.Hidden)) SDL.ShowWindow(w);
        }
        if (flags.HasFlag(ConfigFlags.WindowMinimized))
        {
            if (on) SDL.MinimizeWindow(w);
            else SDL.RestoreWindow(w);
        }
        if (flags.HasFlag(ConfigFlags.WindowMaximized))
        {
            if (on) SDL.MaximizeWindow(w);
            else SDL.RestoreWindow(w);
        }
        if (flags.HasFlag(ConfigFlags.Msaa4xHint))
            ApiLogger.Warn("MSAA is chosen as the window opens, and SetWindowState and ClearWindowState leave it as it is.");
        if ((flags & (ConfigFlags.WindowHighdpi | ConfigFlags.WindowTransparent)) != 0)
            ApiLogger.Warn("High density and transparency are chosen as the window opens, and SetWindowState and ClearWindowState leave them as they are.");
    }

    // Whether the program asked for its loop to go on while minimized, which it does either way,
    // and whether the window covers the monitor by ToggleBorderlessWindowed rather than ToggleFullscreen.
    private static bool _alwaysRun;
    private static bool _borderless;

    private static void ForgetConfigFlags()
    {
        _configFlags = ConfigFlags.None;
        _configSamples = null;
        (_alwaysRun, _borderless) = (false, false);
        SDL.ResetHint(SDL.Hints.WindowActivateWhenShown);
    }
}
