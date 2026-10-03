using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Trees;

namespace Engine;

/// <summary>Spatial queries: raycasts (and future overlap / sweep tests).</summary>
public sealed partial class PhysicsWorld
{
    /// <inheritdoc />
    public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
    {
        var handler = new ClosestRayHitHandler();
        Simulation.RayCast(origin, direction, maxDistance, BufferPool, ref handler);
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
            Normal = handler.Normal,
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AllowTest(CollidableReference collidable) => true;

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