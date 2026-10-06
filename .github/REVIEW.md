# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `134d4f3d`. A game's own text box places the input method's window beside it with
`SetTextInputArea`, as an ImGui field does, read back from SDL on `text_input_box` run hidden
(`134d4f3d`), RENDERING.md's order of work names what is built (`fd9af099`), and the README's status
points at the comparison page for the functions left out (`16bd04ef`). BevyCSharp's kinematic batch
found two things in Bepu that this engine has the same code for. A convex manifold's friction is
shared among its contacts, so a box on four corners slid a quarter as rough, which `ed0f3aa6` mended
here by scaling the coefficient by their count. And a body that has rested long enough to be a
candidate for sleep is put to sleep at the next step's start though `SetLinearVelocity` or
`ApplyImpulse` gave it speed, since `Awake = true` on an awake body clears nothing, which item 6
checks here. No verdict is open.

Before them, shadowed lights past the slots there are room for are ranked by the light that reaches
the eye, a light's brightness over one plus the square of its reach's distance, with the reach for
ties, and the flat API's remarks say what is so (`545189cc`). `coverage.py --check` holds the
comparison page's two tables and two counts to its list, in the build workflow beside the examples
table's check, and a page broken on purpose failed it (`ab1a218e`). Five commits of moves alone
split the offscreen render tests, the model renderer and the physics tests and give fifteen public
types files of their own names, so N 1.2's list stands at 101 from 116 and N 1.3's at 2 from 6, and
two probe tests stop sleeping for a worker the GPU filter replaced, so N 3.3 leaves out 9 from 10
(`a96ed25a` to `f8b6a65b`). N 1.5's three rows of AGENTS.md wait for the owner's word in the working
session, asked for there. No verdict was open.

Before them, the environment map is filtered on the GPU by the probe's stages, from a cube that
weighs each direction by its solid angle, a 4096 map in 10 ms where it took 917, with a test holding
a cap of light at the zenith and one on the horizon to the same mean at every mip, which the old
filter fails from mip 1 (`c5b4c7d9`). A script compiled again is swapped in between frames and
carries its components and resources onto its new types by their fields, where the swap on the
compiler's thread could skip a system or run one twice (`2d506d4b`). Four more of raylib's functions
are carried, 506 of 619, and each of the 113 left has its line on the comparison page (`63f0fc30`).
A probe refreshes every so many seconds while it stays ready (`74e1827a`), a texture and a sampler
declared apart are laid out and bound as such (`b0386c1e`), and STYLE.md's checks are run over the
tree, with a nullable warning that reached `main` under an incremental build mended (`2b39ddd2`),
and a probe's capture draws the frame's particles after its meshes, so a fire glows in a room's
metal (`d46c829a`). No verdict was open.

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
6. **A resting body given speed moves** (SHARED.md). BevyCSharp found a body that has rested long
   enough to be Bepu's candidate for sleep put to sleep at the start of the next step though
   `SetVelocity` or `ApplyImpulse` gave it speed, since Bepu decides sleep from the step before and
   `Awake = true` on an awake body clears nothing. `PhysicsWorld.Bodies.cs` sets `Awake` the same
   way, so a test lets a crate rest past the steps the sleep threshold asks, gives it 3 a second by
   each call, and reads it moving in the next step. Where it sleeps instead, the body's candidacy is
   cleared where its velocity is set, its `Activity`'s candidate flag and count of steps under the
   threshold, and the test holds it.

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

**Now 5, the pictures against raylib's own programs, the measure and its first module.**
`build/raylib-bench/compare.py <group or example>...` builds raylib's example from the checkout
`examples-table.py` reads, against raylib as `run.sh` builds it with its SDL3 backend, with
`PLATFORM_DESKTOP` defined so a shader example loads its GLSL 330 shaders, and with `shim.c` linked
around `EndDrawing` by the linker's `--wrap`: at the frame named, it draws the batch, takes raylib's
own screenshot and ends. This engine's picture is `capture-example.sh`'s, as the README's is taken,
and `shot` answers with the number of the frame it captured, the time counted at the top of the
frame it is asked in, so raylib's program is run to the same frame. A pair is compared as the
reference frames are, a pixel apart past 24 of 255 in a channel, and the share goes into
`3DEngine.Examples/measured.tsv`, which `examples-table.py` writes into an `Apart` column of the
table. raylib draws with one sample unless a program asks for four, and edges smoothed here stood
for most of the difference of the first pair, 2.1% apart at four samples and 1.3% at one, so
`--samples N`, or `E3D_SAMPLES`, sets the samples a window is drawn with where its program asks for
none, the program's flag or `SetConfigSamples` still deciding, and the measure runs at one.

The 45 shapes examples are measured: 24 within the reference frames' 2%, and 21 past it, from 2.1%
to 39.2%, `shapes_top_down_lights` the furthest, driven by a click here before its picture where
raylib's program has no input, then `shapes_outlines_testbed` at 15.7%, `shapes_pie_chart` at 12.9%
and `shapes_digital_clock` at 11.2%. Text drawn in ImGui's font where raylib draws its own, which
the comparison page keeps, is part of every share. Each of the 21 is taken down next, the largest
first, to a fault mended or a line of the page. The suite: 1,346 passed, 0 failed, 1 skipped.
