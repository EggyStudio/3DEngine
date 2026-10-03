using System.Numerics;

namespace Engine;

/// <summary>
/// Lightweight value-type handle to a body inside an <see cref="PhysicsWorld"/>.
/// Safe to store on ECS components, copy by value, and use across frames; all operations
/// are forwarded to the owning world via the embedded reference, so the underlying physics
/// engine (BepuPhysics, etc.) is never exposed to user code.
/// </summary>
/// <example>
/// <code>
/// var body = ctx.Physics.CreateCapsule(new Vector3(0, 5, 0), radius: 0.5f, height: 1f);
/// body.SetLinearVelocity(new Vector3(0, 0, 5));
/// body.ApplyImpulse(new Vector3(0, 5, 0));
/// </code>
/// </example>
public readonly struct PhysicsBody : IEquatable<PhysicsBody>
{
    /// <summary>The world that owns this body. <c>null</c> for the default/uninitialised handle.</summary>
    public readonly PhysicsWorld? World;

    /// <summary>Backend-specific handle identifier (Bepu BodyHandle.Value or StaticHandle.Value).</summary>
    public readonly int Handle;

    /// <summary>Whether this handle refers to a dynamic, kinematic or static body.</summary>
    public readonly BodyKind Kind;

    /// <summary>Constructs a body handle. Use <see cref="PhysicsWorld"/> creation methods instead of calling this directly.</summary>
    public PhysicsBody(PhysicsWorld world, int handle, BodyKind kind)
    {
        World = world;
        Handle = handle;
        Kind = kind;
    }

    /// <summary><c>true</c> if this handle is bound to a world (regardless of whether the body still exists).</summary>
    public bool IsValid => World is not null && World.Exists(this);

    // -- Pose --

    /// <summary>World-space position of the body.</summary>
    public Vector3 Position
    {
        get => World!.GetPosition(this);
        set => World!.SetPosition(this, value);
    }

    /// <summary>World-space orientation of the body.</summary>
    public Quaternion Rotation
    {
        get => World!.GetRotation(this);
        set => World!.SetRotation(this, value);
    }

    /// <summary>Sets the body's world-space position.</summary>
    public void SetPosition(Vector3 position) => World!.SetPosition(this, position);

    /// <summary>Sets the body's world-space orientation.</summary>
    public void SetRotation(Quaternion rotation) => World!.SetRotation(this, rotation);

    // -- Velocities --

    /// <summary>Linear velocity in world space (m/s). Static bodies always return zero.</summary>
    public Vector3 Velocity => World!.GetLinearVelocity(this);

    /// <summary>Angular velocity in world space (rad/s). Static bodies always return zero.</summary>
    public Vector3 AngularVelocity => World!.GetAngularVelocity(this);

    /// <summary>Sets the linear velocity (no-op for static bodies). Wakes the body.</summary>
    public void SetLinearVelocity(Vector3 velocity) => World!.SetLinearVelocity(this, velocity);

    /// <summary>Sets the angular velocity (no-op for static bodies). Wakes the body.</summary>
    public void SetAngularVelocity(Vector3 velocity) => World!.SetAngularVelocity(this, velocity);

    // -- Forces / impulses --

    /// <summary>Applies an instantaneous change in momentum at the body's centre of mass.</summary>
    public void ApplyImpulse(Vector3 impulse) => World!.ApplyImpulse(this, impulse, Vector3.Zero);

    /// <summary>Applies an instantaneous change in momentum at a world-space point.</summary>
    public void ApplyImpulseAtPoint(Vector3 impulse, Vector3 worldPoint) =>
        World!.ApplyImpulse(this, impulse, worldPoint - Position);

    /// <summary>Applies an instantaneous change in angular momentum.</summary>
    public void ApplyAngularImpulse(Vector3 impulse) => World!.ApplyAngularImpulse(this, impulse);

    // -- State --

    /// <summary><c>true</c> when the body is awake (being simulated this step).</summary>
    public bool IsAwake => World!.IsAwake(this);

    /// <summary>Forces the body awake.</summary>
    public void Wake() => World!.Wake(this);

    /// <summary>Forces the body asleep (skipped during the next step until something wakes it).</summary>
    public void Sleep() => World!.Sleep(this);

    /// <summary>Removes the body from the world. The handle becomes invalid afterwards.</summary>
    public void Destroy() => World!.Destroy(this);

    /// <inheritdoc />
    public bool Equals(PhysicsBody other) =>
        ReferenceEquals(World, other.World) && Handle == other.Handle && Kind == other.Kind;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PhysicsBody b && Equals(b);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Handle, (int)Kind);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(PhysicsBody a, PhysicsBody b) => a.Equals(b);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(PhysicsBody a, PhysicsBody b) => !a.Equals(b);
}