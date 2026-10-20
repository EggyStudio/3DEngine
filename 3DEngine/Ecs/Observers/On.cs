namespace Engine;

/// <summary>What an observer is handed, the event and the entity it has reached.</summary>
/// <typeparam name="TEvent">The event observed.</typeparam>
/// <remarks>
/// Bevy's <c>On</c>. One is made for each trigger and handed to each observer in turn, so an
/// observer that changes <see cref="Event"/> hands the changed event to the observers after it.
/// </remarks>
public sealed class On<TEvent>
{
    private TEvent _event;

    internal On(EcsWorld ecs, TEvent value, Entity entity)
    {
        Ecs = ecs;
        _event = value;
        Entity = entity;
    }

    /// <summary>The event, to read or to change for the observers after this one.</summary>
    public ref TEvent Event => ref _event;

    /// <summary>
    /// The entity the event is at, the one it happened to or, as it goes up, the parent it has
    /// reached, and <see cref="Entity.None"/> for an event that happened to no entity.
    /// </summary>
    public Entity Entity { get; internal set; }

    /// <summary>The world it happened in.</summary>
    public EcsWorld Ecs { get; }

    /// <summary>Whether the event goes on to the entity's parent once this entity's observers have run.</summary>
    internal bool Propagating { get; private set; } = true;

    /// <summary>Lets the event go on up to the entity's parent, or, given false, stops it at this entity.</summary>
    /// <remarks>
    /// Read for an <see cref="IPropagatingEvent"/> alone, once every observer of the entity it is
    /// at has run, so the observers of that entity after this one are still told.
    /// </remarks>
    public void Propagate(bool propagate) => Propagating = propagate;
}
