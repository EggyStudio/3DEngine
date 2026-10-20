using System.Numerics;

namespace Engine;

/// <summary>The button held through a drag of the entity came up, wherever the pointer is, as Bevy's <c>DragEnd</c>.</summary>
/// <param name="Button">The button.</param>
/// <param name="Distance">How far the pointer is from where the button went down, in window pixels.</param>
public readonly record struct DragEnd(MouseButton Button, Vector2 Distance) : IPointerEvent;
