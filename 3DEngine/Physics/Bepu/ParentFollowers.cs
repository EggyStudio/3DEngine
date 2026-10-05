using System.Numerics;

namespace Engine;

/// <summary>
/// How each kinematic body under a parent follows its parent on the fixed steps, so what rests on
/// it keeps the parent's pace at every frame rate.
/// </summary>
/// <remarks>
/// <para>
/// A parent the program moves in its update moves once a frame, and a frame holds as many steps as
/// its time comes to, none, one or several. Aiming each step at the parent's place gave the body the
/// whole frame's distance in the first step and nothing in the rest, so a crate on a platform rode
/// at 1.75 of a pace of 2 at 144 frames a second, 2.37 at 50 and 0.06 at 20. Instead, the parent's
/// place is observed once the frame's update has moved it, its velocity is that move over the
/// frame's time, and each step aims at the observed place moved on by that velocity for the time
/// from the observation to the end of the step. The steps run behind the frame's clock by what the
/// fixed step has not yet stepped through, so that time starts below zero, and a step that ends
/// before the moment observed aims behind the place observed, which keeps the body at the parent's
/// speed through every step however the frames fall. A parent that stops is reached in the next
/// step.
/// </para>
/// <para>
/// A parent moved in the steps themselves, by an <c>[OnFixedUpdate]</c> behavior, has not moved
/// since the last step when the frame ends, and is followed as before, each step aiming at its
/// place, which such a parent moves a step's distance a step. A parent the program says it placed
/// with <see cref="PhysicsWorld.MarkPlaced"/>, as when a level starts again, or one that jumps more
/// than <see cref="PhysicsSettings.PlaceBeyond"/> in a frame, is placing its body, which is put
/// there rather than flung there through whatever is between.
/// </para>
/// </remarks>
internal sealed class ParentFollowers
{
    private sealed class Follower
    {
        public required Entity Entity;
        // The parent's place for the body, and its turn, as last observed or stepped to.
        public Vector3 Place;
        public Quaternion Turn = Quaternion.Identity;
        // Where the parent was at the last step, which says whether it moved since.
        public Vector3 SteppedPlace;
        public Quaternion SteppedTurn = Quaternion.Identity;
        // The parent's velocity and spin over the frame observed, and the seconds from the moment
        // observed to the end of the last step, below zero while the steps are behind it.
        public Vector3 Velocity;
        public Vector3 Spin;
        public double Since;
        // Whether the parent moves once a frame and is followed by its velocity, or is aimed at.
        public bool ByFrame;
    }

    private readonly Dictionary<int, Follower> _followers = [];

    // The bodies observed in a frame, and those no longer there, kept and used again each frame.
    private readonly HashSet<int> _seen = [];
    private readonly List<int> _gone = [];

    // The pose under the parent each kinematic body under one is to have, as its own Transform puts it.
    private readonly List<(int Entity, PhysicsBody Body, Vector3 Place, Quaternion Turn)> _wanted = [];

    private List<(int Entity, PhysicsBody Body, Vector3 Place, Quaternion Turn)> Wanted(EcsWorld ecs)
    {
        _wanted.Clear();
        foreach (var row in ecs.QueryReadOnly<PhysicsBody, Transform>())
        {
            if (row.C1.Kind != BodyKind.Kinematic) continue;
            var parent = ecs.ParentOf(row.Entity);
            if (parent == 0) continue;
            var world = TransformPropagation.ToMatrix(row.C2) * TransformPropagation.ComposedWorldMatrix(ecs, parent);
            if (Matrix4x4.Decompose(world, out _, out var rotation, out var position))
                _wanted.Add((row.Entity, row.C1, position, rotation));
        }
        return _wanted;
    }

    private Follower For(EcsWorld ecs, int entity, Vector3 place, Quaternion turn)
    {
        var handle = ecs.Handle(entity);
        if (_followers.TryGetValue(entity, out var known) && known.Entity == handle) return known;
        return _followers[entity] = new Follower { Entity = handle, Place = place, Turn = turn, SteppedPlace = place, SteppedTurn = turn };
    }

