namespace Engine;

/// <summary>The pointer left the entity, for another or for none, or the entity stopped being drawn under it, as Bevy's <c>Out</c>.</summary>
public readonly record struct Out : IPointerEvent;
