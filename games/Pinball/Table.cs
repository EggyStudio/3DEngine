using System.Numerics;
using Engine;
using static Engine.E3D;

/// <summary>
/// The table's layout, lying flat in its own frame, x across and z toward the player, the tilt
/// given by gravity rather than by turning the table, so every wall is a box turned about the
/// vertical alone. A unit of it is about 8.5 centimeters of a real table.
/// </summary>
public static class Table
{
    // A real table tilts 6.5 degrees, and gravity is scaled with the table, a real ball's 9.81
    // meters a second squared being 115 units here, so a ball runs at a real table's pace.
    public const float Tilt = 6.5f * MathF.PI / 180, Gravity = 115;
    public static readonly Vector3 Down = new(0, -Gravity * MathF.Cos(Tilt), Gravity * MathF.Sin(Tilt));

    public const float BallRadius = 0.16f, BallMass = 0.08f, WallHeight = 0.5f;

    // The playfield runs from -2.7 to 2.7 across, the plunger's lane beside it on the right.
    public const float Edge = 2.7f, LaneX = 3.025f, LaneOuter = 3.3f;
    public static readonly Vector3 BallStart = new(LaneX, BallRadius, 5.3f);

    public static readonly Vector2[] Bumpers = [new(-0.8f, -3.0f), new(0.8f, -3.0f), new(0, -1.7f)];
    public const float BumperRadius = 0.38f;

    // Each flipper turns about its pivot, lying 30 degrees below the across at rest and 25 above
    // it when flipped, its tips 0.57 apart at rest, so a ball between them drains.
    public static readonly Vector2 LeftPivot = new(-1.15f, 4.6f), RightPivot = new(1.15f, 4.6f);
    public const float FlipperLength = 1.0f, FlipperWidth = 0.22f, RestDegrees = 30, UpDegrees = 55;

    /// <summary>A wall of the table, as the physics holds it and as it is drawn.</summary>
    public readonly record struct Wall(Vector3 Center, Vector3 Size, Quaternion Turn, Color Color, PhysicsBody Body, Kind Kind);

    public enum Kind { Rail, Sling, Guide }

    /// <summary>
    /// A static box from a to b across the table, its long side along the segment, turned about
    /// the vertical, as thick as given and as high as the walls.
    /// </summary>
    public static Wall Build(Vector2 a, Vector2 b, float thickness, Color color, Kind kind = Kind.Rail)
    {
        var along = b - a;
        // A turn about +y by an angle carries +x to (cos, 0, -sin), so the angle is that of the
        // segment with its z negated.
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(-along.Y, along.X));
        var middle = (a + b) / 2;
        var center = new Vector3(middle.X, WallHeight / 2, middle.Y);
        var size = new Vector3(along.Length() + thickness, WallHeight, thickness);
        var body = CreatePhysicsStaticBox(center, size);
        SetPhysicsBodyRotation(body, turn);
        SetPhysicsBodyMaterial(body, 0.05f, kind == Kind.Sling ? 0.8f : 0.45f);
        return new Wall(center, size, turn, color, body, kind);
    }

    /// <summary>
    /// Every wall of the table, the rails round it, the lane, the corners, the slings and the
    /// guides.
    /// </summary>
    public static List<Wall> Walls()
    {
        var rail = new Color(200, 205, 215);
        var sling = new Color(240, 90, 70);
        var guide = new Color(150, 160, 180);
        return
        [
            // The rails round the table, the lane's inner wall, and the bottom behind the drain.
            Build(new(-Edge - 0.05f, -6), new(-Edge - 0.05f, 6.9f), 0.1f, rail),
            Build(new(LaneOuter, -6), new(LaneOuter, 6.9f), 0.1f, rail),
            Build(new(-Edge - 0.1f, -6.05f), new(LaneOuter + 0.05f, -6.05f), 0.1f, rail),
            Build(new(Edge + 0.05f, -3.8f), new(Edge + 0.05f, 6.9f), 0.1f, rail),
            Build(new(-Edge, 6.9f), new(Edge, 6.9f), 0.1f, rail),
            // The top corners, the right one turning a ball out of the lane and across the top.
            Build(new(-Edge, -4.8f), new(-1.4f, -6.0f), 0.1f, rail),
            Build(new(LaneOuter, -4.6f), new(1.6f, -6.0f), 0.1f, rail),
            // The slings above the flippers, which kick a ball back in, and the guides into the
            // flippers.
            Build(new(-2.0f, 2.2f), new(-1.45f, 3.55f), 0.12f, sling, Kind.Sling),
            Build(new(2.0f, 2.2f), new(1.45f, 3.55f), 0.12f, sling, Kind.Sling),
            Build(new(-Edge, 3.2f), new(-1.32f, 4.42f), 0.1f, guide, Kind.Guide),
            Build(new(Edge, 3.2f), new(1.32f, 4.42f), 0.1f, guide, Kind.Guide),
        ];
    }

    /// <summary>
    /// The way along a flipper from its pivot, a degrees below the across, mirrored for the right
    /// one.
    /// </summary>
    public static Vector3 Along(bool left, float degreesBelow)
    {
        var a = degreesBelow * MathF.PI / 180;
        return new Vector3(left ? MathF.Cos(a) : -MathF.Cos(a), 0, MathF.Sin(a));
    }
}
