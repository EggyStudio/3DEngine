using System.Numerics;
using BepuPhysics;

namespace Engine;

/// <summary>Which of a vehicle's wheels the engine turns.</summary>
public enum VehicleDrive
{
    /// <summary>The back two, as most cars.</summary>
    Rear,
    /// <summary>The front two.</summary>
    Front,
    /// <summary>All four.</summary>
    All,
}

/// <summary>
/// How a vehicle rides and drives: its wheels and their springs, its engine, brakes, grip and
/// steering, and the air it moves through. The defaults are a car of about 1000 kg.
/// </summary>
/// <remarks>
/// A wheel is a ray cast down from its mount, which pushes the body up as a spring as far as the
/// ground presses it and as a damper against how fast it is pressed, never pulling it down. The
/// tyre grips sideways up to <see cref="Grip"/> times what presses it, and drives and brakes along
/// the way it points up to half again that. Forces are in newtons, for the body's mass in kilograms.
/// </remarks>
public record struct Vehicle()
{
    /// <summary>Where each wheel is fixed to the body in its own space, front first, left before right. Null for four at its corners.</summary>
    public Vector3[]? Wheels { get; init; }

    /// <summary>A wheel's radius.</summary>
    public float WheelRadius { get; init; } = 0.38f;

    /// <summary>How far below its mount a wheel's middle hangs with nothing pressing it.</summary>
    public float SuspensionLength { get; init; } = 0.45f;

    /// <summary>How hard a wheel's spring pushes for each unit it is pressed.</summary>
    public float Spring { get; init; } = 42000;

    /// <summary>How hard it pushes back against each unit a second it is pressed at.</summary>
    public float Damper { get; init; } = 4200;

    /// <summary>The engine's push at full throttle, shared by the driven wheels.</summary>
    public float EngineForce { get; init; } = 11000;

    /// <summary>The brakes' push, shared by every wheel.</summary>
    public float BrakeForce { get; init; } = 14000;

    /// <summary>How much of what presses a tyre it can push sideways before it slides.</summary>
    public float Grip { get; init; } = 1.15f;

    /// <summary>How far the front wheels turn at full lock, in radians, less the faster the vehicle goes.</summary>
    public float MaxSteer { get; init; } = 0.55f;

    /// <summary>Which wheels the engine drives.</summary>
    public VehicleDrive Drive { get; init; } = VehicleDrive.Rear;

    /// <summary>The air's drag, which grows with the square of the speed and holds a car of the defaults near 150 km/h.</summary>
    public float Drag { get; init; } = 5;

    /// <summary>The air pressing the body down while a wheel is on the ground, with the square of the speed, so it keeps its grip over a crest.</summary>
    public float Downforce { get; init; } = 3;
}

/// <summary>One of a vehicle's wheels as its last step left it, for drawing it and for sounds.</summary>
/// <param name="Center">Where the wheel's middle is, in the world.</param>
/// <param name="Contact">Where it touches the ground, when it does.</param>
/// <param name="Grounded">Whether it touches the ground.</param>
/// <param name="Slip">How fast the tyre slides sideways over the ground, in units a second, which a skid's sound follows.</param>
/// <param name="Spin">How far it has turned about its axle, in radians.</param>
/// <param name="Steer">How far it is turned to steer, in radians, 0 for a wheel that does not steer.</param>
public readonly record struct VehicleWheel(Vector3 Center, Vector3 Contact, bool Grounded, float Slip, float Spin, float Steer);

/// <summary>Vehicles: bodies held up and driven by raycast wheels before every step.</summary>
public sealed partial class PhysicsWorld
{
    private sealed class VehicleState
    {
        public required PhysicsBody Body;
        public required Vehicle Settings;
        public required Vector3[] Mounts;
        public required VehicleWheel[] Wheels;
        public float Throttle, SteerInput, Steer;
        public bool Brake;
    }

    private readonly Dictionary<int, VehicleState> _vehicles = [];

