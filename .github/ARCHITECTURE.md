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
| `PreUpdate` | ImGui's frame starts, finished asset loads land, scenes spawn |
| `FixedUpdate` | zero or more times, once per whole step of `FixedTime` (60 Hz by default), physics steps |
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

## The ECS

`EcsWorld` is a resource. Each component type has a sparse set: an array from entity to dense
index, and dense arrays of entities, components and change bits, so iterating one component is a
walk over a contiguous array and adding or removing is constant time. Entities are `int` ids,
reused from a free list, each with a generation that a despawn bumps. An `Entity` handle
(`ecs.Handle(id)`) carries the generation, so a reference kept across frames can tell, through
`TryResolve` or `IsAlive`, that its entity is gone even when a new one has its id.

- `Query<T1, T2, T3>()` walks the smallest set and looks the others up, yielding copies.
  `QueryRef<T>()` yields references and marks what it visits as changed.
  `BulkProcess<T>` hands a span of the dense array to a delegate.
- `Changed<T>(entity)` reads the change bit, which `Update<T>` and `QueryRef` set and `First` clears.
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
on a static bool, and `[ToggleKey]` lets a key switch it on and off. A method with the wrong
signature, two stage attributes or a `[RunIf]` naming nothing usable is reported on the method
(E3D001 to E3D003) and left out of what is generated. `BehaviorContext` resolves the ECS, commands,
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
and `SceneSpawner` turns it into entities.

## The renderer

`RenderPlugin` builds the `Renderer` and runs it in `Last`. A frame has four steps:

1. **Extract** copies what the frame needs out of the `World` into a `RenderWorld` (cameras, meshes
   and materials, lights, the clear color, the draw list).
2. **Prepare** uploads what changed (vertex buffers, textures, the lighting buffer) and fills
   per-frame buffers from `DynamicBufferAllocator`, a ring of arenas one per frame in flight.
3. **Queue** sorts draw items into `Opaque3dPhase` and `Transparent3dPhase`.
4. **Graph** runs the render graph's nodes in dependency order.

The graph has five nodes. The first draws into render targets, and the rest into one swapchain pass:

| node | draws |
|---|---|
| `targets` | every render target sent drawing this frame: its models, then its shapes, each into its own pass |
| `main_pass` | clears, then the meshes ECS cameras see, through `mesh.slang` |
| `models` | the frame's `ModelDrawList`: every mesh `DrawModel` and `DrawMesh` recorded, through `model.slang` |
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

`RunMode` reads `--serve`, `--headless`, `--hidden` and `--frames` from the command line and the
environment into `Config` when the `App` is made. A headless run has no `AppWindow`.
`HeadlessLoopDriver` paces its frames, the renderer stays uninitialized, and ImGui ends its own
frame.

## Logging

`Log.Category(name)` returns a logger. Messages go to the console and to `logs/Engine.log` beside
the program, unhandled exceptions to `Crash.log` as well, and every line at Info or above to
`ConsoleLog`, the ring `log.tail` reads.
