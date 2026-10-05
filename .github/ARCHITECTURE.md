# Architecture

How a frame runs, from the flat API down to the Vulkan queue. [DESIGN.md](DESIGN.md) says what the
public surface is for, and this document says how the parts under it fit together.

## The layers

```
  Program            using static Engine.Engine3D; InitWindow, BeginDrawing, DrawCube, ImGui.*
     │
  Api/               Engine3D: the flat functions, a DrawList, Camera3D, Color
     │
  Core/  Ecs/        App, plugins, the schedule, World (resources), EcsWorld (entities), events
  Behaviors/         [Behavior] structs, generated into systems at compile time or at runtime
     │
  Platform/  Gui/    SDL3 window, events, input and audio; ImGui context and input
  Assets/  Scenes/   AssetServer, loaders, textures, Assimp models, materials, the scene spawner
  Physics/           BepuPhysics bodies
     │
  Rendering/         Renderer: extract, prepare, the render graph and its nodes
  Graphics/          GraphicsDevice over Vortice.Vulkan, SlangCompiler
```

## The app and its frame

`App` owns a `World` of resources and a `Schedule` of systems. Plugins (`IPlugin.Build(App)`) add
both. `DefaultPlugins` is the set a windowed program uses, sorted by `IPlugin.Order` so the
foundations (logging, the window, assets) build before what depends on them, and a plugin that
names a missing dependency fails with `PluginOrderException`.

A frame is nine stages:

| stage | runs |
|---|---|
| `Startup` | once, before the first frame |
| `First` | time advances, the ECS's frame begins, the draw list clears |
| `PreUpdate` | ImGui's frame starts, finished asset loads land, scenes spawn, then queued state moves apply |
| `FixedUpdate` | zero or more times, once per whole step of `FixedTime` (60 Hz by default), physics steps and sends its contacts as events |
| `Update` | the game |
| `PostUpdate` | deferred ECS commands apply, physics bodies are written back |
| `Render` | ImGui windows that systems draw |
| `Last` | the renderer runs, input's per-frame state clears |
| `Cleanup` | once, after the last frame |

`App.Run()` runs `Startup`, then `Frame()` until the window closes, then `Shutdown()`. The flat API
drives the same app itself. `BeginDrawing` calls `App.BeginFrame()` (`First` to `Update`) and
`EndDrawing` calls `App.EndFrame()` (`PostUpdate` to `Last`), so the program's own drawing and
ImGui calls happen between the game's update and the render.

Dear ImGui has one current context for the whole process, and a program calls it directly, so
one app holds it at a time. Building `SdlImGuiPlugin`, which `DefaultPlugins` does, for a second
app while another that has not shut down holds it throws an `InvalidOperationException` that says
so, and the holder lets go in `Cleanup`, when `App.Shutdown` or `CloseWindow` runs. Tests that
build such apps share the `Engine3D` collection, so they run one after another.

## The schedule

A system is a `SystemFn(World)` in a `SystemDescriptor`, which declares the resources it reads and
writes (`.Read<T>()`, `.Write<T>()`), whether it must run on the main thread, and a run condition.
Within a stage, systems are packed greedily into batches whose declarations do not conflict, and
each batch runs with `Parallel.ForEach`. A system that declares nothing conflicts with everything
and runs alone. `Startup`, `Render` and `Cleanup` run on one thread. Within a stage, systems run
in the order they were added, and there is no before or after ordering.

## Resources and events

`World` maps a type to one instance. `InsertResource`, `InitResource`, `Resource<T>` and
`TryGetResource` cover it, and every disposable resource is disposed with the world.

`Events<T>` is a resource of its own per event type, written with `world.SendEvent` and read with
`world.ReadEvents<T>()`. Asset events are cleared in `Last`.

## States

A state is an enum the game moves between, added with `app.AddState(Screen.Title)`, which inserts
`State<Screen>` (the value it is in) and `NextState<Screen>` (a move waiting to happen).
`app.OnEnter(value, system)` and `app.OnExit(value, system)` register systems that run once on a
transition, `app.OnTransition(from, to, system)` one that runs on a move from one value to a
particular other, and `BehaviorConditions.InState(value)` is a run condition. Moves are queued and
applied once a frame, after `PreUpdate`, by `StateTransitions`: the old value's exit systems run,
then the systems of that particular move, then the new value's enter systems, in the order they
were added, and the commands they queued
apply at once so `Update` sees what they spawned. The first value is entered on the first frame.
A move to the value already held does nothing. Each move is sent as a
`StateTransition<Screen>(From, To)` event, readable until the next frame begins.

