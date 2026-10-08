# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `9a13915d`. TODO.md's build and release entry: `build/drive-game.sh` takes `window`
as its second argument and opens the game through `./e3d` with no `--offscreen`, its captures and
its error's heading named apart and the empty flag list expanded in the form bash 3.2 takes under
`set -u`, and the Windows and macOS jobs play Pusher that way after the offscreen games, four
minutes in a step of six, a failure in the games' own annotation, so SDL's window and the swapchain
on those two systems are first proved by the next run the owner pushes (`d559bb05`); packing for it
found `PackageContentsTests` counting one build a shader where `e3d shaders` has compiled a second
naming `RAY_QUERY` since `1b4edb7e`, which the pack workflow would have refused, so the test counts
each shader's builds as `SlangLoader.VariantsOf` finds them (`96d9cb06`). Item 3's third fault:
`build/spirv-tools/merge-blocks/` holds seventeen lines of SPIR-V, two unreachable blocks, the
second branching back to the first, which `spirv-val` takes and spirv-reduce's block merge dies on
in v2025.1, the SDK's v2026.4.rc2 and main at `db9f967`, since `MergeWithSuccessor` looks for the
successor from its predecessor onward, which holds for reachable blocks alone; `run.sh` reproduces
it with any spirv-reduce, the patch has `CanMergeWithSuccessor` refuse a successor laid out before
its predecessor, and the issue's text waits for the owner (`9a13915d`); the Mesa faults stand at 11
KB and, `gi_trace`'s compute shader, 13 KB, the second crashing under the validation layer with no
error reported first, which points at lavapipe. With that entry TODO.md's order is walked to its
end, every entry left a limit described, a decision of the owner's, or a wait on lavapipe or on an
answer, so item 2 is settled and its number taken by the upgrading page's Added section, and a
thirteenth game, the first lit by the bounce, is item 5, after item 3's two Mesa reports;
RENDERING.md's one entry of its order left, the tonemap as a pass over every frame, stays parked,
since it costs every program every frame and redraws every lit reference for no fault measured. The
suite: 1,548 passed.

Before them, TODO.md's prose entry came to be done in words alone: STYLE.md's remaining hits for
dashes, spaced hyphens, wants and clefts, earlier revisions and old reference paths are all
arithmetic, raylib's own words on screen, a license, or STYLE.md and COMMITS.md naming what they
forbid, so N 4.1's list keeps those two files and nothing more; every one of the 485 colons that are
not a channel's label was read, eleven joined two clauses and are rewritten, and the check reports
lists and labels alone; STYLE.md asks for American spelling with no check for it, so about forty
British spellings in the carried-over code's comments, XML documents and log lines are American, a
private method and a local among them, raylib's own words in the examples kept; and
`ScenePurposeMask.Editor`'s summary describes no editor any more, the strict build clean and the
model pass byte for byte the same (`7115eeaa`). Item 3's second fault was found to reproduce in a
harness of its own with no ray query, a lamp, a glossy floor and the bounce on, `gi_trace`,
`gi_screen` and the model pass each crashing lavapipe with `lampLight` split out and all three
drawing unsplit, so `gi_trace`'s compute kernel is the one reduced. The suite: 1,548 passed.

Before them, the light-bounce entry's last gap came to be closed, moving meshes bouncing gray: the
resolve keeps a still copy of the field's colors, and the stamp starts each cell from that copy and
paints it the color of the nearer of the still surface and the boxes, so a cell goes back to the
still color once a box leaves; the test is the red wall scene with the wall swaying every frame,
held as a box and never built, the block's side reading red with the painting and gray without,
which fails the test; with three robots the stamp costs 0.062 to 0.067 ms where it cost 0.048 to
0.052, a cascade's build unchanged at 0.45 to 0.47 ms, and the field takes 5 MB a cascade where it
took 4, the model pass byte for byte the same; a moving mesh's own light is still not stamped, since
a still copy of the glow would cost 2 MB a cascade, which the docs say; on lavapipe under validation
160 passed and 1 skipped (`d67d0153`). The suite: 1,548 passed.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdict 33's six games first, then the examples job.** The Windows job of `06b702a1` passed
   every test and played six of the twelve games; Summit, Tempo, Sumo and Jelly exited before they
   were ready and Slide and Wordfall died after they opened, each page line ending in lines of
   information alone, so the reason is unread, Verdict 33's new part, whose three mends are in at
   `1840cbb5` and read on the next Windows job once the owner pushes, the cause e3d's own look for
   the game on Windows before cmd.exe had made it, mended at `63581f04`; its macOS job was still
   playing the games at 21:48, and the same run first shows Pusher in a window on the two runners'
   desktops (`d559bb05`). The examples job, which carries the guides' blocks and Verdicts 30 and 31,
   runs once a run's three test jobs pass. Verdict 37's fourth part at `0ad8636f` reads what climbs
   when the leak test's host next dies. Each push's run is read by the reviewing session, and a
   failure it names comes first here.
