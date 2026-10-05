using System.Numerics;
using Engine;
using static Engine.Engine3D;

/// <summary>
/// The car: the engine's vehicle, one box held up by four raycast wheels, with what the race needs
/// beside it, its reset, the way an autopilot drives it and how long it has been stuck.
/// </summary>
public sealed class Car
{
    public static Car? Current;

    public static readonly Vector3 Half = new(0.9f, 0.3f, 1.9f);
    public const float WheelRadius = 0.38f, Mass = 1000;

    // Where each wheel is fixed to the body, front left, front right, rear left, rear right.
    private static readonly Vehicle Tuning = new()
    {
        Wheels = [new(-0.85f, -0.15f, -1.35f), new(0.85f, -0.15f, -1.35f), new(-0.85f, -0.15f, 1.35f), new(0.85f, -0.15f, 1.35f)],
        WheelRadius = WheelRadius,
    };

    public PhysicsBody Body { get; }
    // The wheels as the last step left them, read once a frame.
    public VehicleWheel[] Wheels { get; private set; } = [];
    public (float Throttle, float Steer, bool Brake) Input;
    public float Steer => Wheels.Length > 0 ? Wheels[0].Steer : 0;
    // Seconds the car has been upside down or barely moving while asked to drive.
    public float Stuck { get; private set; }

    public Car(Vector3 at) => Body = CreatePhysicsVehicle(at, Half * 2, Mass, Tuning);

    public Vector3 Position => GetPhysicsBodyPosition(Body);
    public Quaternion Rotation => GetPhysicsBodyRotation(Body);
    public float Speed => GetPhysicsBodyVelocity(Body).Length();

    /// <summary>Puts the car at a point, upright, facing along a way, at rest.</summary>
    public void Reset(Vector3 at, Vector3 facing)
    {
        var yaw = MathF.Atan2(-facing.X, -facing.Z);
        SetPhysicsBodyPosition(Body, at + new Vector3(0, GetPhysicsVehicle(Body).SuspensionLength + WheelRadius + 0.3f, 0));
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

    /// <summary>Hands the input to the vehicle and reads its wheels back, once a frame.</summary>
    public void Update(float dt)
    {
        SetPhysicsVehicleInput(Body, Input.Throttle, Input.Steer, Input.Brake);
        Wheels = GetPhysicsVehicleWheels(Body);
        // A car on its roof or sitting still while asked to go counts toward a reset.
        var upsideDown = Vector3.Transform(Vector3.UnitY, Rotation).Y < 0.3f;
        var idle = Wheels.Any(w => w.Grounded) && MathF.Abs(Input.Throttle) > 0.5f && Speed < 1;
        Stuck = upsideDown || idle ? Stuck + dt : 0;
    }
}
