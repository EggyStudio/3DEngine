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
- **Copying queries do not filter.** `QueryRef<T>()` and `QueryRef<T1, T2>()` narrow with
  `With`, `Without` and `Changed` (up to four of each) and allocate nothing. There is no by-reference
  query of three components, and `Query<...>` yields copies through an allocating iterator.
- **Transform propagation walks every parented entity each frame**, with a dictionary, in
  `Stage.Render`. It has no change detection, so a large static hierarchy costs its size every
  frame, and physics writes a body's `Transform` as if it had no parent. Skipping unchanged chains
  by change bits needs `GetRef` to mark what it hands out as changed, as Bevy's `Mut` does, since
  code moves transforms through it and it marks nothing.

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
- **Audio is partial.** Sounds have no pan in the flat API, MP3 and FLAC are not read, and a WAV
  file played as music is read whole rather than streamed.
- **Models are partial.** Only the base color and its texture are used of a material, animation
  is not played, and the flat API has no lights of its own, so models are lit by one fixed light
  unless the ECS holds light entities. A mesh cannot be changed after upload, and the model
  pass draws both sides of every face, so `GenMeshCubicmap` makes no roof over a maze's open cells
  as raylib's does.
- **Images and textures are partial.** Images are edited on the CPU (resize, flip, colors,
  shapes, `ImageDraw`), but text cannot be drawn into an image (`ImageDrawText`), and Perlin and
  cellular noise are not generated. Mip levels are made by GPU blits, which have not been run
  under the validation layers, since the machine they were written on has none installed.
  Anisotropic filtering is not offered.
- **Fonts bake at one size each**, with no signed distance fields, so text far larger than its
  bake blurs. A font has Latin-1 or the characters it was asked for, and characters above U+FFFF
  (most emoji) cannot be baked, because ImGui's atlas names characters in 16 bits.
- **Render targets** have no multisampling and no depth to sample, and the window has no
  multisampling either. Window state and monitors are queried and changed, but a monitor's
  modes cannot be listed or switched, and there is no `SetConfigFlags` for choosing these before
  the window opens.

### Meshes, materials and light

- **Lighting is diffuse only.** The model pass sums up to 16 light entities (distant, dome as
  ambient, and every other kind as a point, with a spot's cone), by Lambert's cosine and the
  square of the distance, and falls back to one fixed light when there are none. There is no
  specular, no tonemapping (the sum is clamped), and of a material only the base color is used
  (RENDERING.md §3 and §4). A mesh entity is drawn through the first camera entity only, into the
  window only.
- **Shader reflection and compute** are not built (RENDERING.md §1).
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
answers `Raycast`, and writes each body's `Transform` blended between its last two steps by
`FixedTime.Alpha`. That blend is the `Transform` game code reads too, so code that needs the
simulation's own pose asks `PhysicsWorld.GetPosition`. There is no `Engine3D` surface for it, a
body's `Transform` is written as if it had no parent, and contacts are not reported as events.

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

Keyboard, mouse, typed text and gamepads come from SDL3 into the `Input` resource, and the flat
API hands out typed characters and pressed keys one at a time (`GetCharPressed`,
`GetKeyPressed`). Text input is started once on the window and never stopped, so there is no IME
composition window placed at a text field, and typing from a real keyboard has only been checked
through injected text. Touch is not read, and a gamepad's sensors (gyro, touchpad) and lights are
not reached.

## Project

### Testing

- **One run failed most tests that construct an `App`**, right after a build, and sixteen runs
  after it passed. The cause is not known, and the failing run's messages were not kept. (Runs
  that reported fewer tests than exist were the test host crashing in the render tests, through
  Vortice's cache of API tables by handle, which the device no longer uses.)
- **Few tests render.** `OffscreenRenderTests` draws shapes and a lit cube offscreen and reads the
  pixels back. The other passes (text, ImGui, render targets, custom shaders) are covered only by
  the example captures CI takes, which nothing compares against a reference.

### Prose

The code carried over from the module repositories predates [STYLE.md](STYLE.md). Its comments use
spaced hyphens, em dashes and colons as joints, name module repositories that no longer exist
(`Engine.Textures`, `Engine.Scenes`), and some restate the line below them. Each file is to be
brought under the style guide when it is next changed, and the checks at the end of STYLE.md report
what is left.

### Build and release

- **CI covers Linux only.** `.github/workflows/build.yml` builds, tests with lavapipe and captures
  every example offscreen on Ubuntu, which has not been run since it was written. Windows and
  macOS runners are not set up.
- **The package is local.** `build/pack.sh` makes a package a game outside this repository
  builds and runs from with no `slangc` (BUILDING.md), but it is not published to nuget.org, its
  version is set by hand, and CI does not make one.
- **The command line has no evaluator.** `./e3d` lists, runs commands, drives input (keyboard,
  text, mouse and gamepads, reaching ImGui as well) and captures, spawns and despawns entities,
  adds components and writes their fields, and a game adds commands with `[Command]`, but C#
  cannot be typed at a running app, and a field holding an array (a `Mesh`'s positions) cannot be
  written from it.
