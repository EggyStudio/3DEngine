using System.Diagnostics;
using SDL3;

namespace Engine;

public static partial class E3D
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
    /// It seeds the random generator from the clock, as raylib's does (<see cref="SetRandomSeed"/>).
    /// </remarks>
    /// <exception cref="InvalidOperationException">A window is open already.</exception>
    public static void InitWindow(int width, int height, string title)
    {
        if (_app is not null)
            throw new InvalidOperationException("A window is open already. Call CloseWindow first.");

        _app = new App(ConfigFor(width, height, title)).AddPlugin(new DefaultPlugins());
        // A window never shown is left its size, as fullscreen is.
        if (_configFlags.HasFlag(ConfigFlags.BorderlessWindowedMode) && !IsWindowHidden()) ToggleBorderlessWindowed();
        _shouldClose = false;
        _eventsPumped = false;
        _frameTimeTakenAt = 0;
        _lastFrameEnd = Stopwatch.GetTimestamp();
        // Seeded from the clock's seconds, as raylib's is, unless the run names a seed.
        SetRandomSeed(RunMode.Seed() ?? (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>Runs <see cref="Stage.Cleanup"/>, closes the window and frees what the app holds.</summary>
    /// <remarks>Textures still loaded are freed with the window, and the log says how many there were.</remarks>
    public static void CloseWindow()
    {
        if (_app is not null)
        {
            ApiLogger.Info("CloseWindow was called, so the app shuts down.");
            ForgetDefaultFonts();
            ForgetCompute();
        }
        ForgetSky();
        if (_app?.World.TryGetResource<TextureStore>(out var textures) == true && textures.Count > 0)
            Log.Category("Engine.Api").Warn($"CloseWindow: {textures.Count} texture(s) were still loaded.");
        _app?.Shutdown();
        _app = null;
        ForgetConfigFlags();
        ForgetRlgl();
        ForgetAutomation();
        MixedProcessors.Clear();
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

    /// <summary>
    /// Has <see cref="WindowShouldClose"/> wait for input or a window event before the frame goes
    /// on, up to a tenth of a second, so a tool that changes only when used leaves the CPU idle.
    /// </summary>
    public static void EnableEventWaiting()
    {
        if (TryRes<AppWindow>(out var window)) window.WaitForEvents = true;
    }

    /// <summary>Has <see cref="WindowShouldClose"/> return at once again, as a game drawing every frame needs.</summary>
    public static void DisableEventWaiting()
    {
        if (TryRes<AppWindow>(out var window)) window.WaitForEvents = false;
    }

    /// <summary>Sets the key that makes <see cref="WindowShouldClose"/> return true. <see cref="Key.Null"/> disables it.</summary>
    public static void SetExitKey(Key key) => _exitKey = key;

    /// <summary>Sets the window's title.</summary>
    public static void SetWindowTitle(string title)
    {
        if (TryRes<AppWindow>(out var window)) SDL.SetWindowTitle(window.Sdl.Window, title);
    }

    /// <summary>The window's width, in the units mouse positions and 2D drawing use. In a headless run, the width asked for.</summary>
    public static int GetScreenWidth() =>
        TryRes<AppWindow>(out var window) ? window.Sdl.Width
        : TryRes<OffscreenSurface>(out var surface) ? (int)surface.Size.Width : Res<Config>().WindowData.Width;

    /// <summary>The window's height, in the units mouse positions and 2D drawing use. In a headless run, the height asked for.</summary>
    public static int GetScreenHeight() =>
        TryRes<AppWindow>(out var window) ? window.Sdl.Height
        : TryRes<OffscreenSurface>(out var surface) ? (int)surface.Size.Height : Res<Config>().WindowData.Height;

    /// <summary>Writes the frame being drawn to a PNG file, once it is presented at <see cref="EndDrawing"/>.</summary>
    /// <remarks>A headless run draws nothing, so it logs why and writes nothing.</remarks>
    public static void TakeScreenshot(string fileName)
    {
        var path = Path.GetFullPath(fileName);
        if (Screenshots.Request(World, path, failure => { if (failure is not null) Log.Category("Engine.Api").Warn($"TakeScreenshot: {failure}"); }) is { } refusal)
            Log.Category("Engine.Api").Warn($"TakeScreenshot: {refusal}");
    }

    /// <summary>
    /// Starts recording what the window shows as an animated GIF or PNG, by the extension of
    /// <paramref name="fileName"/>, a picture every <paramref name="intervalMs"/> milliseconds of the
    /// app's clock, until <see cref="StopRecording"/>, as raylib's examples record a GIF.
    /// </summary>
    /// <param name="fileName">Where the file goes, ending in <c>.gif</c> or <c>.png</c>.</param>
    /// <param name="intervalMs">How often a picture is taken, each shown for that long as it plays.</param>
    /// <param name="scale">How much smaller each picture is kept, above 0 and up to 1.</param>
    /// <remarks>
    /// Each picture is the frame <see cref="TakeScreenshot"/> would capture, kept in memory, at most
    /// a thousand. The file is written on a worker once the last pictures are back, under its own
    /// name only when whole, with a line in the log saying where. The <c>record</c> command records
    /// for a length as well. A file of neither extension, a second recording while one is made,
    /// and a run with no renderer are each left out with a warning.
    /// </remarks>
    public static void StartRecording(string fileName, int intervalMs = 100, float scale = 1)
    {
        if (!TryRes<Renderer>(out var renderer) || !renderer.Context.IsInitialized)
        {
            ApiLogger.Warn("StartRecording: there is no renderer to record from (a headless run draws nothing).");
            return;
        }
        try
        {
            Recorder.Start(new Recorder.Request(Path.GetFullPath(fileName), new Recorder.Measure(Math.Max(1, intervalMs) / 1000.0, false), null, null,
                Math.Clamp(scale, 0.01f, 1f), null));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            ApiLogger.Warn($"StartRecording: {error.Message}");
        }
    }

    /// <summary>Stops the recording <see cref="StartRecording"/> started, whose file is written once its last pictures are back.</summary>
    public static void StopRecording()
    {
        if (Recorder.Stop() is null) ApiLogger.Warn("StopRecording: nothing is being recorded.");
    }

    /// <summary>
    /// Sets the key that writes a screenshot in every program, F2 until moved, as Minecraft takes
    /// one, or turns it off with <see cref="Key.Null"/>.
    /// </summary>
    /// <remarks>
    /// The key writes the frame being drawn as a PNG into a <c>screenshots</c> folder beside the
    /// program, made on first use, named by the time as <c>2026-10-21_03.45.12.png</c>, a second in
    /// the same second <c>_2</c>, through <see cref="TakeScreenshot"/>, with a line in the log saying
    /// where. The press reaches the program too, which reads the key as any other.
    /// </remarks>
    public static void SetScreenshotKey(Key key) => World.GetOrInsertResource(static () => new DebugKeys()).Screenshot = key;

    /// <summary>
    /// Sets the key that shows and hides the engine's performance window, F3 until moved, or turns
    /// it off with <see cref="Key.Null"/>, as a program whose own F3 does something does.
    /// </summary>
    public static void SetProfileWindowKey(Key key) => World.GetOrInsertResource(static () => new DebugKeys()).ProfileWindow = key;

    // -- Timing

    /// <summary>Caps the frame rate. <see cref="EndDrawing"/> waits out the rest of each frame. Zero removes the cap.</summary>
    /// <remarks>An offscreen or headless run is paced at <see cref="Config.HeadlessFps"/> until this is called, and zero uncaps it too, as a measurement needs.</remarks>
    public static void SetTargetFPS(int fps) => _targetFps = Math.Max(0, fps);

    /// <summary>Seconds the last frame took.</summary>
    public static float GetFrameTime() => (float)Res<Time>().DeltaSeconds;

    /// <summary>Seconds since the window opened, read from the clock as it is called, as raylib's <c>GetTime</c> is.</summary>
    /// <remarks>
    /// It goes on past the frame's start, through the frame and the wait after it, so a program
    /// that times its own frames reads the time it waited. In a run whose frames each count a set
    /// time (<see cref="Config.FrameSeconds"/>) it is the frame's time, so a test reads the same
    /// time on any machine.
    /// </remarks>
    public static double GetTime()
    {
        var time = Res<Time>();
        return time.FrameSeconds > 0 || _frameTimeTakenAt == 0
            ? time.ElapsedSeconds
            : time.ElapsedSeconds + Stopwatch.GetElapsedTime(_frameTimeTakenAt).TotalSeconds;
    }

    // When the frame's time was taken, which GetTime counts on from.
    private static long _frameTimeTakenAt;

    /// <summary>Frames per second, smoothed over the last few frames.</summary>
    public static int GetFPS() => (int)Math.Round(Res<Time>().SmoothedFps);

    /// <summary>
    /// Processes the window's pending events, as raylib's <c>PollInputEvents</c> does, unless the
    /// frame has already, which <see cref="WindowShouldClose"/> and <see cref="BeginDrawing"/> do.
    /// </summary>
    /// <remarks>
    /// raylib built with <c>SUPPORT_CUSTOM_FRAME_CONTROL</c> leaves the polling to this call, where
    /// the engine polls each frame as raylib's own build does, so a program written for that build
    /// runs as it was meant to.
    /// </remarks>
    public static void PollInputEvents() => PumpEvents();

    /// <summary>
    /// Shows the frame drawn, as raylib's <c>SwapScreenBuffer</c> does, which <see cref="EndDrawing"/>
    /// has done already.
    /// </summary>
    /// <remarks>
    /// raylib built with <c>SUPPORT_CUSTOM_FRAME_CONTROL</c> leaves the swap to this call, where
    /// the engine presents each frame as <see cref="EndDrawing"/> renders it, so it has nothing
    /// left to do.
    /// </remarks>
    public static void SwapScreenBuffer() { }

    // Processes the window's events once per frame. Input keeps a key's pressed state until
    // Stage.Last, so events read here are seen by everything in the frame that follows.
    private static void PumpEvents()
    {
        if (_eventsPumped) return;
        _eventsPumped = true;
        _sizeBeforeEvents = (GetScreenWidth(), GetScreenHeight());

        if (!Res<IMainLoopDriver>().PumpEvents())
            SetShouldClose("the main loop's driver says the app was asked to close");
        if (_exitKey != Key.Null && Res<Input>().KeyPressed(_exitKey))
            SetShouldClose($"the exit key, {_exitKey}, was pressed");
        _resized = (GetScreenWidth(), GetScreenHeight()) != _sizeBeforeEvents;
    }

    // Has WindowShouldClose return true from here on, and logs why the first time, so every way the
    // main loop ends leaves a line, and a log that stops with none ended in a kill or a fault.
    private static void SetShouldClose(string why)
    {
        if (!_shouldClose) ApiLogger.Info($"WindowShouldClose returns true from here on, since {why}.");
        _shouldClose = true;
    }

    // Sleeps through most of what is left of the frame and spins through the last millisecond,
    // because a sleep on most systems overshoots by up to a millisecond.
    private static void WaitForTargetFrame()
    {
        // A headless run with no target set is paced at Config.HeadlessFps, so it does not spin a core.
        var config = Res<Config>();
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
