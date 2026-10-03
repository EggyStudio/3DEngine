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
        if (body.Kind == BodyKind.Static)
        {
            Simulation.Statics.Remove(new StaticHandle(body.Handle));
            _staticToEntity.Remove(body.Handle);
        }
        else
        {
            Simulation.Bodies.Remove(new BodyHandle(body.Handle));
            _bodyToEntity.Remove(body.Handle);
        }
    }

    // -- Pose --

    /// <inheritdoc />
    public Vector3 GetPosition(PhysicsBody body)
    {
        if (body.Kind == BodyKind.Static)
            return Simulation.Statics.GetStaticReference(new StaticHandle(body.Handle)).Pose.Position;
        return Simulation.Bodies.GetBodyReference(new BodyHandle(body.Handle)).Pose.Position;
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
        }
    }

    // -- Velocities --

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

    // -- Forces / impulses --

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

    // -- Sleep state --

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