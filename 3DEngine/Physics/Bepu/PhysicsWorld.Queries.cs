using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Trees;

namespace Engine;

/// <summary>Spatial queries, which are raycasts.</summary>
public sealed partial class PhysicsWorld
{
    /// <inheritdoc />
    public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit) =>
        Raycast(origin, direction, maxDistance, default, out hit);

    /// <summary>The closest hit along a ray that is not <paramref name="ignore"/>, as a body looking past itself.</summary>
    public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, PhysicsBody ignore, out RaycastHit hit) =>
        Raycast(origin, direction, maxDistance, ignore, BufferPool, out hit);

    // The same through a pool of the caller's, so rays cast on several threads at once each take
    // their scratch memory from a pool of their own.
    private bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, PhysicsBody ignore, BepuUtilities.Memory.BufferPool pool, out RaycastHit hit)
    {
        var handler = new ClosestRayHitHandler { Triggers = _triggerFlags };
        if (ignore.World == this)
        {
            handler.Skips = true;
            handler.Skip = ignore.Kind == BodyKind.Static
                ? new CollidableReference(new StaticHandle(ignore.Handle))
                : new CollidableReference(ignore.Kind == BodyKind.Kinematic ? CollidableMobility.Kinematic : CollidableMobility.Dynamic, new BodyHandle(ignore.Handle));
        }
        Simulation.RayCast(origin, direction, maxDistance, pool, ref handler);
        if (!handler.Found)
        {
            hit = default;
            return false;
        }

        var coll = handler.Collidable;
        BodyKind kind = coll.Mobility == CollidableMobility.Static
            ? BodyKind.Static
            : (coll.Mobility == CollidableMobility.Kinematic ? BodyKind.Kinematic : BodyKind.Dynamic);
        int rawHandle = coll.Mobility == CollidableMobility.Static ? coll.StaticHandle.Value : coll.BodyHandle.Value;
        int entityId = kind == BodyKind.Static
            ? (_staticToEntity.TryGetValue(rawHandle, out var se) ? se : 0)
            : (_bodyToEntity.TryGetValue(rawHandle, out var be) ? be : 0);
        hit = new RaycastHit
        {
            Body = new PhysicsBody(this, rawHandle, kind),
            Distance = handler.T,
            Normal = handler.Normal == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(handler.Normal),
            Point = origin + Vector3.Normalize(direction) * handler.T,
            EntityId = entityId,
        };
        return true;
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