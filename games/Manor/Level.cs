using System.Numerics;
using Engine;
using static Engine.Engine3D;

/// <summary>
/// The estate: a grid of 8 by 8 cells 16 meters across, each a prefab streamed in as the player
/// nears, round a house of six rooms, with the doors on hinges and the lanterns to find.
/// </summary>
public static class Level
{
    public const float CellSize = 16;
    public const int Cells = 8;

    // The cell a point is in, from 0 to 7 along x and z.
    public static (int I, int J) CellAt(Vector3 at) =>
        ((int)MathF.Floor(at.X / CellSize) + Cells / 2, (int)MathF.Floor(at.Z / CellSize) + Cells / 2);

    public static Vector3 Center(int i, int j) => new((i - Cells / 2 + 0.5f) * CellSize, 0, (j - Cells / 2 + 0.5f) * CellSize);

    // The rooms of the house, by cell, which make-art.py builds by the same numbers.
    public static readonly Dictionary<(int, int), string> Rooms = new()
    {
        [(3, 4)] = "hall", [(4, 4)] = "library", [(3, 3)] = "gallery",
        [(4, 3)] = "dining", [(3, 2)] = "study", [(4, 2)] = "kitchen",
    };

    // The yard's paths, from the gate in the south up to the front door, east to the fountain and
    // north round the house to the garden behind it.
    private static readonly Dictionary<(int, int), string> Paths = new()
    {
        [(3, 7)] = "path-ns", [(3, 6)] = "path-ns", [(3, 5)] = "path-cross", [(4, 5)] = "path-ew",
        [(5, 5)] = "fountain", [(5, 4)] = "path-ns", [(5, 3)] = "path-ns", [(5, 2)] = "path-ns",
        [(5, 1)] = "path-cross", [(4, 1)] = "garden", [(3, 1)] = "garden",
    };

    /// <summary>The prefab a cell places, by its name in resources/cells.</summary>
    public static string PrefabOf(int i, int j)
    {
        if (Rooms.TryGetValue((i, j), out var room)) return "room-" + room;
        if (Paths.TryGetValue((i, j), out var path)) return path;
        if (i == 0 || j == 0 || i == Cells - 1 || j == Cells - 1) return "woods";
        return ((i * 7 + j * 3) % 3) switch { 0 => "trees", 1 => "pines", _ => "meadow" };
    }

    public static IEnumerable<string> Prefabs =>
        Enumerable.Range(0, Cells * Cells).Select(n => PrefabOf(n % Cells, n / Cells)).Distinct();

    public static readonly Vector3 Start = new(-8, 0, 56);

    /// <summary>The doors on hinges: where the hinge is, which way the closed door runs from it, and a name.</summary>
    public static readonly (string Name, Vector3 Hinge, Vector3 Along)[] Doors =
    [
        ("front door", new(-9.2f, 0, 16.1f), Vector3.UnitX),
        ("gallery door", new(-9.2f, 0, 0), Vector3.UnitX),
        ("kitchen door", new(6.8f, 0, -16), Vector3.UnitX),
        ("back door", new(6.8f, 0, -32.1f), Vector3.UnitX),
    ];

    public const float DoorWidth = 2.4f, DoorHeight = 3.0f;

    /// <summary>The lanterns to find, on furniture in four rooms and out in the yard.</summary>
    public static readonly Vector3[] Lanterns =
    [
        new(12, 0.8f, 12),      // the library, on the table by the fire
        new(-8, 1.1f, -8),      // the gallery, on the middle pedestal
        new(8, 0.8f, -22),      // the kitchen table
        new(-8, 0.78f, -28),    // the study's desk
        new(20, 0.6f, 24),      // the fountain's rim
        new(-8, 0.4f, -40),     // the garden behind the house
    ];