    /// <summary>
    /// A vehicle: a box of <paramref name="size"/> and <paramref name="mass"/> held up and driven by
    /// raycast wheels, facing -Z, which every step pushes as <paramref name="settings"/> says.
    /// </summary>
    internal PhysicsBody CreateVehicle(Vector3 position, Vector3 size, float mass = 1000, Vehicle? settings = null, int entityId = 0)
    {
        var body = CreateBox(position, size / 2, mass, entityId: entityId);
        var tuning = settings ?? new Vehicle();
        var mounts = tuning.Wheels ?? Corners(size / 2);
        _vehicles[body.Handle] = new VehicleState
        {
            Body = body, Settings = tuning, Mounts = mounts, Wheels = new VehicleWheel[mounts.Length],
        };
        return body;
    }

    // Four wheels under the body's corners, a little in from its sides and its ends.
    private static Vector3[] Corners(Vector3 half) =>
    [
        new(-half.X + 0.05f, -half.Y * 0.5f, -half.Z * 0.71f), new(half.X - 0.05f, -half.Y * 0.5f, -half.Z * 0.71f),
        new(-half.X + 0.05f, -half.Y * 0.5f, half.Z * 0.71f), new(half.X - 0.05f, -half.Y * 0.5f, half.Z * 0.71f),
    ];

    /// <summary>Sets what drives a vehicle from the next step on: throttle and steering from -1 to 1, reverse and right being negative.</summary>
    internal void SetVehicleInput(PhysicsBody body, float throttle, float steer, bool brake)
    {
        if (!_vehicles.TryGetValue(body.Handle, out var vehicle)) return;
        vehicle.Throttle = Math.Clamp(throttle, -1, 1);
        vehicle.SteerInput = Math.Clamp(steer, -1, 1);
        vehicle.Brake = brake;
    }

    /// <summary>Changes how a vehicle rides and drives, its wheels' mounts among it.</summary>
    internal void SetVehicle(PhysicsBody body, Vehicle settings)
    {
        if (!_vehicles.TryGetValue(body.Handle, out var vehicle)) return;
        vehicle.Settings = settings;
        if (settings.Wheels is { } wheels && wheels.Length != vehicle.Mounts.Length) vehicle.Wheels = new VehicleWheel[wheels.Length];
        vehicle.Mounts = settings.Wheels ?? vehicle.Mounts;
    }

    /// <summary>How a vehicle rides and drives, or the defaults for a body that is not one.</summary>
    internal Vehicle GetVehicle(PhysicsBody body) => _vehicles.TryGetValue(body.Handle, out var vehicle) ? vehicle.Settings : new Vehicle();

    /// <summary>A vehicle's wheels as its last step left them, empty for a body that is not one.</summary>
    internal ReadOnlySpan<VehicleWheel> GetVehicleWheels(PhysicsBody body) => _vehicles.TryGetValue(body.Handle, out var vehicle) ? vehicle.Wheels : [];

    private void ForgetVehicle(int handle) => _vehicles.Remove(handle);

    // Each vehicle's wheels push it, before the step that moves it.
    private void UpdateVehicles(float dt)
    {
        foreach (var vehicle in _vehicles.Values)
            if (Simulation.Bodies.BodyExists(new BodyHandle(vehicle.Body.Handle))) Drive(vehicle, dt);
    }

