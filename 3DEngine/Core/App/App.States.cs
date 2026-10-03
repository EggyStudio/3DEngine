namespace Engine;

public sealed partial class App
{
    /// <summary>
    /// Adds a state machine over <typeparamref name="TState"/>, starting at <paramref name="initial"/>.
    /// </summary>
    /// <remarks>
    /// Inserts <see cref="State{TState}"/> and <see cref="NextState{TState}"/>. The enter systems of
    /// <paramref name="initial"/> run at the first frame's transition point, after
    /// <see cref="Stage.Startup"/>. Adding the same enum again starts it over at the new value.
    /// </remarks>
    /// <example>
    /// <code>
    /// public enum Screen { Menu, Playing }
    ///
    /// app.AddState(Screen.Menu)
    ///    .OnEnter(Screen.Playing, SpawnLevel)
    ///    .AddSystem(Stage.Update, new SystemDescriptor(MovePlayer).RunIf(BehaviorConditions.InState(Screen.Playing)));
    /// </code>
    /// </example>
    /// <returns>This <see cref="App"/> for chaining.</returns>
    public App AddState<TState>(TState initial) where TState : struct, Enum
    {
        World.InsertResource(new State<TState>(initial));
        World.InsertResource(new NextState<TState>());
        Transitions().Machine<TState>().Restart();
        Logger.Info($"State {typeof(TState).Name} added at {initial}.");
        return this;
    }

    /// <summary>Registers a system that runs once each time <typeparamref name="TState"/> enters <paramref name="state"/>.</summary>
    /// <returns>This <see cref="App"/> for chaining.</returns>
    public App OnEnter<TState>(TState state, SystemFn system) where TState : struct, Enum =>
        OnEnter(state, new SystemDescriptor(system));

    /// <inheritdoc cref="OnEnter{TState}(TState, SystemFn)"/>
    /// <remarks>A run condition on the descriptor is asked when the transition runs.</remarks>
    public App OnEnter<TState>(TState state, SystemDescriptor system) where TState : struct, Enum
    {
        Transitions().Machine<TState>().OnEnter(state, system);
        return this;
    }

    /// <summary>Registers a system that runs once each time <typeparamref name="TState"/> leaves <paramref name="state"/>.</summary>
    /// <returns>This <see cref="App"/> for chaining.</returns>
    public App OnExit<TState>(TState state, SystemFn system) where TState : struct, Enum =>
        OnExit(state, new SystemDescriptor(system));

    /// <inheritdoc cref="OnExit{TState}(TState, SystemFn)"/>
    /// <remarks>A run condition on the descriptor is asked when the transition runs.</remarks>
    public App OnExit<TState>(TState state, SystemDescriptor system) where TState : struct, Enum
    {
        Transitions().Machine<TState>().OnExit(state, system);
        return this;
    }

    private StateTransitions Transitions() => World.GetOrInsertResource(static () => new StateTransitions());
}