    /// <summary>
    /// The way the autopilot walks, from the gate through the front door and every room with a
    /// lantern, out round the house past the fountain to the garden.
    /// </summary>
    public static readonly Vector3[] Route =
    [
        // The gate to the hall, and east into the library to the table by the fire.
        new(-8, 0, 40), new(-8, 0, 22), new(-8, 0, 12), new(-4, 0, 8), new(4, 0, 8), new(11, 0, 11),
        // North past the sofa into the dining room and round its long table to the kitchen door.
        new(7, 0, 11), new(8, 0, 4), new(8, 0, -1), new(5, 0, -3), new(5, 0, -13), new(8, 0, -14), new(8, 0, -18.5f),
        // Beside the kitchen table, west into the study to its desk, and south into the gallery.
        new(6, 0, -21), new(3, 0, -24), new(-3, 0, -24), new(-6.4f, 0, -27.5f), new(-7, 0, -21), new(-8, 0, -18),
        new(-8, 0, -13), new(-8, 0, -9.5f), new(-6.5f, 0, -7), new(-8, 0, -2.5f),
        // Through the hall round its table, out of the front door, and east to the fountain.
        new(-8, 0, 2), new(-5, 0, 4), new(-5, 0, 9), new(-8, 0, 13), new(-8, 0, 22), new(8, 0, 24), new(19, 0, 24),
        // North along the east path round the house, and west into the garden.
        new(24, 0, 18), new(24, 0, -38), new(10, 0, -40), new(-7.5f, 0, -40),
    ];
}

/// <summary>Writes the estate and each cell's prefab as scene files, built by the calls that make them.</summary>
public static class LevelBuilder
{
    public static void Build(string folder)
    {
        SetConfigFlags(ConfigFlags.WindowHidden);
        InitWindow(320, 180, "Manor level");
        var ecs = GetApp().World.Resource<EcsWorld>();
        Directory.CreateDirectory(Path.Combine(folder, "cells"));
        var placed = new List<Entity>();

        Entity Piece(string model, Vector3 at, float yaw = 0, bool solid = true)
        {
            var piece = ecs.Spawn();
            ecs.SetName(piece, model);
            ecs.Add(piece, new ModelRef { Path = $"resources/models/{model}.obj" });
            ecs.Add(piece, new Transform(at, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * MathF.PI / 180), Vector3.One));
            if (solid)
            {
                ecs.Add(piece, Collider.Mesh);
                ecs.Add(piece, RigidBody.Static);
            }
            placed.Add(ecs.Handle(piece));
            return ecs.Handle(piece);
        }
        void Lamp(Vector3 at, Color color, float intensity, float range, bool shadows = false)
        {
            var light = ecs.Spawn();
            ecs.SetName(light, "lamp");
            ecs.Add(light, new Transform(at));
            ecs.Add(light, Light.Default with
            {
                Color = new Vector3(color.R, color.G, color.B) / 255, Intensity = intensity, Range = range, CastsShadows = shadows,
            });
            placed.Add(ecs.Handle(light));
        }
        void Probe(Vector3 at, Vector3 size)
        {
            var probe = ecs.Spawn();
            ecs.SetName(probe, "probe");
            ecs.Add(probe, new Transform(at));
            ecs.Add(probe, new ReflectionProbe(size, 0.8f));
            placed.Add(ecs.Handle(probe));
        }
        void Emitter(string name, Vector3 at, ParticleEmitter emitter)
        {
            var entity = ecs.Spawn();
            ecs.SetName(entity, name);
            ecs.Add(entity, new Transform(at));
            ecs.Add(entity, emitter);
            placed.Add(ecs.Handle(entity));
        }
        void Save(string name)
        {
            SaveScene(Path.Combine(folder, "cells", name + ".json"), placed);
            foreach (var entity in placed) ecs.Despawn(entity.Index);
            placed.Clear();
        }

        // -- The estate, always there: the ground, the wall round it and the house from outside.
        Piece("ground", Vector3.Zero);
        Piece("boundary", Vector3.Zero);
        Piece("shell", Vector3.Zero);
        SaveScene(Path.Combine(folder, "estate.json"), placed);
        foreach (var entity in placed) ecs.Despawn(entity.Index);
        placed.Clear();

        // -- The rooms, each with its own walls, a lamp, a probe and its furniture.
        var room = new Vector3(15.4f, 4.6f, 15.4f);
        var warm = new Color(255, 214, 160);

        Piece("room-hall", Vector3.Zero);
        Piece("rug", Vector3.Zero, solid: false);
        Piece("table", new(0, 0, -3));
        Piece("bench", new(-6.5f, 0, -3), 90);
        Piece("bench", new(-6.5f, 0, 3), 90);
        Piece("painting", new(-5, 2.3f, -7.75f), solid: false);
        Piece("painting", new(5, 2.3f, -7.75f), solid: false);
        Lamp(new(0, 3.4f, 0), warm, 8, 16, shadows: true);
        Probe(new(0, 2.3f, 0), room);
        Save("room-hall");

