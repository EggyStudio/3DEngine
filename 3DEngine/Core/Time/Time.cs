namespace Engine;

/// <summary>
/// Frame timing data resource. Updated once per frame by <see cref="TimePlugin"/>.
/// <para>
/// Provides raw delta, max-clamped delta (guards against debugger pauses / hitches),
/// frame count, and an exponentially smoothed FPS estimate.
/// </para>
/// </summary>
/// <example>
/// <code>
/// // Read timing data inside a behavior
/// [Behavior]
/// public partial struct FpsOverlay
/// {
///     [OnRender]
///     public static void Draw(BehaviorContext ctx)
///     {
///         Console.WriteLine($"Frame {ctx.Time.FrameCount}: {ctx.Time.SmoothedFps:F0} FPS");
///     }
/// }
/// </code>
/// <code>
/// // Read timing data inside a raw system
/// app.AddSystem(Stage.Update, static world =>
/// {
///     var time = world.Resource&lt;Time&gt;();
///     float dt = (float)time.DeltaSeconds;
///     Console.WriteLine($"Frame {time.FrameCount}: {time.SmoothedFps:F0} FPS, delta {dt * 1000:F1}ms");
/// });
/// </code>
/// </example>
public sealed class Time
{
    /// <summary>
    /// Maximum delta time in seconds. Frames longer than this are clamped to prevent
    /// physics explosions or spiral-of-death after debugger pauses.
    /// Defaults to 0.25 s (4 FPS equivalent). Set to <see cref="double.MaxValue"/> to disable.
    /// </summary>
    public double MaxDeltaSeconds { get; set; } = 0.25;

    /// <summary>Total wall-clock seconds since the app started.</summary>
    public double ElapsedSeconds { get; private set; }

    /// <summary>
    /// Seconds since previous frame, clamped to <see cref="MaxDeltaSeconds"/>.
    /// Use this for gameplay and animation.
    /// </summary>
    public double DeltaSeconds { get; private set; }

    /// <summary>Un-clamped seconds since previous frame (raw wall-clock measurement).</summary>
    public double RawDeltaSeconds { get; private set; }

    /// <summary>Total number of frames elapsed.</summary>
    public ulong FrameCount { get; private set; }

    /// <summary>
    /// Exponentially smoothed frames-per-second estimate.
    /// Uses a smoothing factor of 0.9 to dampen per-frame jitter.
    /// </summary>
    public double SmoothedFps { get; private set; }

    /// <summary>Instantaneous FPS derived from <see cref="DeltaSeconds"/>. Zero when delta is zero.</summary>
    public double Fps => DeltaSeconds > 0.0 ? 1.0 / DeltaSeconds : 0.0;

    /// <summary>Called by <see cref="TimePlugin"/> once per frame to advance timing state.</summary>
    /// <param name="elapsedSeconds">Total wall-clock seconds since the app started.</param>
    /// <param name="rawDelta">Un-clamped seconds since the previous frame.</param>
    internal void Update(double elapsedSeconds, double rawDelta)
    {
        ElapsedSeconds = elapsedSeconds;

        RawDeltaSeconds = Math.Max(0.0, rawDelta);
        DeltaSeconds = Math.Min(RawDeltaSeconds, MaxDeltaSeconds);

        FrameCount++;

        // Exponential moving average: smoothFps = lerp(smoothFps, instantFps, 0.1)
        double instantFps = DeltaSeconds > 0.0 ? 1.0 / DeltaSeconds : SmoothedFps;
        SmoothedFps = FrameCount == 1
            ? instantFps
            : SmoothedFps * 0.9 + instantFps * 0.1;
    }

    /// <summary>Human-readable summary for diagnostics.</summary>
    public override string ToString()
        => $"Time {{ Frame={FrameCount}, Elapsed={ElapsedSeconds:F2}s, Delta={DeltaSeconds * 1000.0:F2}ms, FPS={SmoothedFps:F0} }}";
}
