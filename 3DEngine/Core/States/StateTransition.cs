namespace Engine;

/// <summary>
/// An event sent when a state machine over <typeparamref name="TState"/> moves, readable with
/// <c>world.ReadEvents</c> until the next frame begins.
/// </summary>
/// <param name="From">The value it left, or null when it came into being, as the first value entered or a sub-state created.</param>
/// <param name="To">The value it entered, or null when it went away, as a sub-state whose parent left its value.</param>
public readonly record struct StateTransition<TState>(TState? From, TState? To) where TState : struct, Enum;
