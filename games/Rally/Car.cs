using System.Numerics;
using Engine;
using static Engine.Engine3D;

/// <summary>
/// The car: one box body held up by four wheels, each a ray cast down from the body to the ground,
/// which pushes the body up as a spring and damper where it is pressed, grips the ground sideways
/// up to what the press allows, and drives or brakes along the way it points.
/// </summary>
public sealed class Car
{
    public static Car? Current;

    public static readonly Vector3 Half = new(0.9f, 0.3f, 1.9f);
    public const float WheelRadius = 0.38f, Rest = 0.45f, Mass = 1000;
    private const float Spring = 42000, Damper = 4200, Engine = 11000, Braking = 14000, Grip = 1.15f, MaxSteer = 0.55f;

    // Where each wheel is fixed to the body, front left, front right, rear left, rear right.
    private static readonly Vector3[] Mounts = [new(-0.85f, -0.15f, -1.35f), new(0.85f, -0.15f, -1.35f), new(-0.85f, -0.15f, 1.35f), new(0.85f, -0.15f, 1.35f)];

    public sealed class Wheel
    {
        public bool Grounded;
        public Vector3 Center, Contact;
        // How fast the tyre slides sideways over the ground, and how far it has rolled, for its look.
        public float Slip, Roll;
    }

    public PhysicsBody Body { get; }
    public Wheel[] Wheels { get; } = [new(), new(), new(), new()];
    public (float Throttle, float Steer, bool Brake) Input;
    public float Steer { get; private set; }
    // Seconds the car has been upside down or barely moving while asked to drive.
    public float Stuck { get; private set; }

    public Car(Vector3 at) => Body = CreatePhysicsBox(at, Half, Mass);

    public Vector3 Position => GetPhysicsBodyPosition(Body);
    public Quaternion Rotation => GetPhysicsBodyRotation(Body);
    public float Speed => GetPhysicsBodyVelocity(Body).Length();

