# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `5107f9a5`, which gives the window program that was not raylib's
`core_window_flags` a name of its own, `core_window_toggles` (N 5.1), and has raylib's row and
the two of high pixel density as missing the flags `ConfigFlags` lacks. The table stands at 45
written, 139 that can be and 37 missing.

Before it, four commits were read and their four replies settled up to `99b9c97d`, and with
them Verdicts 14 and 15 and the item on N 3.7.

The tests run through `build/test.py` (`42b162d9`). The suite runs whole under `--blame` and a
hang timeout, held to 40 minutes and 4 GB, and in parts only after a process is lost. Each run
ends its log with a page of at most 200 lines, which is the job's summary and its annotations,
and a last job joins the three systems. `TestScriptTests` holds the page to its limits, and the
loss of a process to its parts with a stand-in for `dotnet`. The profile of a headless app ends
its frame (`92d30bbd`), a system that throws every frame is logged whole once and counted after
(`c35472ba`), and a test fails for an error the engine logs that it did not say it expects
(`99b9c97d`), which found a fault at once, a physics world disposed twice. The suite through the
script is 1,193 passing at 1.8 GB.

The run of `92d30bbd` is the first with the page, and it was read from GitHub with nothing
pasted. Linux and macOS pass. Windows fails 126 tests of ten causes, and the first is that no
Vulkan device starts there at all, which Verdict 16 has with the rest. The job that joins the
three pages was given no runner in that run or the one before it and ended cancelled after a
quarter of an hour, which is GitHub's and is watched.

Before them, two commits of ports were settled up to `4be25c8e`, four more of raylib's core
examples. The viewport's port found three
defaults that answer otherwise than raylib's, a window's four samples, a render texture drawn at
the window's samples, and a texture's bilinear filter. The comparison with raylib has each, and
which of them follows raylib's is put to the owner. Before those, eight commits were settled up
to `48fbb663`, Verdicts 10 and 12 among them, the Linux job passing since `c06ec659` read the
script compiler's references once for the process.

The norm has 43 rules, and this engine stands at 31 checked, 3 with places listed, none to take
and 9 by review.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Windows, by its page**, Verdict 16 and then Verdict 17, before the next port. The page
   names the 126 for the first time, and 117 of them are one line of the workflow, the driver's
   manifest named where an elevated loader reads it. A package waits for it too, since the pack
   workflow runs these tests first and 5.1 is not packed. Verdicts 18 and 19 are small and
   follow.
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
   how many rows each holds, as BevyCSharp takes its gaps. Verdict 16 comes before the next
   port.

   Two things go with the ports. A program of this engine's own that answers a raylib example
   under another name takes raylib's name once it is read against raylib's source (N 5.1), as
   `shapes_basic_2d` may be `shapes_basic_shapes`, `shapes_basic_3d` `models_geometric_shapes`,
   `audio_sound` `audio_sound_loading` and `models_terrain` `models_heightmap_rendering`. And a
   call that answers otherwise than raylib's of the same name, where the difference is kept, is
   a line on `docs/compared-with-raylib.md`, in a table of its own a port adds to, the first
   being a trigger's axis, from 0 at rest here and from -1 in raylib, which docs/input.md says
   and the comparison does not.
3. **What the trimmer cannot follow in the library** (N 2.5). The native publish warns that the
   library has code the trimmer cannot follow, which Pusher does not reach and another game may.
   The library is marked `IsAotCompatible`, which turns the same analysis on in every build, and
   each warning is mended where a generator can register what was reflected on, or said at its
   place with the reason it is safe, so the build is clean and `-warnaserror` holds it there.
   AssimpNetter's own warnings are the package's and are said once, where the reader calls it.
4. **A probe filtered on the GPU** (TODO.md, Probes capture once and on the CPU), so a capture
   costs a frame's worth of GPU and no readback, which recapturing on a light's change made
   worth having.
5. **C# typed at a running program** (TODO.md, The command line has no evaluator), which this
   engine's own list names: an `e3d eval` that compiles a line or a file against the running
   world through the script compiler already there, for looking at and changing a game while it
   runs.
6. **TODO.md's order** for everything else, the Scenes entry on a program's own spawn among it,
   and another game only when it is of a kind that uses what none of the seven has.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 15 are settled, and their numbers are not given again.

