# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `2bcac3a6`. Verdict 29's causes were the tests' own: macOS ships bash 3.2, where a
command not found in a sourced script exits 1, so the test holds the error's title to whatever code
the step ended with; the notices test kept a carriage return on each line's last pair; and the
missing notice is most likely .NET reading the script's UTF-8 in the console code page, said as
likely and not certain, so both script tests read UTF-8, compare with the line end taken off, and
the page test's lines end in Windows line ends on every system (`0ce5aac6`); the verdict settles
with the run. An ImGui window dragged outside the game's window gets an SDL window and a swapchain
of its own once a program turns ImGui's viewports on, off by default, offered where ImGui's own SDL
backend offers it and so not on Wayland, drawn after the window's pass and taken by the frame's one
submit and present, held by a test on SDL's offscreen driver on Linux (`2bcac3a6`), which settles
item 5 and the list the owner's decisions made; items 5 and 6 are new. The suite: 1,435 passed, none
skipped.

Before them, release notes and the documents a game's author reads give reasons and name no one who
decided: the comparison page's row gives its reason alone, `NormTests.N_4_7` reads the README, the
cheatsheet and `docs/` line by line, `pack.sh` leaves the two commit lines that name the owner out
of the release notes with a warning, since history does not change, `PackageContentsTests` fails a
package whose notes name anyone, and COMMITS.md has the rule (`78d79c22`), which settles item 6. The
runs of `38e81c4f` and `78d79c22` fail on Windows and macOS in the new scripts' own tests, Linux
passing, which is Verdict 29, and the examples job did not run, so Verdict 28 waits. The suite here:
1,434 passed, none skipped.

Before them, Verdict 28's cause was found through GitHub's public listing of a run's jobs, which
gives each step's conclusion and time without a sign-in: the step that failed in all three runs was
the first-person game's walk, where each pad press held two frames and the runner drew Manor's menu
under 5 frames a second, so the first press went past Settings to Quit and the game quit itself,
`e3d` answering 4 for an app it could not reach. Presses are held one frame with a check that the
walk began, every step of the examples job runs through `build/step.py`, which gives a step that
fails silently an `::error::` naming the step, the command, its code, the last lines and the session
logs' warnings, `e3d` writes its errors on stderr, the pairs measured for the first time are at most
ten notices that fail nothing while `measured-ci.tsv` is empty, and `build/page.py` is shared with
`test.py` under `StepScriptTests`, which N 1.4 leaves out, eleven from ten (`38e81c4f`); the verdict
settles when an examples job passes. The owner packed 5.1 from `b43818f9`, whose pack workflow
passed. The suite: 1,433 passed, none skipped.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdict 29 first, the scripts' tests on Windows and macOS, then Verdict 28's examples job.**
   Two runs fail in `StepScriptTests` and `TestScriptTests` off Linux; the run after the mend shows
   whether all three systems pass and whether the examples job, which did not run, passes its
   first-person walk. Each push's run is read by the reviewing session, and a failure it names comes
   first here.
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
   how many rows each holds, as BevyCSharp takes its gaps.

   Two things go with the ports. A program of this engine's own that answers a raylib example
   under another name takes raylib's name once it is read against raylib's source (N 5.1), as
   `shapes_basic_2d` may be `shapes_basic_shapes`, `shapes_basic_3d` `models_geometric_shapes`,
   `audio_sound` `audio_sound_loading` and `models_terrain` `models_heightmap_rendering`. And a
   call that answers otherwise than raylib's of the same name, where the difference is kept, is
   a line on `docs/compared-with-raylib.md`, in a table of its own a port adds to, the first
   being a trigger's axis, from 0 at rest here and from -1 in raylib, which docs/input.md says
   and the comparison does not.
3. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the seven has.
4. **Every picture measured against raylib's own program** (N 5.2). The table sets each example's
   capture beside raylib's screenshot, read by eye, and `692cefee` built raylib's deferred program
   here to compare the same frame, which is the measure item 2 asks for and the 216 written have not
   had. A module at a time: raylib's examples built from the checkout `run.sh` pins, each run to the
   frame the capture here is taken at, with a shim around `EndDrawing` that takes the screenshot and
   closes, and each pair compared as the reference tests compare their frames, by the share of
   pixels that differ past the tolerance. A pair that differs is taken down to the smallest program
   that still differs, as item 2 has it, and ends as a fault mended or as a line of the comparison
   page where the difference is kept, a trigger's axis being the first. The share each pair differs
   by is written by the script into the table, so the number is measured again on each run.