2. **The upgrading page's Added section whole.** Every public name the surface gained since the
   commit 5.1 was packed from (`b43818f9`) is on the page that moves a game from 5.1 to 6.0, held by
   the test that holds the page's rows for the names lost, read the other way from the two
   `PublicApi.txt`; the names grouped as the features they belong to, a line on what each does and
   the guide that shows it, the scene's field and the light that bounces, the reflections and their
   tiers, text shaped and the color fonts, ImGui's viewports and the rest, so an author of a 5.1
   game reads in one place what 6.0 gives them, and nothing is named there that the code lacks.
3. **The two lavapipe faults reduced and reported.** The null pointer in the compiled shader that
   a split of `directLight`'s lamp loop brings on, and the crash at the first ray query in a
   fragment stage, each cut down to the smallest Slang or SPIR-V that shows it under lavapipe of
   Mesa 25.2 and the report's text written beside each in the repository and TODO.md pointing at
   them, the filing on Mesa's tracker the owner's since it is done under their account, and
   SPIRV-Tools' own fault beside them, the block merge in `spirv-reduce` that runs off the end of
   the layout, with its reproduction and the patch that refuses the merge, for the owner to file
   there, so the model pass is held in a shape around a driver's fault only as long as it must be,
   and the ray-query path is drawn on CPU devices once the fault is mended upstream.
4. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.
5. **A thirteenth game, the first lit by the bounce.** A game of a kind none of the twelve is,
   since none calls `SetSceneField` or `SetGlobalIllumination`: its play is the light that bounces,
   a lamp carried through dark rooms whose light reaches around corners and through doorways and not
   through walls, on glossy floors that reflect it; built from the package and played through
   `./e3d` on the three systems as the twelve are, its walk asserted, at the quality lavapipe draws
   within the games' step, a lit room of it a reference frame compared whole, and its line in the
   games' table; after item 2 and item 3's two Mesa reports.

## Verdicts

