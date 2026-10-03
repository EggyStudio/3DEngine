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

/// <summary>
/// Two bodies touching, as <see cref="PhysicsWorld.TakeContacts"/> reports them, with their
/// entities as they were when the contact started, or <see cref="Entity.None"/>.
/// </summary>
public readonly record struct PhysicsContact(PhysicsBody BodyA, PhysicsBody BodyB, Entity A, Entity B);

/// <summary>
/// Sent when two bodies start touching, in the fixed step it happened, and readable until the
/// frame ends. Which body is A and which is B is not meaningful.
/// </summary>
/// <example>
/// <code>
/// foreach (var hit in ctx.World.ReadEvents&lt;ContactStarted&gt;())
///     if (hit.Involves(player, out var other) &amp;&amp; ctx.Ecs.Has&lt;Coin&gt;(other))
///         Collect(other);
/// </code>
/// </example>
/// <param name="A">The first body's entity, or <see cref="Entity.None"/> for a body made without one.</param>
/// <param name="B">The second body's entity, or <see cref="Entity.None"/>.</param>
/// <param name="BodyA">The first body.</param>
/// <param name="BodyB">The second body.</param>
public readonly record struct ContactStarted(Entity A, Entity B, PhysicsBody BodyA, PhysicsBody BodyB)
{
    /// <summary>Whether <paramref name="entity"/> is one of the two, with the other in <paramref name="other"/>.</summary>
    public bool Involves(Entity entity, out Entity other) => PhysicsContacts.Involves(A, B, entity, out other);
}

/// <summary>
/// Sent when two bodies stop touching, or when one of them is destroyed, and readable until the
/// frame ends. The entities are as they were when the contact started, so one despawned since
/// is still named, and <see cref="EcsWorld.IsAlive"/> tells which.
/// </summary>
/// <param name="A">The first body's entity, or <see cref="Entity.None"/>.</param>
/// <param name="B">The second body's entity, or <see cref="Entity.None"/>.</param>
/// <param name="BodyA">The first body, which may no longer exist.</param>
/// <param name="BodyB">The second body, which may no longer exist.</param>
public readonly record struct ContactEnded(Entity A, Entity B, PhysicsBody BodyA, PhysicsBody BodyB)
{
    /// <summary>Whether <paramref name="entity"/> is one of the two, with the other in <paramref name="other"/>.</summary>
    public bool Involves(Entity entity, out Entity other) => PhysicsContacts.Involves(A, B, entity, out other);
}

internal static class PhysicsContacts
{
    public static bool Involves(Entity a, Entity b, Entity entity, out Entity other)
    {
        other = a == entity ? b : b == entity ? a : Entity.None;
        return !entity.IsNone && (a == entity || b == entity);
    }
}
