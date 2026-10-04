# TODO

Work outstanding on 3DEngine, in the order it blocks making a game: a window
and a frame a program drives with plain calls, something on screen, content loaded from files, an
interface, behavior, and shipping the result.

The ECS, the schedule and the behavior generator exist and are tested, and every area of the flat
API described in [DESIGN.md](DESIGN.md) has a first version. What is thin is depth. Lighting has
one shadow and no materials behind it, and each area of the flat API lacks pieces raylib has. The
renderer's own plan is [RENDERING.md](RENDERING.md).

An item says what exists, what is missing, and what the missing part needs. Finished work is
removed from this file, and an item that is partly done is rewritten around what is left.

## Core

### Entities

- **Queries hand out bare ids.** `Has`, `TryGet`, `GetRef`, `GetReadOnly`, `Add`, `Update`,
  `Remove` and `Despawn` take an `Entity` handle too, whose generation refuses a stale one, reads
  answering as if the component were missing and writes throwing. Contacts, `ctx.Entity` and the
  flat API's scene functions hand out handles. Queries and `ctx.EntityId` still give the bare
  `int`, which is right for the frame it is used in and which nothing stops code from keeping
  across frames.
- **Change detection misses raw arrays.** `GetRef`, `Update` and the by-reference queries stamp what
  they hand out with the tick of the running system, a component's first arrival is stamped too, and
  the `Changed` and `Added` filters and `Removed<T>()` see what happened since that system last ran
  (`ChangeTicks`). A writable span (`GetSpan`, `BulkProcess`) stamps every component it holds, since
  any may be written through it. A write through a store's raw array (`ComponentsArray`) is not
  seen, removals are kept for 60 frames only, and a system that has never run sees every stamp made
  before it.

## Rendering

### Cost

- **Per-draw work on the CPU bounds a frame** (RENDERING.md §6, measured by `textures_bunnymark` and
  `models_stress`). Mesh entities write their instances on several threads straight into groups
  the pass draws as they are, and a frame holds about 267,000, of which `MeshEntityDraws` takes
  5.2 ms and copying the 43 MB of instances into the ring 5.8 ms, while the GPU takes 12.5 ms for
  the shadow and model passes. An entity whose transform has not changed could keep its instance
  in a buffer the GPU reads from frame to frame, which would remove most of both copies, and the
  GPU's share needs culling, since every cascade draws every entity. Each `DrawTexture` costs about 55 nanoseconds, which writing four vertices for a quad in
  place of six did not change measurably, so the draw list's lock is the next part to time, and
  the GPU draws 186,000 sprites in 5.1 ms.

### The flat API

`Engine3D` covers the window, timing, input, the frame, 2D and 3D cameras, render targets, 2D and
3D shapes, 2D collision, images and textures, models and meshes, shaders, lights, states, scenes,
physics, text and fonts, audio and text files
([CHEATSHEET.md](CHEATSHEET.md)). What is missing:

- **Models are partial.** Skinned meshes are posed on the GPU at a frame, between frames
  (`UpdateModelAnimationAt`) or between two clips (`UpdateModelAnimationBlend`), and on the CPU in a
  run with no renderer. A mesh posed on the GPU keeps its vertices at rest on the CPU, where its
  wires are posed from the same joints, and a collider made from it is at rest. An entity plays a
  file's clips through `AnimatedModel`, which loads a copy of the model for each entity and poses
  and draws it through the flat API, so only in the app `InitWindow` built, and a skinned file a
  scene spawns as mesh entities through `ModelRef` stands at rest. A material the program makes
  draws both sides of each face unless `DoubleSided` is cleared, so `GenMeshCubicmap` makes no roof
  over a maze's open cells as raylib's does.
- **Fonts reach the Basic Multilingual Plane only.** A coverage font loaded from a file is baked
  again at a size it is drawn at a quarter or more past its own, eight sizes at most, and one
  loaded as `FontType.Sdf` stays sharp at any size. A font has Latin-1 or the characters it was
  asked for, and characters above U+FFFF (most emoji) cannot be baked, because ImGui's atlas names
  characters in 16 bits.