Verdicts 1 to 29, 32, 34 to 36 and 38 are settled, and their numbers are not given again.

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
    Windows job runs. The run of `1c848a20` opened every game, and Windows ran out of its 75 minutes
    in the games' step with no error on the page, so each game has a budget, `DRIVE_MINUTES`, past
    which a watcher stops it and one error says how far it got by its last status, the games after
    it played on, the errors written to the script's own output so one said inside `$(ask ...)`
    reaches the page (`596535ce`); the twelve games timed on lavapipe gave each twelve minutes and
    the Windows job 180 (`3afcc4d0`), which the run of `3afcc4d0` tries with twelve games. The
    Windows job of `3afcc4d0` was cancelled at the job's 180 minutes in the games' step at 18:31,
    its annotations holding the tests' notice alone, and the four jobs after it sit in the same
    step, so no budget fired. The cause reads from the script and e3d together. `drive-game.sh`
    reads the answer of `./e3d open` through `$(...)`, which ends when every holder of the pipe's
    write end has closed it; e3d on Windows starts the game through `cmd.exe /c start /b`
    (`Launch.cs`), and a process made that way is handed every inheritable handle e3d holds, the
    pipe among them, whatever the line redirects, so the substitution waits for the game to end, the
    game runs until a stop the script has not reached, and the budget's watcher, started after the
    opening, never runs; BevyCSharp's Windows step hung the same way the same day, read the same,
    and its mend at `070e5e0` writes each answer to a file. Three things: the opening's answer is
    written to a file under the captures folder and read from there, `$(...)` never wrapping a
    command that starts a program, which the SHARED.md row carries; the watcher starts before the
    opening, so a game that hangs in its opening is stopped at its budget with `as far as its
    opening` on the page; and the games' step takes `timeout-minutes` under the job's 180, so a wait
    ends the step with the page written and not the job with nothing. The four jobs still running
    end the same way and are not read again. Mended at `5057c3cb`: the opening's answer goes to
    `captures/<game>-opened.json` and is read from there, the watcher starts before the opening, and
    the step has 110 of the job's 180 minutes; Pusher played through the script on the working
    machine. Clearing the inherit flag of e3d's own handles before it starts `cmd.exe`, offered in
    the reply, is not asked for while nothing waits on them. Unproven until a push. The Windows job
    of `06b702a1` reached the games with the mends and played six of them, Pusher, Hopper, Swarm,
    Rally, Manor and Tactics, in under eight minutes, and six failed: Summit, Tempo, Sumo and Jelly
    did not open, e3d answering `NOT_READY` with the program exited before it was ready, Summit's
    log ending as its physics world was disposed and Jelly's as its device came up on llvmpipe; and
    Slide and Wordfall opened and died before their first command, which ended with 2. Every quoted
    log line is information, none a warning, so a game ends there by a path that logs nothing, a
    native death or an exit of its own, and the page cannot say which. Three things: e3d's answer
    and the script's error carry what is known of the end, the exit code where the session or the
    process gives one and the log's last lines whatever their level, and a game that exits in its
    startup logs why at warning or worse before it goes; the Windows job's `LocalDumps` key covers
    every process and not `testhost.exe` alone, the dumps listed on the page, so a game that dies
    natively on llvmpipe leaves its stack; and the six are then mended by what the dumps and the
    lines say, their shared cause first, five of the six being the five newest games and Summit the
    sixth. Mended at `1840cbb5`, the exit code and its meaning in e3d's answer, a warning from an
    app closing of its own, the log's last three lines and the dumps in the script's error, and
    Windows' dumps for every process; the Lato pattern a coincidence of the order the games ran in.
    The cause was e3d's own: on Windows its first look for the game by name could come before
    cmd.exe had made it, and a look that found none answered the game as exited, so four games were
    answered so while they went on to serve, and the sessions they left made two later games'
    commands and stops ambiguous, code 2. Mended at `63581f04`, e3d reading whether cmd.exe still
    runs before looking, so a game not found while it runs is waited for and one not found after it
    ended has gone, and the script sending every command and stop to its game by name through
    `E3D_NAME`, a refused command's error carrying e3d's code and sentence. Settled when a Windows
    job plays the twelve games.

