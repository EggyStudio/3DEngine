using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The physics, in lanes running east side by side, each a station the character controller is
/// tried on, a ramp of 25 degrees, stairs of 0.25, a gap to jump, a platform moved by the program
/// that carries it, crates it pushes and a tunnel it crouches through, each ending in a trigger.
/// </summary>
/// <remarks>
/// A station is reached when the player's body touches the trigger at its end, which the frame's
/// contacts say, and arriving at a station's spot sets it to be tried afresh, its platform and its
/// crates back where they began. The status names each station reached, how many crates moved and
/// how many of the course's bodies are awake.
/// </remarks>
sealed class Course : Zone
{
    private const float Lane = 58, End = 70;
    private readonly Model _cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
    private readonly List<(Vector3 Center, Vector3 Size, Quaternion Turn, Color Color)> _solid = [];
    private readonly List<Station> _stations = [];
    private readonly List<(PhysicsBody Body, Vector3 Start)> _crates = [];
    private readonly Dictionary<string, (Vector3 Feet, float Facing)> _spots = [];
    private readonly Toggle _moving = new("platform", "The platform moves");
    private readonly Toggle _shown = new("triggers", "The triggers drawn");
    private PhysicsBody _platform;
    private float _ride;

    private sealed class Station(string name, float z, PhysicsBody trigger)
    {
        public string Name { get; } = name;
        public float Z { get; } = z;
        public PhysicsBody Trigger { get; } = trigger;
        public bool Reached;
    }

    public override string Name => "Course";
    public override Vector3 Center => Zones.At(90);
    public override float Reach => 24;
    public override Vector3 Start => new(Center.X - 18, 0, 0);
    public override IReadOnlyDictionary<string, (Vector3 Feet, float Facing)> Spots => _spots;
    public override IReadOnlyList<Toggle> Toggles => [_moving, _shown];

    public Course()
    {
        var stone = new Color(160, 160, 170);
        var wood = new Color(170, 130, 90);

        // A ramp of 25 degrees rising 2 to a landing, its top face starting on the ground at 59.
        var z = -15f;
        var angle = float.DegreesToRadians(25);
        var top = new Vector3(59 + 1 / MathF.Tan(angle), 1, z);
        Solid(top + new Vector3(MathF.Sin(angle), -MathF.Cos(angle), 0) * 0.2f, new Vector3(2 / MathF.Sin(angle) + 0.2f, 0.4f, 3), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle), stone);
        Solid(new Vector3(67.6f, 1, z), new Vector3(8.8f, 2, 3), Quaternion.Identity, stone);
        Add("ramp", z, 2, new Vector3(Lane - 1, 0, z));

        // Eight steps of 0.25 up to a landing at 2.
        z = -9;
        for (var i = 0; i < 8; i++)
            Solid(new Vector3(59.225f + 0.45f * i, 0.125f * (i + 1), z), new Vector3(0.45f, 0.25f * (i + 1), 3), Quaternion.Identity, wood);
        Solid(new Vector3(67.3f, 1, z), new Vector3(9.4f, 2, 3), Quaternion.Identity, wood);
        Add("stairs", z, 2, new Vector3(Lane - 1, 0, z));

        // Two blocks a meter high with a gap of 1.6 between, jumped from a run.
        z = -3;
        Solid(new Vector3(58.5f, 0.5f, z), new Vector3(7, 1, 3), Quaternion.Identity, stone);
        Solid(new Vector3(67.8f, 0.5f, z), new Vector3(8.4f, 1, 3), Quaternion.Identity, stone);
        Add("gap", z, 1, new Vector3(56, 1, z));

        // A platform the program moves between two blocks, which carries what stands on it.
        z = 3;
        Solid(new Vector3(56, 0.5f, z), new Vector3(4, 1, 3), Quaternion.Identity, wood);
        Solid(new Vector3(69, 0.5f, z), new Vector3(6, 1, 3), Quaternion.Identity, wood);
        _platform = CreatePhysicsKinematicBox(new Vector3(Near, 0.8f, z), new Vector3(3, 0.4f, 3));
        Add("platform", z, 1, new Vector3(Near, 1, z));

        // Four crates of six kilograms in the way, pushed along by walking into them.
        z = 9;
        foreach (var (x, side) in new[] { (62f, -0.45f), (62f, 0.45f), (63f, -0.45f), (63f, 0.45f) })
        {
            var at = new Vector3(x, 0.4f, z + side);
            _crates.Add((CreatePhysicsBox(at, new Vector3(0.8f), mass: 6), at));
        }
        Add("crates", z, 0, new Vector3(Lane - 1, 0, z));