`app.AddSubState(Screen.Playing, Pause.Running)` adds a state that exists only while its parent
holds one value. It is created and entered when the parent enters the value, in the same transition
point, and its exit systems run and it goes away when the parent leaves, before the parent's own
exit systems. `app.AddComputedState<InGame, Screen>(compute)` adds a state worked out from another
after each of its moves, with no state where `compute` gives null, and moves it with its own exit
and enter systems only when its value changes. Either may have no `State<T>`, which `InState` reads
as false. Either can be declared instead of added: `[SubStateOf(Screen.Playing)]` on an enum makes
it a sub-state entered at its first member or at `Initial`, and `[ComputedState]` on a static method
from the source enum to the computed one, nullable, makes the method its `compute`. The generated
registration adds them before the behaviors.

`ecs.DespawnOnExit(entity, Screen.Playing)` gives an entity a `DespawnOnExit<Screen>` component, and
when the state leaves that value, by a move or by a sub-state ending with its parent's value, the
entity and every entity below it are despawned, after the value's exit systems, which can still
read them. A game's level or menu goes with the state that made it, with no list of its own to
clear.

## The ECS

`EcsWorld` is a resource. Each component type has a sparse set: an array from entity to dense
index, and dense arrays of entities, components and change ticks, so iterating one component is a
walk over a contiguous array and adding or removing is constant time. Entities are `int` ids,
reused from a free list, each with a generation that a despawn bumps. An `Entity` handle
(`ecs.Handle(id)`) carries the generation, so a reference kept across frames can tell, through
`TryResolve` or `IsAlive`, that its entity is gone even when a new one has its id.

- `Query<T1, T2, T3>()` walks the smallest set and looks the others up, yielding copies through a
  struct enumerator, and takes the same filters as `QueryRef`.
  `QueryRef` of one, two or three components yields references and marks what it visits as changed,
  and narrows with `.With<U>()`, `.Without<U>()`, `.Changed<U>()` and `.Added<U>()` without allocating.
  `BulkProcess<T>` hands a span of the dense array to a delegate.
- Every component operation takes an `int` id or an `Entity` handle. The handle carries a
  generation, so one kept to an entity since despawned is refused rather than reaching the entity
  that reused the id.
- `Changed<T>(entity)` compares the component's change tick, which `Update<T>`, `GetRef<T>` and
  `QueryRef` stamp, with the tick the running system last ran at (`ChangeTicks`). Each system run
  takes the next tick, and a write is stamped with the tick of the system making it, so a system
  sees each change once whether it runs less often than once a frame, as one in `FixedUpdate` at
  a high frame rate, or more often. Code outside a system, as a program's own between
  `BeginDrawing` and `EndDrawing`, sees what changed since the ECS's frame began in `First`.
  `Added<T>(entity)` and the `Added` filter compare the tick the component was added at the same
  way, when its entity did not have one before, whether through `Add` or `Update`. `Removed<T>()`
  lists the entities that lost a `T`, by `Remove` or a despawn, in the same window, from a log
  each store keeps for 60 frames.
  `GetReadOnly<T>` and `QueryReadOnly` of one, two or three components read by reference without
  marking, and physics writes a body's `Transform` only when its pose moved. A behavior method
  marks its component unless it is `readonly`. Transform propagation in `Render` recomputes only
  the chains whose transforms or parents changed since it last ran, which counts writes made after
  it in the frame before.
- `Name` and `Parent` components give entities names and a hierarchy (`SetName`, `SetParent`,
  `ChildrenOf`, `DespawnRecursive`). The parent is a handle. A child's `Transform` is relative to
  its parent, and `TransformPropagation` writes the composed world matrix into its
  `GlobalTransform` in `Render`, which the renderer's extracts read.
- `EcsCommands` queues spawns, despawns, adds and removes as closures, applied in `PostUpdate`, so a
  system can change the world's shape while iterating it.

## Behaviors

A `[Behavior]` struct's methods carry a stage attribute (`[OnStartup]`, `[OnFixedUpdate]`,
`[OnUpdate]`, `[OnRender]` and the rest). `BehaviorGenerator`, a Roslyn incremental generator,
emits a system per method, so a behavior may have several methods on one stage:

- a **static** method is one system, called with a `BehaviorContext`, which runs alone on the main
  thread, since it is a rule over the whole game that writes resources, calls ImGui and plays
  sounds with nothing declaring it;