**16. On Windows no Vulkan device starts, and 117 tests fail saying that one did** (N 6.2). The
page of `92d30bbd` has ten causes for the 126. Seventy are `OffscreenRenderTests` finding no
renderer in the app, 47 are captures that were never written, and one is the driver's own
answer, `VkException [-9] ErrorIncompatibleDriver` at `GraphicsDevice.CreateInstance`. The
seventy say `the probe started a Vulkan device`, and it did not. With `E3D_REQUIRE_VULKAN` set,
`Needs.cs` runs the drawing tests where the probe failed, so the words are wrong in the one
place they are read.

The loader finds no driver. `test.yml` names lavapipe's manifest to the loader with
`VK_DRIVER_FILES` and `VK_ICD_FILENAMES`, and the validation layer's folder with
`VK_ADD_LAYER_PATH`, and the Vulkan loader leaves those variables unread in a process that runs
with an administrator's rights, which a job on a hosted Windows runner does. The loader's
documentation says so under its caveats for elevated privilege, and with `VK_LOADER_DEBUG` set
to `error,warn,driver` the loader says so itself in the output. The Windows job registers the
manifest where an elevated loader reads it, as a value of `HKLM\SOFTWARE\Khronos\Vulkan\Drivers`
named by the manifest's path with the number 0, which is one `reg add`. The SDK's installer
registers its layer the same way under `ExplicitLayers`. If the next page still has the
driver's answer, the loader's own lines go into the output, and the page's repeated lines say
what it read.

A drawing test that runs because the variable is set, where the probe failed, fails with the
probe's own error, the exception the probe caught being kept beside its answer. The page then
has one cause that reads as the driver's words, and not 117 that read as the app's.

The other causes, each a test or two:

- `FirstGameTests` finds no block of code in the page it walks, where it expects more than ten.
  The repository has no `.gitattributes`, so git on Windows checks the page out with CRLF ends,
  which is the likely reason. `* text=auto eol=lf` gives every system the same lines, and the
  test's own reading of the page is looked at with it.
- A `cut-short` `.ogg` cannot be removed because the test host holds it, so the loader that
  refuses a file cut short leaves it open (N 2.9). A loader is held to letting go of its file
  after a refusal as after a load, with a file cut short for each loader.
- Two tests find Dear ImGui's context held by an app that has not shut down, one has validation
  errors, one an `ArgumentOutOfRangeException` for a length of -1, one `Sequence contains no
  matching element`, and one a file that does not exist. They may follow from the missing
  device, and the page after the mend says which are left. Verdict 17 gives each its tests.

**17. The annotations are all of a page that is read without signing in, and they carry a
cause's first line only** (N 6.7). GitHub gives a run's annotations to anyone, and its log, its
summary and its files to those signed in, so the reviewing session and a working session read
the annotations and nothing else. `annotations()` writes a cause's count and type as the title
and the first line of its message, so the run of `92d30bbd` says that one test failed for a
file that does not exist and does not say which. Each cause's annotation carries its whole entry
of the page, the message, the frames and the tests with the count of the rest, its lines joined
as the script's `escape` writes them, and one notice carries the page's head and the lines
repeated most. GitHub keeps ten errors and ten notices of a step, so ten causes and the notice
fit. `TestScriptTests` reads the annotations the script prints for its 500 failures and finds a
test's name and a frame in each.

**18. The hook of N 3.7 hears by the thread, and a test that awaits leaves its thread** (N 3.7).
`FailOnLoggedErrors` keeps a test's ears in a `[ThreadStatic]` field that `Before` sets, and
`App.Created` lays an app to the ears of the thread that makes it. A test that awaits goes on
on another thread of the pool, which has no ears, or those of another test that awaited and is
still running. An app made after an `await` is then laid to no test or to the wrong one, and
the wrong one fails for an error it did not cause. 33 tests in seven files are `async`, and
`CliTests` among them makes apps. The ears go in an `AsyncLocal`, as `App.Current` is one, so
they follow a test over its awaits. `LoggedErrorsTests` gains a test that awaits before it
makes its app and logs, which fails for the error where it is not expected, with no other test
failing beside it.

