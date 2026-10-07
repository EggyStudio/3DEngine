# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `a4f31573`. Every C# block of the guides builds on the packed package in the examples
job, 138 blocks of fifteen pages: `build/docs-on-package.py` sorts a block's lines by what they
declare, types into a namespace, members into a class and statements into a method inside a loop run
once, after the lines a `<!-- compiled with: -->` comment before the fence gives, leaves out a block
marked `<!-- not compiled: -->` with its reason, and says an error at the page's line as an
annotation, with `DocsScriptTests` feeding it a good, a stale and a skipped block, which settles
item 4. Its first run found three faults: the spatial sound the audio guide teaches had been
internal since `82b1feb4` and is public again as `BehaviorSounds.PlaySpatialSound`, two lines added
to `PublicApi.txt` and none lost; the states guide and the cheatsheet named a `Screen.Menu` the
page's enum lacks; and five fragments no compiler reads are written as their code (`a4f31573`).
N 1.4 leaves out 13. The session works on, the owner having set it no limit, and Verdicts 32 and 33
come before item 5. The suite: 1,462 passed, none skipped.

Before them, Windows and macOS came to follow the README in a new project and build and run every
step of the first game after their games, each as a step of its own with its own error, the paths
written into files and variables put through `cygpath` where Git bash would mangle them, about three
to five minutes added to each job and unrun on either system until the push (`913e78e0`), which
settles item 4. TODO.md's order is through to its described limits, so items 4 and 5 are new; the
working session reached the end of its context at 06:10 on 2026-10-07 with item 4 scoped and not
begun, its plan in the item, and the next session in this repository takes 4 and then 5. The suite:
1,461 passed, none skipped. The owner pushed `913e78e0` at 09:23, and its run: Linux green; macOS
failed one test, the offscreen hundred of `AppLeakTests` with the heap up by 7 MB, which is Verdict
32; Windows passed its suite and then every game failed to open through `e3d` with no reason on the
page, which is Verdict 33; the README walk and the first game ran on neither system behind those
failures, and the examples job was skipped, so Verdicts 30 and 31 wait.

Before them, an OpenType font of CFF outlines came to draw its characters past U+FFFF from its Type
2 charstrings, CID-keyed fonts among them, checked against STIX Two Math and Noto Sans CJK and by
the scan of 444 fonts (`3c72e321`); the games' errors on Windows and macOS name the system as a
reader knows it (`eb5f95f5`); a font that joins sequences shapes a string once and draws it again
from what it kept (`d9e5bac2`); and a twentieth reference frame compares color text whole, paints,
joined bitmaps and tinted layers, TODO.md's testing entry naming the twenty (`f5a102d6`). TODO.md's
text entry is down to text shaped whole, which stays described. Item 4 is new. The suite: 1,461
passed, none skipped.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdicts 30 to 33 first, the run of `913e78e0` and the examples job.** The run of `913e78e0`
   failed on macOS in the offscreen hundred of `AppLeakTests` (Verdict 32) and on Windows in every
   game's opening through `e3d` (Verdict 33), its examples job skipped behind them, so Verdicts 30
   and 31 wait for the next examples job, and the README walk and the first game, which come after
   the games in the Windows and macOS jobs, have run on neither system. Each push's run is read by
   the reviewing session, and a failure it names comes first here.
2. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the seven has.
3. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.
4. **A page for a game moving from 5.1 to 6.0.** Every change of the public surface since the 5.1
   pack at `b43818f9`, the names raylib's took, the arguments reordered, the three names dropped and
   the fields renamed, is a line of `docs/upgrading.md` saying what a game wrote and what it writes,
   held by a test that every name `PublicApi.txt` lost since that commit appears on the page, so 6.0
   can be cut with the page beside its notes. The page names no one who decided (N 4.7).

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 29 are settled, and their numbers are not given again.

30. **The examples job of `22bbf15a` fails at the soak, and its error names no game.** Step 25,
    `Play each game a while and check nothing it holds grows`, ended with `a game grew, or could not
    be played, over two minutes`, the workflow's own line, after every game's own step had passed.
    Which of the eight, and whether it grew or could not be played, is in `build/soak-check.py`'s
    output and the soak's CSV, which the artifact holds behind a sign-in. Two things. The step's
    error names the game and the measure, the resident memory at the start and the end of its two
    minutes against the allowance, or the exit it ended with, as the test page names a failure's
    cause (N 6.7); `step.py` carries the script's last lines for that, so `soak-check.py` prints
    them. And the soak is run here in the workflow's image on four cores, where the runner draws a
    game under five frames a second, to find whether a game grows there or the two minutes of a slow
    runner tripped the play, as the first-person walk's presses did (Verdict 28), and the cause is
    mended. Settled when an examples job passes the soak.

31. **The measure's first run on the workflow's device drew no frame of raylib's for any pair.**
    The examples job's ten notices list every written example with `none`, the word `compare.py`
    gives a pair whose raylib program drew no frame, so on that device raylib's side failed whole,
    the build of its examples, Mesa's OpenGL through SDL's offscreen driver, or the shim's
    screenshot, and the measure is blind there with nothing to record, since a share of `none`
    recorded would hold every pair to nothing. Item 4 waits. Two things. `compare.py` treats a run
    in which raylib's program drew no frame for every pair as a broken measure and not a
    measurement: it fails the step, says the first pair's raylib exit code and the last lines of its
    output, and records nothing. And the cause is found in the workflow's image, where the job's
    steps were run before the measure joined them, by running one pair's raylib program there as
    `compare.py` runs it and reading what it says. Settled when a run's notices carry shares.

