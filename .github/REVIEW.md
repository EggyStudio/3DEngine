# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `9decca1d`. The norm's checks are settled on the reply, which was read. `NormTests`
holds twelve rules and the norm itself, written after BevyCSharp's so the two read the norm
alike, each list from its test's own finding. Its first run found a fault at once, 30 commits
whose subject has the three marks with none of the spaces COMMITS.md puts between them, written
from a session's summary and not from the document. None is written again, 19 of them being
pushed, and the check holds every commit since, which is what a rule with a check is for.

The three lines beginning `Rule:` are answered in [NORM.md](NORM.md). A test of what is no area
of the library, the documents or the package, is in a folder named for what it tests and is left
out, and the norm's own class is at the root (N 1.4). The table of areas is held to the top
folders of the repository and of the library, as this engine read it, a row naming a folder by
itself or by a folder within it (N 1.5). A game's capture is left out of the size as an example
with a window of its own is (N 4.5). The table under Conformance has this engine's column: 17
rules checked, 7 with places listed, 3 still to take and 9 by review.

Everything the Windows run of `db942962` showed is mended. The owner pushed `main` up to
`15fa305a` on 2026-10-05, which has the clock and the follow rule, and its run is the first to
draw on Windows and macOS. `Config.FrameSeconds`, `Time.FrameSeconds`,
`PhysicsSettings.PlaceBeyond` and `PhysicsWorld.MarkPlaced` are new public lines and
`PhysicsSettings.MaxStepsPerFrame` is gone, which Decision 5 leaves with the owner.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What the norm's lists hold that a batch can mend.** The wait in `WindowResizeTests`, which
   nothing in the engine times (N 3.3). The three temporary folders outside `TestFolder` (N 3.4).
   `TrueTypeFontTests` in a folder that is no area of the library (N 1.4). The three packages D 8's
   table does not name (N 2.8), which are companions of ones it does and are said there as such.
   The checks of N 1.4 and N 1.5 brought to the norm's words since, a test of the documents or
   the package left out with that reason, and a row for a folder within a top folder naming it.
   The larger lists, the 116 places of N 1.2 and the 6 files of N 1.3, are mended as the norm
   says, when a batch next touches a file on them, in a commit of its own.

   Two rules still to take are steps of the workflow, each a batch. A game is published native
   and played by the workflow (N 2.5), as Pusher was by hand. A handful of examples are built in a
   project of their own on the packed package (N 2.7), which they keep already and nothing holds.
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
   on. Many a batch, a module at a time, and it is the item to come back to whenever the ones
   above are through.
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

**Now 1, what the lists hold.** `8d92856d` moved `TrueTypeFontTests` and its `planes.ttf` to the
tests of `Api`, where `TrueTypeFont` is, alone, as the norm asks of a commit that only moves code.
This batch mends the rest one batch can.

- N 3.3. The wait in `WindowResizeTests` is gone, its twenty frames kept without the ten
  milliseconds after each. Nothing in the engine settles a resize by the clock, since the next
  frame carries out the size asked for, and the test passed five runs in five without it, and
  under lavapipe in the container. The list keeps the 10 waits the rule leaves out.
- N 3.4. `Engine3DSceneTests`, `ImageTests` and `SceneSpawnerEmbeddedTextureTests` hold a
  `TestFolder` each. The last two made a folder under the system's temporary folder on every
  run and left it there. The list is empty and goes.
- N 2.8. D 8's table names `SDL3-CS.Native`, `BepuUtilities` and `Microsoft.CodeAnalysis.CSharp`,
  the companions it lacked and the generator's package under its full name, as
  THIRD-PARTY-NOTICES.md already did. The list goes.
- N 1.4 and N 1.5 read as the norm now words them. The documents' tests, the package's and the
  norm's own class are on N 1.4's list with those reasons, and a folder is named by a row of its
  own or by one within it. N 1.4 has no place left to mend, and N 1.5 keeps its 3.

**N 2.5, a game published native.** `build/play-native.sh <game>` publishes a game from the package
as native code for the machine it runs on and draws 300 frames of it offscreen under the
validation layer, failing where it stops early, where the layer is not on, or where the layer
reports an error, as `play-game.sh` does for a game on the runtime. `build.yml` runs it for Pusher
after the pack, with clang and zlib's headers installed beside lavapipe for the native compiler.
In the Ubuntu 24.04 container with lavapipe it drew its 300 frames with no error. The publish warns
that the library and AssimpNetter have code the trimmer cannot follow, which is the rest of N 2.5,
the reflection a generator could replace, and which this game does not reach.
