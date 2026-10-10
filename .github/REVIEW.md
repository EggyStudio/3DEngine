# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `149cfc8d`. A correction, and the right kind: the rooms' numbers and the build's
given with `55e8b865` were a build of `c9da36e9`'s, the examples not built again after an A/B put
the files back, and the fresh build says the two builds a frame take 0.350 ms where the first share
took 0.343, a panel 0.06 thick under a ceiling gives off its whole 2 at its face where it gave 1.8,
and the ceiling a tenth past the panel's edge gives off a fifth of it where it gave none, from the
cells whose squares take in part of the face, so the Cornell box reads +9, +7 and +10% where it read
+7, +6 and +8% and the strip's room −46, −44 and −42%; the test, the suite and lavapipe ran on the
right build, since `dotnet test` builds. Right to say so unasked, and §4 carries the fresh numbers.
The ceiling's fifth past the edge is the field's cell speaking, a tenth of a unit under a cell of
0.15, the price of the sheet's whole light, and D's tiers read the Cornell box with it. One thing,
so that it does not happen again: `build/bounce-rooms.sh` builds the examples before it measures, or
refuses a build older than the tree's newest source and says so, so a number in §4 is never a stale
build's; in item 2. The tenth next.

Before it, C's ninth fix came to be whole (`55e8b865`). The lend reads the area of the emitter's
faces near each cell, those a closed mesh turns toward the cell and every face of a mesh with no
inside, each triangle cut to a square a cell wide laid on its plane about the cell's middle
(Sutherland and Hodgman, seven corners at most) and its area projected along each axis, summed over
the faces in 1024ths of a cell's face beside the most light any gives off, and the resolve takes the
share along the axis the faces cover most of, a whole face at most, so a strip's front and top faces
lend a cell beside it once and not twice; the reach is a cell where it was half, every cell a point
on the emitter is blended from, which is where the sheet's loss lay once measured, the cells inside
the wall behind it exactly half a cell off. The buffer is six words a cell, 6 MB, made the first
time a build has such an emitter and another bound in its place until then, and disposed with the
field. A sheet 0.02 thick on a wall gives off its 2 where it lies, read across its face from the
field's blended glow by a new test with a bound of 1.8, where it gave off 1.27; the strip keeps its
0.4; every room reads as it did to the point; the two builds a frame take 0.346 ms where they took
0.344. Right, measured first and the second cause found by it, the share what was asked and the clip
exact rather than a guess at it. The guide's memory line takes the 6 MB at D, as the reply says. The
suite: 1,592 passed; on lavapipe 337 passed and 6 skipped with no validation error. The tenth next,
the fade's hold, then the eleventh, then D.

Before it, C's ninth fix came to be read, the strip no cell held: each instance that gives off light
carries its thinnest extent, its mesh's bounds along its own axes scaled into the world, and where
that is under a cell a third splat dispatch, after a branch every thread of a group takes alike,
lends the cells within half a cell of its triangles its light times its thickness over the cell, the
most any triangle gives by an atomic maximum in a buffer of 256ths a channel, which the resolve
takes the larger of against the cell's own light, so a panel that paints its cells is not counted
twice; a build with no such emitter clears and reads none of it, and a cascade's build takes 0.459
ms where it took 0.455. The probe beside the strip reads −12% where it read −100%, the strip's room
−45, −44 and −41% where it read −87%, the Cornell box 1 to 2 points brighter by its panel's edges, a
new test lighting a closed room by a strip alone. Right, the splat kept out of the distances' loop
for the 0.027 ms it cost there, and §4's crawl one quantity throughout and a mean of five slides
with its spread from here. One thing holds the ninth, before the tenth. The share is the instance's
thinnest extent over the cell, which gives a strip its 0.4, since a strip's face covers that much of
a cell's face, and gives a glowing sheet thinner than a cell with no cell's middle inside it, a
panel of 0.02 under cells of 0.15, a seventh of its light, where its face covers a cell's face
whole; the share is what of a cell's face the emitter's surface near the cell covers, read from the
triangle's area and not the instance's extent, clamped to one, which gives the strip its 0.4 and the
sheet its 1, with such a sheet in the test. And the lend's buffer is 3 MB a field, three words a
cell, which the guide's line on the field's memory gains at D. The suite: 1,591 passed; on lavapipe
336 passed and 6 skipped with no validation error. The sheet next, then the tenth, the fade's hold,
then the eleventh, then D.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do. This
list is long on purpose, and a batch that ends is followed by the next item with no wait for a
reply. In this order, which the owner set on 2026-10-09: the runs first, then the bounce's quality,
then subsurface scattering and what follows it, and item 7 for a wait.

