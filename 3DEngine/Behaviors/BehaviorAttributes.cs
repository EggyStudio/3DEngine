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

/// <summary>Runs at a fixed rate, zero or more times a frame, between pre-update and update (see <see cref="FixedTime"/>).</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnFixedUpdateAttribute : Attribute;

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

/// <summary>Filter: an entity is visited only when each listed component changed since the method's system last ran.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ChangedAttribute : Attribute
{
    /// <summary>The component types to watch for changes.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="ChangedAttribute"/> watching the specified component types for changes.</summary>
    /// <param name="types">The component types, each of which must have changed since the system last ran.</param>
    public ChangedAttribute(params Type[] types) => Types = types;
}

/// <summary>Filter: an entity is visited only when it got each listed component since the method's system last ran.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class AddedAttribute : Attribute
{
    /// <summary>The component types to watch for being added.</summary>
    public Type[] Types { get; }

    /// <summary>Creates a new <see cref="AddedAttribute"/> watching the specified component types for being added.</summary>
    /// <param name="types">The component types, each of which the entity must have got since the system last ran.</param>
    public AddedAttribute(params Type[] types) => Types = types;
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

/// <summary>
/// Runs a behavior method once each time a state machine enters a value, instead of in a stage.
/// </summary>
/// <remarks>
/// The argument is a value of the state's enum, and the state is added with
/// <see cref="App.AddState{TState}"/>. An instance method runs on every entity carrying the
/// behavior, as a stage method does. A value that is not an enum member is reported as E3D004.
/// </remarks>
/// <example>
/// <code>
/// [OnEnter(Screen.Playing)]
/// public static void SpawnLevel(BehaviorContext ctx) { ... }
/// </code>
/// </example>
/// <param name="state">The enum value whose entry runs the method.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnEnterAttribute(object state) : Attribute
{
    /// <summary>The enum value whose entry runs the method.</summary>
    public object State { get; } = state;
}

/// <summary>
/// Runs a behavior method once each time a state machine leaves a value, instead of in a stage.
/// </summary>
/// <remarks>The counterpart of <see cref="OnEnterAttribute"/>, with the same rules.</remarks>
/// <param name="state">The enum value whose exit runs the method.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnExitAttribute(object state) : Attribute
{
    /// <summary>The enum value whose exit runs the method.</summary>
    public object State { get; } = state;
}

/// <summary>
/// Runs a behavior method once each time a state machine moves from one value to a particular
/// other, instead of in a stage.
/// </summary>
/// <remarks>
/// It runs after the exit systems of <see cref="From"/> and before the enter systems of
/// <see cref="To"/>, as Bevy's <c>OnTransition</c> does. Both are values of the same enum, which
/// the generator reports as E3D004 when they are not.
/// </remarks>
/// <example>
/// <code>
/// [OnTransition(Screen.Paused, Screen.Playing)]
/// public static void Resume(BehaviorContext ctx) { ... }
/// </code>
/// </example>
/// <param name="from">The value the move leaves.</param>
/// <param name="to">The value the move enters.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class OnTransitionAttribute(object from, object to) : Attribute
{
    /// <summary>The value the move leaves.</summary>
    public object From { get; } = from;

    /// <summary>The value the move enters.</summary>
    public object To { get; } = to;
}

/// <summary>
/// Declares an enum a state machine that exists only while another is in a value, as
/// <see cref="App.AddSubState{TSub, TParent}"/> adds one, entered at <see cref="Initial"/> or at
/// its first member each time the parent enters <see cref="WhileIn"/>.
/// </summary>
/// <example>
/// <code>
/// [SubStateOf(Screen.Playing)]
/// public enum Pause { Running, Paused }
/// </code>
/// </example>
/// <param name="whileIn">The value of the parent state the sub-state exists in.</param>
[AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public sealed class SubStateOfAttribute(object whileIn) : Attribute
{
    /// <summary>The value of the parent state the sub-state exists in.</summary>
    public object WhileIn { get; } = whileIn;

    /// <summary>The value the sub-state starts at, or null for its first member.</summary>
    public object? Initial { get; set; }
}

/// <summary>
/// Declares a state machine computed from another by the static method it is on, as
/// <see cref="App.AddComputedState{TComputed, TSource}"/> adds one. The method takes the source
/// state's enum and returns the computed one's, nullable, with null for no state at all.
/// </summary>
/// <example>
/// <code>
/// [ComputedState]
/// public static InGame? FromScreen(Screen screen) => screen is Screen.Playing or Screen.Paused ? InGame.Yes : null;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ComputedStateAttribute : Attribute;

/// <summary>
/// Runs a stage method only while a state machine is in a value.
/// </summary>
/// <remarks>
/// A run condition, so it combines with <see cref="RunIfAttribute"/> and
/// <see cref="ToggleKeyAttribute"/> and the method runs only when all of them pass. The method
/// does not run while the state was never added.
/// </remarks>
/// <example>
/// <code>
/// [OnUpdate]
/// [InState(Screen.Playing)]
/// public void Move(BehaviorContext ctx) { ... }
/// </code>
/// </example>
/// <param name="state">The enum value the method runs in.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class InStateAttribute(object state) : Attribute
{
    /// <summary>The enum value the method runs in.</summary>
    public object State { get; } = state;
}