        // A tunnel whose roof is 1.3 up, which a player 1.8 tall walks into and one crouched passes.
        z = 15;
        Solid(new Vector3(64, 1.5f, z), new Vector3(6, 0.4f, 3), Quaternion.Identity, stone);
        Solid(new Vector3(64, 0.85f, z - 1.65f), new Vector3(6, 1.7f, 0.3f), Quaternion.Identity, stone);
        Solid(new Vector3(64, 0.85f, z + 1.65f), new Vector3(6, 1.7f, 0.3f), Quaternion.Identity, stone);
        Add("tunnel", z, 0, new Vector3(Lane - 1, 0, z));
    }

    // The platform's ends, where it waits two seconds, and its speed between them.
    private const float Near = 59.5f, Far = 64.5f, Speed = 2, Wait = 2;
    private const float Cycle = 2 * Wait + 2 * (Far - Near) / Speed;

    private void Solid(Vector3 center, Vector3 size, Quaternion turn, Color color)
    {
        var body = CreatePhysicsStaticBox(center, size);
        if (turn != Quaternion.Identity) SetPhysicsBodyRotation(body, turn);
        _solid.Add((center, size, turn, color));
    }

    // A station, its trigger at the lane's end standing on the floor at a height, and its spot.
    private void Add(string name, float z, float floor, Vector3 spot)
    {
        _stations.Add(new Station(name, z, CreatePhysicsTrigger(new Vector3(End, floor + 0.9f, z), new Vector3(1.5f, 1.8f, 3))));
        _spots[name] = (spot, 90);
    }

    public override void Arrive(string? spot)
    {
        foreach (var station in _stations)
            if (spot is null || station.Name == spot) station.Reached = false;
        if (spot is null or "platform")
        {
            _ride = 0;
            PlacePhysicsBody(_platform, new Vector3(Near, 0.8f, 3), Quaternion.Identity);
        }
        if (spot is null or "crates")
            foreach (var (body, start) in _crates) PlacePhysicsBody(body, start, Quaternion.Identity);
    }

    public override void Update(float dt, Player player)
    {
        // The platform waits at its near end, crosses, waits at its far end and comes back, its
        // velocity set so what stands on it rides along, and put at each end exactly as it stops.
        var before = _ride % Cycle;
        if (_moving.On) _ride += dt;
        var now = _ride % Cycle;
        var going = (Far - Near) / Speed;
        float velocity = !_moving.On ? 0 : now switch
        {
            _ when now < Wait => 0,
            _ when now < Wait + going => Speed,
            _ when now < 2 * Wait + going => 0,
            _ => -Speed,
        };
        if (velocity == 0 && (before < Wait + going && now >= Wait + going || before > now))
            SetPhysicsBodyPosition(_platform, new Vector3(now < Wait ? Near : Far, 0.8f, 3));
        SetPhysicsBodyVelocity(_platform, new Vector3(velocity, 0, 0));

        foreach (var contact in GetPhysicsContacts())
            foreach (var station in _stations)
                if ((contact.BodyA == station.Trigger && contact.BodyB == player.Body) || (contact.BodyB == station.Trigger && contact.BodyA == player.Body))
                    station.Reached = true;
    }

    public override void Draw(Camera3D camera)
    {
        foreach (var (center, size, turn, color) in _solid) Box(Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation(center), size, color);
        Box(GetPhysicsBodyTransform(_platform), new Vector3(3, 0.4f, 3), new Color(90, 150, 210));
        foreach (var (body, _) in _crates) Box(GetPhysicsBodyTransform(body), new Vector3(0.8f), new Color(200, 150, 70));
        foreach (var station in _stations)
        {
            if (!_shown.On) break;
            var at = GetPhysicsBodyPosition(station.Trigger);
            DrawCubeWires(at, 1.5f, 1.8f, 3, station.Reached ? new Color(60, 200, 90) : new Color(220, 220, 220));
        }
    }

    public override void DrawScreen(Camera3D camera)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        foreach (var station in _stations)
        {
            var label = new Vector3(Lane - 2, 2.6f, station.Z);
            var toward = label - camera.Position;
            if (Vector3.Dot(toward, forward) <= 0 || toward.Length() > 30) continue;
            var at = GetWorldToScreen(label, camera);
            DrawText(station.Name, (int)at.X - MeasureText(station.Name, 20) / 2, (int)at.Y, 20, station.Reached ? new Color(30, 130, 50) : new Color(30, 30, 30));
        }
    }

    private void Box(Matrix4x4 pose, Vector3 size, Color color)
    {
        _cube.Transform = Matrix4x4.CreateScale(size) * pose;
        DrawModel(_cube, Vector3.Zero, 1, color);
    }

    public override string Status()
    {
        var moved = _crates.Count(crate => Vector3.Distance(GetPhysicsBodyPosition(crate.Body), crate.Start) > 0.5f);
        var awake = _crates.Count(crate => crate.Body.IsAwake) + (_platform.IsAwake ? 1 : 0);
        var stations = string.Join(", ", _stations.Select(s => $"{s.Name} {(s.Reached ? "reached" : "not reached")}"));
        return $"{stations}, {moved} of {_crates.Count} crates moved, {_solid.Count + _crates.Count + _stations.Count + 1} bodies, {awake} awake"
               + (_moving.On ? "" : ", the platform held still");
    }
}
