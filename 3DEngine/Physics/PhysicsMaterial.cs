using System.Numerics;

namespace Engine;

/// <summary>
/// A body's friction and bounce, given when it is made or with <see cref="PhysicsWorld.SetMaterial"/>,
/// and on an entity beside its <see cref="Collider"/>, which a scene file holds. Use
/// <see cref="Default"/> for sensible defaults.
/// </summary>
/// <remarks>The damping is the world's, the largest any body was given, since every body is damped alike.</remarks>
[SceneComponent]
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
