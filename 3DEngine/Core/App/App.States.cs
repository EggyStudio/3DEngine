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
        ClearTransitionEvents<TState>();
        Logger.Info($"State {typeof(TState).Name} added at {initial}.");
        return this;
    }

    /// <summary>
    /// Adds a state machine over <typeparamref name="TSub"/> that exists only while
    /// <typeparamref name="TParent"/> is in <paramref name="whileIn"/>, starting at
    /// <paramref name="initial"/> each time the parent enters that value.
    /// </summary>
    /// <remarks>
    /// The parent is added first. When the parent enters <paramref name="whileIn"/> the sub-state is
    /// created and entered at <paramref name="initial"/> in the same transition point, and when it
    /// leaves, the sub-state's exit systems run and it goes away, so <c>InState</c> of any of its
    /// values is false and a move queued for it is dropped. Bevy's pause that exists only while
    /// playing is one.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.AddState(Screen.Menu)
    ///    .AddSubState(Screen.Playing, Pause.Running);
    /// </code>
    /// </example>
    /// <returns>This <see cref="App"/> for chaining.</returns>
    public App AddSubState<TSub, TParent>(TParent whileIn, TSub initial)
        where TSub : struct, Enum where TParent : struct, Enum
    {
        var parent = Transitions().Machine<TParent>();
        var sub = Transitions().Machine<TSub>();
        parent.OnEnter(whileIn, new SystemDescriptor(world => sub.Activate(world, initial), $"{typeof(TSub).Name}.Create"));
        // Ahead of the parent's own exit systems, so the sub-state leaves first.
        parent.OnExitFirst(whileIn, new SystemDescriptor(world => sub.Deactivate(world), $"{typeof(TSub).Name}.Remove"));
        ClearTransitionEvents<TSub>();

        // A parent already entered into the value has the sub-state from the next transition point.
        if (parent.Entered && World.TryGetResource<State<TParent>>(out var state) && EqualityComparer<TParent>.Default.Equals(state.Current, whileIn))
            sub.Activate(World, initial);
        Logger.Info($"State {typeof(TSub).Name} added under {typeof(TParent).Name}.{whileIn}, at {initial}.");
        return this;
    }

    /// <summary>
    /// Adds a state machine over <typeparamref name="TComputed"/> whose value is worked out from
    /// <typeparamref name="TSource"/> by <paramref name="compute"/> after each of its moves, with no
    /// state at all where it gives null.
    /// </summary>
    /// <remarks>
    /// The computed state moves in the same transition point as its source, running its own exit
    /// and enter systems. Its <see cref="NextState{TState}"/> is the source's to set, not the
    /// program's. Bevy's computed states are the model, such as an <c>InGame</c> that holds for each of
    /// several playing screens.
    /// </remarks>
    /// <returns>This <see cref="App"/> for chaining.</returns>
    public App AddComputedState<TComputed, TSource>(Func<TSource, TComputed?> compute)
        where TComputed : struct, Enum where TSource : struct, Enum
    {
        var source = Transitions().Machine<TSource>();
        var computed = Transitions().Machine<TComputed>();
        source.OnMoved((world, value) =>
        {
            var next = value is { } v ? compute(v) : null;
            var exists = world.TryGetResource<State<TComputed>>(out var state);
            if (next is not { } target)
            {
                computed.Deactivate(world);
                return;
            }
            if (!exists)
            {
                computed.Activate(world, target);
                return;
            }
            // A move, made later in the same transition point, since the computed machine comes
            // after its source.
            if (!EqualityComparer<TComputed>.Default.Equals(state.Current, target))
                world.Resource<NextState<TComputed>>().Set(target);
        });
        ClearTransitionEvents<TComputed>();
        Logger.Info($"State {typeof(TComputed).Name} added, computed from {typeof(TSource).Name}.");
        return this;
    }

    // Each state's transition events last until the next frame begins.
    private void ClearTransitionEvents<TState>() where TState : struct, Enum
    {
        if (!Transitions().ClearsEventsOf(typeof(TState))) return;
        AddSystem(Stage.First, new SystemDescriptor(world => world.ClearEvents<StateTransition<TState>>(), $"States.{typeof(TState).Name}.ClearTransitions")
            .Write<Events<StateTransition<TState>>>());
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