**19. The schedule's count of what was thrown keeps a script's type** (N 3.1). `_thrown` is keyed
by stage, system and the exception's `Type`, and lives as long as the schedule. An exception a
script defines and a system of that script throws is then a key, and a `Type` that is referred
to keeps its assembly's load context from being unloaded, so that generation of the script
stays for the app's life, where `ScriptLoadContext` is there to let it go. The key is the
type's full name. A test compiles a script whose system throws a type of its own, compiles it
again, and finds the first generation unloaded.

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

**Verdict 16, Windows's device.** The Windows job registers lavapipe's manifest under
`HKLM\SOFTWARE\Khronos\Vulkan\Drivers`, its path the value's name and 0 its value, in a step of
its own before the build, where an elevated loader reads it. Where `E3D_REQUIRE_VULKAN` is set and
the probe started no device, a test marked to draw fails before it runs with the probe's own
exception, kept by the probe beside its answer (`RequiredDevice`, a hook beside the others in
`Needs.cs`). Run here with the driver's manifest named to a file that is not there, the loader
gives the Windows run's answer, and all 19 drawing tests tried fail with `No Vulkan device
started, which E3D_REQUIRE_VULKAN requires. The probe's error: VkException: [-9]
ErrorIncompatibleDriver`, one cause on the page.

`.gitattributes` gives every text file LF ends in every checkout, which the index already has
for all of them, and `FirstGameTests` reads its page with any ends as well. Music whose file holds
no sound closed nothing, as one cut short does, so the `.ogg` stayed open behind the refusal. It
is closed there now, and music closed is not valid, where its validity read the closed decoder
and threw. `FileHandleTests` cuts a file of each loader's kind to its first third, nine of them,
and finds each let go after the load, which failed for music alone without the mend. The suite
through the script passes, 1,202 tests. The rest of the 126 wait for the next page.

The reflection probe's reference frame fails here now and then when its class runs alone, 82.9%
of its pixels off with the room lit dimmer, in two runs of three at `5107f9a5`, one in four at
`c06ec659` and `71224507`, and none in four at `d1667840`. It has passed in every run of the
whole suite. It is taken up after Verdicts 17 to 19.

**Verdict 17, the annotations.** Each cause's annotation carries its whole entry of the page, the
message, the frames and the tests with the count of the rest, a line each, and a lost process's
carries its account, its tests and its last lines. One notice carries the page's head and the
lines the output repeated most, and the digest job's carries each system's head. The page and
the annotations build a cause's entry the one way. `TestScriptTests` reads what the script prints
for its 500 failures as a run on GitHub would, ten errors each with a frame and a test's name and
one notice with the head and the 60,000 repeated lines, and finds the page in the summary file.

**Verdict 18, the hook's ears.** They are an `AsyncLocal` now, which `Before` sets in the flow
xUnit runs the test in, since it calls `Before` from a method that is not `async`, so they follow
a test over its awaits and into the threads and tasks it starts. An app is the test's when its
flow made it, and a thread the test starts is the test's. `LoggedErrorsTests` gains an awaiting
test that makes its app on a thread of the pool and logs from a thread the app started, judged by
a hook of its own, and one through the hook on every test, which expects its error. Both failed
with the ears bound to a thread. Another test's app is made on a thread started with the flow
suppressed, which inherits nothing. The whole suite passed beside them, but for N 3.3 finding
`Task.Delay` in the two, which await `Task.Yield` now.

**Verdict 19, a script's generation kept.** The schedule's count of what was thrown is keyed by
the type's full name. `ScriptGenerationTests` compiles a script whose update system throws a type
of the script's own for twenty frames, compiles it again, and finds the first generation's load
context collected. That found a second holder, which held the generation with the key mended.
The behavior generator's module initializer runs in a script's assembly too, as its types are
first touched, and added the script's registration to the process's `GeneratedBehaviors`, where
it stayed for the process, held its generation, and was invoked by the `BehaviorsPlugin` of every
app made after, registering a stale script into an app that never compiled it. A registration
from a collectible assembly is passed over there now, the compiler registering the script into
its own app as before, and the test finds the list holding none. Each mend alone leaves the
test failing. The hook of N 3.7 keeps an exception as its type's name and message, where it kept
the exception, which held a script's type for the length of the test. The suite through the
script passes, 1,205 tests.
