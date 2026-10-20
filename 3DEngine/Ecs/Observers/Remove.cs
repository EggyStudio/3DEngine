namespace Engine;

/// <summary>A component left an entity, removed or despawned with it.</summary>
/// <typeparam name="T">The component.</typeparam>
/// <param name="Entity">The entity it left.</param>
/// <param name="Value">The value it had.</param>
/// <remarks>
/// Bevy's <c>On&lt;Remove, T&gt;</c>. The observer runs once the component has gone, and for a
/// despawn once the entity has, so it reads what the component was from <see cref="Value"/>.
/// </remarks>
public readonly record struct Remove<T>(Entity Entity, T Value) : IEntityEvent, IComponentEvent
{
    void IComponentEvent.Count(EcsWorld ecs, int by) => ecs.CountObservers<T>(added: false, by);
}
