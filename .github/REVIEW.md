# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `c774a379`. Verdicts 4 to 6 are settled on their replies, which were read: Assimp
reading through C# streams with one folder for the tests (`abc24192`), a body woken when its layer
or its trigger changes (`ac897afa`) and a pair's press as the push alone (`c774a379`). The first
went past what was asked, a path with letters outside ASCII no longer crossing into native code
and a test of the check itself, which finds a file it left open. The third measured the old sum at
0.87 for a crate dragged and turned where its weight times the step is 0.33. Verdicts 1 to 3 were
settled before them, so everything the Windows run of `db942962` showed is mended. Verdicts 8 and
9 are two small things left by 4 and 5.

The owner pushed `main` up to `15fa305a` on 2026-10-05, which has the clock and the follow rule
and has neither the one clamp nor the file system for Assimp. Its run is the first to draw on
Windows and macOS, and the platform test should be green in it. `Config.FrameSeconds` and
`Time.FrameSeconds` are new public lines and `PhysicsSettings.MaxStepsPerFrame` is gone, which
Decision 5 leaves with the owner.

[NORM.md](NORM.md) is new, and item 3 of the Now list is about it.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdicts 7, 8 and 9**, the distance past which a parent's move is a placing, which is in
   hand, an exception inside Assimp's callbacks, and a crate asleep on a kinematic floor.
2. **A picture in the README opens the example's own source** (N 4.5). The owner chose this for
   BevyCSharp on 2026-10-05 over the live demos, so that a picture leads to the program that drew
   it and nothing is cached from another project's site, and the reason holds here word for word.
   Each picture in the gallery links to the file that holds its example, as a full address under
   `https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/`, the README being the
   package's page too. The links to raylib's site go, with `build/raylib-examples.sh`,
   `build/raylib-examples.txt`, their paragraph in BUILDING.md and the comparison in
   `DocumentLinkTests`, which holds every picture to a link whose file is in the checkout
   instead, with no request made. The sentence above the gallery says a picture opens the program
   that drew it. BevyCSharp did the same in its `57fc7e9`.
3. **The norm's checks.** [NORM.md](NORM.md) is new, at the owner's wish of 2026-10-05: the
   rules both engines keep, numbered, each with its reason and what checks it, 36 of them and
   DESIGN.md's eleven sections by reference. One batch gives the rules their checks here. A class
   `NormTests` has a test for each rule the table under Conformance calls `to take` for 3DEngine
   and a test or a setting can check, named for the rule as `N_1_3` is for N 1.3, its message
   beginning with the rule's number. A rule existing code does not keep gets its list,
   `build/norm/<number>.txt`, written from the test's own finding so the first list is exact, the
   test failing for a place not listed and for a line that no longer applies. One more test holds
   NORM.md and `NormTests` to each other. No file is rearranged in this batch. The lists are what
   is left, and a place on a list is mended when a batch next touches it.

   N 3.3 and N 3.4 start at what Verdicts 1 and 4 leave. N 2.8 finds `BepuUtilities` and
   `SDL3-CS.Native` missing from D 8's table, and `Microsoft.CodeAnalysis.CSharp` there under a
   shorter name. N 1.5 finds `templates/`, `games/` and `docs/` missing from AGENTS.md's table,
   whose rows are added without asking, as N 7.4 says. N 2.5, N 2.7 and N 5.2 are steps of the
   workflow and more than this batch, and keep `to take` until their own, N 5.2 being item 4. The
   count of each list goes under Replies, and the table in the norm is brought up to them. A rule
   read as wrong is answered under Replies with a line beginning `Rule:`.
4. **raylib's own examples, one by one, as the measure** (N 5.2). `coverage.py` counts raylib's
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
5. **A probe filtered on the GPU** (TODO.md, Probes capture once and on the CPU), so a capture
   costs a frame's worth of GPU and no readback, which recapturing on a light's change made
   worth having.
6. **C# typed at a running program** (TODO.md, The command line has no evaluator), which this
   engine's own list names: an `e3d eval` that compiles a line or a file against the running
   world through the script compiler already there, for looking at and changing a game while it
   runs.
7. **TODO.md's order** for everything else, the Scenes entry on a program's own spawn among it,
   and another game only when it is of a kind that uses what none of the seven has.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 6 are settled, and their numbers are not given again.

**7. A far jump is ten units, whatever a unit is and whatever the frame took.**
`ParentFollowers.PlaceBeyond` places a body whose parent moved more than 10 units in a frame. A
game whose unit is a centimeter has a lift at 7 meters a second placed every frame, carrying and
pushing nothing. At the clamp of a quarter second any parent faster than 40 units a second is
placed for that frame, and what rode it is left behind, so a slow frame undoes for a fast
platform what Verdict 2 mended. No distance tells a move from a placing for every game. The
number becomes a setting of the physics, in units and documented as one, and a program that knows
it is placing a parent says so with a call, which a level starting again uses. A test carries a
crate on a platform at 60 units a second through a frame of a quarter second. In the same file,
`Observe` makes a set and an array every frame for any world with a parent in it, which are kept
and used again as `_wanted` is.

