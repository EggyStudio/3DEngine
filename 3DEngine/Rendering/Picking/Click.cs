namespace Engine;

/// <summary>A mouse button went down over the entity and came up over it again, as Bevy's <c>Click</c>.</summary>
/// <param name="Button">The button.</param>
/// <param name="Hit">Where the pointer meets the entity as the button comes up.</param>
/// <remarks>Triggered after the <see cref="Release"/>, and after a drag too where it ends over the entity it started on.</remarks>
public readonly record struct Click(MouseButton Button, PointerHit Hit) : IPointerEvent;
