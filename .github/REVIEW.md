# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `4be25c8e`. Two commits of ports were read and their replies settled, four more of
raylib's core examples (`a77f6e5c`, `4be25c8e`), each held against raylib's C at the pinned commit
and carrying its numbers and its words, with the table at 46 written and 141 that can be. The
viewport's port found three defaults that answer otherwise than raylib's, a window's four
samples, a render texture drawn at the window's samples, and a texture's bilinear filter. The
comparison with raylib has each, and which of them follows raylib's is put to the owner.

Before them, eight commits with six replies were settled, up to `48fbb663`.

Verdict 12 is settled (`c06ec659`). The growth was the script compiler's references, read at
every app's start and held in native memory until a finalizer ran, and they are read once for the
process at the first compilation. `AppLeakTests` holds a hundred apps to what ten held, read
before any collection, and it found in the whole suite a fault no part of the suite would have
shown, a test of another area keeping every later app. The runs of `71224507`, `fd7b17f3` and
`48fbb663` passed on Linux, the job taking two and a half minutes, where the three runs before
them ended at the runner's memory.

With it, the test of removals reads its tick before the removal (`6c458f8c`). N 7.2's check
leaves out the owner's setting of the version and reads a list for the owner's other commits
(`6b3ee413`), `5d88a601` being the owner's own change to AGENTS.md. rcamera's twelve functions
are carried, `UpdateCamera` is rcamera's, and two more of raylib's examples are written
(`71224507`). Verdict 10 is settled with two more rows than it named (`30158c83`), the table
standing at 42 written, 145 that can be, 34 missing and 1 that does not apply. The check of
scripts more than one system runs finds nothing (`fd7b17f3`), so the norm has it as N 6.6. Its
cell names `ScriptTests`, which stays where it is. And every method native code calls catches
every exception (`48fbb663`), six of twelve mended, with N 2.10's test finding them by how they
are handed over.

Windows is the one system that fails. Its test step has failed in each of the seven runs since
its job began to draw, `48fbb663` the last read, and no run has named a test. A run shows one
annotation without its log, `Process completed with exit code 1`, the log is 60,000 lines, and
that log pasted into the reviewing session on 2026-10-05 ended it. The owner decided the same
day how a run says what failed, which is Verdict 15 and comes before the next port. The reviewing
session reads a run's jobs and annotations from GitHub and is given no log.

The norm has 43 rules. The owner took N 3.7, N 6.7 and N 6.8 on 2026-10-05, and N 6.6 came with
its check. This engine stands at 28 checked, 3 with places listed, 3 to take and 9 by review.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What a run says**, Verdict 15 and then Verdict 14, before the next port. Windows has failed in
   every run since its job began to draw and no run has named a test. The script and the page of
   Verdict 15 come first, since the first run with them names the 126. Verdict 14 is one line
   and a test, with the schedule counting what it has said once.
2. **A test in which the engine logs an error fails, unless the test says it expects that
   error** (N 3.7), which the owner took into the norm on 2026-10-05. The error Verdict 14 names
   was logged in every frame of every headless test for thirteen hours and failed none. The
   test project hears what the engine logs, as `SpyLoggerProvider` does in `LoggerTests`, and a
   test during which an error was logged fails with the first error's text, as a test that
   draws fails for an error of the validation layer. A test of a failure says which error it
   expects, by its category and a part of its message, and fails if that error does not come.
   Tests run side by side and the log is the process's, so an error has to be laid to the app
   that logged it, and how is the first thing to find. A test whose errors cannot be laid to it
   goes on the rule's list with that reason. The tests that log an error today go on
   `build/norm/3.7.txt` as places to mend, each read for whether its error is a fault of the
   engine's or the thing the test is about.
3. **raylib's own examples, one by one, as the measure** (N 5.2). `coverage.py` counts raylib's
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
   how many rows each holds, as BevyCSharp takes its gaps. Verdict 15 comes before the next
   port.

   Two things go with the ports. A program of this engine's own that answers a raylib example
   under another name takes raylib's name once it is read against raylib's source (N 5.1), as
   `shapes_basic_2d` may be `shapes_basic_shapes`, `shapes_basic_3d` `models_geometric_shapes`,
   `audio_sound` `audio_sound_loading` and `models_terrain` `models_heightmap_rendering`. And a
   call that answers otherwise than raylib's of the same name, where the difference is kept, is
   a line on `docs/compared-with-raylib.md`, in a table of its own a port adds to, the first
   being a trigger's axis, from 0 at rest here and from -1 in raylib, which docs/input.md says
   and the comparison does not.
4. **What the trimmer cannot follow in the library** (N 2.5). The native publish warns that the
   library has code the trimmer cannot follow, which Pusher does not reach and another game may.
   The library is marked `IsAotCompatible`, which turns the same analysis on in every build, and
   each warning is mended where a generator can register what was reflected on, or said at its
   place with the reason it is safe, so the build is clean and `-warnaserror` holds it there.
   AssimpNetter's own warnings are the package's and are said once, where the reader calls it.
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

