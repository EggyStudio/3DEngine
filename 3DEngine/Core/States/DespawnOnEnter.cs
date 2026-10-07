namespace Engine;

/// <summary>
/// Ties an entity to a value of a state, so it is despawned, with every entity below it, when the
/// state enters that value, as a notice put up on leaving a screen goes when the screen comes back.
/// </summary>
/// <remarks>
/// It is added with <see cref="EcsWorld.DespawnOnEnter{TState}(int, TState)"/>, the other edge of
/// <see cref="DespawnOnExit{TState}"/>. The despawn comes before the value's <c>[OnEnter]</c>
/// systems, so what they spawn is not taken with it, as Bevy's <c>DespawnOnEnter</c> does.
/// </remarks>
/// <typeparam name="TState">The enum of the state.</typeparam>
/// <param name="Value">The value whose entering takes the entity.</param>
public readonly record struct DespawnOnEnter<TState>(TState Value) where TState : struct, Enum;
