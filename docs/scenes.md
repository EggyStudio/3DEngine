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

```csharp
// A level made in code the first time: a camera, two lights and three models from a file.
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

`SceneFile` is what the two flat calls use, with a path taken as it is.

## Building what is saved

The level is entities with names and components, made as any entities are:

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

```csharp
var ready = ecs.Query<Collider>().All(c => ecs.Has<PhysicsBody>(c.Entity));
```

A `Joint` component on an entity of its own joins two bodies at its place, along its up. Its `A`
and `B` name the bodies' entities by the `Entity` handle `ecs.Handle(id)` gives, as `SaveScene`
takes the entities it saves. From `games/Summit`, a plank turned about a post by a hinge's motor:

```csharp
ecs.Add(hinge, new Transform(new Vector3(0, 2.2f, -21)));
ecs.Add(hinge, new Joint { Kind = JointKind.Hinge, A = ecs.Handle(post), B = ecs.Handle(board), MotorSpeed = 25, MotorTorque = 20000 });
```

A `ModelRef`, a `SceneRef` and `LoadScene` find a file beside the program, in its `source` folder or
from the working directory, so a level names its models and its prefabs alike, as
`resources/models/house.obj` and `resources/prefabs/house.json`.

## Scenes inside scenes

A `SceneRef` places another scene file under an entity, as a `ModelRef` places a model, so a tree,
a house or an enemy is one file, placed many times in a level. Each copy gets fresh ids, so copies
of one file stay apart. A placed file saved again while the level runs, from another tool or with
`scene.save`, is spawned again in place of every copy within half a second, so a prefab is edited
while the game shows it.

## From outside

The console's `scene.save` and `scene.load` commands save and load the running program's world,
which `./e3d command scene.save level.json` reaches from a terminal, so a level arranged by hand in
a running game is kept.

## See also

- Examples: [`scenes_level`](../3DEngine.Examples/Scenes/ScenesLevel.cs)
- The cheatsheet's [Scenes](../CHEATSHEET.md#scenes)
- [ARCHITECTURE.md](../.github/ARCHITECTURE.md#scene-files), on the format
- Previous: [States](states.md)
- Next: [Driving a program with e3d](driving-with-e3d.md)
