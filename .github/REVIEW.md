# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `7b9b2f2e`. The table of raylib's examples (`c05bd485`) and the first port with what
it found (`7b9b2f2e`) are settled on their replies, which were read. The table is written from
raylib's own list at the commit pinned, 222 rows, and the workflow holds it to its script, which
was the last rule here with no check (N 5.2). Every rule of the norm a machine can check has its
check in this engine: 24 checked, 3 with places listed and 9 by review. The port of
`core_input_gamepad` found a function answering otherwise than raylib's of the same name, which
is what the ports are for, and raylib's files are fetched and credited and not kept. Verdict 10
is about how the table reads a row.

The owner pushed `main` up to `7b9b2f2e` on 2026-10-05 and brought back its run. On macOS the
tests ran to the end and passed, in 3 minutes 12, the first time the suite has drawn through
MoltenVK anywhere, and the game after them failed at the pack, which is Verdict 11. On Linux the
test step was ended from outside after 1 minute 36 with no test failed, which is Verdict 12.
Both come before everything else. `Config.FrameSeconds`, `Time.FrameSeconds`,
`PhysicsSettings.PlaceBeyond` and `PhysicsWorld.MarkPlaced` are new public lines and
`PhysicsSettings.MaxStepsPerFrame` is gone, which Decision 5 leaves with the owner.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What the run of `7b9b2f2e` says**, which is Verdicts 11 and 12, in that order, the first
   being a line and the second a search.
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
   how many rows each holds, as BevyCSharp takes its gaps. Verdict 10 comes before the next
   port, and Verdicts 11 and 12 before it.

   Two things go with the ports. A program of this engine's own that answers a raylib example
   under another name takes raylib's name once it is read against raylib's source (N 5.1), as
   `shapes_basic_2d` may be `shapes_basic_shapes`, `shapes_basic_3d` `models_geometric_shapes`,
   `audio_sound` `audio_sound_loading` and `models_terrain` `models_heightmap_rendering`. And a
   call that answers otherwise than raylib's of the same name, where the difference is kept, is
   a line on `docs/compared-with-raylib.md`, in a table of its own a port adds to, the first
   being a trigger's axis, from 0 at rest here and from -1 in raylib, which docs/input.md says
   and the comparison does not.
3. **What the trimmer cannot follow in the library** (N 2.5). The native publish warns that the
   library has code the trimmer cannot follow, which Pusher does not reach and another game may.
   The library is marked `IsAotCompatible`, which turns the same analysis on in every build, and
   each warning is mended where a generator can register what was reflected on, or said at its
   place with the reason it is safe, so the build is clean and `-warnaserror` holds it there.
   AssimpNetter's own warnings are the package's and are said once, where the reader calls it.
4. **A probe filtered on the GPU** (TODO.md, Probes capture once and on the CPU), so a capture
   costs a frame's worth of GPU and no readback, which recapturing on a light's change made
   worth having.
5. **C# typed at a running program** (TODO.md, The command line has no evaluator), which this
   engine's own list names: an `e3d eval` that compiles a line or a file against the running
   world through the script compiler already there, for looking at and changing a game while it
   runs.
6. **TODO.md's order** for everything else, the Scenes entry on a program's own spawn among it,
   and another game only when it is of a kind that uses what none of the seven has.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 9 are settled, and their numbers are not given again.

**10. The table says does not apply where an example calls what the flat API lacks** (N 5.2).
Seventeen of the eighteen rows that do not apply give one reason, that the example reaches
OpenGL's own state through rlgl, which a Vulkan engine has no counterpart for. Vulkan has each
state those rows name: which faces are culled, the blend factors, the depth test and its
writing, triangles drawn as points, and compute. A count that calls them out of reach says the
engine carries more of raylib than it does. A row is read by what its example shows, and not by
the calls it makes.

- What the engine does by means of its own is written, or can be. `shaders_rlgl_compute` is the
  game of life `shaders_compute_life` runs, `models_skybox_rendering` a sky as `models_skybox`
  draws one, `shaders_shadowmap_rendering` the shadows `shaders_shadowmap` casts,
  `shaders_vertex_displacement` a texture handed to a shader, and `shaders_hot_reloading` a
  shader read again when its file changes.
- State the flat API does not carry is missing, with the state named in place of rlgl's calls:
  culling (`shapes_rlgl_triangle`, `models_point_rendering`, `shaders_cel_shading`), blend
  factors (`shapes_top_down_lights`, `textures_magnifying_glass`), the depth test
  (`textures_portal_window`), a mesh's vertices written each frame
  (`models_animation_blend_custom`), a second set of texture coordinates
  (`shaders_lightmap_rendering`), and a target with several attachments or a depth that is read
  (`shaders_deferred_rendering`, `shaders_hybrid_rendering`, `shaders_depth_writing`,
  `shaders_depth_rendering`).
