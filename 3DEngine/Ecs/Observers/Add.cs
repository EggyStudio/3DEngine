namespace Engine;

/// <summary>A component was added to an entity that did not have one.</summary>
/// <typeparam name="T">The component.</typeparam>
/// <param name="Entity">The entity it was added to.</param>
/// <param name="Value">Its value as added.</param>
/// <remarks>
/// Bevy's <c>On&lt;Add, T&gt;</c>, observed with
/// <see cref="EcsWorld.Observe{TEvent}(Action{On{TEvent}})"/>. A component that replaces one the
/// entity had is no addition.
/// </remarks>
public readonly record struct Add<T>(Entity Entity, T Value) : IEntityEvent, IComponentEvent
{
    void IComponentEvent.Count(EcsWorld ecs, int by) => ecs.CountObservers<T>(ComponentChange.Added, by);
}
