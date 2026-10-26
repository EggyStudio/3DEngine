# Scenes

A scene file holds entities and their components as JSON, so a level built once, in code or from
a running game, is saved and loaded again. There is no editor to make one with. A program builds a
level by the calls that make it, saves it, and edits the file or its own code from there.

## Saving and loading

`SaveScene` writes the entities of the ECS to a file, and `LoadScene` spawns a file's entities and
returns their handles:

```csharp
SaveScene("level.json");
// ...
var level = LoadScene("level.json");
```

`LoadScene` finds a file as models and sounds are found, beside the program or in the working
directory. `SaveScene` writes every entity a scene did not spawn, or those given, so a program
saves part of its world as a file of its own, as a prefab.

The `scenes_level` example builds a level in code the first time, a camera, two lights and three
models, and saves it, then despawns it and loads it back on a key:

<!-- compiled with:
World world = null!;
EcsWorld ecs = null!;
string file = "", message = "";
void Build(EcsWorld level) { }
int Clear(EcsWorld level) => 0;
-->
```csharp
// A level made in code the first time, a camera, two lights and three models from a file.
Build(ecs);
SceneFile.Save(ecs, file);
// ...
if (IsKeyPressed(Key.S))
{
    SceneFile.Save(ecs, file);
    message = "Saved again.";
}
if (IsKeyPressed(Key.D)) message = $"Despawned {Clear(ecs)} entities. R brings them back.";
if (IsKeyPressed(Key.R))
{
    Clear(ecs);
    message = $"Loaded {SceneFile.Load(world, file).Count} entities from {Path.GetFileName(file)}.";
}
```

The two flat calls use `SceneFile`, with a path taken as it is.

What a file does not hold, a game builds once the file is in. Each component of each entity the
file spawned is told as a `Loaded<T>` once every entity of the file is spawned, every parent set and
every component read, so an observer of a component's builds for its entity what the file leaves
out, finding any other entity of the file, and the file's `SceneLoaded` follows once, with its path
and every entity it spawned, for what is built once for the level:

<!-- compiled with:
EcsWorld ecs = null!;
public struct Door { public Entity Frame; }
public struct Patrol { public string Route; }
static void Hinge(EcsWorld ecs, Entity door, Entity frame) { }
static void Plan(EcsWorld ecs, IReadOnlyList<Entity> level) { }
-->
```csharp
// A door's hinge joins it to its frame, an entity the file holds too, by the id it was saved with.
ecs.Observe<Loaded<Door>>(on => Hinge(on.Ecs, on.Event.Entity, on.Event.Value.Frame));
// The routes the guards walk, planned once from every marker of the level.
ecs.Observe<SceneLoaded>(on => Plan(on.Ecs, on.Event.Entities));

var level = LoadScene("level.json");
```

A save read back is a scene file read, so it is told the same way, and so is each copy a
`SceneRef` places, once the copy is under its entity. `LoadScene` makes the bodies the file's
colliders describe after these, so an observer does not find them yet, and a file read with
`SceneFile` has them the next frame. A component no observer watches costs a load nothing, and a
world with no observer of a load reads one number to know.

## A saved game over its scenes

A game played through its levels saves what play changed rather than the whole world again, so a
save is a few lines and a level fixed in an update reaches a player's save from before it.
`SaveGame.Start` loads the scene files a game begins from and keeps what each of their entities
holds, `SaveGame.Save` writes only what differs, and `SaveGame.Load` loads the same files again and
lays the save over them:

<!-- compiled with:
World world = null!;
EcsWorld ecs = null!;
Vector3 where = default;
-->
```csharp
SaveGame.Start(world, "levels/keep.json", "levels/village.json");
// ...
var arrow = ecs.Spawn();
ecs.Add(arrow, new Transform(where));
ecs.Add(arrow, default(Saved));    // spawned in play, and saved whole
// ...
if (IsKeyPressed(Key.F5)) SaveGame.Save(world, "saves/slot.json");
if (IsKeyPressed(Key.F9)) SaveGame.Load(world, "saves/slot.json");
```

A save names its scenes and holds, for each entity of theirs that play changed, the components it
changed, put on or took off, a component taken off written as `null`, keyed by the scene and the id
the entity has there, which stays the same from one run to the next. An entity of theirs that play
despawned is written as removed, and one the game spawned with `Saved` is written whole, so what the
game spawns on its own each run, its camera or its interface, is not spawned twice when the save is
read. Entities a `SceneRef` or a `ModelRef` placed are left out, as a scene file leaves them.