- an **instance** method runs once per entity that has the struct as a component, with `this` by
  reference, and switches to a parallel loop above 4096 entities, unless it is marked
  `[MainThread]`, which runs it alone on the main thread too. It may take the entity's other
  components after its context, `ref` to write one, which marks it changed and declares a write to
  the scheduler, and `in` or `ref readonly` to read one, which does neither, and then runs only for
  entities that have them all. The generated loop finds each by its dense index, once an entity.

`[With]`, `[Without]`, `[Changed]` and `[Added]` filter the entities, `[RunIf(nameof(member))]`
gates a method on a static bool, `[InState(Screen.Playing)]` gates it on a state, and `[ToggleKey]`
lets a key switch it on and off, and when a method has several of these it runs only when all pass.
`[OnEnter(value)]`, `[OnExit(value)]` and `[OnTransition(from, to)]` take the place of a stage and
register the method on a state transition. A method with the wrong signature, two stage attributes,
a `[RunIf]` naming nothing usable, a state attribute whose argument is not an enum value or a filter
naming a type no entity can have (an interface, a static class, an open generic) is reported on the
method (E3D001 to E3D005) and left out of what is generated, and so is a parameter after the context
that cannot be a component: taken by value or `out`, not a struct, the behavior itself, a type
taken twice, or any on a static method (E3D008). A field holding a reference other than
a string is warned of (E3D006), since every copy of the behavior shares what it points to, and a
state declaration that cannot be registered is reported on the enum or method (E3D007).
`3DEngine.CodeFixes` offers an editor's fixes where the change is clear: a stage method given its
`BehaviorContext` first, keeping the parameters taken by `ref` or `in` (E3D001), one stage kept of several (E3D002), and a command made static (E3D100)
or internal (E3D101). `BehaviorContext` resolves the ECS, commands, time and input when it is made,
and `ctx.Physics` only when it is read, so behaviors run without `PhysicsPlugin`. The generated
registrations are found by `BehaviorsPlugin` when it builds. `RuntimeBehaviorCompiler` watches
`source/behaviors` beside the program, or the project's own when the program runs from a project's
`bin/<configuration>/<framework>`, compiles what it finds with Roslyn and the same generator against
the engine and the program's assembly into a collectible load context, and replaces the previous
generation's systems.

## Assets

`AssetServer` loads files on background workers and stores results in `Assets<T>`. `Load<T>(path)`
returns a `Handle<T>` at once, `LoadSync<T>` blocks, and the same path loads once. A loader is an
`IAssetLoader<T>` registered for its extensions. The built-in ones are Slang programs
(`SlangLoader`), textures (StbImageSharp) and models (Assimp), and `SoundsPlugin`, which is not in
`DefaultPlugins` and which `InitAudioDevice` adds, reads WAV, Ogg Vorbis and MP3 sounds and plays them
through SDL3. Files are read from
`source/` beside the program, and a watched file that changes is loaded again and announced as
`AssetEvent<T>.Modified`.

A model loads as a `SceneAsset`, a tree of nodes with mesh, material, camera and light payloads,
and `SceneSpawner` turns it into entities, under the entity whose `SpawnSceneRequest` asked for it.

## Scene files

A level is a JSON scene file that `SceneFile` writes from the ECS and reads back: per entity its
`SceneId` (given on first save, so a rename keeps references), name, parent, and components. A
component type is saved when it is marked `[SceneComponent]` or is a `[Behavior]`, and
`SceneComponentGenerator` writes the code that saves its public fields (numbers, strings, enums,
vectors, colors, entity references by id, asset handles by path, and arrays of them, as a `Mesh`'s
positions) and registers it from a module initializer, so loading runs no reflection. A component is
keyed by its type's name, or by its full name when two registered types share the name, and a name
two of a program's types share loads neither. A name one of the engine's own types has always means
that type, in writing and in reading, and a program's type of the same name takes its full name, so
a file reads the same whatever the program registers after it was saved. A physics body is described
by a `Collider` and a `RigidBody`, which `PhysicsBodies` turns into a `PhysicsBody` in `PreUpdate`,
and `LoadScene` at once, with the joints `Joint` components describe, each on an entity of its own
whose place is the joint's point and whose up is its axis. A model is named with a `ModelRef`, which
`ModelRefSystem` spawns under its entity, and the entities a model spawns are not saved, since the
file brings them back. Another scene file is placed the same way with a `SceneRef`, a prefab, which
`SceneRefSystem` spawns under its entity in the frame it appears, without the file's ids so copies
of one file stay apart, and down to eight references deep, and spawns again when the file is written
while the level runs, looking at the files twice a second. The console's `scene.save` and
`scene.load` do the same from `./e3d`.

