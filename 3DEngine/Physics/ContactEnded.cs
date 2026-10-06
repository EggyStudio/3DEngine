using System.Numerics;

namespace Engine;

/// <summary>
/// Sent when two bodies stop touching, or when one of them is destroyed, and readable until the
/// frame ends. The entities are as they were when the contact started, so one despawned since
/// is still named, and <see cref="EcsWorld.IsAlive(Entity)"/> tells which.
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
