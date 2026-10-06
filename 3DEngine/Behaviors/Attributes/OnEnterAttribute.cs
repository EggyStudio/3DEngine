namespace Engine;

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
