# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `6412daca`. Text read right to left is drawn and measured in the order it is read:
`TextDirection` is a reduced UAX #9 over grapheme clusters, each line a paragraph of its first
strong letter's direction, the weak types, the neutrals and the levels resolved by the rules' names,
trailing white space at the paragraph's level, the runs reversed from the highest level down,
mirrored brackets turned, embeddings and isolates passed over, and `TextKeys` orders a line only
where it holds a character read right to left, so Latin takes the path it took and measures as
raylib's (`0c19c335`). Arabic is joined: a run of Arabic clusters at one level is shaped in stored
order and then reordered, `ArabicJoining` holding Unicode 16's joining types, the forms and the
presentation forms with the eight lam-alef, `GlyphSubstitution` applying plans of features under a
script with a mask a position, lookup flags honored through GDEF's classes and filtering sets, a
font without the positional features drawn by the presentation forms it maps, with two test fonts
from `make-color-test-fonts.py`, a reference frame each, and 25 of the machine's Arabic fonts
joining through `e3d eval` (`6412daca`); batch three, marks and pairs through GPOS, is under way.
The runs: macOS passed the leak test at `3afcc4d0`, `0c19c335` and `6412daca`, which settles Verdict
32; the three Windows jobs were in the games' step at 16:40; Linux passed the first two and failed
one test at `6412daca`, a race of the shader cache under parallel tests, which is Verdict 34; the
examples job waits on Windows. The suite: 1,492 passed, none skipped.

Before them, the Linux job came to fetch `b43818f9` alone, so the upgrading page's test runs on
every push (`5dca5694`), which settles item 4, and no name lost since 5.1 is written in the other
documents, held by a second test that found `ImageDraw` in the cheatsheet (`8f3d456d`). Four games
of kinds none of the eight was came in. Sumo, two players on one screen split between two cameras
drawing into render textures, with shaders of its own and a floor painted by a compute shader, found
e3d making a console pad at 0 alone, so `input.button` and `input.axis` make pads up to the one
named (`2b5b0873`). Wordfall, played by typing words dropped from a file and falling along splines,
its sounds made in a stream's callback, found a run with no window dropping the clipboard, so such a
run keeps one of its own (`01bfcae8`). Slide, the puzzle of merging tiles played by gestures, its
faces drawn into images and its sounds made as waves, found no command making a double click, so
`input.click` takes a count (`a9ff8c1b`). Jelly, a runner squashed by its model's morph targets,
records each run as automation events and watches it again to the same end, in the session and from
a file in a new process (`37064205`). The run of `1c848a20` was read: macOS's heap rose and fell
back by 6 MB every thirty apps with no slope under it and the census found 0.25 MB of strings more
alive, so the leak tests judge how far the heap's floor rose, and Windows opened every game and ran
out of its 75 minutes, so each game has a budget past which its error says how far it got
(`596535ce`); e3d writes UTF-8 on every system (`e60ba729`); and the twelve games timed on lavapipe
at four cores took 22 minutes together, Manor's walk six, so each has twelve in the workflow and the
Windows job 180 minutes (`3afcc4d0`). N 4.5 leaves out 15. The owner chose text shaped whole on
2026-10-07 (Decision 15), which is item 4. The suite: 1,475 passed, none skipped.

Before them, `docs/upgrading.md` came to move a game from 5.1 to 6.0, counting from 5.1.116, the
package packed at `b43818f9`, with a row for every name the public surface lost since saying what a
game wrote and what it writes, the two changes that still compile first, a capsule's rings before
its slices and the log levels numbered as raylib numbers them, then the three functions gone, the 26
keys, the 15 gamepad buttons, `Critical` as `Fatal`, the skeleton and the keyframes, and what was
added; the README links it, and it names no one who decided. `UpgradingTests` reads `PublicApi.txt`
at `b43818f9` from git and as it is, and fails naming each lost type or member the page lacks in
code, which the workflow's shallow checkout skips, so the Linux job fetching that commit is item 4
by the new count. A sweep mended two stale names, `GetKeyPressed`'s summary and RENDERING.md's
`FrameMorphWeights`. N 1.4 leaves out 14 (`2a81d369`), which settles item 4. The suite: 1,470
passed, none skipped.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdicts 30, 31, 33 and 34 first, the runs of `3afcc4d0`, `0c19c335` and `6412daca`.** macOS
   passed all three, so Verdict 32 is settled; the three Windows jobs were in the games' step at
   16:40, Verdict 33's proof; Linux failed `6412daca` in the shader cache's race, Verdict 34; the
   examples job, which carries the guides' blocks and Verdicts 30 and 31, runs once a run's three
   test jobs pass. Each push's run is read by the reviewing session, and a failure it names comes
   first here.
2. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the twelve has.
3. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.
4. **Text shaped whole, in three batches (Decision 15).** First right-to-left text, a reduced UAX
   #9 with strong types, numbers, neutrals, marks and mirrored brackets and no explicit embeddings,
   putting Hebrew and Arabic runs in display order for drawing and measuring. Then Arabic joining,
   each character's joining type choosing its form, applied through the font's GSUB under the `arab`
   script with feature masks, then `rlig` and `calt`, honoring lookup flags and GDEF classes, a font
   with no Arabic GSUB falling back to the presentation forms it maps, the shaped glyphs baked as
   the joined emoji glyphs are. Then GPOS inside shaped runs, mark-to-base and mark-to-ligature for
   harakat and pair adjustment there. Latin outside shaped runs stays one character at a time with
   raylib's advances and unkerned, so ported layouts measure the same. Each batch holds with
   synthetic fonts from `make-color-test-fonts.py` and a reference frame, and TODO.md's text entry
   shrinks as each lands.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 29 and 32 are settled, and their numbers are not given again.

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
    Windows jobs of `3afcc4d0`, `0c19c335` and `6412daca` were in the games' step at 16:40, each
    with 180 minutes. Settled when the Windows job plays the twelve games.

34. **The Linux job of `6412daca` fails one test in the shader cache, a race of two writers.**
    Read from the page: 1,485 passed, 1 failed, 6 skipped, and `RendererSmokeTests`' frame with null
    graphics ended in `FileNotFoundException` at `SlangCompiler.WriteAtomically`, the file
    `velocity.vertexMain.<key>.uniforms`. The writer puts the bytes in `<path>.partial` and moves it
    over the path, and two tests compiling the same shader at once write the same partial file, so
    the second's move finds the first's gone; the key is the source's, so both would have written
    the same bytes. Nothing of batch two's, which added tests beside the ones that raced. Two
    things. The partial file takes a name unique to its writer, and the move over the path tolerates
    a winner, keeping what is there when the target exists, as the engine's other atomic writers do;
    and a test compiles one shader from two threads a hundred times against one cache. Settled when
    a Linux job passes.

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

## Replies

**Now 4, batch three, marks and pairs placed.** A run of Arabic is positioned by the font's GPOS
table after its substitutions, so a mark is put on its letter and a pair is kerned, and the rest of
the text is drawn as before, a character at a time with raylib's advances. What GSUB and GPOS share,
the scripts, features, lookups, flags, GDEF classes and contexts, moved into `GlyphLayout`, which
`GlyphSubstitution` and the new `GlyphPositioning` extend, so the contextual positionings are the
contextual substitutions' code. `GlyphPositioning` reads single and pair adjustments, the pair in
both formats, marks on a base, on a ligature's component and on another mark, the contexts and the
extension, value records' placements and advance and anchors' points, device tables and cursive
attachment left out. A ligature numbers the marks it passes over by the component they followed, so
a mark goes on its own letter of lam-alef. A shaped line's keys carry where each glyph is drawn from
the pen and how far it moves it (`PlacedKey`), which `DrawTextPro`, `ImageDrawTextEx` and
`MeasureTextEx` read, a mark of the GDEF table advancing nothing, as HarfBuzz zeroes it. Comparing
with HarfBuzz on seven fonts of this machine, Noto Naskh, Noto Sans Arabic, PakType Naskh,
Vazirmatn, Arial, Times and Segoe UI, found three more things, each mended. HarfBuzz sorts a
letter's marks by combining class with shadda moved first and the modifying hamzas put before the
rest (UTR #53), so fatha is put on shadda whatever order the text stores them in, which `ArabicMarks`
does. It applies Arabic's features in stages, the forms before the ligatures made of them, and
`liga`, `clig`, `rclt` and `mset` with them, Arial's and Times's Allah being `liga`, so a plan can
be staged (`PlanInStages`). And batch two read the zero width non-joiner as transparent where
ArabicShaping.txt says it joins nothing, as it does the isolates and Arabic's signs that span
digits. After those, 52 words in Arabic, Persian and Urdu, with harakat and without, shaped 328
times on those fonts, came out as HarfBuzz's glyphs at HarfBuzz's positions in the font's units,
unit for unit, but for a joiner HarfBuzz keeps as a glyph of no width, which the engine leaves out. `make-color-test-fonts.py` writes
`arabic-marks.ttf`, `arabic.ttf` with kasra, shadda and a GPOS table holding each lookup type the
reader takes, a mark lookup inside an extension, read by fontTools and shaped by HarfBuzz to the
places the tests expect, the other fonts it writes coming out byte for byte the same. Six tests hold
the places, the order of marks and a mark drawn into an image, one holds the non-joiner, and a
reference frame, `arabic_marks`, draws marks on letters, on lam-alef and on each other, the kerned
and raised pairs, and the same marks in the font with no positions. The twenty-five Arabic fonts of
this machine drew a phrase with harakat through `e3d eval` with nothing failing, a beh with shadda
and fatha measuring as one with none, and Noto Naskh's and Arial's basmala read as HarfBuzz draws
them. The guide, the cheatsheet, the comparison row and TODO.md's entry say what is placed and what
is left, cursive attachment and a letter composed with its mark. With this the three batches of
Decision 15 are in. The suite: 1,503 passed, none skipped.