`Load` despawns the game in progress first, then loads the scenes and lays the save over them, so
what the save does not touch is as the files hold it when it loads, so a level edited after the save
shows each edit on a component play never changed. It tells the save's `Loaded<T>` events and one
`SceneLoaded` naming it once over the whole, after the save is laid. A file that is not a save is
refused before anything is despawned. Each component is compared as a whole, so a component play
changed comes back as play left it, every field of it. A save of one change in a level of 2,000
entities is 291 bytes where the scene written whole is 438,739.

## A value kept between runs

Settings and records a game keeps whatever level is played, a volume, the controls, a best lap, go
in a `Persistent<T>`, a value of a `[SceneComponent]` type kept in a file of its own beside the
program, written as a scene file writes one component:

<!-- compiled with:
[SceneComponent] public struct Settings { public float Volume; public bool Subtitles; public static Settings Default => new() { Volume = 0.8f }; }
-->
```csharp
var settings = new Persistent<Settings>("settings.json", Settings.Default);
SetMasterVolume(settings.Value.Volume);
// ...
settings.Update(s => s with { Volume = 0.5f });
settings.Save();
```

The file is read laid over the default given, so a field the file does not name, as one a later
version of the game added, keeps the default's value. A missing file gives the default, and one that
cannot be read gives it too, with the reason in `Problem` and the log, and is not written over until
the value is saved. `Save` writes on demand, and one made with `saveOnChange: true` writes at each
`Set` as well. `Revert` reads the file again and `Reset` saves the default.

## Data in files of its own

A game's own data, a weapon's numbers, an enemy's kind or a level's rules, which many entities
share and which changes without the game being built again, goes in a struct marked `[DataAsset]`,
kept in a file of its own. A component refers to it through a `DataRef<T>` field by the id the file
was given at its first save, so a file renamed or moved keeps every scene that names it:

<!-- compiled with:
[DataAsset] public struct Weapon { public int Damage; public float Reach; public string Title; public static Weapon Default => new() { Damage = 1, Reach = 1, Title = "Fist" }; }
[SceneComponent] public struct Armed { public DataRef<Weapon> Weapon; }
EcsWorld ecs = null!;
int guard = 0;
-->
```csharp
var sword = DataAssets.Save("weapons/sword.json", new Weapon { Damage = 12, Reach = 2, Title = "Sword" });
ecs.Add(guard, new Armed { Weapon = sword });
// ...
var weapon = ecs.GetReadOnly<Armed>(guard).Weapon;
if (!weapon.IsNone) DrawText($"{weapon.Value.Title} {weapon.Value.Damage}", 10, 10, 20, Color.White);
```

The file holds its format, its id, the type's name and the type's fields as a scene file holds a
component. It is read once and shared by every reference to it, and a save through `DataAssets.Save`
replaces the value for all of them. `DataAssets.Load` gives a reference by the file's name, found as
a model is, and gives a file written by hand an id the first time it reads it, and
`DataAssets.Find` gives one by the id. A scene file writes a reference as the id and finds the file
as it loads, through a table of the ids and where their files are, filled as files are read and
written. An id the table lacks is looked for through the folder beside the program, the scene
file's own folder and the folders data files came from, in the start of each JSON file there, so a
file renamed by hand is found again. A reference whose file is missing reads as none, `IsNone` with the reason naming the id in
`Problem` and the log, and `Value` the type's default. It keeps the id, so the scene saved again
still names the file. A `Persistent<T>` takes a `[DataAsset]` type as well, for a value the player
changes, kept by its file's name, and an `AnimationMachine` names its machine's file by the id
through `File`, beside its `Path`.

## Building what is saved

The level is entities with names and components, made as any entities are:

<!-- compiled with:
EcsWorld ecs = null!;
-->
```csharp
var camera = ecs.Spawn();
ecs.SetName(camera, "Camera");
ecs.Add(camera, Camera.Default);
ecs.Add(camera, new Transform(new Vector3(0, 4, 10), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.35f), Vector3.One));

var sun = ecs.Spawn();
ecs.SetName(sun, "Sun");
ecs.Add(sun, Light.Directional(Vector3.One, 0.9f) with { CastsShadows = true });
ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(0.5f, -0.9f, 0), Vector3.One));
// ...
for (int i = 0; i < 3; i++)
{
    var torus = ecs.Spawn();
    ecs.SetName(torus, $"Torus {i + 1}");
    ecs.Add(torus, new ModelRef { Path = "../resources/torus.obj" });
    ecs.Add(torus, new Transform((i - 1) * 3.5f * Vector3.UnitX, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.4f * i), Vector3.One));
}
```

