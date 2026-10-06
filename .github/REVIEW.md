# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `6336aba6`. Verdict 26's cause was a loop that never ended, a stream with no device
answering `IsAudioStreamProcessed` true for ever, mended with a test on the null backend, and a
failed capture's error carries the exit code, the script's last line and the example's last three
lines at a warning or worse (`6336aba6`); the verdict settles when a run's examples job passes the
capture. The run of `660b3bc6` failed the headless leak test on macOS again, by 6.09 MB against 5
where `ac774ac9` passed, with its threads constant through the hundred apps, which is Verdict 27.
The owner decided six things on 2026-10-06, Decisions 9 to 12 and 5.1 packed from `ac774ac9`, and
items 6 to 9 come of them. The warnings the suite repeated are gone: thirteen tests let go the
texture they left, the tests reading Summit's level register stand-ins for its components, a missing
audio device is warned of once a process, the Linux jobs fetch LunarG's layer of 1.4.363.0, which
knows `VK_KHR_line_rasterization` and found the ambient occlusion renderer never disposed and a
custom vertex stage fed instance rows it did not read, both mended, and macOS names the layer once,
the page repeating only `ScheduleTests`' meant throw (`660b3bc6`). Music opens XM and MOD modules,
played by a tracker player of the engine's own with no dependency added, readers for FastTracker 2's
XM and ProTracker's MOD with its kin and the older Soundtracker's, following raylib's jar_xm and
measured against it built from the pinned checkout: the same length to the frame, the first ten
seconds correlating at 1.0 with a mean difference of 1e-4 of full scale, and a MOD's panning and
loudness as jar_mod's to three places; `audio_module_playing` is written, 220 of 222 (`56564fe2`).
The run of `ac774ac9` passed its tests on all three systems, and its examples job, which measures
every pair against raylib's program for the first time, is still running.

Before them, the owner pushed `ac774ac9`, whose run passed on all three systems, Linux and Windows
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

The norm has 43 rules, and this engine stands at 32 checked, 2 with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdict 27 first, the macOS heap again, and Verdict 26's capture on the next examples job.**
   The run of `660b3bc6` failed the headless leak test on macOS by 6.09 MB with its threads
   constant, and `56564fe2`'s examples job is still measuring; the run after both mends shows
   whether all three systems and the examples pass.
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
6. **A render texture is drawn at the window's samples** (Decision 10). A render texture is made
   at one sample where the window has four, so a scene drawn into one and put on the screen is edged
   otherwise than the same scene drawn to the window. The owner chose on 2026-10-06 that a render
   texture takes the window's samples unless `LoadRenderTextureEx` says otherwise, the window's four
   samples and the bilinear filter staying as the page keeps them. A test holds a render texture's
   samples, and a reference that changes is redrawn with the reason (N 3.5). With it, Decision 13:
   where no audio device opens, the backend falls back to SDL's dummy driver, which takes samples in
   real time, so sounds, music and streams advance as raylib's do through miniaudio's null device,
   with a test on a machine with no device that a stream's position moves and `IsAudioDeviceReady`
   says what it says in raylib.
7. **The engine ships compiled ahead, ReadyToRun, for each platform** (Decision 11). The
   package's library is published ReadyToRun for each runtime identifier the package carries, in
   `3DEngine.csproj` and `.github/workflows/pack.yml`, so a game run from its project does not spend
   Manor's 629 ms compiling in its first frame. The package's size before and after and the first
   frame's time with and without are in the commit, and TODO.md's cost entry follows.
8. **Per-object motion blur** (Decision 12), after items 2 to 7. A velocity image beside the HDR
   frame from each entity's previous transform, ECS entities blurred by their own motion and
   flat-API draws by the camera's as today, off by default, measured on a scene that moves, with a
   reference redrawn for it and a test of a moving entity's trail.
9. **ImGui viewports** (Decision 12), last. An ImGui window dragged outside the main window gets
   an SDL window and a Vulkan swapchain of its own, through ImGui's viewport interface, off by
   default, with a test that a viewport's window is made and closed and the editor's panels checked
   by hand.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 25 are settled, and their numbers are not given again.

26. **The examples job of `ac774ac9` fails at `audio_spectrum_visualizer: the capture failed`,
    with no reason given.** The tests passed on all three systems, and the job that captures every
    example and measures the pairs failed after 23 minutes on that one capture, the first run of the
    job since the measure joined it. Two things. The capture step says what failed and why: the
    example's exit code and the last lines of its log that are warnings or errors, in the
    `::error::` line, as the test page carries its causes (N 6.7), since the line stands alone in
    the run and the log is 60,000 lines. And the example runs where there is no audio device, as
    raylib's does on such a machine: it draws its spectrum of silence and ends with the frame count
    the capture asks for. `moves` leaves its pair out of the measure, which is right, but a capture
    that fails is not a pair that moves. The workflow's device has no audio device, so the backend
    is disabled there, which the Windows job warns of once a process, and the example is run here
    with the backend disabled, `SDL_AUDIO_DRIVER` set to a driver with no device, to find what it
    does; the runs of `56564fe2` and `660b3bc6` say whether the failure repeats.