Verdicts 1 to 13 are settled, and their numbers are not given again.

**14. `FrameProfile` throws in every frame of an app with no device, and the log says so every
frame** (N 3.1). `Measure` asks `renderer.Context.Graphics` at line 178 of `FrameProfile.cs`,
which throws where the renderer is there and not initialized, and that is every headless run
since `433c7868`. The schedule logs the exception and goes on, so a headless run's profile never
ends its frame, each frame pays for an exception and its trace, and the log of a test run is
mostly this error, 60,000 lines on Windows. `IsInitialized` is asked first. A test runs a
headless app for some frames and finds its profile holding them and no error logged.

The other half is the schedule's. A system that throws in every frame writes its trace sixty
times a second, into a player's log file as into a test's. `Schedule.Helpers.cs` logs a system's
exception in full the first time it throws in a stage and counts after that, saying the count in
one line when it reaches 10, 100, 1,000 and so on, and once more when the app closes. An
exception of another type from the same system is a first of its own. A test throws from a
system for a thousand frames and finds one trace and a handful of lines.

It stood for thirteen hours with every headless test passing over it, since a system that
throws is logged and the test goes on. The owner decided on 2026-10-05 that such a test fails,
which is N 3.7 and item 2.

**15. A red run names nothing, and a process that is lost leaves nothing** (N 6.7, N 6.8). The
Windows test step has failed in each of seven runs, 1,033 passed, 126 failed and 6 skipped in
the run of `aae58f45`, the last whose count was read. Such a run shows one annotation to anyone
who does not open its log, `Process completed with exit code 1`, and the log is 60,000 lines. On
Linux the three runs that ended at the runner's memory left no results at all, the runner going
with the process. `test.yml` gives its jobs no time limit, so a test that hangs runs for six
hours. The owner decided on 2026-10-05 how this is mended.

One script runs the tests, `build/test.py`, in Python as `build/soak-check.py` and
`build/examples-table.py` are, on every system and for a working session, and the three
`dotnet test` steps of `test.yml` become it.

- It runs the suite whole, as one process, with the results file the step writes today,
  `--blame` so a process that is lost names the tests it was running, and `--blame-hang-timeout`
  so a test that hangs is ended and named, with no dump taken (`--blame-hang-dump-type none`).
  `dotnet test` has these, and no package is added. What the process prints goes to a file
  under `TestResults/`, and the step's log gets a line for each process and the page.
- The process is held to a time and to a memory, well above what the suite takes, 1.4 GB at its
  most here, and well under what a runner has, so 4 GB would do on all three. A process that
  grows is then ended while the runner can still say so. The script watches the process where
  the system lets it, or the test host ends itself with a line saying how much it held,
  whichever holds on all three systems.
- A process that ends by itself, passing or failing, is read from its results file, and nothing
  runs twice.
- A process that is lost, by a crash, a hang, its time or its memory, is said first on the page:
  after how long, holding how much, in which tests, with what exit code, and the last lines it
  printed. Then the suite runs again in parts, each a process of its own under the same limits.
  The parts are read from `dotnet test --list-tests`, one for each name after `Engine.Tests.`
  that holds thirty tests or more and one for everything else, whose filter is the negation of
  the others, so no test falls between two parts. A part that is lost costs its own tests and
  no other's.
- The page is at most 200 lines of at most 240 characters, whatever happened. Its head says the
  system, the commit, how many tests passed, failed, were skipped and have no result, how long
  it took and the most memory held. The failures follow by cause, the most frequent first and
  ten at most. A cause is the exception's type, the first line of its message with numbers and
  paths taken out, and the first frame that is the engine's. Each has its count, its message,
  its first six frames that are the engine's or a test's, and four of its tests by name with a
  count of the rest. After them come the three lines the output repeated most, each with its
  count, so a system that throws every frame shows as one line.
- The page ends the step's log, between two lines that mark it, so the end of a log is the
  page. It is the job's summary, with a cause an error annotation, of which GitHub keeps ten a
  step. And it is `TestResults/digest.md`, with the same as `digest.json`, uploaded from every
  run as `test-digest-linux` and so on, a few KB. The results files and the output are uploaded
  when a job fails, as they are today.
- A last job of `test.yml`, `digest`, runs whether the three passed or not, takes their three
  small files and writes one page as its summary and annotations: a line for each system, then
  each cause with the systems it was seen on, a cause seen on two being one entry. It is the
  one place that says where a commit fails.
- Each job has `timeout-minutes`.

The script has tests of its own, which run with the suite and need no `dotnet`: a results file
of 500 failures of 12 causes beside an output of 100,000 lines gives a page within its limits
that names each of the first ten causes, and a process standing in for `dotnet test` that
hangs, one that grows and one that dies each give the lost process on the page and the parts
run after it. The path taken after a loss runs on no green day, so these tests keep it working.

