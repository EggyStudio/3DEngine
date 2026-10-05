namespace Engine;

/// <summary>
/// The clock change detection runs on. Each system run takes the next tick, a write to a component
/// is stamped with the tick of the system making it, and a <c>Changed</c> filter in a system sees
/// the writes stamped after the tick that system last ran at. A system that runs less often than
/// once a frame, as one in <see cref="Stage.FixedUpdate"/> does at a high frame rate, or more often,
/// at a low one, then sees each change once.
/// </summary>
/// <remarks>
/// <para>
/// The schedule enters each system with <see cref="Enter"/> and leaves it with <see cref="Leave"/>,
/// on the thread that runs it, so systems running in parallel each have their own.
/// </para>
/// <para>
/// Outside a system, as in a program's own code between <c>BeginDrawing</c> and
/// <c>EndDrawing</c>, a test or a console command, a write is stamped with the tick after the
/// latest, so every system that has run sees it at its next run, and a <c>Changed</c> filter sees
/// what changed since <see cref="EcsWorld.BeginFrame"/>, as it did when changes lasted one frame.
/// </para>
/// <para>
/// The clock is one for the process, since ticks only order writes against runs, and two worlds
/// never compare theirs.
/// </para>
/// </remarks>
internal static class ChangeTicks
{
    private static long _latest;

    [ThreadStatic] private static long t_tick;
    [ThreadStatic] private static long t_since;

    /// <summary>The latest tick handed out.</summary>
    public static long Latest => Volatile.Read(ref _latest);

    /// <summary>The tick a write on the calling thread is stamped with.</summary>
    public static long ForWrite => t_tick != 0 ? t_tick : Volatile.Read(ref _latest) + 1;

    /// <summary>The tick after which a write counts as changed to a reader on the calling thread, given when its world's frame began.</summary>
    public static long Since(long frameStart) => t_tick != 0 ? t_since : frameStart;

    /// <summary>Whether the calling thread is running a system.</summary>
    public static bool InSystem => t_tick != 0;

    /// <summary>Takes the next tick, the one a frame begins at.</summary>
    internal static long Advance() => Interlocked.Increment(ref _latest);

    /// <summary>
    /// Starts a system run on this thread, which last ran at <paramref name="lastRun"/> (0 for
    /// never), and returns what <see cref="Leave"/> restores, so a system that runs another nests.
    /// </summary>
    public static (long Tick, long Since) Enter(long lastRun, out long tick)
    {
        var outer = (t_tick, t_since);
        tick = Interlocked.Increment(ref _latest);
        t_tick = tick;
        t_since = lastRun;
        return outer;
    }

    /// <summary>Ends a system run on this thread, restoring what <see cref="Enter"/> returned.</summary>
    public static void Leave((long Tick, long Since) outer) => (t_tick, t_since) = outer;
}
