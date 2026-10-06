using System.Numerics;

namespace Engine;

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
/// <param name="Point">Where they touched, in the world, at their deepest contact.</param>
/// <param name="Normal">The contact's normal, from B toward A, the way A is pushed.</param>
/// <param name="Speed">
/// How fast they closed along the normal as they met, in units a second, which says how hard they
/// hit, as for the loudness of a sound or the damage done. Two bodies that come to rest against
/// each other meet at nearly 0.
/// </param>
public readonly record struct ContactStarted(Entity A, Entity B, PhysicsBody BodyA, PhysicsBody BodyB,
    Vector3 Point = default, Vector3 Normal = default, float Speed = 0)
{
    /// <summary>Whether <paramref name="entity"/> is one of the two, with the other in <paramref name="other"/>.</summary>
    public bool Involves(Entity entity, out Entity other) => PhysicsContacts.Involves(A, B, entity, out other);

    /// <summary>The normal pointing toward <paramref name="entity"/> and away from the other, the way it is pushed.</summary>
    public Vector3 NormalToward(Entity entity) => entity == B ? -Normal : Normal;
}
