# Behaviors and the ECS

Under the flat API is an entity component system. An entity is a number, a component is a struct
of data an entity has, and a behavior is a struct whose methods run every frame over the entities
that have it. A program can start with the loop alone and move its game into behaviors one piece
at a time, since both run in the same frames.

## A behavior

A `[Behavior]` struct is a component and the code for it at once. Its fields are each entity's
state, and a method marked with a stage runs in that stage of every frame. The `ecs_behaviors`
example makes balls that fall and bounce:

```csharp
/// <summary>A ball that falls and bounces. Its fields are its per-entity state.</summary>
[Behavior]
public struct Ball
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Color Color;
    // ...

    /// <summary>Runs once per entity that has a <see cref="Ball"/>, with the ball by reference.</summary>
    [OnPostUpdate]
    public void Move(BehaviorContext ctx)
    {
        var dt = (float)ctx.Time.DeltaSeconds;
        Velocity.Y -= 9.81f * dt;
        Position += Velocity * dt;
        if (Position.Y < 0.3f)
        {
            Position.Y = 0.3f;
            Velocity.Y = MathF.Abs(Velocity.Y) * 0.8f;
        }
    }
}
```

A method of the instance runs once for each entity that has the behavior, with that entity's
fields as its own, so `Velocity.Y -= ...` changes the ball it runs for. A static method runs once
a frame, as a spawner or a rule over the whole game does. A source generator turns the methods
into the engine's systems as the program compiles, so a behavior needs no registering, and runs in
any program that references the engine.

## Stages

A frame runs its stages in order, and a method's attribute says which it runs in:

| Attribute | Runs |
|---|---|
| `[OnStartup]` | Once, in the first `BeginDrawing`, before the first frame's other stages |
| `[OnFirst]` | At the start of each frame |
| `[OnPreUpdate]` | Before the update |
| `[OnFixedUpdate]` | At a fixed rate, zero or more times a frame, as physics steps |
| `[OnUpdate]` | The game's own logic |
| `[OnPostUpdate]` | After the update, where the frame's spawns are applied |
| `[OnRender]` | Where drawing calls belong, as `DrawCube` from a behavior |
| `[OnLast]` | At the end of each frame |
| `[OnCleanup]` | Once, after the last frame |

In a program with a loop the stages run inside `BeginDrawing`, so the loop and the behaviors take
turns in each frame, and a behavior added between `InitWindow` and the first frame takes part from
the start. `ctx.Time.DeltaSeconds` is the frame's time, as `GetFrameTime()` is, and
`ctx.FixedDelta` the fixed step's.

## Spawning entities

`ctx.Ecs.Spawn()` makes an entity, and `ctx.Ecs.Add` gives it components. The `ecs_mesh_entities`
example spawns a camera, two meshes and two lights in a startup method, which the engine draws
lit with no drawing code:

<!-- compiled with:
public static bool Running = true;
-->
```csharp
[OnStartup]
[RunIf(nameof(Running))]
public static void Start(BehaviorContext ctx)
{
    var camera = ctx.Ecs.Spawn();
    ctx.Ecs.Add(camera, new Camera(fovY: 60f, near: 0.1f, far: 1000f));
    ctx.Ecs.Add(camera, new Transform(new Vector3(0, 1, 5), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.2f), Vector3.One));

    var triangle = ctx.Ecs.Spawn();
    ctx.Ecs.Add(triangle, new Mesh([new Vector3(0, 1, 0), new Vector3(-1, -1, 0), new Vector3(1, -1, 0)]));
    ctx.Ecs.Add(triangle, new Material(new Color(255, 161, 0)) { RoughnessFactor = 0.6f });
    ctx.Ecs.Add(triangle, new Transform(new Vector3(-1.5f, 0, 0)));
    // ...
}
```

`GetMeshComponent` turns a generated or loaded mesh into a `Mesh` component, and one component given
to many entities is one upload they share, as `games/Swarm` makes its shapes once:

<!-- compiled with:
Mesh Ball = default!, Cube = default!;
-->
```csharp
Ball = GetMeshComponent(GenMeshSphere(1, 8, 12));
Cube = GetMeshComponent(GenMeshCube(1, 1, 1));
```

