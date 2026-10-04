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

## Rendering

### Cost

- **Per-entity work on the CPU bounds a frame** (RENDERING.md §6, measured by `textures_bunnymark`
  and `models_stress`). Mesh entities write their instances on several threads straight into
  groups the pass copies into its ring on several threads, and each view draws the blocks of 64
  instances it sees. A frame holds about 410,000, of which `MeshEntityDraws` takes 5.6 ms and the
  program's loop turning them most of the rest, while the GPU takes 5.1 ms for the model pass. A
  culled block is still written into the ring, and an entity whose transform has not changed is
  written again each frame, though its instance, which holds nothing of the camera, would be the
  same. A frame holds about 243,000 sprites, each `DrawTexture` about 48 nanoseconds with the
  example's loop, the upload 3.0 ms and the GPU 6.3 ms, so what is left is shared between the
  three.

### The flat API

`Engine3D` covers the window, timing, input, the frame, 2D and 3D cameras, render targets, 2D and
3D shapes, 2D collision, images and textures, models and meshes, shaders, lights, states, scenes,
physics, text and fonts, audio, audio streams and waves, and files
([CHEATSHEET.md](../CHEATSHEET.md)). What is missing:

- **A few of raylib's functions are not carried.** `UpdateTextureRec` needs a texture's pixels on
  the CPU, which the texture store does not keep. `LoadImageFromScreen` needs the frame as it is
  drawn, which the GPU has not finished when the call returns. The audio processors run on the
  audio thread, which the backend does not open to the program. VR stereo, automation events
  (which `./e3d` stands in for), `ImageMipmaps`, `GenImageText`, `ExportMesh` and the C string
  helpers (`TextFormat`, `TextSplit` and the rest, which C# has) are left out.

- **Models are partial.** Skinned meshes are posed on the GPU at a frame, between frames
  (`UpdateModelAnimationAt`) or between two clips (`UpdateModelAnimationBlend`), and on the CPU in
  a run with no renderer. A mesh posed on the GPU keeps its vertices at rest on the CPU, where its
  wires are posed from the same joints, and a collider made from it is at rest. An entity plays a
  file's clips through `AnimatedModel`, which loads a file once and gives each entity a copy with
  skinned meshes of its own, and poses and draws it through the flat API, so only in the app
  `InitWindow` built, and a skinned file a scene spawns as mesh entities through `ModelRef` stands
  at rest. A material the program makes draws both sides of each face unless `DoubleSided` is
  cleared, so `GenMeshCubicmap` makes no roof over a maze's open cells as raylib's does.
- **Fonts reach the Basic Multilingual Plane only.** A coverage font loaded from a file is baked
  again at a size it is drawn at a quarter or more past its own, eight sizes at most, and one
  loaded as `FontType.Sdf` stays sharp at any size. A font has Latin-1 or the characters it was
  asked for, and characters above U+FFFF (most emoji) cannot be baked, because ImGui's atlas names
  characters in 16 bits.

### Meshes, materials and light

- **The environment is one prefiltered cube.** The model pass reflects up to 16 light entities and
  an environment map by the material's metallic-roughness model, its diffuse light from nine
  spherical harmonics of irradiance (RENDERING.md §3 and §4). The map is made on the CPU in a few
  hundred milliseconds, and there are no reflection probes for the inside of a room. A mesh entity
  and an `AnimatedModel` are drawn into the window through the first camera entity without a
  render texture, and into each camera entity's render texture, each with its shadow fitted to
  its own camera.
- **Layouts are written by hand.** A dispatch runs a compute shader over storage buffers, which
  the CPU reads back and drawing shaders read, and textures it writes and samples (RENDERING.md
  §1), but descriptor layouts and vertex inputs are still written by hand beside each pipeline
  rather than read from the reflection. A render texture is written only where the GPU can store
  to the window's format.
- **One directional, four spot and four point lights cast shadows.** The first directional light
  with `CastsShadows` set shadows what each view's camera sees within 150 units, or the distance
  `SetShadowDistance` sets, in three cascades, four such spot lights shadow their cones in the
  map's last tile, and four such point lights shadow all around them, six faces of a quarter of a
  tile each (RENDERING.md §4), the four of each whose reach comes nearest the camera.
  `SetShadowMapSize` sets the tile from 256 to 4096 texels (2048 by default). A fifth spot or point
  light near the camera casts none, and each render target that draws meshes draws the map again
  for its own camera, with the point and spot lights chosen for the window's.

### The device

Every pass is a `VkRenderPass` with framebuffers, and barriers are synchronization1. Dynamic
rendering and synchronization2 replace them (RENDERING.md, What the engine needs). Buffers and
textures are carved out of blocks of 64 MiB a memory type, ten thousand buffers and three thousand
textures in a handful of allocations, and render targets, cube maps and the frame's images keep an
allocation each.

## Interface

### Dear ImGui

ImGui is drawn by `ImGuiRenderNode` into the main pass. Its frame starts in `PreUpdate`, so ImGui
calls a system makes in `First` are lost. Docking is enabled (`gui_imgui_window` makes a dock space
over the window), and a window dragged onto a dock target and held there docks, as `./e3d command
input.drag Left 280 156 20 20` shows on that example. Viewports, which take ImGui windows out of the
game's window, are not supported. Keyboard navigation is on, which makes `WantCaptureKeyboard` true
whenever an ImGui window has focus, so the engine's own shortcuts ask `WantTextInput` instead.

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
and loads it back (ARCHITECTURE.md, Scene files), a mesh entity made in code with its arrays and a
model through its `ModelRef`. A body is described by a `Collider` (box, sphere, capsule, or the
meshes of the entity and those under it) and a `RigidBody` (static, dynamic with a mass, or
kinematic), which a file holds, and `PhysicsBodies` makes it when the entity appears, a character
when a `CharacterController` is beside a capsule. A `Joint` on an entity of its own joins two
entities' bodies at its place, and a `PhysicsMaterial` beside a `Collider` gives its body a friction
and a bounce. A scene file placed in another with `SceneRef` is spawned when the reference first
appears, and again in place of that copy when the file is written while the level runs. An older
file is read by keeping the fields it has, with no migration.

`SceneLightPayload` and `Light` hold what the model pass reads, and the model pass reads every
field of `SceneMaterialPayload`.

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

- **Six scenes are compared whole.** `OffscreenRenderTests` draws each pass offscreen (shapes,
  text, render targets, immediate and model shaders, lit models and ImGui) and reads chosen pixels
  back, and `ReferenceFrameTests` compares whole frames of 2D shapes and text, a lit and shadowed
  scene, a render texture, an ImGui window, materials with maps beside a model shader, and a
  skinned model posed mid-clip with the references beside it, allowing 2 percent of the pixels to
  differ, which a missing shadow exceeds at 4. Point and spot shadows, the environment map, fonts
  baked from files and compute shaders have no reference, and are caught by their chosen pixels
  and the example captures CI takes.

### Prose

The code carried over from the module repositories predates [STYLE.md](STYLE.md). Its dashes,
spaced hyphens and padded banners are gone, and so are its claims of readers and modules that never
came (USD, MaterialX, a web view, an editor). The colons that joined clauses in its comments are
rewritten, so the colon check reports lists and labels, and some comments still restate the line
below them. Each file is to be brought under the style guide when it is next changed, and the
checks at the end of STYLE.md report what is left.

### Build and release

- **CI draws on Linux only.** `.github/workflows/test.yml` builds and tests with lavapipe and the
  validation layer on Ubuntu, and builds and runs the tests that need no device on Windows.
  `build.yml` runs it on each push and then captures every example offscreen, and `pack.yml` runs it
  before packing. Nothing draws on Windows, and macOS has no job.
- **The command line has no evaluator.** `./e3d` lists, runs commands, drives input (keyboard,
  text, mouse and gamepads, reaching ImGui as well) and captures, spawns and despawns entities,
  adds components and writes their fields, arrays among them, and a game adds commands with
  `[Command]`, but C# cannot be typed at a running app.