        Piece("room-library", Vector3.Zero);
        Piece("fireplace", new(7.3f, 0, 0), -90);
        Piece("bookcase", new(-4.5f, 0, -7.6f));
        Piece("bookcase", new(4.5f, 0, -7.6f));
        Piece("bookcase", new(-7.6f, 0, 4.5f), 90);
        Piece("sofa", new(2, 0, 0), -90);
        Piece("rug", new(4, 0, 0), 90, solid: false);
        Piece("table", new(4, 0, 4));
        Lamp(new(0, 3.4f, 0), warm, 5, 14);
        Lamp(new(6.4f, 0.8f, 0), new Color(255, 140, 60), 6, 9);
        Emitter("fire", new(7.0f, 0.15f, 0), ParticleEmitter.Default with
        {
            MaxParticles = 256, Rate = 70, Life = 0.7f, LifeVariation = 0.4f, Velocity = new(0, 1.2f, 0), Spread = 20,
            Radius = 0.35f, Gravity = new(0, 0.8f, 0), StartSize = 0.35f, EndSize = 0.05f,
            StartColor = new Color(255, 170, 60), EndColor = new Color(200, 40, 10, 0), Intensity = 3, Blend = ParticleBlend.Additive,
        });
        Probe(new(0, 2.3f, 0), room);
        Save("room-library");

        Piece("room-gallery", Vector3.Zero);
        foreach (var (x, z) in new[] { (-4f, -4f), (4f, -4f), (-4f, 4f), (4f, 4f), (0f, 0f) }) Piece("pedestal", new(x, 0, z));
        Piece("painting", new(-5, 2.3f, -7.75f), solid: false);
        Piece("painting", new(5, 2.3f, -7.75f), solid: false);
        Piece("painting", new(-5, 2.3f, 7.75f), 180, solid: false);
        Piece("painting", new(5, 2.3f, 7.75f), 180, solid: false);
        Piece("painting", new(7.75f, 2.3f, -5), -90, solid: false);
        Piece("painting", new(7.75f, 2.3f, 5), -90, solid: false);
        Lamp(new(0, 3.4f, 0), new Color(240, 236, 255), 7, 16);
        Emitter("dust", new(0, 2.2f, 0), ParticleEmitter.Default with
        {
            MaxParticles = 300, Rate = 40, Life = 7, LifeVariation = 0.5f, Velocity = new(0, 0.05f, 0), Spread = 180, SpeedVariation = 1,
            Radius = 6, Gravity = new(0, -0.01f, 0), StartSize = 0.04f, EndSize = 0.04f,
            StartColor = new Color(255, 250, 230, 200), EndColor = new Color(255, 250, 230, 0), Lit = true, Blend = ParticleBlend.Alpha,
        });
        Probe(new(0, 2.3f, 0), room);
        Save("room-gallery");

        Piece("room-dining", Vector3.Zero);
        Piece("long-table", Vector3.Zero);
        for (var z = -3f; z <= 3; z += 1.5f)
        {
            Piece("chair", new(-1.2f, 0, z), 90);
            Piece("chair", new(1.2f, 0, z), -90);
        }
        Piece("painting", new(-7.75f, 2.3f, 5), 90, solid: false);
        Piece("painting", new(-7.75f, 2.3f, -5), 90, solid: false);
        Lamp(new(0, 3.3f, -2), warm, 4.5f, 12);
        Lamp(new(0, 3.3f, 2), warm, 4.5f, 12);
        Probe(new(0, 2.3f, 0), room);
        Save("room-dining");

        Piece("room-study", Vector3.Zero);
        Piece("bookcase", new(0, 0, -7.6f));
        Piece("desk", new(0, 0, -4));
        Piece("chair", new(0, 0, -3.2f));
        Piece("sofa", new(-5, 0, 3), 90);
        Piece("rug", new(0, 0, 0), solid: false);
        Lamp(new(0, 3.4f, -2), warm, 6, 14);
        Probe(new(0, 2.3f, 0), room);
        Save("room-study");