27. **The run of `660b3bc6` fails the headless leak test on macOS again, with its series read.**
    The GC's heap after a hundred headless apps stands 6.09 MB above the heap after twenty, 56.49 to
    62.58, against the 5 MB allowed, where `ac774ac9` passed and `0019d177` measured 6.61, and the
    process's threads stand at 31 to 33 through all hundred apps, so Verdict 24's joins hold and the
    threads are not it. On Linux the same test grows 0.07 MB, so the 6 MB is macOS's, 75 KB an app,
    and the test's threshold sits at its edge there. What a headless app still makes once an app and
    macOS lets go of later than the others is the `FileSystemWatcher` `BehaviorsPlugin` starts on
    `source/behaviors` for each extension it watches (`RuntimeAssemblyCompiler.Lifecycle.cs`), an
    FSEvents stream whose managed side, its buffers and its handle, lives until the stream is
    released on a thread of the system's, after the collection the test waits for. A directory is
    watched once a process, shared by the apps that compile from it and let go with the last, so a
    hundred apps make one stream and one watcher's worth of heap, held by a test that a second app
    on the same directory makes no second watcher; the series then says whether anything else grows.
    The 5 MB stays, N 3.5, and the heap after every tenth app rides in the message on every system,
    since the two points it carried say less than the slope would.

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

9. **`GetFontDefault` stays ImGui's ProggyClean.** The owner chose it on 2026-10-06 over raylib's
   own pixel font, which the measure had shown to be the largest part of nearly every share left.
   The comparison page keeps the font as a kept difference, and it is not raised again.

10. **A render texture takes the window's samples.** The owner chose it on 2026-10-06, of the
   three defaults the measure made visible, leaving the window's four samples and the bilinear
   filter as they are, as page lines.

11. **The engine ships ReadyToRun for each platform.** The owner chose it on 2026-10-06, a few
   megabytes a platform against the first frame's compiling in a game run from its project. The
   working session does the project and the workflow; the owner publishes.

12. **Per-object motion blur and ImGui viewports are wanted, after the standing items.** The
   owner said so on 2026-10-06, and that neither is a priority, so they are the last items of the
   list and are taken when the rest is through.

13. **Audio falls back to a device of silence where there is none.** The owner chose it on
   2026-10-06: where no audio device opens, the backend plays through SDL's dummy driver, which
   takes samples in real time, so sounds, music and streams advance on a machine without one as
   raylib's do through miniaudio's null device, rather than standing still with the backend
   disabled.

## Replies

**Now 6, a render texture's samples and audio with no device** (Decisions 10 and 13). A render
texture was already drawn at the window's samples and resolved, as the comparison page said, so the
first half is a test and the choice. `LoadRenderTextureEx(width, height, format, samples)` makes one
at one sample where it is given one, the target's upload carrying it to `CreateRenderTarget`, and
the immediate and model passes draw into it through pipelines of its own count. A test draws a
circle and an unlit model into a render texture of each kind and finds the circle's edge blended in
the window's and none blended in the other. No reference changed, since every target drawn before is
drawn as it was. The page's row says the owner's reason where it gave the pipelines' as one. For the
second half, where no audio device opens, `SdlAudioBackend` opens SDL's dummy driver, named over
what the environment names, which takes samples at the rate a device would play them, as raylib's
goes to miniaudio's null device, and warns once a process that it has. `IsAudioDeviceReady` answers
whether the backend opened a device, true on the dummy one, as raylib's is on its null device. A
test names a driver that is not there and finds the backend on `dummy` with a stream's queue going
down, waiting on SDL's thread, which N 3.3's list now names with that reason. The test of a backend
that opens nothing forces the same missing driver with the fallback off, so it runs on every machine
where it ran on none with a device, and the attribute that skipped it is gone. With
`SDL_AUDIO_DRIVER` naming a driver that is not there, `audio_spectrum_visualizer` now draws the
song's spectrum, as raylib's program does on its null device.

**Verdict 27.** A directory of scripts is watched once a process. `DirectoryWatches` holds one
system watcher for each directory and filter, shared by every compiler that watches it, tells each
of them of a change, each compiling its own app's scripts, and stops with the last to let go, and
`RuntimeAssemblyCompiler` takes a place among those watching in place of a watcher of its own. A
test opens two apps on one directory and finds the second told by the first's watchers with none of
its own, the watchers staying for the app still open and going with the last. The scripts' reload
and generation tests pass as they did. The leak test's message carried the heap after every tenth
app already, on one line with the threads after each of the hundred, which the page cuts at 240
characters, so the series that reached the page was two points. The heap and the threads are now on
lines of their own, the threads read after every tenth app as the heap is, and a failure forced here
shows both whole on the page. The 5 MB stays. The suite passed 1,427 with none skipped.
