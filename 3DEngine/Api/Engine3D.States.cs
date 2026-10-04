namespace Engine;

public static partial class Engine3D
{
    /// <summary>
    /// Adds a state machine over an enum, starting at <paramref name="initial"/>, as a game's menu,
    /// play and pause are. Behaviors' <c>[OnEnter]</c>, <c>[OnExit]</c> and <c>[InState]</c> follow it.
    /// </summary>
    /// <remarks>Called once, after <see cref="InitWindow"/>. Adding it again starts it over at the new value.</remarks>
    public static void AddState<TState>(TState initial) where TState : struct, Enum => GetApp().AddState(initial);

    /// <summary>
    /// Adds a state machine that exists only while <typeparamref name="TParent"/> is at
    /// <paramref name="whileIn"/>, starting at <paramref name="initial"/> each time the parent gets
    /// there, as a pause that exists only while playing.
    /// </summary>
    public static void AddSubState<TSub, TParent>(TParent whileIn, TSub initial)
        where TSub : struct, Enum where TParent : struct, Enum => GetApp().AddSubState(whileIn, initial);

    /// <summary>
    /// Adds a state machine whose value <paramref name="compute"/> works out from
    /// <typeparamref name="TSource"/>'s each time it moves, with none where it gives null, as an
    /// in-game state that holds for several playing screens.
    /// </summary>
    public static void AddComputedState<TComputed, TSource>(Func<TSource, TComputed?> compute)
        where TComputed : struct, Enum where TSource : struct, Enum => GetApp().AddComputedState(compute);

    /// <summary>The value a state machine is in.</summary>
    /// <exception cref="InvalidOperationException">No state over <typeparamref name="TState"/> exists, as one never added, or a sub-state whose parent is elsewhere.</exception>
    public static TState GetState<TState>() where TState : struct, Enum =>
        TryRes<State<TState>>(out var state)
            ? state.Current
            : throw new InvalidOperationException(
                $"No state over {typeof(TState).Name} exists. It was never added with AddState, or it is a sub-state or computed state that has no value at present.");

    /// <summary>
    /// Moves a state machine to <paramref name="value"/> at the start of the next frame, running the
    /// exit systems of the value it leaves and the enter systems of the one it reaches.
    /// </summary>
    /// <exception cref="InvalidOperationException">No state over <typeparamref name="TState"/> was added.</exception>
    public static void SetState<TState>(TState value) where TState : struct, Enum
    {
        if (!TryRes<NextState<TState>>(out var next))
            throw new InvalidOperationException(
                $"No state over {typeof(TState).Name} exists. It was never added with AddState, or it is a sub-state or computed state that has no value at present.");
        next.Set(value);
    }

    /// <summary>Whether a state machine is at <paramref name="value"/>, false for one with no value at present.</summary>
    public static bool IsState<TState>(TState value) where TState : struct, Enum =>
        TryRes<State<TState>>(out var state) && EqualityComparer<TState>.Default.Equals(state.Current, value);
}
