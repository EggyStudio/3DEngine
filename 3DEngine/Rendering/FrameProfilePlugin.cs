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

    // The frame that ended, read before the schedule overwrites its numbers with this frame's.
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
            foreach (var (system, ms) in t.PrepareCpu) profile.Add($"prepare.{system}", ms);
            foreach (var (node, ms) in t.NodeCpu) profile.Add($"cpu.{node}", ms);
            foreach (var (node, ms) in t.NodeGpu) profile.Add($"gpu.{node}", ms);
        }
        profile.EndFrame();
    }
}
