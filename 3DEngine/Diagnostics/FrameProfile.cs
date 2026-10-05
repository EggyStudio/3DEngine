using System.Text;

namespace Engine;

/// <summary>
/// Where the time of a frame goes, averaged over about the last second of frames. It holds the
/// frame, the work in it (the schedule's stages, which leave out the wait for the target frame
/// rate), each stage and system, the renderer's phases and prepare systems, and each render graph
/// node on the CPU and, where the device writes timestamps, on the GPU. A program adds numbers of its own with
/// <see cref="Engine3D.SetProfileValue"/>, as a stress test's count.
/// </summary>
/// <remarks>
/// <para>
/// A program on the flat API runs its own code between the schedule's stages, which the profile
/// shows as <c>program.update</c> (from the end of one frame to <c>BeginDrawing</c>) and
/// <c>program.drawing</c> (from <c>BeginDrawing</c> to <c>EndDrawing</c>), and the wait for the
/// target frame rate as <c>wait</c>.
/// </para>
/// <para>Read with <c>e3d command profile</c>, or <see cref="Report"/> in a program.</para>
/// </remarks>
public sealed class FrameProfile
{
    // Each value's average, kept with a weight that halves a value's say after about 40 frames.
    private const double Weight = 1.0 / 60;
    private readonly Dictionary<string, double> _averages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    // Every value of the frame being measured, and of the slowest frame since it was last read,
    // since a stall of one frame is lost in the averages.
    private Dictionary<string, double> _frame = new(StringComparer.Ordinal);
    private Dictionary<string, double> _slowest = new(StringComparer.Ordinal);

    /// <summary>How many frames have been measured.</summary>
    public long Frames { get; private set; }

    /// <summary>The average of a measured value in milliseconds, or 0 before it was measured.</summary>
    public double Average(string name)
    {
        lock (_lock) return _averages.GetValueOrDefault(name);
    }

    /// <summary>Sets a number of the program's own that the report shows as it is.</summary>
    public void Set(string name, double value)
    {
        lock (_lock) _values[name] = value;
    }

    /// <summary>Forgets every average, so a measurement starts afresh.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _averages.Clear();
            Frames = 0;
        }
    }

    internal void Add(string name, double milliseconds)
    {
        lock (_lock)
        {
            _averages[name] = _averages.TryGetValue(name, out var average) ? average + (milliseconds - average) * Weight : milliseconds;
            _frame[name] = _frame.GetValueOrDefault(name) + milliseconds;
        }
    }

    internal void EndFrame()
    {
        lock (_lock)
        {
            Frames++;
            if (_frame.GetValueOrDefault("frame") > _slowest.GetValueOrDefault("frame")) (_slowest, _frame) = (_frame, _slowest);
            _frame.Clear();
        }
    }

    /// <summary>
    /// The slowest frame since this was last called, every value measured in it, largest first
    /// within each group, and forgets it, so the next call reports the frames after this one.
    /// </summary>
    /// <remarks>
    /// The averages smooth a stall of one frame away, so a frame of a quarter second among frames
    /// of 16 milliseconds shows here with the stage or the wait that held it.
    /// </remarks>
    public string Slowest()
    {
        lock (_lock)
        {
            if (_slowest.Count == 0) return "no frame measured since the last call";
            var text = new StringBuilder();
            foreach (var group in _slowest.GroupBy(a => a.Key.Split('.')[0]).OrderBy(g => g.Key == "frame" ? "" : g.Key, StringComparer.Ordinal))
                foreach (var (name, ms) in group.OrderByDescending(a => a.Value)) text.Append($"{name} {ms:0.000} ms\n");
            _slowest.Clear();
            return text.ToString().TrimEnd();
        }
    }

    /// <summary>The program's values, then every average in milliseconds, largest first within each group.</summary>
    public string Report()
    {
        var (values, groups) = Snapshot();
        var text = new StringBuilder();
        text.Append($"frames {Frames}\n");
        foreach (var (name, value) in values) text.Append($"{name} {value:0.###}\n");
        foreach (var (_, averages) in groups)
            foreach (var (name, average) in averages) text.Append($"{name} {average:0.000} ms\n");
        return text.ToString().TrimEnd();
    }

    /// <summary>
    /// The program's values by name, and the averages in groups by the part of their name before
    /// the first dot (<c>frame</c> first, then <c>cpu</c>, <c>gpu</c>, <c>stage</c>, <c>system</c>
    /// and the rest by name), largest first within each.
    /// </summary>
    public (IReadOnlyList<(string Name, double Value)> Values, IReadOnlyList<(string Group, IReadOnlyList<(string Name, double Milliseconds)> Averages)> Groups) Snapshot()
    {
        lock (_lock)
        {
            var values = _values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => (v.Key, v.Value)).ToArray();
            var groups = _averages.GroupBy(a => a.Key.Split('.')[0])
                .OrderBy(g => g.Key == "frame" ? "" : g.Key, StringComparer.Ordinal)
                .Select(g => (g.Key, (IReadOnlyList<(string, double)>)g.OrderByDescending(a => a.Value).Select(a => (a.Key, a.Value)).ToArray()))
                .ToArray();
            return (values, groups);
        }
    }
}

