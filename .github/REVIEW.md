# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `0f26acf3`. The 36 shaders examples are 25 within 2%. `shaders_mesh_instancing` and
`shaders_postprocessing` were programs of this engine's own under raylib's names and are raylib's,
0.2% and 3.1% apart from 99.8% and 72.2%, the second through raylib's twelve post shaders written in
Slang, and the engine's own keep names of their own, which the shaders guide quotes. Two faults more
are mended with tests: `GenMeshCube` made its faces in another order with each texture upright where
raylib lays an image's first row along a face's lower edge, held against a C program's print, and a
mesh under a node scaled more one way than another had its normals turned by the node's matrix
rather than its inverse turned over. raylib leaving such normals one over the scale long, so a cel
outline is thicker there, is a line of the page, and the `materials_and_shader` reference is drawn
again for the cube's texture (`0f26acf3`). The meshes par_shapes makes for raylib are the next
batch. No verdict is open.

Before them, `MeasureTextEx` counted a spacing after every character where raylib counts one fewer,
so centered text sat half a spacing left, and measures as raylib's does, `text_font_sdf` and
`text_input_box` being raylib's own programs from here on; the 16 text examples are 8 within 2%, the
rest the font, Latin-1 where raylib loads ASCII, or a TrueType font rasterized otherwise
(`6fa69925`). A model drawn before a program makes a light is unlit as raylib's is, texture times
color with its emission, decided here on 2026-10-06, the first light turning lighting on as before,
six of the engine's own examples making a sun and a fill, and three reference frames redrawn with
the reason. Measuring found two faults more: Assimp's gray default material of 0.6 on a file naming
none, where raylib's is white, so a texture on it showed at six tenths, and `DrawPoint3D` drawn as a
cross where raylib draws a short line along z, both mended with tests. `models_loading` is raylib's
castle in place of a torus of this engine's own, and the 31 models examples are 24 within 2% from
12, `models_basic_voxel` from 65.9% among them (`5b03dfe1`). No verdict is open.

Before them, a line at one sample was drawn by OpenGL's diamond rule, through the line rasterization
extension's Bresenham mode where the device has it, and every untextured batch moves a 256th of a
pixel down to break a tie between rows as raylib's GL does, since GL counts rows up the screen and
Vulkan down, a test holding both ties and the reference frames unchanged; the 45 core examples are
25 within 2% (`2b3c23a7`). A shape drawn in 3D leaves out its back faces until a program switches
rlgl's culling, as decided, 2D shapes and text drawing both faces and a model the faces its material
says, with a test, so `core_3d_camera_split_screen` is 8.8% apart from 99.4%, the rest a render
texture's alpha that raylib blends by the color's factors, a line of the page with its reason
(`9727caac`). The 33 textures examples are 26 within 2%, `textures_image_drawing` and
`textures_bunnymark` being raylib's own programs from here on, the benchmark kept behind `--stress`,
and the shim turns `UpdateCamera`'s orbit by the frame's time, which the linker's wrap does not
reach inside raylib's own file (`748c5abe`). No verdict is open.

The norm has 43 rules, and this engine stands at 31 checked, 3 with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What the next page says.** The run after `fb68cfad` is pushed shows whether macOS passes
   `AppLeakTests`, which the reviewing session reads and says here. The ports go on meanwhile.
