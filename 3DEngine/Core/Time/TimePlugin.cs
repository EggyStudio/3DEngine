using System.Diagnostics;

namespace Engine;

/// <summary>
/// Tracks wall-clock time since start, per-frame delta, frame count, and smoothed FPS.
/// Updates during the <see cref="Stage.First"/> stage each frame.
/// </summary>
/// <remarks>
/// Registers a <see cref="Time"/> resource in the world and a system that measures
/// elapsed wall-clock time using <see cref="System.Diagnostics.Stopwatch"/>.
/// The system clamps large deltas (e.g., debugger pauses) via <see cref="Time.MaxDeltaSeconds"/>.
/// </remarks>
/// <example>
/// <code>
/// // Access frame timing inside a behavior
/// [Behavior]
/// public partial struct TimingDemo
/// {
///     [OnUpdate]
///     public static void Tick(BehaviorContext ctx)
///     {
///         float dt = (float)ctx.Time.DeltaSeconds;
///         Console.WriteLine($"FPS: {ctx.Time.SmoothedFps:F0}, delta: {dt * 1000:F1}ms");
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="Time"/>
/// <seealso cref="Stage.First"/>
public sealed class TimePlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Time");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("TimePlugin: Registering Time resource and frame-timing system.");
        app.World.InitResource<Time>();

        var watch = Stopwatch.StartNew();
        double lastElapsed = 0.0;

        app.AddSystem(Stage.First, new SystemDescriptor(world =>
            {
                double now = watch.Elapsed.TotalSeconds;
                double rawDelta = now - lastElapsed;
                lastElapsed = now;
            
                var time = world.Resource<Time>();
                time.Update(now, rawDelta);
            }, "TimePlugin.Update")
            .Write<Time>());
    }
}