A working session runs the tests through the script as well, and reads a page.
`python3 build/test.py Rendering` runs one part, and `--parts` all of them. AGENTS.md names
`dotnet test 3DEngine.Tests` and changes on the owner's word in that session, which the owner is
asked for once the script is in.

Then the failures on Windows, which the first run with the script names. 126 is close to the
count of tests that draw, which were skipped on Windows until its job was given a device, so one
cause is likely, in how the device under lavapipe on Windows starts or in what the validation
layer says of it there. The reviewing session reads the page from GitHub and asks for nothing
pasted.

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

**Verdict 15, the run's page** (N 6.7, N 6.8). `build/test.py` runs the suite in the three jobs
and for a working session, as one process with the results file, `--blame`, a hang timeout of five
minutes with no dump, and the process held to 40 minutes and 4 GB. The script reads the largest
process under it, from `/proc` on Linux, `ps` on macOS and the process snapshot on Windows, and
ends the tree at a limit. What the process prints goes to `TestResults/output.txt`, and the log
has a line for each process and the page, between two marking lines at its end. A process that
is lost is said first, after how long, holding how much, in which tests the blame collector names
or after the last test to end, with its exit code and the last lines it printed before
`dotnet test`'s own account of the loss. Then the suite runs again in parts from `--list-tests`,
twelve areas of thirty tests or more and everything else, whose filter negates the twelve. The
page has the counts, the failures by cause, ten at most, each with its message, six frames and
four tests, and the three lines the output repeated most, within 200 lines of 240 characters. It
is `digest.md` and `digest.json`, the job's summary, and a cause an error annotation. Each job
uploads its page from every run, a last job `digest` writes one page of the three, each cause
with its systems, and each job has a time limit.

`TestScriptTests` runs the script with no `dotnet`. A results file of 500 failures of 12 causes
beside 100,000 lines of output gives a page within its limits naming the first ten, and a stand-in
for `dotnet` that hangs, grows or dies where the suite would run whole is said lost by its time,
its memory or a crash, after which the three parts run and pass. Against `dotnet test` itself, a
test that crashed the host and one that hung were each named. The suite through the script here
is 1,188 passed and 1 skipped in 1 m 55 s at 1,439 MB, and its page's most repeated lines are
Verdict 14, 5,802 times each. AGENTS.md still names `dotnet test 3DEngine.Tests`, and changes on
the owner's word.

**Verdict 14, the profile of a headless app.** `FrameProfile` asks for the device's waits only
once the renderer's context is initialized, so a headless app's profile ends each frame and logs
nothing. `FrameProfileHeadlessTests` runs one for five frames and finds them in its profile and no
error of the profile logged, and without the mend found none of the five. Before it, the run of the
whole suite through `build/test.py` repeated the error and its exception 5,802 times each.

**Verdict 14, the schedule's half.** A system's exception is logged whole the first time that
system throws that type in that stage, and counted after, with a line at the 10th, the 100th and
each power of ten, and the app says each total as it closes. `ScheduleTests` throws from a system
for a thousand frames, a different type every 250th, and finds two traces and four lines, the
counts at 10 and 100 and the totals of 996 and 4 at shutdown. The logger's extra providers are
replaced whole when one comes or goes, where a list was iterated while another thread could add
to it, and a test can take its spy off again (`RemoveProvider`). The whole suite through the
script passes, 1,190 tests, and its most repeated lines are the startup banner of 555 apps.

**Now 2, N 3.7.** A test during which the engine logs an error it does not expect fails, with the
first error's text, from a hook the test project puts on every test (`FailOnLoggedErrors`, an
assembly's `BeforeAfterTestAttribute`, whose `After` fails the test as a test that draws fails for
the validation layer). An error is laid to its test by the app that logged it. The app sets itself
as the current app of the flow that made it (`App.Current`, an `AsyncLocal`), which the threads
and tasks it starts inherit, the hook takes an app made on a test's thread during the test as that
test's (`App.Created`), and the log raises each error with its category (`Log.ErrorLogged`). An
error logged on the test's own thread is the test's too. What a class's constructor makes logs on
other threads, and what its Dispose logs, comes after the test is judged and is not read. A test
of a failure names the error it expects, `[ExpectsError(category, part)]`, and fails where it does
not come. `LoggedErrorsTests` holds the hook to an error on the test's thread, one on a thread the
test's app started, one from another app's, and an expected one present and missing.

The survey of the whole suite found 19 tests logging an error, which leaves `build/norm/3.7.txt`
with nothing on it. Ten are tests of a failure whose error is their subject, and say so: the two
of a plugin's missing dependency, a world disposing a resource that throws, the schedule's
counting, the renderer on `NullGraphicsDevice`, the bad files, three of the logger's own, and
ImGui's second context. Nine were a fault, `PhysicsWorld` throwing from Bepu when disposed a
second time, which a program does where it disposes a world it also put in the app's. It is
disposed once now, and without the mend the hook fails all nine with the error's text. The suite
through the script passes, 1,193 tests.
