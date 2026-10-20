using System.Numerics;

namespace Engine;

/// <summary>The pointer moved with a button held that went down over the entity, wherever it is now, as Bevy's <c>Drag</c>.</summary>
/// <param name="Button">The button held.</param>
/// <param name="Distance">How far the pointer is from where the button went down, in window pixels.</param>
/// <param name="Delta">How far it moved since the frame before, in window pixels.</param>
public readonly record struct Drag(MouseButton Button, Vector2 Distance, Vector2 Delta) : IPointerEvent;
