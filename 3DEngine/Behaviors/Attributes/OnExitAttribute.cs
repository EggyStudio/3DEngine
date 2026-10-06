namespace Engine;

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