## The renderer

`RenderPlugin` builds the `Renderer` and runs it in `Last`. A frame has three steps:

1. **Extract** copies what the frame needs out of the `World` into a `RenderWorld` (cameras,
   lights, the clear color, the draw lists).
2. **Prepare** uploads what changed (meshes, textures, the lighting buffer) and fills per-frame
   buffers from `DynamicBufferAllocator`, a ring of arenas one per frame in flight.
3. **Graph** runs the render graph's nodes in dependency order.

Mesh entities reach the renderer the way `DrawModel` does. `MeshEntityDraws`, a system in
`Render`, records every entity with a `Mesh` and a `Material` into the `ModelDrawList` through
the first `Camera` entity without a render texture, for the window, and through each with one,
for its texture, uploading a mesh's arrays to `MeshStore` the first time and copying its
base color, normal and metallic-roughness textures from the asset store into `TextureStore` once
loaded.

The graph has five nodes. The first draws into render targets, and the rest into one swapchain pass:

| node | draws |
|---|---|
| `targets` | every render target sent drawing this frame: its models, then its shapes, each into its own pass |
| `main_pass` | begins the window's pass, clearing it |
| `models` | the frame's `ModelDrawList`: every mesh `DrawModel` and `DrawMesh` recorded and every mesh entity, through `model.slang` |
| `immediate` | the frame's `DrawList`: every shape and texture the flat API recorded, through `immediate.slang` |
| `imgui` | Dear ImGui's draw data, through `imgui.slang` |

The draw list batches consecutive shapes with the same topology, transform, depth mode and
texture, so a scene of shapes is a handful of draw calls. Each batch's transform is a push
constant and its texture the descriptor set. `TextureStore` and `MeshStore` queue the textures and
meshes the flat API loads, and two prepare systems (`GpuTexturesPrepare`, `GpuMeshesPrepare`)
upload them before the graph runs, keep their GPU objects for both passes, and destroy an unloaded
one only after the frames in flight that might read it have finished.

`GraphicsDevice` is Vulkan 1.3 over Vortice.Vulkan with dynamic rendering, three frames in
flight and a depth buffer. `NullGraphicsDevice` stands in for it in tests. Shaders are Slang,
compiled per stage by `slangc` and cached (see [RENDERING.md](RENDERING.md) §1).

## The command line

`CliPlugin` (in `DefaultPlugins`, idle unless `Config.Serve` is set) listens on a loopback port with
a random token and writes a session file naming both. The `e3d` client reads session files to find
an app and sends one JSON line per request. The socket thread only queues a request, and the queue
is answered at the top of `First` on the main thread, so a command reads and changes the world
between frames. A command can answer at once, hold its answer until a later frame
(`ConsoleHost.Hold`, as `frames.wait` does), or answer when a poll says it is ready
(`ConsoleHost.Later`, as `shot` does while the capture is written).

Commands are static methods marked `[Command]`, which `CommandGenerator` registers from a module
initializer with typed argument parsing. A command that is not static (E3D100), is neither public
nor internal (E3D101), returns something other than a string or nothing (E3D102) or takes a
parameter the console cannot read (E3D103) is reported on the method. Input commands write into `Input` through
`SyntheticInput`, which also hands mouse events to ImGui, and captures copy the presented swapchain
image into a host buffer (`GraphicsDevice.RequestCapture`) and write it with `PngWriter`.

`RunMode` reads `--serve`, `--headless`, `--offscreen`, `--hidden` and `--frames` from the
command line and the environment into `Config` when the `App` is made. A headless run has no
`AppWindow`. `HeadlessLoopDriver` paces its frames, the renderer stays uninitialized, and ImGui
ends its own frame. An offscreen run is a headless one whose renderer is initialized against an
`OffscreenSurface`: the device makes no surface and no swapchain, draws each frame into one of
its own images in the swapchain's usual format, presents nothing, and captures from it as from a
swapchain image.

## Logging

`Log.Category(name)` returns a logger. Messages go to the console and to `logs/Engine.log` beside
the program, unhandled exceptions to `Crash.log` as well, and every line at Info or above to
`ConsoleLog`, the ring `log.tail` reads.