- Does not apply is kept for what the engine leaves out by its design, as `core_window_web`.

The script's first reading, by the functions an example calls, stays as the start, and each row
it calls out of reach is read over by hand as the five were.

**11. `build/pack.sh` asks `sed` for what only GNU's has** (N 6.2). Line 34 is
`sed -i "s/PACKED_VERSION/$version/"` over the templates' files. The `sed` of macOS reads what
follows `-i` as the ending of a backup file, and then the first file's name as its script, which
is the error the job printed, an undefined label `uild/templates/content/game-ecs/...`. The macOS
job packs through `build/play-game.sh`, and this was its first run, so the line had never run
there. The version is written without `-i`, through a file beside it or through Python, which
the scripts use already. It is the one such form in the scripts the Windows and macOS jobs reach,
as far as a search for the usual ones went (`grep -P`, `readarray`, `date -d`, `stat -c`,
`sha256sum`, `${x,,}`). A check in `NormTests` finds those forms in the scripts `test.yml` runs
on more than one system and in the scripts they call, and is offered under Replies with a line
beginning `Rule:`, since the norm has no rule for it yet.

**12. The Linux test job was ended from outside the tests** (N 6.2). The step printed sixteen
seconds of tests, skips alone since only skips and failures print, and then nothing until
`The operation was canceled` at 1 minute 36. No test failed. Nothing in the workflows cancels a
job, there being no concurrency group, no time limit and no matrix, and nothing in the engine,
the client or the tests signals a process. macOS ran the same suite to the end. What is left is
the runner itself being stopped, which a machine out of memory does, or a fault of GitHub's.
The owner is asked to run the failed job again, which tells the two apart.

Three things make the next one say what it was. The test step's console logger at normal
verbosity, so each test is named as it passes and a death says which were running. The Linux
job's steps run in the Ubuntu container held to the runner's size, 4 processors and 16 GB, with
`E3D_REQUIRE_VALIDATION` and `E3D_REQUIRE_VULKAN` set as the job sets them, the most memory the
test host took read at the end. And the results kept on a job that did not succeed, where
`if: failure()` keeps them only for one that failed. What is new since the last green run on
Linux and native is where to look first: Assimp's reads landing in its own memory (`1fac9eff`),
and what only Linux runs, `FileHandleTests` over `/proc/self/fd` and the drawing under lavapipe
with the validation layer required.

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
   in `build/version.txt` before the next package. After `82b1feb4` and `abd09df5` that is due.
6. **AGENTS.md's bullet and table row on NORM.md are the owner's, with the exception N 7.4
   makes.** They approved both on 2026-10-05 in the reviewing session, with the plan for the
   norm. A working session that commits a change to its instruction file only on the owner's
   word in its own session is right to, and waits for that word.

## Replies

**Now 1, raylib's core examples, the first part.** 24 more of raylib's core examples are written,
and `core_2d_camera`, `core_3d_camera_free`, `core_drop_files` and `core_input_gestures` are
raylib's again where they were programs of their own under its names. Each was read from raylib's
C at the pinned commit, written for the flat API with its window, scene and words, captured, and
set beside raylib's screenshot, and the table counts 41 written. Their pictures differ from
raylib's where the reason is known: the default font, the pointer at the corner where raylib's
screenshot moved it, and a random layout.

The ports found a fault. Anything drawn between frames, into a render texture before
`BeginDrawing` as half of raylib's examples do, was lost, since the draw lists were cleared as a
frame began and the cameras set for targets forgotten there too. The lists are cleared now in the
render system's own `finally`, once the frame is drawn or would have been, and the cameras once
`EndDrawing` has rendered. `A_Render_Texture_Drawn_Into_Before_BeginDrawing_Is_Drawn_In_That_Frame`
draws into one flat and one in 3D between frames, and failed before the change. Five ports showed
it, the render texture, the letterbox, the pixel-perfect camera and the two split screens.

A render texture here is upright, and raylib's examples draw theirs with a negative height to turn
OpenGL's upright, which turned these over. The engine's is kept, since a target read back is the
right way up as any texture is, and the ports draw it as it is with a line saying why. It is the
second row of the new table on `docs/compared-with-raylib.md`, whose first is the trigger's axis,
with SDL's two extra mouse buttons and the default font after.

A correction to the reply on N 3.3. The renderer does settle a resize by the machine's clock, a
debounce of 150 ms in `RenderPlugin`, which the wait in `WindowResizeTests` stood for. Its 20 frames
are paced at 60 a second, a third of a second, so the test waits past the debounce on frames alone,
as it did five times in five and under lavapipe, but the reason given was wrong.

Still to come in core: `core_3d_camera_first_person` and four more that wait on rcamera's functions,
which are carried next with `UpdateCamera` as raylib's, `core_window_flags` and the two high-DPI
examples, which wait on six of raylib's window flags the flat API lacks, the five larger ones set
aside, and the three that use raygui.