37. **The Windows jobs of `80227981` and `c4f248fd` fail the Cornell box frame and the offscreen
    leak test.** Read from the pages: 1,505 passed, 2 failed, 12 skipped and 1 without a result at
    `80227981`, where the offscreen leak test crashed the test host, in the whole suite after 13
    minutes and again in the Core part after 5, its output ending at an app's `Startup stage
    complete`; and 1,507 passed, 2 failed, 12 skipped at `c4f248fd`, where the same test ran through
    and found the process holding 58 MB more after a hundred apps than after twenty, against 50, the
    heap flat at 32.17 MB from the thirtieth app on and the threads steady, so what grows is native,
    what each app leaves in the device or the driver since the field, the bounce and the reflections
    gave an app images, buffers, pipelines and descriptor sets of their own. In both runs the
    `cornell_box` frame differs by 3.3% of its pixels from the reference, over the 2% allowed, where
    the Linux job's lavapipe matches it and the laptop drew it, so the two lavapipes draw the bounce
    apart. The Windows job of `3afcc4d0` passed every test at 15:31, and the hot reload test that
    failed once at `80227981` passed at `c4f248fd`, so it is watched and not mended. Three things:
    the leak test counts the device's objects, images, buffers, pipelines and descriptor sets, after
    the twentieth app and the hundredth as the soak counts a game's, and names the kinds that grew,
    so the native growth is read on the page; a test host that dies leaves a minidump,
    `DOTNET_DbgEnableMiniDump` with the dump under the runner's temp among the artifacts, and the
    step says which app the test was at from its output; and the frame comparison's notice says
    where the differing pixels lie, the rows and columns they fall in, so the bounce's difference
    between the two lavapipes is read from the page, after which the frame is matched on both, by a
    step made deterministic or by a share set from the two with its reason beside it. Mended at
    `6a2d916e`: the ledger of Vulkan objects read by the leak test, a minidump and the last progress
    line of a test host that dies, and the frame's notice saying where the pixels differ,
    `cornell_box` allowing 5% with its reason, which the macOS job of `4db6fe46` at 2.6% is within.
    The Windows job of `6a2d916e`, the first with the mends, passed the Cornell frame and lost the
    leak test again, the host dying at the ninetieth app in the Core part and the ninety-first in
    the whole suite, a death at the same place twice being a limit reached rather than chance, and
    the page named the app and no dump, so `createdump` left none for that death. Three more things:
    the page's account of a lost process carries the last ledger line the test printed, so it says
    whether any Vulkan object had climbed by the eightieth app; the leak test prints the process's
    handle count every ten apps, and on Windows its GDI and USER objects through `GetGuiResources`,
    so a count climbing toward a limit shows before the death; and where `createdump` leaves
    nothing, the job turns on Windows' own dumps for the test host through the `LocalDumps` key into
    the same folder, read as a `.dmp` by `dotnet-dump` or WinDbg, so the death's own stack is on the
    artifacts. Mended at `0ad8636f`, the progress line carrying the ledger, the handles and the GUI
    objects, and Windows' own dumps kept. The Windows job of `06b702a1` passed both, so the death is
    intermittent, and the readings tell when it next comes. Settled when a Windows job passes both
    tests.
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

15. **Text is shaped whole.** The owner chose it on 2026-10-07, a feature raylib lacks:
    right-to-left text in display order, Arabic joining through the font's GSUB and marks and pairs
    through GPOS, in three batches, with Latin outside shaped runs left one character at a time with
    raylib's advances and unkerned, so what raylib measures stays measured the same.

16. **Global illumination comes in four phases, a scene distance field first.** The owner chose
    it on 2026-10-07 over Radiance Cascades in screen space alone, over a world-space volume alone
    and over a voxel or surface-anchored variant: a cascaded signed distance field of the scene,
    hybrid Radiance Cascades over it with the first cascade in screen space, then glossy and
    screen-space reflections with a ray-query path where the GPU has one, each phase measured and
    tiered, nothing baked and no ray-tracing hardware needed, the kernels in Slang so the bridge
    runs them in BevyCSharp once proven here, which meanwhile keeps Solari and Bevy's probes. The
    spectral extensions, subsurface scattering, caustics, iridescence, volumetric multiple
    scattering and dispersion, wait until those three phases ship with numbers, each then an
    experiment of its own, and HTrace's WSGI was passed over as closed and as far more code for a
    hybrid that ghosts when things move.

## Replies

Item 2, the upgrading page's Added section whole:

- **Held both ways.** `UpgradingTests` reads the two `PublicApi.txt` the other way as well: a type
  5.1 lacked is named by itself, and a member whose name its type lacked by its name, a call whose
  arguments were reordered or retyped keeping its name. It found thirteen the page lacked, the
  field's and the bounce's six (`SetSceneField`, `SceneFieldConfig`, `Config.SceneField`,
  `SetGlobalIllumination`, `GlobalIllumination`, `Config.GlobalIllumination`), `EcsWorld`'s two
  despawn calls by those names, and `Model.Skeleton`, `ModelSkeleton` and the three keyframe
  members, which the models table now names on its right as it names the old ones on its left. The
  124 lines the surface gained come to those and the names the page already had, the renamed keys,
  buttons and levels among them.
- **Grouped by feature.** The section is a list of features, each with what it does and the guide
  that shows it: the scene's field, the light that bounces, glossy reflections and their tier at
  `High` through the GPU's rays, text shaped, color fonts, text from a file where raylib's lies,
  ImGui's viewports, VR, states, sound, textures and the window, models and shadows. The ones with
  no new name, text shaping, color fonts, viewports and the reflections among them, are said as
  what 5.1's calls do now.
- **Nothing the code lacks.** A third test holds each `Type.Member` of the section whose type is
  the engine's to the surface, and failed on a misspelled `Music.FrameCounts` put there to try it.
  The links are held by `DocumentLinkTests`.
- **Item 3.** Fault 1 stands at 6 KB and fault 2 at 8 KB, and each report follows its reduction's
  end.
- The suite: 1,550 passed.
