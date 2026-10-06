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
  and `models_stress`). Mesh entities write their instances on several threads straight into groups
  the pass copies into its ring on several threads, and each view draws the blocks of 64 instances
  it sees. `models_stress` without its arms held 410,266 on 2026-10-06 (`E3D_STRESS_ARMS=0`, the
  Release build opened offscreen and `./e3d command profile` read once the search ended, as
  RENDERING.md §6 runs it), in a frame of 17.7 ms: the program's loop turning every entity through
  `GetRef` 8.4 ms, `MeshEntityDraws` 5.4 ms, the first pass, the shadows', copying the instances
  into the ring and boxing their blocks 3.2 ms, and the GPU 4.9 ms for the model pass. Two changes
  would take more of it, and neither pays for its reach at a count no raylib-style game nears.
  `MeshEntityDraws` writing straight into the renderer's mapped ring, across the two worlds and
  after a pass counting each group, would save the copy's read and about 1.5 ms, since most of the
  copy is the write into write-combined memory, which stays. An instance of a 3x4 world matrix and
  an index into a kept table of materials, in place of 96 bytes with its color, emission and
  factors, would halve what the gather writes and the copy moves, about 2.5 to 3 ms, and changes the
  model pass's instance layout and every vertex stage of a program's own that reads an instance's
  color. A chunk of 4096 entities none of which changed keeps the instances it gathered the frame
  before, so 400,000 standing still take 2.4 ms in place of 6.0, but every instance is still copied
  into the ring each frame and a culled block with them, and one entity moving gathers its whole
  chunk again. A frame holds about 243,000 sprites, each `DrawTexture` about 48 nanoseconds with the
  example's loop, the upload 3.0 ms and the GPU 6.3 ms, so what is left is shared between the three.

- **A crowd's physics is mostly its characters' controllers.** The step runs on four workers once
  500 bodies are awake (`PhysicsSettings.ThreadedAbove`), in Bepu's deterministic mode with the
  contacts sorted by pair, so a run repeats to the bit on every machine. Four workers step 2000
  boxes in 1.4 ms where one takes 2.6, and under 500 bodies one worker is as fast or faster, so
  `games/Swarm`'s 180 creatures take 1.5 to 1.8 ms. A character on the flat top of an upright
  static box, a floor or `CreateGroundPlane`, with nothing else near its foot but triggers and
  characters clear of its rays, reads its ground from one query of the broad phase in place of
  five rays down and one ahead, which gives the rays' answer to the bit. 2000 characters standing
  take 0.4 ms to plan in place of 1.3, and the step 1.8 ms in place of 2.5. Walking in a crowd
  that bumps, they take 1.4 ms in place of 1.8, since the ray ahead is still cast where another
  character is near, and its ray onto the step after a hit. Ground of a mesh, a turned box or a
  heightfield still takes the rays.

