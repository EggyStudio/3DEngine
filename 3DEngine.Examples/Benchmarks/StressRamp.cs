using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>
/// Finds how many of something a frame holds at 60 frames a second. The count grows by half at a
/// time until a frame takes longer than a sixtieth of a second, then halves the distance between
/// the last count that held and the first that did not until the two are within about 3 percent.
/// </summary>
/// <remarks>
/// Each count is measured over at least half a second of frames, after a few frames for the new
/// things to settle, and run without a target frame rate, so a frame's time is its cost. The
/// counts and the result go to the frame profile, where <c>e3d command profile</c> reads them.
/// </remarks>
public sealed class StressRamp(int start)
{
    private const double Budget = 1.0 / 60;
    private int _held, _failed = -1, _frames;
    private double _seconds;

    /// <summary>How many there should be this frame.</summary>
    public int Count { get; private set; } = start;

    /// <summary>The largest count measured inside the budget, once the search has ended, or 0 before.</summary>
    public int Limit { get; private set; }

    /// <summary>The last measured count's frame time in milliseconds.</summary>
    public double FrameMs { get; private set; }

    /// <summary>Adds the frame that just ended and moves the count when a measurement is complete.</summary>
    public void Measure()
    {
        if (Limit > 0 || ++_frames <= 10) return;
        _seconds += GetFrameTime();
        if (_frames - 10 < 20 || _seconds < 0.5) return;

        FrameMs = _seconds / (_frames - 10) * 1000;
        if (FrameMs <= Budget * 1000) _held = Count;
        else _failed = Count;
        _frames = 0;
        _seconds = 0;

        if (_failed < 0) Count = Count * 3 / 2;
        else if (_failed - _held <= Math.Max(1, _failed / 32))
        {
            Limit = Math.Max(_held, 1);
            Count = Limit;
            Console.WriteLine($"{Example.Current}: {Limit} held 60 frames a second, {_failed} did not");
        }
        else Count = (_held + _failed) / 2;

        SetProfileValue("count", Count);
        SetProfileValue("limit", Limit);
        SetProfileValue("measured.ms", FrameMs);
    }
}