A `ModelRef` names a model file in place of the meshes and materials in it. The engine loads the
file and spawns its meshes under the entity, which places them with its `Transform`, each mesh with
its own material. A program on the flat API sees them through the camera its `BeginMode3D` uses. The spawned
meshes are left out of the file, since the model brings them back. A file with animation clips, a
character, plays its first clip on a loop instead, through an `AnimatedModel` on a child of the
entity, which is left out of the file the same way. An entity given its own `AnimatedModel` in
place of the `ModelRef` chooses the clip and its speed.

## The file

The file the example writes begins:

```json
{
  "format": "3dengine-scene",
  "version": 1,
  "entities": [
    {
      "id": "f9c0abc2a029",
      "name": "Camera",
      "components": {
        "Camera": {
          "FovY": 1.0471976,
          "Near": 0.1,
          "Far": 1000,
          "TargetName": null
        },
        "Transform": {
          "Position": [0, 4, 10],
          "Rotation": [-0.17410813, -0, -0, 0.98472655],
          "Scale": [1, 1, 1]
        }
      }
    },
```

Each entity has an id, given when it is first saved and kept after, so a reference from one entity
to another holds across saves and renames. Components are written by their type's name, with
their public fields, so the file reads well and merges well in version control, and a value can be
changed in a text editor and loaded again.

## A program's own components

The engine's components and every `[Behavior]` are saved. A component of the program's own is
saved when it is marked `[SceneComponent]`:

```csharp
[SceneComponent]
public struct Crate
{
    public int Coins;
    public bool Opened;
}
```

Its public fields of numbers, strings, enums, vectors, colors, entity references and asset handles
are saved, and arrays of those, and fields of other types are left out. A `Mesh` made in code is
saved with its arrays this way. A source generator writes the code that saves
and reads them as the program compiles, so loading does no reflection and works in a trimmed or
AOT build.

A component or a field renamed keeps its saved levels with `[FormerName]`, which names what it was
called. A file is read from the old name where it has not the new one, and the next save writes
the new one. A type's former name given alone is in its own namespace, and one with a dot is a full
name, for a type moved to another, and the attribute is repeated for a name changed more than once:

```csharp
[SceneComponent, FormerName("Chest")]
public struct Coffer
{
    [FormerName("Gold")] public int Coins;
    public bool Opened;
}
```

A field's own name in a file is read before a former one, and a former name another component has
since taken is read as that component.

A change a name cannot describe, a field split in two or a value counted in other units, is made
by numbering the component's layout with `[DataVersion]` and giving it a static `Migrate`, which
takes the version a file was saved at and the component as the file holds it, and gives it back as
this version holds it:

```csharp
using System.Text.Json.Nodes;

[SceneComponent, DataVersion(2)]
public struct Health
{
    public int Current;
    public int Most;

    public static JsonObject Migrate(int from, JsonObject value)
    {
        if (from < 2) // 1 saved both in one array, "Life": [current, most]
        {
            var life = value["Life"]!.AsArray();
            value.Remove("Life");
            value["Current"] = (int)life[0]!;
            value["Most"] = (int)life[1]!;
        }
        return value;
    }
}
```

The next change raises the number to 3 and adds a check of `from < 3` after this one, so a file
several versions old passes through each change in order. A component past version 1 is saved with its number under
`"$version"`, and one saved with none is version 1, so the first number given reads every level
saved before it. A versioned component with no `Migrate` is warned of as the program compiles
(E3D200), and an editor offers to add one. A level saved by a later build of the game, at a version
past the one running, is read as far as this build knows its fields and saved back at that version
with every value it held, so a player who opens it in an older build loses nothing.

## What is solid

A `Collider` says what shape an entity is solid as, and a `RigidBody` beside it whether it falls
or stays still. Both are saved, and `LoadScene` makes the bodies before it returns, so a level's
walls are solid and its crates fall from the first frame:

```csharp
var ecs = GetApp().World.Resource<EcsWorld>();
var level = LoadScene("level.json");
var crate = level.First(e => ecs.Has<Name>(e) && ecs.GetReadOnly<Name>(e).Value == "Crate 1");
var body = ecs.GetReadOnly<PhysicsBody>(crate);
ApplyPhysicsImpulse(body, new Vector3(0, 5, 0));
```

A collider is a `Collider.Box`, a `Collider.Sphere`, a `Collider.Capsule`, or `Collider.Mesh`, the
triangles of the meshes the entity and those below it show, which never moves. A level's floors,
steps and rooms are each a `ModelRef` with a mesh collider, solid where they are drawn. A mesh
collider is made once its model has spawned, a frame or two after the scene loads, so a game waits
until every collider has its body before it lets the player in, as `games/Summit` does:

<!-- compiled with:
EcsWorld ecs = null!;
-->
```csharp
var ready = ecs.Query<Collider>().All(c => ecs.Has<PhysicsBody>(c.Entity));
```

