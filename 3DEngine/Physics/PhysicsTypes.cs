using System.Numerics;

namespace Engine;

/// <summary>Per-body friction/restitution/damping properties. Use <see cref="Default"/> for sensible defaults.</summary>
public struct PhysicsMaterial
{
    /// <summary>Coulomb friction coefficient. <c>0</c> = ice, <c>1</c> = rubber-on-concrete.</summary>
    public float Friction;

    /// <summary>Bounciness. <c>0</c> = inelastic, <c>1</c> = perfectly elastic.</summary>
    public float Restitution;

    /// <summary>Linear-velocity damping per second (drag).</summary>
    public float LinearDamping;

    /// <summary>Angular-velocity damping per second (drag).</summary>
    public float AngularDamping;

    /// <summary>Default material: friction 1, restitution 0, no damping.</summary>
    public static PhysicsMaterial Default => new()
    {
        Friction = 1f, 
        Restitution = 0f, 
        LinearDamping = 0f, 
        AngularDamping = 0f
    };
}

/// <summary>Result of an <see cref="PhysicsWorld.Raycast(Vector3,Vector3,float,out RaycastHit)"/>.</summary>
public struct RaycastHit
{
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