using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// A place on the map that exercises one area of the engine, with a start the player is put at,
/// named spots a run is put at, and a status line saying what it found.
/// </summary>
/// <remarks>
/// A bearing is in degrees from north, which is -Z, round to the east, +X, as a compass reads. A
/// status says what the zone measured or read back, so a run that finds the words it asserts knows
/// the area did its work, and not only that the zone was drawn.
/// </remarks>
abstract class Zone
{
    public abstract string Name { get; }
    public abstract Vector3 Center { get; }

    /// <summary>How far from its middle it runs, across the ground.</summary>
    public virtual float Reach => 16;

    /// <summary>Where the player's feet are put on arriving.</summary>
    public abstract Vector3 Start { get; }

    /// <summary>The bearing the player faces on arriving, toward the middle.</summary>
    public virtual float Facing => Bearing(Start, Center);

    /// <summary>Places a run puts the player at, each with the bearing it faces there.</summary>
    public virtual IReadOnlyDictionary<string, (Vector3 Feet, float Facing)> Spots { get; } =
        new Dictionary<string, (Vector3, float)>();

    /// <summary>Called as the player is put at a spot, or at the start for none, to set what the spot tries afresh.</summary>
    public virtual void Arrive(string? spot) { }

    public virtual void Update(float dt, Player player) { }

    /// <summary>Draws into textures, inside the frame and before the window's own drawing.</summary>
    public virtual void DrawOffscreen() { }

    public abstract void Draw(Camera3D camera);

    /// <summary>Draws over the 3D view, in the window's pixels.</summary>
    public virtual void DrawScreen(Camera3D camera) { }

    public abstract string Status();

    /// <summary>The bearing from one point to another across the ground.</summary>
    public static float Bearing(Vector3 from, Vector3 to)
    {
        var degrees = float.RadiansToDegrees(MathF.Atan2(to.X - from.X, from.Z - to.Z));
        return degrees < 0 ? degrees + 360 : degrees;
    }

    /// <summary>The way a bearing points across the ground.</summary>
    public static Vector3 Ahead(float bearing)
    {
        var radians = float.DegreesToRadians(bearing);
        return new Vector3(MathF.Sin(radians), 0, -MathF.Cos(radians));
    }
}

/// <summary>The zones, the hub first, and which one a point is in.</summary>
static class Zones
{
    /// <summary>How far from the hub's middle the zones' middles are.</summary>
    public const float Ring = 70;

    public static Zone[] All { get; set; } = [];

    /// <summary>The middle of a zone at a bearing from the hub.</summary>
    public static Vector3 At(float bearing) => Zone.Ahead(bearing) * Ring;

    /// <summary>The zone whose middle is nearest a point within its reach, or null on the roads between.</summary>
    public static Zone? Around(Vector3 point)
    {
        Zone? found = null;
        var nearest = float.MaxValue;
        foreach (var zone in All)
        {
            var apart = Vector2.Distance(new Vector2(point.X, point.Z), new Vector2(zone.Center.X, zone.Center.Z));
            if (apart > zone.Reach || apart >= nearest) continue;
            nearest = apart;
            found = zone;
        }
        return found;
    }

    /// <summary>A zone by its name, whatever its case, or null.</summary>
    public static Zone? Named(string name) =>
        All.FirstOrDefault(zone => zone.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The middle of the map, where the program starts: a plaza with a cube turning on a pedestal, a
/// signpost at its edge for each zone with the zone's name over it, and a road out to each.
/// </summary>
sealed class Hub : Zone
{
    private const float Plaza = 10;
    private readonly Model _road = LoadModelFromMesh(GenMeshPlane(1, 1, 1, 1));
    private readonly Model _cube = LoadModelFromMesh(GenMeshCube(1.6f, 1.6f, 1.6f));
    private readonly Model _board = LoadModelFromMesh(GenMeshCube(1.6f, 0.5f, 0.1f));
    private float _turned;
    private int _named;

    public override string Name => "Hub";
    public override Vector3 Center => Vector3.Zero;
    public override float Reach => 14;
    public override Vector3 Start => new(0, 0, 7);
    public override float Facing => 0;

    public override void Update(float dt, Player player) => _turned += 40 * dt;

    // Each zone's road from the plaza's edge to the zone's, and its signpost on the plaza.
    private IEnumerable<(Zone Zone, Vector3 Sign, float Bearing)> Roads() =>
        All().Select(zone => (zone, Ahead(Bearing(Center, zone.Center)) * (Plaza - 1), Bearing(Center, zone.Center)));

    private static IEnumerable<Zone> All() => Zones.All.Where(zone => zone is not Hub);

    public override void Draw(Camera3D camera)
    {
        DrawCylinder(Center, Plaza, Plaza, 0.04f, 48, new Color(186, 180, 168));
        DrawCylinder(Center, 1.4f, 1.6f, 1, 24, new Color(120, 116, 110));
        DrawModelEx(_cube, new Vector3(0, 2.2f, 0), Vector3.Normalize(new Vector3(1, 2, 0.5f)), _turned, Vector3.One, new Color(70, 140, 220));
        foreach (var (zone, sign, bearing) in Roads())
        {
            var length = Vector3.Distance(Center, zone.Center) - Plaza - zone.Reach + 2;
            var middle = Ahead(bearing) * (Plaza + length / 2) + new Vector3(0, 0.02f, 0);
            DrawModelEx(_road, middle, Vector3.UnitY, -bearing, new Vector3(3, 1, length), new Color(150, 140, 120));
            DrawCylinder(sign, 0.08f, 0.08f, 2.4f, 8, new Color(110, 80, 50));
            // Turned with its road, so its face looks back at the plaza's middle.
            DrawModelEx(_board, sign + new Vector3(0, 2.2f, 0), Vector3.UnitY, -bearing, Vector3.One, new Color(230, 220, 190));
        }
    }

    public override void DrawScreen(Camera3D camera)
    {
        _named = 0;
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        foreach (var (zone, sign, _) in Roads())
        {
            var board = sign + new Vector3(0, 2.7f, 0);
            var toward = board - camera.Position;
            if (Vector3.Dot(toward, forward) <= 0 || toward.Length() > 40) continue;
            var at = GetWorldToScreen(board, camera);
            var width = MeasureText(zone.Name, 20);
            DrawText(zone.Name, (int)at.X - width / 2, (int)at.Y - 10, 20, new Color(30, 30, 30));
            _named++;
        }
    }

    public override string Status() =>
        $"the cube turned {_turned % 360:0} degrees, {All().Count()} signposts and roads, {_named} named on the screen";
}
