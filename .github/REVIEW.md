# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `9ddd9f02`. The six rounded meshes are par_shapes' as raylib makes them, every corner
matching raylib's within a hundred thousandth and every 37th held against a C program's print, the
sphere's poles on z and the hemisphere open below as raylib's are (`610d6b74`). Sound, music and
stream pans take raylib's range of -1 to 1 with the middle at 0 where they took 0 to 1, checked here
against the pinned `raylib.h`, since raylib's own range changed from 0 to 1 after its 5.5;
`audio_raw_stream` is a port of raylib's program, and ten of the eleven audio programs are measured,
five within 2%, `audio_module_playing` waiting on the owner's word on a decoder (`47351deb`). The
sieve's coordinate and the bloom example's grid end as lines of the page, each a driver's rounding,
and every example written has its number in the table (`9ddd9f02`). With that, the seven modules are
measured once over, and the font is the largest part of nearly every share left. No verdict is open.

Before them, the 36 shaders examples were measured, 25 within 2%. `shaders_mesh_instancing` and
`shaders_postprocessing` were programs of this engine's own under raylib's names and are raylib's,
0.2% and 3.1% apart from 99.8% and 72.2%, the second through raylib's twelve post shaders written in
Slang, and the engine's own keep names of their own, which the shaders guide quotes. Two faults more
are mended with tests: `GenMeshCube` made its faces in another order with each texture upright where
raylib lays an image's first row along a face's lower edge, held against a C program's print, and a
mesh under a node scaled more one way than another had its normals turned by the node's matrix
rather than its inverse turned over. raylib leaving such normals one over the scale long, so a cel
outline is thicker there, is a line of the page, and the `materials_and_shader` reference is drawn
again for the cube's texture (`0f26acf3`).

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

**Now 2, automation events carried.** With every row that can be written written, the five missing
hold one example each, and the eight automation functions were the largest gap that needed no
decision. `LoadAutomationEventList`, `UnloadAutomationEventList`, `ExportAutomationEventList`,
`SetAutomationEventList`, `SetAutomationEventBaseFrame`, `StartAutomationEventRecording`,
`StopAutomationEventRecording` and `PlayAutomationEvent` do what raylib's do. Each frame's input is
recorded as `EndDrawing` begins, a held key an event in every frame and another when it comes up,
the pointer, wheel, fingers and a pad's axes when they move, into raylib's text format, and playing
an event sets the input it records for the frame it is played in, ImGui's included. Keys are written
by the engine's own codes, which the comparison page says, so a file of raylib's plays its frames
and types here and not its keys. `core_automation_events` is written, 1.0% apart from raylib's
picture, and a run recorded with S and played with A through `./e3d` in a hidden window moves the
player as recorded. 514 of 619 functions are carried, and the suite: 1,396 passed, 0 failed, 1
skipped.

**A question on `LoadImageFromScreen`**, which `core_screen_recording` needs beside a GIF writer of
its own, as raylib's example includes msf_gif. A call made in the loop comes after the last frame
was presented, and a presented swapchain image cannot be read, so the frame has to be copied before
it is presented. Keeping a copy every frame makes every program pay for what few read, which
DESIGN.md §5's "every query would pay for what few keep" argues against, and keeping copies from the
first call on leaves the first call with nothing to return. I can take either, or leave it not
carried as it is. The remaining three are VR stereo, cubemap textures for `models_skybox_rendering`,
and an XM and MOD decoder, which DESIGN.md §8 would have to allow as a dependency or the engine
write.

**Now 2, `LoadImageFromScreen` carried as decided.** Each frame is copied as it is presented from
the first call on, so a program that never reads the screen pays nothing, and until a frame is kept
the call gives the window's size in the last clear color, which the cheatsheet says. One part is
otherwise than asked. A call inside a frame reads the frame before as well, because nothing of a
frame is on the GPU before `EndDrawing`, the draw lists being rendered in `Stage.Last` and cleared
as they are drawn, so the frame as drawn so far would mean running the render graph twice in a frame
into a target of its own. The page keeps the difference with that reason. A test on the GPU reads
the first call as the clear color, the second as the frame drawn, and a call inside the next frame
as the frame before, and the render tests pass on lavapipe under the validation layer.
`core_screen_recording` is written, 0.2% apart from raylib's picture, with a GIF writer of the
example's own in the place of the msf_gif.h raylib's includes, and a recording made through `./e3d`
with Ctrl and R decodes as 19 frames of the scene. 515 of 619 functions are carried, and the suite:
1,397 passed, 0 failed, 1 skipped.
