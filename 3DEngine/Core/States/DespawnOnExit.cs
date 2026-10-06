namespace Engine;

/// <summary>
/// Ties an entity to a value of a state, so it is despawned, with every entity below it, when the
/// state leaves that value, as a menu's entities go with the menu.
/// </summary>
/// <remarks>
/// It is added with <see cref="EcsWorld.DespawnOnExit{TState}(int, TState)"/>. The despawn comes
/// after the value's <c>[OnExit]</c> systems, which can still read the entity, and covers every
/// way out of the value, a sub-state ending with its parent's value included, so a game keeps no
/// list of what a screen spawned. The same idea as Bevy's <c>DespawnOnExit</c>.
/// </remarks>
/// <typeparam name="TState">The enum of the state.</typeparam>
/// <param name="Value">The value the entity lives as long as.</param>
public readonly record struct DespawnOnExit<TState>(TState Value) where TState : struct, Enum;
