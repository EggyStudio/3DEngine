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

A frame runs its stages in order, and a method's attribute says which it runs in.

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

Within a stage, systems run in the order they were added, and two whose declared reads and writes do
not conflict may run in either order, as a batch run on several threads or a seed's shuffle runs
them. A system that must follow another, as a score counted after the pieces move, asks for it by
the other's name, a behavior's being its type's, its stage's and its method's joined by
underscores:

<!-- compiled with:
App app = null!;
static void Move(World world) { }
static void Score(World world) { }
static void Celebrate(World world) { }
static void Collide(World world) { }
-->
```csharp
app.AddSystem(Stage.Update, new SystemDescriptor(Score, "Score").After("Move").Read<EcsWorld>());
app.AddSystem(Stage.Update, new SystemDescriptor(Move, "Move").Read<EcsWorld>());
app.AddSystem(Stage.Update, new SystemDescriptor(Celebrate, "Celebrate").After("Spin_OnUpdate_Turn"));
app.Chain(Stage.Update, new SystemDescriptor(Collide, "Collide"), new SystemDescriptor(Score, "Score again"));
```

`Before` is the other side of `After`, and `App.Chain` orders each of its systems after the one
before it. The app sorts each stage by what was asked as it starts, keeping the order added wherever
nothing was, and a name the stage does not hold or an order that goes round stops it from starting
with the names. Each asked order holds in the stage's batches and under any seed's shuffle, and a
stage where no system asks is left as it is.

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

A method names the entity's other components after its context, `ref` for one it writes and
`in` for one it reads. It then runs only for entities that have them all. The `ecs_mesh_entities`
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

Attributes narrow which entities a method visits, and whether it runs at all.

| Attribute | The method |
|---|---|
| `[With(typeof(Enemy))]` | Visits only entities that also have these |
| `[Without(typeof(Dead))]` | Skips entities that have any of these |
| `[Changed(typeof(Health))]` | Visits an entity only when these changed since the method last ran |
| `[Added(typeof(Enemy))]` | Visits an entity only when it got these since then |
| `[RunIf(nameof(Running))]` | Runs only while a static `bool` of the behavior is true |
| `[ToggleKey(Key.F4)]` | Is turned on and off by a key, as a debug overlay is |

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

## Observers

A filter finds what changed when its method next runs. An observer runs the moment a component is
added or removed, or an event of the game's own is triggered, before anything else looks at the
world, which suits an index kept in step with a component, or a hit that armor softens before the
body behind it takes it:

<!-- compiled with:
BehaviorContext ctx = null!;
public struct Mine { public int Key; }
public sealed class MineIndex { public void Add(Entity entity, Mine mine) { } public void Forget(Entity entity, Mine mine) { } }
MineIndex index = new();
-->
```csharp
ctx.Ecs.Observe<Add<Mine>>(on => index.Add(on.Event.Entity, on.Event.Value));
ctx.Ecs.Observe<Remove<Mine>>(on => index.Forget(on.Event.Entity, on.Event.Value));
```

`Add<T>` comes as an entity gets a component it did not have, and `Remove<T>` once one has left it,
by `Remove` or with the entity by `Despawn`, with the value it had. Each runs inside the call that
made the change and on its thread, or as the commands are applied for a change queued on
`ctx.Cmd`, with the whole world to work in. A component no observer watches is added and removed as
it would be with none, its store reading one number to know, and a world with no observer at all
despawns as it would without them.

An event is any type a game declares, sent with `Trigger`:

<!-- compiled with:
BehaviorContext ctx = null!;
Entity hand = default, arm = default, body = default;
int health = 100;
-->
```csharp
public readonly record struct Hit(Entity Entity, int Damage) : IPropagatingEvent;

// The arm's armor takes half, and stops a hit left under 4 from reaching the body.
ctx.Ecs.Observe<Hit>(arm, on =>
{
    on.Event = on.Event with { Damage = on.Event.Damage / 2 };
    if (on.Event.Damage < 4) on.Propagate(false);
});
ctx.Ecs.Observe<Hit>(body, on => health -= on.Event.Damage);

ctx.Ecs.Trigger(new Hit(hand, 10));
```

One that names an entity, an `IEntityEvent`, reaches the observers of every such event and then the
entity's own, each in the order it was added. An `IPropagatingEvent` goes on the same way to each
parent in turn, until an observer calls `on.Propagate(false)` or it reaches the root, and since
`on.Event` is a reference, an observer that changes it hands the change to those after it. Any other
event reaches the observers of every such event. An exception an observer throws is logged and goes
no further. The handle `Observe` returns stops the observer when disposed, and an entity's own
observers go with it once it is despawned, after they are told of what it took.

