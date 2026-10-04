using System.Numerics;
using BepuPhysics;

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
    public void Destroy(PhysicsBody body)
    {
        // A handle is given out again, and the next body to have it is no trigger unless asked.
        _triggerFlags.Set(body, false);
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
            ForgetCharacter(body.Handle);
            _joined.RemoveBody(body.Handle);
        }
    }

    // -- Pose

    /// <inheritdoc />
    public Vector3 GetPosition(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static)
            return Simulation.Statics.GetStaticReference(new StaticHandle(body.Handle)).Pose.Position;
        return Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Pose.Position;
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
        return (Vector3.Lerp(before.Position, position, alpha), Quaternion.Slerp(before.Orientation, rotation, alpha));
    }

    /// <inheritdoc />
    public Quaternion GetRotation(PhysicsBody body)
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
            br.Pose.Position = position;
            br.Awake = true;
            br.UpdateBounds();
            // A teleport, so it is not blended into.
            _previousPoses.Remove(body.Handle);
        }
    }

    /// <inheritdoc />
    public void SetRotation(PhysicsBody body, Quaternion rotation)
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
            br.Pose.Orientation = rotation;
            br.Awake = true;
            br.UpdateBounds();
            _previousPoses.Remove(body.Handle);
        }
    }

    // -- Velocities

    /// <inheritdoc />
    public Vector3 GetLinearVelocity(PhysicsBody body)
        => body.Kind == BodyKind.Static
            ? Vector3.Zero
            : Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Velocity.Linear;

    /// <inheritdoc />
    public Vector3 GetAngularVelocity(PhysicsBody body)
        => body.Kind == BodyKind.Static
            ? Vector3.Zero
            : Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Velocity.Angular;

    /// <inheritdoc />
    public void SetLinearVelocity(PhysicsBody body, Vector3 velocity)
    {
        if (body.Kind == BodyKind.Static) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.Velocity.Linear = velocity;
        br.Awake = true;
    }

    /// <inheritdoc />
    public void SetAngularVelocity(PhysicsBody body, Vector3 velocity)
    {
        if (body.Kind == BodyKind.Static) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.Velocity.Angular = velocity;
        br.Awake = true;
    }

    /// <summary>
    /// Gives a kinematic body the velocities that carry it to <paramref name="position"/> and
    /// <paramref name="rotation"/> over the next <paramref name="seconds"/>, so it reaches the pose
    /// by moving there, which carries what rests on it, rather than by being put there.
    /// </summary>
    public void FollowPose(PhysicsBody body, Vector3 position, Quaternion rotation, float seconds)
    {
        if (body.Kind != BodyKind.Kinematic || seconds <= 0 || !Simulation.Bodies.BodyExists(new BodyHandle(body.Handle))) return;
        var reference = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        reference.Velocity.Linear = (position - reference.Pose.Position) / seconds;

        // The turn from the pose it has to the one it is to have, the short way round.
        var turn = Quaternion.Normalize(rotation * Quaternion.Conjugate(reference.Pose.Orientation));
        if (turn.W < 0) turn = -turn;
        var half = MathF.Acos(Math.Clamp(turn.W, -1f, 1f));
        var sin = MathF.Sin(half);
        reference.Velocity.Angular = sin > 1e-6f ? new Vector3(turn.X, turn.Y, turn.Z) / sin * (2 * half / seconds) : Vector3.Zero;
        reference.Awake = true;
    }

    // -- Forces / impulses

    /// <inheritdoc />
    public void ApplyImpulse(PhysicsBody body, Vector3 impulse, Vector3 offsetFromCenter)
    {
        if (body.Kind != BodyKind.Dynamic) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.ApplyImpulse(impulse, offsetFromCenter);
        br.Awake = true;
    }

    /// <inheritdoc />
    public void ApplyAngularImpulse(PhysicsBody body, Vector3 impulse)
    {
        if (body.Kind != BodyKind.Dynamic) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.ApplyAngularImpulse(impulse);
        br.Awake = true;
    }

    // -- Sleep state

    /// <inheritdoc />
    public bool IsAwake(PhysicsBody body)
        => body.Kind != BodyKind.Static && Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Awake;

    /// <inheritdoc />
    public void Wake(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static) return;
        Simulation.Awakener.AwakenBody(new BodyHandle(body.Handle));
    }

    /// <inheritdoc />
    public void Sleep(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static) return;
        var br = Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle));
        br.Awake = false;
    }
}