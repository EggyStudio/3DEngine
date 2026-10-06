using System.Numerics;
using BepuPhysics;

namespace Engine;

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
