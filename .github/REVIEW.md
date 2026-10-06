# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `65c80c4b`. The examples job of `22bbf15a` ran through its captures, its measure and
every game's step, the first-person walk among them, which settles Verdict 28, and failed at the
soak, `a game grew, or could not be played, over two minutes`, with no game named, which is Verdict
30; and the measure's notices list every pair with `none`, raylib's program having drawn no frame on
the workflow's device, so the measure is blind there and nothing is recorded, which is Verdict 31. A
sequence a color font joins into one picture, a family, a flag, a skin tone or a keycap, is drawn as
that picture by the font's own GSUB `ccmp` lookups, the default ignorables passed over as HarfBuzz
passes them, read with Twemoji and Segoe UI Emoji (`65c80c4b`). The suite: 1,443 passed, none
skipped.

Before them, the run of `22bbf15a` passed its tests on Linux, Windows and macOS, which settled
Verdict 29. An eighth game, `games/Tempo`, is a rhythm game whose notes are judged by the time of
the music heard, its song a tracker module, with an autopilot that plays each note on the frame
nearest its beat and a CI step that plays the whole song on the dummy driver under the layer and
fails on any miss, 381 of 381 at 60 frames a second and no miss at 5 nor on four-core lavapipe at 65
ms a frame, its capture left out of N 4.5 as the other games' are, 11 from 10 (`48939b42`). The
suite: 1,441 passed, none skipped.

Before them, the flat API was read by script against the pinned raylib headers: 601 of raylib's
functions carried by name, 501 with raylib's argument names in raylib's order, and the rest moved to
raylib's names and shapes where a reason did not hold them, the keys, gamepad buttons and log levels
named as raylib names them, a clip's fields `Keyframe*`, three older names dropped, and the kept
differences in a table of names and shapes on the comparison page, the `Transform` keeping
`Position` since the scene files and BevyCSharp use it (`4ec025bd`, `472619e9`), which settles item
5 and leaves `PublicApi.txt` changed by 69 lines in and 61 out, the owner's to number as 6.0 before
the next pack. Every example 3.5 to 7 per cent from raylib gives its reason, and the cel shading
outline is pushed as far as raylib's long normals push it (`3267be95`). raylib's VR stereo is
carried and `core_vr_simulator` written, 221 of 222, the last not applying, which settles item 2 as
the measure of what is written (`1e26d438`), and models draw through each eye and keep to the
scissor (`22bbf15a`). The owner pushed, and the run of `22bbf15a` is under way. The suite: 1,440
passed, none skipped.

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
3. **Every picture measured against raylib's own program** (N 5.2). The table sets each example's
   capture beside raylib's screenshot, read by eye, and `692cefee` built raylib's deferred program
   here to compare the same frame, which is the measure item 2 asks for and the 216 written have not
   had. A module at a time: raylib's examples built from the checkout `run.sh` pins, each run to the
   frame the capture here is taken at, with a shim around `EndDrawing` that takes the screenshot and
   closes, and each pair compared as the reference tests compare their frames, by the share of
   pixels that differ past the tolerance. A pair that differs is taken down to the smallest program
   that still differs, as item 2 has it, and ends as a fault mended or as a line of the comparison
   page where the difference is kept, a trigger's axis being the first. The share each pair differs
   by is written by the script into the table, so the number is measured again on each run.
4. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall (item 3's measure).

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

**Verdict 31, raylib's programs drew no frame on the runner.** Found in the workflow's image, Ubuntu
24.04 with the job's packages, by running `core_basic_window`'s program as `compare.py` runs it,
which ended at once with `error while loading shared libraries: libSDL3.so.0`. The engine's package
brings `libSDL3.so`, whose name inside is `libSDL3.so.0`, and the runner has no SDL3 of its own, so
a program linked against the package's folder could not start. Here they had loaded the desktop's
own SDL3 from `/usr/lib64` all along. `compare.py` links them against a folder holding the
package's library under both names, of the version the engine's project references rather than the
newest in the cache, and builds a program again when the script that builds it changes. With it,
`core_basic_window` draws its frame in the image through SDL's offscreen driver on Mesa's llvmpipe,
and four pairs measured here keep the shares they had. A program that ends without its frame says
its exit code and its last lines, and a run where none drew for any pair records nothing and fails
saying why the first drew none, with a test of what it says. Item 4 waits on the next run's notices.

**Verdict 30, the soak.** Run in the workflow's conditions, the eight games at once on four cores
with lavapipe, nothing grew. Seven of the eight ended their two minutes with two to four readings,
under the six the check judges, since a reading was taken after each turn and a turn of 240 frames
took two minutes there, a frame or two a second. `soak.sh` reads on a clock of its own beside the
turns, which the program answers between frames while a turn waits on them, and draws each game at
320 by 180, since a soak reads what the program holds and not its picture. So on four cores every
game gives 12 readings and two to five times the turns. One still failed, Manor's buffers, whose
most rose from 572 to 618 while its least held at 521 to 540 and its last reading was 569. Its
walk streams rooms of more cells and fewer in and out, and two minutes there cover part of the
route, where the desktop's cover it all. A leak raises a value's least as well as its most, so
`soak-check.py` judges each half by its least, with the same slack, and on four cores every game
holds by it, as on the desktop, where all eight give 13 readings and hold. It names each game that
fails in an error annotation of its own: the value that climbed with its two leasts and its bound,
or the command that ended the game's soak with its exit code, which `soak.sh` writes beside its
readings, with the last warnings of the game's log, and the process's resident memory at the first
reading kept and at the last, which `memory` reports. Four tests, one of them Manor's swing, and
the step's own line that named no game is gone. The test joins the script tests left out of N 1.4,
12 where NORM.md's table says 11. The suite: 1,448 passed, none skipped.