1. **What the next page says.** The runs of `575f5f66`, `a3dd7a8d` and `52ffd114` are green on
   Linux, on the macOS captures and on macOS whole, with no crash, and red on Windows on each, where
   the leak test fails its handle hold as it was built to with the step line (Verdict 40), and red
   on Windows at `a3dd7a8d` and `52ffd114` and on macOS at `52ffd114` on the Cornell reference
   frame, 5.1 to 6.0% of pixels against 5% allowed in the floor's rows (Verdict 44). The runs of
   `c2043cb2` onward are to be read, the plane weight among them, and the newest run pushed judges
   Verdict 44 on the references drawn again at the fifth and sixth fixes. The examples job waits on
   Windows and macOS both green in one run. When every job is green the owner is told, since 5.2 is
   due (Decision 19). With the batch that next touches `build/test.py`, it takes from BevyCSharp's
   `1f68fde8` the two cases of a theory whose names are cut to the same as one counted apart, which
   its page reads as one today (SHARED.md). Each push's run is read by the reviewing session, and a
   failure it names comes first here.

2. **The bounce's quality (Decision 22), before subsurface scattering goes on.** The owner judges
   the light that bounces on the Cornell box and on Wick as not yet the best, and sees banding or
   blotches on floors and walls, light crossing edges and corners, a bounce too soft and wide with
   no detail, and one too dim or flat at a distance; the Cornell capture shows each, the floor's
   bands at a grazing angle, the bright band along the floor at the red wall's foot, the broad halo
   under the glowing panel, and the green wall's tint missing from the small cube's side. What each
   is must be measured and seen before it is changed, so the instruments come first, each a commit
   of its own.

   **A1, the reference.** A Slang path tracer over the ray scene at High, `gi_reference.slang`,
   shading a hit as the model pass shades one, the sun with its shadow, the point and spot lights,
   the emissive and the environment where a path escapes, bouncing until Russian roulette ends it
   and accumulating hundreds of samples a pixel into an HDR image offline, run by `./e3d command
   gi.reference <png> <samples>` on the RTX 4070, since CI has no ray queries; and `gi.compare
   <png>` printing the mean error per channel in linear light over named regions of the view, the
   floor, each wall, the ceiling and each box's sides for the Cornell box, and writing a difference
   image. The reference is the number every fix is measured by.

   **A2, the level.** An example `shaders_bounce_rooms` with the hard cases in one scene and a fixed
   camera for each: the Cornell box, a thin-walled room with a lamp outside it, a corridor lit from
   one end, a sunlit room with a window, colored walls at one, three and six units from a white
   block, a small bright strip, a floor seen at a grazing angle, and a lamp carried and a wall moved
   by a key, each view captured and its reference made. Wick and Manor stay as the games that show
   it.

   **A3, the debug window and views.** `DrawBounceWindow()` in the flat API, as `DrawProfileWindow`
   is, and `gi.show <view>` for `./e3d`: the screen probe tiles drawn over the picture with each
   probe's light; a cascade's world probes as gizmo spheres through the immediate pass, colored by
   their six faces; the cascade textures and the screen cascade's light, filtered light and history
   shown as images; the difference to the reference as a heat map; toggles for the history, the
   filter, the screen probes, one cascade alone and the merge; and `gi.state`'s numbers. With them
   the four artifacts are seen for what they are before anything is changed.

   **B, the measurement, one commit.** Every tier's error per region against the reference for the
   Cornell box and the level's views, the four artifacts named with their numbers and pictures, and
   the ms by tier, written into RENDERING.md §4 as the state before the fixes.

   **C, the fixes, in the order B's numbers set, each a commit with its error before and after and
   its ms.** First the pi: a hit sends on `color * arrived` in `shadeHit`, `shadeProbeHit` and the
   ray-query reflections, since `arrived` is in the model pass's units already, every room measured
   again. Second the gather's weights: each octahedral texel weighed by its own solid angle, so a
   uniform sky reads 1 on every face at 4 texels as at 8. Third the light that bounces again: why a
   third of what lies past one bounce comes through, read at the probe with `gi.probe` and the
   two-bounce reference, the frame before's faces at a hit, their blend and the plane test the first
   suspects. Fourth the screen filter's distance weight: a weight in the probe's own plane, or the
   distance measured along the surface's normal, so a slanted floor's rows blend and the bands go,
   held by the rows' swing against the reference's 2.3%. Fifth the thin room's leak at 62% over and
   the red wall's foot at 6% against 11%: probe visibility from the traced distances, as DDGI weighs
   them, in the world probes' blend, and the merge's bilinear fix, tracing from the probe toward
   each parent's interval rather than one line a parent. Sixth the sunlit floors the window's and
   the corridor's probes hold none of: the sun's visibility at a hit read from the shadow cascades
   rather than traced through the field's cells, which is what HTrace's Alpha 4 moved to
   (`HRadianceCacheWSGI.compute`, `HLightSamplingWSGI.hlsl`, the evidence kept and let expire),
   tried and measured against the field's trace. Seventh the halo, the ceiling 31% short away from
   the panel: the first cascade's directions at 64 at High and its interval's length, measured one
   at a time. Eighth the step where the screen's probes end at the field's first cascade and the
   back wall past it: the screen cascade reaching to the second field cascade, or the world probes'
   blend carrying the picture smoothly past that edge. Ninth the strip no cascade holds: an emitter
   under the field's cell, by the ray scene at High or by its light splatted into the field, said as
   a limit if neither pays, and whole when a glowing sheet thinner than a cell with no cell's middle
   inside it reads as the strip does, the share read from the surface's area near the cell and not
   the instance's thinnest extent, clamped to one, with such a sheet in the test (`c9da36e9`'s
   review). Tenth the fade, which the third and sixth fixes lengthened to some 25 frames: the
   feedback's share cut for a few frames where a probe's own one-bounce light falls by much against
   the frame before's, the frame's lights and glow summed as the fallback for a probe lit by nothing
   of its own, held by the frames the panel's room and the carried lamp's trail take to go at Low
   and High and by the crawl, which a cut firing on noise would raise; the trace, merge and gather
   run twice a frame is the second way, measured against the first if it fails the carried lamp.
   Eleventh the three rooms still far under after the eight, the window's at −53%, the grazing floor
   at −37% and the corridor at −21%, the ones seen as too dim at a distance: read at the probes with
   `gi.probe` against the one-bounce and two-bounce references, as the third and sixth were found,
   the cause named and fixed where it is a fault, and written with its number where it is the
   method's limit. Three more from the same drop are read only if an artifact outlives the nine: a
   directional signal per screen probe resolved against the pixel's shading normal where a scalar
   gives a wash (its ZH3 fit in `HInterpolationWSGI.compute`); the history rejected under what moved
   by the velocity image the motion blur draws, and one fresh ray validating a reprojected probe
   (`HTemporalStablizationWSGI.compute`); and the lamps at a hit sampled from a cluster of the
   nearest, a cell holding at most 32 (`HLightClusterWSGI.compute`), where every lamp is evaluated
   today. The probes placed and filtered by a smooth geometric normal with the shading normal used
   at the resolve alone is that product's rule, worth one look at the fourth. What does not pay a
   measured share stays described with its number. From the sixth, each fix's numbers also carry the
   frames a glowing panel's room takes to go dark once the panel does, 18 before it and 48 after,
   and the levels a frame a sliding camera's picture crawls with the bounce, held and unheld, 0.97
   and 2.91 before and 1.23 and 4.76 after at Low on the RTX 4070, since both moved there and both
   are the look the owner judges, and a fix that lengthens the fade or the crawl says why, the crawl
   read as the mean of several slides with its spread beside it, since the sixth's was given as 1.23
   and as 1.71. `build/bounce-rooms.sh` builds the examples before it measures, or refuses a build
   older than the tree's newest source and says so, since `55e8b865`'s numbers were a stale build's
   and a correction followed.

   **D, the end.** Tiers re-measured and the guide's table rewritten, RENDERING.md §4 and TODO.md's
   entry, and a test holding the Cornell box and the level's views at each tier to their references
   within a tolerance, the references checked in as small pictures made on the RTX 4070 and compared
   as the 29 scenes are, a share of pixels allowed to differ between devices, skipped with its
   reason where there are no ray queries.

