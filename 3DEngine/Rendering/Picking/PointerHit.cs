using System.Numerics;

namespace Engine;

/// <summary>Where the pointer's ray met an entity's mesh.</summary>
/// <param name="Point">The point it met, in the world.</param>
/// <param name="Normal">Which way the triangle it met faces there, toward the camera, of length one.</param>
/// <param name="Distance">How far the point is from the camera, in world units.</param>
public readonly record struct PointerHit(Vector3 Point, Vector3 Normal, float Distance);
