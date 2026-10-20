namespace Engine;

/// <summary>An event that happens to one entity, so the observers of that entity are told of it.</summary>
/// <remarks>
/// Bevy's <c>EntityEvent</c>. Triggered with <see cref="EcsWorld.Trigger{TEvent}(TEvent)"/>, it
/// reaches the observers of every such event and then those of <see cref="Entity"/>.
/// </remarks>
public interface IEntityEvent
{
    /// <summary>The entity it happened to.</summary>
    Entity Entity { get; }
}
