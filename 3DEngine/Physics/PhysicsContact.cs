using System.Numerics;

namespace Engine;

/// <summary>
/// Two bodies touching, as <see cref="PhysicsWorld.TakeContacts"/> reports them, with their
/// entities as they were when the contact started, or <see cref="Entity.None"/>, and where and
/// which way they met then.
/// </summary>
/// <param name="BodyA">The first body.</param>
/// <param name="BodyB">The second body.</param>
/// <param name="A">The first body's entity.</param>
/// <param name="B">The second body's entity.</param>
/// <param name="Point">Where they touched first, in the world, at their deepest contact.</param>
/// <param name="Normal">The contact's normal, from B toward A.</param>
/// <param name="Speed">How fast they closed along the normal as they met, in units a second, 0 for bodies that only came to rest against each other.</param>
public readonly record struct PhysicsContact(PhysicsBody BodyA, PhysicsBody BodyB, Entity A, Entity B,
    Vector3 Point = default, Vector3 Normal = default, float Speed = 0);

internal static class PhysicsContacts
{
    public static bool Involves(Entity a, Entity b, Entity entity, out Entity other)
    {
        other = a == entity ? b : b == entity ? a : Entity.None;
        return !entity.IsNone && (a == entity || b == entity);
    }
}
