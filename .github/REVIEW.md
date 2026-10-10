# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `0f39daf5`. Item 2's part g, the last of the game's seven asks, and the item is done.
Measured first: a box sealed on every side read 37.5 in blue on its rough inside walls under a blue
sky and an open floor beside it 62.7, the game's carved chamber with its own dimming off (6.6, 13.3,
24.4). The probes say how much sky they see: a ray of the last cascade that meets nothing brings the
sky's light marked a quarter in its alpha, which the merge reads as it reads a hit so nothing else
moves, each merged texel carrying its share of the sky in an image of its own, carried down as the
light from beyond is, the gather weighing it into a sixth image of faces, and the model pass
weighing the environment map's reflection by the share the surface's probes see along the mirror
direction, read from the eight probes around it with the light's own weights and visibility. After:
the sealed box 0.13, the open floor 61.8, and the game's chamber (0.1, 0.1, 0.1), as with no map at
all, so the game's dimming of the whole map goes; the sealed box is a test. The cost 0.01 ms of the
bounce at Medium and High and 0.14 to 0.84 MB, the guide's table measured again; the game's scene
pass 1.55 to 1.65 ms in Release, reading eight probes more where a map is set; Wick without a map
unchanged. The glossy reflections' reference is drawn again, the floor under the shelf and the
chrome sphere's underside reflecting less sky as their probes see the floor, looked at, 1.37% from
the old on the RTX 4070. Two gaps noted and not asked: the screen's probes carry no share of sky,
the world's standing in, and a reflection probe's capture stands in for the map unweighed. Right,
the probes asked what they already knew, measured on the game's chamber and the box both ways, and
the merge's and gather's hand-listed bindings found as the first try's fault and said. The suite:
1,642 passed; on lavapipe 362 passed and 7 skipped with no validation error. Item 2 is closed in the
Now list; by it, item 3's remainder next, unless a page or a verdict comes first.

Before it, Verdict 40's census at each step came: on Windows each step of the four followed apps
ends with the kinds of handle it changed, as `SDL plugin built +4 (+3 Thread, +1 Event)`, the kinds
read against the step before, so the next Windows page names the step that opens the thread handles
the apps keep and whether `ended` gives them back; elsewhere it reads nothing. Right, the smallest
change that makes the next page say the thing. The suite: 1,641 passed. Part g next, the last of the
game's asks.

Before it, item 2's part f came to be read, a changed mesh's light, the fix the measurement chose: a
still mesh replaced in place, by a mesh of other vertices drawn through the same matrix whose bounds
overlap it, stays in the field as it was until the replacement is still, the replacement not stamped
meanwhile, so one build takes the one out and puts the other in and an emitter inside a replaced
mesh stays lit; a mesh that moves keeps its vertices and is stamped as before, and one kept for a
replacement that never settles leaves after sixteen frames. The probe is a GPU test, three walls in
one mesh around a lamp drawn apart and replaced at a frame by a mesh of four, which without the
change reads the wall at 107, 98, 91 and down to 69 after 116 and with it holds 114 to 116, the
floor rising from 85 until the replacement settles; two unit tests hold the plan to keeping the
replaced mesh, building the swap once and letting go after sixteen frames; §4 says it with its
numbers; Manor and Wick hold their times. Right, measured first, the fix the measure chose rather
than the one the item named first, and the probe kept as the test. The suite: 1,641 passed; on
lavapipe 361 passed and 7 skipped with no validation error. Verdict 40's census at each step next,
then part g, the last of the game's asks.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do. This
list is long on purpose, and a batch that ends is followed by the next item with no wait for a
reply. In this order, which the owner set on 2026-10-09: the runs first, then the bounce's quality,
then subsurface scattering and what follows it, and item 7 for a wait.

