namespace Engine;

/// <summary>The pointer came over the entity.</summary>
/// <param name="Hit">Where it meets the entity.</param>
public readonly record struct Over(PointerHit Hit) : IPointerEvent;
