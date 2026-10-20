namespace Engine;

/// <summary>The pointer came over the entity, as Bevy's <c>Over</c>.</summary>
/// <param name="Hit">Where it meets the entity.</param>
public readonly record struct Over(PointerHit Hit) : IPointerEvent;