1. **What the next page says.** The runs of `88673244` and `c6579710`, the first with Verdicts 40
   and 44's fixes and the redrawn references, are green on Linux, on macOS whole and on the macOS
   captures, 254 of 254 drawn, and red on Windows by the leak test alone, so the Cornell reference
   frame passes on both devices and Verdict 44 is settled; the Linux examples job with its soak and
   measure (Verdicts 30 and 31) still waits on a run with Windows green. The run of `4f03a8cb` reads
   the same, its captures green. The run of `0e982877`, the first with the handle census, is green
   on Linux and red on Windows by the leak test alone, whose step line names the kept handles for
   the first time, threads' (Verdict 40); its macOS job and captures green after; `2dd527f1`'s run
   reads the same, and the run of the game's `0ccf8f9a` has begun, the first with the census at each
   step (`a063a336`), whose Windows page names the step that opens the kept thread handles. The page
   showed the leak test's progress line as its first of 198, `app 2`, where the line named its app
   all along; the page shows the last of a repeated line since `88673244`. Verdicts 40 and 44 are
   carried out there, the leak test following four apps through every plugin's making, a failing
   frame written where the job uploads it with each surface's means, and four references drawn again
   after a drift of up to 2.16% on the RTX 4070 itself; the next pushed run judges both. The
   examples job waits on Windows and macOS both green in one run. When every job is green the owner
   is told, since 5.2 is due (Decision 19). With the batch that next touches `build/test.py`, it
   takes from BevyCSharp's `1f68fde8` the two cases of a theory whose names are cut to the same as
   one counted apart, which its page reads as one today (SHARED.md). The engine's own despawn of
   what a state scopes is read against a soak of the world's entity indices across many transitions,
   which in BevyCSharp found Bevy 0.20.0 losing every index it despawned that way (SHARED.md), with
   the batch that next touches states. Each push's run is read by the reviewing session, and a
   failure it names comes first here.