2. **raylib's own examples, one by one, as the measure** (N 5.2). `coverage.py` counts raylib's
   functions, 491 of 619 carried, and nothing counts its examples, of which 45 programs here
   carry a few. BevyCSharp holds itself to Bevy's 421 examples in a table a script writes from
   Bevy's own list, and writing them one by one found faults no test had. The same here: a table
   of every example in the `examples/` folder of the raylib checkout `build/raylib-bench/run.sh`
   pins, made by a script, each row saying whether it is written, written in part, can be written
   with what the flat API has, is missing something, or does not apply, with the count at its
   head. An example written keeps raylib's name, its window of 800 by 450 and its scene, is
   opened by name and captured as the others are, and its picture is set beside the screenshot
   raylib keeps next to each example's source. A function it calls that the flat API lacks is
   carried, or its row says why not, which is TODO.md's entry on the 128 functions taken from
   the side a program meets them. A picture that differs from raylib's for no known reason is
   taken down to the smallest program that still differs and explained before the pass goes
   on. Many a batch, a module at a time, and it is the item to come back to whenever the others
   are through.

   What C# has of its own, strings, files and memory, covers raylib's helpers for them, so their
   examples can be written. rlgl's matrix stack and its vertices one at a time are missing and
   not out of reach, since a raylib program turns a drawn shape with the one and draws a shape
   of its own with the other. Once the rows that can be written are, the missing are taken by
   how many rows each holds, as BevyCSharp takes its gaps.

   Two things go with the ports. A program of this engine's own that answers a raylib example
   under another name takes raylib's name once it is read against raylib's source (N 5.1), as
   `shapes_basic_2d` may be `shapes_basic_shapes`, `shapes_basic_3d` `models_geometric_shapes`,
   `audio_sound` `audio_sound_loading` and `models_terrain` `models_heightmap_rendering`. And a
   call that answers otherwise than raylib's of the same name, where the difference is kept, is
   a line on `docs/compared-with-raylib.md`, in a table of its own a port adds to, the first
   being a trigger's axis, from 0 at rest here and from -1 in raylib, which docs/input.md says
   and the comparison does not.
3. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the seven has.
4. **The norm's lists are paid down.** A listed file is mended when a batch next touches it, in a
   commit of its own that moves code alone, the largest first where there is a choice, and a batch
   reads the lists for the files it will touch before it starts, as BevyCSharp's list has it.
   N 1.5's three rows, `docs`, `games` and `templates`, are the change to AGENTS.md that N 7.4
   allows, and they wait for the owner's word in the working session, which is asked for. The
   reviewing session does not stand in for it.
5. **Every picture measured against raylib's own program** (N 5.2). The table sets each example's
   capture beside raylib's screenshot, read by eye, and `692cefee` built raylib's deferred program
   here to compare the same frame, which is the measure item 2 asks for and the 216 written have not
   had. A module at a time: raylib's examples built from the checkout `run.sh` pins, each run to the
   frame the capture here is taken at, with a shim around `EndDrawing` that takes the screenshot and
   closes, and each pair compared as the reference tests compare their frames, by the share of
   pixels that differ past the tolerance. A pair that differs is taken down to the smallest program
   that still differs, as item 2 has it, and ends as a fault mended or as a line of the comparison
   page where the difference is kept, a trigger's axis being the first. The share each pair differs
   by is written by the script into the table, so the number is measured again on each run.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 23 are settled, and their numbers are not given again.

None open.

## Decisions

1. **Commits stay local.** The owner pushes `main` from their own tools, and the working session
   commits and does not push, as AGENTS.md and COMMITS.md say.
2. **The package is `3DEngine` on nuget.org, the owner's own, numbered from 5.0.** The owner
   sets the major and minor in `build/version.txt`, the patch counts the commits since, and the
   `pack` workflow run by hand makes the package to download or push.

3. **AGENTS.md's bullet and table row on SHARED.md are the owner's.** They approved them on
   2026-10-04, and they are committed like any other change.

4. **TODO.md's two Entities entries are closed as decided**, which answers the question asked
   under Replies. Ids from a query are the frame's own, and a program that keeps an entity
   across frames takes its handle with `ecs.Handle(id)`, which is one call where it matters and
   costs nothing where it does not. Query rows carrying a handle beside the id were considered
   and rejected, since every query would pay for what few keep. A write through a store's raw
   array going unseen is the price of generated code reaching the array, a system's first run
   seeing every earlier stamp is Bevy's rule and is kept so the two engines agree, and removals
   kept 60 frames bound the memory. DESIGN.md says each of these where it describes the ECS, and
   the two entries leave TODO.md.
5. **A commit that takes something out of the public surface is the owner's to number.** The
   patch counts commits and says nothing of what broke. When `PublicApi.txt` loses or reshapes a
   line, the working session says so under Replies, and the owner raises the minor or the major
   in `build/version.txt` before the next package. On 2026-10-05 the owner chose 5.1 for what
   has changed since 5.0.12 and set it in `aae58f45`. A minor may break what a game calls until
   the table of raylib's examples is mostly written, and 6.0 is the surface promised after
   that, which BUILDING.md says where it says how a package is numbered, in the next batch that
   touches it.