5. **The public surface read whole before 6.0** (Decision 5). The table of raylib's examples is
   written, 220 of 222, which is where Decision 5 promises the surface a game can lean on. Every
   public type and member in `PublicApi.txt` is read against raylib's name for the same thing, the
   cheatsheet's line for it and its neighbors, and each that answers to another name, takes its
   arguments in another order, or stands alone where raylib has a family is renamed or reshaped
   before the surface is promised, in commits that say what moved, the examples and games following;
   a line of the comparison page says each difference kept with its reason. The owner says when 6.0
   is cut, and this goes before it.
6. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall (item 4's measure).

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 27 are settled, and their numbers are not given again.

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

29. **The runs of `38e81c4f` and `78d79c22` fail on Windows and macOS in the tests of the new
    scripts, and pass on Linux.** Read from the pages. On macOS, `StepScriptTests`' test of a step
    not in the workflow expects exit 127 for a command that is not found and gets 1, both runs. On
    Windows, `TestScriptTests` finds the pairs measured for the first time unequal to the 219 it
    expects, both runs, and at `78d79c22` its page of 500 failures beside 100,000 lines finds no
    `::notice` line saying `500 failed` and `60,000 ×`. Three faults of `build/step.py` and
    `build/page.py` on the systems the scripts were not run on: a shell's code for a missing command
    is the shell's, so the test accepts what the system's shell gives or the script maps it to one
    code; and lines read on Windows end in a carriage return and a line feed, so pairs and repeated
    lines are compared with the line end taken off, which is where both page failures point. Each is
    reproduced by feeding the scripts a file with Windows line ends and by asking the shell the
    system has, and the two systems' pages are read again after.

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

**Now 4, the pairs furthest apart from raylib.** Six written examples stood 8 to 27 per cent apart
from raylib's program with no reason on their rows, and each is traced. `textures_image_rotate`
(19.2) turns `raylib_logo.png`, which is RGB, and raylib fills the corners the turn opens with
zeros in the image's own format, which are opaque black, where an image here is RGBA and its zeros
clear, which the comparison page now keeps with its reason. `core_text_file_loading` (27.1) is
ImGui's default font, wider than raylib's, so its lines wrap elsewhere, which the page keeps under
`GetFontDefault`. The two split screens (10.6 and 8.8) differ in their bars, which raylib blends
into a render texture with alpha by the color's factors, so a bar at 0.8 over the opaque sky leaves
0.84 and darkens over the black it is drawn on: ours times 0.84 is raylib's to the unit, and the 2D
one's 245 times 0.76 its 186, which the page keeps, with their text in the default font.
`text_unicode_ranges` loads Latin-1 where raylib loads ASCII, which the page keeps as well.

`text_codepoints_loading` (9.8) held a fault. Its line of glyphs drifted from raylib's along each
line, since the atlas builder keeps a glyph's advance in fractions where raylib cuts it to whole
pixels at the size a font is loaded at, its baseline sat a pixel lower, at the ascent plus one
rounded down where raylib's is the ascent cut, and text drawn past a font's size from a larger bake
was laid out by that bake's own boxes, where raylib scales its one bake. A font loaded from a file
now has whole advances and its glyphs a pixel higher, and a larger bake is drawn in the boxes and
advances of the font's own bake scaled, keeping its sharper pixels. Measured over all 220 pairs,
`text_codepoints_loading` went from 9.8 to 6.0, `text_unicode_ranges` 11.2 to 9.1,
`text_font_filters` 4.4 to 3.0, `text_font_sdf` 6.1 to 5.7 and `textures_image_text` 5.1 to 0.3, and
no other pair moved by 0.3 points but three whose rows say they move with the clock or the audio
device. `measured.tsv` is that run's. The reference `font_from_file` stood 9.5 per cent from raylib's
own drawing of its three lines, a program built here against the pinned raylib, and the new frame
stands 0.7, so it is the reference now, and the 38 reference and font tests pass on lavapipe. A
color emoji test sampled the emoji's last row, which is a pixel higher now, and samples the middle
of each half. Five captures are taken again, and the six rows say their reasons. The suite: 1,435
passed, none skipped.