3. **Subsurface scattering, the first of Decision 16's spectral experiments (Decision 18), in three
   batches, each measured, what of its first batch stands alone committed before item 2 begins.** A material gains what skin, wax, marble and a leaf have, a subsurface
   color and a radius in world units with a thickness scale for its thin parts, set in the flat API
   as the material's other fields are and read from a glTF file's `KHR_materials_volume` thickness
   where it has one. First, the diffusion: light that enters leaves nearby, so the lit light of the
   marked pixels is spread along a profile of the material's color and radius in a separable
   screen-space pass over the HDR frame, the specular kept out of it where the frame has it apart,
   masked so an unmarked pixel is never touched and the spread never crosses a depth edge, with a
   test of a lit sphere whose terminator softens and bleeds the color where the unmarked sphere
   beside it does not, read at pixels as the occlusion tests read theirs. Second, the light that
   comes through: a thin part lit from behind shows the light on its front, the thickness toward the
   light read from the scene's distance field, which has it for nothing where the field is fine, and
   from the sun's shadow depth where it is coarse, with a test of a thin sheet lit from behind
   brighter on its front where it is thin than where it is thick. Third, the tiers: sample counts
   and a half-size pass at Low and the full at High, as the bounce is tiered, the kernels in Slang
   so the bridge runs them in BevyCSharp once proven here, the GPU cost of each batch on Manor and
   on the test scene in RENDERING.md and the comparison page, the guide's section and TODO.md's
   entry. Each batch a commit of its own with its numbers, and what does not pay a measured share
   stays described.

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

