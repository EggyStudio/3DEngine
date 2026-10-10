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
  before, so 400,000 standing still take 2.4 ms in place of 6.0. Every instance is still copied into
  the ring each frame, a block no pass draws with them, and one entity moving gathers its whole
  chunk again, and neither pays to change: `./e3d command models.blocks` finds no block left undrawn
  in `models_stress`, whose camera keeps the grid in view, in Manor's hall and grounds or in Summit,
  which put 172 and 11 instances in groups, a copy of a few microseconds; and with one entity moving
  (`E3D_STRESS_MOVING=1`, `E3D_STRESS_COUNT` holding the count, on 2026-10-09), `MeshEntityDraws`
  takes 1.850 ms at 410,266 against 1.852 with none moving and 5.87 with all, and 0.15 to 0.18 ms at
  a game's 3,000 whichever move. A frame holds about 243,000 sprites, each `DrawTexture` about 48
  nanoseconds with the example's loop, the upload 3.0 ms and the GPU 6.3 ms, so what is left is
  shared between the three.

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
  (`input.button 0 RightFaceDown 2`, then `manor.autopilot true`, `profile.slowest` read every ten seconds):
  the native build's worst frame was 18 ms, and the build `dotnet run` makes 57 ms, its slow frames
  holding 23 to 27 ms of the runtime compiling the code of a thing's first use, the first probe
  capture's and the first point shadows' among them. Natively the first frame shown takes 30 ms, and
  the first texture's memory, 11 ms, is taken before it. What is left belongs to the compiler a
  player's build does not have. Packing the engine compiled ahead (ReadyToRun) for each of its six
  systems was measured on 2026-10-06: the package 14.9 MB where it is 1.3 MB, and Manor run from
  its project on Linux ending its startup stage at 0.44 s where it does at 0.58 and its slowest of
  the first sixty frames 140 ms where it is 205. The package stays the smaller, since a game's
  author compiles the engine ahead with their own game by publishing it with `PublishReadyToRun`
  or NativeAOT, which gains the same however the package is made.

### The flat API

`Engine3D` covers the window, timing, input, the frame, 2D and 3D cameras, render targets, 2D and
3D shapes, 2D collision, images and textures, models and meshes, shaders, lights, states, scenes,
physics, text and fonts, audio, audio streams and waves, and files
([CHEATSHEET.md](../CHEATSHEET.md)). What is missing:

- **99 of raylib's 619 functions are not carried**, which `build/raylib-bench/coverage.py` names
  and [compared-with-raylib.md](../docs/compared-with-raylib.md) answers one by one. Most have their
  counterparts in C#, its strings, code points, files, directories, hashes, compression and freeing
  of memory, each there beside its counterpart. The rest are left out for a reason the page gives.
  Images have one level, so `ImageMipmaps` is left out. Shapes are drawn untextured and fonts keep
  their glyphs by code point in ImGui's atlas, so the shapes texture, `GetGlyphIndex`,
  `LoadFontData` and `GenImageFontAtlas` have no meaning. The vertex layout has no tangents, for
  `GenMeshTangents` and `GetShaderLocationAttrib`, and `UpdateSound` reaches into the audio thread,
  which the backend does not open to the program. The file callbacks (which the asset server's
  sources stand in for) and the exports as C code are left out too. VR stereo draws the shapes,
  lines, text and models of a `BeginMode3D` once for each eye, and the ECS's mesh entities and
  particles once, through the camera.

- **Models are partial.** Skinned meshes are posed on the GPU at a frame, between frames
  (`UpdateModelAnimationAt`), between two clips (`UpdateModelAnimationBlend`) or with a clip on part
  of the skeleton (`UpdateModelAnimationLayer`), with their morph targets moved by weights a clip, a
  clip on part of the skeleton or `SetModelMorphWeight` sets, and on the CPU in a run with no
  renderer. A mesh posed on the GPU keeps its vertices at rest on the CPU, and its wires and a body
  made from it (`CreatePhysicsStaticModel`, `CreatePhysicsConvexHull`) are posed from the same
  joints. An entity plays a file's clips through `AnimatedModel`, which loads a file once and gives
  each entity a copy with skinned meshes of its own, and poses and draws it through the flat API,
  made to run against the entity's app while it does, and a file with clips a level places through
  `ModelRef` plays its first on a loop through one. What is missing is the copy's meshes in the
  world, so an entity an `AnimatedModel` draws is given a capsule or a box, and a mesh or a hull
  collider on it is refused with a warning.