**8. A reader's exception inside Assimp's callbacks ends the process** (N 2.6). `AssimpFiles`
says so itself, and catches a list: I/O, access, argument and not supported. What it calls is a
reader, and `IAssetReader` is public, so a game's own reader over an archive throws what it likes,
`InvalidDataException` for a damaged entry being the likely one, which is none of the four. Each
callback catches everything, the first exception is kept, and the load answers with it and the
name of the file once Assimp has returned. A test gives a model whose `.mtl` comes from a reader
that throws `InvalidDataException`, and finds a message naming the `.mtl`.

A model through the asset server is copied whole into a `MemoryStream` that grows as it is
filled, where a file on disk was read in place before. For a large model that is the file twice
over in large blocks on the way, and Manor streams its cells through that path. What it costs is
measured there, the memory the soak reads and the frames `frame.profile` shows as a cell comes
in, before and after, and a stream that knows its length is given a buffer of that size, or
handed over as it is when it can seek.

**9. The five ways are tried over a static floor** (N 3.1). `WakeAround` wakes a body that is not
static with `AwakenBody`, which wakes the set that body is in. A crate asleep on a kinematic
platform at rest may be in a set of its own, since an island does not reach through a kinematic
body, and would then sleep on when the platform's layer changes or it is made a trigger. A test
tells, the same crate on a kinematic floor, and if it stays in the air the bodies within the
kinematic body's bounds are woken as a static's are.

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

**Verdict 7, a far jump.** `PhysicsSettings.PlaceBeyond` is the distance now, in units, documented
as one, and 100 to begin with, so a parent going 400 units a second is followed through the
longest frame. `PhysicsWorld.MarkPlaced(entity)`, which a behavior reaches as `ctx.Physics`, says a
parent was put where it is in this frame, and the bodies under it, or under anything below it, are
put at their places at rest. The test of a far jump is a theory of two now, a carrier put back 5
units with the call and one put 150 units away without it, each placing its platform and flinging
nothing. The physics page shows the call and says when to raise the setting. `Observe` keeps its
set of bodies seen and its list of bodies gone and clears them each frame.

What a platform at 60 units a second did through a frame of a quarter second at ten units was
smaller than the verdict expected. The frame's own steps still followed the parent by the
velocity observed the frame before, so the platform was where it should be when the frame ended.
The placing came after them and stopped the platform for the next step, from which it caught up at
twice its speed. The crate kept its own momentum through that step and moved 0.003 against the
platform, so it was not left behind, and the test holds the platform's own speed instead. It
finds the platform at 0 for a step at ten units, and within half a unit of 60 in every frame at
100, the crate with it. PublicApi.txt gains `PlaceBeyond` and `MarkPlaced`.

**Verdict 8, a reader's exception inside Assimp.** Every callback `AssimpFiles` gives Assimp
catches every exception, keeps the first with the name of its file, and reports the file to
Assimp as missing or short. Once Assimp returns, the load is answered with an `IOException`
naming the file, with the reader's exception inside it, ahead of whatever Assimp made of the
absence. The theory gives a model whose `.mtl` comes from a reader that throws
`InvalidDataException`, once as the entry is opened and once as it is read. It finds the
exception and the asset loader's message naming `models/tri.mtl`, where the code before ended the
test host with that exception. A stream that can seek is now shared as it is, each of Assimp's
opens at a place of its own in it, and only one that cannot is read into memory. Assimp's reads
land in its own memory from the stream, through a read callback of the stream's own, where the
binding's read filled an array as long as the read and copied it over.

The 32 models of Manor, 549 KB of OBJ, read through the reader as the asset server reads them,
allocated 2.31 MB a round before Verdict 4, when Assimp read the files itself, 3.94 MB with the
copy into a growing `MemoryStream`, 3.16 MB with the stream shared, and 2.56 MB with the reads
landing in Assimp's memory, in 77 to 88 ms a round each way, which is within the noise. Manor
itself, packed, built from the package and walked by its autopilot for a minute, before and
after, made the same walk to the entity: managed memory read 14.6 to 15.2 MB before and 11.0 to
14.8 after, the heap 24 to 30 MB both times, and `profile.slowest` found 22.6 ms before and 23.0
after, Manor's own longest frame 23 ms both times. A cell coming in costs the same either way.

**Verdict 9, a kinematic floor.** The theory has five more cases, the same crate asleep on a
kinematic floor, and all pass with no change to the engine. Bepu puts a kinematic body at rest to
sleep in the set of what rests on it: the crate and the floor were in one set, and a second crate
brought to rest beside the first woke that set and slept in it too, so `AwakenBody` on the floor
wakes everything on it.