    /// <summary>Puts the car at a point, upright, facing along a way, at rest.</summary>
    public void Reset(Vector3 at, Vector3 facing)
    {
        var yaw = MathF.Atan2(-facing.X, -facing.Z);
        SetPhysicsBodyPosition(Body, at + new Vector3(0, Rest + WheelRadius + 0.3f, 0));
        SetPhysicsBodyRotation(Body, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw));
        SetPhysicsBodyVelocity(Body, Vector3.Zero);
        SetPhysicsBodyAngularVelocity(Body, Vector3.Zero);
        Stuck = 0;
    }

    /// <summary>The throttle and steering that head the car toward a point, slower into a sharp turn.</summary>
    public (float Throttle, float Steer) Toward(Vector3 target)
    {
        var forward = Vector3.Transform(-Vector3.UnitZ, Rotation);
        var to = target - Position;
        var angle = MathF.Atan2(Vector3.Dot(Vector3.Cross(forward, to), Vector3.UnitY), Vector3.Dot(forward, to));
        var steer = Math.Clamp(angle * 2.5f, -1, 1);
        // About 80 km/h along a straight and slower the sharper the turn ahead.
        var wanted = 22 - 14 * MathF.Min(1, MathF.Abs(angle));
        return (Math.Clamp((wanted - Speed) / 4, -1, 1), steer);
    }

    /// <summary>One fixed step of the wheels' pushes, as the physics steps.</summary>
    public void Step(float dt)
    {
        var rotation = Rotation;
        var center = Position;
        var up = Vector3.Transform(Vector3.UnitY, rotation);
        var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        var right = Vector3.Transform(Vector3.UnitX, rotation);
        var velocity = GetPhysicsBodyVelocity(Body);
        var ahead = Vector3.Dot(velocity, forward);

        // Steering turns less the faster the car goes, so it does not spin at speed.
        var steerWanted = Input.Steer * MaxSteer / (1 + MathF.Abs(ahead) / 20);
        Steer += (steerWanted - Steer) * MathF.Min(1, dt * 8);

        var grounded = 0;
        for (int i = 0; i < 4; i++)
        {
            var wheel = Wheels[i];
            var mount = center + Vector3.Transform(Mounts[i], rotation);
            var reach = Rest + WheelRadius;
            if (!GetRayCollisionPhysicsEx(new Ray(mount, -up), reach, Body, out var hit))
            {
                wheel.Grounded = false;
                wheel.Center = mount - up * Rest;
                wheel.Slip = 0;
                continue;
            }
            grounded++;
            wheel.Grounded = true;
            wheel.Contact = hit.Point;
            wheel.Center = mount - up * (hit.Distance - WheelRadius);

            // The spring pushes up as far as it is pressed, and the damper against how fast it is
            // pressed, never pulling the car down.
            var pressed = reach - hit.Distance;
            var pointVelocity = GetPhysicsBodyPointVelocity(Body, mount);
            var closing = -Vector3.Dot(pointVelocity, up);
            var load = MathF.Max(0, Spring * pressed + Damper * closing);
            ApplyPhysicsImpulseAt(Body, up * load * dt, mount);

            // The way the wheel points and its side, along the ground.
            var turn = i < 2 ? Quaternion.CreateFromAxisAngle(up, Steer) : Quaternion.Identity;
            var along = Flatten(Vector3.Transform(forward, turn), hit.Normal);
            var side = Flatten(Vector3.Transform(right, turn), hit.Normal);
            var contactVelocity = GetPhysicsBodyPointVelocity(Body, hit.Point);
            var sliding = Vector3.Dot(contactVelocity, side);
            var rolling = Vector3.Dot(contactVelocity, along);
            wheel.Slip = sliding;
            wheel.Roll += rolling * dt / WheelRadius;

            // Grip cancels the sideways slide, up to what the load on the tyre allows, pushed at the
            // body's height so the car leans less than a push at the ground would lean it.
            var most = Grip * load * dt;
            var sideways = Math.Clamp(-sliding * Mass / 4, -most, most);
            var at = hit.Point + up * (Vector3.Dot(center - hit.Point, up) * 0.7f);
            ApplyPhysicsImpulseAt(Body, side * sideways, at);

            // The rear wheels drive, and every wheel brakes, or rolls to a stop slowly.
            var push = 0f;
            if (i >= 2 && !Input.Brake) push += Input.Throttle * Engine / 2;
            if (Input.Brake || (Input.Throttle < 0 && rolling > 1)) push -= MathF.Sign(rolling) * Braking / 4;
            push -= rolling * 30;
            ApplyPhysicsImpulseAt(Body, along * Math.Clamp(push * dt, -most * 1.5f, most * 1.5f), at);
        }

        // Air drag, which holds the car near 150 km/h, and air pressing it down as it goes faster,
        // so it keeps its grip over the crests.
        ApplyPhysicsImpulse(Body, -velocity * velocity.Length() * 5 * dt);
        if (grounded > 0) ApplyPhysicsImpulse(Body, -up * velocity.LengthSquared() * 3 * dt);

        // Rolling and pitching settle rather than build, on the ground, and in the air the car turns
        // itself level a little, as a driver's weight and the wheels' spin would.
        var spin = GetPhysicsBodyAngularVelocity(Body);
        var settle = 1 - MathF.Exp(-(grounded > 0 ? 6 : 1.5f) * dt);
        spin -= (Vector3.Dot(spin, forward) * forward + Vector3.Dot(spin, right) * right) * settle;
        if (grounded == 0) spin += Vector3.Cross(up, Vector3.UnitY) * 2 * dt;
        SetPhysicsBodyAngularVelocity(Body, spin);

        // A car on its roof or sitting still while asked to go counts toward a reset.
        var upsideDown = up.Y < 0.3f;
        var idle = grounded > 0 && MathF.Abs(Input.Throttle) > 0.5f && velocity.Length() < 1;
        Stuck = upsideDown || idle ? Stuck + dt : 0;
    }

    private static Vector3 Flatten(Vector3 v, Vector3 normal)
    {
        var flat = v - Vector3.Dot(v, normal) * normal;
        return flat.LengthSquared() > 1e-6f ? Vector3.Normalize(flat) : v;
    }
}

/// <summary>Steps the car on the physics' fixed steps, before each step runs.</summary>
[Behavior]
public struct Driving
{
    [OnFixedUpdate]
    public static void Drive(BehaviorContext ctx) => Car.Current?.Step(ctx.FixedDelta);
}
