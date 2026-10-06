# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `78d79c22`. Release notes and the documents a game's author reads give reasons and
name no one who decided: the comparison page's row gives its reason alone, `NormTests.N_4_7` reads
the README, the cheatsheet and `docs/` line by line, `pack.sh` leaves the two commit lines that name
the owner out of the release notes with a warning, since history does not change,
`PackageContentsTests` fails a package whose notes name anyone, and COMMITS.md has the rule
(`78d79c22`), which settles item 6. The runs of `38e81c4f` and `78d79c22` fail on Windows and macOS
in the new scripts' own tests, Linux passing, which is Verdict 29, and the examples job did not run,
so Verdict 28 waits. The suite here: 1,434 passed, none skipped.

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

Before them, the runs of `6336aba6`, `b9ebd0bd` and `039bd788` passed their tests on all three
systems, macOS at 1,398, 1,401 and 1,402 with none failed, so Verdict 27 is settled, with the note
that the two runs before the watcher's mend passed as well, so the leak test's margin on macOS is
thin and the next failure's series will say; no examples job names a failed capture, so Verdict 26
is settled too. All three examples jobs fail with `Process completed with exit code 4` and nothing
else, which is Verdict 28. Motion blur blurs each mesh entity along its own movement where
`SetMotionBlur` is given objects, from a velocity image its moving entities are drawn into as
instanced runs, off by default, a new reference and no old one changed, about 425,000 turning
entities held at 60 fps against 700,000 camera-only, two slower designs measured on the way
(`b43818f9`), which settles item 5. The suite: 1,429 passed, none skipped.

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
5. **ImGui viewports** (Decision 12), last. An ImGui window dragged outside the main window gets
   an SDL window and a Vulkan swapchain of its own, through ImGui's viewport interface, off by
   default, with a test that a viewport's window is made and closed and the editor's panels checked
   by hand.
6. **N 4.7 taken** (Decision 14), before item 5. `docs/compared-with-raylib.md`'s row on a render
   texture's samples says the reason alone, without who chose it or when, and the same words are
   looked for in the pack workflow's release notes step and by `NormTests` over `README.md`,
   `CHEATSHEET.md` and `docs/`, which pass with no list. COMMITS.md says a message names no one who
   decided, since the release notes are made from the messages, and `32bc8543`'s message is the kind
   to avoid.

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

**Verdict 29.** Both causes are the tests' own, and the scripts give what they should. GitHub's
annotations of the failing jobs, read without a sign-in from `/check-runs/<id>/annotations`, give
each failure whole. On macOS the bash 3.2 macOS ships, run here in its own image, ends a sourced
script whose command is not found with 1 where bash 5 ends it with 127, and `step.py` named that 1
rightly, so the test holds the error's title to the code the step ended with, whatever the shell
gave. On Windows the notices test split the output at its line feeds and kept each line's carriage
return, so the last pair of each notice carried one. Output with Windows line ends did not lose
the page test's notice here, and the one thing on that line no other has is its "×", which a
Windows console's code page reads otherwise than the UTF-8 the script writes, as .NET reads a
child's output in that code page unless told, which is the likeliest cause and not a certain one,
since it passed on Windows at `38e81c4f`. Both
tests now read a script's output as UTF-8, with `PYTHONIOENCODING` set for its errors, and compare
lines with the carriage return taken off, and the page test's 100,000 lines end as Windows ends them
on every system, so Linux reads what Windows gives. The suite: 1,434 passed, none skipped.

**Now 5, ImGui viewports** (Decision 12). Off by default. A program sets ImGui's own
`ImGuiConfigFlags.ViewportsEnable`, and a running session `imgui.viewports on`. `SdlImGuiViewports`
gives ImGui the platform's callbacks over SDL windows and the renderer's over Vulkan swapchains,
with SDL's displays as its monitors, on X11, Windows, macOS and SDL's offscreen driver, and none on
Wayland, where no program reads or sets where its windows are, as ImGui's own SDL backend has it.
`GraphicsDevice.Windows` makes a window's surface and swapchain in the main window's present mode
and acquires its image as the frame draws it. The frame's one submit waits on and signals each
window's semaphores beside the main window's, and its one present presents them all, a swapchain
gone out of date made again before its next image. The renderer calls a node's `AfterWindowPass`
once the window's pass has ended, where the ImGui node calls `UpdatePlatformWindows` and draws each
viewport into its own window, with a pipeline for each pass, since the window's may be multisampled
and a viewport's is not. With viewports on ImGui measures from the desktop, so SDL's mouse positions
are moved by where their window is, and e3d's and a replayed recording's by where the main window is,
and the game's own pointer takes no event from a window not its own. The cimgui in the package is
built with its asserts, which stop the process, and two were met on the way. ImGui asks for
`UpdatePlatformWindows` after every frame once a backend offers viewports, the flag on or off, which
a frame the renderer skips now does as well, and a program turning the flag on between ImGui's first
two frames, as one setting it in its first frame does, is held back a frame.

A test opens an app on SDL's offscreen driver, places an ImGui window outside it, and finds a second
SDL window of the ImGui window's size with a swapchain of that size, a captured frame of it holding
the window's white text, and both gone when the window comes back inside. It passed here and on
lavapipe under LunarG's layer in a container, with the GUI, offscreen and reference tests, 113 with
no message of the layer's. It runs alone, in a collection with parallelism off, since closing the
app quits SDL for the process, and it is skipped off Linux, since SDL's offscreen driver draws
through `VK_EXT_headless_surface`, which the Windows and macOS jobs' devices are not known to offer.
By hand, through `imgui.viewports` and `imgui.shot`, which writes a viewport's next frame to a file,
`gui_imgui_window`'s Help window and the engine's Performance panel (F2), each dragged out of the
window, had a window of their own drawn whole and were gone from the main one. Dragged back in, the
window closed, and turned off and on again, the windows closed and came back, with no warning logged
under the layer. The example places Help from the main viewport's corner, and its capture is taken
again, the one kept having been taken with an ImGui settings file of an earlier session's that had
moved Help to the right edge. The suite: 1,435 passed, none skipped.
