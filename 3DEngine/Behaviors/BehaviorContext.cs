namespace Engine;

/// <summary>
/// Per-invocation context providing convenient access to the <see cref="World"/>,
/// <see cref="EcsWorld"/>, <see cref="EcsCommands"/>, <see cref="Time"/>, and <see cref="Input"/>
/// resources, plus the current <see cref="EntityId"/> being processed.
/// </summary>
/// <remarks>
/// Created once per system invocation by the source-generated behavior runner.
/// Systems that iterate over entities set <see cref="EntityId"/> for each entity before
/// calling the behavior method.
/// </remarks>
/// <example>
/// <code>
/// [Behavior]
/// public partial struct PlayerMovement
/// {
///     [OnUpdate]
///     public static void Move(BehaviorContext ctx)
///     {
///         float dt = (float)ctx.Time.DeltaSeconds;
///         var ecs = ctx.Ecs;
///
///         foreach (var (e, pos, vel) in ecs.Query&lt;Position, Velocity&gt;())
///             ecs.Update(e, new Position(pos.X + vel.X * dt, pos.Y + vel.Y * dt));
///
///         // Deferred spawn via commands (applied in PostUpdate)
///         if (ctx.Input.KeyPressed(Key.Space))
///             ctx.Cmd.Spawn((id, w) => w.Add(id, new Projectile()));
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="BehaviorAttribute"/>
/// <seealso cref="EcsWorld"/>
/// <seealso cref="EcsCommands"/>
public sealed class BehaviorContext
{
    /// <summary>Global state bag (resources and ECS world).</summary>
    public World World { get; }

    /// <summary>Direct access to the ECS world (components/entities).</summary>
    public EcsWorld Ecs { get; }

    /// <summary>Buffered ECS commands applied after systems run (spawn/despawn/add/remove).</summary>
    public EcsCommands Cmd { get; }
    
    /// <summary>Frame timing data (delta time, elapsed, FPS).</summary>
    public Time Time { get; }
    
    /// <summary>Current frame input state (keyboard, mouse, text).</summary>
    public Input Input { get; }

    private PhysicsWorld? _physics;

    /// <summary>Active physics world (rigid bodies, raycasts, gravity).</summary>
    /// <remarks>
    /// Resolved when first read rather than when the context is made, so an app without
    /// <c>PhysicsPlugin</c> runs every behavior that does not touch physics.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The world has no <see cref="PhysicsWorld"/>.</exception>
    public PhysicsWorld Physics => _physics ??= World.Resource<PhysicsWorld>();

    /// <summary>Entity being processed for instance methods; <c>0</c> if not applicable.</summary>
    public int EntityId { get; set; }

    /// <summary>A handle to the entity being processed that can be kept across frames (see <see cref="Engine.Entity"/>).</summary>
    public Entity Entity => Ecs.Handle(EntityId);

    /// <summary>Creates a new <see cref="BehaviorContext"/> by resolving resources from the specified <paramref name="world"/>.</summary>
    /// <param name="world">The <see cref="World"/> from which to resolve ECS, commands, time and input.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if any required resource (<see cref="EcsWorld"/>, <see cref="EcsCommands"/>,
    /// <see cref="Time"/>, <see cref="Input"/>) is missing from the world.
    /// </exception>
    public BehaviorContext(World world)
    {
        World = world;
        Ecs = world.Resource<EcsWorld>();
        Cmd = world.Resource<EcsCommands>();
        Time = world.Resource<Time>();
        Input = world.Resource<Input>();
    }

    /// <summary>
    /// Fast constructor that accepts pre-resolved resource references, avoiding
    /// repeated dictionary lookups per construction.
    /// Used by source-generated behavior runners for the parallel iteration path where
    /// one context is created per chunk/thread.
    /// </summary>
    /// <param name="world">The world instance.</param>
    /// <param name="ecs">Pre-resolved <see cref="EcsWorld"/>.</param>
    /// <param name="cmd">Pre-resolved <see cref="EcsCommands"/>.</param>
    /// <param name="time">Pre-resolved <see cref="Time"/>.</param>
    /// <param name="input">Pre-resolved <see cref="Input"/>.</param>
    public BehaviorContext(World world, EcsWorld ecs, EcsCommands cmd, Time time, Input input)
    {
        World = world;
        Ecs = ecs;
        Cmd = cmd;
        Time = time;
        Input = input;
    }

    /// <summary>
    /// Seconds one fixed step simulates (<see cref="FixedTime.StepSeconds"/>), which an
    /// <c>[OnFixedUpdate]</c> method advances by. Without <see cref="FixedTime"/>, the frame's delta.
    /// </summary>
    public float FixedDelta => World.TryGetResource<FixedTime>(out var fixedTime)
        ? (float)fixedTime.StepSeconds
        : (float)Time.DeltaSeconds;

    /// <summary>Gets a typed resource from the world.</summary>
    /// <typeparam name="T">The resource type to retrieve.</typeparam>
    /// <returns>The resource instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource is not found.</exception>
    public T Res<T>() where T : notnull => World.Resource<T>();
}
