namespace Engine;

/// <summary>
/// Schedules systems into stages and executes them.
/// Stages run sequentially in a fixed order; within a stage, systems run in parallel by default.
/// </summary>
/// <remarks>
/// <para>
/// Features: named system descriptors, optional <c>run_if</c>-style conditions,
/// per-system exception isolation, removal/introspection APIs, and frame-level
/// <see cref="ScheduleDiagnostics"/>.
/// </para>
/// <para>
/// <b>Parallelism model:</b> Each stage's systems are partitioned into execution batches.
/// Systems that share no conflicting resource access (based on <see cref="SystemDescriptor.Read{T}"/>
/// and <see cref="SystemDescriptor.Write{T}"/> metadata) run in parallel within a batch.
/// Systems lacking metadata are conservatively serialized.
/// </para>
/// <para>
/// <b>Default threading:</b> <see cref="Stage.Startup"/>, <see cref="Stage.Render"/>, and
/// <see cref="Stage.Cleanup"/> are single-threaded by default. All other stages are parallel.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var schedule = new Schedule();
/// schedule.AddSystem(Stage.Update, static world =>
/// {
///     var time = world.Resource&lt;Time&gt;();
///     Console.WriteLine($"Frame delta: {time.DeltaSeconds:F4}s");
/// });
/// </code>
/// <code>
/// schedule.AddSystem(Stage.Update, PhysicsStep, world =>
///     world.ContainsResource&lt;PhysicsWorld&gt;());
/// </code>
/// <code>
/// schedule.RunStage(Stage.Update, world);
/// </code>
/// </example>
/// <seealso cref="Stage"/>
/// <seealso cref="StageOrder"/>
/// <seealso cref="SystemDescriptor"/>
/// <seealso cref="ScheduleDiagnostics"/>
public sealed partial class Schedule
{
    private static readonly ILogger Logger = Log.Category("Engine.Schedule");

    private readonly Lock _lock = new();
    private readonly Dictionary<Stage, List<SystemDescriptor>> _systemsByStage = new();
    private readonly HashSet<Stage> _parallelStages = [];
    private readonly HashSet<string> _missingAccessWarnings = [];

    /// <summary>Per-stage and per-system timing recorded during execution.</summary>
    /// <seealso cref="ScheduleDiagnostics"/>
    public ScheduleDiagnostics Diagnostics { get; } = new();

    /// <summary>
    /// Initializes a new <see cref="Schedule"/> with all stages pre-registered.
    /// <see cref="Stage.Startup"/>, <see cref="Stage.Render"/>, and <see cref="Stage.Cleanup"/>
    /// are configured as single-threaded; all other stages default to parallel.
    /// </summary>
    public Schedule()
    {
        foreach (var stage in StageOrder.AllInOrder())
        {
            _systemsByStage[stage] = [];
            _parallelStages.Add(stage);
        }
        SetSingleThreaded(Stage.Startup);
        SetSingleThreaded(Stage.Render);
        SetSingleThreaded(Stage.Cleanup);
        Logger.Trace("Schedule created - all stages initialized, Startup, Render, and Cleanup stages set to single-threaded.");
    }
}
