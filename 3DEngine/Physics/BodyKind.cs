namespace Engine;

/// <summary>The simulation behaviour of a <see cref="PhysicsBody"/>.</summary>
public enum BodyKind : byte
{
    /// <summary>Affected by gravity, forces, impulses, and collisions.</summary>
    Dynamic,

    /// <summary>Moved manually (via <see cref="PhysicsBody.SetPosition"/> /
    /// <see cref="PhysicsBody.SetLinearVelocity"/>); has infinite mass and is unaffected
    /// by external forces but pushes dynamic bodies.</summary>
    Kinematic,

    /// <summary>Immovable collider used for level geometry.</summary>
    Static,
}