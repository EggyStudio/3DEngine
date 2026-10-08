# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `ca4d474a`. Verdict 39's mend: the captures on macOS are a job of their own,
`macos-examples`, beside the tests' job, which keeps its suite, games, window, native publish and
walk and drops the raylib files fetched for the captures alone; the job has 160 minutes, its step
140, a budget of 130 (`CAPTURE_MINUTES`) and 300 seconds an example (`CAPTURE_SECONDS`), each
capture run in the background with its output in a file, one past its seconds stopped and failing
alone with exit code 124, the budget's end an error naming the last example reached and how many
were left, and a closing notice saying how many drew of how many, in how long, about how long a
capture took and the five slowest, each example's seconds and result in `times.tsv` in the artifact;
the step log of `b526089c` needs a sign-in no session has, and its 3,012 seconds and 22.6 MB, about
253 pictures at the 72 KB each weighs, say one example hung near the end, which the limit names from
here on, the set cut only if a notice says a pass does not fit; tried with a limit of 2 seconds,
which stopped and named `core_basic_window`, and a budget of 0, which named none reached, bash 3.2
parsing the script and `ScriptTests` reading the new job's scripts (`ca4d474a`). Verdict 39 settles
on the next macOS run. The coder goes on to item 2, the tonemappers.

Before them, item 3 came to be settled: the window's scene goes through the HDR frame and one
tonemapping pass every frame it shows one, a frame of 2D alone straight into the window as before;
the frame holds its light sRGB-encoded and carried past 1 rather than linear, so a shader of the
program's own reads the same with effects on or off and the frame's blending and multisampling stay
on encoded light as the window's were, and `decode.slang` decodes it once, with the scene's depth,
for bloom, the exposure, the lens passes, the bounce's reflections and the particles drawn over it
in linear light; the composite decodes, bends and encodes, the engine's curve giving way to a cut at
1 where nothing past white can come about, so white stays white as raylib draws it; the output flag
stays as the view's kind, since a render texture of eight bits still needs the curve in the model
pass and a probe's faces linear light, which the brief had not weighed; a program's shader drawn
into the frame has its color held by an FClamp put into its SPIR-V, alpha within 0 and 1 and no
channel below 0, light past 1 kept (`ShaderProgram.HeldToEightBits`), which brought five raylib
pairs back from 15 to 42 percent apart to their shares; the frame and its pass cost 0.02 to 0.035 ms
of the coder's GPU with every effect off and 60 bytes a pixel at four samples, written in the guide,
RENDERING.md §5 and TODO.md; twelve references are redrawn, none past 0.79 percent of its pixels,
the lit ones at the edges of light past white resolved before the curve and the effects' ones on
encoded light; three tests cover a program's shader drawing alike with bloom on or off, white bent
only where light past white can come about and an alpha past 1 blending as in an eight-bit frame,
and the upgrading page's third change that still compiles is a shader that returned linear light for
an effect (`c58ff65b`). The bounce's lag moved up to item 3 after item 2. The suite: 1,557 passed,
and on lavapipe under the validation layer the rendering tests, 292.

