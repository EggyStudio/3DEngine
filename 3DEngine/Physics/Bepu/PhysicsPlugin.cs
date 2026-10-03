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

        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(static w =>
            {
                var phys = w.Resource<PhysicsWorld>();
                var time = w.Resource<Time>();
                phys.Step((float)time.DeltaSeconds);
            }, "Physics.Step")
            .Read<Time>()
            .Write<PhysicsWorld>()
            .MainThreadOnly());

        app.AddSystem(Stage.PostUpdate, new SystemDescriptor(static w =>
            {
                var phys = w.Resource<PhysicsWorld>();
                var ecs = w.Resource<EcsWorld>();
                phys.SyncTransforms(ecs);
            }, "Physics.SyncTransforms")
            .Read<PhysicsWorld>()
            .Write<EcsWorld>());

        Logger.Info("PhysicsPlugin: physics systems registered (PreUpdate=Step, PostUpdate=SyncTransforms).");
    }
}