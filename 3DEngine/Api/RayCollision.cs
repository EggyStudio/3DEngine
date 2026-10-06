using System.Numerics;

namespace Engine;

/// <summary>Where a ray met something, as raylib's <c>RayCollision</c> has it.</summary>
/// <param name="Hit">Whether it met it at all.</param>
/// <param name="Distance">How far along the ray, in world units.</param>
/// <param name="Point">Where, in the world.</param>
/// <param name="Normal">The surface's normal there.</param>
public readonly record struct RayCollision(bool Hit, float Distance, Vector3 Point, Vector3 Normal);