### Meshes, materials and light

- **The environment is one prefiltered cube.** The model pass reflects up to 16 light entities and
  an environment map by the material's metallic-roughness model (RENDERING.md §3 and §4). The map's
  roughest mip stands in for a cosine-weighted irradiance, it is made on the CPU in a few hundred
  milliseconds, and there are no reflection probes for the inside of a room. A mesh entity is drawn
  into the window through the first camera entity without a render texture, and into each camera
  entity's render texture, with the shadow fitted to the window's camera, and an `AnimatedModel`
  into the window only.
- **Layouts are written by hand.** A dispatch runs a compute shader over storage buffers, which
  the CPU reads back and drawing shaders read, and textures it writes and samples (RENDERING.md
  §1), but descriptor layouts and vertex inputs are still written by hand beside each pipeline
  rather than read from the reflection. A texture a dispatch writes keeps its other mip levels as
  they were, and a render texture cannot be written.
- **One directional, four spot and four point lights cast shadows.** The first directional light
  with `CastsShadows` set shadows what the window's camera sees within 150 units, or the distance
  `SetShadowDistance` sets, in three cascades, the first four such spot lights shadow their cones
  in the map's last tile, and the first four such point lights shadow all around them, six faces of
  512 texels each (RENDERING.md §4). The tile and face sizes are constants, a fifth spot or point
  light casts none, and render targets sample the window camera's map.

### The device

Every pass is a `VkRenderPass` with framebuffers, every buffer and image has an allocation of its
own, and barriers are synchronization1. Dynamic rendering, synchronization2 and the Vulkan Memory
Allocator replace them (RENDERING.md, What the engine needs).

## Interface

### Dear ImGui

ImGui is drawn by `ImGuiRenderNode` into the main pass. Its frame starts in `PreUpdate`, so ImGui
calls a system makes in `First` are lost. Docking is enabled (`gui_imgui_window` makes a dock
space over the window), but docking by a drag has not been checked, since a drag injected through
`./e3d` moves a window without resting on the drop targets. Viewports, which take ImGui windows
out of the game's window, are not supported. Keyboard
navigation is on, which makes `WantCaptureKeyboard` true whenever an ImGui window has focus, so
the engine's own shortcuts ask `WantTextInput` instead.

## Simulation

### Physics

`PhysicsWorld` runs BepuPhysics with bodies and colliders from components, steps once per
`Stage.FixedUpdate` run on `FixedTime`'s step (the same steps `[OnFixedUpdate]` behaviors run on),
answers `Raycast`, and writes each body's `Transform` blended between its last two steps by
`FixedTime.Alpha`. That blend is the `Transform` game code reads too, so code that needs the
simulation's own pose asks `PhysicsWorld.GetPosition`. A body under a `Parent` is given the local
`Transform` that puts it at its pose under the parent as the parent is in that frame, so it can be
grouped under a level's entity and stays where the simulation has it, and a parent never carries
it. Two bodies starting and stopping touching (a hundredth of a unit apart or closer) are sent as
`ContactStarted` and `ContactEnded` events after each step and cleared at `Stage.First`, with the
entities as they were when the contact started. A resting pair whose bodies sleep stays touching.