/// <summary>Measures every frame into the <see cref="FrameProfile"/> resource, at the start of the next.</summary>
public sealed class FrameProfilePlugin : IPlugin
{
    /// <inheritdoc />
    public void Build(App app)
    {
        app.World.InsertResource(new FrameProfile());
        app.AddSystem(Stage.First, new SystemDescriptor(Measure, "Diagnostics.FrameProfile")
            .Read<ScheduleDiagnostics>()
            .Write<FrameProfile>()
            .MainThreadOnly());
    }

    private static TimeSpan _paused = GC.GetTotalPauseDuration();

    // The frame just ended, read before the schedule overwrites its numbers with this frame's.
    private static void Measure(World world)
    {
        var profile = world.Resource<FrameProfile>();
        if (world.TryGetResource<Time>(out var time) && time.DeltaSeconds > 0) profile.Add("frame", time.DeltaSeconds * 1000);
        // The garbage collector's pauses in the frame, which land in whatever stage was running.
        var paused = GC.GetTotalPauseDuration();
        profile.Add("gc.pause", (paused - _paused).TotalMilliseconds);
        _paused = paused;

        if (world.TryGetResource<ScheduleDiagnostics>(out var schedule))
        {
            double work = 0;
            foreach (var (stage, duration) in schedule.StageDurations)
            {
                if (stage is Stage.Startup or Stage.Cleanup) continue;
                work += duration.TotalMilliseconds;
                profile.Add($"stage.{stage}", duration.TotalMilliseconds);
            }
            profile.Add("work", work);
            foreach (var ((stage, system), duration) in schedule.SystemDurations)
                if (stage is not (Stage.Startup or Stage.Cleanup)) profile.Add($"system.{stage}.{system}", duration.TotalMilliseconds);
        }

        if (world.TryGetResource<Renderer>(out var renderer))
        {
            var t = renderer.Timings;
            profile.Add("render.extract", t.ExtractMs);
            profile.Add("render.beginframe", t.BeginFrameMs);
            profile.Add("render.prepare", t.PrepareMs);
            profile.Add("render.graph", t.GraphMs);
            profile.Add("render.endframe", t.EndFrameMs);
            // The three waits a frame can make on the device and the display, which begin and end
            // frame hold between them.
            if (renderer.Context.Graphics is GraphicsDevice device)
            {
                profile.Add("render.fence", device.FenceWaitMs);
                profile.Add("render.acquire", device.AcquireMs);
                profile.Add("render.present", device.PresentMs);
            }
            foreach (var (system, ms) in t.PrepareCpu) profile.Add($"prepare.{system}", ms);
            foreach (var (node, ms) in t.NodeCpu) profile.Add($"cpu.{node}", ms);
            foreach (var (node, ms) in t.NodeGpu) profile.Add($"gpu.{node}", ms);
        }
        profile.EndFrame();
    }
}
