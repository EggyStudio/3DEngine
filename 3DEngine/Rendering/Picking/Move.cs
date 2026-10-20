using System.Numerics;

namespace Engine;

/// <summary>The pointer moved and is still over the entity, as Bevy's <c>Move</c>.</summary>
/// <param name="Hit">Where it meets the entity now.</param>
/// <param name="Delta">How far it moved since the frame before, in window pixels.</param>
public readonly record struct Move(PointerHit Hit, Vector2 Delta) : IPointerEvent;
