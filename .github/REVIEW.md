# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `2a81d369`. `docs/upgrading.md` moves a game from 5.1 to 6.0, counting from 5.1.116,
the package packed at `b43818f9`, with a row for every name the public surface lost since saying
what a game wrote and what it writes, the two changes that still compile first, a capsule's rings
before its slices and the log levels numbered as raylib numbers them, then the three functions gone,
the 26 keys, the 15 gamepad buttons, `Critical` as `Fatal`, the skeleton and the keyframes, and what
was added; the README links it, and it names no one who decided. `UpgradingTests` reads
`PublicApi.txt` at `b43818f9` from git and as it is, and fails naming each lost type or member the
page lacks in code, which the workflow's shallow checkout skips, so the Linux job fetching that
commit is item 4 by the new count. A sweep mended two stale names, `GetKeyPressed`'s summary and
RENDERING.md's `FrameMorphWeights`. N 1.4 leaves out 14 (`2a81d369`), which settles item 4. The
session takes TODO.md's order while Verdicts 30 to 33 wait for a push. The suite: 1,470 passed, none
skipped.

Before them, Verdict 33's cause was found to be e3d's: on Windows it added its `cmd.exe /c start`
line to `ArgumentList` as one argument, so .NET escaped each inner quote with a backslash that
cmd.exe keeps, `start` took the escaped title for the program and the log's path was none; the line
goes to cmd.exe as written through `Arguments`, `drive-game.sh` keeps e3d's answer as JSON and fails
with its code, its sentence and the last five lines of the log it names, and a path that is not
there is refused with `NOT_FOUND` where e3d died making a folder from it. For Verdict 32 the
offscreen test reads the heap every ten apps, the macOS job installs `dotnet-gcdump`, and both leak
tests count the heap's objects by type after the twentieth app and the hundredth, a failure naming
the five types that grew most; on the way the session read that the scripts' shared watch of Verdict
27 is let go with the last app watching, so a hundred apps made one after another make a hundred
FSEvents streams, which the census's answer is read against first (`1c848a20`). Both verdicts wait
for the Windows and macOS jobs. BevyCSharp's `bcs` gives cmd.exe its start line the same way, which
is its item 4. The suite: 1,462 passed, none skipped.

Before them, every C# block of the guides came to build on the packed package in the examples job,
138 blocks of fifteen pages: `build/docs-on-package.py` sorts a block's lines by what they declare,
types into a namespace, members into a class and statements into a method inside a loop run once,
after the lines a `<!-- compiled with: -->` comment before the fence gives, leaves out a block
marked `<!-- not compiled: -->` with its reason, and says an error at the page's line as an
annotation, with `DocsScriptTests` feeding it a good, a stale and a skipped block, which settles
item 4. Its first run found three faults: the spatial sound the audio guide teaches had been
internal since `82b1feb4` and is public again as `BehaviorSounds.PlaySpatialSound`, two lines added
to `PublicApi.txt` and none lost; the states guide and the cheatsheet named a `Screen.Menu` the
page's enum lacks; and five fragments no compiler reads are written as their code (`a4f31573`).
N 1.4 leaves out 13. The suite: 1,462 passed, none skipped.

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
4. **The upgrading page held in the workflow.** `UpgradingTests` needs `b43818f9` in the
   checkout, which the workflow's shallow checkout lacks, so the page is held on a developer's
   machine alone. The Linux job fetches that one commit before the suite, `git fetch --depth=1
   origin b43818f9`, so the test runs there on every push, and the skip's sentence stays for a
   checkout without it.

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
    FSEvents lets go of its stream, is a place to look and not the cause. Mended for the reading at
    `1c848a20`: the offscreen test reads the heap every ten apps, the macOS job installs
    `dotnet-gcdump` and names it in `E3D_GCDUMP`, and both leak tests count the heap's objects by
    type after the twentieth app and the hundredth, a failure's message naming the five types that
    grew most and the output thirty; on Linux the census found 0.1 MB of reflection's caches and the
    runtime's strings. The session read on the way that the scripts' shared watch of Verdict 27 is
    let go with the last app watching it, so a hundred apps made one after another make a hundred
    FSEvents streams, and `039bd788`'s one stream holds for apps that overlap; the census's answer
    is read against that first. Settled when the macOS job passes the test.

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
    Mended at `1c848a20`: the cause was e3d's, which on Windows added its `cmd.exe /c start` line to
    `ArgumentList` as one argument, so .NET escaped each inner quote with a backslash that cmd.exe
    keeps, `start` took the escaped title for the program and the log's path was none; the line goes
    to cmd.exe as written through `Arguments`, the script keeps e3d's answer as JSON and fails with
    its code, its sentence and the last five lines of the log it names, and a path that is not there
    is refused with `NOT_FOUND` where e3d died making the log's folder from it. Unproven until a
    Windows job runs. Settled when the Windows job plays the eight games.

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

