using System.Numerics;

namespace Engine;

/// <summary>A ray in world space, from a position along a direction, as raylib's.</summary>
public readonly record struct Ray(Vector3 Position, Vector3 Direction);
