namespace Engine;

/// <summary>Provides core ECS resources (<see cref="EcsWorld"/>, <see cref="EcsCommands"/>) and flushes the command buffer each <see cref="Stage.PostUpdate"/>.</summary>
/// <remarks>
/// This plugin initialises the <see cref="EcsWorld"/> and <see cref="EcsCommands"/> resources and
/// registers a <see cref="Stage.PostUpdate"/> system that calls <see cref="EcsCommands.Apply"/>
/// to flush deferred spawn/despawn/add/remove operations in FIFO order.
/// </remarks>
/// <example>
/// <code>
/// // Spawn entities and add components via deferred commands
/// [Behavior]
/// public partial struct Spawner
/// {
///     [OnStartup]
///     public static void Init(BehaviorContext ctx)
///     {
///         ctx.Cmd.Spawn((id, ecs) =>
///         {
///             ecs.Add(id, new Position { X = 0, Y = 0 });
///             ecs.Add(id, new Health { Current = 100, Max = 100 });
///         });
///     }
/// }
/// </code>
/// <code>
/// // Query and iterate entities in a system
/// [Behavior]
/// public partial struct MovementSystem
/// {
///     [OnUpdate]
///     public static void Move(BehaviorContext ctx)
///     {
///         float dt = (float)ctx.Time.DeltaSeconds;
///         foreach (var rc in ctx.Ecs.QueryRef&lt;Position&gt;())
///             rc.Component.X += 10f * dt;
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="EcsWorld"/>
/// <seealso cref="EcsCommands"/>
/// <seealso cref="BehaviorsPlugin"/>
public sealed class EcsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.ECS");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("EcsPlugin: Registering EcsWorld and EcsCommands resources...");
        app.World.InitResource<EcsWorld>();
        app.World.InitResource<EcsCommands>();

        app.AddSystem(Stage.First, new SystemDescriptor(world =>
            {
                world.Resource<EcsWorld>().BeginFrame();
            }, "DefaultPlugins.EcsBeginFrame")
            .Write<EcsWorld>());
        Logger.Info("EcsPlugin: EcsWorld frame-begin system registered to First stage.");

        app.AddSystem(Stage.PostUpdate, new SystemDescriptor(world =>
            {
                world.Resource<EcsCommands>().Apply(world.Resource<EcsWorld>());
            }, "EcsPlugin.FlushCommands")
            .Write<EcsCommands>()
            .Write<EcsWorld>());
        Logger.Info("EcsPlugin: ECS command flush system registered to PostUpdate stage.");

        // Code-generated [Behavior] layer that registers systems against the ECS world.
        app.AddPlugin(new BehaviorsPlugin());
    }
}
