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

/// <summary>
/// An event sent when a state machine over <typeparamref name="TState"/> moves, readable with
/// <c>world.ReadEvents</c> until the next frame begins.
/// </summary>
/// <param name="From">The value it left, or null when it came into being, as the first value entered or a sub-state created.</param>
/// <param name="To">The value it entered, or null when it went away, as a sub-state whose parent left its value.</param>
public readonly record struct StateTransition<TState>(TState? From, TState? To) where TState : struct, Enum;

/// <summary>
/// The value a state machine over <typeparamref name="TState"/> moves to at the next transition
/// point, as a resource.
/// </summary>
/// <remarks>
/// The move is queued rather than made, and applied once a frame between
/// <see cref="Stage.PreUpdate"/> and <see cref="Stage.FixedUpdate"/>. Setting it twice in one frame
/// keeps the second value. Setting it to the value the machine is already in does nothing, so no
/// exit and enter run for a move that goes nowhere.
/// </remarks>
/// <typeparam name="TState">The enum the machine moves between.</typeparam>
public sealed class NextState<TState> where TState : struct, Enum
{
    /// <summary>The value queued, or null when nothing is.</summary>
    public TState? Pending { get; private set; }

    /// <summary>Queues a move to <paramref name="value"/> at the next transition point.</summary>
    public void Set(TState value) => Pending = value;

    /// <summary>Drops a queued move.</summary>
    public void Reset() => Pending = null;
}

/// <summary>
/// Every state machine an app has and the systems that run on its transitions, as a resource.
/// </summary>
/// <remarks>
/// A machine is created by whichever comes first, <see cref="App.AddState{TState}"/> or a system
/// registered with <see cref="App.OnEnter{TState}(TState, SystemDescriptor)"/>, because behaviors
/// register their transition systems when they are discovered, which can be before the program has
/// added its states. A machine that is never added has no value and never moves.
/// </remarks>
internal sealed class StateTransitions
{
    private readonly List<IStateMachine> _order = [];
    private readonly Dictionary<Type, IStateMachine> _byType = [];

    private readonly HashSet<Type> _clearing = [];

    /// <summary>Whether a system clearing <paramref name="state"/>'s transition events has yet to be added, marking it added.</summary>
    internal bool ClearsEventsOf(Type state) => _clearing.Add(state);

    /// <summary>The enums that have a machine, in the order transitions are applied.</summary>
    public IEnumerable<Type> StateTypes => _order.Select(m => m.StateType);

    internal StateMachine<TState> Machine<TState>() where TState : struct, Enum
    {
        if (_byType.TryGetValue(typeof(TState), out var existing)) return (StateMachine<TState>)existing;

        var machine = new StateMachine<TState>();
        _byType[typeof(TState)] = machine;
        _order.Add(machine);
        return machine;
    }

    /// <summary>Each machine's enum name, the value it is in (null when never added) and the values it has.</summary>
    /// <remarks>For tools such as the console, which know a state only by its name.</remarks>
    public IEnumerable<(string State, string? Current, string[] Values)> Describe(World world) =>
        _order.Select(m => (m.StateType.Name, m.Current(world), m.Values));

    /// <summary>Queues a move of the machine whose enum is called <paramref name="state"/> to the value called <paramref name="value"/>.</summary>
    /// <remarks>For tools such as the console. Code that knows the enum uses <see cref="NextState{TState}"/>.</remarks>
    /// <returns>Null when queued, or why it was not.</returns>
    public string? TryQueue(World world, string state, string value)
    {
        var machine = _order.FirstOrDefault(m => string.Equals(m.StateType.Name, state, StringComparison.OrdinalIgnoreCase));
        if (machine is null)
            return $"No state is called '{state}'. The states are: {string.Join(", ", _order.Select(m => m.StateType.Name))}.";
        return machine.TryQueue(world, value);
    }

    /// <summary>
    /// Applies every queued move, running the old value's exit systems and then the new value's
    /// enter systems, and enters each machine's first value the first time it runs.
    /// </summary>
    /// <remarks>
    /// Commands the transition systems queued are applied before this returns, so entities an enter
    /// system spawned are there for <see cref="Stage.Update"/> in the same frame.
    /// </remarks>
    /// <returns>Whether any machine moved.</returns>
    public bool Apply(World world)
    {
        var moved = false;
        foreach (var machine in _order)
            moved |= machine.Apply(world);

        if (moved && world.TryGetResource<EcsCommands>(out var cmd) && world.TryGetResource<EcsWorld>(out var ecs))
            cmd.Apply(ecs);

        return moved;
    }
}

internal interface IStateMachine
{
    Type StateType { get; }

    string[] Values { get; }

    string? Current(World world);

    string? TryQueue(World world, string value);

    bool Apply(World world);
}

internal sealed class StateMachine<TState> : IStateMachine where TState : struct, Enum
{
    private static readonly EqualityComparer<TState> Same = EqualityComparer<TState>.Default;
    private static readonly ILogger Logger = Log.Category("Engine.States");

