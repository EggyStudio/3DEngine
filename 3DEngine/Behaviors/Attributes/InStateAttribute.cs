namespace Engine;

/// <summary>
/// Runs a stage method only while a state machine is in a value.
/// </summary>
/// <remarks>
/// A run condition, so it combines with <see cref="RunIfAttribute"/> and
/// <see cref="ToggleKeyAttribute"/> and the method runs only when all of them pass. More than one
/// names a value of each of several states, and the method runs only while every one holds. The
/// method does not run while a state it names was never added.
/// </remarks>
/// <example>
/// <code>
/// [OnUpdate]
/// [InState(Screen.Playing)]
/// public void Move(BehaviorContext ctx) { ... }
/// </code>
/// </example>
/// <param name="state">The enum value the method runs in.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class InStateAttribute(object state) : Attribute
{
    /// <summary>The enum value the method runs in.</summary>
    public object State { get; } = state;
}
