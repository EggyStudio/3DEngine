namespace Engine;

/// <summary>A component of an entity a scene file spawned, told once the whole file is in.</summary>
/// <typeparam name="T">The component.</typeparam>
/// <param name="Entity">The entity the file spawned.</param>
/// <param name="Value">The component as the file left it.</param>
/// <remarks>
/// <para>
/// Triggered for each component of each entity a scene file spawned, in the file's order, once every
/// entity of the file is spawned, every parent set and every component read, so an observer of it
/// builds what the file does not hold, as a door's hinge or a sound a lamp hums, with every other
/// entity of the file present to find. It is told for a file read by <see cref="SceneFile"/> or
/// <c>LoadScene</c>, a save being one, and for each copy a <see cref="SceneRef"/> places, once the
/// copy is placed under its entity. <see cref="SceneLoaded"/> follows, once for the file.
/// </para>
/// <para>
/// Observed with <see cref="EcsWorld.Observe{TEvent}(Action{On{TEvent}})"/> as
/// <see cref="Add{T}"/> is, and counted by the component's store, so a component no observer
/// watches costs a read nothing. <c>LoadScene</c> makes the bodies the file's colliders describe
/// after these, so an observer does not find them yet.
/// </para>
/// </remarks>
public readonly record struct Loaded<T>(Entity Entity, T Value) : IEntityEvent, IComponentEvent
{
    void IComponentEvent.Count(EcsWorld ecs, int by) => ecs.CountObservers<T>(ComponentChange.Loaded, by);
}
