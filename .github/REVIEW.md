# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `bf1a559c`. Verdicts 7 to 9 and the README's pictures are settled on their replies,
which were read: the distance past which a move is a placing as a setting, with `MarkPlaced`
(`7ae91e7c`), every exception caught inside Assimp's callbacks, with a crate asleep on a kinematic
floor (`1fac9eff`), and each of the 52 pictures opening the program that drew it (`bf1a559c`).
Two of the verdicts feared more than was there, and the replies measured it. A crate on a fast
platform through a slow frame slipped 0.003 where Verdict 7 had it left behind, the placing
having come after the frame's steps. A crate asleep on a kinematic floor wakes with it, Bepu
putting a kinematic body at rest to sleep in the set of what rests on it, so Verdict 9 changed
nothing and its five cases stay as the proof. The third found more than was asked, a model read
from its own stream into Assimp's memory, 2.56 MB a round for Manor's 32 models against 3.94 with
the copy and 2.31 before any of it, and Manor's own walk the same either way.

Everything the Windows run of `db942962` showed is mended. The owner pushed `main` up to
`15fa305a` on 2026-10-05, which has the clock and the follow rule, and its run is the first to
draw on Windows and macOS. `Config.FrameSeconds`, `Time.FrameSeconds`,
`PhysicsSettings.PlaceBeyond` and `PhysicsWorld.MarkPlaced` are new public lines and
`PhysicsSettings.MaxStepsPerFrame` is gone, which Decision 5 leaves with the owner.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **The norm's checks**, which are in hand. [NORM.md](NORM.md) is the owner's wish of
   2026-10-05: the rules both engines keep, numbered, each with its reason and what checks it, 36
   of them and DESIGN.md's eleven sections by reference. One batch gives the rules their checks
   here. A class `NormTests` has a test for each rule the table under Conformance calls `to take`
   for 3DEngine and a test or a setting can check, named for the rule as `N_1_3` is for N 1.3,
   its message beginning with the rule's number. A rule existing code does not keep gets its
   list, `build/norm/<number>.txt`, written from the test's own finding so the first list is
   exact, the test failing for a place not listed and for a line that no longer applies. One
   more test holds NORM.md and `NormTests` to each other. No file is rearranged in this batch.
   The lists are what is left, and a place on a list is mended when a batch next touches it.

   BevyCSharp wrote its own first, `BevyCSharp.Tests/NormTests.cs` in its checkout, and it is
   the model, so the two suites read NORM.md alike: one `Hold` that takes the places found and
   fails for one not listed and for a listed one that keeps the rule, a list's line being the
   place, then a tab and the reason where there is one, and the one test reading this engine's
   column of the table, where a cell that begins `listed`, or begins `checked` and names
   `NormTests`, has its test. The norm has since said three things its questions brought out. A
   rule may leave a kind of place out, and those places are on its list with their reason and
   stay. What the tests share is at the test project's root (N 1.4). A mending that only moves
   code is a commit of its own.

   N 3.3 and N 3.4 start at what the clock and `TestFolder` leave, the waits on something
   outside the frame being left out with their reasons. N 2.8 finds `BepuUtilities` and
   `SDL3-CS.Native` missing from D 8's table, and `Microsoft.CodeAnalysis.CSharp` there under a
   shorter name. N 1.5 finds `templates/`, `games/` and `docs/` missing from AGENTS.md's table,
   whose rows wait for the owner's word in this session with the rest of that file. N 2.5, N 2.7
   and N 5.2 are steps of the workflow and more than this batch, and keep `to take` until their
   own, N 5.2 being item 2. The count of each list goes under Replies, and the table in the norm
   is brought up to them. A rule read as wrong is answered under Replies with a line beginning
   `Rule:`.
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

**Now 1, the norm's checks.** `NormTests` at the test project's root holds twelve rules and the
norm itself, after BevyCSharp's: one `Hold(number, found, what)`, a list line being the place, then
a tab and the reason where there is one, and `NormAndItsTestsAgree` reading this engine's column.
Each list was written from its test's own finding, and a line added for a file that is not there
fails the test. The counts follow, the places to mend first and those the rule leaves out after.

- N 1.1, every exported type in `Engine`, the namespace Annex A gives first. Checked, no list.
- N 1.2, listed 116, a public type in a file not named for it, most of them the flat API's handle
  types beside `Engine3D` in its parts and the attributes of `BehaviorAttributes.cs`.
- N 1.3, listed 6, `OffscreenRenderTests.cs` the longest at 2,283 lines.
- N 1.4, listed 5, and 2 left out, `Needs.cs` and `TestFolder.cs`, which the tests share.
- N 1.5, listed 3, `docs`, `games` and `templates`, whose rows wait for the owner's word.
- N 2.8, listed 3, `BepuUtilities`, `Microsoft.CodeAnalysis.CSharp` and `SDL3-CS.Native`.
- N 3.3, listed 1, the wait in `WindowResizeTests`, whose comment speaks of a resize settling,
  which nothing in the engine times, and 10 left out, the waits on the asset server's and the
  probe's workers, the socket's and the session file's clocks, a file's write time and
  `TestFolder`'s second try.
- N 3.4, listed 3, `Engine3DSceneTests` writing to the temporary folder, and `ImageTests` and
  `SceneSpawnerEmbeddedTextureTests` making folders there that nothing removes.
- N 4.1, checked, with 2 left out, STYLE.md and COMMITS.md, which name the dashes.
- N 4.2, checked, the README at 292 lines and linking every page of `docs/`.
- N 4.5, checked, with 7 left out, the games' captures at their own windows' sizes.
- N 7.2, checked from `bf1a559c` on, and skipped with its reason in a checkout that does not hold
  that commit, which the workflow's checkout of one commit is.

N 7.2 found a fault of my own before it ran. COMMITS.md puts a space between the three marks, and
the 30 commits from `7a8360ae` to `bf1a559c` have them with none, written from a session's summary
rather than from COMMITS.md. The first 19 of them are pushed, so none is written again. This commit
is the first in the form again, and the test holds every one after it.

Rule: N 1.4 has no folder for a test of what is not an area of the library. `DocumentLinkTests`
and `FirstGameTests` test the documents, `PackageContentsTests` the packed package and `NormTests`
the repository, and each is on the list with nowhere to go. A test of what is not the library, in
a folder named for what it tests, could be left out as what the tests share is.

Rule: N 1.5 is read here as each top folder of the repository, the hidden ones apart, and each top
folder of the library, a row covering what is beneath it, which finds the three the Now list
expected. BevyCSharp's test reads each project, a row covering it only from the project's own
folder down, which here would ask for a row for each game and each template where one row for
`games/` says where they are. The two read alike only once the rule says which it means.

Rule: N 4.5 leaves out an example that asks for a window of its own, and the seven games' captures
are at their own windows' sizes too, which `DocumentLinkTests` has held since they came. A game
could be left out of the size by the rule's words as such an example is.
