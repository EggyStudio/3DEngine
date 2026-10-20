using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The tester, a capsule on the character controller walked by W A S D relative to a camera that
/// orbits behind it, which sprints, jumps, crouches and flies.
/// </summary>
/// <remarks>
/// The mouse turns the camera while Tab holds the cursor in the window, and the arrow keys always
/// do, so a run with no pointer turns it too. F flies with gravity held off, Space and C rising and
/// sinking, walls and ceilings still stopping it. R puts the player back at the start of the zone it
/// is in, at rest, as a fall below the ground does.
/// </remarks>
sealed class Player
{
    public const float Radius = 0.35f, Tall = 1.8f, Low = 1.0f;
    private const float WalkSpeed = 4, RunSpeed = 7, FlySpeed = 6, JumpSpeed = 5, Behind = 5.5f;

    private float _height = Tall;

    public Player(Vector3 feet, float bearing)
    {
        Body = CreatePhysicsCharacter(feet, Radius, Tall);
        // The course's stairs rise 0.25 a step.
        SetPhysicsCharacterStepHeight(Body, 0.3f);
        Yaw = Heading = bearing;
    }

    public PhysicsBody Body { get; }

    /// <summary>The camera's bearing, which W walks along.</summary>
    public float Yaw { get; private set; }

    /// <summary>How far the camera looks down, in degrees.</summary>
    public float Pitch { get; private set; } = 18;

    /// <summary>The bearing the player last walked along, which its drawing faces.</summary>
    public float Heading { get; private set; }

    public bool Flying { get; private set; }
    public bool Crouching => _height < Tall;
    public bool Grounded => IsPhysicsCharacterGrounded(Body);
    public Vector3 Feet => GetPhysicsBodyPosition(Body) - new Vector3(0, _height / 2, 0);
    public Camera3D Camera { get; private set; }

    /// <summary>Puts the player's feet at a point, at rest, facing a bearing.</summary>
    public void Put(Vector3 feet, float bearing)
    {
        PlacePhysicsBody(Body, feet + new Vector3(0, _height / 2 + 0.02f, 0), Quaternion.Identity);
        Yaw = Heading = bearing;
    }

    public void Fly(bool on)
    {
        Flying = on;
        SetPhysicsCharacterFly(Body, on);
    }

    /// <summary>Reads the keys and the mouse and gives the character the velocity they ask for.</summary>
    public void Update(float dt, Zone? here)
    {
        if (IsKeyPressed(Key.Tab))
        {
            if (IsCursorHidden()) EnableCursor();
            else DisableCursor();
        }
        if (IsCursorHidden())
        {
            var moved = GetMouseDelta();
            Yaw += moved.X * 0.15f;
            Pitch += moved.Y * 0.15f;
        }
        Yaw += (Held(Key.Right) - Held(Key.Left)) * 90 * dt;
        Yaw = (Yaw % 360 + 360) % 360;
        Pitch = Math.Clamp(Pitch + (Held(Key.Down) - Held(Key.Up)) * 45 * dt, -10, 70);

        if (IsKeyPressed(Key.F)) Fly(!Flying);
        var ahead = Zone.Ahead(Yaw);
        var right = new Vector3(-ahead.Z, 0, ahead.X);
        var wish = ahead * (Held(Key.W) - Held(Key.S)) + right * (Held(Key.D) - Held(Key.A));
        var speed = Flying ? FlySpeed : IsKeyDown(Key.LeftShift) ? RunSpeed : WalkSpeed;
        var velocity = wish == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(wish) * speed;
        if (wish != Vector3.Zero) Heading = Zone.Bearing(Vector3.Zero, wish);

        if (Flying) velocity.Y = (Held(Key.Space) - Held(Key.C)) * FlySpeed;
        else
        {
            // A height with no room for it overhead is asked for again each frame until there is.
            var wanted = IsKeyDown(Key.C) ? Low : Tall;
            if (MathF.Abs(wanted - _height) > 0.01f && SetPhysicsCharacterHeight(Body, wanted)) _height = wanted;
            if (IsKeyPressed(Key.Space) && Grounded) JumpPhysicsCharacter(Body, JumpSpeed);
        }
        MovePhysicsCharacter(Body, velocity);

        if (IsKeyPressed(Key.R) || Feet.Y < -20)
        {
            var zone = here ?? Zones.All[0];
            Put(zone.Start, zone.Facing);
        }
    }

    /// <summary>Puts the camera behind the player as the step left it, once the frame has stepped.</summary>
    public void Follow()
    {
        var head = Feet + new Vector3(0, _height - 0.2f, 0);
        var pitch = float.DegreesToRadians(Pitch);
        var back = -Zone.Ahead(Yaw) * MathF.Cos(pitch) + Vector3.UnitY * MathF.Sin(pitch);
        Camera = new Camera3D(head + back * Behind, head, Vector3.UnitY, 60);
    }

    public void Draw()
    {
        var feet = Feet;
        var color = Flying ? new Color(90, 170, 240) : new Color(240, 150, 60);
        DrawCapsule(feet + new Vector3(0, Radius, 0), feet + new Vector3(0, _height - Radius, 0), Radius, 8, 12, color);
        // A nose, so which way it faces shows.
        DrawSphere(feet + new Vector3(0, _height - 0.35f, 0) + Zone.Ahead(Heading) * Radius, 0.09f, new Color(40, 40, 40));
    }

    /// <summary>Where the player stands and how it moves, as the console reads it.</summary>
    public string Line(Zone? here)
    {
        var feet = Feet;
        var moving = Flying ? "flying" : Grounded ? "grounded" : "in the air";
        return $"at {feet.X:0.00} {feet.Y:0.00} {feet.Z:0.00}, {moving}{(Crouching ? ", crouching" : "")}, in {here?.Name ?? "the roads"}, facing {Heading:0}";
    }

    private static int Held(Key key) => IsKeyDown(key) ? 1 : 0;
}
