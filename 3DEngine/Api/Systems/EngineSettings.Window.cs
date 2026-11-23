using Diagnostics;
using ImGuiNET;
using SDL3;

namespace Engine;

internal static partial class EngineSettings
{
    // Why the window's page only reads in a run whose window is never shown.
    private const string Unshown = "The window is hidden, as --hidden or E3D_HIDDEN asks, so its settings only read.";

    // Why a setting of the window itself is refused where the run draws with none.
    private const string Undrawn = "The program has no window and draws no frames.";
    private const string Offscreen = "The program draws into images of its own with no window, as --offscreen asks.";

    /// <summary>
    /// The settings of the window's page a start gives a window from the settings file before it is
    /// shown, its size first, so a mode that covers the screen keeps the size to leave it at, and
    /// its mode last, on the screen it was given (<see cref="DiagnosticsBinding.OpenWindow"/>).
    /// </summary>
    internal static IReadOnlyList<string> WindowStart { get; } = ["window.width", "window.height", "window.resizable", "window.monitor", "window.scaling", "window.mode"];

    // The window's page, its mode, whether it is resizable, its size as a window, its screen and
    // its vsync, each acting at once and kept by the settings file, and ImGui's viewports. The page
    // only reads while the window is hidden, and the settings of the window itself while the run
    // has none.
    // Makes the window resizable or not, or says why the system kept it as it was, since a driver
    // with no edges of its own (SDL's offscreen one) answers yes and leaves the window.
    private static string? Resizable(IntPtr window, bool on)
    {
        var asked = SDL.SetWindowResizable(window, on);
        if (asked && ((SDL.GetWindowFlags(window) & SDL.WindowFlags.Resizable) != 0) == on) return null;
        var error = asked ? "" : SDL.GetError();
        return $"The system kept the window {(on ? "fixed" : "resizable")}{(error.Length > 0 ? $". {error}" : "")}.";
    }

    private static IEnumerable<Setting> Window()
    {
        static World? At() => ConsoleHost.SettingsWorld;
        static AppWindow? Shown() => At()?.TryGetResource<AppWindow>(out var window) == true ? window : null;
        static string? Hidden() => At()?.TryGetResource<Config>(out var config) == true && config.Hidden ? Unshown : null;
        // Why a setting of the window itself only reads for now, or null where there is a window.
        static string? Windowless() => Hidden() ?? (At() is not { } world || Shown() is not null ? null
            : world.TryGetResource<OffscreenSurface>(out _) ? Offscreen : Undrawn);

        yield return new Setting<WindowMode>("window.mode", "How the window stands on its screen, as a window with its frame, borderless with no frame at window.width by window.height in the middle of the screen and covering it at the screen's size or larger, borderless fullscreen covering the screen with no frame, drawing at window.width by window.height scaled to it as window.scaling says, or fullscreen taking the screen's mode.",
            WindowMode.Windowed, () => Shown() is { } window ? WindowModes.Of(window) : WindowMode.Windowed,
            apply: mode => Shown() is { } window ? WindowModes.Set(window, mode) : Undrawn)
        { Saved = true, Lock = Windowless };
        yield return new Setting<WindowScaling>("window.scaling", "How borderless fullscreen scales the frames it draws at window.width by window.height onto the screen, as large as fits with black round them, linear filtered and soft, nearest sharp, even at a whole ratio and uneven otherwise, or sharp, each frame pixel a block of one width with one blended screen pixel at most on its edges.",
            WindowScaling.Linear, () => Shown() is { } window ? window.Sdl.Scaling : WindowScaling.Linear,
            apply: scaling => { if (Shown() is not { } window) return Undrawn; WindowModes.SetScaling(window, scaling); return null; })
        { Saved = true, Lock = Windowless };
        yield return new Setting<bool>("window.resizable", "Whether the window's edges can be dragged to resize it.", true,
            () => Shown() is { } window ? (SDL.GetWindowFlags(window.Sdl.Window) & SDL.WindowFlags.Resizable) != 0 : true,
            apply: on => Shown() is { } window ? Resizable(window.Sdl.Window, on) : Undrawn)
        { Saved = true, Lock = Windowless };

        // The size reads the window's as a window, kept while it covers its screen, or what an
        // offscreen run draws at, and with neither the default.
        static (int Width, int Height) Sized() => At() is { } world && WindowCommands.Windowed(world) is { } size ? size : (600, 400);
        static string? Resize(int width, int height) => At() is { } world && WindowCommands.Resize(world, width, height) ? null : Undrawn;
        yield return new Setting<int>("window.width", "The width of the window as a window and as a borderless one, and of the frames borderless fullscreen draws, which the window takes on leaving fullscreen, or of the images an offscreen run draws into, which window.size sets with the height.",
            600, () => Sized().Width, apply: width => Resize(width, Sized().Height))
        { Low = 0, High = 16384, Step = 16, Saved = true, Lock = Hidden };
        yield return new Setting<int>("window.height", "The height of the window as a window and as a borderless one, and of the frames borderless fullscreen draws, which the window takes on leaving fullscreen, or of the images an offscreen run draws into, which window.size sets with the width.",
            400, () => Sized().Height, apply: height => Resize(Sized().Width, height))
        { Low = 0, High = 16384, Step = 16, Saved = true, Lock = Hidden };

        // The screen only reads where there is one, since the window has nowhere to go.
        static string? OneScreen() => Windowless() ?? (WindowModes.Screens().Length > 1 ? null
            : "There is one screen, so the window stays on it.");
        yield return new Setting<int>("window.monitor", "The screen the window is on, by its number from 0, where there is more than one, the window centered on another as it moves there.",
            0, () => Shown() is { } window ? WindowModes.ScreenOf(window.Sdl.Window) : 0,
            apply: screen => Shown() is { } window ? WindowModes.MoveTo(window, screen) : Undrawn)
        { Low = 0, High = 15, Step = 1, Saved = true, Lock = OneScreen };

        static SurfaceResize? Swapchain() => At()?.TryGetResource<SurfaceResize>(out var held) == true ? held : null;
        yield return Held("window.vsync", "Whether a frame waits for the display, the swapchain made again on the next frame.", false,
            Swapchain, Swapchain, s => s.Vsync, (s, on) => s.RequestVsync(on), Undrawn, saved: true, locked: Hidden);
        yield return new Setting<bool>("imgui.viewports", "Whether ImGui's windows may leave the program's window for windows of their own, where SDL's driver places windows on the desktop.",
            false, () => ImGui.GetCurrentContext() != IntPtr.Zero && (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0, apply: Viewports);
    }
}
