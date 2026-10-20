using System.Numerics;
using Engine;
using static Engine.E3D;

/// <summary>
/// The catapult, an arm on a hinge across the field driven by the hinge's motor, swung from behind
/// and below its pivot up and over toward the castle, its shot held at the cup by a weld the game
/// destroys at the angle the shot is to leave at, the cup's speed then the shot's.
/// </summary>
public sealed class Catapult
{
    public static readonly Vector3 Pivot = new(0, 3.8f, 0);
    public const float ArmLength = 4.5f, ArmMass = 120;
    // The arm rests pointing back and down, and swings over the top, the angle of the way it points
    // falling as it goes. A shot leaves along the way the cup moves, 90 degrees below the arm's.
    public const float RestDegrees = 225;
    // The most torque the motor gives, enough to swing the arm and a shot to full speed within a
    // quarter turn.
    public const float Torque = 60000;

    public enum Ammo { Stone, Bomb }

    public readonly PhysicsBody Anchor, Arm;
    public readonly PhysicsJoint Hinge;
    public PhysicsBody Shot { get; private set; }
    public Ammo Loaded { get; private set; }
    private PhysicsJoint _sling = PhysicsJoint.None;
    private readonly Model _stone;

    public Catapult(Model stone)
    {
        _stone = stone;
        Anchor = CreatePhysicsKinematicBox(Pivot, new Vector3(0.2f, 0.2f, 0.2f));
        var rest = Way(RestDegrees);
        Arm = CreatePhysicsBox(Pivot + rest * ArmLength / 2, new Vector3(ArmLength, 0.3f, 0.4f), ArmMass);
        SetPhysicsBodyRotation(Arm, Turn(RestDegrees));
        Hinge = CreatePhysicsHingeJoint(Anchor, Arm, Pivot, Vector3.UnitZ);
        // From rest, where it hangs against the limit, over the top and no further than a little
        // past straight up, the way the swing goes being the negative one about the field's z.
        SetPhysicsHingeLimits(Hinge, -150, 0);
        SetPhysicsHingeMotor(Hinge, 0, Torque);
    }

    /// <summary>Which way the arm points, in degrees about the field's z from +x.</summary>
    public float Angle
    {
        get
        {
            var way = Vector3.Transform(Vector3.UnitX, GetPhysicsBodyRotation(Arm));
            var degrees = MathF.Atan2(way.Y, way.X) * 180 / MathF.PI;
            return degrees < 0 ? degrees + 360 : degrees;
        }
    }

    /// <summary>Whether a shot is held in the cup.</summary>
    public bool IsLoaded => _sling.IsValid && IsPhysicsJointValid(_sling);

    /// <summary>Where the cup holds a shot with the arm at an angle, on its leading side.</summary>
    public static Vector3 Cup(float degrees) =>
        Pivot + Way(degrees) * (ArmLength - 0.4f) + Lead(degrees) * 0.75f;

    /// <summary>The way the cup moves as the arm swings through an angle.</summary>
    public static Vector3 Lead(float degrees)
    {
        var a = degrees * MathF.PI / 180;
        return new Vector3(MathF.Sin(a), -MathF.Cos(a), 0);
    }

    /// <summary>Puts a shot in the cup, the arm at rest, welded there until it is let go.</summary>
    public void Load(Ammo ammo)
    {
        var at = Cup(RestDegrees);
        Shot = ammo == Ammo.Stone
            ? CreatePhysicsConvexHull(_stone, at, 60)
            : CreatePhysicsCapsule(at, 0.3f, 1.0f, 40);
        // Swept over each step, since a shot leaves at near twenty meters a second and a block is
        // a meter wide.
        SetPhysicsBodyContinuous(Shot, true);
        _sling = CreatePhysicsWeldJoint(Arm, Shot);
        Loaded = ammo;
    }

    /// <summary>Takes the shot out of the cup, as another is chosen.</summary>
    public void Unload()
    {
        if (IsLoaded) DestroyPhysicsBody(Shot);
        _sling = PhysicsJoint.None;
    }

    /// <summary>Drives the arm over at a speed in degrees a second.</summary>
    public void Swing(float degreesPerSecond) => SetPhysicsHingeMotor(Hinge, -degreesPerSecond, Torque);

    /// <summary>Lets the shot go, and brakes the arm.</summary>
    public PhysicsBody Release()
    {
        DestroyPhysicsJoint(_sling);
        _sling = PhysicsJoint.None;
        SetPhysicsHingeMotor(Hinge, 0, Torque);
        return Shot;
    }

    /// <summary>Brings the arm back to rest, slowly, and says whether it is there.</summary>
    public bool Return()
    {
        // Within a few degrees, since the limit it rests against is a spring the motor holds it in.
        var home = Angle >= RestDegrees - 3;
        SetPhysicsHingeMotor(Hinge, home ? 0 : 40, Torque);
        return home;
    }

    /// <summary>Draws the frame, the arm and the shot in the cup.</summary>
    public void Draw(Model cube)
    {
        // Two posts either side of the arm and a beam across them under the pivot.
        foreach (var z in new[] { -0.6f, 0.6f })
        {
            cube.Transform = Matrix4x4.CreateScale(0.4f, Pivot.Y, 0.3f) * Matrix4x4.CreateTranslation(Pivot.X, Pivot.Y / 2, z);
            DrawModel(cube, Vector3.Zero, 1, new Color(110, 80, 55));
        }
        cube.Transform = Matrix4x4.CreateScale(3, 0.4f, 1.6f) * Matrix4x4.CreateTranslation(Pivot.X, 0.2f, 0);
        DrawModel(cube, Vector3.Zero, 1, new Color(95, 70, 50));
        cube.Transform = Matrix4x4.CreateScale(ArmLength, 0.3f, 0.4f) * GetPhysicsBodyTransform(Arm);
        DrawModel(cube, Vector3.Zero, 1, new Color(140, 100, 65));
    }

    public static Vector3 Way(float degrees)
    {
        var a = degrees * MathF.PI / 180;
        return new Vector3(MathF.Cos(a), MathF.Sin(a), 0);
    }

    private static Quaternion Turn(float degrees) => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, degrees * MathF.PI / 180);
}
