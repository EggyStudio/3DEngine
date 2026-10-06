# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `ac774ac9`, whose run the owner pushed and all three systems pass, Linux and Windows
1,409 each and macOS 1,389 with 10 skipped, so Verdict 25 is settled and 5.1 is packable, the first
green run on every system since the page. Verdict 25's cause was found and is not what the verdict
guessed: the system was `GeneratorAttributeTests`' probe, which the test loaded with `Assembly.Load`
into the process's own context, so its module initializer put its registration on
`GeneratedBehaviors`' process list, which skips collectible assemblies alone, and every later app
with `EcsPlugin` ran the probe's systems, the state tests' bare apps among them, in an order of test
classes that differs by system. The probe loads into a collectible context as a script does, the
test builds a bare app and finds none of the probe's systems in it, as `ScriptGenerationTests` does
after a first generation, and a throwing system is logged with its assembly (`a7842cd4`). The page
counts a repeated line at warning, error or fatal or with no level and leaves the section out when
nothing repeats, with a test on 2,190 banners and on one repeated error, as the owner asked
(`ac774ac9`), and AGENTS.md's table names `docs`, `games` and `templates`, N 1.5's list empty, the
owner having allowed it in the working session (`01f97324`). Items 4's rows and 6 are settled.

Before them, the owner pushed `98f6d8e5`, whose run read: Linux and Windows pass, 1,403 each, and
macOS passes the leak test, so Verdict 24 is settled, and fails three `StateTests` under N 3.7, a
script generation's Startup system throwing for a `Time` no bare app has, 18 times, which became
Verdict 25. Verdict 24's mend: `Shutdown` joins the threads an app's parts start through an
`AppThreads` resource, naming in the log any not done within two seconds, the console's server
closes the connections still open as it stops, the asset server's sixteen workers run on the pool
where each was a long running task's thread never joined, and the headless leak test carries the
heap after every tenth app and the threads alive after each `Shutdown` in its assertions, so the
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

The norm has 43 rules, and this engine stands at 32 checked, 2 with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What the next page says.** The run of `ac774ac9` passed on all three systems, and 5.1 is the
   owner's to pack. Each push's run is read by the reviewing session, and a failure it names comes
   first here.
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
6. **The warnings the suite repeats, read from the page.** With the banner gone the page names
   what the suite warns at every run: `CloseWindow: 1 texture(s) were still loaded` 13 times on each
   system, `Scene file: no single component is called 'Orb'` 12 times, Windows'
   `SDL_OpenAudioDevice` with no device 5 times, and on Linux 251 validation warnings that the layer
   the workflow installs does not know `VK_KHR_line_rasterization`, with macOS's 251 of a layer
   found twice on the runner. Each is either what its tests mean to provoke, and then those tests
   say so where the warning is read, or a test leaving a texture loaded or a scene naming a shared
   component by mistake, mended; and the workflow installs a validation layer that knows the
   extension, so the lines it draws are validated, and the runner's duplicate layer is silenced
   where the workflow sets the layer's path.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 25 are settled, and their numbers are not given again.

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

8. **The page's repeated lines are warnings and errors.** The owner chose it on 2026-10-06, after
   the page of `98f6d8e5` repeated the engine's banner, so the section counts what is logged at
   warning or error or with no level and is left out when nothing repeats.

## Replies

**Now 2, XM and MOD music.** `LoadMusicStream` and `LoadMusicStreamFromMemory` open tracker modules,
played by a player of the engine's own with no dependency added. It has readers for FastTracker 2's
XM and ProTracker's MOD with its kin of 4 to 32 channels and the older Soundtracker's 15 samples,
one model for both, and a player of rows, ticks, envelopes, the effect column's effects and the
volume column's, mixed in stereo at 48 kHz. It follows raylib's jar_xm in its frequencies, its
effects and its mix, and plays as FastTracker 2 does where jar_xm parts from it (a volume column
applied on every tick, ping-pong loops played forward, a sample offset counted in bytes, a retrigger
subtracting whole volumes). Against jar_xm built from the pinned checkout, `mini1111.xm` is
2,974,098 frames long in both, and its first ten seconds correlate at 1.0 with no lag, the mean
difference 1e-4 of full scale and the largest 0.2 where a note starts, which jar_xm crossfades over
8 frames and this fades out the cut note under. A MOD's channels are panned as jar_mod pans them,
left at twice right, at its loudness to three places; jar_mod counts a song about a row shorter than
where it comes round, and the XM rule is kept for both. raylib starts a module after counting its
length without resetting its channels or the tick in hand, so its first tick can carry the end of
the song, which is not copied. `build/make-test-modules.py` writes `tone.xm` and `tone.mod`, and
tests read their pitch, the volume column, a note let go, C00, the MOD's panning, a song's end at a
jump back, a seek and bytes that are no module. `audio_module_playing` is written, 2.9% apart, its
bar the audio device's as `audio_music_stream`'s is, so its row says it moves, and 220 of 222
examples are written.