    /// <summary>Moves each body over a step of <paramref name="seconds"/> toward where its parent is to be.</summary>
    public void Step(EcsWorld ecs, PhysicsWorld physics, float seconds)
    {
        foreach (var (entity, body, place, turn) in Wanted(ecs))
        {
            var follower = For(ecs, entity, place, turn);
            if (follower.ByFrame)
            {
                follower.Since += seconds;
                var ahead = (float)follower.Since;
                var aim = follower.Place + follower.Velocity * ahead;
                var spin = follower.Spin * ahead;
                var aimTurn = spin.LengthSquared() > 1e-12f
                    ? Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.Normalize(spin), spin.Length()) * follower.Turn)
                    : follower.Turn;
                physics.FollowPose(body, aim, aimTurn, seconds);
            }
            else physics.FollowPose(body, place, turn, seconds);
            (follower.SteppedPlace, follower.SteppedTurn) = (place, turn);
        }
    }

    /// <summary>
    /// Observes each parent once the frame's update has moved it, <paramref name="frameSeconds"/>
    /// after the frame before, with the steps <paramref name="behind"/> the moment observed by the
    /// time not yet stepped through, and forgets the bodies gone.
    /// </summary>
    public void Observe(EcsWorld ecs, PhysicsWorld physics, double frameSeconds, double behind)
    {
        _seen.Clear();
        foreach (var (entity, body, place, turn) in Wanted(ecs))
        {
            _seen.Add(entity);
            var follower = For(ecs, entity, place, turn);
            var moved = place != follower.SteppedPlace || turn != follower.SteppedTurn;
            if (moved && (Vector3.Distance(place, follower.Place) > physics.Settings.PlaceBeyond || SaidPlaced(ecs, physics, entity)))
            {
                // Placed, not moved, and at rest there.
                physics.SetPosition(body, place);
                physics.SetRotation(body, turn);
                physics.SetLinearVelocity(body, Vector3.Zero);
                physics.SetAngularVelocity(body, Vector3.Zero);
                (follower.Velocity, follower.Spin, follower.ByFrame) = (Vector3.Zero, Vector3.Zero, false);
            }
            else if (moved && frameSeconds > 0)
            {
                var time = (float)frameSeconds;
                follower.Velocity = (place - follower.Place) / time;
                var delta = Quaternion.Normalize(turn * Quaternion.Conjugate(follower.Turn));
                if (delta.W < 0) delta = -delta;
                var half = MathF.Acos(Math.Clamp(delta.W, -1f, 1f));
                var sin = MathF.Sin(half);
                follower.Spin = sin > 1e-6f ? new Vector3(delta.X, delta.Y, delta.Z) / sin * (2 * half / time) : Vector3.Zero;
                follower.ByFrame = true;
            }
            else
            {
                // Still, or moved in the steps themselves: aimed at where it is.
                (follower.Velocity, follower.Spin, follower.ByFrame) = (Vector3.Zero, Vector3.Zero, false);
            }
            (follower.Place, follower.Turn, follower.Since) = (place, turn, -behind);
            (follower.SteppedPlace, follower.SteppedTurn) = (place, turn);
        }
        _gone.Clear();
        foreach (var entity in _followers.Keys)
            if (!_seen.Contains(entity)) _gone.Add(entity);
        foreach (var entity in _gone) _followers.Remove(entity);
        physics.Placed.Clear();
    }

    // Whether the program said it placed the body or any entity above it in this frame.
    private static bool SaidPlaced(EcsWorld ecs, PhysicsWorld physics, int entity)
    {
        if (physics.Placed.Count == 0) return false;
        for (var at = entity; at != 0; at = ecs.ParentOf(at))
            if (physics.Placed.Contains(at)) return true;
        return false;
    }
}
