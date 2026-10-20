namespace Engine;

/// <summary>A mouse button came up over the entity, wherever it went down, as Bevy's <c>Release</c>.</summary>
/// <param name="Button">The button.</param>
/// <param name="Hit">Where the pointer meets the entity.</param>
public readonly record struct Release(MouseButton Button, PointerHit Hit) : IPointerEvent;
