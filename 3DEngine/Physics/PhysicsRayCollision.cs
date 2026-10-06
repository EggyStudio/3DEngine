using System.Numerics;

namespace Engine;

/// <summary>Where a ray cast through the physics world met a body, as raylib's <c>RayCollision</c> is where a ray met a shape it was given, with the body.</summary>
public struct PhysicsRayCollision
{
    /// <summary>Whether the ray met a body, as raylib's <c>RayCollision</c> says, the rest left at their defaults when it did not.</summary>
    public bool Hit;

    /// <summary>The body that was hit.</summary>
    public PhysicsBody Body;

    /// <summary>World-space hit point.</summary>
    public Vector3 Point;

    /// <summary>World-space surface normal at the hit point.</summary>
    public Vector3 Normal;

    /// <summary>Distance along the ray to the hit point.</summary>
    public float Distance;

    /// <summary>The ECS entity associated with the hit body, or <c>0</c> when unknown.</summary>
    public int EntityId;
}