    private readonly List<(TState Value, SystemDescriptor System)> _enter = [];
    private readonly List<(TState Value, SystemDescriptor System)> _exit = [];
    private readonly List<(TState From, TState To, SystemDescriptor System)> _transition = [];
    // Told after every move, with the value entered or null when the state went away, as the
    // computed states worked out from this one are.
    private readonly List<Action<World, TState?>> _moved = [];
    private bool _entered;

    /// <summary>Whether the first value has been entered.</summary>
    public bool Entered => _entered;

    public Type StateType => typeof(TState);

    public string[] Values => Enum.GetNames<TState>();

    public string? Current(World world) =>
        world.TryGetResource<State<TState>>(out var state) ? state.Current.ToString() : null;

    public string? TryQueue(World world, string value)
    {
        if (!world.TryGetResource<NextState<TState>>(out var next))
            return $"{typeof(TState).Name} has transition systems but was never added with App.AddState.";
        if (!Enum.TryParse<TState>(value, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
            return $"{typeof(TState).Name} has no value '{value}'. Its values are: {string.Join(", ", Values)}.";
        next.Set(parsed);
        return null;
    }

    public void OnEnter(TState value, SystemDescriptor system) => _enter.Add((value, system));

    public void OnExit(TState value, SystemDescriptor system) => _exit.Add((value, system));

    public void OnTransition(TState from, TState to, SystemDescriptor system) => _transition.Add((from, to, system));

    /// <summary>Registers an exit system ahead of those already registered, as a sub-state's removal is, which leaves before its parent.</summary>
    public void OnExitFirst(TState value, SystemDescriptor system) => _exit.Insert(0, (value, system));

    /// <summary>Forgets that the first value was entered, for a machine added again with a new one.</summary>
    public void Restart() => _entered = false;

    /// <summary>Asks to be told after every move of this machine.</summary>
    public void OnMoved(Action<World, TState?> moved) => _moved.Add(moved);

    /// <summary>
    /// Brings the state into being at <paramref name="initial"/>, entered at the transition point
    /// it is in or the next, as a sub-state is when its parent enters its value.
    /// </summary>
    public void Activate(World world, TState initial)
    {
        world.InsertResource(new State<TState>(initial));
        world.InsertResource(new NextState<TState>());
        _entered = false;
    }

    /// <summary>Runs the exit systems of the value the state is in and takes it away, as a sub-state is when its parent leaves its value.</summary>
    public void Deactivate(World world)
    {
        if (!world.TryGetResource<State<TState>>(out var state)) return;
        var from = state.Current;
        if (_entered)
        {
            Run(_exit, from, world);
            DespawnTied(world, from);
        }
        world.RemoveResource<State<TState>>();
        world.RemoveResource<NextState<TState>>();
        _entered = false;
        Logger.Info($"State {typeof(TState).Name}: {from} -> none");
        Moved(world, from, null);
    }

    // Sends the move as an event and tells whoever asked.
    private void Moved(World world, TState? from, TState? to)
    {
        world.SendEvent(new StateTransition<TState>(from, to));
        foreach (var moved in _moved) moved(world, to);
    }

    public bool Apply(World world)
    {
        if (!world.TryGetResource<State<TState>>(out var state)) return false;

        // The first value is entered at the first transition point rather than when the state is
        // added, so the enter systems of a state added in a plugin run after Startup has set up what
        // they rely on.
        if (!_entered)
        {
            _entered = true;
            Run(_enter, state.Current, world);
            Moved(world, null, state.Current);
            return true;
        }

        if (!world.TryGetResource<NextState<TState>>(out var next) || next.Pending is not { } target) return false;
        next.Reset();
        if (Same.Equals(target, state.Current)) return false;

        var from = state.Current;
        Run(_exit, from, world);
        DespawnTied(world, from);
        state.Previous = from;
        state.Current = target;
        Logger.Info($"State {typeof(TState).Name}: {from} -> {target}");
        foreach (var (key, to, desc) in _transition)
            if (Same.Equals(key, from) && Same.Equals(to, target))
                RunOne(desc, world, from);
        Run(_enter, target, world);
        Moved(world, from, target);
        return true;
    }

    // The entities tied to the value left, despawned once its exit systems have run.
    private static void DespawnTied(World world, TState value)
    {
        if (world.TryGetResource<EcsWorld>(out var ecs)) ecs.DespawnTiedTo(value);
    }

    // Runs one after another in the order registered, because a transition is a sequence (save,
    // then unload, then load) more often than it is independent work.
    private static void Run(List<(TState Value, SystemDescriptor System)> systems, TState value, World world)
    {
        foreach (var (key, desc) in systems)
            if (Same.Equals(key, value))
                RunOne(desc, world, value);
    }

    private static void RunOne(SystemDescriptor desc, World world, TState value)
    {
        if (desc.RunCondition is { } cond && !cond(world)) return;
        try
        {
            desc.System(world);
        }
        catch (Exception ex)
        {
            Logger.Error($"Transition system '{desc.Name}' threw entering or leaving {typeof(TState).Name}.{value}", ex);
        }
    }
}
