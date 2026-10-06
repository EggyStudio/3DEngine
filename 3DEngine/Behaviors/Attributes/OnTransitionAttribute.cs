namespace Engine;

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
