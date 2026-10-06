namespace Engine;

/// <summary>
/// The value a state machine over <typeparamref name="TState"/> is in, as a resource.
/// </summary>
/// <remarks>
/// <para>
/// A state is an enum the game moves between, such as a menu, a level and a pause, with systems
/// that run on entering or leaving a value and systems that run only while it holds. It is added
/// with <see cref="App.AddState{TState}"/> and changed through <see cref="NextState{TState}"/>, never
/// by writing here, so every system in a frame agrees on which value it is in.
/// </para>
/// <para>
/// The same idea as Bevy's <c>State</c>. A sub-state (<see cref="App.AddSubState{TSub, TParent}"/>)
/// exists only while another state is in one value, and a computed state
/// (<see cref="App.AddComputedState{TComputed, TSource}"/>) is worked out from another state, so
/// either may have no <see cref="State{TState}"/> at all, which <c>InState</c> reads as false.
/// Each move is sent as a <see cref="StateTransition{TState}"/> event.
/// </para>
/// </remarks>
/// <typeparam name="TState">The enum the machine moves between.</typeparam>
public sealed class State<TState> where TState : struct, Enum
{
    internal State(TState current) => Current = current;

    /// <summary>The value the machine is in.</summary>
    public TState Current { get; internal set; }

    /// <summary>The value it was in before the last transition, or null before the first.</summary>
    public TState? Previous { get; internal set; }

    /// <inheritdoc />
    public override string ToString() => Current.ToString();
}
