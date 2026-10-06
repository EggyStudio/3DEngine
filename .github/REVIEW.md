# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `48939b42`. The run of `22bbf15a` passes its tests on Linux, Windows and macOS, so
Verdict 29 is settled, and its examples job is still running, Verdict 28 and the first shares with
it. An eighth game, `games/Tempo`, is a rhythm game whose notes are judged by the time of the music
heard, its song a tracker module, with an autopilot that plays each note on the frame nearest its
beat and a CI step that plays the whole song on the dummy driver under the layer and fails on any
miss, 381 of 381 at 60 frames a second and no miss at 5 nor on four-core lavapipe at 65 ms a frame,
its capture left out of N 4.5 as the other games' are, 11 from 10 (`48939b42`). The suite: 1,441
passed, none skipped.

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

Before them, the six pairs furthest from raylib were traced: with no reason on their rows are
traced: five are kept differences with their reasons on the page and the rows, an RGB logo's corners
opaque where RGBA's are clear, ImGui's wider font wrapping lines elsewhere, raylib blending alpha
into a render texture by the color's factors so a bar darkens over black, ours times 0.84 being
raylib's to the unit, and Latin-1 loaded where raylib loads ASCII; and one was a fault, text in a
font from a file drifting along each line, its advances kept in fractions where raylib cuts them to
whole pixels, its baseline a pixel low and a larger bake laid out by its own boxes where raylib
scales its one bake, mended and measured over all 220 pairs, `textures_image_text` 5.1 to 0.3 and
the `font_from_file` reference 9.5 to 0.7 per cent from raylib's own drawing, so it is retaken
(`20bf8c72`). The suite: 1,435 passed, none skipped. Item 5, the surface read whole, is under way.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdict 28's examples job, and the first shares.** The run of `22bbf15a` passed its tests on
   all three systems, and its examples job is running; when it ends green, Verdict 28 settles and
   its notices hold the pairs measured for the first time, which item 4 records. Each push's run is
   read by the reviewing session, and a failure it names comes first here.
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

Verdicts 1 to 27 and 29 are settled, and their numbers are not given again.

28. **The examples job ends with `Process completed with exit code 4` and nothing else, three
    runs in a row.** The jobs of `6336aba6`, `b9ebd0bd` and `039bd788` each fail so, after the
    captures, the measure, the games and the README's walk, and no `::error::` names a capture, a
    pair or a game, so what failed and in which step is in a log nobody reads, which is the state
    the test page was made to end (N 6.7). None of `compare.py`, `capture-example.sh` or
    `play-game.sh` exits with 4, so the code is some program's own, `e3d`'s or `dotnet`'s. Two
    things. The examples job gets what the test job has: each step that runs a program ends with a
    page or an `::error::` saying the step, the program, its exit code and its last lines at a
    warning or worse, and `build/test.py`'s page tests cover the examples' script where one is
    shared, so a bare exit code cannot end a job again. And the measure's first run leaves its
    shares where they can be read without a sign-in: the pairs measured for the first time go into
    `::notice::` lines as well as the summary, at most ten of them, since the summary and the
    artifact need a signed-in reader, and until `measured-ci.tsv` is recorded the measure's own
    result does not fail the job. Found in the workflow's own image, where the coder ran the job's
    steps before.
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

**Now 2, TODO's entry on text, emoji sequences.** A sequence a color font joins into one picture is
drawn as that picture: a family, a flag, a skin tone, a keycap, the rainbow flag and a subdivision
flag. `GlyphSubstitution` reads a font's GSUB table and applies its `ccmp` feature's lookups in the
table's order, single, multiple and ligature substitutions and the contextual and chained
contextual ones in their three formats, through extensions. While matching it passes over a default
ignorable character such as U+FE0F where that does not match itself, as HarfBuzz does, since a
font's ligature for a keycap or the rainbow flag leaves out the U+FE0F the text has inside it. Text
in a font the engine's reader draws some of is shaped in runs of those characters and the joiners
and selectors between them. The glyphs the font can make of the characters asked for are found by
following its substitutions and baked with them under keys past U+10FFFF, so `LoadCodepoints` of
the text is enough, and a joiner or selector left over is not drawn, as a shaper hides it. Text
outside those runs is drawn a character at a time as before, so nothing changes for a text font.
Read here with Twemoji, of bitmaps, and Segoe UI Emoji, of layers: every sequence each font holds
joins, Segoe drawing a family as the three parts it is made of and a subdivision flag as the black
flag, having none of its own. Three tests on the test font, which `make-color-test-fonts.py` gives a
ligature and a chained context, and the 117 render and font tests pass on lavapipe under the layer.
The comparison page, `docs/text-and-fonts.md` and TODO.md say so. The suite: 1,443 passed, none
skipped.
