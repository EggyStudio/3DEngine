namespace Engine;

/// <summary>The pointer first moved with a button held that went down over the entity, as Bevy's <c>DragStart</c>.</summary>
/// <param name="Button">The button held.</param>
/// <param name="Hit">Where the pointer met the entity as the button went down.</param>
public readonly record struct DragStart(MouseButton Button, PointerHit Hit) : IPointerEvent;
