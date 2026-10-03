namespace Engine;

/// <summary>
/// Registers the BepuPhysics v2 implementation of <see cref="PhysicsWorld"/>, the per-frame
/// physics step in <see cref="Stage.PreUpdate"/>, the transform write-back in <see cref="Stage.PostUpdate"/>,
/// and disposes the simulation in <see cref="Stage.Cleanup"/>.
/// </summary>
/// <remarks>
/// Add explicitly via <c>app.AddPlugin(new PhysicsPlugin())</c>; included in
/// <see cref="DefaultPlugins"/> so games get rigid-body physics out-of-the-box.
/// To override <see cref="PhysicsSettings"/>, insert the resource before adding this plugin:
/// <code>
/// app.World.InsertResource(new PhysicsSettings { Gravity = new Vector3(0, -20f, 0) });
/// app.AddPlugin(new PhysicsPlugin());
/// </code>
/// </remarks>
/// <example>
/// <code>
/// [Behavior]
/// public partial struct Player
/// {
///     public PhysicsBody Body;
///
///     [OnStartup]
///     public static void Spawn(BehaviorContext ctx)
///     {
///         var e = ctx.Ecs.Spawn();
///         var body = ctx.Physics.CreateCapsule(new Vector3(0, 5, 0), 0.5f, 1f);
///         ctx.Ecs.Add(e, new Player { Body = body });
///     }
/// }
/// </code>
/// </example>
public sealed class PhysicsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Physics.Bepu");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("PhysicsPlugin: initialising BepuPhysics v2 backend...");
        var settings = app.World.GetOrInsertResource(() => new PhysicsSettings());
        var world = new PhysicsWorld(settings);
        app.World.InsertResource(world);

        // With FixedTime (TimePlugin adds it), the simulation advances one step per FixedUpdate run,
        // on the same steps as [OnFixedUpdate] behaviors, so a behavior that pushes a body pushes it
        // once per step. Without it, PhysicsWorld keeps its own accumulator and steps in PreUpdate.
        // Contacts are sent as events after each step and kept until the next frame starts, so
        // code in any stage of the frame reads every contact of the frame's steps once.
        app.AddSystem(Stage.First, new SystemDescriptor(static w =>
            {
                w.ClearEvents<ContactStarted>();
                w.ClearEvents<ContactEnded>();
            }, "Physics.ClearContacts")
            .Write<Events<ContactStarted>>()
            .Write<Events<ContactEnded>>());

        app.AddSystem(Stage.FixedUpdate, new SystemDescriptor(static w =>
            {
                if (!w.TryGetResource<FixedTime>(out var fixedTime)) return;
                var phys = Prepared(w);
                phys.StepOnce((float)fixedTime.StepSeconds);
                SendContacts(w, phys);
            }, "Physics.FixedStep")
            .Read<FixedTime>()
            .Write<PhysicsWorld>()
            .Write<Events<ContactStarted>>()
            .Write<Events<ContactEnded>>()
            .MainThreadOnly());

        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(static w =>
            {
                if (w.ContainsResource<FixedTime>()) return;
                var phys = Prepared(w);
                var time = w.Resource<Time>();
                phys.Step((float)time.DeltaSeconds);
                SendContacts(w, phys);
            }, "Physics.Step")
            .Read<Time>()
            .Write<PhysicsWorld>()
            .Write<Events<ContactStarted>>()
            .Write<Events<ContactEnded>>()
            .MainThreadOnly());

        app.AddSystem(Stage.PostUpdate, new SystemDescriptor(static w =>
            {
                var phys = w.Resource<PhysicsWorld>();
                var ecs = w.Resource<EcsWorld>();
                var alpha = w.Resource<PhysicsSettings>().Interpolate && w.TryGetResource<FixedTime>(out var fixedTime)
                    ? (float)fixedTime.Alpha
                    : 1f;
                phys.SyncTransforms(ecs, alpha);
            }, "Physics.SyncTransforms")
            .Read<PhysicsWorld>()
            .Write<EcsWorld>());

        Logger.Info("PhysicsPlugin: physics systems registered (FixedUpdate=Step, PostUpdate=SyncTransforms).");
    }

    // Reused by the step systems, which run on the main thread only.
    private static readonly List<PhysicsContact> Started = [];
    private static readonly List<PhysicsContact> Ended = [];

    // The physics world, able to name the entities of the contacts its next step finds.
    private static PhysicsWorld Prepared(World w)
    {
        var phys = w.Resource<PhysicsWorld>();
        if (phys.EntityHandle is null && w.TryGetResource<EcsWorld>(out var ecs))
            phys.EntityHandle = ecs.Handle;
        return phys;
    }

    private static void SendContacts(World w, PhysicsWorld phys)
    {
        Started.Clear();
        Ended.Clear();
        phys.TakeContacts(Started, Ended);
        if (Started.Count > 0)
        {
            var events = Events.Get<ContactStarted>(w);
            foreach (var c in Started) events.Send(new ContactStarted(c.A, c.B, c.BodyA, c.BodyB));
        }
        if (Ended.Count > 0)
        {
            var events = Events.Get<ContactEnded>(w);
            foreach (var c in Ended) events.Send(new ContactEnded(c.A, c.B, c.BodyA, c.BodyB));
        }
    }
}