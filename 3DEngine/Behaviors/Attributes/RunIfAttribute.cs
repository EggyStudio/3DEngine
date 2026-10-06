namespace Engine;

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