**Now 4, the upgrading page held in the workflow.** The Linux job fetches `b43818f9` alone after its
build and before the suite, by its full hash, since a server is asked for a commit by the whole of
it, so `UpgradingTests` runs there on every push. A shallow clone of this repository fetching it
that way held the commit, read `PublicApi.txt` at it and kept its history at one commit. BUILDING.md
says so, and the skip's sentence stays for a checkout without it. Nothing that builds changed, and
the norm's tests pass.

**Now 2, a ninth game of a kind none of the eight is.** `games/Sumo` is for two players on one
screen, two marbles on a ring of clay each trying to knock the other off, first to three. It uses
what no game did. The window is split between two cameras, each drawing into a render texture of
its own with shadows, particles and billboards, and the second player plays on pad 1 or the arrow
keys. Shaders of the game's own draw the marbles, the ring's floor and a crowd of 321 in one
instanced draw, a compute shader paints the floor each frame from a storage buffer of the last
eight bumps, and the score and the screens' words are in a distance field font. Its first run
found a fault of e3d's. `input.button` and `input.axis` made a console pad at 0 alone, so no second
player could be driven, and they make pads up to the one named now, four at most, after any real
pad, with two tests. Its other faults were its own. A key held 60 frames on lavapipe's slow frames
was three seconds of game time and rolled a marble off the ring, so each input is held 15 frames,
and two autopilots circled for minutes, so a round lasts 30 seconds at most, the marble nearer the
middle taking the point. The Linux job plays it from the package and captures it, the soak and the
resize storm take it, and `drive-game.sh` plays it on Windows and macOS, where the second pad
starts the match and rolls its marble, a key rolls the first, and the autopilot plays a match out.
Here, on lavapipe under the validation layer in the workflow's image, it played through with no
validation error, the soak held level and the storm left it drawing. N 4.5 lists its capture with
the games'. The suite: 1,472 passed, none skipped.

**Now 2, a tenth game, played by typing.** `games/Wordfall` drops words on curving paths toward a
town, each cleared by typing it before it lands, and uses what none of the nine did. Its words come
from its own list or a text file dropped on its window, accents and all, through `GetCharPressed`.
They fall along Catmull-Rom splines. Every sound is made as it plays by a callback that feeds an
audio stream, a finished game's result is copied to the clipboard and read back, and F12 saves a
screenshot beside the game. Its first run found a fault of the engine's. A run with no window,
offscreen or headless, never starts SDL's video, so `SetClipboardText` dropped the text and
`GetClipboardText` answered empty, and a game driven offscreen could not copy and paste its own
text. Such a run now keeps a clipboard of its own, as audio there falls back to a device of silence,
with a test, and the system's clipboard is used wherever there is a window. The storm found the
game's own fault, a layout fixed at 1280 by 720 that a smaller window cut off, so it is drawn
through a 2D camera that scales it to fit. `drive-game.sh` drops a list of accented words, types
the lowest through `input.text`, misses on purpose, lets the autopilot type until the town is
buried, then asserts the copy read back and the screenshot written. The Linux job runs that script
and captures the game, and the soak and the storm take it. On lavapipe under the validation layer
in the workflow's image it played through twice with no validation error, and the soak held level.
The cheatsheet's `GetKeyPressed` line still said `Unknown`, a name of 5.1, and says `Null` now. The
suite: 1,473 passed, none skipped.