    private void Drive(VehicleState vehicle, float dt)
    {
        var tuning = vehicle.Settings;
        var body = Simulation.Bodies.GetBodyReference(new BodyHandle(vehicle.Body.Handle));
        var mass = body.LocalInertia.InverseMass > 0 ? 1 / body.LocalInertia.InverseMass : 1;
        var rotation = body.Pose.Orientation;
        var center = body.Pose.Position;
        var up = Vector3.Transform(Vector3.UnitY, rotation);
        var forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        var right = Vector3.Transform(Vector3.UnitX, rotation);
        var velocity = body.Velocity.Linear;
        var ahead = Vector3.Dot(velocity, forward);

        // Steering turns less the faster the vehicle goes, so it does not spin at speed, and eases
        // toward what is asked rather than snapping to it.
        var steerWanted = vehicle.SteerInput * tuning.MaxSteer / (1 + MathF.Abs(ahead) / 20);
        vehicle.Steer += (steerWanted - vehicle.Steer) * MathF.Min(1, dt * 8);

        var count = vehicle.Mounts.Length;
        var driven = Enumerable.Range(0, count).Count(i => Drives(tuning.Drive, i, count));
        var grounded = 0;
        var reach = tuning.SuspensionLength + tuning.WheelRadius;
        for (int i = 0; i < count; i++)
        {
            var front = i < count / 2;
            var mount = center + Vector3.Transform(vehicle.Mounts[i], rotation);
            var steer = front ? vehicle.Steer : 0;
            var spin = vehicle.Wheels[i].Spin;
            if (!Raycast(mount, -up, reach, vehicle.Body, out var hit))
            {
                vehicle.Wheels[i] = new VehicleWheel(mount - up * tuning.SuspensionLength, default, false, 0, spin, steer);
                continue;
            }
            grounded++;

            // The spring pushes up as far as it is pressed, the damper against how fast, never down.
            var pressed = reach - hit.Distance;
            var closing = -Vector3.Dot(PointVelocity(body, mount), up);
            var load = MathF.Max(0, tuning.Spring * pressed + tuning.Damper * closing);
            body.ApplyImpulse(up * load * dt, mount - center);

            // The way the wheel points and its side, along the ground.
            var turn = steer != 0 ? Quaternion.CreateFromAxisAngle(up, steer) : Quaternion.Identity;
            var along = Flatten(Vector3.Transform(forward, turn), hit.Normal);
            var side = Flatten(Vector3.Transform(right, turn), hit.Normal);
            var contactVelocity = PointVelocity(body, hit.Point);
            var sliding = Vector3.Dot(contactVelocity, side);
            var rolling = Vector3.Dot(contactVelocity, along);
            spin += rolling * dt / tuning.WheelRadius;

            // Grip cancels the sideways slide up to what the load allows, pushed at the body's
            // height so the vehicle leans less than a push at the ground would lean it.
            var most = tuning.Grip * load * dt;
            var sideways = Math.Clamp(-sliding * mass / count, -most, most);
            var at = hit.Point + up * (Vector3.Dot(center - hit.Point, up) * 0.7f);
            body.ApplyImpulse(side * sideways, at - center);

            // Driven wheels push, every wheel brakes, or rolls to a stop slowly.
            var push = 0f;
            if (Drives(tuning.Drive, i, count) && !vehicle.Brake) push += vehicle.Throttle * tuning.EngineForce / driven;
            if (vehicle.Brake || (vehicle.Throttle < 0 && rolling > 1)) push -= MathF.Sign(rolling) * tuning.BrakeForce / count;
            push -= rolling * 30;
            body.ApplyImpulse(along * Math.Clamp(push * dt, -most * 1.5f, most * 1.5f), at - center);

            vehicle.Wheels[i] = new VehicleWheel(mount - up * (hit.Distance - tuning.WheelRadius), hit.Point, true, sliding, spin, steer);
        }

        // The air's drag, and the air pressing it down while it is on the ground.
        body.ApplyLinearImpulse(-velocity * velocity.Length() * tuning.Drag * dt);
        if (grounded > 0) body.ApplyLinearImpulse(-up * velocity.LengthSquared() * tuning.Downforce * dt);

        // Rolling and pitching settle rather than build on the ground, and in the air the body turns
        // itself level a little, as a driver's weight and the wheels' spin would.
        var turning = body.Velocity.Angular;
        var settle = 1 - MathF.Exp(-(grounded > 0 ? 6 : 1.5f) * dt);
        turning -= (Vector3.Dot(turning, forward) * forward + Vector3.Dot(turning, right) * right) * settle;
        if (grounded == 0) turning += Vector3.Cross(up, Vector3.UnitY) * 2 * dt;
        body.Velocity.Angular = turning;
        Wake(body);
    }

    private static bool Drives(VehicleDrive drive, int wheel, int count) => drive switch
    {
        VehicleDrive.Front => wheel < count / 2,
        VehicleDrive.All => true,
        _ => wheel >= count / 2,
    };

    private static Vector3 PointVelocity(BodyReference body, Vector3 point) =>
        body.Velocity.Linear + Vector3.Cross(body.Velocity.Angular, point - body.Pose.Position);

    private static Vector3 Flatten(Vector3 v, Vector3 normal)
    {
        var flat = v - Vector3.Dot(v, normal) * normal;
        return flat.LengthSquared() > 1e-6f ? Vector3.Normalize(flat) : v;
    }
}