Before them, item 2 came to be settled, the Windows and macOS jobs running `build/play-native.sh
Pusher` after the step that packs the engine and draws the game, publishing Pusher native from the
package for the machine's runtime, `win-x64` or `osx-arm64`, linked by the runner's own toolchain as
a player's machine does, and drawing 300 frames offscreen under the validation layer, each step
given 15 minutes; the script names what failed in an error annotation with the game and the system,
a publish that fails with the compiler's or the linker's own lines, a run that ends early with its
log's last lines, no validation layer or an error from it, where a failed publish had ended it
through `set -e` with nothing of its own; `ScriptTests` reads the scripts the macOS job names from
the workflow, so it holds this one to macOS's tools too; 30 seconds cold and 11 warm on the coder's
machine, the runners' first restore and link a few minutes the next run measures; BUILDING.md and
TODO.md say both jobs publish it (`b526089c`). Its number went to the light that bounces following a
changing light within a frame or two. At 07:10 the owner decided that every tonemapper BevyCSharp
offers comes to 3DEngine (Decision 17), which takes item 2 after item 3, the bounce's lag moving to
item 5. The owner pushed at 06:15 and the run of `b526089c` came back green on Linux and Windows,
the thirteen games, Pusher in a window, the native publish and the leak test's hundred apps passing
there, which settles Verdicts 33 and 37, and failed on macOS at the examples' capture, killed by its
50 minutes with the games and the rest behind it skipped, Verdict 39; the examples job was skipped,
so Verdicts 30 and 31 wait on.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdict 39 first, then the examples job.** The run of `b526089c`, the first pushed since
   `06b702a1`: Linux green in 3 minutes; Windows green in 29, its suite of 1,535 with the leak
   test's hundred apps, Pusher published native in 48 seconds, the thirteen games in 14 minutes and
   Pusher in a window in 12 seconds, which settles Verdicts 33 and 37; macOS green through its suite
   of 1,518 and then timed out capturing the examples, Verdict 39, mended at `ca4d474a` with the
   captures in a job of their own and read on the next macOS run, so the examples job, which carries
   the guides' blocks and Verdicts 30 and 31, was skipped and runs once a run's three test jobs
   pass. Each push's run is read by the reviewing session, and a failure it names comes first here.
2. **Every tonemapper BevyCSharp offers (Decision 17).** In the one tonemap pass of item 3,
   `SetTonemap` gains Bevy's eight, none, Reinhard, Reinhard by luminance, the ACES fit, AgX, the
   somewhat boring display transform, Tony McMapface and Blender's filmic, named as BevyCSharp's
   `Tonemapper` names them, the engine's own curve, Narkowicz's fit and the cut kept beside them;
   AgX, Tony McMapface and Blender's filmic from Bevy 0.19.1's own tables (the cargo registry's
   `bevy_core_pipeline-0.19.1/src/tonemapping/luts`, with its `info.txt`), carried as the engine's
   own 3D textures and sampled as Bevy's `tonemapping_shared.wgsl` samples them, with Bevy's
   attributions in THIRD-PARTY-NOTICES.md, the other four ported from that shader; a test draws
   SHARED.md's ramp through each and holds it to BevyCSharp's reference within two levels of 255 for
   the formulas and four for the tables; the guide's effects table, the cheatsheet and the upgrading
   page's Added section name them, and the cost of a sampled table is in the guide. After Verdict
   39.
3. **The bounce following a changing light within a frame or two.** The light-bounce entry's
   limit, the screen's probes blended with the frame before's following a changing light some five
   frames late: where the light at a probe changed, a lamp carried or a wick lit, the blend's
   history is rejected or shortened there, so the bounce follows Wick's lamp within a frame or two
   while a still scene keeps its blended calm; the lag counted in frames by a test that moves a lamp
   and reads a probe, measured on Wick's doorway with the cost of telling a change written in the
   guide, and the guide's, RENDERING.md's and TODO.md's sentences updated. After item 2.
4. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.

## Verdicts

Verdicts 1 to 29 and 32 to 38 are settled, and their numbers are not given again.

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

39. **The macOS job of `b526089c` times out capturing every example, and the games, the window,
    the native publish and the walk behind it are skipped.** The step, added at `0dae7bc6` with 50
    minutes from the coder's GPU's 25 for the 253, ran from 04:23 to 05:13 UTC on the runner's
    paravirtual GPU through MoltenVK and was killed by its limit, the artifact `examples-macos`
    holding 22.6 MB of what it drew and the page saying no more than the timeout, where a run that
    fails says what failed (N 6.7); the steps after it never ran, so the run proved nothing of the
    thirteen games, Pusher in a window, the native publish or the README walk on macOS, which the
    Windows job proved whole. The capture goes into a job of its own on macOS beside the test job,
    never inside it, with its own limit, a budget an example that fails the one and goes on, the
    script saying at its end how many drew and how long each took and the page naming the last
    example reached when the budget ends; the job's own time read from this run's log first, since
    the runner's rate sets what fits. Settled when a macOS run passes its tests, games, window,
    native publish and walk again and the captures' job ends within its limit saying what it drew.

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