- **A build that compiles as it runs stalls on a thing's first use.** `games/Manor` streams its
  estate in as cells of prefabs, and its walk's worst frame offscreen was 47 ms, read with
  `profile.slowest`. Four causes were found and moved off the frame: a stage's batch of tiny systems
  waiting on the thread pool behind the loads (up to 47 ms), each model file read with Assimp on the
  main thread to look for clips (8 to 10 ms), each texture upload waiting for the frames in flight
  (20 ms for a cell's textures), and a probe's readback waiting for its whole frame (8 to 24 ms). A
  frame's texture uploads go to the queue in one submit, and a probe's faces are recorded one a
  frame. On 2026-10-06 the autopilot walked the route for a minute offscreen
  (`input.button 0 South 2`, then `manor.autopilot true`, `profile.slowest` read every ten seconds):
  the native build's worst frame was 18 ms, and the build `dotnet run` makes 57 ms, its slow frames
  holding 23 to 27 ms of the runtime compiling the code of a thing's first use, the first probe
  capture's and the first point shadows' among them. Natively the first frame shown takes 30 ms, and
  the first texture's memory, 11 ms, is taken before it. What is left belongs to the compiler a
  player's build does not have, and would go by packing the engine compiled ahead (ReadyToRun) for
  each platform beside its portable code, a copy of a few megabytes each, so a game run from its
  project compiles only its own code.

### The flat API

`Engine3D` covers the window, timing, input, the frame, 2D and 3D cameras, render targets, 2D and
3D shapes, 2D collision, images and textures, models and meshes, shaders, lights, states, scenes,
physics, text and fonts, audio, audio streams and waves, and files
([CHEATSHEET.md](../CHEATSHEET.md)). What is missing:

- **103 of raylib's 619 functions are not carried**, which `build/raylib-bench/coverage.py` names
  and [compared-with-raylib.md](../docs/compared-with-raylib.md) answers one by one. Most have their
  counterparts in C#, its strings, code points, files, directories, hashes, compression and freeing
  of memory, each there beside its counterpart. The rest are left out for a reason the page gives.
  Images have one level, so `ImageMipmaps` is left out. Shapes are drawn untextured and fonts keep
  their glyphs by code point in ImGui's atlas, so the shapes texture, `GetGlyphIndex`,
  `LoadFontData` and `GenImageFontAtlas` have no meaning. The vertex layout has no tangents, for
  `GenMeshTangents` and `GetShaderLocationAttrib`, and `UpdateSound` reaches into the audio thread,
  which the backend does not open to the program. VR stereo, the file callbacks (which the asset
  server's sources stand in for) and the exports as C code are left out too.

- **Models are partial.** Skinned meshes are posed on the GPU at a frame, between frames
  (`UpdateModelAnimationAt`), between two clips (`UpdateModelAnimationBlend`) or with a clip on
  part of the skeleton (`UpdateModelAnimationLayer`), with their morph targets moved by weights a
  clip, a clip on part of the skeleton or `SetModelMorphWeight` sets, and on the CPU in a run with
  no renderer. A mesh posed on the GPU keeps its vertices at rest on the CPU, where its wires are
  posed from the same joints, and a collider made from it is at rest. An entity plays a
  file's clips through `AnimatedModel`, which loads a file once and gives each entity a copy with
  skinned meshes of its own, and poses and draws it through the flat API, so only in the app
  `InitWindow` built, where a file with clips a level places through `ModelRef` plays its first on
  a loop through one.
- **Color emoji and distance fields past U+FFFF are not drawn.** A coverage font loaded from a
  file is baked again at a size it is drawn at a quarter or more past its own, eight sizes at
  most, and one loaded as `FontType.Sdf` stays sharp at any size. A font has Latin-1 or the
  characters it was asked for, those past U+FFFF drawn by the engine's own TrueType reader into
  the same atlas, since ImGui's names characters in 16 bits. A font of CFF outlines or color
  bitmaps (most color emoji) gives none past U+FFFF, and a distance field font none either.

### Meshes, materials and light

- **Vertex inputs are written by hand.** A dispatch runs a compute shader over storage buffers,
  which the CPU reads back and drawing shaders read, and textures it writes and samples, and every
  pass's descriptor set layouts are read from its shaders' reflection (RENDERING.md §1). The vertex
  inputs are still written beside each pipeline for the engine's fixed formats, and a render texture
  is written only where the GPU can store to the window's format.
- **One directional, ten spot and twelve point lights cast shadows.** The first directional light
  with `CastsShadows` set shadows what each view's camera sees within 150 units, or the distance
  `SetShadowDistance` sets, in three cascades, ten such spot lights shadow their cones in the map's
  last tile, and twelve such point lights shadow all around them (RENDERING.md §4), those the camera
  sees ranked first and then by the light that reaches the eye, its brightness over one plus the
  square of how far its reach is, the first two spots and four points with the most texels.
  `SetShadowMapSize` sets the tile from 256 to 4096 texels (2048 by default). An eleventh spot or a
  thirteenth point light casts none, the ranking does not weigh how much of the picture a light
  lights, and each render target that draws meshes draws the map again for its own camera, with the
  point and spot lights chosen for the window's.

- **Particles collide with nothing.** A `ParticleEmitter` gives off particles a compute shader
  steps, drawn as round dots or the program's texture facing the camera after the meshes, into the
  window, into each render texture meshes are drawn into and into a reflection probe's capture, lit
  or giving off their own light, with a rate, a burst, a life, a velocity in a cone, gravity, drag,
  a size and color that change over each life, and a sheet's frames played through, cut or blended
  (RENDERING.md §3). A render texture drawn only in 2D has no camera to draw them through, those
  laid over by alpha are sorted from the window's camera in a render texture too, and none collides
  with the world.

- **Effects over the frame are bloom, exposure fixed or following the scene, a curve, grading, a
  vignette, FXAA, depth of field and motion blur.** Any of them draws the window's scene into a
  half-float target and brings it into the window in one pass, after passes of their own for the
  depth of field and motion blur (RENDERING.md §5). With all of them off the tonemap still runs at
  the end of the model pass, render targets loaded without formats stay eight bits, a shader of the
  program's own inside `BeginMode3D` is read as linear in the HDR frame, and motion blur knows only
  the camera's movement and not a thing's own. Ambient occlusion darkens the window's light from all
  around, from a depth of the meshes that cast shadows drawn at half size, so a mesh that casts none
  closes nothing off, and render textures and probe captures are drawn without it.

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
and writes each body's `Transform` blended between its last two steps by `FixedTime.Alpha`. That
blend is the `Transform` game code reads too, so code that needs the simulation's own pose asks
`PhysicsWorld.GetPosition`. A body under a `Parent` is given the local `Transform` that puts it at
its pose under the parent as the parent is in that frame, so it can be grouped under a level's
entity and stays where the simulation has it, and a parent never carries it. Two bodies starting
and stopping touching (a hundredth of a unit apart or closer) are sent as `ContactStarted` and
`ContactEnded` events after each step and cleared at `Stage.First`, with the entities as they were
when the contact started. A resting pair whose bodies sleep stays touching.

The flat API creates boxes, spheres, capsules, static and kinematic boxes, triggers, which report
what enters them as contacts and stop nothing, level geometry shaped as a model's triangles, and
bodies shaped as a model's convex hull, read and drawn at the model's origin. It puts bodies on 32
layers whose pairs collide or not, joins bodies with ball, hinge, weld, distance and slider joints,
a hinge or a slider limited between two angles or distances or driven by a motor, reads their blended poses, turns and how fast a
point of them moves, pushes them at their center or at a point, casts rays and balls along them,
which go through triggers and may look past one body and the layers it does not collide with,
finds the bodies a sphere reaches, reads the frame's contacts with the point and normal where
each pair met and the speed they closed at, and how hard a touching pair presses (CHEATSHEET.md,
Physics). A `Collider` marked `IsTrigger` makes a trigger from a
scene, and its `Layer` puts the body on a layer. A body a game knows is fast is swept over each
step (`SetPhysicsBodyContinuous`, a `RigidBody`'s `Continuous`), which stops it at a wall of any
thickness up to 50 units a second and at thicker ones faster, the contact's spring at the stiffest
a step of a sixtieth solves. A kinematic body under a `Parent` follows its
place under the parent by velocity, so a platform a moving parent carries carries what stands on
it, a character walking relative to it and a crate by friction. A ball joint swings and twists
within a cone, and a distance joint keeps a range that can change. Two bodies a joint holds do not
collide with each other. The character controller is a dynamic capsule walked toward a velocity
before each step, which slides along walls, climbs steps up to its step height (its radius unless
set), holds slopes up to its limit, rides what moves under it, crouches and stands where there is
room, and reports ground. A vehicle is a box held up by raycast wheels as springs, gripping,
driving, braking and steering on the fixed step.

### Scenes

`SceneFile` saves a level of entities and their `[SceneComponent]` and behavior components to JSON
and loads it back (ARCHITECTURE.md, Scene files), a mesh entity made in code with its arrays and a
model through its `ModelRef`. A body is described by a `Collider` (box, sphere, capsule, the
meshes of the entity and those under it, or their convex hull) and a `RigidBody` (static, dynamic
with a mass, or kinematic), which a file holds, and `PhysicsBodies` makes it when the entity
appears, a character when a `CharacterController` is beside a capsule. A `Joint` on an entity of
its own joins two entities' bodies at its place, and a `PhysicsMaterial` beside a `Collider` gives
its body a friction and a bounce. A scene file placed in another with `SceneRef` is spawned when
the reference first appears, and again in place of that copy when the file is written while the
level runs. The models, textures and parsed scene files a level loads through its references are
let go some seconds after no entity uses them (`AssetRelease`), so a level streamed in as the
player nears holds what is near. `SceneLightPayload` and `Light` hold what the model pass reads,
and the model pass reads every field of `SceneMaterialPayload`. What is missing:

- **An older file is read by keeping the fields it has, with no migration.** A field renamed or a
  component split leaves the old file's value behind. BevyCSharp has files that outlive a renamed
  type, which SHARED.md keeps to consider, as the owner decided.

## Platform

### Input

Keyboard, mouse, typed text and gamepads come from SDL3 into the `Input` resource, and the flat API
hands out typed characters and pressed keys one at a time (`GetCharPressed`, `GetKeyPressed`). Text
input is started once on the window and never stopped. An ImGui text field being typed into places
the input method's composition window at its cursor, which is checked against what ImGui reports and
not with an input method running, and a text box a game draws itself places it with
`SetTextInputArea`. Typing from a real keyboard has only been checked through injected text. Fingers
are read as touch points (`GetTouchPosition`) and recognized as raylib's gestures (taps, holds,
drags, swipes and pinches), and a gamepad's gyro, accelerometer, touchpad and light are read and set
through SDL, which has been checked against the state it fills and not with a pad that has them.

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

- **CI draws on Windows and macOS for the tests and one game.** `.github/workflows/test.yml` runs
  the tests under the validation layer on Ubuntu and Windows with lavapipe and on macOS with
  MoltenVK, and builds Pusher from the package and draws 300 frames of it on Windows and macOS.
  `build.yml` captures every example and plays every game on Linux alone, so a game's input,
  sound and a window are not tried on the other two, and the Windows and macOS jobs had not run
  when they were written.
