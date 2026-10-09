# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `12b1f0c3`. Three commits. ImGui's frame begins in `First` (`096797bc`), the plugin
ordered late and after the command line's, so the frame's time comes first, then the served
commands, then ImGui's new frame, then a program's own systems, with a test of a window made in
`First` drawn in the frame `Update` draws in; right, and the order is the plugin's `Order` and not
its place in the list, the sturdier of the two. Item 2's sweep measured and took nothing
(`27c60b1b`): `models.blocks` counts the instances copied into the ring and those in blocks some
pass drew, `models_stress` holds a count and turns a few, and neither change pays, no block undrawn
at 410,266 or 3,000 with the camera over its grid nor in Manor, Summit or Pusher, and a chunk
gathered for its moved entities alone within the readings' noise, 1.850 ms against 1.852 at 410,266,
so both stay described with their numbers; they were measured where the camera sees every block,
which is where a culled copy can gain nothing, so the entry says so and the question is asked again
when a game has a level larger than its view. A run that shows no window neither reads nor writes
`imgui.ini` (`12b1f0c3`), so a capture is the same whatever was dragged before, with the docking
drag's numbers given again and a test finding no file; right. The disputed second limit of item 3 is
decided the coder's first way, particles staying where a texture is drawn in 3D, since a texture
drawn only in 2D is an interface, a minimap or a canvas and would show the window's smoke a second
time, and the guide says so (item 2). The suite: 1,576 passed. The runs: Verdict 39 settles, the
captures passing in a job of their own at `8ac5912a` in 52 minutes of the runner's; `8ac5912a` is
red on Windows and `096797bc` on macOS, Verdicts 40 and 41, and `12b1f0c3`'s run is under way, Linux
green. TODO.md's limits are spent, as the last pass said, and no large item is queued, so the next
is the owner's to name and has been asked for.

Before them, item 2 came to be settled, where each emitter laid over by alpha keeps the eye its
buffer was last sorted from, the step sorting from the window's as before, and
`ParticleRenderer.SortFor` sorts it again from a view's own eye where that differs, outside any
pass, the targets node for each render target before its pass and the HDR scene node for the window
before its particles' pass where a target sorted after the step, a probe's faces drawing in the last
order sorted and a render texture drawn only in 2D still drawing none; a new test draws one stream,
blue born at the back and red aging toward the front, into two render textures from either end, the
front view showing red in front and the back view blue, where the back view showed the window's
order; Sumo's dust is additive and never sorted, so an emitter of 300 laid over by alpha was added
to its ring through `./e3d eval`, its two views taking 0.377 to 0.383 ms of the GPU in `targets`
where they took 0.352, some 0.013 ms a sort, written in the guide and RENDERING.md §3, TODO.md's
sentence gone and the 2D render texture's want of a camera kept (`8ac5912a`). Item 2 is settled, and
its number goes to a measured sweep of the per-entity cost entry, after item 3, which the coder has
started, since TODO.md's limits are spent and the owner has not named the next large item. The
suite: 1,574 passed, and on lavapipe the rendering tests, 308.

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

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do. This
list is long on purpose, and a batch that ends is followed by the next item with no wait for a
reply. In this order. It is short today because TODO.md's limits are spent and no large item is
queued; the owner has been asked for the next, and item 4 fills the wait.

1. **What the next page says.** Verdict 39 is settled: the macOS captures ran in a job of their own
   at `8ac5912a` and passed, 52 minutes of the runner's for the coder's 25, the test job beside them
   green in 30. `8ac5912a` is red on Windows, the leak test hung at app 79 of 100 and the hot reload
   test's texture never came (Verdict 40); `096797bc` is red on macOS, the suite lost to a crash in
   a particle test and whole in its parts (Verdict 41); `12b1f0c3`'s run is under way, Linux green
   at 17:59 UTC. The examples job, which carries the guides' blocks and Verdicts 30 and 31, runs
   once a run's three test jobs pass, so Verdicts 40 and 41 come first. Each push's run is read by
   the reviewing session, and a failure it names comes first here.

2. **A render texture drawn only in 2D keeps drawing no particles, and the guide says so.** The
   coder's first way, chosen over a call naming a camera: particles are the world's, and a texture
   drawn only in 2D is an interface, a minimap or a canvas, which would show the window's smoke a
   second time with no depth to hide it; a program that wants them in a texture draws that texture
   in 3D through `BeginMode3D`, which draws them already. One sentence in the drawing guide beside
   the render textures, and TODO.md's particles entry closed with the reason.

3. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.

4. **A game is written meanwhile.** When the items above wait on a run or on the owner, the next
   game of `games/` is written, as the owner asked on 2026-10-07, a later game finding nothing new
   being the point of each.

## Verdicts

Verdicts 1 to 29 and 32 to 39 are settled, and their numbers are not given again.

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

40. **The Windows job of `8ac5912a` loses the suite to a hang in
    `AppLeakTests.An_Offscreen_App_That_Draws_Made_And_Closed_A_Hundred_Times_Leaves_Nothing_Behind`,
    at app 79 of 100 whole and at app 78 in the Core part, and fails
    `AssetReleaseTests.A_Model_Spawned_Again_By_Hot_Reload_Lets_Its_Texture_Go_With_It`.** Read from
    the page. Both hangs are in a new app's Startup stage after `ImGuiRenderNode registered in
    render graph`, five minutes without a line, and the handles the test prints climb through the
    run, 1704 at one app and 2527 at the 79th whole, 1113 at the 78th in the part, where the Vulkan
    objects stay at none; `b526089c` passed the test on Windows in 29 minutes and `096797bc` passed
    it after, so it hangs on some runs and not others and nothing yet says why. Three things. The
    test holds the process's handles as it holds the Vulkan objects, to the twentieth app's count
    and a small allowance, so a handle kept per app on Windows fails with a number before it can
    hang. The Startup stage's systems each log a line at debug, so the next hang names the system it
    stopped in, where the page names the stage alone. And the hot reload test waits up to 300 frames
    for a texture a loader thread brings, a wait in frames on another thread's work, which a runner
    busy with the leak test's hundred apps beside it can miss, so it waits on the load itself with a
    bound in seconds, as a loader's own tests do, and does not retry. Settled when a Windows run
    passes whole.

41. **The macOS job of `096797bc` loses the suite to a crash in
    `ParticleTests.A_Textured_Particle_Is_Drawn_As_Its_Image_The_Right_Way_Up_And_Square` after 2 m
    22 s, the runtime writing a dump, and passes whole in its parts, 1,538.** The test's app is its
    own, opened by `Open()` and closed in `Dispose`, and the test ends with `UnloadTexture` while
    the emitter drawn with that texture lives until the close, so whether the particle pass or the
    close touches the freed texture's view on MoltenVK is read first, the test's last frame and the
    shutdown's work; the test passed on macOS at `8ac5912a` and `b526089c` and in this run's parts,
    so the crash is one of timing or of a freed thing and not of the drawing itself. The dump is in
    the run's macOS artifact behind a sign-in, which the owner downloads and names a path to if the
    reading finds nothing. Settled when a macOS run passes whole.

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

Taken while the list waited, before this pass arrived, the effects entry's limit that render
textures were drawn without ambient occlusion. A render texture that draws meshes through a camera
now draws a depth of its meshes that cast shadows at half its size, works out its occlusion and the
sun's contact shadows from it as the window's are, in the targets node before its pass
(`AmbientOcclusionRenderer.DrawTarget`), and binds them for its model pass through
`TargetOcclusion`, its buffer saying to read them, all let go the frame after one it is not drawn
in. Its screen probes, where light bounces, stand on that same depth in place of one drawn for them
alone, so a target with both draws its depth once, and a program drawing its scene into render
textures alone keeps the targets' images while the window, drawn in 2D, lets its own go. A new test
draws the cube in its corner into a render texture of the window's size, the floor beside the cube
and in the corner darkening within 9 levels of the window's, summed over the three channels, and
the open floor not at all; before the change the texture's floor read as it does with the
occlusion off. Sumo's two views at 640 by 720 take 0.775 ms of the GPU in `targets` with it on
where they take 0.678, the medians of three runs of seven profile readings each, and 1.15 where
they take 1.05 with the bounce at `Low`. The guide, the API's remarks, RENDERING.md §4 and §5 and
TODO.md say so, and RENDERING.md's sentence placing the occlusion's node after `shadows` now places
it after `particles`, where it has run since `c7ee9c72`. Seen while writing the test, a render
texture is upright here, so the bounce test of `c7ee9c72` drew its texture upside down with a
negative height and passed because the rows it averages are their own mirror. It draws it upright
now. The suite: 1,577 passed; on lavapipe the rendering, compute and particle tests, 309 passed
and 2 skipped. Verdicts 40 and 41 are next.
