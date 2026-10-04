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
            _averages[name] = _averages.TryGetValue(name, out var average) ? average + (milliseconds - average) * Weight : milliseconds;
    }

    internal void EndFrame()
    {
        lock (_lock) Frames++;
    }

    /// <summary>The program's values, then every average in milliseconds, largest first within each group.</summary>
    public string Report()
    {
        lock (_lock)
        {
            var text = new StringBuilder();
            text.Append($"frames {Frames}\n");
            foreach (var (name, value) in _values.OrderBy(v => v.Key, StringComparer.Ordinal))
                text.Append($"{name} {value:0.###}\n");
            foreach (var group in _averages.GroupBy(a => a.Key.Split('.')[0]).OrderBy(g => g.Key == "frame" ? "" : g.Key, StringComparer.Ordinal))
                foreach (var (name, average) in group.OrderByDescending(a => a.Value))
                    text.Append($"{name} {average:0.000} ms\n");
            return text.ToString().TrimEnd();
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

    // The frame just ended, read before the schedule overwrites its numbers with this frame's.
    private static void Measure(World world)
    {
        var profile = world.Resource<FrameProfile>();
        if (world.TryGetResource<Time>(out var time) && time.DeltaSeconds > 0) profile.Add("frame", time.DeltaSeconds * 1000);

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
            foreach (var (system, ms) in t.PrepareCpu) profile.Add($"prepare.{system}", ms);
            foreach (var (node, ms) in t.NodeCpu) profile.Add($"cpu.{node}", ms);
            foreach (var (node, ms) in t.NodeGpu) profile.Add($"gpu.{node}", ms);
        }
        profile.EndFrame();
    }
}