2. **What the owner saw on 2026-10-10 (Decision 27), before item 3's remainder.** The owner looked
   at `shaders_subsurface` and at the voxel game and found four things, each measured before it is
   changed, each a commit of its own with its numbers before and after and a test that holds the
   measure, the voxel game's read through the testbed at eight columns, and the game's session asked
   to write what it measures of them in ASKS.md. The game's session measured b, c and d on the game
   and wrote them in ASKS.md with captures under `.github/assets/asks`, which go in with that file.
   The order of work: a, then b, then d, then c.

   **a. A dark ring at the terminator of every scattering sphere, and a jagged red line on the
   skin's.** In `shaders_subsurface` all three spheres that scatter show a dark line along the ring
   where the light's direction and the normal are square, the scattering beginning too far into the
   shadow where it should wrap the terminator, and the skin sphere, whose radius is 0.06 under cells
   of 0.15, ends its red light through on the dark side in a zig-zag edge, where the wax's and the
   marble's end smooth. Measured first: the frame's light along a meridian of each sphere from the
   lit pole through the terminator into the shadow, read every degree with the spread on and off and
   against a Burley profile worked out for the sphere, which says where the dip lies and how deep;
   and the angle of the light-through band's edge around the sphere, whose variance is the zig-zag.
   The dip's suspects, in order: the spread down taking away the pixel's own diffuse where the
   spread across under-fills near the terminator, so a pixel ends darker than it began; the model
   pass's diffuse and the scatter pass's disagreeing at the terminator, one wrapped or softened and
   the other cut at zero; and the light through at grazing angles, where the march runs along the
   surface. The edge's suspect is the thickness read texel by texel from the sun's map where the
   field's cells are too coarse for skin's reach, which the nine-sample mean softened and did not
   smooth; the thickness is read with a wider filter or from the field at a finer reach, measured.
   The tests hold the profile falling without a dip across the terminator and the band's edge within
   a bound of its variance.

   **b. The bounce drifts for 13 to 18 frames after the camera moves in the voxel game, then
   holds.** Measured by the game's session in a closed room of white concrete at night with one
   glowstone, the light levels and corners off: still, the picture changes nothing; after a turn of
   ninety degrees in one frame it changes 1.02 sRGB levels on the first frame after and 5.41 summed
   over 18 frames, after a walk of four blocks 0.57 and 2.63 over 15, under one pixel in ten
   thousand past 8 levels, so a faint drift over the whole room; the difference between the first
   frame after the walk and the settled one is a grid of patches on the walls and the floor at the
   first cascade's probe spacing of two blocks, up to 12 levels, which the walk moves twice. With
   the frame before's light left out, or the screen's probes, the drift after the walk is gone and
   the turn settles in 5 or 6 frames, and the other parts change little; on the hills by day the
   same motions settle within a frame. So the drift is the screen probes' history converging after a
   motion, the world probes' light they take beyond their rays having changed as the cascades
   shifted with the walk and the old history standing while it blends toward the new over the frames
   of its weight, the patches being the world probes' cells as the screen probes read them. The
   cause is read first: why the history's hold within this frame's spread lets the old value stand
   where the change is coherent over a patch; then the remedy measured against it, the blend
   tightened or shortened where the frame's light moves coherently, or the history let go where a
   cascade moved, each by the summed drift after the walk and the turn, and the slide test, which
   must not grow. The test holds the drift after a walk under a bound by the fifth frame in the room
   above.

   **c. The border between near and far is the field's last cascade's end.** Measured by the game's
   session on the hills at sunset with the sun under the horizon, the bounce at High over the light
   with the bounce off, which takes the environment map's: 0.56 to 0.80 of it from 60 to 96 blocks
   and 1.00 from 122 on, the step between 96 and 102 blocks where `field.state` ends the last
   cascade; doubling the cell moves the step past the 148 blocks drawn, and the shadows taken to 200
   leave it where it was, so the line is the field's and not the shadows'; by day the bounce and the
   map differ under 8% and no step shows. Past the last cascade a surface takes the map's unoccluded
   light, up to two thirds brighter than the bounce inside at a low sun, and nothing blends the one
   into the other. The near is blended into the far across a band at the last cascade's edge, the
   probes' light fading to the map's over the outer part of the cascade's reach as the light's
   cascades are merged into one another, and the field's distances the same where a pass reads them
   to the edge; measured by the luminance along the view's middle rows across the edge at sunset,
   and a test holds the step across it under a bound.

   **d. Eight lobes of light around a glowstone on a floor at High, four at Low.** Measured by the
   game's session straight down over one glowstone on grass at night, the floor's light read at 72
   points around a circle with the moonlit floor's own taken off and the lobes read as the harmonics
   of the ring: at High eight lobes at 0.15 of the lamp's light at two blocks and 0.39 at two and a
   half, at Medium eight at 0.03 and 0.11, at Low four at 0.11 and 0.16, under 0.07 within a block
   and a half, and the lamp's light down to the floor's own by three blocks. The cause is the
   probes' octahedron, the few of its directions that meet a small emitter a probe or two away, and
   the first cascade's 64 directions at High where the cascades above have 256, so the ring of eight
   texels shows as eight lobes. Measured against each other, cheapest first: the first cascade given
   256 directions at High, a quarter more rays, read for the lobes' depth and the cost; the rays'
   directions jittered each frame within their texels and the faces averaged over frames, which the
   hold's follow must not read as a light changing, read for the lobes, the fade and the crawl; and
   a small emitter near a probe taken by its rays' hits as a light with a position, as the lamps
   are, read for the lobes and the cost in the game's room of many lamps. The test holds the lobes'
   depth at two and a half blocks under a bound at each quality.

   The game's seven asks that stood here are done at `0f39daf5`, a to g each with its commit in the
   history of this file and RENDERING.md, the game's frame at eight columns from 25.5 ms to 8.9 in
   Debug and its sealed chamber from a blue sheen to dark.