What the mouse does to a mesh entity is an event on it, where the camera drawing the window asks
for it with `Picking`:

<!-- compiled with:
BehaviorContext ctx = null!;
int camera = 0;
Entity door = default, crate = default;
static void Open(EcsWorld ecs, Entity entity) { }
static void Push(EcsWorld ecs, Entity entity, System.Numerics.Vector2 by) { }
-->
```csharp
ctx.Ecs.Add(camera, new Camera(60) { Picking = true });

ctx.Ecs.Observe<Pointer<Click>>(door, on => Open(on.Ecs, on.Entity));
ctx.Ecs.Observe<Pointer<Drag>>(crate, on => Push(on.Ecs, on.Entity, on.Event.Event.Delta));
```

Each frame the pointer's ray is cast at every `Mesh` entity, by the sphere around its positions and
then its triangles, and the nearest it meets is under the pointer. `Over` and `Out` say the pointer
came over the entity or left it, `Move` that it moved on it, `Press`, `Release` and `Click` what a
button did over it, and `DragStart`, `Drag` and `DragEnd` what a button held from it did, wherever
the pointer went. Each carries where the pointer is in the window, and those that happen over the
entity carry a `PointerHit`, the point met, the surface's normal there and its distance. They go up
the entity's parents as the events above do, so a model's root hears a click on any of its meshes,
until an observer calls `on.Propagate(false)`. A skinned mesh is met as it stands unposed, its bind
pose, and not as a clip has posed it on the GPU, so an animated figure is picked where it stands at
rest. The cast costs some 40 nanoseconds a mesh entity a frame, and a world whose window camera does
not ask casts nothing, so a program testing a ray with `GetRayCollisionMesh` itself pays for no
picking. The mouse over an ImGui window picks nothing.

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

A behavior reaches the rest of the engine through its context, `ctx.Input` for input,
`ctx.Physics` for the physics world, `ctx.Res<T>()` for any resource of the world, and the flat
functions themselves, which work inside behaviors of the app `InitWindow` built.

## Testing a behavior

A test builds the app its behaviors run in with no window, `Config.Default` with `Headless` set,
adds `DefaultPlugins` with `AddPlugin`, and runs a frame with each call of `Frame`, reading the
world between them. With `FrameSeconds` set each frame is that long and no clock is read, so the
same frames give the same state on any machine:

```csharp
/// <summary>A dropped item that falls until it lies on the ground.</summary>
[Behavior]
public struct Falling
{
    public float Height;

    [OnUpdate]
    public void Fall(BehaviorContext ctx) => Height = MathF.Max(0, Height - 10 * (float)ctx.Time.DeltaSeconds);
}

using var app = new App(Config.Default with { Headless = true, FrameSeconds = 0.1 }).AddPlugin(new DefaultPlugins());
var ecs = app.World.Resource<EcsWorld>();
var drop = ecs.Spawn();
ecs.Add(drop, new Falling { Height = 2.5f });
for (int frame = 0; frame < 3; frame++) app.Frame();
// Three frames of a tenth of a second have taken it down three units, onto the ground.
var landed = ecs.GetRef<Falling>(drop).Height == 0;
```

The game's behaviors register as their assembly loads, which a test method that names one of
them has done before its first line runs, so they are in the app it builds. Every app built
after that holds them too, as every app the game builds does. The default plugins hold Dear
ImGui's one context for the process, so the tests that build such apps run one at a time, as
xUnit runs a collection whose definition turns its parallel runs off, and each disposes its app
before the next builds one.

## See also

- Examples: [`ecs_behaviors`](../3DEngine.Examples/Ecs/EcsBehaviors.cs),
  [`ecs_mesh_entities`](../3DEngine.Examples/Ecs/EcsMeshEntities.cs),
  [`ecs_physics`](../3DEngine.Examples/Ecs/EcsPhysics.cs),
  [`ecs_animated_models`](../3DEngine.Examples/Ecs/EcsAnimatedModels.cs), and the game
  [`games/Swarm`](../games/Swarm/Program.cs), written in behaviors with a script tuned as it runs
- [DESIGN.md](../.github/DESIGN.md#5-the-ecs-underneath), on why the ECS sits under the flat API
- Previous: [Physics](physics.md)
- Next: [States](states.md)
