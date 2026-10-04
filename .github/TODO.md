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
- **Change detection has no added filter and misses spans.** `GetRef`, `Update` and the
  by-reference queries stamp what they hand out with the tick of the running system, and a
  `Changed` filter sees what was stamped since that system last ran (`ChangeTicks`). A component
  added with `Add` is not stamped, so nothing tells a system what appeared since it ran, as
  Bevy's `Added` does, and a write through a span of a store's array is not seen. A system that
  has never run sees every stamp made before it.

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

### Cost

- **Per-draw work on the CPU bounds a frame** (RENDERING.md §6, measured by `textures_bunnymark` and
  `models_stress`). Mesh entities are instanced, and a frame holds about 34,000, where
  `MeshEntityDraws` takes 6.4 ms building a `ModelDraw` for each entity every frame and the two
  passes 9 ms gathering and writing instances. Writing an entity's instance straight from its
  components, kept from frame to frame while nothing marks it changed, would remove most of both.
  Each `DrawTexture` costs about 55 nanoseconds, of which the draw list's lock and the two vertices
  a quad repeats without an index buffer are most, and the GPU draws 186,000 sprites in 5.1 ms. An
  index buffer for quads would cut the vertices a third.
- **Skinning runs on the CPU.** An animated mesh's posed vertices are written into a ring of
  mapped buffers, which costs its vertex count in copying each frame. GPU skinning would upload
  the bone matrices instead, with each vertex's bone indices and weights kept in its buffer, and is
  the step after the ring.

### The flat API

`Engine3D` covers the window, timing, input, the frame, 2D and 3D cameras, render targets, 2D and
3D shapes, 2D collision, images and textures, models and meshes, shaders, lights, states, scenes,
physics, text and fonts, audio and text files
([CHEATSHEET.md](CHEATSHEET.md)). What is missing:

- **Custom shaders are partial.** A model shader reads uniforms by name, but an immediate shader
  still reads four `float4` slots, and no shader can bind textures of its own beyond the one it
  draws. Each draw with a model shader takes a descriptor set from a pool of 4096 shared with
  textures, kept for four frames, so a frame has room for about a thousand such draws.
- **Audio is partial.** Sounds have no pan in the flat API, MP3 and FLAC are not read, and a WAV
  file played as music is read whole rather than streamed.
- **Models are partial.** The flat API has no lights of its own, so models are lit by one fixed
  light, which shows their color, texture and normal map but not how metallic or rough they are,
  unless the ECS holds light entities. `UpdateModelAnimation` poses skinned meshes on the CPU, as raylib does by default,
  and writes the vertices into a ring of buffers (Cost above). Clips are sampled at
  60 frames a second with no blending between frames or between two clips, and mesh entities have
  no animation component. The model pass draws both sides of every face, so `GenMeshCubicmap`
  makes no roof over a maze's open cells as raylib's does.
- **Images and textures are partial.** Images are edited on the CPU (resize, flip, colors,
  shapes, `ImageDraw`), but text cannot be drawn into an image (`ImageDrawText`), and Perlin and
  cellular noise are not generated. Mip levels are made by GPU blits. Anisotropic filtering is
  not offered.
- **Fonts bake at one size each**, with no signed distance fields, so text far larger than its
  bake blurs. A font has Latin-1 or the characters it was asked for, and characters above U+FFFF
  (most emoji) cannot be baked, because ImGui's atlas names characters in 16 bits.
- **Render targets** have no depth to sample. The window and targets are multisampled at
  `Config.Samples` (4 by default, `SetConfigSamples` before the window opens). Window state and
  monitors are queried and changed, but a monitor's modes cannot be listed or switched, and the
  windows are always resizable, where raylib's are only with `FLAG_WINDOW_RESIZABLE`.

### Meshes, materials and light

- **The environment is one prefiltered cube.** The model pass reflects up to 16 light entities
  and an environment map by the material's metallic-roughness model (RENDERING.md §3 and §4). The
  map's roughest mip stands in for a cosine-weighted irradiance, it is not drawn as a sky behind
  the scene, it is made on the CPU in a few hundred milliseconds, and there are no reflection
  probes for the inside of a room. A mesh entity is drawn through the first camera entity only,
  into the window only.
- **Shader reflection and compute** are not built (RENDERING.md §1).
- **One directional light casts a shadow, from one map.** The first directional light with
  `CastsShadows` set shadows what the window's camera sees within 40 units (RENDERING.md §4). The
  map's size and that distance are constants, a long view loses detail near the camera without
  cascades, point and spot lights cast none, and render targets sample the window camera's map.

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

The flat API creates boxes, spheres and static and kinematic boxes, reads their blended poses,
pushes them, casts rays and reads the frame's contacts (CHEATSHEET.md, Physics). What is missing is
capsules, meshes and joints in the flat API, contact points, normals and impulses on the events,
triggers that report overlap without colliding, and a body whose parent moves it, as a platform
carries what stands on it, which needs a kinematic body driven from the parent's pose. The character
controller is a dynamic capsule walked toward a velocity before each step, which slides along walls,
rides edges lower than about half its radius, holds slopes up to its limit and reports ground. It
does not climb a taller step, ride a moving platform, or crouch.

### Scenes

`SceneFile` saves a level of entities and their `[SceneComponent]` and behavior components to JSON
and loads it back (ARCHITECTURE.md, Scene files). Arrays are not saved, so a mesh entity made in
code comes back without its mesh, and a level shows meshes through `ModelRef`. A body is described
by a `Collider` (box, sphere or capsule) and a `RigidBody` (static, dynamic with a mass, or
kinematic), which a file holds, and `PhysicsBodies` makes it when the entity appears, a character
when a `CharacterController` is beside a capsule. Joints, physics materials and mesh colliders are
not described. There are no prefabs (a scene file spawned as part of another), and an older file is
read by keeping the fields it has, with no migration.

`SceneLightPayload` and `Light` hold what the model pass reads. Of `SceneMaterialPayload`'s fields
the model pass reads all but the alpha mode and the double-sided flag. A draw is translucent when
its color's alpha is below 255 and is then drawn after the opaque ones, in the order recorded or,
for mesh entities, far to near, but a texture with clear parts on an opaque color is drawn with
the opaque batches, and glTF's mask and blend modes, which would say so, are not read.

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

- **CI draws on Linux only.** `.github/workflows/build.yml` builds, tests with lavapipe and the
  validation layer and captures every example offscreen on Ubuntu, and builds and runs the tests
  that need no device on Windows. The Windows job has not run yet, nothing draws there, and macOS
  has no job.
- **The package is local.** `build/pack.sh` makes a package a game outside this repository
  builds and runs from with no `slangc` (BUILDING.md), but it is not published to nuget.org, its
  version is set by hand, and CI does not make one.
- **The command line has no evaluator.** `./e3d` lists, runs commands, drives input (keyboard,
  text, mouse and gamepads, reaching ImGui as well) and captures, spawns and despawns entities,
  adds components and writes their fields, and a game adds commands with `[Command]`, but C#
  cannot be typed at a running app, and a field holding an array (a `Mesh`'s positions) cannot be
  written from it.
