using System.Globalization;
using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>A block of the scenes zone's level, which a scene file holds.</summary>
[SceneComponent]
public struct Block
{
    public Color Tint;
    public float Size;
}

/// <summary>A crate's numbers, kept in a data file of their own that the level names by its id.</summary>
[DataAsset]
public struct Crate
{
    public float Mass;
    public Color Tint;
    public string Label;

    public static Crate Default => new() { Mass = 1, Tint = Color.Gray, Label = "" };
}

/// <summary>A stack of crates in the level, whose crate is a data file named by its id.</summary>
[SceneComponent]
public struct Stacked
{
    public DataRef<Crate> Crate;
}

/// <summary>How often the scenes zone was visited, kept beside the program between runs.</summary>
[SceneComponent]
public struct Visits
{
    public int Count;
}

/// <summary>
/// The scenes and saves: a level written as a scene file and started as a saved game, with a prefab
/// a scene reference places, a stack whose crate is a data file named by its id, a block play moves
/// and one play spawns, saved and read back, the level written again and read back apart, and the
/// zone's visits kept in a persistent value between runs.
/// </summary>
/// <remarks>
/// <para>
/// All of it is done as the program starts, in a folder of its own beside the program made afresh
/// each run. The crate's file is written by hand with an id of its own and moved into a folder below
/// before the level is read, so the stack's reference finds it by its id, through the level's own
/// folder, after the rename. The saved game is started from the level, a block moved and a block
/// spawned, saved, and loaded again, which reads the level afresh and lays the save over it, so the
/// moved block and the spawned one are read back from the save.
/// </para>
/// <para>
/// The scenes are ok when the moved block stands where it was moved, the spawned one is there, the
/// crate was read from its new place with its own mass, the level written again reads back with as
/// many entities, and the prefab placed its block.
/// </para>
/// </remarks>
sealed class Scenes : Zone
{
    private readonly EcsWorld _ecs = GetApp().World.Resource<EcsWorld>();
    private readonly Persistent<Visits> _visits = new("feature-visits.json", new Visits(), saveOnChange: true);
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "scene-zone");
    private readonly string _started = "", _crate = "the crate not read", _resaved = "the level not written again";
    private readonly bool _moved, _spawned, _crateOk, _resavedOk;
    private bool _here;

    public Scenes()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        Directory.CreateDirectory(Path.Combine(_folder, "moved"));
        var world = GetApp().World;

        // The crate's file written by hand with an id of its own, and moved before anything reads it.
        var id = Guid.NewGuid().ToString("N")[..12];
        File.WriteAllText(Path.Combine(_folder, "crate.json"),
            $$"""{ "format": "3dengine-data", "version": 1, "id": "{{id}}", "type": "FeatureTest.Crate", "fields": { "Mass": 12, "Tint": [200, 140, 60, 255], "Label": "heavy" } }""");
        File.Move(Path.Combine(_folder, "crate.json"), Path.Combine(_folder, "moved", "heavy-crate.json"));

        string At(Vector3 p) => string.Create(CultureInfo.InvariantCulture, $"[{p.X}, {p.Y}, {p.Z}]");
        File.WriteAllText(Path.Combine(_folder, "prefab.json"), """
            { "format": "3dengine-scene", "version": 1, "entities": [
              { "id": "small", "name": "prefab block", "components": { "Transform": { "Position": [0, 0.3, 0] }, "Block": { "Tint": [90, 200, 120, 255], "Size": 0.6 } } }
            ] }
            """);
        var entities = new List<string>();
        for (var i = 0; i < 6; i++)
            entities.Add($$"""{ "id": "block{{i}}", "name": "block {{i}}", "components": { "Transform": { "Position": {{At(Place(-5 + i * 2, 2, 0.5f))}} }, "Block": { "Tint": [{{60 + i * 30}}, 120, 220, 255], "Size": 1 } } }""");
        entities.Add($$"""{ "id": "stack", "name": "stack", "components": { "Transform": { "Position": {{At(Place(-3, -2, 0.6f))}} }, "Stacked": { "Crate": "{{id}}" } } }""");
        entities.Add($$"""{ "id": "placed", "name": "placed", "components": { "Transform": { "Position": {{At(Place(3, -2))}} }, "SceneRef": { "Path": "scene-zone/prefab.json" } } }""");
        var level = Path.Combine(_folder, "level.json");
        File.WriteAllText(level, $$"""{ "format": "3dengine-scene", "version": 1, "entities": [ {{string.Join(",\n", entities)}} ] }""");

        // Started as a saved game, a block moved and one spawned, saved, and loaded again.
        var started = SaveGame.Start(world, level);
        _started = $"the level's {started.Count} entities started as a saved game";
        var moved = Named("block 0");
        var to = Place(-5, 5, 0.5f);
        if (moved != 0) _ecs.GetRef<Transform>(moved).Position = to;
        var spawned = _ecs.Spawn();
        _ecs.SetName(spawned, "spawned block");
        _ecs.Add(spawned, new Transform(Place(5, 5, 0.5f)));
        _ecs.Add(spawned, new Block { Tint = new Color(230, 80, 80), Size = 1 });
        _ecs.Add(spawned, default(Saved));
        var save = Path.Combine(_folder, "game.save.json");
        SaveGame.Save(world, save);
        var loaded = SaveGame.Load(world, save);
        moved = Named("block 0");
        _moved = moved != 0 && Vector3.Distance(_ecs.GetReadOnly<Transform>(moved).Position, to) < 0.01f;
        _spawned = Named("spawned block") != 0;

        // The stack's crate, found by its id after the rename.
        var stack = Named("stack");
        if (stack != 0)
        {
            var crate = _ecs.GetReadOnly<Stacked>(stack).Crate;
            _crateOk = !crate.IsNone && crate.Path?.EndsWith(Path.Combine("moved", "heavy-crate.json"), StringComparison.Ordinal) == true && crate.Value.Mass == 12;
            _crate = crate.IsNone ? $"the crate read as none, {crate.Problem}"
                : $"the crate's data found by its id at {Path.GetRelativePath(_folder, crate.Path!)} after a rename, {crate.Value.Mass} kilograms";
        }

        // The level as it now stands written again and read back into a world of its own.
        var resaved = Path.Combine(_folder, "resaved.json");
        var kept = loaded.Where(entity => _ecs.Has<Transform>(entity)).ToList();
        SceneFile.Save(_ecs, resaved, kept);
        var apart = new World();
        apart.InsertResource(new EcsWorld());
        var read = SceneFile.Load(apart, resaved);
        _resavedOk = read.Count == kept.Count;
        _resaved = $"the level written again and read back apart as {read.Count} of its {kept.Count} entities";
    }

    public override string Name => "Scenes";
    public override Vector3 Center => Zones.At(292.5f, Zones.Outer);
    public override Vector3 Start => Place(0, -8);

    // The entity of a name, or 0 for none.
    private int Named(string name)
    {
        foreach (var (entity, named) in _ecs.Query<Name>())
            if (named.Value == name) return entity;
        return 0;
    }

    public override void Update(float dt, Player player)
    {
        var here = Zones.Around(player.Feet) == this;
        if (here && !_here) _visits.Update(v => v with { Count = v.Count + 1 });
        _here = here;
    }

    public override void Draw(Camera3D camera)
    {
        // Where each block stands in the world, a prefab's under the entity that placed it.
        foreach (var (entity, block) in _ecs.Query<Block>())
        {
            var at = _ecs.Has<GlobalTransform>(entity) ? _ecs.GetReadOnly<GlobalTransform>(entity).Matrix.Translation
                : _ecs.Has<Transform>(entity) ? _ecs.GetReadOnly<Transform>(entity).Position : (Vector3?)null;
            if (at is { } p) DrawCube(p, block.Size, block.Size, block.Size, block.Tint);
        }
        foreach (var (entity, stacked) in _ecs.Query<Stacked>())
        {
            if (!_ecs.Has<Transform>(entity)) continue;
            var at = _ecs.GetReadOnly<Transform>(entity).Position;
            for (var i = 0; i < 3; i++) DrawCube(at + new Vector3(0, i * 0.8f, 0), 0.75f, 0.75f, 0.75f, stacked.Crate.Value.Tint);
        }
    }

    public override string Status()
    {
        var prefab = 0;
        foreach (var (_, named) in _ecs.Query<Name>())
            if (named.Value == "prefab block") prefab++;
        var ok = _moved && _spawned && _crateOk && _resavedOk && prefab > 0;
        return $"{_started}, the moved block {(_moved ? "read back where it was moved" : "not where it was moved")}, "
               + $"the spawned block {(_spawned ? "read back from the save" : "missing from the save")}, {_crate}, {_resaved}, "
               + $"the prefab placed {prefab} block{(prefab == 1 ? "" : "s")}, visited {_visits.Value.Count} times across runs{(ok ? ", scenes ok" : "")}";
    }
}
