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
/// The same idea as Bevy's <c>State</c>, kept to plain states. Sub-states and computed states are
/// not written.
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
public sealed class StateTransitions
{
    private readonly List<IStateMachine> _order = [];
    private readonly Dictionary<Type, IStateMachine> _byType = [];

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
    private bool _entered;

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

    /// <summary>Forgets that the first value was entered, for a machine added again with a new one.</summary>
    public void Restart() => _entered = false;

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
            return true;
        }

        if (!world.TryGetResource<NextState<TState>>(out var next) || next.Pending is not { } target) return false;
        next.Reset();
        if (Same.Equals(target, state.Current)) return false;

        var from = state.Current;
        Run(_exit, from, world);
        state.Previous = from;
        state.Current = target;
        Logger.Info($"State {typeof(TState).Name}: {from} -> {target}");
        Run(_enter, target, world);
        return true;
    }

    // Runs one after another in the order registered, because a transition is a sequence (save,
    // then unload, then load) more often than it is independent work.
    private static void Run(List<(TState Value, SystemDescriptor System)> systems, TState value, World world)
    {
        foreach (var (key, desc) in systems)
        {
            if (!Same.Equals(key, value)) continue;
            if (desc.RunCondition is { } cond && !cond(world)) continue;

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
}
