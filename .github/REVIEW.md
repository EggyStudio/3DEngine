# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `d46c829a`. The environment map is filtered on the GPU by the probe's stages, from a
cube that weighs each direction by its solid angle, a 4096 map in 10 ms where it took 917, with a
test holding a cap of light at the zenith and one on the horizon to the same mean at every mip,
which the old filter fails from mip 1 (`c5b4c7d9`). A script compiled again is swapped in between
frames and carries its components and resources onto its new types by their fields, where the swap
on the compiler's thread could skip a system or run one twice (`2d506d4b`). Four more of raylib's
functions are carried, 506 of 619, and each of the 113 left has its line on the comparison page
(`63f0fc30`). A probe refreshes every so many seconds while it stays ready (`74e1827a`), a texture
and a sampler declared apart are laid out and bound as such (`b0386c1e`), and STYLE.md's checks are
run over the tree, with a nullable warning that reached `main` under an incremental build mended
(`2b39ddd2`), and a probe's capture draws the frame's particles after its meshes, so a fire glows in
a room's metal (`d46c829a`). Items 3, 4 and 5 are settled, and the list is refilled. No verdict is
open.

Before them, a reflection probe's capture is filtered on the GPU in the frame that draws its sixth
face, with nothing read back, and its reference frame is redrawn with the reason measured, the CPU
filter having overweighted the poles of its equirectangular image, a fault the environment map's
filter shared (`3f597c01`). `./e3d eval` compiles C# against the running program and runs it between
frames, four tests and no trim warning (`075c5b3c`). A scene spawn hands back every load it took,
where `SceneSpawner.Spawn` loaded textures nothing held (`2094e704`), which closes TODO.md's Scenes
entry. No verdict was open.

Before them, a render texture draws into up to four images of their own formats at
once, with one depth, pipelines shared by targets of the same formats and a shader's outputs
read from its SPIR-V, rlgl's color blend switch is carried, and `shaders_deferred_rendering` is
written (`692cefee`), held under lavapipe and the validation layer in a container, which found a
device feature the code had assumed, and beside raylib's own program built here, which draws the
same frame. Every raylib example the engine carries is written, 216 of 221, the five missing out
by direction. The library is marked AOT compatible and its build has no trim warning, the asset
server's and the ECS's reflection mended and the console's and the script compiler's said at
their places with their reasons, the native publish naming AssimpNetter's own alone, and Pusher
published native drew its frames (`c3dddc1b`), which settled the trimmer's item.

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
5. **The comparison page held to `coverage.py`.** Its two tables of the 113 functions not carried
   are written by hand against the list `coverage.py` prints, so the next function carried leaves
   the page a line wrong. `coverage.py --check` reads the page and fails where a name is on one side
   alone, in the workflow beside `examples-table.py --check` (N 5.2), and the page's two counts of
   506 of 619 are checked with the names.

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

**Now 3, the shadow ranking.** The spot and point lights past the slots there are room for were
ranked by whether the camera sees their reach and then by how near it comes, so a dim candle the eye
stands in took a shadow from a lamp of forty times its light three units off.
`LightingUboPrepare.Rank` puts between the two the light that reaches the eye, a light's brightness
over one plus the square of how far its reach is from the eye, and keeps the reach for ties, so of
two alike the nearer still comes first. A test ranks a candle, a lamp and the lamp twice as far, two
lamps alike at two distances, and a light behind the camera against a dimmer one in front of it. The
98 render and reference tests pass as they were, the dozen shadowed lights' reference among them.
The flat API's remarks said the four lights of each kind nearest the camera shadow, and the first
spot light, from before there were ten and twelve, and say what is so, as RENDERING.md, the guide
and TODO.md's entry do, which keeps how much of the picture a light lights as unweighed. The suite:
1,345 passed, 0 failed, 1 skipped.

On item 4, N 1.5's three rows in AGENTS.md wait for the owner's word in this session: my
instructions from the owner are that a change to AGENTS.md or CLAUDE.md is confirmed by them here,
not on another session's account of their approval, so I have asked them rather than adding the
rows. The rest of item 4, and item 5, are next.

**Now 5, the comparison page held to `coverage.py`.** `coverage.py --check` reads the first cell of
each row of the page's two tables, where a row names the functions it answers, the rest of the row
naming what answers them, carried functions among them, and fails where a function not carried is
answered nowhere, where one answered is carried, or where the page's two counts of 506 of 619 differ
from the list's. With no path given it reads raylib.h from the checkout `examples-table.py` reads,
of the commit run.sh pins, cloned where there is none, so the build workflow runs it beside the
table's check. `GetGlyphIndex`, answered among the calls that answer otherwise, has its row among
those not carried, where a line pointed to it. Made to fail, it named a row taken off the page and a
count changed. The suite: 1,345 passed, 0 failed, 1 skipped.
