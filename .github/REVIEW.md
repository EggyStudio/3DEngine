# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `e5812611`. The random values come from raylib's own generator, xoshiro128** started
from the seed by SplitMix64 as `rprand.h` has it, so `SetRandomSeed` gives raylib's numbers, held by
a test against values a C program printed from the pinned header, and `InitWindow` seeds from the
clock or from `--seed` or `E3D_SEED`. `compare.py` gives both programs of a pair a sixtieth of a
second a frame and the same seed, raylib's through the shim, and captures this engine's program with
no input, as raylib's gets none. The shapes past 2% are 12 of 45, from 18: `shapes_top_down_lights`
0.6% from 26.9%, and eight of the twelve are the font and raygui's panel drawn as ImGui with their
shapes matching raylib's, one adds the wall clock, and three at four samples resolve their lines
otherwise, a one-pixel line covering half of each of its two rows here and a quarter in raylib's
frame, which is the driver's (`e5812611`). The core module is under way, with the back faces of 3D
shapes culled by default as raylib's are, decided here on 2026-10-06, 2D shapes drawn on both faces
as the page says, and models left until their module is measured. No verdict is open.

Before them, each written example was measured against raylib's own program drawn to the same frame:
`build/raylib-bench/compare.py` builds raylib's example with `shim.c` wrapped around `EndDrawing`,
which takes raylib's screenshot at the frame named, builds the examples `./e3d open` starts, takes
the engine's picture as `capture-example.sh` does, compares the pair as the reference frames are
compared, and writes the share into `3DEngine.Examples/measured.tsv` and the table's `Apart` column
(`de6038fa`). The 45 shapes examples are measured, and the first faults are mended: thick outlines
laid their pieces over each other where raylib's meet, so eight outline calls are bands that meet,
lie outside the edge for a negative width as raylib's do, and draw circles in raylib's 36 pieces,
held by `OutlineTests` sampling a grid a quarter of a pixel apart (`fe255b28`). 18 of the 45 are
past 2%, from 21, and in four of them the shapes match raylib's pixel for pixel, the share being the
font and raygui's panel drawn as ImGui, which the page keeps. Item 6 was a fault: a crate Bepu had
marked to sleep slept through 3 a second given by any of four calls, and every wake clears the
candidate flag and the count (`f2d3bcf4`), which settles it. Whether `GetFontDefault` returns
raylib's own pixel font is the owner's, put to them.

Before them, a game's own text box places the input method's window beside it with
`SetTextInputArea`, as an ImGui field does, read back from SDL on `text_input_box` run hidden
(`134d4f3d`), RENDERING.md's order of work names what is built (`fd9af099`), and the README's status
points at the comparison page for the functions left out (`16bd04ef`). BevyCSharp's kinematic batch
found two things in Bepu that this engine has the same code for. A convex manifold's friction is
shared among its contacts, so a box on four corners slid a quarter as rough, which `ed0f3aa6` mended
here by scaling the coefficient by their count. And a body that has rested long enough to be a
candidate for sleep is put to sleep at the next step's start though `SetLinearVelocity` or
`ApplyImpulse` gave it speed, since `Awake = true` on an awake body clears nothing, which item 6
checks here. No verdict is open.

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

**Now 5, core measured, and lines and ties drawn as OpenGL draws them.** A line of one pixel at a
whole coordinate lay a row above raylib's, since OpenGL counts rows up the screen and Vulkan down,
so the two break a tie between rows the other way round, and the same tie filled a shape's upper
row of pixel middles where raylib fills its lower. The immediate pass moves every untextured batch
a 256th of a pixel down after its transform, which breaks each tie as raylib's does, a textured
one left where it is since a filtered texture would take a trace of the next row. A line at one
sample is drawn by the diamond rule of the line rasterization extension's Bresenham mode where the
device has it, as OpenGL draws raylib's, and with several samples stays the driver's, whose samples
smooth it, the reference frames unchanged. A test holds both ties, failing without the move, and
the render tests pass on lavapipe under the validation layer.

The 45 core examples are measured: 25 within 2% and 20 past it. `core_undo_redo` and
`core_2d_camera_mouse_zoom` went under 2% with the move, from 6.8% and 5.6%, and
`core_highdpi_testbed` to 3.6% from 8.4%. `core_3d_camera_split_screen`, at 99.4%, draws each
player's cube at the player's own camera, which raylib's culling hides from inside, and is the next
batch, 3D shapes culled by default as the word above has it. `core_smooth_pixelperfect` is a target
scaled up with the bilinear filter the page keeps, `core_directory_files` lists another working
directory and `core_highdpi_demo` another monitor, and the rest are the font and raygui's panel.
In shapes `shapes_rectangle_advanced` is 0.0% apart and `shapes_basic_shapes` 0.7%.
The suite: 1,379 passed, 0 failed, 1 skipped.

**Now 5, 3D shapes culled as rlgl culls them.** As the word on the culling question has it, a shape
drawn inside `BeginMode3D` leaves out its back faces until a program switches rlgl's culling, so a
cube drawn around the camera is hollow seen from within, while 2D shapes and text draw both faces
and a model the faces its material says. A test draws a cube at the camera's place, finds it
hollow and a clockwise 2D triangle drawn, and the cube filled once `rlDisableBackfaceCulling` is
called. `core_3d_camera_split_screen` is 8.8% apart from 99.4%, its scene matching raylib's, and
the rest is a bar partly clear drawn into a render texture, whose alpha raylib blends by the
color's factors so the black behind shows through, where alpha here is laid over by alpha and the
texture stays opaque. That is a new line on the comparison page with its reason, and the culling
line there says the three cases. The suite: 1,380 passed, 0 failed, 1 skipped, and the render
tests pass on lavapipe under the validation layer.

**Now 5, textures measured.** The 33 textures examples are 26 within 2% and 7 past it.
`textures_image_drawing` was a program of this engine's own under raylib's name and is now raylib's,
the cat drawn into the parrots and the font's text over them, 0.9% apart from 60.9%.
`textures_bunnymark` was the benchmark and is now raylib's program, 0.7% apart, the benchmark kept
behind `--stress`, which `run.sh` passes. `UpdateCamera`'s orbit reads the frame's time inside
raylib's own file, where the linker's wrap does not reach, so the shim turns it by the frame's
time the example reads, and `textures_framebuffer_rendering` is 1.8% apart from 5.4%. Of the 7,
`textures_image_rotate` is the black corners of an image without alpha, `textures_image_channel` and
`textures_portal_window` are textures drawn scaled with the bilinear filter, all three lines the
page keeps, `textures_image_text` is a TrueType font rasterized otherwise, and the rest are the
font. The suite: 1,380 passed, 0 failed, 1 skipped.