3. **Subsurface scattering, the first of Decision 16's spectral experiments (Decision 18), in three
   batches, each measured, what of its first batch stands alone committed before item 2 begins.** A
   material gains what skin, wax, marble and a leaf have, a subsurface color and a radius in world
   units with a thickness scale for its thin parts, set in the flat API as the material's other
   fields are and read from a glTF file's `KHR_materials_volume` thickness where it has one. First,
   the diffusion: light that enters leaves nearby, so the lit light of the marked pixels is spread
   along a profile of the material's color and radius in a separable screen-space pass over the HDR
   frame, the specular kept out of it where the frame has it apart, masked so an unmarked pixel is
   never touched and the spread never crosses a depth edge, with a test of a lit sphere whose
   terminator softens and bleeds the color where the unmarked sphere beside it does not, read at
   pixels as the occlusion tests read theirs. Second, the light that comes through: a thin part lit
   from behind shows the light on its front, the thickness toward the light read from the scene's
   distance field, which has it for nothing where the field is fine, and from the sun's shadow depth
   where it is coarse, with a test of a thin sheet lit from behind brighter on its front where it is
   thin than where it is thick. Third, the tiers: sample counts and a half-size pass at Low and the
   full at High, as the bounce is tiered, the kernels in Slang so the bridge runs them in BevyCSharp
   once proven here, the GPU cost of each batch on Manor and on the test scene in RENDERING.md and
   the comparison page, the guide's section and TODO.md's entry. Each batch a commit of its own with
   its numbers, and what does not pay a measured share stays described. The second batch is in at
   `d885c89e`, the light that comes through. The third brings the tiers as a setting of their own,
   `SetSubsurfaceQuality` with Low, Medium and High and High the default, each timed on Manor and on
   a new `shaders_subsurface` example with its capture and README row, Low's half-size spread laid
   onto the frame where the pixel's depth matches, and the two things the reply of `d885c89e` left
   for it, a material's own thickness from `KHR_materials_volume` and a lamp's light through from
   its shadow map where there is no field. The third batch is in at `94c4bf3b` with the tiers and
   four mends of the light through, and what remains of the item comes after Verdicts 40 and 44 and
   before item 4: the thickness read from `KHR_materials_volume` into the material for a part the
   field holds thicker than it is or not at all, a leaf drawn as one sheet, which the example's leaf
   shows; a lamp's light through a part where the field is coarser than half the reach, from the
   lamp's shadow map as the sun's gives it; and the spread in render textures and probe captures, or
   the reason it stays out written with its number.

4. **The animated model's meshes in the world (Decision 21), after subsurface scattering.**
   TODO.md's "Models are partial" says an entity an `AnimatedModel` draws keeps its copy's meshes
   out of the world, so it is given a capsule or a box and a mesh or hull collider on it is refused
   with a warning. The copy's meshes become the entity's meshes in the world, posed from the same
   joints as its wires and a body made from it are, so `CreatePhysicsConvexHull` and a mesh collider
   take them as they take any mesh, with a test of an animated entity whose hull follows its pose,
   the guide's section on animated models and the entry updated with what it then says.

5. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.

6. **Three packages bumped, after 5.2 is packed (Decision 20).** Vortice.Vulkan 3.2.1 to 3.3.0,
   AssimpNetter 6.0.4 to 6.0.5 and StbImageSharp 2.30.15 to 2.30.16, in one commit with the suite
   run and the notices written again, on the owner's word typed into this session once the package
   is out, as AGENTS.md has it for a version; SDL3-CS stays on its preview and NVorbis on 0.10.5.

7. **A game is written meanwhile.** When the items above wait on a run or on the owner, the next
   game of `games/` is written, as the owner asked on 2026-10-07, a later game finding nothing new
   being the point of each.

## Verdicts

Verdicts 1 to 29, 32 to 39, 41 and 42 are settled, and their numbers are not given again.

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

