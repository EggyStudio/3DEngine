namespace Engine;

/// <summary>
/// Ties an entity to a rule over a state's transitions, so it is despawned, with every entity below
/// it, at the first transition the rule answers true for.
/// </summary>
/// <remarks>
/// It is added with <see cref="EcsWorld.DespawnWhen{TState}(int, Func{StateTransition{TState}, bool})"/>,
/// for what goes by more than one value, as text saying the state holds one of several values goes
/// as it leaves any of them, as Bevy's <c>DespawnWhen</c> does. The rule is asked at every
/// transition, the first value's entering and a sub-state's going away among them, after the exit
/// systems of the value left and before the enter systems of the value entered, and a rule that
/// throws is taken as answering false.
/// </remarks>
/// <typeparam name="TState">The enum of the state.</typeparam>
/// <param name="Rule">Whether a transition takes the entity.</param>
public readonly record struct DespawnWhen<TState>(Func<StateTransition<TState>, bool> Rule) where TState : struct, Enum;
