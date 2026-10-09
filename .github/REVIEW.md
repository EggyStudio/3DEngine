# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `8ac5912a`. Item 2: each emitter laid over by alpha keeps the eye its buffer was last
sorted from, the step sorting from the window's as before, and `ParticleRenderer.SortFor` sorts it
again from a view's own eye where that differs, outside any pass, the targets node for each render
target before its pass and the HDR scene node for the window before its particles' pass where a
target sorted after the step, a probe's faces drawing in the last order sorted and a render texture
drawn only in 2D still drawing none; a new test draws one stream, blue born at the back and red
aging toward the front, into two render textures from either end, the front view showing red in
front and the back view blue, where the back view showed the window's order; Sumo's dust is additive
and never sorted, so an emitter of 300 laid over by alpha was added to its ring through `./e3d
eval`, its two views taking 0.377 to 0.383 ms of the GPU in `targets` where they took 0.352, some
0.013 ms a sort, written in the guide and RENDERING.md §3, TODO.md's sentence gone and the 2D render
texture's want of a camera kept (`8ac5912a`). Item 2 is settled, and its number goes to a measured
sweep of the per-entity cost entry, after item 3, which the coder has started, since TODO.md's
limits are spent and the owner has not named the next large item. The suite: 1,574 passed, and on
lavapipe the rendering tests, 308.

Before them, item 3 came to be settled, the lights' upload grouping the views drawing meshes through
a camera, the window first and then the targets, each joining the first group whose first cascade,
fitted to all of the group's cameras and its own, is no more than a quarter wider in its texels than
any member's own (`LightingUboPrepare.SharedTexelGrowth`), so a view reads its shadows from the
shared map as from its own; each group gets one shadow, `ModelRenderer.DrawShadow` drawing it once
by the first of the views sharing it, as the point lights' faces are drawn once by the first view,
and with no sun every view shares one map; a new test draws one scene into two render textures, two
cameras a third of a unit apart drawing the map once and each reading within 0.01 percent of its
pixels of itself drawn alone, two thirty units apart drawing it twice; Sumo's two views, facing each
other across the ring, share, a probe of that layout finding the shared view apart from itself alone
at 77 of 28,800 pixels along its shadows' edges, and its render textures take 0.35 to 0.37 ms of the
GPU in `targets` where they took 0.45, written in the guide, RENDERING.md §4 and TODO.md with the
entry's sentence gone; the references are unchanged (`e10961bc`). The quarter stands, since a shared
view's texels grow by a quarter at most and Sumo's edges move by a fraction of a percent. Its number
went to the two small limits TODO.md still described, ImGui's frame started in `First` and a 2D
render texture's particles. The suite: 1,573 passed, and on lavapipe the rendering tests, 307.

Before them, item 2 came to be settled, a render target that draws meshes through a camera getting
screen probes of its own, a depth at half its size of the meshes it draws that cast shadows, probes
traced, blended and held on it as the window's are, their history let go the frame after one the
target is not drawn in (`GlobalIlluminationRenderer.DrawTarget`, called by the targets node before
the target's pass), bound for its model pass through `TargetIllumination`, a probe capture keeping
the world's probes alone; the targets node runs after `global_illumination` to read this frame's
world probes, the window's shadows still after every target; a new test draws the red wall's room
into the window and into a render texture of its size, the block's side reading (111, 45, 45) in
both where the texture read (52, 37, 37) with the world's probes alone; Sumo's two views at 640 by
720 with the bounce at `Low` take 1.14 ms of the GPU in `targets` where they took 0.62, some 0.26 ms
a view, which a view carries, so no limit stays, written in the guide, RENDERING.md and TODO.md with
the entry's sentence gone; the references are unchanged, Summit's frame moving by some 20 pixels
with what ran before it whichever build draws it (`c7ee9c72`). Its number went to particles sorted
from each view's camera. The suite: 1,572 passed, and on lavapipe the rendering tests, 306.

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
2. **The ring's copy and a chunk's gather, measured first.** TODO.md's per-entity cost entry says
   every instance is still copied into the ring each frame and a culled block with them, and one
   entity moving gathers its whole chunk of 4096 again, the two larger changes it weighed set aside
   as not paying at a count no raylib-style game nears: measure on `models_stress` and the bunnymark
   what the culled blocks' copy and a moving entity's whole chunk cost at 410,266 and at a game's
   count of a few thousand, and take only what pays a measured share, the culled blocks left out of
   the ring's copy and a chunk gathered for the entities that moved, each with its number in the
   entry; what does not pay stays described with the number that says so. After item 3.
3. **The two small limits left.** TODO.md's ImGui entry says ImGui's frame starts in `PreUpdate`,
   so calls a system makes in `First` are lost, and its particles entry says a render texture drawn
   only in 2D has no camera to draw them through: ImGui's frame starts before `First` so a system
   there draws into it, with a test of a window made in `First`; and a render texture drawn only in
   2D draws its particles through the window's camera, the one its 2D is laid over, with a test
   reading an emitter in such a texture; the guide's sentences and the two entries' updated. After
   item 2.
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

Item 3's first limit, ImGui's frame begun before First. The ImGui plugin's frame begins in `First`
(`SdlImGuiPlugin.NewFrame`, windowed and headless), and the plugin is ordered late and listed after
the command line's, so in `First` the frame's time is taken first, then `./e3d`'s commands are
served, handing ImGui their input in the frame, then ImGui's frame begins, and then a program's own
systems run there and draw into it. A new test has a system in `First` make a window, which is sized
and drawn in the frame a system in `Update` draws in. The suite: 1,575 passed; on lavapipe the
rendering, Gui and command line tests, 337 passed and 3 skipped. Seen while trying it, at HEAD as
well: `./e3d command input.drag Left 280 156 20 20`, which TODO.md gives for docking on
`gui_imgui_window`, and a drag from the Help window's title both orbit the camera there and move no
window, so the docking sentence's drag no longer shows what it says; it is left for a batch of its
own.

Disputed, item 3's second limit, a render texture drawn only in 2D drawing its particles through the
window's camera. Particles are the world's, and the textures a game draws only in 2D are most often
its interface, a minimap, a pixel-art canvas scaled up or a layer laid over the window, and each of
those would show every emitter of the window's scene again, through the window's camera, in a
texture that has no depth to hide them behind, so a HUD would carry the window's smoke and sparks a
second time. The texture that wants them is the one a game draws its scene into, which has a camera
of its own through `BeginMode3D` and draws them already. Two ways seem better than the change: leave
it as it is and say in the guide that a texture shows particles where it is drawn in 3D, or let a
program ask for them in a 2D texture by a call naming the camera, as `DrawParticles(camera)` inside
`BeginTextureMode` would. Nothing is done for it until the reviewing session or the owner says
which.
