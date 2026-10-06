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
    /// <summary>
    /// Let the desktop show through where the window is drawn clear, as <c>ClearBackground(Color.Blank)</c>
    /// leaves it, where the desktop composites windows. Asked before the window opens.
    /// </summary>
    WindowTransparent = 0x00000010,
    /// <summary>Keep the program's loop going while the window is minimized, as the engine's loop always goes on.</summary>
    WindowAlwaysRun = 0x00000100,
    /// <summary>
    /// Leave the keyboard focus where it is when the window is shown. <see cref="Engine3D.IsWindowState"/>
    /// answers whether the window is without the focus, as raylib's does.
    /// </summary>
    WindowUnfocused = 0x00000800,
    /// <summary>
    /// Draw at the monitor's pixels on one that scales them, so a monitor at twice the density
    /// draws twice as many each way, which <see cref="Engine3D.GetRenderWidth"/> gives. Asked
    /// before the window opens.
    /// </summary>
    /// <remarks>
    /// Without it, on Wayland and macOS, the window is drawn at its size and the desktop scales the
    /// picture up. On Windows and X11, which size a window in pixels, the engine enlarges the window
    /// by the monitor's scale and draws it at those pixels either way.
    /// </remarks>
    WindowHighdpi = 0x00002000,
    /// <summary>Cover the monitor with the window, borderless, at the desktop's display mode, as <see cref="Engine3D.ToggleBorderlessWindowed"/> does.</summary>
    BorderlessWindowedMode = 0x00008000,
    /// <summary>Let the mouse through the window to what is under it, which SDL3 has no way to, so it is warned of and does nothing, as raylib's SDL backend does.</summary>
    WindowMousePassthrough = 0x00004000,
    /// <summary>An interlaced video mode on a Raspberry Pi's V3D, which a desktop has none of, so it is warned of and does nothing, as raylib's SDL backend does.</summary>
    InterlacedHint = 0x00010000,
}
