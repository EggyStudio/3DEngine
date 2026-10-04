using SDL3;

namespace Engine;

/// <summary>What <see cref="Engine3D.SetConfigFlags"/> asks of the next window, with raylib's names and values.</summary>
[Flags]
public enum ConfigFlags : uint
{
    /// <summary>Nothing asked.</summary>
    None = 0,
    /// <summary>Present frames in step with the display's refresh.</summary>
    VsyncHint = 0x00000040,
    /// <summary>Fill the display.</summary>
    FullscreenMode = 0x00000002,
    /// <summary>Let the window be resized by its edges, which raylib's windows are not unless asked.</summary>
    WindowResizable = 0x00000004,
    /// <summary>No title bar or border.</summary>
    WindowUndecorated = 0x00000008,
    /// <summary>Open the window hidden.</summary>
    WindowHidden = 0x00000080,
    /// <summary>Open the window minimized.</summary>
    WindowMinimized = 0x00000200,
    /// <summary>Open the window maximized.</summary>
    WindowMaximized = 0x00000400,
    /// <summary>Keep the window above others.</summary>
    WindowTopmost = 0x00001000,
    /// <summary>Four samples a pixel, which the engine draws with unless <see cref="Engine3D.SetConfigSamples"/> says otherwise.</summary>
    Msaa4xHint = 0x00000020,
}

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
        return config with
        {
            Samples = _configSamples ?? (flags.HasFlag(ConfigFlags.Msaa4xHint) ? 4 : config.Samples),
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

    /// <summary>Turns flags on for the open window: fullscreen, resizable, undecorated, hidden, minimized, maximized or topmost.</summary>
    /// <remarks>Vsync and MSAA are chosen as the window opens and are left as they are, which the log says.</remarks>
    public static void SetWindowState(ConfigFlags flags) => ChangeWindowState(flags, on: true);

    /// <summary>Turns flags off for the open window, as <see cref="SetWindowState"/> turns them on.</summary>
    public static void ClearWindowState(ConfigFlags flags) => ChangeWindowState(flags, on: false);

    /// <summary>Whether the open window has every one of the flags, its vsync and MSAA as it opened with them.</summary>
    public static bool IsWindowState(ConfigFlags flags)
    {
        if (WindowHandle is not (not 0 and var w)) return false;
        var sdl = SDL.GetWindowFlags(w);
        var config = TryRes<Config>(out var c) ? c : null;
        bool Has(ConfigFlags flag) => flag switch
        {
            ConfigFlags.FullscreenMode => (sdl & SDL.WindowFlags.Fullscreen) != 0,
            ConfigFlags.WindowResizable => (sdl & SDL.WindowFlags.Resizable) != 0,
            ConfigFlags.WindowUndecorated => (sdl & SDL.WindowFlags.Borderless) != 0,
            ConfigFlags.WindowHidden => (sdl & SDL.WindowFlags.Hidden) != 0,
            ConfigFlags.WindowMinimized => (sdl & SDL.WindowFlags.Minimized) != 0,
            ConfigFlags.WindowMaximized => (sdl & SDL.WindowFlags.Maximized) != 0,
            ConfigFlags.WindowTopmost => (sdl & SDL.WindowFlags.AlwaysOnTop) != 0,
            ConfigFlags.VsyncHint => config?.Vsync == true,
            ConfigFlags.Msaa4xHint => config?.Samples > 1,
            _ => false,
        };
        foreach (var flag in Enum.GetValues<ConfigFlags>())
            if (flag != ConfigFlags.None && flags.HasFlag(flag) && !Has(flag)) return false;
        return true;
    }

    private static void ChangeWindowState(ConfigFlags flags, bool on)
    {
        if (WindowHandle is not (not 0 and var w)) return;
        if (flags.HasFlag(ConfigFlags.FullscreenMode)) SDL.SetWindowFullscreen(w, on);
        if (flags.HasFlag(ConfigFlags.WindowResizable)) SDL.SetWindowResizable(w, on);
        if (flags.HasFlag(ConfigFlags.WindowUndecorated)) SDL.SetWindowBordered(w, !on);
        if (flags.HasFlag(ConfigFlags.WindowTopmost)) SDL.SetWindowAlwaysOnTop(w, on);
        if (flags.HasFlag(ConfigFlags.WindowHidden))
        {
            if (on) SDL.HideWindow(w);
            else SDL.ShowWindow(w);
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
        if ((flags & (ConfigFlags.VsyncHint | ConfigFlags.Msaa4xHint)) != 0)
            ApiLogger.Warn("Vsync and MSAA are chosen as the window opens, and SetWindowState and ClearWindowState leave them as they are.");
    }

    private static void ForgetConfigFlags()
    {
        _configFlags = ConfigFlags.None;
        _configSamples = null;
    }
}
