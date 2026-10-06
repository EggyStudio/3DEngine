# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `a7d7e1e2`. The owner pushed `98f6d8e5`, and its run: Linux and Windows pass, 1,403
each, and macOS passes the leak test, so Verdict 24 is settled, and fails three `StateTests` under
N 3.7, a script generation's Startup system throwing for a `Time` no bare app has, 18 times, which
is Verdict 25 and comes first. Verdict 24's mend: `Shutdown` joins the threads an app's parts start
through an `AppThreads` resource, naming in the log any not done within two seconds, the console's
server closes the connections still open as it stops, the asset server's sixteen workers run on the
pool where each was a long running task's thread never joined, and the headless leak test carries
the heap after every tenth app and the threads alive after each `Shutdown` in its assertions, so the
next macOS run says what is left, the runtime's file watcher on `source/` the one thing a headless
app still leaves (`27f949bf`). Particles can bounce off or end at the window's depth of the scene,
off unless set, the depth of the shadow casters drawn at half size, measured at 0.02 ms of CPU and
0.009 ms of GPU on the particles example, whose sparks bounce off the ground (`f13cab78`). Three
commits of moves and one mending the console's tests' clock take N 1.2's list to 83, N 1.3's to 1
and N 3.3's to 6 left out (`e2780345`, `93615075`, `98f6d8e5`). Since then item 6 is settled: the
build workflow measures every pair against raylib's program after its captures, against
`measured-ci.tsv` from the job's own device, failing where a pair stands more than a point above its
share or draws no frame, the eight pairs that move by the clock or the audio device left out with
their reason (`6e87257f`); and fonts draw their color emoji, read by the engine's TrueType reader
from PNG bitmaps or colored layers, with two generated test fonts and a GPU test, sequences, COLR
version 1 and sbix kept in TODO.md (`a7d7e1e2`).

Before them, the run of `0019d177` was read from its page: Linux and Windows pass, and macOS fails
one test, `AppLeakTests.A_Headless_App_Made_And_Closed_A_Hundred_Times_Leaves_Nothing_Behind`, 1,371
passed, 1 failed, 10 skipped, the heap after a hundred apps 6.61 MB above the heap after twenty
against the 5 MB allowed, where `cac05ded` had 5.87 and both leak tests failing, so `fb68cfad`
mended the other, which became Verdict 24. The commits: a particle emitter blends a sheet's frames
into the next over each particle's life, off by default (`9924be97`), `ParticleBlend` in its own
file, N 1.2's list at 86 (`67048763`), and `LoadTextureCubemap` makes a cube from an image's six
faces in raylib's four layouts, found by raylib's own tests, models honor `rlDisableDepthMask`, and
`models_skybox_rendering` is written, 0.2% apart, 219 examples and 516 of 619 functions
(`586670cd`).

Before them, raylib's eight automation functions were carried, each frame's input recorded as
`EndDrawing` begins into raylib's text format and played back, with the engine's own key codes,
which the page says, and `core_automation_events` is written, 1.0% apart (`3082aad5`).
`LoadImageFromScreen` reads the last frame presented, kept from its first call on so a program that
never reads the screen pays nothing, the first call giving the window's size in the clear color, and
a call inside a frame reads the frame before as well, since nothing of a frame is on the GPU before
`EndDrawing`, decided here on 2026-10-06 and kept on the page with that reason;
`core_screen_recording` is written with a GIF writer of its own, 0.2% apart, and 515 of 619
functions are carried (`53d99c7e`). Four types moved into files of their names, N 1.2's list at 94
(`0019d177`). TODO.md's cost entries are measured again: the per-entity entry keeps 410,266 entities
in 17.7 ms and names two changes with their savings, as decided, and the first-use stalls are the
runtime's compiling, 629 ms in Manor's first frame run from its project and 30 ms built native, so
the entry names packing the engine compiled ahead for each platform as the owner's to weigh
(`f9004abf`). The owner pushed.

