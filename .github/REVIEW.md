# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `3afcc4d0`. The Linux job fetches `b43818f9` alone, so the upgrading page's test runs
on every push (`5dca5694`), which settles item 4, and no name lost since 5.1 is written in the other
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

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdicts 30 to 33 first, the run of `3afcc4d0`.** The run of `1c848a20` is read into
   Verdicts 32 and 33, and the run of `3afcc4d0`, in its Windows and macOS suites at 15:36, is the
   proof of their mends; its examples job, which carries the guides' blocks and Verdicts 30 and 31,
   runs once the three test jobs pass. Each push's run is read by the reviewing session, and a
   failure it names comes first here.
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
    is read against that first. The run of `1c848a20` answered the reading: the heap every ten apps
    rose and fell back, 61.8, 64.9, 67.7, 61.0, 64.5, 67.0, 60.7, 64.1 and 67.2 MB from the
    twentieth app, three steps up and one down with no slope under them, and the census found 0.25
    MB more alive at the hundredth, all strings, so no closed app is kept and the two readings had
    fallen on a trough and a crest; both leak tests judge how far the heap's floor rose, the least
    reading from the twentieth app to the fiftieth against the least from the seventieth to the
    hundredth (`596535ce`), which the run of `3afcc4d0` tries. Settled when the macOS job passes the
    test.

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
    the Windows job 180 (`3afcc4d0`), which the run of `3afcc4d0` tries with twelve games. Settled
    when the Windows job plays the eight games.

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

**Now 4, batch one, text read right to left.** A line with a letter of a script written right to
left is drawn and measured in the order it is read, by a reduced bidirectional algorithm in
`TextDirection`. Each line is a paragraph whose direction is its first strong letter's, the weak
types are resolved by W1 to W7, the neutrals by N1 and N2 and the levels by I1 and I2, trailing
white space goes back to the paragraph's level, and the runs are reversed from the highest level
down. A line is reversed by grapheme clusters, as .NET's `StringInfo` finds them, so a letter keeps
its marks after it and a joined emoji sequence, a flag or a keycap its own order, and a mirrored
character is turned where it is read right to left. The explicit embeddings, overrides and isolates
are passed over and paired brackets resolved as other neutrals are. `TextKeys` orders such a line
before it keys or shapes it and keeps the result with the font as shaped text is kept, and a line
with no character read right to left takes the path it took, so Latin draws and measures as
raylib's does. `make-color-test-fonts.py` writes `rtl.ttf`, alef, bet and gimel as bars of three
heights, a mark of no width, digits, brackets and a Latin letter, the other fonts it writes coming
out byte for byte the same. Twelve tests hold the rules on strings and the font's keys, measure and
pixels, and a reference frame, `right_to_left`, draws a line of Hebrew, a run of it in a line read
left to right, a number in it, brackets turned in it and a mark on its letter. The reference
frames, these tests among them, pass on lavapipe under the validation layer in the workflow's
image. The text frames moved to `ReferenceFrameTests.Text.cs`, ahead of the next two batches' frames,
which the file would not have held under N 1.3. The text guide has a section on it, the cheatsheet
a sentence, the comparison page a row, since raylib draws such a line in the order it is stored,
and TODO.md's entry says what is left. The suite: 1,488 passed, none skipped.

**Now 4, batch two, Arabic joined.** A run of Arabic is shaped in the order it is stored and then put
in the order it is shown, so each letter takes its form by the letters beside it. A line is cut
into grapheme clusters with their levels, a run of Arabic clusters at one level is shaped together,
each glyph keeping its cluster, and the clusters are reversed for display with their glyphs in
order. `ArabicJoining` holds Unicode 16's joining types for the Arabic script's blocks, the form
each letter takes, and the presentation forms by their compatibility decompositions with the eight
lam-alef. `GlyphSubstitution` applies plans now. A plan is the features asked for under a script,
the `arab` script or the default one, each lookup with the mask of the positions it is applied at.
The run is a list of `ShapedGlyph`, glyph, cluster, mask and whether it is default ignorable, a
lookup's flags honored by the GDEF table's classes, mark attachment classes and mark filtering sets,
a ligature taking its first component's cluster and the marks it passed over staying after it. The
emoji keep their `ccmp` plan and their behavior, the font tests passing unchanged. Arabic's plan is
`ccmp`, `locl`, `isol`, `fina`, `medi`, `init`, `rlig` and `calt`, where the font has the positional
features, the forms they can reach from the letters asked for baked by the reader as joined emoji
glyphs are, and a font without them is drawn by the presentation forms it maps, which are asked for
with the letters. `make-color-test-fonts.py` writes `arabic.ttf`, with its own GSUB under `arab`, a
lam-alef that ignores marks and a GDEF table, and `arabic-forms.ttf`, with presentation forms and no
GSUB, both read by fontTools and shaped by HarfBuzz to the glyphs the tests expect. Twenty-five
Arabic fonts of this machine, Noto Naskh, Vazirmatn, Tahoma and Segoe UI among them, loaded and drew
an Arabic phrase through `e3d eval` with nothing failing, each joining where its forms differ in
width. Noto Naskh's keys for a phrase were HarfBuzz's glyphs one for one, apart from a mark coming
after its letter where HarfBuzz puts it before, as the text's drawing needs. A reference frame,
`arabic_joined`, draws joined words, lam-alef alone and after a letter, a mark on the ligature, and
the same words by presentation forms, and the text tests and every reference frame pass on lavapipe
under the validation layer. Marks are drawn where their glyphs lie, which batch three moves. The
guide, the cheatsheet, the comparison row and TODO.md's entry say so. The suite: 1,492 passed, none
skipped.
