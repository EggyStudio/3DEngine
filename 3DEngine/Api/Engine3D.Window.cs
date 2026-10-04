using System.Diagnostics;
using SDL3;

namespace Engine;

public static partial class Engine3D
{
    private static bool _eventsPumped;
    private static bool _shouldClose;
    private static Key _exitKey = Key.Escape;
    private static int? _targetFps;
    private static long _lastFrameEnd;

    // -- Window

    /// <summary>Opens a window and builds the app behind it.</summary>
    /// <remarks>
    /// <see cref="Stage.Startup"/> runs on the first <see cref="BeginDrawing"/>, so plugins and
    /// behaviors added to <see cref="GetApp"/> between this call and the first frame take part in it.
    /// </remarks>
    /// <exception cref="InvalidOperationException">A window is open already.</exception>
    public static void InitWindow(int width, int height, string title)
    {
        if (_app is not null)
            throw new InvalidOperationException("A window is open already. Call CloseWindow first.");

        _app = new App(ConfigFor(width, height, title)).AddPlugin(new DefaultPlugins());
        _shouldClose = false;
        _eventsPumped = false;
        _lastFrameEnd = Stopwatch.GetTimestamp();
    }

    /// <summary>Runs <see cref="Stage.Cleanup"/>, closes the window and frees what the app holds.</summary>
    /// <remarks>Textures still loaded are freed with the window, and the log says how many there were.</remarks>
    public static void CloseWindow()
    {
        if (_app is not null) ForgetDefaultFonts();
        if (_app?.World.TryGetResource<TextureStore>(out var textures) == true && textures.Count > 0)
            Log.Category("Engine.Api").Warn($"CloseWindow: {textures.Count} texture(s) were still loaded.");
        _app?.Shutdown();
        _app = null;
        ForgetConfigFlags();
        _ambient = Entity.None;
    }

    /// <summary>Whether the window was asked to close, by its close button or by the exit key.</summary>
    /// <remarks>Processes the window's pending events, unless this frame already has.</remarks>
    public static bool WindowShouldClose()
    {
        PumpEvents();
        return _shouldClose;
    }

    /// <summary>Whether <see cref="InitWindow"/> has opened a window that is still open.</summary>
    public static bool IsWindowReady() => _app is not null;

    /// <summary>Sets the key that makes <see cref="WindowShouldClose"/> return true. <see cref="Key.Unknown"/> disables it.</summary>
    public static void SetExitKey(Key key) => _exitKey = key;

    /// <summary>Sets the window's title.</summary>
    public static void SetWindowTitle(string title)
    {
        if (World.TryGetResource<AppWindow>(out var window)) SDL.SetWindowTitle(window.Sdl.Window, title);
    }

    /// <summary>The window's width, in the units mouse positions and 2D drawing use. In a headless run, the width asked for.</summary>
    public static int GetScreenWidth() =>
        World.TryGetResource<AppWindow>(out var window) ? window.Sdl.Width : World.Resource<Config>().WindowData.Width;

    /// <summary>The window's height, in the units mouse positions and 2D drawing use. In a headless run, the height asked for.</summary>
    public static int GetScreenHeight() =>
        World.TryGetResource<AppWindow>(out var window) ? window.Sdl.Height : World.Resource<Config>().WindowData.Height;

    /// <summary>Writes the frame being drawn to a PNG file, once it is presented at <see cref="EndDrawing"/>.</summary>
    /// <remarks>A headless run draws nothing, so it logs why and writes nothing.</remarks>
    public static void TakeScreenshot(string fileName)
    {
        var path = Path.GetFullPath(fileName);
        if (Screenshots.Request(World, path, failure => { if (failure is not null) Log.Category("Engine.Api").Warn($"TakeScreenshot: {failure}"); }) is { } refusal)
            Log.Category("Engine.Api").Warn($"TakeScreenshot: {refusal}");
    }

    // -- Timing

    /// <summary>Caps the frame rate. <see cref="EndDrawing"/> waits out the rest of each frame. Zero removes the cap.</summary>
    /// <remarks>An offscreen or headless run is paced at <see cref="Config.HeadlessFps"/> until this is called, and zero uncaps it too, as a measurement needs.</remarks>
    public static void SetTargetFPS(int fps) => _targetFps = Math.Max(0, fps);

    /// <summary>Seconds the last frame took.</summary>
    public static float GetFrameTime() => (float)World.Resource<Time>().DeltaSeconds;

    /// <summary>Seconds since the first frame.</summary>
    public static double GetTime() => World.Resource<Time>().ElapsedSeconds;

    /// <summary>Frames per second, smoothed over the last few frames.</summary>
    public static int GetFPS() => (int)Math.Round(World.Resource<Time>().SmoothedFps);

    // Processes the window's events once per frame. Input keeps a key's pressed state until
    // Stage.Last, so events read here are seen by everything in the frame that follows.
    private static void PumpEvents()
    {
        if (_eventsPumped) return;
        _eventsPumped = true;
        _sizeBeforeEvents = (GetScreenWidth(), GetScreenHeight());

        if (!World.Resource<IMainLoopDriver>().PumpEvents())
            _shouldClose = true;
        if (_exitKey != Key.Unknown && World.Resource<Input>().KeyPressed(_exitKey))
            _shouldClose = true;
        _resized = (GetScreenWidth(), GetScreenHeight()) != _sizeBeforeEvents;
    }

    // Sleeps through most of what is left of the frame and spins through the last millisecond,
    // because a sleep on most systems overshoots by up to a millisecond.
    private static void WaitForTargetFrame()
    {
        // A headless run with no target set is paced at Config.HeadlessFps, so it does not spin a core.
        var config = World.Resource<Config>();
        var fps = _targetFps ?? (config.Headless ? config.HeadlessFps : 0);
        if (fps > 0)
        {
            var deadline = _lastFrameEnd + (long)(Stopwatch.Frequency / fps);
            while (true)
            {
                var remaining = (deadline - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
                if (remaining <= 0) break;
                if (remaining > 2) Thread.Sleep((int)(remaining - 1));
                else Thread.SpinWait(64);
            }
        }

        _lastFrameEnd = Stopwatch.GetTimestamp();
    }
}