17. **Every tonemapper BevyCSharp offers is in 3DEngine.** The owner decided on 2026-10-08:
    Bevy's eight, none, Reinhard, Reinhard by luminance, the ACES fit, AgX, the somewhat boring
    display transform, Tony McMapface and Blender's filmic, are choices of `SetTonemap` beside the
    engine's own curve, Narkowicz's fit and the cut, the three Bevy draws through lookup tables from
    Bevy's own table data with its attributions, the others ported from Bevy's shader, so a picture
    tonemapped here is the one BevyCSharp draws, proved by one ramp drawn through each in both
    engines and compared, the ramp defined in SHARED.md and its references made on BevyCSharp's
    side. Chosen over tables made here for every one, since Bevy's data gives the same picture, and
    over porting alone, since Tony McMapface and Blender's filmic exist as tables alone.

## Replies

Item 2, Bevy's tonemappers. `SetTonemap` takes Bevy's eight beside the engine's curve, Narkowicz's
fit (`Aces`) and the cut (`Clamp`), named as BevyCSharp names them, `None`, `Reinhard`,
`ReinhardLuminance`, `AcesFitted`, `AgX`, `SomewhatBoring`, `TonyMcMapface` and `BlenderFilmic`,
appended so the four old numbers hold. The old `Reinhard` was Bevy's Reinhard by luminance, so it is
`ReinhardLuminance` now and `Reinhard` is Bevy's per channel, the upgrading page's fourth change
that still compiles. Five curves are ported from `bevy_core_pipeline` 0.19.1's
`tonemapping_shared.wgsl`, its matrices as dot products. AgX, Tony McMapface and Blender's filmic
look the light up in Bevy's own tables, carried by `build/bevy-luts.py` as Bevy has them, format,
descriptor, key and values and texels, with their Zstandard swapped for KTX2's zlib, 816 KB in all,
which .NET's `ZLibStream` reads, so no decoder is taken, and DESIGN.md §8 says data is carried so. A
table is read the first time its curve is chosen, 0.4, 1.6 and 2.4 ms warm, into a 3D texture at the
composite's binding 3, sampled linearly and held at its edges as Bevy samples it, Tony McMapface's
kept in RGB9E5. Bevy's two license texts and its `info.txt` are beside the tables, and
THIRD-PARTY-NOTICES.md names Bevy and the authors it credits for each table and curve.
`TonemapTests` draws SHARED.md's ramp through each and holds it to BevyCSharp's pictures from its
64ec311, copied as they are under `References/tonemapping`, and to a CPU model of each curve and
table, within two levels for a curve and four for a table; all eight are within one level of both on
the RTX 4070 and pass on lavapipe under the validation layer. A table costs the composite about
0.002 ms, 0.015 against 0.013 to 0.014 for the engine's curve, written in the guide with each
table's memory, and the guide's tables, the cheatsheet and the upgrading page's Added section name
the eight. Bloom.cs passed 800 lines, so its two graph nodes moved to HdrNodes.cs. The suite: 1,566
passed; on lavapipe the rendering tests, 301 passed and 2 skipped. Next is item 3, the bounce
following a changing light.

Shared: the eight are in 3DEngine at this commit, each within one level of BevyCSharp's references,
for SHARED.md's row to say so. `build/bevy-luts.py` and the CPU model in `TonemapTests` are there
for BevyCSharp's side to read where a check of its tables apart from Bevy's own drawing is asked
for.
