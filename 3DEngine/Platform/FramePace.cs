using System.Diagnostics;

namespace Engine;

/// <summary>
/// The most frames a second a program draws (<c>render.fps_limit</c>), beside the vertical sync,
/// which the window's loop and the flat API's frame end wait for.
/// </summary>
internal static class FramePace
{
    /// <summary>The most frames a second, or 0 for no limit.</summary>
    internal static int Limit { get; set; }

    /// <summary>
    /// The frames a second a program that asks for <paramref name="asked"/> is held to, the fewer
    /// of that and <see cref="Limit"/> where both are set, or whichever is, or 0 for neither.
    /// </summary>
    internal static double Of(double asked) => (asked, Limit) switch
    {
        ( > 0, > 0) => Math.Min(asked, Limit),
        ( > 0, _) => asked,
        _ => Limit,
    };

    /// <summary>
    /// Waits from <paramref name="last"/>, the time the frame before ended, through what is left of
    /// a frame at <paramref name="perSecond"/> frames a second, sleeping through most of it and
    /// spinning through the last millisecond, since a sleep on most systems overshoots by up to a
    /// millisecond, and returns the time the wait ended.
    /// </summary>
    internal static long Wait(long last, double perSecond)
    {
        if (perSecond > 0)
        {
            var deadline = last + (long)(Stopwatch.Frequency / perSecond);
            while (true)
            {
                var remaining = (deadline - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
                if (remaining <= 0) break;
                if (remaining > 2) Thread.Sleep((int)(remaining - 1));
                else Thread.SpinWait(64);
            }
        }
        return Stopwatch.GetTimestamp();
    }
}
