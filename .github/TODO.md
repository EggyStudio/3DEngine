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
  chunk of 4096 entities none of which changed keeps the instances it gathered the frame before,
  so 400,000 standing still take 2.4 ms in place of 6.0, but every instance is still copied into
  the ring each frame and a culled block with them, and one entity moving gathers its whole chunk
  again. A frame holds about 243,000 sprites, each `DrawTexture` about 48 nanoseconds with the
  example's loop, the upload 3.0 ms and the GPU 6.3 ms, so what is left is shared between the
  three.

- **A crowd's physics is stepped on one worker.** `games/Swarm` walks about 280 creatures at once,
  each a dynamic capsule on the character controller, in 2.0 ms of physics a frame, and 2000 take
  12 ms a step, of which the controllers' ground rays take 2.1 ms on several threads, contacts 1.5 ms
  and Bepu's own step 8 ms. `PhysicsSettings.WorkerThreads` is 1 by default, so the step is the same
  on every machine, and more workers save little at these sizes (4.4 ms with four at 2000, and
  thirty-one take longer than one at 290), so a world of thousands of bodies sets it.

### The flat API

`Engine3D` covers the window, timing, input, the frame, 2D and 3D cameras, render targets, 2D and
3D shapes, 2D collision, images and textures, models and meshes, shaders, lights, states, scenes,
physics, text and fonts, audio, audio streams and waves, and files
([CHEATSHEET.md](../CHEATSHEET.md)). What is missing:

- **128 of raylib's 619 functions are not carried**, which `build/raylib-bench/coverage.py` names.
  Most are what C# has, its strings, codepoints, files, directories, hashes, compression and freeing
  of memory, and the exports as C code. `LoadImageFromScreen` needs the frame as drawn, which the
  GPU has not finished when the call returns and has presented after. Images are eight bits a
  channel with one level, so `ImageFormat`, `LoadImageRaw`, `ImageMipmaps` and the raw pixel
  functions have nothing to do, and textures are two-dimensional in one format, so cubemaps and
  render textures of other formats are left out. Shapes are drawn untextured, and fonts keep their
  glyphs by codepoint in ImGui's atlas, so the shapes texture, `GetGlyphIndex`, `LoadFontData` and
  `GenImageFontAtlas` have no meaning. The vertex layout is fixed and has no tangents, for
  `UpdateMeshBuffer`, `GenMeshTangents` and `GetShaderLocationAttrib`. The audio processors and
  `UpdateSound` reach into the audio thread, which the backend does not open to the program. VR
  stereo, automation events (which `./e3d` stands in for), the frame control a loop of its own needs
  and the monitor's size in millimetres (which SDL3 does not give) are left out too.

- **Models are partial.** Skinned meshes are posed on the GPU at a frame, between frames
  (`UpdateModelAnimationAt`), between two clips (`UpdateModelAnimationBlend`) or with a clip on
  part of the skeleton (`UpdateModelAnimationLayer`), with their morph targets moved by weights a
  clip or `SetModelMorphWeight` sets, and on the CPU in a run with no renderer. A clip on part of
  the skeleton leaves the morph weights to the clip under it. A mesh posed on the GPU keeps its vertices at rest on the CPU, where its
  wires are posed from the same joints, and a collider made from it is at rest. An entity plays a
  file's clips through `AnimatedModel`, which loads a file once and gives each entity a copy with
  skinned meshes of its own, and poses and draws it through the flat API, so only in the app
  `InitWindow` built, where a file with clips a level places through `ModelRef` plays its first on
  a loop through one. A material the program makes draws both sides of each face unless
  `DoubleSided` is cleared, so `GenMeshCubicmap` makes no roof over a maze's open cells as raylib's
  does.
- **Color emoji and distance fields past U+FFFF are not drawn.** A coverage font loaded from a
  file is baked again at a size it is drawn at a quarter or more past its own, eight sizes at
  most, and one loaded as `FontType.Sdf` stays sharp at any size. A font has Latin-1 or the
  characters it was asked for, those past U+FFFF drawn by the engine's own TrueType reader into
  the same atlas, since ImGui's names characters in 16 bits. A font of CFF outlines or color
  bitmaps (most color emoji) gives none past U+FFFF, and a distance field font none either.

