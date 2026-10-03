using System.Diagnostics;

namespace Engine;

/// <summary>
/// Drives frames with no window: paced at a fixed rate so a headless app does not spin a core, and
/// ended when <see cref="AppExit.Requested"/> is set, by <c>app.quit</c>, by <c>--frames</c> or by
/// the program.
/// </summary>
/// <remarks>
/// A headless run has no events to process, so <see cref="PumpEvents"/> only reports whether the
/// app has been asked to close.
/// </remarks>
public sealed class HeadlessLoopDriver(World world, double fps) : IMainLoopDriver
{
    private readonly long _frameTicks = fps > 0 ? (long)(Stopwatch.Frequency / fps) : 0;
    private long _last = Stopwatch.GetTimestamp();

    /// <inheritdoc />
    public void Run(Action frameStep)
    {
        while (PumpEvents())
        {
            frameStep();
            Pace();
        }
    }

    /// <inheritdoc />
    public bool PumpEvents() => !(world.TryGetResource<AppExit>(out var exit) && exit.Requested);

    /// <summary>Waits out the rest of the frame at the paced rate.</summary>
    public void Pace()
    {
        if (_frameTicks > 0)
        {
            var remaining = _last + _frameTicks - Stopwatch.GetTimestamp();
            if (remaining > 0) Thread.Sleep(TimeSpan.FromTicks(remaining * TimeSpan.TicksPerSecond / Stopwatch.Frequency));
        }
        _last = Stopwatch.GetTimestamp();
    }
}