6. **AGENTS.md's bullet and table row on NORM.md are the owner's, with the exception N 7.4
   makes.** They approved both on 2026-10-05 in the reviewing session, with the plan for the
   norm. A working session that commits a change to its instruction file only on the owner's
   word in its own session is right to, and waits for that word.
7. **A run that fails says what failed in a page, and the reviewing session is given no log.**
   The owner chose it on 2026-10-05, after a log pasted into the reviewing session ended it.
   The suite runs whole as it does, and in parts only after a process is lost, which the owner
   chose over parts on every run. A test in which the engine logs an error fails unless it says
   it expects that error, with a list of the tests that log one today. The norm has these as
   N 6.7, N 6.8 and N 3.7, and the reviewing session reads a run's jobs and annotations from
   GitHub.

## Replies

**Now 5, the rounded meshes made as raylib makes them.** `GenMeshSphere`, `GenMeshHemiSphere`,
`GenMeshCylinder`, `GenMeshCone`, `GenMeshTorus` and `GenMeshKnot` were the engine's own lathes and
tubes, laid out otherwise than the par_shapes surfaces raylib makes them from, so a texture wrapped
them otherwise. `ParShape` carries as much of par_shapes as raylib uses: a surface over a grid with
the grid's texture coordinate at each point, normals averaged across seams by par_shapes' weld,
its scale, turn, move and merge, and its disk, whose turn about an axis of no length raylib's caps
depend on. Every corner of the six matches the meshes raylib makes, position, normal and texture
coordinate, within a hundred thousandth, and a test holds every 37th of them against a C program's
print. The sphere's poles are on z as raylib's are, the hemisphere is open below as raylib's is,
and a torus' or a knot's `radSeg` counts the pieces around its tube, as raylib's does, which
`shaders_model`'s knot now asks as such. `shaders_simple_mask` is 0.9% apart from 3.0%,
`models_rotating_cube` 0.2% from 1.4% and `shaders_fog_rendering` 1.0% from 1.8%. The suite: 1,389
passed, 0 failed, 1 skipped, and the render and model tests pass on lavapipe under the validation
layer. Audio is the module left.

**Now 5, audio measured.** Ten of raylib's eleven audio programs are written, `audio_module_playing`
waiting on a decoder for XM and MOD, and five are within 2% of raylib's picture. The fault found was
that `SetSoundPan`, `SetMusicPan` and `SetAudioStreamPan` took 0 to 1 with the middle at 0.5, where
raylib's take -1 to 1 with the middle at 0, so `audio_music_stream` and `audio_sound_positioning`,
which pass raylib's values, played raylib's middle and everything left of it at the far left, and
the right half spread across both sides. They take raylib's range now, held at either side as raylib
holds it, and the law between the sides was already the equal power raylib's cubic comes close to.
`audio_raw_stream` was a program of this engine's own under raylib's name and is now a port of
raylib's, its buffer filled whenever `IsAudioStreamProcessed` says so, with the arrows for frequency
and pan. What is left apart is the font, raygui in `audio_amp_envelope` (14.3%), and in
`audio_raw_stream` (6.0%) and `audio_mixed_processor` (2.4%) a picture drawn from when the device
last asked for samples, which is the clock on both sides and moves from run to run. The suite: 1,390
passed, 0 failed, 1 skipped.

**Now 5, the sieve and the bloom grid explained.** Each ends as a line of the comparison page, since
both come from how a driver rounds. `shaders_eratosthenes_sieve` (4.3%) differs only below its
quad's diagonal, on 44 of the 50 rows of pixels where its coordinate times 1000 is exactly whole,
row 22's being 950. raylib's floors it to 949, because OpenGL's interpolation comes out a hair under
in that triangle, where Vulkan's here lands on it in both. Turning the quad's triangles so each
starts from the vertex OpenGL's would start from changed nothing, so it is not the provoking vertex.
`shaders_lights_bloom` (3.2%) draws `DrawGrid` in its floor's plane, and raylib's lines across the
screen are dashed where OpenGL's depth for a line comes out behind the floor's, where ours draw
whole at the same one sample and the same `LessOrEqual`. The rest of it is the font. Every example
written now has its number in the table.
