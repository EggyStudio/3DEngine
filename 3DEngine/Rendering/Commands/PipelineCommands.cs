using System.Diagnostics;

namespace Engine;

/// <summary>A command that waits for the pipelines the frames asked for, so a script reads a pixel after them.</summary>
internal static class PipelineCommands
{
    private static readonly ILogger Logger = Log.Category("Engine.Renderer");

    [Command("pipelines.wait", "Answers once no pipeline is being made, none asked of the workers and not yet made and none in the driver on any thread, or once this many seconds have passed, 60 unless given, saying which, as a script that reads a pixel after the first sight of a material waits before it reads: pipelines.wait [seconds]")]
    internal static string Wait(double seconds = 60)
    {
        var world = ConsoleHost.World!;
        var started = Stopwatch.GetTimestamp();
        var from = ConsoleHost.Time.FrameCount;
        // A frame that asks for a pipeline starts it on a worker or makes it on its own thread, so
        // the count is read between frames, after the frame that asked. The frames it is given are
        // a thousand a second of the wait and more, so the seconds run out first.
        ConsoleHost.Later(() =>
        {
            if (!world.TryGetResource<Renderer>(out var renderer) || renderer.Context.Graphics is not GraphicsDevice device)
                return "no device makes pipelines, so nothing was waited for";
            var asked = renderer.RenderWorld.TryGet<PipelineCache>()?.StillMaking ?? 0;
            var inFlight = device.PipelinesInFlight;
            var waited = Stopwatch.GetElapsedTime(started);
            var frames = ConsoleHost.Time.FrameCount - from;
            if (asked == 0 && inFlight == 0)
                return $"no pipeline is being made, after {frames} frames and {waited.TotalSeconds:0.0} s";
            if (waited.TotalSeconds < seconds) return null;
            var said = $"after {waited.TotalSeconds:0.0} s {asked} pipelines asked of the workers were not made and {inFlight} were in the driver";
            Logger.Warn(said);
            return said;
        }, (ulong)(Math.Max(seconds, 1) * 1000) + ConsoleHost.LaterFrames);
        return "waiting for the pipelines being made";
    }
}
