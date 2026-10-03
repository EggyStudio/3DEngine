namespace Engine;

/// <summary>Marks a method for registration by the generator; must be used on methods in a struct with <see cref="BehaviorAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class GeneratedBehaviorRegistrationAttribute : Attribute;

/// <summary>Marks a struct as an ECS Behavior; methods with stage attributes will be scheduled by the generator.</summary>
/// <example>
/// <code>
/// // Static methods run once per frame - ideal for global logic
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

/// <summary>Runs once during app startup, before the window loop begins.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnStartupAttribute : Attribute;

/// <summary>Runs at the beginning of each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnFirstAttribute : Attribute;

/// <summary>Runs before Update each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnPreUpdateAttribute : Attribute;

/// <summary>Runs during the main update stage each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnUpdateAttribute : Attribute;

/// <summary>Runs after Update each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnPostUpdateAttribute : Attribute;

/// <summary>Runs during the render stage each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnRenderAttribute : Attribute;

/// <summary>Runs at the very end of each frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnLastAttribute : Attribute;

/// <summary>Runs once during app cleanup, after the window loop ends.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnCleanupAttribute : Attribute;

/// <summary>Filter: schedule only for entities that also have all listed component types.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WithAttribute : Attribute
{
    /// <summary>The component types to require.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="WithAttribute"/> requiring all specified component types.</summary>
    /// <param name="types">The component types that must be present on the entity.</param>
    public WithAttribute(params Type[] types) => Types = types;
}

/// <summary>Filter: skip entities that have any of the listed component types.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class WithoutAttribute : Attribute
{
    /// <summary>The component types to exclude.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="WithoutAttribute"/> excluding entities with any of the specified types.</summary>
    /// <param name="types">The component types that must <em>not</em> be present on the entity.</param>
    public WithoutAttribute(params Type[] types) => Types = types;
}

/// <summary>Filter: run only if any of the listed component types changed this frame.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ChangedAttribute : Attribute
{
    /// <summary>The component types to watch for changes.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="ChangedAttribute"/> watching the specified component types for changes.</summary>
    /// <param name="types">The component types; the system runs only if any of these changed this frame.</param>
    public ChangedAttribute(params Type[] types) => Types = types;
}

/// <summary>
/// Attaches a run condition to a behavior system method.
/// The <c>memberName</c> parameter must be a static bool member (method, property, or field) declared on the
/// same behavior struct. Use <c>nameof(...)</c> to keep the reference refactor-safe.
/// The system is skipped for the frame when the condition returns <c>false</c>.
/// </summary>
/// <example>
/// <code>
/// // Method version
/// [OnUpdate]
/// [RunIf(nameof(IsGamePlaying))]
/// public static void Tick(BehaviorContext ctx) { ... }
/// public static bool IsGamePlaying(World world) =>
///     world.TryGetResource&lt;GameState&gt;(out var s) &amp;&amp; s.IsPlaying;
/// </code>
/// <code>
/// // Property version
/// [OnUpdate]
/// [RunIf(nameof(IsEnabled))]
/// public static void Tick(BehaviorContext ctx) { ... }
/// public static bool IsEnabled { get; } = true;
/// </code>
/// <code>
/// // Field version
/// [OnUpdate]
/// [RunIf(nameof(IsVisible))]
/// public static void Tick(BehaviorContext ctx) { ... }
/// public static bool IsVisible = true;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class RunIfAttribute : Attribute
{
    /// <summary>Name of the static bool member (method, property, or field) on the same behavior struct.</summary>
    public string MethodName { get; }

    /// <summary>Creates a new <see cref="RunIfAttribute"/> referencing a static bool member by name.</summary>
    /// <param name="memberName">
    /// The name of a static bool member (method, property, or field) on the same behavior struct.
    /// Use <c>nameof(...)</c> to keep the reference refactor-safe.
    /// </param>
    public RunIfAttribute(string memberName) => MethodName = memberName;
}

/// <summary>
/// Binds a keyboard shortcut directly to a system, toggling it on/off without any boilerplate method.
/// Each press of the specified <c>key</c> (with optional <c>modifier</c> held) flips the
/// enabled state. The system starts enabled unless <c>DefaultEnabled = false</c> is set.
/// </summary>
/// <remarks>
/// Toggle state is managed via <see cref="SystemToggleRegistry"/> in <see cref="BehaviorConditions"/>.
/// </remarks>
/// <example>
/// <code>
/// [OnRender]
/// [ToggleKey(Key.F3)]                          // F3 alone
/// [ToggleKey(Key.F3, DefaultEnabled = false)]  // F3, default off
/// [ToggleKey(Key.F3, KeyModifier.Ctrl)]        // Ctrl + F3
/// public static void Draw(BehaviorContext ctx) { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ToggleKeyAttribute : Attribute
{
    /// <summary>The keyboard key that toggles this system.</summary>
    public Key Key { get; }

    /// <summary>Optional modifier keys that must be held when pressing <see cref="Key"/>.</summary>
    public KeyModifier Modifier { get; }

    /// <summary>Initial enabled state before the first toggle. Defaults to <c>true</c>.</summary>
    public bool DefaultEnabled { get; init; } = true;

    /// <summary>Creates a new <see cref="ToggleKeyAttribute"/> binding the specified key (with optional modifier) to toggle this system.</summary>
    /// <param name="key">The keyboard key that toggles the system.</param>
    /// <param name="modifier">Optional modifier keys that must be held when pressing <paramref name="key"/>.</param>
    public ToggleKeyAttribute(Key key, KeyModifier modifier = KeyModifier.None)
    {
        Key = key;
        Modifier = modifier;
    }
}