40. **The Windows job of `0e982877` fails the hundred-app leak test's handle hold at app 65, 2,728
    handles against 2,698 allowed, and its step line names the kept handles for the first time:
    threads'.** Read from the page: the four followed apps keep, by kind, −3 Event and +3 Thread, +6
    Thread and +2 Event, +5 Thread, and +5 Thread, while the process's threads stay at 24 to 26
    throughout, so the handles kept are handles to threads that have ended, left open by whatever
    started them, five an app or so, on Windows alone, where a thread's handle outlives the thread
    until it is closed and no finalizer closes a native one. The compiler is ruled out (`0e982877`),
    the 315 the renderer's step takes and gives back being lavapipe's own. The census runs at each
    step of the followed apps since `a063a336`, so the next Windows page says at which step the
    thread handles are taken and whether `ended` gives them back, naming the starter: SDL's threads,
    the audio device's under the dummy driver (Decision 25) and its timer, the CLI's listener, the
    asset workers, the physics' workers and the behaviors' compiler are the engine's own starters,
    and lavapipe's rasterizer threads the device's. And the starter named is read for a thread
    started and never waited on or detached, an `SDL_CreateThread` without its `SDL_WaitThread` or
    `SDL_DetachThread`, a handle kept after a join, and closed where the app ends, or said to be the
    driver's with the evidence. The bound stays. Settled when a Windows run passes whole.

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

18. **Subsurface scattering is the next large item, the first of Decision 16's spectral
    experiments.** The owner chose it on 2026-10-09, the three phases having shipped with their
    numbers, over the other four extensions and over another game, as an experiment of its own,
    measured and tiered as the phases were, its kernels in Slang so the bridge runs them in
    BevyCSharp once proven here.

19. **5.2 is packed when every job is green, before subsurface scattering lands.** The owner chose
    it on 2026-10-09 over one release after subsurface scattering and over none for now, 107 commits
    having landed since 5.1 was packed on 2026-10-06; the owner sets `build/version.txt` to 5.2 and
    runs the pack workflow when the reviewing session says every job is green.

20. **Three packages are bumped after 5.2.** The owner allowed on 2026-10-09 Vortice.Vulkan 3.3.0,
    AssimpNetter 6.0.5 and StbImageSharp 2.30.16 in one commit after the package, with the word
    typed into the working session; SDL3-CS stays on its 3.5 preview, whose stable line is 3.4, and
    NVorbis on 0.10.5, its newer release a prerelease.

21. **The animated model's meshes in the world follow subsurface scattering.** The owner chose it on
    2026-10-09 over another game and over leaving the list open, TODO.md's "Models are partial"
    being the one gap a game is likely to meet.

22. **The bounce's quality comes before subsurface scattering goes on.** The owner chose it on
    2026-10-09, judging the light that bounces on the Cornell box and on Wick as not yet the best
    and naming banding, light across edges, a bounce too soft and wide, and one too dim at a
    distance; a path-traced reference the engine draws itself is the measure, over Bevy's Solari in
    BevyCSharp and over the eye alone, a level of the hard cases is made since no project checks the
    bounce, and a debug window with gizmos and texture views shows it; subsurface scattering's first
    batch is committed as far as it stands alone and goes on after.

23. **No document or comment a reader sees names the owner or a session.** The owner ordered it on
    2026-10-09 after DESIGN.md's NLayer row said who admitted the crate and when; N 4.7 reaches
    every Markdown file but REVIEW.md, SHARED.md, NORM.md, AGENTS.md and COMMITS.md and the comments
    of every source, script, manifest and workflow, in both repositories, and who chose what stays
    in these Decisions.

