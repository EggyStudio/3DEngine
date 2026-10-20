namespace Engine;

/// <summary>A mouse button went down over the entity, as Bevy's <c>Press</c>.</summary>
/// <param name="Button">The button.</param>
/// <param name="Hit">Where the pointer meets the entity.</param>
public readonly record struct Press(MouseButton Button, PointerHit Hit) : IPointerEvent;