        Piece("room-kitchen", Vector3.Zero);
        Piece("counter", new(7.4f, 0, -4.5f), -90);
        Piece("counter", new(7.4f, 0, 4.5f), -90);
        Piece("stove", new(-6, 0, -7.3f));
        Piece("table", new(0, 0, 2));
        Lamp(new(0, 3.4f, 0), new Color(255, 236, 210), 7, 16);
        Emitter("steam", new(-6, 1.0f, -7.0f), ParticleEmitter.Default with
        {
            MaxParticles = 128, Rate = 12, Life = 2.5f, LifeVariation = 0.3f, Velocity = new(0, 0.6f, 0), Spread = 15,
            Radius = 0.2f, Gravity = new(0, 0.15f, 0), StartSize = 0.2f, EndSize = 0.9f,
            StartColor = new Color(240, 240, 240, 110), EndColor = new Color(240, 240, 240, 0), Lit = true, Blend = ParticleBlend.Alpha,
        });
        Probe(new(0, 2.3f, 0), room);
        Save("room-kitchen");

        // -- The yard's cells.
        void Lamps(params (float X, float Z)[] at)
        {
            foreach (var (x, z) in at) Piece("lamppost", new(x, 0, z));
        }

        Piece("path-ns", Vector3.Zero, solid: false);
        Lamps((-3, 0), (3, 0));
        Piece("flowerbed", new(-5.5f, 0, -4), 90);
        Piece("flowerbed", new(5.5f, 0, 4), 90);
        Save("path-ns");

        Piece("path-ew", Vector3.Zero, solid: false);
        Lamps((0, -3), (0, 3));
        Piece("flowerbed", new(-4, 0, -5.5f));
        Piece("flowerbed", new(4, 0, 5.5f));
        Save("path-ew");

        Piece("path-cross", Vector3.Zero, solid: false);
        Lamps((-3, -3), (3, 3));
        Save("path-cross");

        Piece("path-cross", Vector3.Zero, solid: false);
        Piece("fountain", Vector3.Zero);
        foreach (var (x, z, yaw) in new[] { (-5.5f, -5.5f, 45f), (5.5f, -5.5f, -45f), (-5.5f, 5.5f, 135f), (5.5f, 5.5f, -135f) })
            Piece("bench", new(x, 0, z), yaw);
        Emitter("spray", new(0, 1.8f, 0), ParticleEmitter.Default with
        {
            MaxParticles = 512, Rate = 160, Life = 1.1f, LifeVariation = 0.2f, Velocity = new(0, 3.2f, 0), Spread = 25, SpeedVariation = 0.2f,
            Radius = 0.1f, Gravity = new(0, -9, 0), StartSize = 0.08f, EndSize = 0.04f,
            StartColor = new Color(220, 240, 255, 220), EndColor = new Color(180, 210, 240, 0), Lit = true, Blend = ParticleBlend.Alpha,
        });
        Save("fountain");

        Piece("path-ew", Vector3.Zero, solid: false);
        foreach (var x in new[] { -4.5f, 4.5f })
            foreach (var z in new[] { -4f, 4f }) Piece("hedge", new(x, 0, z));
        Piece("bench", new(0, 0, -3.2f), 180);
        Piece("flowerbed", new(-4.5f, 0, -6.5f));
        Piece("flowerbed", new(4.5f, 0, 6.5f));
        Save("garden");

        Piece("tree", new(-4, 0, -3));
        Piece("tree", new(3.5f, 0, 2));
        Piece("tree", new(-1, 0, 5.5f));
        Save("trees");

        Piece("pine", new(-5, 0, -5));
        Piece("pine", new(4, 0, -4));
        Piece("pine", new(-3, 0, 4));
        Piece("pine", new(5, 0, 5));
        Piece("rock", new(0.5f, 0, 0), 30);
        Save("pines");

        Piece("flowerbed", new(-3, 0, -3), 20);
        Piece("flowerbed", new(3, 0, 3), -20);
        Piece("rock", new(4, 0, -4), 70);
        Piece("bench", new(-4, 0, 4), 45);
        Save("meadow");

        foreach (var (x, z, pine) in new[] { (-5f, -5f, false), (0f, -6f, true), (5f, -4f, false), (-6f, 0f, true), (2f, 1f, false), (6f, 3f, true), (-3f, 5f, false), (3f, 6f, true) })
            Piece(pine ? "pine" : "tree", new(x, 0, z));
        Save("woods");

        CloseWindow();
    }
}