24. **Content streamed on the go stays in the ledger, and the browser waits for a game that asks.** The owner chose
    on 2026-10-09, after reading a browser port of a large game that downloads its world as it is
    played, that the idea is recorded in SHARED.md as the file layer that port has (packs on a
    static host, reads by byte range into a block cache, a recorded first-run set, prefetch by the
    game's own streaming), to consider until a game here ships a world too large to download first,
    over a streaming file layer and over an HTTP source alone, since Manor's cells from disk are all
    a game here needs; and that the browser waits for a game that asks, its shape recorded for that day:
    .NET's browser runtime, SDL3 built with Emscripten and linked into it, a WebGPU
    `IGraphicsDevice` beside the Vulkan one without ray queries, bindless or 64-bit atomics, Slang
    to WGSL, threads behind cross-origin isolation, and a one-week feasibility spike before any
    commitment.

25. **A run that shows no window makes no sound.** The owner ordered it on 2026-10-10, since a
    hidden or offscreen run, a test or a soak, played through the machine's speakers; its audio goes
    to SDL's dummy driver, sounds still run their course, and a config field turns real audio on for
    such a run.

26. **A game's session writes what the engine lacks in `.github/ASKS.md`.** The owner ordered it on
    2026-10-10, when the session making a voxel game in `3DEngine.Game` found the sun's shadows
    costing the GPU 14.8 ms and the CPU 13 ms over some 1,800 draws, every chunk drawn once a
    cascade with no culling, and had no way to tell the engine's session: the game's session writes
    an entry there with what it measured and how, the reviewing session turns it into an item of the
    Now list by its weight and writes the item's number under the entry, and the engine's session
    reads REVIEW.md as before; AGENTS.md names the file beside SHARED.md and NORM.md, with the
    owner's word, and the engine's session writes nothing in it. ASKS.md and AGENTS.md's bullet and
    row on it are the reviewing session's and are committed with whichever batch comes next, as
    REVIEW.md is; a game's own files, `3DEngine.Game` and the solution's line among them, are the
    game's session's to commit.

27. **The owner's eye on 2026-10-10 opens item 2 again, four things seen.** The owner looked at
    `shaders_subsurface` and the voxel game in the afternoon and said what they saw: a dark ring
    along the terminator of every scattering sphere with the scattering beginning too late, and a
    jagged red line where the skin sphere's light through ends; the voxel game's bounce changing and
    looking bad while moving, flickering for a second after a move and then holding, which turning
    one part of the bounce off takes away at the cost of a lacking picture; a border between near
    and far too sharp in the distance; and some eight spokes of light around an emissive block seen
    from above, where the Cornell box and the bounce rooms look smooth. The owner asked that these
    be thought about and fixed; each is measured before it is changed and held by a test, as
    Decision 22's were.

## Replies

Item 3's remainder, the first of its three: a material's own thickness. `ModelMaterial` and a mesh
entity's `Material` gain `SubsurfaceThickness`, how thick the material's parts are at most in world
units, which a glTF file's `KHR_materials_volume` thickness sets through Assimp's
`$mat.volume.thicknessFactor`; it rides beside the batch's profile as a second attribute of the
scatter pass's fifth binding, the batch keyed by it, and the scatter pass takes the lesser of it
and the thickness measured, or it alone where neither the field nor the sun's map measures. Two
cases it answers, each now a test: a sheet with a lamp behind it and no field to measure toward the
lamp read (39, 39, 39), the ambient alone, and (136, 120, 109) with five centimeters given; and a
glTF file's leaf material reads its 4 mm. `shaders_subsurface` draws its leaf as one sheet two
millimeters thick by its material, where it was a box two centimeters thick, and its capture is
drawn again; the guide, which had a stray word and a sentence run into its table's last row, the
CHEATSHEET's line, the upgrading page, PublicApi.txt, RENDERING.md §5 and TODO.md say it. Nothing
draws more: the profile buffer is 32 bytes a scattering batch in place of 16.

And the second, in the same commit, since it changes the same thickness function and its slabs'
test: a spot or point light that casts shadows measures the thickness from its own map where the
field is coarse, as the sun does, the mean of nine depths a texel apart in its square or face, read
through the lookups its shadow shares, now `spotTile` and `pointFace` in `lights.slang`. From the
light a point's clip coordinates are the light's plus its distance times the way's, so the depth
read gives the distance to the face nearest the light in one division, whatever the face's near
and far planes. The slabs lit from behind, a case each now, read the thin one (149, 133, 122)
through a point light's map and a spot light's alike, the field's lamp (143, 118, 102), and the
thick one (48, 39, 39) where the field gives (59, 39, 39); before, nothing measured toward a lamp
with no field and the thin slab read the ambient 39. Part (a) alone passed lavapipe, 363 and 7
skipped with no validation error, before (b) was written; the two together, 365 and 7 skipped with
no validation error, and the suite 1,646 passed. What remains of item 3 is the spread in render
textures and probe captures.
