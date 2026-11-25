using System.Text;

namespace Engine;

/// <summary>Measures every frame into the <see cref="FrameProfile"/> resource, at the start of the next.</summary>
internal sealed class FrameProfilePlugin : IPlugin
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

    // The names the frame's values are kept under, each made the first time it is met, so a frame
    // formats none, and the stage and system durations it reads, refilled each frame.
    private static readonly Dictionary<Stage, string> StageKeys = Enum.GetValues<Stage>().ToDictionary(stage => stage, stage => $"stage.{stage}");
    private readonly Dictionary<(Stage, string), string> _systemKeys = [];
    private readonly Dictionary<string, string> _prepareKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _cpuKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _gpuKeys = new(StringComparer.Ordinal);
    private readonly List<(Stage Stage, TimeSpan Elapsed)> _stages = [];
    private readonly List<(Stage Stage, string System, TimeSpan Elapsed)> _systems = [];

    private static string Key(Dictionary<string, string> keys, string prefix, string name)
    {
        if (!keys.TryGetValue(name, out var key)) keys[name] = key = prefix + name;
        return key;
    }

    // Each named time of a list the renderer fills, read by index, since a foreach over the list's
    // interface would make an enumerator each frame.
    private static void AddEach(FrameProfile profile, Dictionary<string, string> keys, string prefix, IReadOnlyList<(string Name, double Milliseconds)> times)
    {
        for (int i = 0; i < times.Count; i++)
            profile.Add(Key(keys, prefix, times[i].Name), times[i].Milliseconds);
    }

    // The frame that ended, read before the schedule overwrites its numbers with this frame's.
    private void Measure(World world)
    {
        var profile = world.Resource<FrameProfile>();
        if (world.TryGetResource<Time>(out var time) && time.DeltaSeconds > 0) profile.Add("frame", time.DeltaSeconds * 1000);
        // The garbage collector's pauses in the frame, which land in whatever stage was running.
        var paused = GC.GetTotalPauseDuration();
        profile.Add("gc.pause", (paused - _paused).TotalMilliseconds);
        _paused = paused;

        if (world.TryGetResource<ScheduleDiagnostics>(out var schedule))
        {
            schedule.CopyDurations(_stages, _systems);
            double work = 0;
            foreach (var (stage, duration) in _stages)
            {
                if (stage is Stage.Startup or Stage.Cleanup) continue;
                work += duration.TotalMilliseconds;
                profile.Add(StageKeys[stage], duration.TotalMilliseconds);
            }
            profile.Add("work", work);
            foreach (var (stage, system, duration) in _systems)
            {
                if (stage is Stage.Startup or Stage.Cleanup) continue;
                if (!_systemKeys.TryGetValue((stage, system), out var key)) _systemKeys[(stage, system)] = key = $"system.{stage}.{system}";
                profile.Add(key, duration.TotalMilliseconds);
            }
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
            // frame hold between them. A headless app has a renderer and no device, whose Graphics
            // throws, so it is asked only once the device is up.
            if (renderer.Context.IsInitialized && renderer.Context.Graphics is GraphicsDevice device)
            {
                profile.Add("render.fence", device.FenceWaitMs);
                profile.Add("render.acquire", device.AcquireMs);
                profile.Add("render.present", device.PresentMs);
                // The batch ahead of the frame that fills the counted draws' vertex counts.
                if (device.MeshCountsCpuMs > 0)
                {
                    profile.Add("render.mesh_counts", device.MeshCountsCpuMs);
                    profile.Add("gpu.mesh_counts", device.MeshCountsGpuMs);
                }
            }
            AddEach(profile, _prepareKeys, "prepare.", t.PrepareCpu);
            AddEach(profile, _cpuKeys, "cpu.", t.NodeCpu);
            AddEach(profile, _gpuKeys, "gpu.", t.NodeGpu);
        }
        profile.EndFrame();
    }
}
