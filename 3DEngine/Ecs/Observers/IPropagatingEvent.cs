namespace Engine;

/// <summary>
/// An event that happens to one entity and goes on up its parents, one at a time, until an observer
/// stops it with <see cref="On{TEvent}.Propagate"/> or it reaches the root.
/// </summary>
/// <remarks>
/// A hit on an arm is one, which the body the arm belongs to hears of too, where armor on the arm
/// may stop it.
/// </remarks>
public interface IPropagatingEvent : IEntityEvent;
