# TODO

Work outstanding on 3DEngine, in the order it blocks making a game: a project that builds, a window
and a frame a program drives with plain calls, something on screen, content loaded from files, an
interface, behavior, and shipping the result.

The ECS, the schedule and the behavior generator exist and are tested. What is thin is the surface
a program touches. The flat API described in [DESIGN.md](DESIGN.md) does not exist, and the renderer
draws meshes in white. The renderer's own plan is [RENDERING.md](RENDERING.md), and the editor's is
[EDITOR.md](EDITOR.md).

An item says what exists, what is missing, and what the missing part needs. Finished work is
removed from this file, and an item that is partly done is rewritten around what is left.

## Core

### One project

Every module is a git submodule under `Modules/`, and `3DEngine/3DEngine.csproj` compiles them all
into one assembly. The submodules are to be folded into folders of `3DEngine/`, and the modules for
USD, MaterialX, the embedded browser, the Blazor editor, SteamAudio, networking and the database
removed with the wiring that reaches them (`ScenesPlugin`, `MaterialPlugin`, `DefaultPlugins`,
`Renderer.Initialize`, `VulkanImGuiPlugin`). The engine becomes a library, the generator keeps a
project of its own because Roslyn loads it as an analyzer, and the tests follow the engine's folders.

### Driving the frame from outside

`App.Run` owns the loop and hands it to `IMainLoopDriver`, so a program cannot run one frame and
return. `App` needs `Startup`, `Frame` and `Shutdown`, with `Run` built on them, before
`BeginDrawing` and `EndDrawing` can exist.

### Entities

- **An entity is an `int`.** `EcsWorld` tracks generations but does not put them in the handle, so
  a handle kept past a despawn refers to whatever entity reuses the slot. An `Entity` struct holding
  an index and a generation, checked on every access, fixes it.
- **Queries have no filters.** `Query` and `QueryRef` take up to three and two components, with no
  `With` or `Without`, and `Query` allocates an iterator. Behaviors filter by attribute, so the gap
  is in hand-written systems.
- **Entities have no names or parents**, which the editor's world panel and scene files need.

### Behaviors

- **Two methods on one stage do not compile.** `BehaviorGenerator` names a runner after the stage,
  so a second method on the same stage emits a duplicate member. `RunIf` and `ToggleKey` are read
  from the first method of each stage only.
- **`BehaviorContext` requires physics.** Its constructor resolves `PhysicsWorld`, so an app without
  `PhysicsPlugin` cannot run a behavior.
- **There is no fixed stage.** Physics keeps an accumulator of its own and steps at
  `PhysicsSettings.FixedTimeStep`, but a behavior runs once per frame. A `FixedUpdate` stage run zero
  or more times a frame at `Config.FixedHz`, with `[OnFixedUpdate]`, is needed, and physics then
  steps inside it.
- **States** (`[OnEnter]`, `[OnExit]`, `[InState]`) are not implemented.
- **Diagnostics.** The generator reports nothing when a behavior is not `partial` or a method has
  the wrong signature, and the build fails later in generated code instead.

## Rendering

### Shaders

Shaders are GLSL compiled at runtime through shaderc. They are to be Slang compiled by `slangc`, with
a cache beside the assets so a shipped game needs no compiler (RENDERING.md §1).

### The flat API

`Engine3D` does not exist. The first areas are `Window`, `Input` and `Drawing`, then `Shapes3D` over
the immediate pass (RENDERING.md §2), then `Textures`, `Models`, `Shaders`, `Text`, `Shapes` and
`Audio`, each with its line in a cheatsheet and an example.

### Meshes, materials and light

- **Meshes are positions only** and the fragment shader writes white, so `Material.Albedo`, normals
  and the lighting buffer reach the GPU and change nothing on screen (RENDERING.md §3 and §4).
- **There are no shadows.**

### The device

Every pass is a `VkRenderPass` with framebuffers, every buffer and image has an allocation of its
own, and barriers are synchronization1. Dynamic rendering, synchronization2 and the Vulkan Memory
Allocator replace them (RENDERING.md, What the engine needs).

### Text

There is no font rendering outside ImGui. `DrawText` needs a glyph atlas baked from a TTF (stb
truetype or SDL3_ttf) and quads in the immediate pass.

## Interface

### Dear ImGui

ImGui is drawn by `ImGuiRenderNode` into the main pass. Its frame starts in `PreUpdate`, so ImGui
calls made before that stage in a frame are lost, and there is no docking or viewport support.

### The editor

There is no editor. [EDITOR.md](EDITOR.md) is the plan.

## Simulation

### Physics

`PhysicsWorld` runs BepuPhysics with bodies and colliders from components, steps at a fixed rate
through its own accumulator, and answers `Raycast`. There is no `Engine3D` surface for it, and the
interpolation between steps is not applied to `Transform`, so a body moves in visible steps when the
frame rate is above the physics rate.

### Scenes

`SceneSpawner` spawns a `Scene` of nodes read by an `ISceneReader`, and the readers are USD, which
is to be removed, and the model readers (Assimp and glTF), which read a model rather than a level. A JSON scene format written and read through the generated schemas, with ids that survive
a rename, is needed for the editor and for games.

## Platform

### Input

Keyboard and mouse come from SDL3 into the `Input` resource. Gamepads, text input for fields
outside ImGui and touch are not read.

### Audio

`AudioServer` plays WAV through SDL3. There is no streaming of longer files (OGG, MP3) and no
`Engine3D.Audio` surface.

## Project

### Testing

- **FluentAssertions 8** is licensed per seat for commercial use, so the suite is to move to plain
  xUnit assertions.
- **Nothing renders in a test.** Tests use `NullGraphicsDevice`. A headless or offscreen Vulkan run
  with a screenshot to compare would cover the renderer.

### Build and release

- **No CI.** A workflow that builds and runs the tests on Linux, Windows and macOS is needed.
- **No package.** The engine is consumed as a project reference. A NuGet package carrying the
  shaders and the native SDL3 libraries is needed for a game outside this repository.
- **No command line.** A running app cannot be asked what is in its world or told to take a
  screenshot from a terminal, which makes checking a change by an agent slow. A local socket that
  answers console commands in JSON, with a small client, would do it.
