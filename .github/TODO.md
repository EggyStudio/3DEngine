# TODO

Work outstanding on 3DEngine, in the order it blocks making a game: a window
and a frame a program drives with plain calls, something on screen, content loaded from files, an
interface, behavior, and shipping the result.

The ECS, the schedule and the behavior generator exist and are tested, and the flat API described
in [DESIGN.md](DESIGN.md) covers the window, input, the frame, cameras, shapes and text. What is
thin is loading: nothing a program owns can be loaded through the flat API, and the renderer draws
meshes unlit. The renderer's own plan is [RENDERING.md](RENDERING.md).

An item says what exists, what is missing, and what the missing part needs. Finished work is
removed from this file, and an item that is partly done is rewritten around what is left.

## Core

### Entities

- **An entity is an `int`.** `EcsWorld` tracks generations but does not put them in the handle, so
  a handle kept past a despawn refers to whatever entity reuses the slot. An `Entity` struct holding
  an index and a generation, checked on every access, fixes it.
- **Queries have no filters.** `Query` and `QueryRef` take up to three and two components, with no
  `With` or `Without`, and `Query` allocates an iterator. Behaviors filter by attribute, so the gap
  is in hand-written systems.
- **Entities have no names or parents**, which scene files and an ImGui window listing the world
  need.

### Behaviors

- **States** (`[OnEnter]`, `[OnExit]`, `[InState]`) are not implemented.
- **Diagnostics stop at the method.** The generator reports a wrong signature, two stage
  attributes and a bad `[RunIf]` (E3D001 to E3D003). A filter naming a type that is not a
  component, and a behavior whose fields hold references, are not reported.
- **`BehaviorContext` has no fixed delta.** A `[OnFixedUpdate]` method reads the step from
  `ctx.Res<FixedTime>().StepSeconds`, where `ctx.Time.DeltaSeconds` is the frame's.

## Rendering

### The flat API

`Engine3D` covers the window, timing, keyboard and mouse, the frame, `Camera3D`, 2D and 3D shapes,
images and textures, models and meshes, and text ([CHEATSHEET.md](CHEATSHEET.md)). What is missing:

- **Loading shaders.** `LoadShader` with `BeginShaderMode`, its parameters set by name, and an
  example.
- **Music is not streamed.** `LoadMusicStream` decodes the whole file, so `UpdateMusicStream` does
  nothing and a long piece costs its length in memory. Sounds have no pan, and the time a piece has
  played is not reported (`GetMusicTimePlayed`). MP3 and FLAC are not read.
- **Models are partial.** Textures embedded in a file (as `.glb` carries them) are not read, only
  the base color and its texture are used of a material, animation is not played, and models are
  lit by one fixed light. `DrawModelWires`, `GenMeshCylinder` and the other generators raylib has
  are not written, and a mesh cannot be read back or changed after upload.
- **Textures have no mipmaps**, so a texture drawn much smaller than its size shimmers, and an
  image cannot be edited in place (raylib's `ImageDraw*`, `ImageResize` and the rest).
- **Text has no font of its own.** `DrawText` draws with ImGui's built-in font into ImGui's
  foreground layer, so text is always on top of shapes and windows and scales the 13-pixel bitmap.
  A glyph atlas baked from a TTF and drawn in the draw list is needed.
- **Gamepads, render targets and monitors** have no functions.
- **`UpdateCamera`** has the free and orbital modes. raylib's first-person and third-person modes,
  which lock the cursor, are not written.

### Meshes, materials and light

- **Meshes are positions only**, drawn in their material's base color, so normals, textures and the
  lighting buffer change nothing on screen (RENDERING.md §3 and §4).
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

`PhysicsWorld` runs BepuPhysics with bodies and colliders from components, steps at a fixed rate
through its own accumulator in `PreUpdate`, and answers `Raycast`. It is to step in
`Stage.FixedUpdate` on `FixedTime` instead, so behaviors that push bodies run on the same steps. There is no `Engine3D` surface for it, and the
interpolation between steps is not applied to `Transform`, so a body moves in visible steps when the
frame rate is above the physics rate.

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

Keyboard and mouse come from SDL3 into the `Input` resource. Gamepads, text input for fields
outside ImGui and touch are not read.

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
- **The command line has no evaluator.** `./e3d` lists, runs commands, drives input and captures,
  and a game adds commands with `[Command]`, but C# cannot be typed at a running app, and changing a
  component's field needs a command written for it. ImGui receives the mouse from `input.*` and not
  the keyboard, so typing into an ImGui field is not possible from the CLI.
- **A headless run cannot capture.** `--hidden` renders into a window that is never shown, which
  needs a display server. Rendering into an image with no surface at all (for CI) needs an
  offscreen target in place of the swapchain.
