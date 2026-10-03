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
| `First` | time advances, change bits clear, the draw list clears |
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
transition, and `BehaviorConditions.InState(value)` is a run condition. Moves are queued and
applied once a frame, after `PreUpdate`, by `StateTransitions`: the old value's exit systems run,
then the new value's enter systems, in the order they were added, and the commands they queued
apply at once so `Update` sees what they spawned. The first value is entered on the first frame.
A move to the value already held does nothing. Sub-states and computed states are not written.

## The ECS

`EcsWorld` is a resource. Each component type has a sparse set: an array from entity to dense
index, and dense arrays of entities, components and change bits, so iterating one component is a
walk over a contiguous array and adding or removing is constant time. Entities are `int` ids,
reused from a free list, each with a generation that a despawn bumps. An `Entity` handle
(`ecs.Handle(id)`) carries the generation, so a reference kept across frames can tell, through
`TryResolve` or `IsAlive`, that its entity is gone even when a new one has its id.

- `Query<T1, T2, T3>()` walks the smallest set and looks the others up, yielding copies.
  `QueryRef` of one, two or three components yields references and marks what it visits as changed,
  and narrows with `.With<U>()`, `.Without<U>()` and `.Changed<U>()` without allocating.
  `BulkProcess<T>` hands a span of the dense array to a delegate.
- `Changed<T>(entity)` reads the change bit, which `Update<T>` and `QueryRef` set and `First` clears.
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

- a **static** method is one system, called with a `BehaviorContext`;
- an **instance** method runs once per entity that has the struct as a component, with `this` by
  reference, and switches to a parallel loop above 4096 entities.

`[With]`, `[Without]` and `[Changed]` filter the entities, `[RunIf(nameof(member))]` gates a method
on a static bool, `[InState(Screen.Playing)]` gates it on a state, and `[ToggleKey]` lets a key
switch it on and off, and when a method has several of these it runs only when all pass.
`[OnEnter(value)]` and `[OnExit(value)]` take the place of a stage and register the method on a
state transition. A method with the wrong signature, two stage attributes, a `[RunIf]` naming
nothing usable or a state attribute whose argument is not an enum value is reported on the method
(E3D001 to E3D004) and left out of what is generated. `BehaviorContext` resolves the ECS, commands,
time and input when it is made, and `ctx.Physics` only when it is read, so behaviors run without
`PhysicsPlugin`. The generated registrations are
found by `BehaviorsPlugin` when it builds. `RuntimeBehaviorCompiler` watches `source/behaviors`
beside the program, compiles what it finds with Roslyn and the same generator into a collectible
load context, and replaces the previous generation's systems.

## Assets

`AssetServer` loads files on background workers and stores results in `Assets<T>`. `Load<T>(path)`
returns a `Handle<T>` at once, `LoadSync<T>` blocks, and the same path loads once. A loader is an
`IAssetLoader<T>` registered for its extensions. The built-in ones are Slang programs
(`SlangLoader`), textures (StbImageSharp) and models (Assimp), and `SoundsPlugin`, which is not in
`DefaultPlugins` and which `InitAudioDevice` adds, reads WAV and Ogg Vorbis sounds and plays them
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
vectors, colors, entity references by id, asset handles by path) and registers it from a module
initializer, so loading runs no reflection. A component is keyed by its type's name, or by its
full name when two registered types share the name, and a name two of a program's types share
loads neither. A name one of the engine's own types has always means that type, in writing and in
reading, and a program's type of the same name takes its full name, so a file reads the same
whatever the program registers after it was saved. A model is named with a `ModelRef`, which
`ModelRefSystem` spawns under its entity, and the entities a model spawns are not saved, since the
file brings them back. The console's `scene.save` and `scene.load` do the same from `./e3d`.

## The renderer

`RenderPlugin` builds the `Renderer` and runs it in `Last`. A frame has three steps:

1. **Extract** copies what the frame needs out of the `World` into a `RenderWorld` (cameras,
   lights, the clear color, the draw lists).
2. **Prepare** uploads what changed (meshes, textures, the lighting buffer) and fills per-frame
   buffers from `DynamicBufferAllocator`, a ring of arenas one per frame in flight.
3. **Graph** runs the render graph's nodes in dependency order.

Mesh entities reach the renderer the way `DrawModel` does. `MeshEntityDraws`, a system in
`Render`, records every entity with a `Mesh` and a `Material` into the `ModelDrawList` through
the first `Camera` entity, uploading a mesh's arrays to `MeshStore` the first time and copying its
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

`GraphicsDevice` is Vulkan 1.2 over Vortice.Vulkan with classic render passes, three frames in
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
initializer with typed argument parsing. Input commands write into `Input` through
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
