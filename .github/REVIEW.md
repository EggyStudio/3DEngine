# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `468e32ed`. Item 3: `gi_screen_filter.slang` holds the frame before's light at each
screen probe within twice the spread of this frame's light among the like probes around it, each
channel's standard deviation by the 5 by 5 filter's own weights, before the blend, so where a light
changed the history is pulled to it and where it holds still the history lies inside the spread and
keeps its calm; a new test brings a lamp into the red wall's room at frame 30 with the camera on the
block's side, which only the wall's bounce reaches, and counts the frames until the side stays
within a tenth of its new light, 0 with the hold where it took 7 without, the test asking for 2 at
most; the sliding camera's calm reads 0.40 levels a frame over the picture's 0.48 where it read
0.27, a spread of 1.25 having failed it at 0.51 and 3 lagging a frame, so 2; in Wick's doorway the
hold costs nothing that reads, 0.229, 0.229 and 0.360 ms at the three qualities against 0.229, 0.228
and 0.359, and the lag does not read there, since carrying the lamp moves the camera and the
player's meshes, which settle into the field over its eight frames, so the test's room is where the
blend's lag reads alone; the guide, RENDERING.md and TODO.md say so, the five frames gone
(`468e32ed`). Item 3 is settled, and its number goes to the shadow ranking weighing how much of the
picture a light lights, after item 2, which the coder has started. The suite: 1,568 passed, and on
lavapipe the rendering tests, 302.

Before them, item 2 came to be settled, `SetTonemap` taking Bevy's eight beside the engine's curve,
Narkowicz's fit and the cut, named as BevyCSharp names them and appended so the four old numbers
hold; the old `Reinhard` was Bevy's Reinhard by luminance, so it is `ReinhardLuminance` and
`Reinhard` is Bevy's per channel, the upgrading page's fourth change that still compiles; five
curves are ported from `bevy_core_pipeline` 0.19.1's `tonemapping_shared.wgsl`, and AgX, Tony
McMapface and Blender's filmic look the light up in Bevy's own tables, carried by
`build/bevy-luts.py` as Bevy has them with their Zstandard swapped for KTX2's zlib, 816 KB, which
.NET reads with no decoder taken, read the first time a curve is chosen, 0.4 to 2.4 ms, into a 3D
texture at the composite's binding 3 sampled as Bevy samples it, Bevy's two license texts and
`info.txt` beside them and THIRD-PARTY-NOTICES.md naming Bevy and each table's and curve's authors;
`TonemapTests` draws SHARED.md's ramp through each and holds it to BevyCSharp's pictures from
`64ec311`, copied byte for byte, and to a CPU model of each curve and table, all eight within one
level of both on the coder's GPU and passing on lavapipe; a table costs the composite about 0.002
ms, in the guide with each table's memory, and the guide's tables, the cheatsheet and the upgrading
page's Added section name the eight; `Bloom.cs` passed 800 lines, so its two graph nodes moved to
`HdrNodes.cs` (`379e7907`); the package had put the tables a folder too deep, since pack adds an
item's own folder below its path and the csproj named `%(RecursiveDir)` there too, so the path names
no folder, Pusher from the package draws through AgX, and `PackageContentsTests` holds every file
under `3DEngine/Shaders` to its own path (`1e1eafe2`). Decision 17 was done on both sides, and its
number went to the frame's depth lent to the window's pass. The suite: 1,566 passed, and on lavapipe
the rendering tests, 301.

Before them, Verdict 39 came to be mended: the captures on macOS are a job of their own,
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
on the next macOS run.

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
2. **The frame's depth lent to the window's pass.** TODO.md's effects entry notes that the HDR
   frame's multisampled depth stands beside the window's own where the window's pass after it draws
   nothing with depth and could lend it its own: the window's pass takes the frame's depth where it
   can, so a frame of the scene holds one multisampled depth and not two, the 60 bytes a pixel at
   four samples and the 124 MB at 1920 by 1080 measured again and written in the guide, RENDERING.md
   §5 and TODO.md, the references unchanged, and a test drawing shapes with depth over the scene
   showing they meet the scene's depth. After item 3.
3. **The shadow ranking weighing how much of the picture a light lights.** The lights given
   shadows, ten spots and twelve points, are ranked by what the camera sees and then by the light
   that reaches the eye, and TODO.md's shadows entry says the ranking does not weigh how much of the
   picture a light lights, so a lamp lighting a wall across the view can lose its shadows to a
   brighter one lighting a corner: each light's share of the picture, the pixels its reach covers in
   the view, weighs in the rank beside its brightness at the eye, the two spots and four points with
   the most texels chosen the same way, a test with more shadowed lights than the limits where the
   one lighting most of the picture keeps its shadows, the ranking's cost measured in Wick's doorway
   and the hall of shadows, and the entry's sentence dropped. After item 2.
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

Item 2, the frame's depth lent to the window's pass. The HDR frame's target draws into the window's
own multisampled depth (`GraphicsDevice.CreateRenderTargetOnWindowDepth`) and keeps only its
resolved depth, which the decoding, the lens passes and the reflections read; the window's pass
after the composite clears that depth and draws nothing into it with depth, since every batch with
depth is before the split. A target made before the window's depth was made again, as a swapchain
made again at the same size makes it, is made again too (`WindowDepthGeneration`), and with one
sample the target keeps a depth of its own, the window's being none to sample. The target is 44
bytes a pixel at four samples where it was 60, 91 MB at 1920 by 1080 where it was 124: `nvidia-smi`
gave `shaders_bloom` with its bloom off at that size 278 MiB at b526089c with no such frame, 405 MiB
at 468e32ed and 374 MiB now, the 31 MiB the depth's 16 bytes a pixel, written in the guide,
RENDERING.md §5 and TODO.md. The references are unchanged: drawn again on the GPU, each is within 3
levels of its committed frame, those drawn on other devices apart by what they were apart before. A
new test draws a model and shapes with depth in one scene at four samples, a bar behind the model
hidden where the model stands and seen past its sides and a cube before it covering it. The suite:
1,569 passed; on lavapipe the rendering tests, 303 passed and 2 skipped. Next is item 3's number,
the shadow ranking weighing how much of the picture a light lights.
