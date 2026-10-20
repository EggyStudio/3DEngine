namespace Engine;

/// <summary>
/// An event that happens to one entity and goes on up its parents, one at a time, until an observer
/// stops it with <see cref="On{TEvent}.Propagate"/> or it reaches the root.
/// </summary>
/// <remarks>
/// Bevy's propagating <c>EntityEvent</c>, as a hit on an arm that the body it belongs to hears of
/// too, where armor on the arm may stop it.
/// </remarks>
public interface IPropagatingEvent : IEntityEvent;
