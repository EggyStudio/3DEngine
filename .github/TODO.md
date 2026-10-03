# TODO

Work outstanding on 3DEngine, in the order it blocks making a game: a window
and a frame a program drives with plain calls, something on screen, content loaded from files, an
interface, behavior, and shipping the result.

The ECS, the schedule and the behavior generator exist and are tested, and every area of the flat
API described in [DESIGN.md](DESIGN.md) has a first version. What is thin is depth: lighting is one
fixed light, the ECS's own mesh path is unlit, and each area of the flat API lacks pieces raylib
has. The renderer's own plan is [RENDERING.md](RENDERING.md).

An item says what exists, what is missing, and what the missing part needs. Finished work is
removed from this file, and an item that is partly done is rewritten around what is left.

## Core

### Entities

- **Stale ids are caught only through handles.** `EcsWorld`'s operations take an `int` id, which a
  later spawn reuses. `Entity` (from `ecs.Handle(id)` or `ctx.Entity`) carries the generation, and
  `TryResolve` and `IsAlive` refuse a stale one, but nothing stops code from keeping the bare `int`
  across frames instead. Components holding entity references have no type that does this for them.
- **Only single-component queries filter.** `QueryRef<T>()` narrows with `With`, `Without` and
  `Changed` (up to four of each) and allocates nothing. `QueryRef<T1, T2>` has no filters, and
  `Query<...>` yields copies through an allocating iterator.
- **Transform propagation walks every parented entity each frame**, with a dictionary, in
  `Stage.Render`. It has no change detection, so a large static hierarchy costs its size every
  frame, and physics writes a body's `Transform` as if it had no parent.

### Behaviors

- **States are plain.** `App.AddState`, `[OnEnter]`, `[OnExit]` and `[InState]` work, with any
  number of independent enums. Bevy's sub-states (a pause that exists only while playing) and
  computed states (a value worked out from another state) are not written, and a transition
  cannot be observed as an event.
- **Diagnostics stop at the method.** The generator reports a wrong signature, two stage
  attributes, a bad `[RunIf]` and a state attribute without an enum value (E3D001 to E3D004). A
  filter naming a type that is not a component, and a behavior whose fields hold references, are
  not reported.

## Rendering

### The flat API

`Engine3D` covers the window, timing, input, the frame, cameras, render targets, 2D and 3D shapes,
images and textures, models and meshes, shaders, text and fonts, and audio
([CHEATSHEET.md](CHEATSHEET.md)). What is missing:

- **Custom shaders are for the immediate pass only.** A shader loaded with `LoadShader` replaces
  the stages of shapes, textures and text, and reads four `float4` slots (`SetShaderValue`) rather
  than parameters by name. Models cannot take one, and a shader cannot bind textures of its own
  beyond the one it draws.
- **Music is not streamed.** `LoadMusicStream` decodes the whole file, so `UpdateMusicStream` does
  nothing and a long piece costs its length in memory. Sounds have no pan, and the time a piece has
  played is not reported (`GetMusicTimePlayed`). MP3 and FLAC are not read.
- **Models are partial.** Only the base color and its texture are used of a material, animation
  is not played, and models are lit by one fixed light. A mesh cannot be changed after upload,
  and raylib's `GenMeshHeightmap` and `GenMeshCubicmap`, which build terrain from an image, are
  not written.
- **Textures have no mipmaps**, so a texture drawn much smaller than its size shimmers. Images
  are edited on the CPU (resize, flip, colors, shapes, `ImageDraw`), but text cannot be drawn
  into an image (`ImageDrawText`), and Perlin and cellular noise are not generated.
- **Fonts bake Latin-1 only**, at one size each, with no signed distance fields, so text far
  larger than its bake blurs. Characters outside Latin-1 are skipped.
- **Monitors** have no functions, and render targets have no multisampling and no depth to sample.

### Meshes, materials and light

