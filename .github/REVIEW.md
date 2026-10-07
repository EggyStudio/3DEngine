# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `f5a102d6`. An OpenType font of CFF outlines draws its characters past U+FFFF from
its Type 2 charstrings, CID-keyed fonts among them, checked against STIX Two Math and Noto Sans CJK
and by the scan of 444 fonts (`3c72e321`); the games' errors on Windows and macOS name the system as
a reader knows it (`eb5f95f5`); a font that joins sequences shapes a string once and draws it again
from what it kept (`d9e5bac2`); and a twentieth reference frame compares color text whole, paints,
joined bitmaps and tinted layers, TODO.md's testing entry naming the twenty (`f5a102d6`). TODO.md's
text entry is down to text shaped whole, which stays described. Item 4 is new. The suite: 1,461
passed, none skipped.

Before them, no font file came to stop the program: all 444 fonts of the machine load, the eight
that killed the process on the atlas builder's assertions refused with a reason, CFF2-only and
bitmap-only fonts asked for as SDF, or given their first mapped character where the builder knew
none, and a collection is read as its first font (`d19cee7b`). Windows and macOS play every game
through `e3d` as Linux does, each asserting its walk, lap, match or score, a failing game with its
own error while the rest play, about eleven minutes added to each job and unrun on either system
until the push (`9ef68bab`), which settles item 4. A font of Apple's bitmaps draws its emoji in
color (`be170635`). The suite: 1,458 passed, none skipped.

Before them, a distance field font came to hold its characters past U+FFFF from the font's outlines,
rasterized into the bake at four times the size before the distances are measured, and
`LoadFontEx`'s remarks say what is drawn of a color font (`c8e11f3d`). An entity goes as a state
enters a value, or at the first transition a rule answers true for, `DespawnOnEnter` and
`DespawnWhen` beside `DespawnOnExit`, both acting in the frame of the transition since the
transition is the engine's own, where BevyCSharp asks its rule the frame after, with a test each and
the states guide (`acdd8fcb`), which settles item 4. The suite: 1,453 passed, none skipped.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdicts 30 and 31 first, the soak's unnamed game and the blind measure.** The examples job
   of `22bbf15a` passed every game's step and failed at the soak without naming a game, and its
   measure drew no frame of raylib's for any pair; both are taken apart in the verdicts. Each push's
   run is read by the reviewing session, and a failure it names comes first here.
2. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the seven has.
3. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.
4. **The README's walk and the first game's steps on Windows and macOS.** The examples job
   follows the README in a new project and builds and runs every step of the first game on Linux
   alone, where a newcomer on Windows is the common case and the two systems found what Linux did
   not four times today. The test jobs of Windows and macOS run `build/readme-walk.sh` and the first
   game's steps from the package as they play the games, each failing step with its own error on the
   page, the minutes added said in the commit, and a step that cannot run on a system says why.

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

**Now 4, the README and the first game on Windows and macOS.** Both test jobs follow the README in
a new project (`build/readme-walk.sh`) and build and run every step of the first game
(`build/first-game.sh`) from the package after playing the games, each a step of its own, so a
failing one has its own error on the page. Writing a path into a file or a variable does not turn
it into Windows' form as an argument is, so each script writes the package's folder into
`nuget.config`, the template's parameter and `DOTNET_CLI_HOME` through `cygpath -m` where Git's
bash runs, and the README walk takes its own temporary folder in place of `$RUNNER_TEMP`, whose
backslashes Git's bash reads otherwise. Here the walk takes 33 seconds and the eight steps 56, so a
runner, restoring from nuget.org and installing the templates, likely adds three to five minutes to
each job. The script tests hold both to what macOS's bash and BSD's tools read. Neither has run on
either system until the push.
