namespace Engine;

public static partial class Engine3D
{
    /// <summary>
    /// Adds a state machine over an enum, starting at <paramref name="initial"/>, as a game's menu,
    /// play and pause are. Behaviors' <c>[OnEnter]</c>, <c>[OnExit]</c> and <c>[InState]</c> follow it.
    /// </summary>
    /// <remarks>Called once, after <see cref="InitWindow"/>. Adding it again starts it over at the new value.</remarks>
    public static void AddState<TState>(TState initial) where TState : struct, Enum => GetApp().AddState(initial);

    /// <summary>The value a state machine is in.</summary>
    /// <exception cref="InvalidOperationException">No state over <typeparamref name="TState"/> was added.</exception>
    public static TState GetState<TState>() where TState : struct, Enum =>
        TryRes<State<TState>>(out var state)
            ? state.Current
            : throw new InvalidOperationException($"No state over {typeof(TState).Name} was added. Call AddState first.");

    /// <summary>
    /// Moves a state machine to <paramref name="value"/> at the start of the next frame, running the
    /// exit systems of the value it leaves and the enter systems of the one it reaches.
    /// </summary>
    /// <exception cref="InvalidOperationException">No state over <typeparamref name="TState"/> was added.</exception>
    public static void SetState<TState>(TState value) where TState : struct, Enum
    {
        if (!TryRes<NextState<TState>>(out var next))
            throw new InvalidOperationException($"No state over {typeof(TState).Name} was added. Call AddState first.");
        next.Set(value);
    }

    /// <summary>Whether a state machine is at <paramref name="value"/>.</summary>
    public static bool IsState<TState>(TState value) where TState : struct, Enum =>
        EqualityComparer<TState>.Default.Equals(GetState<TState>(), value);
}
