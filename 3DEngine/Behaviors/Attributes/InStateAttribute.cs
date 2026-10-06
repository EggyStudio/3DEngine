namespace Engine;

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
