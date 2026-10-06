# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `c3dddc1b`. A render texture draws into up to four images of their own formats at
once, with one depth, pipelines shared by targets of the same formats and a shader's outputs
read from its SPIR-V, rlgl's color blend switch is carried, and `shaders_deferred_rendering` is
written (`692cefee`), held under lavapipe and the validation layer in a container, which found a
device feature the code had assumed, and beside raylib's own program built here, which draws the
same frame. Every raylib example the engine carries is written, 216 of 221, the five missing out
by direction. The library is marked AOT compatible and its build has no trim warning, the asset
server's and the ECS's reflection mended and the console's and the script compiler's said at
their places with their reasons, the native publish naming AssimpNetter's own alone, and Pusher
published native drew its frames (`c3dddc1b`), which settles item 3. No verdict is open.

Before them, Inter-Quake Models (`52304768`) and Model 3D files (`e5ea2a22`) are read
by readers of the engine's own, as raylib reads them, the first for the skeleton and clips
Assimp left out and the second from m3d.h under its license with no dependency added, each with
four tests on a file the tests write. `IsModelAnimationValid` compares bone counts and parents,
and names where a clip has them, a kept difference the comparison explains.

Before them, Windows passed every test in the run of `cac05ded`, 1,309 of them in five
minutes at 1,020 MB, so the registry step of `1c1a3cea` gave it its device and the 126 failures of
the runs before are gone, and the job that joins the three pages ran and wrote one. macOS failed
the two of `AppLeakTests`, and `fb68cfad` has the holder: `App.CurrentApp`, the `AsyncLocal` the
hook of N 3.7 added, which macOS's `FileSystemWatcher` keeps in the context it captures, so the
script compiler's watchers kept every app. The app is held weakly, with a test that failed
before the mend, the number reproduced on Linux by capturing the context on purpose, 5.66 MB
against the page's 5.87, and the allowance unchanged, which settles Verdict 23. `UpdateMeshBuffer`
is carried by raylib's index and `shaders_lightmap_rendering` is written (`6c0b07ca`), the table
at 213 written and 8 missing.

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
3. **A probe filtered on the GPU** (TODO.md, Probes capture once and on the CPU), so a capture
   costs a frame's worth of GPU and no readback, which recapturing on a light's change made
   worth having.
4. **C# typed at a running program** (TODO.md, The command line has no evaluator), which this
   engine's own list names: an `e3d eval` that compiles a line or a file against the running
   world through the script compiler already there, for looking at and changing a game while it
   runs.
5. **TODO.md's order** for everything else, the Scenes entry on a program's own spawn among it,
   and another game only when it is of a kind that uses what none of the seven has.

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

**Now 3, a probe filtered on the GPU.** The frame that draws a probe's sixth face records its filter
after it, four compute shaders over the faces where they were drawn, so a capture costs that frame's
work and nothing is read back. The readback the probes alone used, with its hooks in the frame, is
gone, and so is `EnvironmentMap.FromCapture`. `probe_gather` fills a cube of faces 64 texels wide,
each texel reading the face that looks most nearly along it through that face's own view-projection,
as the worker did, and `probe_mips` makes its mips. `probe_prefilter` writes the probe's cube of
faces 32 texels wide by GGX over 64 samples a texel, as the CPU filter did. `probe_irradiance`
projects the 16-wide level onto the nine harmonics in one group of 64 threads, into a storage buffer
of the probe's own that the model pass reads at the lights' set's bindings 10 to 13, so the lighting
buffer carries 144 bytes less a probe.

The probe's reference frame differed in 14% of its pixels, and the old filter caused it. It gathered
the faces into an equirectangular image, whose bottom row holds the texel straight down 256 times
over, and that image's mips average rows alike, so the ball's highlight under the probe took the
weight of a row. The mean light of its last mip was 3.8 times its first's, where the GPU filter's
falls by a tenth from first to last, measured on both cubes of the reference's scene read back. The
ball's lower half, which reflects its own top through the box, is darker, the bright bands on the
walls level with the probe are gone, and the reference is redrawn. The environment map is still
filtered on the CPU from its image and has the same fault for a light near a pole. TODO.md's entry,
renamed, says so, and names the probe's filter, given the image as a cube, as its mend. Two tests
read the probe's cube back from the GPU, where they read the worker's. The render and reference
tests, 96, pass on lavapipe under the validation layer in the container. The suite: 1,329 passed, 0
failed, 1 skipped, the set-layout test taught the four buffers.

Shared: a reflection probe's capture filtered on the GPU with nothing read back, which BevyCSharp
has from Bevy's filter of a cubemap, and the mips of an equirectangular image overweighting a pole,
which either engine's filter from such an image may have.

**Now 4, C# typed at a running program.** `./e3d eval <code>` and `./e3d eval -f <file.cs>` compile
C# against the running app and run it on the main thread between frames, as the `eval` command every
app has, so `e3d command eval` and `e3d list` have it too. A fragment is a program's top-level
statements, compiled with an example's usings and the flat API, and with `world`, `ecs` and `app` in
scope, so a file may begin with usings, await, and declare local functions and types after its
statements. A last expression with no semicolon answers with its value, as C# Interactive does, a
collection by its first 50 items and a value with no text of its own by its fields, as `entity.get`
shows a component. A fragment is compiled against every assembly the process has loaded, so a game's
own types are in reach, into a collectible load context let go after its run, and the references are
read once for the process through the cache the script compiler had, moved to a class both use. Code
that does not compile fails with `EVAL_COMPILE_FAILED` and the compiler's first errors by line and
column, and code that throws with `EVAL_THREW` and the exception's type and message, its stack in
the log. The first fragment held its frame for about two seconds while Roslyn's own code was
compiled, and each after for about 150 ms, measured with `profile.slowest` on
`models_reflection_probe`. A native build refuses before compiling, behind
`RuntimeFeature.IsDynamicCodeSupported`, and the build stays free of trim warnings. It runs any code
with the app's rights, which are those of the user who owns the session file, as the guide and the
skill say. The skill and `docs/driving-with-e3d.md` have a passage on it, the README a line, and
TODO.md's entry goes. Four tests run fragments against a world: an expression, statements that
change the world and a value shown by its fields, a file with a using, an await, a local function
and a record, and a compile error and an exception refused with their codes. The suite: 1,333
passed, 0 failed, 1 skipped.

Shared: C# typed at a running app, in the library here where BevyCSharp keeps it in its editor, as
top-level statements with the last bare expression as the answer, which SHARED.md's row to consider
may record as had by both.