### Meshes, materials and light

- **Probes capture once and on the CPU.** The model pass reflects up to 16 light
  entities and an environment map by the material's metallic-roughness model, its diffuse light
  from nine spherical harmonics of irradiance (RENDERING.md §3 and §4), and inside a reflection
  probe's box the probe's capture in place of the map. The map is made on the CPU in a few
  hundred milliseconds. A probe is captured in half floats from the meshes the window draws, or the
  first render target's when it draws none, and its room is read back and prefiltered on a worker
  thread, so a probe is captured again only when it moves or `UpdateReflectionProbe` asks, and a
  door opening in its room or a lamp going out is not seen until then. A mesh entity and an
  `AnimatedModel` are drawn into the window through the first camera entity without a render
  texture, and into each camera entity's render texture, each with its shadow fitted to its own
  camera.
- **Vertex inputs are written by hand.** A dispatch runs a compute shader over storage buffers,
  which the CPU reads back and drawing shaders read, and textures it writes and samples, and every
  pass's descriptor set layouts are read from its shaders' reflection (RENDERING.md §1). The vertex
  inputs are still written beside each pipeline for the engine's fixed formats, a sampler declared
  apart from its texture is not bound, and a render texture is written only where the GPU can
  store to the window's format.
- **One directional, ten spot and twelve point lights cast shadows.** The first directional light
  with `CastsShadows` set shadows what each view's camera sees within 150 units, or the distance
  `SetShadowDistance` sets, in three cascades, ten such spot lights shadow their cones in the map's
  last tile, and twelve such point lights shadow all around them (RENDERING.md §4), those the camera
  sees ranked first and then by how near their reach comes, the first two spots and four points
  with the most texels. `SetShadowMapSize` sets the tile from 256 to 4096 texels (2048 by default).
  An eleventh spot or a thirteenth point light casts none, the ranking does not weigh a light's
  brightness or how much of the picture it lights, and each render target that draws meshes draws
  the map again for its own camera, with the point and spot lights chosen for the window's.

- **Particles are drawn into the window alone.** A `ParticleEmitter` gives off particles a compute
  shader steps, drawn as squares facing the window's camera after its meshes, lit or giving off
  their own light, with a rate, a burst, a life, a velocity in a cone, gravity, and a size and color
  that change over each life (RENDERING.md §3). They are not drawn into render textures or a probe's
  capture, those laid over by alpha are not sorted from back to front, a particle is a round soft
  dot with no texture of the program's own, and none collides with the world or slows by drag.

- **Effects over the frame are bloom, exposure fixed or following the scene, a curve, grading, a
  vignette, FXAA, depth of field and motion blur.** Any of them draws the window's scene into a
  half-float target and brings it into the window in one pass, after passes of their own for the
  depth of field and motion blur (RENDERING.md §5). With all of them off the tonemap still runs at
  the end of the model pass, render targets stay eight bits, a shader of the program's own inside
  `BeginMode3D` is read as linear in the HDR frame, motion blur knows only the camera's movement and
  not a thing's own, and there is no ambient occlusion.

### The device

Passes are drawn by dynamic rendering and barriers are synchronization2's, on Vulkan 1.3. Buffers and
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

- **Sixteen scenes are compared whole.** `OffscreenRenderTests` draws each pass offscreen (shapes,
  text, render targets, immediate and model shaders, lit models and ImGui) and reads chosen pixels
  back, and `ReferenceFrameTests` compares whole frames with the references beside it, allowing 2
  percent of the pixels to differ, which a missing shadow exceeds at 4. They are 2D shapes and
  text, a lit and shadowed scene, a render texture, an ImGui window, materials with maps beside a
  model shader, a skinned model posed mid-clip, point and spot shadows, an environment map with
  its sky, bloom, the other effects over the frame together, a reflection probe, a dozen shadowed
  lights, a morph target beside a clip on part of a skeleton, text in a font from a file, a
  texture a compute shader wrote and a frame of Summit's level. Audio and input have no frame to
  compare and are tested by their values.

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
