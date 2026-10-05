# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `14c8b4a1`. Twenty-eight of raylib's core examples are settled on the reply, which
was read, 24 written for the first time and 4 that were programs of this engine's own under
raylib's names written again from its source. `core_scissor_test` was set beside raylib's C at
the commit pinned and is it line for line, its constants, its words and its colors. The ports
found that whatever a program drew between frames was lost, into a render texture before
`BeginDrawing` as half of raylib's examples draw, which five of them showed and a test holds.
The table of calls that answer otherwise than raylib's has its first four rows, and the table of
examples stands at 41 written, 139 that can be, 24 missing and 18 that do not apply, Verdict 10
not yet taken. The reply corrected an earlier one of its own, on the wait in the resize test,
and Verdict 13 is about what the correction found.

Before these, the table of raylib's examples (`c05bd485`) and the first port (`7b9b2f2e`) were
settled. Every rule of the norm a machine can check has its check in this engine: 24 checked, 3
with places listed and 9 by review.

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

**13. The renderer's resize debounce reads the machine's clock** (N 3.3). `RenderPlugin` carries
a resize out 150 ms after the last one by `Environment.TickCount64`, as the reply's correction
found. `WindowResizeTests` passes because its twenty frames are paced at sixty a second and so
take a third of a second of the machine's time, which a run paced faster would not, as the
physics tests are at a thousand frames a second. Where `Time.FrameSeconds` is set, the debounce
counts the frame's time as everything else stepped does, so the test steps past it in ten frames
at any pace, and a resize in a capture lands on the same frame on every machine.

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

**Verdict 11, macOS's sed.** `build/pack.sh` writes the version into each template through a file
beside it moved over it, which both seds read alike. It wrote both templates in a dry run, and no
other `sed -i` is in the scripts the three jobs run.

Rule: a script a job runs on more than one system uses only what both GNU's and BSD's tools have.
A check could read the scripts `test.yml` runs on Windows and macOS, `build/pack.sh`,
`build/play-game.sh` and `build/fetch-slang.sh` today, for `sed -i`, `grep -P`, `readarray`,
`date -d`, `stat -c`, `sha256sum` and `${x,,}`, with a list.

**Verdict 12, Linux cut short.** It is memory. The Linux job's steps in the Ubuntu 24.04 container
held to 4 processors and 16 GB, with both variables set, built, ran 1,121 tests in about 85 seconds,
and lost its test host at the limit. The host's peak resident memory was 15.6 GB and the
container's 16 GB, and the test run says the host crashed. A second run, its memory read every
second, climbed about 50 MB a second for four minutes to the same end. GitHub's runner for
`ubuntu-24.04` has 16 GB, so the job's step is the host taken by the system, which GitHub reports
as cancelled.

It is no one test. The 44 tests that never reported were render tests running beside the flat
API's, and the render classes alone climb the same way, `OffscreenRenderTests` from 200 MB to
1.5 GB over its 70 tests, `FrameEffectsTests` to 843 MB over 9. The next run reads the GC's heap
beside the process's memory, to say whether what grows is managed or what lavapipe and the
validation layer hold, and the cause and its mend come in a batch of their own.

`test.yml` names each test as it ends, the console logger at normal verbosity in all three jobs,
and keeps the results of a job cancelled as well as of one that failed.
