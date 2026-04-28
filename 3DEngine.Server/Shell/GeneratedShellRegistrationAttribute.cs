namespace Editor.Shell;

/// <summary>
/// Marks a static method that registers compiled-in editor shells with a
/// <see cref="ShellRegistry"/>. Discovered at runtime by
/// <see cref="StaticShellLoader"/> via reflection across all loaded assemblies.
/// </summary>
/// <remarks>
/// Emitted by the <c>EditorShellGenerator</c> source generator on a static
/// helper class produced inside each consuming assembly. Mirrors the pattern
/// used by <c>GeneratedBehaviorRegistrationAttribute</c> for ECS behaviors.
/// The marked method must be <see langword="static"/> and take a single
/// <see cref="ShellRegistry"/> parameter.
/// </remarks>
/// <seealso cref="StaticShellLoader"/>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GeneratedShellRegistrationAttribute : Attribute;