- **Meshes are lit by one fixed light.** Mesh entities and models share the model pass, with
  normals and base color textures, but the lights the ECS holds are uploaded and not read, and of
  a material only the base color is used (RENDERING.md §3 and §4). A mesh entity is drawn through
  the first camera entity only, into the window only.
- **Shader reflection, compute and a shipped shader cache** are not built (RENDERING.md §1).
- **There are no shadows.**

### The device

Every pass is a `VkRenderPass` with framebuffers, every buffer and image has an allocation of its
own, and barriers are synchronization1. Dynamic rendering, synchronization2 and the Vulkan Memory
Allocator replace them (RENDERING.md, What the engine needs).

## Interface

### Dear ImGui

ImGui is drawn by `ImGuiRenderNode` into the main pass. Its frame starts in `PreUpdate`, so ImGui
calls a system makes in `First` are lost, and there is no docking or viewport support. Keyboard
navigation is on, which makes `WantCaptureKeyboard` true whenever an ImGui window has focus, so
the engine's own shortcuts ask `WantTextInput` instead.

## Simulation

### Physics

`PhysicsWorld` runs BepuPhysics with bodies and colliders from components, steps once per
`Stage.FixedUpdate` run on `FixedTime`'s step (the same steps `[OnFixedUpdate]` behaviors run on),
and answers `Raycast`. There is no `Engine3D` surface for it, and the interpolation between steps
(`FixedTime.Alpha`) is not applied to `Transform`, so a body moves in visible steps when the frame
rate is above the fixed rate.

### Scenes

`SceneSpawner` spawns a `Scene` of nodes read by an `ISceneReader`, and the only reader is Assimp,
which reads a model rather than a level. A JSON scene format written and read through the generated schemas, with ids that survive
a rename, is needed for levels a game loads.

The payload types (`SceneLightPayload`, `SceneMaterialPayload`, `Light`) are modeled on UsdLux and
`UsdPreviewSurface`, with dome, portal, cylinder and plugin lights and prim paths the renderer never
reads. They are to shrink to the directional, point and spot lights and the metallic-roughness
material that RENDERING.md §3 and §4 describe.

## Platform

### Input

Keyboard, mouse and gamepads come from SDL3 into the `Input` resource. Text input for fields
outside ImGui and touch are not read, and a gamepad's sensors (gyro, touchpad) and lights are not
reached.

## Project

### Testing

- **One run failed most tests that construct an `App`**, right after a build, and sixteen runs
  after it passed. The cause is not known, and the failing run's messages were not kept.
- **Nothing renders in a test.** Tests use `NullGraphicsDevice`. A headless or offscreen Vulkan run
  with a screenshot to compare would cover the renderer.

### Prose

The code carried over from the module repositories predates [STYLE.md](STYLE.md). Its comments use
spaced hyphens, em dashes and colons as joints, name module repositories that no longer exist
(`Engine.Textures`, `Engine.Scenes`), and some restate the line below them. Each file is to be
brought under the style guide when it is next changed, and the checks at the end of STYLE.md report
what is left.

### Build and release

- **CI covers Linux only.** `.github/workflows/build.yml` builds and tests on Ubuntu. Windows and
  macOS runners, and a job that runs the examples offscreen, are not set up.
- **No package.** The engine is consumed as a project reference. A NuGet package carrying the
  shaders and the native SDL3 libraries is needed for a game outside this repository.
- **The command line has no evaluator.** `./e3d` lists, runs commands, drives input (keyboard,
  text, mouse and gamepads, reaching ImGui as well) and captures, `entity.set` writes one field, and
  a game adds commands with `[Command]`, but C# cannot be typed at a running app, and an entity
  cannot be spawned or given a new component from the CLI.
- **A headless run cannot capture.** `--hidden` renders into a window that is never shown, which
  needs a display server. Rendering into an image with no surface at all (for CI) needs an
  offscreen target in place of the swapchain.
