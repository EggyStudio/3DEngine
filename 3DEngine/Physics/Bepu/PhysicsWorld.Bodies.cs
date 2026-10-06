using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;

namespace Engine;

/// <summary>Per-body operations: existence, pose, velocity, impulses, sleep state, destruction.</summary>
public sealed partial class PhysicsWorld
{
    /// <inheritdoc />
    public bool Exists(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static)
            return Simulation.Statics.HandleToIndex.Length > body.Handle &&
                   Simulation.Statics.HandleToIndex[body.Handle] >= 0;
        return Simulation.Bodies.HandleToLocation.Length > body.Handle &&
               Simulation.Bodies.HandleToLocation[body.Handle].SetIndex >= 0;
    }

    /// <inheritdoc />
    internal void Destroy(PhysicsBody body)
    {
        // A handle is given out again, and the next body to have it is no trigger and on layer 0
        // unless asked.
        _triggerFlags.Set(body, false);
        _layers.Set(body, 0);
        ForgetImpulses(body);
        if (body.Kind == BodyKind.Static)
        {
            Simulation.Statics.Remove(new StaticHandle(body.Handle));
            _staticToEntity.Remove(body.Handle);
        }
        else
        {
            Simulation.Bodies.Remove(new BodyHandle(body.Handle));
            _bodyToEntity.Remove(body.Handle);
            _previousPoses.Remove(body.Handle);
            _origins.Remove(body.Handle);
            ForgetCharacter(body.Handle);
            ForgetVehicle(body.Handle);
            _joined.RemoveBody(body.Handle);
        }
    }

    // -- Pose

    // Where the origin of a body's entity is from the body's center, in the body's own frame, for
    // a body whose shape is not centered on that origin, as a convex hull of a mesh is, so its pose
    // is read and set as where the mesh is drawn, and the solver keeps the center of mass.
    private readonly Dictionary<int, Vector3> _origins = [];

    private Vector3 Origin(int handle, Quaternion rotation) =>
        _origins.TryGetValue(handle, out var origin) ? Vector3.Transform(origin, rotation) : Vector3.Zero;

    /// <inheritdoc />
    public Vector3 GetPosition(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static)
            return Simulation.Statics.GetStaticReference(new StaticHandle(body.Handle)).Pose.Position;
        var pose = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Pose;
        return pose.Position + Origin(body.Handle, pose.Orientation);
    }

    /// <summary>
    /// A body's pose blended from where it was before the last step (at 0) to where it is (at 1),
    /// as <see cref="SyncTransforms(EcsWorld, float)"/> writes it, or as it is for a static body
    /// or one with no earlier pose.
    /// </summary>
    public (Vector3 Position, Quaternion Rotation) GetPose(PhysicsBody body, float alpha)
    {
        var position = GetPosition(body);
        var rotation = GetRotation(body);
        if (body.Kind == BodyKind.Static || alpha >= 1 || !_previousPoses.TryGetValue(body.Handle, out var before))
            return (position, rotation);
        alpha = Math.Clamp(alpha, 0, 1);
        var blended = Quaternion.Slerp(before.Orientation, rotation, alpha);
        var center = Vector3.Lerp(before.Position, position - Origin(body.Handle, rotation), alpha);
        return (center + Origin(body.Handle, blended), blended);
    }

    /// <inheritdoc />
    internal Quaternion GetRotation(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static)
            return Simulation.Statics.GetStaticReference(new StaticHandle(body.Handle)).Pose.Orientation;
        return Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Pose.Orientation;
    }

    /// <inheritdoc />
    public void SetPosition(PhysicsBody body, Vector3 position)
    {
        if (body.Kind == BodyKind.Static)
        {
            var sh = new StaticHandle(body.Handle);
            var sref = Simulation.Statics.GetStaticReference(sh);
            sref.Pose.Position = position;
            Simulation.Statics.UpdateBounds(sh);
        }
        else
        {
            var bh = new BodyHandle(body.Handle);
            var br = Simulation.Bodies.GetBodyReference(bh);
            br.Pose.Position = position - Origin(body.Handle, br.Pose.Orientation);
            Wake(br);
            br.UpdateBounds();
            // A teleport, so it is not blended into.
            _previousPoses.Remove(body.Handle);
        }
    }

    /// <inheritdoc />
    internal void SetRotation(PhysicsBody body, Quaternion rotation)
    {
        if (body.Kind == BodyKind.Static)
        {
            var sh = new StaticHandle(body.Handle);
            var sref = Simulation.Statics.GetStaticReference(sh);
            sref.Pose.Orientation = rotation;
            Simulation.Statics.UpdateBounds(sh);
        }
        else
        {
            var bh = new BodyHandle(body.Handle);
            var br = Simulation.Bodies.GetBodyReference(bh);
            // Turned about its entity's origin, which stays where it is.
            var origin = br.Pose.Position + Origin(body.Handle, br.Pose.Orientation);
            br.Pose.Orientation = rotation;
            br.Pose.Position = origin - Origin(body.Handle, rotation);
            Wake(br);
            br.UpdateBounds();
            _previousPoses.Remove(body.Handle);
        }
    }

    // -- Velocities

    /// <inheritdoc />
    internal Vector3 GetLinearVelocity(PhysicsBody body)
        => body.Kind == BodyKind.Static
            ? Vector3.Zero
            : Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Velocity.Linear;

    /// <inheritdoc />
    internal Vector3 GetAngularVelocity(PhysicsBody body)
        => body.Kind == BodyKind.Static
            ? Vector3.Zero
            : Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Velocity.Angular;

    /// <inheritdoc />
    public void SetLinearVelocity(PhysicsBody body, Vector3 velocity)
    {
        if (body.Kind == BodyKind.Static) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.Velocity.Linear = velocity;
        Wake(br);
    }

    /// <inheritdoc />
    /// <summary>
    /// Sweeps a body over each step to find what it would meet within it, for a body fast enough to
    /// cross a thin wall in one step, as a shot or a ball struck hard is, or stops sweeping it.
    /// </summary>
    /// <remarks>
    /// A body otherwise meets what is within a tenth of a unit of it at the start of a step, so a
    /// ball of 20 units a second crossed a wall a fifth of a unit thick. A swept body's contacts
    /// reach as far as it moves, which costs a sweep test for each pair it nears, so it is for the
    /// few bodies a game knows are fast. A contact stops a body over a step rather than at once,
    /// its spring the stiffest a step of a sixtieth of a second solves, so one at 100 units a
    /// second needs a wall a fifth of a unit thick and one at 300 half a unit, measured with a ball
    /// of a tenth of a unit across.
    /// </remarks>
    internal void SetContinuous(PhysicsBody body, bool continuous)
    {
        if (body.Kind == BodyKind.Static || !Exists(body)) return;
        var reference = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        reference.Collidable.Continuity = continuous ? ContinuousDetection.Continuous(1e-3f, 1e-3f) : ContinuousDetection.Passive;
        reference.Collidable.MaximumSpeculativeMargin = continuous ? float.MaxValue : SpeculativeMargin;
    }

    internal void SetAngularVelocity(PhysicsBody body, Vector3 velocity)
    {
        if (body.Kind == BodyKind.Static) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.Velocity.Angular = velocity;
        Wake(br);
    }

    /// <summary>
    /// Gives a kinematic body the velocities that carry it to <paramref name="position"/> and
    /// <paramref name="rotation"/> over the next <paramref name="seconds"/>, so it reaches the pose
    /// by moving there, which carries what rests on it, rather than by being put there.
    /// </summary>
    internal void FollowPose(PhysicsBody body, Vector3 position, Quaternion rotation, float seconds)
    {
        if (body.Kind != BodyKind.Kinematic || seconds <= 0 || !Simulation.Bodies.BodyExists(new BodyHandle(body.Handle))) return;
        var reference = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        reference.Velocity.Linear = (position - Origin(body.Handle, rotation) - reference.Pose.Position) / seconds;

        // The turn from the pose it has to the one it is to have, the short way round.
        var turn = Quaternion.Normalize(rotation * Quaternion.Conjugate(reference.Pose.Orientation));
        if (turn.W < 0) turn = -turn;
        var half = MathF.Acos(Math.Clamp(turn.W, -1f, 1f));
        var sin = MathF.Sin(half);
        reference.Velocity.Angular = sin > 1e-6f ? new Vector3(turn.X, turn.Y, turn.Z) / sin * (2 * half / seconds) : Vector3.Zero;
        Wake(reference);
    }

    // -- Forces / impulses

    /// <inheritdoc />
    internal void ApplyImpulse(PhysicsBody body, Vector3 impulse, Vector3 offsetFromCenter)
    {
        if (body.Kind != BodyKind.Dynamic) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.ApplyImpulse(impulse, offsetFromCenter);
        Wake(br);
    }

    /// <summary>
    /// Pushes a body at a point in the world, which turns it as well as moving it unless the point
    /// is its center of mass, as a wheel's grip or a hit on a corner does.
    /// </summary>
    internal void ApplyImpulseAt(PhysicsBody body, Vector3 impulse, Vector3 point)
    {
        if (body.Kind != BodyKind.Dynamic) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.ApplyImpulse(impulse, point - br.Pose.Position);
        Wake(br);
    }

    /// <summary>How fast a point of a body moves, its own velocity and the turn about its center of mass at that point.</summary>
    internal Vector3 GetPointVelocity(PhysicsBody body, Vector3 point)
    {
        if (body.Kind == BodyKind.Static) return Vector3.Zero;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        return br.Velocity.Linear + Vector3.Cross(br.Velocity.Angular, point - br.Pose.Position);
    }

    /// <inheritdoc />
    internal void ApplyAngularImpulse(PhysicsBody body, Vector3 impulse)
    {
        if (body.Kind != BodyKind.Dynamic) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.ApplyAngularImpulse(impulse);
        Wake(br);
    }

    // -- Sleep state

    /// <inheritdoc />
    internal bool IsAwake(PhysicsBody body)
        => body.Kind != BodyKind.Static && Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Awake;

    /// <inheritdoc />
    internal void Wake(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static) return;
        Wake(Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)));
    }

    // Wakes a body and starts its count of steps at rest again. Bepu marks a body that has rested
    // long enough as a candidate to sleep as a step ends and puts candidates to sleep as the next
    // begins, which waking a body still awake does not undo, so a body moved or given speed between
    // the two would sleep through it.
    private static void Wake(BodyReference body)
    {
        body.Awake = true;
        ref var activity = ref body.Activity;
        activity.SleepCandidate = false;
        activity.TimestepsUnderThresholdCount = 0;
    }

    /// <inheritdoc />
    internal void Sleep(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.Awake = false;
    }
}