An entity with a `Mesh`, a `Material` and a `Transform` is drawn by every camera entity, or with
no camera entity for the window, through the camera of the frame's first `BeginMode3D`, and one
with a `Light` and a `Transform` lights them. A behavior is added to an entity as any component
is, as `ctx.Ecs.Add(lamp, new Lamp())` gives a light the behavior that moves it.

While systems run, entities are spawned and despawned through `ctx.Cmd`, which holds the changes
and applies them together in the post update stage, so no system of the update sees the world
change under it. The balls are
spawned this way:

<!-- compiled with:
public struct Ball { public Vector3 Position, Velocity; public Color Color; }
BehaviorContext ctx = null!;
-->
```csharp
var ball = new Ball
{
    Position = new Vector3(Random.Shared.NextSingle() * 10 - 5, 4 + Random.Shared.NextSingle() * 6, 0),
    Velocity = new Vector3(Random.Shared.NextSingle() * 2 - 1, 0, 0),
    Color = Color.Orange,
};
ctx.Cmd.Spawn((entity, world) => world.Add(entity, ball));
```

`ctx.Cmd.Despawn(entity)` removes one, as a coin picked up or an enemy defeated is,
`ctx.Cmd.DespawnRecursive(entity)` removes it with everything below it, as a placed prefab, and
`ctx.Cmd.Add` and `ctx.Cmd.Remove` change what an entity has.

## Components a method uses

A method names the entity's other components after its context: `ref` for one it writes and `in`
for one it reads. It then runs only for entities that have them all. The `ecs_mesh_entities`
example turns its cube through its `Transform`:

```csharp
/// <summary>Turns the cube, the one entity carrying this behavior.</summary>
[OnUpdate]
public void Spin(BehaviorContext ctx, ref Transform transform)
{
    var dt = (float)ctx.Time.DeltaSeconds;
    transform.Rotation *= Quaternion.CreateFromYawPitchRoll(0.8f * dt, 0.5f * dt, 0);
}
```

A `ref` marks the component changed for the frame and an `in` does not, which the `[Changed]`
filter below reads, and the scheduler learns from them which systems can run side by side.

## Threads

A method of the instance runs beside the other systems of its stage that take none of the
components it takes, on another thread, and over 4096 entities it splits them between threads. A
static method runs alone on the main thread, since it is a rule over the whole game, which writes
resources, calls ImGui and plays sounds with nothing saying so. A method of the instance that does
those is marked `[MainThread]`, as the player in `games/Swarm` is, since it plays a sound for each
shot and writes where it stands for the creatures to read:

<!-- compiled with:
public enum Screen { Title, Playing, Paused }
-->
```csharp
[OnUpdate]
[MainThread]
[InState(Screen.Playing)]
public void Move(BehaviorContext ctx, ref CharacterController controller, in PhysicsBody body, ref Material material)
{
    // ...
}
```

## Filters and conditions

Attributes narrow which entities a method visits, and whether it runs at all:

| Attribute | The method |
|---|---|
| `[With(typeof(Enemy))]` | Visits only entities that also have these |
| `[Without(typeof(Dead))]` | Skips entities that have any of these |
| `[Changed(typeof(Health))]` | Visits an entity only when these changed since the method last ran |
| `[Added(typeof(Enemy))]` | Visits an entity only when it got these since then |
| `[RunIf(nameof(Running))]` | Runs only while a static `bool` of the behavior is true |
| `[ToggleKey(Key.F3)]` | Is turned on and off by a key, as a debug overlay is |

The examples run in one program, so each startup method carries `[RunIf(nameof(Running))]`, true
only for the example chosen:

<!-- compiled with:
public static class Example { public static string Current = ""; }
-->
```csharp
public static bool Running => Example.Current == "ecs_behaviors";
```

`[InState]`, `[OnEnter]` and `[OnExit]` run a method by the game's state, which the
[States](states.md) page covers.

The entities that lost a component since a method last ran are `ctx.Ecs.Removed<T>()`, kept for a
second. `games/Swarm` ends a wave by them, looking for the last creature only in a frame after one
fell:

<!-- compiled with:
public enum Round { Fighting, Break }
public struct Creature;
public sealed class Arena { public int ToSpawn; }
-->
```csharp
[OnUpdate]
[InState(Round.Fighting)]
public static void Won(BehaviorContext ctx)
{
    if (ctx.Ecs.Removed<Creature>().Count == 0) return;
    if (ctx.Res<Arena>().ToSpawn == 0 && ctx.Ecs.Count<Creature>() == 0 && ctx.Ecs.Count<SceneRef>() == 0)
        ctx.SetState(Round.Break);
}
```

## Scripts changed while the game runs

A behavior in a `.cs` file under `source/behaviors` is compiled by the running game, through Roslyn,
and compiled again within a second of being saved, its systems replacing the last version's between
two frames. The game goes on where it was, since each entity's behavior, and every other component
and resource the script declares, is made again as the new version's type, a field kept keeping its
value, so a counter keeps counting and an entity a behavior keeps is still the one it kept. A field
added starts as the new version leaves it, and a type the new version no longer declares is taken
off its entities. A program run from a project's build folder watches the project's own
`source/behaviors`, so a script is saved where it is written, and one run anywhere else watches the
folder beside it. A script uses the engine and the game's own components and resources.
`games/Swarm` keeps its numbers in one, so a wave is tuned while it is fought:

<!-- compiled with:
public sealed class Tuning { public float PlayerSpeed, EnemySpeed; }
-->
```csharp
[Behavior]
public struct Tune
{
    [OnUpdate]
    public static void Apply(BehaviorContext ctx)
    {
        var tuning = ctx.Res<Tuning>();
        tuning.PlayerSpeed = 6;
        tuning.EnemySpeed = 1;
        // ...
    }
}
```

The project copies the scripts beside the program and leaves them out of its own compile, where
they would be registered a second time:

```xml
<Compile Remove="source\**" />
<Content Include="source\**\*.cs" CopyToOutputDirectory="PreserveNewest" />
```

A native build cannot load code it compiles, so it runs without its scripts, which
[Shipping a game](shipping-a-game.md) says.

## The world from the loop

The program's loop reaches the same world through `GetApp()`, which returns the app `InitWindow`
built. The `ecs_behaviors` example moves its balls in the behavior and draws them in the loop:

<!-- compiled with:
public struct Ball { public Vector3 Position, Velocity; public Color Color; }
Camera3D camera = default;
-->
```csharp
var ecs = GetApp().World.Resource<EcsWorld>();
// ...
// The behavior below moves the balls in Stage.Update, inside BeginDrawing, and the
// loop draws them from the same world.
BeginMode3D(camera);
foreach (var (_, ball) in ecs.Query<Ball>())
    DrawSphereEx(ball.Position, 0.3f, 8, 8, ball.Color);
DrawGrid(20, 1);
EndMode3D();

DrawText($"{ecs.Count<Ball>()} balls. Space adds a hundred.", 10, 10, 20, Color.DarkGray);
```

`Query<T>()` walks every entity with a component, and `Query<T1, T2>()` those with both, each
with its id. An id is the frame's own, and a program keeping an entity across frames keeps the
`Entity` handle `ecs.Handle(id)` gives, which knows when the entity it names is gone.

A behavior reaches the rest of the engine through its context: `ctx.Input` for input,
`ctx.Physics` for the physics world, `ctx.Res<T>()` for any resource of the world, and the flat
functions themselves, which work inside behaviors of the app `InitWindow` built.

## See also

- Examples: [`ecs_behaviors`](../3DEngine.Examples/Ecs/EcsBehaviors.cs),
  [`ecs_mesh_entities`](../3DEngine.Examples/Ecs/EcsMeshEntities.cs),
  [`ecs_physics`](../3DEngine.Examples/Ecs/EcsPhysics.cs),
  [`ecs_animated_models`](../3DEngine.Examples/Ecs/EcsAnimatedModels.cs), and the game
  [`games/Swarm`](../games/Swarm/Program.cs), written in behaviors with a script tuned as it runs
- [DESIGN.md](../.github/DESIGN.md#5-the-ecs-underneath), on why the ECS sits under the flat API
- Previous: [Physics](physics.md)
- Next: [States](states.md)