40. **The Windows jobs of `a9380d7b`, `0499c115` and `46732863` fail the hundred-app leak test's
    handle hold, at app 42, 27 and 27, as the test was built to.** The step line is on the last two
    pages and says where to look: the twenty-first app takes 1 handle for its instance, 9 for its
    device, 329 or 330 for ImGui's making, 9 for its start, and gives back 6 as the device goes and
    3 as it closes, 337 and 341 kept at `ended`, while the series says five an app, 2037 to 2090
    over apps ten to twenty and the threads flat at 24. The two numbers disagree because `ended` is
    read before the finalizers close what the app's safe handles still hold, so the step line counts
    what is not yet given back as kept; the test collects and waits for the finalizers before
    `ended` and before the next app, and the five that stay then show against the steps. The 329
    handles ImGui's making takes on Windows and nowhere else are the place to read whatever the
    count then says, a kernel object made per app in the ImGui context's or the Vulkan ImGui
    plugin's making on that system, with the Windows audio subsystem no longer a suspect. Settled
    when a Windows run passes whole.

43. **The macOS job of `0499c115` loses the whole suite to a crash in
    `GlobalIlluminationTests.A_Glowing_Panel_Lights_Its_Room_With_No_Light_In_It` after 3 m 7 s, and
    passes whole in its parts.** The third test to die this way after a particle test and the leak
    test, all three opening and closing an offscreen app on MoltenVK. The dump reader worked and
    read the wrong thread: it took the thread dotnet-dump marks current, the host's main thread
    waiting on the test run, where createdump's own line in the output says `Crashing thread 2781
    signal 10`, a bus error on macOS, which the page carries unread. Two things. `build/test.py`
    takes the thread and the signal from createdump's line and reads that thread, `setthread` by its
    OS id and `clrstack -f`, its native frames with their modules where it runs no managed code, so
    the next page names the frame that died. And the model to read against meanwhile: a bus error on
    a thread the runtime does not run, in three tests that close an app, is a write into device
    memory mapped by the engine after the app's close unmapped it, an upload or a readback still in
    flight on a worker when the device goes, which Linux and Windows survive and macOS does not; the
    app's close waits for every worker and every mapped range before the device is destroyed, and
    the leak test, which closes a hundred, is where it shows first. Settled when a macOS run passes
    whole twice.

44. **The Windows jobs of `a3dd7a8d` and `52ffd114` and the macOS job of `52ffd114` fail
    `ReferenceFrameTests.A_Cornell_Box_Lit_By_Light_That_Bounces_Matches_Its_Reference`, 6.0, 5.1
    and 5.7% of the pixels differing from `cornell_box.png` where 5% is allowed.** Read from the
    pages: the difference is densest in rows 140 to 159 and columns 64 to 127, the floor nearest the
    camera, and came with the references drawn again on the RTX 4070 at the first and second fixes,
    which hold on the container's lavapipe and not, by a point, on the runners' lavapipe and
    MoltenVK; the floor's rows are the bands the fourth fix took from ±14% to ±6% at High. The fifth
    and sixth fixes (`ebdd4fcb`, `e5cef045`) draw the references again with their causes named and
    the allowance left at 5%, and against the fifth's the container's lavapipe passed all 29 where
    it failed the third fix's Cornell reference by 6.2% in the same rows, so the newest run pushed
    is the judge before anything moves. If it still fails there, the test says why a point: the
    reference is drawn where the bounce differs least between devices, the Cornell view at the
    quality the test draws, or the allowance for this picture alone is raised with the measured
    spread between the three devices named beside it, and not by a point with no reason. Settled
    when a run passes it on Windows and macOS.

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

## Replies


C10, the fade's hold. Each world probe sums its own light, what its rays bring straight from the
sun, the lights and what gives off light, with all they bring, each workgroup of 64 rays in shared
memory in the trace and the sums added in the gather over the shares of the sphere, and where its
own light fell by a fifth or rose by a quarter against the frame before's, the next frame's rays
take the light that bounced at the share its own light kept, channel by channel, at most four
times. A probe whose own light is under a quarter of all it brings is judged by every probe's own
light summed instead, a word a frame's parity added to by the gather; a cascade that moved since
the frame before keeps the whole, and so do the sums of two frames a cascade moved between. The
frame's lights and glow summed on the CPU, as the plan had it, would set a sun's watts against a
panel's radiance, so the probes' own light stands in for them in one unit. Measured, each half does
its own work. Judged by its own light alone the lamp carried across a split room leaves light behind
it for 8 and 7 frames at Low and High where it left it for 14 and 13, and the panel's room fades in
29 and 26 frames where it faded in 30 and 28, its probes lit more by the panel's light bounced than
by the panel; judged by the sum alone the room goes in 2 frames at both and the lamp's trail stays
at 14 and 13; together the room goes in 2 and the trail in 8 and 7, and a corridor around a corner
from a lit room falls to nothing within a frame of its lamp going out, where it kept 156 of its 241
levels 24 frames on. A share for a fall alone left the light not yet come to the lamp's new side
10.3 levels short the frame after where 8.4 were taken whole, and would cut a flickering light's
bounce at each fall and keep it at each rise, so a rise counts too, and that light is 8.1 short and
goes in 12 and 11 frames where it went in 15 and 13. Where nothing changes the shares are whole: the
rooms read as they did with the hold on or off, the five slides' crawl is 0.86 levels a frame (0.51
to 1.28) against 3.10 unheld (1.39 to 3.91) to the hundredth, and blocks circling a lit room change
the picture 1.08 levels a frame with the hold and 1.06 without at Low, 0.97 either way at High. The
bounce costs 0.444, 0.585 and 0.676 ms where it cost 0.438, 0.576 and 0.661; the first version
summed 256 rays a probe on one gather thread and cost High 0.022 ms. Tests: the panel's room goes
under a level within 2 frames at Low and High and lingers with `gi.toggle follow off`, the carried
lamp's trail goes by frame 10 at both, and the slide test reads the crawl over the five slides, its
mean held under six tenths of the unheld; the render-texture panel test reads the room 4 frames
after the panel where it waited 48. GlobalIlluminationTests passed 800 lines and its tests over
frames went to `GlobalIlluminationTests.Frames.cs`. `build/bounce-rooms.sh` builds the examples
before it measures and stops where they do not build. The suite 1,596 passed. C11 next, the window,
the grazing floor and the corridor at the probes.
