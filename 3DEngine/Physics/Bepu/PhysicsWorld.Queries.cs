using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Trees;

namespace Engine;

/// <summary>Spatial queries: rays, spheres swept along a ray, and the bodies a sphere overlaps.</summary>
public sealed partial class PhysicsWorld
{
    /// <inheritdoc />
    internal bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out PhysicsRayCollision hit) =>
        Raycast(origin, direction, maxDistance, default, out hit);

    /// <summary>The closest hit along a ray that is not <paramref name="ignore"/>, as a body looking past itself.</summary>
    internal bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, PhysicsBody ignore, out PhysicsRayCollision hit) =>
        Raycast(origin, direction, maxDistance, ignore, BufferPool, out hit);

    // The same through a pool of the caller's, so rays cast on several threads at once each take
    // their scratch memory from a pool of their own.
    private bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, PhysicsBody ignore, BepuUtilities.Memory.BufferPool pool, out PhysicsRayCollision hit)
    {
        var (skips, skip) = SkipOf(ignore);
        var handler = new ClosestRayHitHandler { Triggers = _triggerFlags, Skips = skips, Skip = skip };
        Simulation.RayCast(origin, direction, maxDistance, pool, ref handler);
        if (!handler.Found)
        {
            hit = default;
            return false;
        }

        hit = new PhysicsRayCollision
        {
            Hit = true,
            Body = BodyOf(handler.Collidable),
            Distance = handler.T,
            Normal = handler.Normal == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(handler.Normal),
            Point = origin + Vector3.Normalize(direction) * handler.T,
            EntityId = EntityOf(handler.Collidable),
        };
        return true;
    }

    // The collidable a body is, to leave out of a query, or a skip of nothing.
    private (bool Skips, CollidableReference Skip) SkipOf(PhysicsBody ignore) =>
        ignore.World != this ? (false, default)
        : (true, ignore.Kind == BodyKind.Static
            ? new CollidableReference(new StaticHandle(ignore.Handle))
            : new CollidableReference(ignore.Kind == BodyKind.Kinematic ? CollidableMobility.Kinematic : CollidableMobility.Dynamic, new BodyHandle(ignore.Handle)));

    /// <summary>
    /// The first body a sphere of <paramref name="radius"/> meets moving from <paramref name="origin"/>
    /// along <paramref name="direction"/> within <paramref name="maxDistance"/>, past triggers and
    /// <paramref name="ignore"/>, as a thick shot or a camera pulled in from a wall needs. Its
    /// distance is how far the sphere's middle moved, its point where the sphere touched, and its
    /// normal the way the surface faces there. A sphere that starts inside a body meets it at 0,
    /// facing back along the direction.
    /// </summary>
    internal bool SphereCast(Vector3 origin, float radius, Vector3 direction, float maxDistance, PhysicsBody ignore, out PhysicsRayCollision hit)
    {
        hit = default;
        if (radius <= 0 || direction == Vector3.Zero || maxDistance < 0) return false;
        var way = Vector3.Normalize(direction);
        var (skips, skip) = SkipOf(ignore);
        var handler = new ClosestSweepHitHandler { Triggers = _triggerFlags, Skips = skips, Skip = skip };
        Simulation.Sweep(new Sphere(radius), new RigidPose(origin), new BodyVelocity(way), maxDistance, BufferPool, ref handler);
        if (!handler.Found) return false;

        hit = new PhysicsRayCollision
        {
            Hit = true,
            Body = BodyOf(handler.Collidable),
            Distance = handler.T,
            Normal = handler.T == 0 || handler.Normal == Vector3.Zero ? -way : Vector3.Normalize(handler.Normal),
            Point = handler.T == 0 ? origin : handler.Point,
            EntityId = EntityOf(handler.Collidable),
        };
        return true;
    }

    /// <summary>
    /// Every body a sphere overlaps or touches, past triggers, each once, as the reach of an
    /// explosion or of a sound heard nearby.
    /// </summary>
    /// <remarks>
    /// The sphere is swept a hundredth of a millimetre, which Bepu reports a body it starts inside
    /// at 0 for, so the bodies are those whose shapes the sphere meets, and not only their bounds.
    /// </remarks>
    internal List<PhysicsBody> Overlap(Vector3 center, float radius)
    {
        var found = new List<PhysicsBody>();
        if (radius <= 0) return found;
        var handler = new EverySweepHitHandler { Triggers = _triggerFlags, Found = [] };
        Simulation.Sweep(new Sphere(radius), new RigidPose(center), new BodyVelocity(Vector3.UnitY), 1e-5f, BufferPool, ref handler);
        foreach (var collidable in handler.Found) found.Add(BodyOf(collidable));
        return found;
    }

    /// <summary>Bepu sweep callback that keeps the nearest hit, past triggers and one collidable.</summary>
    private struct ClosestSweepHitHandler : ISweepHitHandler
    {
        public bool Found;
        public float T;
        public Vector3 Point;
        public Vector3 Normal;
        public CollidableReference Collidable;
        public bool Skips;
        public CollidableReference Skip;
        public TriggerFlags Triggers;

        public bool AllowTest(CollidableReference collidable) => (!Skips || collidable.Packed != Skip.Packed) && !Triggers.Is(collidable);

        public bool AllowTest(CollidableReference collidable, int child) => true;

        public void OnHit(ref float maximumT, float t, Vector3 hitLocation, Vector3 hitNormal, CollidableReference collidable)
        {
            if (Found && t >= T) return;
            (Found, T, Point, Normal, Collidable) = (true, t, hitLocation, hitNormal, collidable);
            maximumT = t;
        }

        public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
        {
            (Found, T, Point, Normal, Collidable) = (true, 0, default, default, collidable);
            maximumT = 0;
        }
    }

    /// <summary>
    /// Bepu sweep callback that keeps every collidable it meets once, past triggers, in the order
    /// the traversal met them, so a query answers alike every run.
    /// </summary>
    private struct EverySweepHitHandler : ISweepHitHandler
    {
        public TriggerFlags Triggers;
        public List<CollidableReference> Found;

        public bool AllowTest(CollidableReference collidable) => !Triggers.Is(collidable);

        public bool AllowTest(CollidableReference collidable, int child) => true;

        public void OnHit(ref float maximumT, float t, Vector3 hitLocation, Vector3 hitNormal, CollidableReference collidable) => Keep(collidable);

        public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable) => Keep(collidable);

        // A mesh is met once a triangle, so a collidable already kept is not kept again.
        private readonly void Keep(CollidableReference collidable)
        {
            foreach (var kept in Found)
                if (kept.Packed == collidable.Packed) return;
            Found.Add(collidable);
        }
    }

    /// <summary>Bepu ray-hit callback that retains the closest hit encountered along the ray.</summary>
    private struct ClosestRayHitHandler : IRayHitHandler
    {
        public bool Found;
        public float T;
        public Vector3 Normal;
        public CollidableReference Collidable;
        public bool Skips;
        public CollidableReference Skip;
        // A trigger stops nothing, so a ray goes through it as a body does. A car's wheel or a
        // character's feet that met a gate's sensor stood on the air inside it.
        public TriggerFlags Triggers;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable) => (!Skips || collidable.Packed != Skip.Packed) && !Triggers.Is(collidable);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable, int childIndex) => true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal,
            CollidableReference collidable, int childIndex)
        {
            if (!Found || t < T)
            {
                Found = true;
                T = t;
                Normal = normal;
                Collidable = collidable;
                maximumT = t;
            }
        }
    }
}