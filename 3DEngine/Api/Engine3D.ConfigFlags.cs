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

    private static void ForgetConfigFlags()
    {
        _configFlags = ConfigFlags.None;
        _configSamples = null;
    }
}