A `Joint` component on an entity of its own joins two bodies at its place, along its up. Its `A`
and `B` name the bodies' entities by the `Entity` handle `ecs.Handle(id)` gives, as `SaveScene`
takes the entities it saves. From `games/Summit`, a plank turned about a post by a hinge's motor:

<!-- compiled with:
EcsWorld ecs = null!;
int hinge = 0, post = 0, board = 0;
-->
```csharp
ecs.Add(hinge, new Transform(new Vector3(0, 2.2f, -21)));
ecs.Add(hinge, new Joint { Kind = JointKind.Hinge, A = ecs.Handle(post), B = ecs.Handle(board), MotorSpeed = 25, MotorTorque = 20000 });
```

A `ModelRef`, a `SceneRef` and `LoadScene` find a file beside the program, in its `source` folder or
from the working directory, so a level names its models and its prefabs alike, as
`resources/models/house.obj` and `resources/prefabs/house.json`.

## Scenes inside scenes

A `SceneRef` places another scene file under an entity, as a `ModelRef` places a model, so a tree,
a house or an enemy is one file, placed many times in a level. `games/Summit` builds its prefabs,
each one entity naming a model and solid as it is drawn, and places them in its level:

<!-- compiled with:
EcsWorld ecs = null!;
string folder = "";
void Place(string name, string prefab, Vector3 at) { }
-->
```csharp
// A prefab is one entity naming a model, solid as the model is drawn, found beside the
// program as the prefabs themselves are.
foreach (var name in new[] { "island", "block", "steps", "house" })
{
    var piece = ecs.Spawn();
    ecs.SetName(piece, name);
    ecs.Add(piece, new ModelRef { Path = $"resources/models/{name}.obj" });
    ecs.Add(piece, new Transform(Vector3.Zero));
    ecs.Add(piece, Collider.Mesh);
    ecs.Add(piece, RigidBody.Static);
    SaveScene(Path.Combine(folder, "prefabs", name + ".json"), [ecs.Handle(piece)]);
    ecs.Despawn(piece);
}
// ...
Place("Block A", "block", new Vector3(0, 2, -15.5f));
```

`Place` spawns an entity with a `SceneRef` naming the prefab's file and a `Transform` where it goes. Each copy gets fresh ids, so copies
of one file stay apart. A file is read and parsed once for every copy of it, and the copies share its
meshes, one upload drawn as instances, so `games/Swarm` places hundreds of creatures from two
prefabs, each copy taking a small part of a millisecond. A placed file saved again while the level runs, from another tool or with
`scene.save`, is spawned again in place of every copy within half a second, so a prefab is edited
while the game shows it. A model a `ModelRef` placed, its file written again where the asset server
watches its files, is spawned again under the entity that placed it, and the textures the old copy
held are given back once it is gone, so a level edited over and over while it runs does not grow.

## A level streamed in

A level larger than memory should hold is placed a piece at a time, an entity with a
`SceneRef` spawned for each part of the level as the player comes near, and despawned with what is
under it once they are far, so only the near parts are drawn and simulated.

<!-- compiled with:
EcsWorld ecs = null!;
const float CellSize = 16;
int x = 0, z = 0;
-->
```csharp
// The part of the level a cell holds, near the player.
var cell = ecs.Spawn();
ecs.Add(cell, new SceneRef { Path = $"resources/cells/{x}-{z}.json" });
ecs.Add(cell, new Transform(new Vector3(x * CellSize, 0, z * CellSize)));
// ...and once the player is far from it.
ecs.DespawnRecursive(cell);
```

What a level loads through its references is let go once no entity uses it, a model once no
`ModelRef` names it, a texture once no spawned material names it, and a scene file's parsed copy
once no copy of it is left. Each waits `AssetRelease.Grace` seconds first, ten unless set, so a
room left and entered again is not read again. The meshes go from the GPU in the first frame no
entity draws them. What the program loads itself, with `AssetServer.Load` or the flat API's
`Load` functions, stays until the program lets it go, even where a level used the same file. The
`memory` command reports the assets the server knows, which climbs as a level streams in and
settles once what is left behind is let go.

## From outside

The console's `scene.save` and `scene.load` commands save and load the running program's world,
which `./e3d command scene.save level.json` reaches from a terminal, so a level arranged by hand in
a running game is kept.

## See also

- Examples: [`scenes_level`](../3DEngine.Examples/Scenes/ScenesLevel.cs), and the game
  [`games/Summit`](../games/Summit/Program.cs), whose level is prefabs placed in a scene
- The cheatsheet's [Scenes](../CHEATSHEET.md#scenes)
- [ARCHITECTURE.md](../.github/ARCHITECTURE.md#scene-files), on the format
- Previous: [States](states.md)
- Next: [The command line](command-line.md)