The norm has 43 rules, and this engine stands at 31 checked, 3 with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdict 25 first, the script generation in bare apps on macOS.** The run of `98f6d8e5`
   passes on Linux and Windows and on macOS fails three `StateTests`, which the verdict takes apart.
   The run after its mend is pushed shows whether all three systems pass, and then 5.1 is packable.
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
6. **The page's repeated lines count warnings and errors alone.** The owner asked on 2026-10-06,
   the page's "Repeated most in the output" having shown the engine's banner at every app's start,
   2,190 lines of `====`, where it was meant for the error a system logs every frame.
   `build/test.py` counts the lines logged at warning or error, or lines of no level at all, and
   leaves the section out when nothing repeats, with its own test on a log of banners and one
   repeated error.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 24 are settled, and their numbers are not given again.

25. **The run of `98f6d8e5` fails on macOS alone, in three `StateTests`, under N 3.7.** Read
    from the page: 1,380 passed, 3 failed, 10 skipped, and in
    `An_Entity_Spawned_On_Enter_Is_There_For_Update_In_The_Same_Frame`,
    `An_Entity_Tied_To_A_Sub_State_Goes_When_Its_Parent_Leaves_Its_Value` and
    `An_Entity_Tied_To_A_Value_Goes_With_Its_Children_When_The_State_Leaves_It` the engine logged 18
    errors, each `System 'Stages_Generated_Startup_Startup' threw in stage Startup:
    InvalidOperationException: Resource of type Time not found`. The tests make bare apps with `new
    App()` and no plugins, so no `Time`, and that system is no code of theirs: its name is the
    runtime behavior compiler's, so it is a script generation another test compiled, reaching apps
    that never asked for it. Linux and Windows run the same tests and log nothing, 1,403 passed
    each, so the generation reaches those apps only where it lives longer, and on macOS Verdict 24
    found the file watcher's FSEvents stream let go after the collection a test waits for.
    `d7e370ed` mended a script registered into every later app once, and this is the same fault by
    another door. Two things. An app takes behaviors from the assemblies it was given and from no
    generation another app compiled, whatever is still loaded, held by a test on every system that
    compiles a script in one app, makes a bare app, and finds no system of the script's in it. And
    the logged error names the assembly a throwing system came from, so the page says where it came
    from without a Mac. The three tests stay as they are, bare apps being right for what they test.
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

8. **The page's repeated lines are warnings and errors.** The owner chose it on 2026-10-06, after
   the page of `98f6d8e5` repeated the engine's banner, so the section counts what is logged at
   warning or error or with no level and is left out when nothing repeats.

## Replies

**Now 4, N 1.5's three rows.** The owner allowed in this session that AGENTS.md be committed, and
its own text allows since 2026-10-05 a row N 1.5 asks for, so the table of areas names `docs`,
`games` and `templates` and N 1.5's list is empty. NORM.md's row for N 1.5 still says three wait for
the owner, which is the reviewer's to change.

**Verdict 25.** The system was no script's. `Stages_Generated_Startup_Startup` is the name the
behavior generator gives in a build as at run time, and `Stages` is the probe of
`GeneratorAttributeTests`, the one source in the suite with that type. That test compiled its probe
and loaded it with `Assembly.Load`, into a context that is never let go, so the probe's module
initializer put its registration in the process's list, which `GeneratedBehaviors.Add` keeps for an
assembly of the program and passes over for a collectible one, and every app the suite made after it
with `EcsPlugin` ran the probe's systems, the state tests' bare apps with no `Time` among them.
Whether the state tests come after it is the order the collections, run side by side, reach them in,
which differs between the three systems. The probe now loads into a collectible context, as a
script's generation does, so the list passes it over and the test registers it into its own app
alone, and the test then makes a bare app with `EcsPlugin` and finds none of the probe's systems in
it, which fails with `Assembly.Load` put back. `ScriptGenerationTests` makes the same bare app after
a script's first generation and finds none of its systems either. No other test loads what it
compiles. The engine's rule stands as it was. An assembly loaded where it cannot be let go is part
of the program, as a plugin a game loads is, and its behaviors go to every app, while one compiled
at run time goes to the app that compiled it alone. A system that throws is logged with its
assembly's name, `System 'X' from Y threw in stage Z`, so a page says where it came from.

**Now 6, the page's repeated lines.** `build/test.py` counts a line logged as a warning or an error,
or one with no level as an exception's message is, and passes over the lines logged at trace, debug
or info, the banner among them, and the section is left out when no line repeats, as it was.
`TestScriptTests` reads a log of 2,190 banners with a debug line beside each, whose page has no
section, and the same log with an error logged three times beside its exception's line, which the
section counts three times each with no banner in it.
