namespace Engine;

/// <summary>Marks a struct as an ECS Behavior; methods with stage attributes will be scheduled by the generator.</summary>
/// <example>
/// <code>
/// // A static method runs once a frame, as logic over the whole game does
/// [Behavior]
/// public partial struct PlayerMovement
/// {
///     [OnStartup]
///     public static void Init(BehaviorContext ctx)
///         => ctx.Cmd.Spawn((id, ecs) => ecs.Add(id, new Position()));
///
///     [OnUpdate]
///     public static void Move(BehaviorContext ctx)
///     {
///         float dt = (float)ctx.Time.DeltaSeconds;
///         foreach (var rc in ctx.Ecs.QueryRef&lt;Position&gt;())
///             rc.Component.X += 10f * dt;
///     }
/// }
/// </code>
/// <code>
/// // Local fields make the behavior both a component and a system.
/// // Instance methods run per entity that has this behavior component.
/// [Behavior]
/// public partial struct Spawner
/// {
///     public float Timer;
///
///     [OnStartup]
///     public static void Init(BehaviorContext ctx)
///     {
///         var e = ctx.Ecs.Spawn();
///         ctx.Ecs.Add(e, new Spawner { Timer = 0f });
///     }
///
///     [OnUpdate]
///     public void Tick(BehaviorContext ctx)
///     {
///         Timer += (float)ctx.Time.DeltaSeconds;
///         Console.WriteLine($"Entity {ctx.EntityId} timer: {Timer:F2}s");
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class BehaviorAttribute : Attribute;