- **Text is shaped in emoji and Arabic, and ordered right to left.** A coverage font loaded from a file is baked again at a size it
  is drawn at a quarter or more past its own, eight sizes at most, and one loaded as `FontType.Sdf`
  stays sharp at any size, its characters past U+FFFF as their outlines. A font has Latin-1 or the
  characters it was asked for, those past U+FFFF drawn by the engine's own TrueType reader into the
  same atlas, since ImGui's names characters in 16 bits, and a color font's colored characters in
  their colors, from its bitmaps (CBDT or Apple's sbix), its layers (COLR version 0) or its paints
  (COLR version 1, `ColorPaint`, a variable font's at its default), in any plane. A sequence a font joins into one picture (a family, a flag, a skin tone, a keycap) is drawn
  as that picture, the font's `ccmp` substitutions applied to each run of the characters the reader
  draws (`GlyphSubstitution`). A line with a letter read right to left is drawn in the order it is
  read, by a reduced bidirectional algorithm with no explicit embeddings (`TextDirection`), and a
  run of Arabic is joined, each letter given its form by the letters beside it (`ArabicJoining`),
  through the font's GSUB under the `arab` script with the lookups' flags and the GDEF classes, or
  by the presentation forms the font maps, and its marks are put on their letters, its pairs
  kerned and each letter joined to the next by its exit and that one's entry, as Nastaliq is
  written, by the font's GPOS (`GlyphPositioning`). A letter and a mark after it are drawn as the
  one character Unicode has for both where the font has it (`UnicodeCompositions`, which
  `build/make-compositions.py` writes). The rest of the text is drawn a character at a time, so a
  Latin font's ligatures and kerning and the shaping Devanagari needs are not made.

### Meshes, materials and light

- **A compute shader writes buffers, textures and render textures on every device.** A dispatch
  runs a compute shader over storage buffers, which the CPU reads back and drawing shaders read, and
  textures it writes and samples, a render texture's colors through a stand-in copied into them
  where the GPU cannot store to their format, every pass's descriptor set layouts are read from its
  shaders' reflection, and a vertex stage of a program's own is fed each input by its semantic, in
  whatever order it declares them (RENDERING.md §1).
- **One directional, ten spot and twelve point lights cast shadows.** The first directional light
  with `CastsShadows` set shadows what each view's camera sees within 150 units, or the distance
  `SetShadowDistance` sets, in three cascades, ten such spot lights shadow their cones in the map's
  last tile, and twelve such point lights shadow all around them (RENDERING.md §4), those the camera
  sees ranked first and then by the light that reaches the eye, its brightness over one plus the
  square of how far its reach is, weighed by the share of the picture its reach covers, the first
  two spots and four points with the most texels. `SetShadowMapSize` sets the tile from 256 to 4096
  texels (2048 by default). An eleventh spot or a thirteenth point light casts none, views drawing
  meshes through cameras near enough share one set of cascades drawn once, and the point and spot
  lights are chosen for every view the frame draws meshes through, a light any camera sees first.

- **Light bounces and glossy surfaces reflect.** The scene's distance field (`SetSceneField`), the
  light that bounces through it (`SetGlobalIllumination`), Radiance Cascades of world probes with
  probes on the screen for the near light, glossy reflections traced through the window's depth and
  the field, and at `High` the GPU's own rays for what the field misses are built (RENDERING.md §4).
  A device that draws on its CPU traces no rays, since lavapipe crashes in a fragment shader that
  reads a uniform buffer in a loop after a ray query, on Mesa 25.2, 26.2 and main, whose
  reproduction and the text of an issue for Mesa's tracker are in `build/mesa/ray-query-fragment`,
  so CI tests the ray-query path nowhere and the laptop's RTX 4070 alone draws it. The bounce
  itself traces through the field alone, with no ray query. Lavapipe also crashes, reading a null
  pointer the same way, where `directLight`'s loop over the lamps stands ahead of its sun's branch,
  in the model pass and in the bounce's compute passes alike, a uniform buffer read in a loop in a
  branch some invocations skip, very likely the same fault, whose reproduction and issue text are
  in `build/mesa/uniform-loop-compute`, so that code is kept in the shape lavapipe draws. In the bounce a point or spot light that casts shadows lights a surface only
  where the field lets it through, a probe's ray that meets a surface before its interval is
  blocked, and the world's probes take bounced light from the probes they see, so a closed room is
  dark to a lamp outside it, and a reflection is lit by those lamps the same way, or through the
  GPU's rays at `High`. The screen's probes blend every probe around what they meet, a skinned or
  moving mesh is boxes of its color,
  of its joints or of its parts, that give off none of its light, and the screen's probes, blended
  with the frame before's, hold that within the spread of this frame's light, so they follow a
  changing light within the frame. A render texture that draws meshes through a camera has screen
  probes of its own, as the window's, in the same frame, probe captures take the bounce from the
  world's probes alone, and where the window draws no mesh the field follows the first render
  texture's camera. Against path-traced references of `shaders_bounce_rooms`' eight views, held by
  `BounceRoomsTests` where the GPU traces rays, a view's pixels differ from its reference by 0.02
  of its light among the red walls to 0.5 in the strip's room at each quality. What is left
  (RENDERING.md §4): a corridor narrower than the second cascade's probe spacing, whose probes
  stand in its walls, carries no light along it from past the first cascade's reach, 21% under;
  a small bright strip's room reads a quarter under, its light within a few of a probe's
  directions; the Cornell box reads 9% over, which the loop's gain, its walls' share, does not
  account for; and light that newly comes takes some ten frames to build its bounces, one a frame.

- **Particles meet the meshes that cast shadows, and nothing else.** A `ParticleEmitter` gives off
  particles a compute shader steps, drawn as round dots or the program's texture facing the camera
  after the meshes, into the window, into each render texture meshes are drawn into and into a
  reflection probe's capture, lit or giving off their own light, with a rate, a burst, a life, a
  velocity in a cone, gravity, drag, a size and color that change over each life, a sheet's frames
  played through, cut or blended, and bouncing off or ending at the scene's distance field where it
  is built and holds the particle, and at the window's depth of the meshes that cast shadows
  elsewhere (RENDERING.md §3 and §4). A render texture drawn only in 2D draws none, since it is most
  often an interface that would show the window's particles a second time with no depth to hide
  them. Without the field a particle passes through what the window does not show, off screen or
  behind something, and it passes through shapes drawn without a model. Colliding with the physics
  world stays a limit, since raylib has no particles and a game that needs that much has bodies.

- **Effects over the frame are bloom, exposure fixed or following the scene, a curve, the engine's
  or one of Bevy's eight, grading, a vignette, FXAA, depth of field and motion blur.** The window's
  scene is drawn into a half-float target every frame it shows one and brought into the window in
  one pass that tonemaps it, after passes of their own for the depth of field and motion blur
  (RENDERING.md §5), so a shader of the program's own inside `BeginMode3D` reads the same with every
  effect on or off. Render targets loaded without formats stay eight bits with the curve at the end
  of the model pass. The frame's target takes 44 bytes a pixel at four samples, 91 MB at 1920 by
  1080, drawing into the window's own multisampled depth. Motion blur blurs a mesh entity by its own
  movement where asked, a model drawn with `DrawModel` and a skinned mesh's limbs by the camera's
  alone. Ambient occlusion darkens the light from all around of the window and of each render
  texture drawn through a camera, from a depth of the meshes that cast shadows drawn at half the
  view's size and from the scene's distance field where it is built, so a mesh that casts none
  closes nothing off, and probe captures are drawn without it.

- **Light scatters under a surface in the window and comes through its thin parts.** A
  material's `SubsurfaceRadius` and `SubsurfaceColor` spread the diffuse light of the window's
  opaque and masked surfaces drawn with the model pass's own shader across and then down the
  decoded frame, seventeen taps each way, a pixel of another surface never touched and the spread
  stopped at a depth edge, and a thin part lit from behind shows the light that comes through it,
  its thickness toward a lamp read from the scene's distance field and toward the sun from the
  field or its shadow map (RENDERING.md §5). What is missing is tiers of the taps and a half-size
  pass for a slow GPU; a material's own thickness for a part the field cannot measure, as a leaf
  drawn as one sheet, read from a glTF file where it has `KHR_materials_volume`; the light through
  a lamp's shadow where there is no field; and the spread in render textures and probe captures.

### The device

Passes are drawn by dynamic rendering and barriers are synchronization2's, on Vulkan 1.3. Buffers and
textures are carved out of blocks of 64 MiB a memory type, ten thousand buffers and three thousand
textures in a handful of allocations, and render targets, cube maps and the frame's images keep an
allocation each.

## Interface

### Dear ImGui

ImGui is drawn by `ImGuiRenderNode` into the main pass. Its frame starts in `First`, after the
frame's time and the commands `./e3d` serves there, and before a program's own systems in it, so a
system in any stage draws into it. Docking is enabled (`gui_imgui_window` makes a dock space over
the window), and a window dragged onto a dock target and held there docks, as `./e3d command
input.move 100 67` and then `./e3d command input.drag Left 300 158 20 20` show on that example, the
Cube window's title carried to the middle of the window and held there. A window no one sees, as
`./e3d`'s and the tests', neither reads nor writes `imgui.ini`, so each such run starts from the
program's own layout. Viewports, which take an ImGui window dragged outside the game's window into a
window of its own (`SdlImGuiViewports`), are offered on X11, Windows, macOS and SDL's offscreen
driver, and stay off until a program sets `ImGuiConfigFlags.ViewportsEnable`. Each such window has
an SDL window and a swapchain drawn after the main window's pass and presented with it, and
`imgui.viewports` and `imgui.shot` show them from `./e3d`. Wayland lets no program place a window,
so there ImGui keeps every window inside. Keyboard navigation is on, which makes
`WantCaptureKeyboard` true whenever an ImGui window has focus, so the engine's own shortcuts ask
`WantTextInput` instead.

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
  type, which SHARED.md keeps to consider.

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

- **Twenty-nine scenes are compared whole.** `OffscreenRenderTests` draws each pass offscreen (shapes,
  text, render targets, immediate and model shaders, lit models and ImGui) and reads chosen pixels
  back, and `ReferenceFrameTests` compares whole frames with the references beside it, allowing 2
  percent of the pixels to differ, which a missing shadow exceeds at 4. They are 2D shapes and
  text, a lit and shadowed scene, a render texture, an ImGui window, materials with maps beside a
  model shader, a skinned model posed mid-clip, point and spot shadows, an environment map with
  its sky, bloom, ambient occlusion, motion blur, the other effects over the frame together,
  particles, a reflection probe, a dozen shadowed lights, a morph target beside a clip on part of a
  skeleton, text in a font from a file, color emoji text from paints, from bitmaps joined into one
  glyph and from layers, text read right to left, Arabic joined and with its marks on its letters,
  a texture a compute shader wrote, the scene's distance field over a scene and lighting it, the
  Cornell box, Manor's library and the hall of reflections lit by light that bounces, a frame of
  Summit's level and a room of Wick's house. Audio and input have no frame to compare and are
  tested by their values.

### Prose

The code carried over from the module repositories predates [STYLE.md](STYLE.md). Its dashes,
spaced hyphens, padded banners and British spellings are gone, and so are its claims of readers and
modules that never came (USD, MaterialX, a web view, an editor). The colons that joined clauses in its comments are
rewritten, so the colon check reports lists and labels, and some comments still restate the line
below them. Each file is to be brought under the style guide when it is next changed, and the
checks at the end of STYLE.md report what is left.

### Build and release

- **CI plays the games on Windows and macOS offscreen, and one in a window.**
  `.github/workflows/test.yml` runs the tests under the validation layer on Ubuntu and Windows with
  lavapipe and on macOS with MoltenVK, and on Windows and macOS builds Pusher from the package and
  draws 300 frames of it, built and published native, then plays every game through `./e3d`
  (`build/drive-game.sh`), each asserting its walk or its win, Pusher once more in a window on the
  runner's desktop, and follow the README and the first game's steps in new projects. Every example
  is captured on Linux for the gallery and on macOS, in a job of its own beside the tests, for its
  artifact, each capture held to five minutes and the whole to a budget whose end names the last
  example reached.