The flat API creates boxes, spheres, capsules, static and kinematic boxes, triggers, which report
what enters them as contacts and stop nothing, and level geometry shaped as a model's triangles. It
joins bodies with ball, hinge, weld and distance joints, a hinge limited between two angles or
driven by a motor, reads their blended poses, pushes them, casts rays and reads the frame's contacts
with the point and normal where each pair met (CHEATSHEET.md, Physics). A `Collider` marked
`IsTrigger` makes a trigger from a scene, and a kinematic body under a `Parent` follows its place
under the parent by velocity, so a platform a moving parent carries carries what stands on it, a
character walking relative to it and a crate by friction. A contact carries the speed its pair
closed at as they met, read while they approach since the solver slows them before they touch, and
not the impulse the solver gave them. A hinge has limits and a motor, a ball joint a cone it swings
and twists within, and a distance joint a range that can change, and no other joint has a motor. Two
bodies a joint holds do not collide with each other. The character controller is a dynamic capsule
walked toward a velocity before each step, which slides along walls, climbs steps up to its step
height (its radius unless set), holds slopes up to its limit, rides what moves under it, crouches
and stands where there is room, and reports ground.

### Scenes

`SceneFile` saves a level of entities and their `[SceneComponent]` and behavior components to JSON
and loads it back (ARCHITECTURE.md, Scene files). Arrays are not saved, so a mesh entity made in
code comes back without its mesh, and a level shows meshes through `ModelRef`. A body is described
by a `Collider` (box, sphere, capsule, or the meshes of the entity and those under it) and a
`RigidBody` (static, dynamic with a mass, or kinematic), which a file holds, and `PhysicsBodies`
makes it when the entity appears, a character when a `CharacterController` is beside a capsule. A
`Joint` on an entity of its own joins two entities' bodies at its place, and a `PhysicsMaterial`
beside a `Collider` gives its body a friction and a bounce. A scene file placed in another with
`SceneRef` is spawned once, when the reference first appears, and a change to the placed file
reaches a running level only when it is loaded again. An older file is read by keeping the fields it
has, with no migration.

`SceneLightPayload` and `Light` hold what the model pass reads, and the model pass reads every
field of `SceneMaterialPayload`. A blended surface casts the shadow of a solid, with no lighter
shadow where it is clearer.

## Platform

### Input

Keyboard, mouse, typed text and gamepads come from SDL3 into the `Input` resource, and the flat API
hands out typed characters and pressed keys one at a time (`GetCharPressed`, `GetKeyPressed`). Text
input is started once on the window and never stopped. An ImGui text field being typed into places
the input method's composition window at its cursor, which is checked against what ImGui reports and
not with an input method running, and text a game reads itself (`GetCharPressed`) has no place to
give one. Typing from a real keyboard has only been checked through injected text. Fingers are read
as touch points (`GetTouchPosition`) and recognized as raylib's gestures (taps, holds, drags, swipes
and pinches), and a gamepad's gyro, accelerometer, touchpad and light are read and set through SDL,
which has been checked against the state it fills and not with a pad that has them.

## Project

### Testing

- **Render tests check a few pixels.** `OffscreenRenderTests` draws each pass offscreen (shapes,
  text, render targets, immediate and model shaders, lit models and ImGui) and reads chosen pixels
  back. Whole frames are not compared with references, so a fault that leaves those pixels right
  is caught only by looking at the example captures CI takes.

### Prose

The code carried over from the module repositories predates [STYLE.md](STYLE.md). Its comments use
spaced hyphens, em dashes and colons as joints, name module repositories that no longer exist
(`Engine.Textures`, `Engine.Scenes`), and some restate the line below them. Each file is to be
brought under the style guide when it is next changed, and the checks at the end of STYLE.md report
what is left.

### Build and release

- **CI draws on Linux only.** `.github/workflows/test.yml` builds and tests with lavapipe and the
  validation layer on Ubuntu, and builds and runs the tests that need no device on Windows.
  `build.yml` runs it on each push and then captures every example offscreen, and `pack.yml` runs it
  before packing. Nothing draws on Windows, and macOS has no job.
- **The command line has no evaluator.** `./e3d` lists, runs commands, drives input (keyboard,
  text, mouse and gamepads, reaching ImGui as well) and captures, spawns and despawns entities,
  adds components and writes their fields, and a game adds commands with `[Command]`, but C#
  cannot be typed at a running app, and a field holding an array (a `Mesh`'s positions) cannot be
  written from it.