32. **The run of `913e78e0` fails on macOS alone, in the offscreen hundred of `AppLeakTests`.**
    Read from the page: 1,434 passed, 1 failed, 10 skipped, and the test that makes and closes a
    drawing app a hundred times found the GC's heap at 65.3 MB after the hundredth app against 58.2
    MB after the twentieth, 7.1 MB over eighty apps against a cap of 5, about 90 KB an app if it is
    a slope, the threads level at 31 to 33 throughout. Linux and Windows pass the same test, and the
    headless hundred passes on macOS, so a closed app that drew leaves something on the managed heap
    on macOS that it leaves nowhere else, and the test's two readings cannot tell a slope from a
    step. Two things. The offscreen test reads the heap every ten apps as the headless one does
    (`heapEveryTen`), so the page says whether the heap grew by an app's worth at a time or in one
    step. And the cause is found on macOS, by reading which types grew between the twentieth app and
    the hundredth, a `dotnet-gcdump` or `GC.GetGCMemoryInfo` in the job for the failing test; the
    one macOS-only thing the tests name, the context macOS's `FileSystemWatcher` keeps until
    FSEvents lets go of its stream, is a place to look and not the cause. Settled when the macOS job
    passes the test.

33. **The run of `913e78e0` fails on Windows in every game's opening, and the page says no more
    than `did not open`.** Read from the page: the suite passed there, 1,452 with 9 skipped,
    `build/play-game.sh` drew Pusher from the package under the validation layer, and then
    `build/drive-game.sh` failed for all eight games at `./e3d open`, each error `<game>: did not
    open.` and nothing after it, since `--quiet` keeps e3d's own refusal off the page and the
    script's last warnings came from a log that had none or is not where the script looks,
    `build/sessions/<game>.log`. e3d refuses an opening with a sentence, `could not be started` with
    the error, or `did not start serving within` its patience naming the log it wrote, and neither
    reached the page, so the cause of the first opening of the games through a session on Windows is
    unread. Two things. The script's error for `open` carries e3d's refusal, its code and its
    sentence, and the last lines of the log e3d names, read from that path, which N 6.7 asks of a
    failure; and the cause is found with the run of the mend, the eight failing alike pointing at
    what they share, the session a game serves and e3d waits for on Windows, and not at a game.
    Settled when the Windows job plays the eight games.

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

11. **The package ships no ReadyToRun images, the author's own publish being the way.** The owner
   chose ReadyToRun per platform at 19:45 on 2026-10-06 on a figure of 629 ms of compiling in a
   game's first frame, which was CPU time across threads and not what a player waits, and reversed
   it at 20:50 on the measure the commit after `039bd788` made: the package 14.9 MB from 1.3, the
   worst early frame about 140 ms from about 205, and no difference for a game its author publishes
   with `PublishReadyToRun` or NativeAOT. The measurement stays in TODO.md's cost entry, and
   `docs/shipping-a-game.md` says in a line how a shipped game compiles the engine ahead.

12. **Per-object motion blur and ImGui viewports are wanted, after the standing items.** The
   owner said so on 2026-10-06, and that neither is a priority, so they are the last items of the
   list and are taken when the rest is through.

13. **Audio falls back to a device of silence where there is none.** The owner chose it on
   2026-10-06: where no audio device opens, the backend plays through SDL's dummy driver, which
   takes samples in real time, so sounds, music and streams advance on a machine without one as
   raylib's do through miniaudio's null device, rather than standing still with the backend
   disabled.

14. **A document a game's author reads names no one who decided.** The owner asked on 2026-10-06
   that release notes and the documents under `docs/`, the README and the cheatsheet give reasons
   and not who wanted what, which is N 4.7, and who chose what stays here under Decisions.

## Replies

**Verdict 33, every game's opening on Windows.** The cause is e3d's. On Windows it starts a program
through `cmd.exe /c start`, and the line was added to `ArgumentList` as one argument, which .NET
quotes as a program's argument is, each quote inside escaped with a backslash. cmd.exe reads no
backslash as an escape, so `start` took `\"e3d\"` for the program to run and the log's path was no
path, which is why no log was where the script looked. The line goes to cmd.exe as written now,
through `Arguments`. No machine here runs Windows, so the next Windows job is the proof. The
script keeps e3d's answer to `open` as JSON and fails with its code and sentence and the last five
lines of the log it names, read from that path, through `cygpath` on Windows, and perl's JSON
module reads the answer, which Git's bash, macOS and Linux all have. Trying that found a fault of
e3d's own. A program's path that does not exist was taken for an example's name, and e3d died
making a folder for its log from the path. It is refused with `NOT_FOUND` now.

**Verdict 32, the offscreen hundred on macOS.** The offscreen test reads the heap every ten apps.
For the cause, the macOS job installs `dotnet-gcdump` and names it in `E3D_GCDUMP`, and the leak
tests count the heap's objects by type after their twentieth app and their hundredth, the tool run
on the test's own process. A failure's message has a line of the five types that grew most, with how
many more there are and about how many KB, and the test's output has thirty. The first census is
taken before the twentieth reading, so what it keeps is in both readings. Here a census takes half a
second, and over a hundred apps it finds nothing grown but reflection's caches and the runtime's
strings, 0.1 MB. One thing was read on the way. The scripts' watch from Verdict 27 is let go with the
last app watching it, so a hundred apps made one after another still make a hundred FSEvents
streams, and `039bd788`'s sentence that they make one holds for apps that overlap. Whether that is
what grows is for the census to say, and nothing is changed for it before then. The suite: 1,462
passed, none skipped.
