using System.Diagnostics;
using SDL3;

namespace Engine;

public static partial class Engine3D
{
    private static bool _eventsPumped;
    private static bool _shouldClose;
    private static Key _exitKey = Key.Escape;
    private static int _targetFps;
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

        _app = new App(Config.Default.WithWindow(title, width, height)).AddPlugin(new DefaultPlugins());
        _shouldClose = false;
        _eventsPumped = false;
        _lastFrameEnd = Stopwatch.GetTimestamp();
    }

    /// <summary>Runs <see cref="Stage.Cleanup"/>, closes the window and frees what the app holds.</summary>
    public static void CloseWindow()
    {
        _app?.Shutdown();
        _app = null;
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
    public static void SetWindowTitle(string title) => SDL.SetWindowTitle(World.Resource<AppWindow>().Sdl.Window, title);

    /// <summary>The window's width, in the units mouse positions and 2D drawing use.</summary>
    public static int GetScreenWidth() => World.Resource<AppWindow>().Sdl.Width;

    /// <summary>The window's height, in the units mouse positions and 2D drawing use.</summary>
    public static int GetScreenHeight() => World.Resource<AppWindow>().Sdl.Height;

    // -- Timing

    /// <summary>Caps the frame rate. <see cref="EndDrawing"/> waits out the rest of each frame. Zero removes the cap.</summary>
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

        if (!World.Resource<IMainLoopDriver>().PumpEvents())
            _shouldClose = true;
        if (_exitKey != Key.Unknown && World.Resource<Input>().KeyPressed(_exitKey))
            _shouldClose = true;
    }

    // Sleeps through most of what is left of the frame and spins through the last millisecond,
    // because a sleep on most systems overshoots by up to a millisecond.
    private static void WaitForTargetFrame()
    {
        if (_targetFps > 0)
        {
            var deadline = _lastFrameEnd + Stopwatch.Frequency / _targetFps;
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
