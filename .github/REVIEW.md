# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `a61308b0`. What the norm's lists held that a batch could mend is settled on the
replies, which were read (`8d92856d`, `42da1c44`), with two rules taken that were steps of the
workflow, a game published native and played (`f2abc4f0`, N 2.5) and every example built on the
packed package alone (`a61308b0`, N 2.7). The lists found two things nobody had seen: two tests
that left a folder in the system's temporary folder on every run, and a wait in the resize test
that timed nothing the engine does. The table under Conformance has this engine at 23 rules
checked, 3 with places listed, 1 still to take, which is the table of raylib's examples, and 9 by
review. The norm's check came after BevyCSharp's and its first run found 30 commits out of the
form COMMITS.md gives, none written again, 19 of them being pushed.

Everything the Windows run of `db942962` showed is mended. The owner pushed `main` up to
`15fa305a` on 2026-10-05, which has the clock and the follow rule, and its run is the first to
draw on Windows and macOS. `Config.FrameSeconds`, `Time.FrameSeconds`,
`PhysicsSettings.PlaceBeyond` and `PhysicsWorld.MarkPlaced` are new public lines and
`PhysicsSettings.MaxStepsPerFrame` is gone, which Decision 5 leaves with the owner.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **raylib's own examples, one by one, as the measure** (N 5.2). `coverage.py` counts raylib's
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

   The first count, 17 written, 168 that can be, 20 missing and 17 that do not apply, is read as
   it should be. What C# has of its own, strings, files and memory, covers raylib's helpers for
   them, so their examples can be written. rlgl's matrix stack and its vertices one at a time are
   missing and not out of reach, since a raylib program turns a drawn shape with the one and
   draws a shape of its own with the other. Once the rows that can be written are, the missing
   are taken by how many rows each holds, as BevyCSharp takes its gaps.
2. **What the trimmer cannot follow in the library** (N 2.5). The native publish warns that the
   library has code the trimmer cannot follow, which Pusher does not reach and another game may.
   The library is marked `IsAotCompatible`, which turns the same analysis on in every build, and
   each warning is mended where a generator can register what was reflected on, or said at its
   place with the reason it is safe, so the build is clean and `-warnaserror` holds it there.
   AssimpNetter's own warnings are the package's and are said once, where the reader calls it.
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

None open. Verdicts 1 to 9 are settled, and their numbers are not given again.

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

**Now 2, raylib's examples: the table.** `build/examples-table.py` writes `.github/EXAMPLES.md` from
the `examples_list.txt` of the raylib `run.sh` pins, fetching that commit alone where no checkout is
given, and `--check` in `build.yml` holds the table to what it writes (N 5.2). Of raylib's 222, 17
are written, 163 can be written, 24 are missing something and 18 do not apply. A row for a written
example shows raylib's screenshot beside the capture here, and the 28 programs of this engine's own
are listed after raylib's.

`3DEngine.Examples/triage.tsv` holds the rest, started by `--triage` from the functions of raylib.h
each example calls that CHEATSHEET.md lacks and read over by hand. C#'s strings, files and memory
count as carrying raylib's `TextFormat`, its file and directory functions and `MemAlloc`, which
otherwise made 101 examples missing for `TextFormat` alone. Of the 25 that call rlgl, 9 use only
its matrix stack and its vertices one at a time, which the flat API could carry and which are
listed as missing, and 16 reach OpenGL's own state, its framebuffers, blend factors, buffers and
culling, which do not apply. Twenty use raygui and are noted as written with ImGui in its place.
Five were read over for what the functions do not say: the M3D, VOX, XM and BMFont files no reader
here takes, and `core_window_web`, which is about a browser's loop. The 17 written ones are read
against raylib's screenshots next, before the first new one.
