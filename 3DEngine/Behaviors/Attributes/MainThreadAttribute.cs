namespace Engine;

/// <summary>
/// Runs a behavior method alone on the main thread, visiting its entities in turn, as a method
/// that calls ImGui, plays a sound or writes a resource other methods use needs.
/// </summary>
/// <remarks>
/// A method of the instance otherwise runs beside other systems that take none of its components,
/// on another thread, and over 4096 entities splits them between threads. A static method always
/// runs on the main thread, since it is a rule over the whole game.
/// </remarks>
/// <example>
/// <code>
/// [OnUpdate]
/// [MainThread]
/// public void Fire(BehaviorContext ctx, in Transform transform) => PlaySound(Shot);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class MainThreadAttribute : Attribute;
