# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `b5805aa5`. Item 2's part e, the field's colors from the vertices: each pooled
corner's fourth word carries its vertex's sRGB bytes, white for a mesh with none, beside the open
edge's bit it held, the splat reading the corners as words and blending the three colors in linear
light at the cell's nearest point by their barycentric shares, times the instance's color, as the
model pass multiplies them into the base color; a mesh still settling is stamped in its color times
its vertices' mean, kept per mesh, so a section's boxes are not white for its first frames. A test
draws one white mesh of two boxes with red and green vertices and reads the field's cells by each at
0.79 and 0.77 where both read white, and a unit test holds the stamp's tint; built every frame in
the game at eight columns the field's node takes 0.46 to 0.48 ms of the GPU where 0.41 to 0.45, and
Manor and Wick, with no vertices' colors, hold their times. Right, the field made to agree with the
frame drawn, which is the whole of the ask. Two things noted and not asked: the reflections the
GPU's rays trace still color a copy by its instance alone, a gap of its own if a game asks; and the
game's vertex colors hold its shade, light times corner occlusion, which the model pass multiplies
into the albedo already, so the field agrees with the picture and a shade meant as light belongs in
a stream of its own the game's shader applies, which the game's session was told. Part f is
measured, as its text asked, with a frame-exact probe: a lamp cube drawn apart from a walls mesh,
lit by the bounce alone, and the walls replaced by a mesh with one more block dropped the floor's
light from 85 to 60 and a wall's from 116 to 69 for six frames, recovering by the tenth, the lamp
never changing, since the old walls left the field at once and the new stood in as boxes for eight
frames; so the stamp carrying emission would not touch it and the other fix is written, a still mesh
replaced in place, the same world matrix with other vertices, staying in the field until its
replacement settles, sixteen frames at most, the replacement unstamped meanwhile, the probe then
rising without a dip; its tests come with its commit. The suite: 1,638 passed; on lavapipe 358
passed and 7 skipped with no validation error.

Before it, item 2's part d came to be read, the field's plan on a still scene, measured first inside
the field's node in Release at eight columns over 1,836 meshes: the gather that turns the frame's
draws into the plan's instances took 325 µs and the plan 1,600, since each frame built a new
dictionary of every mesh's instance and asked it and the still meshes' of each mesh, some four
hashes a mesh. A frame that draws the meshes the frame before did, in the same order, is read by
place, one comparison a mesh by its instance, a skinned one by its mesh since its parts are posed
afresh and it is never still, the frames each has been drawn the same counted in place and one that
comes to eight settled as before; any other frame, a mesh added, moved, gone or drawn twice, is
worked out whole as before and its order kept for the next. The plan took 92 µs where 1,600 and the
field's node 0.39 ms of the CPU where 1.8 to 2.0; Wick's field 0.05 to 0.14 ms where 0.19 to 0.32,
Manor's off, the GPU of both unchanged. A test draws meshes that settle, move, leave and a skinned
figure to one plan in the same order every frame and to another turned a place each frame, which
never reads by place, and holds the two to the same still meshes, builds, shapes and bricks frame by
frame, failing with the settling left out of the fast path. Right, the fast path held to the slow
one frame by frame rather than to a picture, and the whole path kept for every frame that is not the
same. What is left of the CPU a draw costs in the field is the gather, 330 µs, and the bounce's node
holds 0.77 ms outside part d, both noted with their numbers. The suite: 1,636 passed; on lavapipe
356 passed and 7 skipped with no validation error. Part e next, written and in testing.

Before it, item 2's parts a and b came to be read, the culling, in one commit since one change culls
for every pass, measured first as the item asks: on the game's scene at eight columns the frame took
25.5 ms, the GPU 14.2 ms for the shadows, 4.9 for the scene and 4.8 for the occlusion's depth, over
9,181 calls, 1,836 in each of the camera's pass, the depth and the three cascades the game's shadow
distance gives, not four. `GpuMeshes` keeps the box around each mesh's positions, none for a skin
the GPU poses; a batch of plain draws is in blocks of 64 as a group is, each block around its draws'
boxes placed by their world matrices by Arvo's bound, a batch with a shader of the program's own
keeping no blocks since its vertex stage may move what it draws; each pass makes its planes once for
the matrix it draws through and passes over a batch it sees none of before binding its buffers, the
first try having bound first and cost the CPU 0.4 ms; and a light's pass adds its near and far
planes, exact since no pipeline clamps depth and the shadow shader writes the light's position as
given, so a block past either is clipped whole whether drawn or not; the field's gather is
untouched, so the game draws its world as before and the engine draws of it what each pass sees,
which is part b. After: 11.9 ms a frame, the GPU 2.3 ms for the shadows, 1.4 for the scene and 1.2
for the depth, over 1,825 calls, 424 the camera's, 423 the depth's and 17, 90 and 871 the cascades',
the CPU for the shadows 1.29 ms where 1.78, no pixel moved by more than one level in 255, and six
columns from 14.3 to 8.4 ms; Manor and Wick hold their GPU times to the hundredth against the
package before, Manor's CPU for its shadows 0.19 to 0.24 ms where 0.29 to 0.32; `models.draws` gives
the calls by pass; a test draws a caster outside a narrow view whose shadow falls on the floor the
camera sees and counts the camera's one call and the cascade's two, a unit test holds the light's
planes, and §6 says it as item 8. Right, measured before and after on the game's scene and the games
beside it, the planes made once, and the field left whole. The game's two entries carry it under
their `Review:` lines. What remains of a comes next in the item's order, the batched draws of meshes
sharing a material, which need the meshes in shared buffers and so are measured for the CPU a call
in Release first to see whether they pay, right, and the far cascades' setting; then c to g.
NormTests names ASKS.md, which is in, and `e57f3fc1` is on `build/norm/7.2.txt`. The suite: 1,633
passed; on lavapipe 354 passed and 7 skipped with no validation error.

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
   the first time, threads' (Verdict 40); its macOS jobs run, and `2dd527f1`'s run has begun. The
   page showed the leak test's progress line as its first of 198, `app 2`, where the line named its
   app all along; the page shows the last of a repeated line since `88673244`. Verdicts 40 and 44
   are carried out there, the leak test following four apps through every plugin's making, a failing
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

2. **What the voxel game asks (ASKS.md, Decision 26), after Verdicts 40 and 44 and before item 3's
   remainder.** The game in `3DEngine.Game`, a testbed built on the engine's project (NORM.md's
   term), wrote seven entries on 2026-10-10 from one scene, seed 1's hills at 1280 by 720 on an RTX
   4070 Laptop GPU with the bounce at High and the sun casting shadows to 96 units, each pass's time
   the average over 600 frames read twice. Each part below is measured on that scene first, through
   `./e3d command profile` and `GetProfileAverage` as the entries were, then mended in a commit of
   its own with its numbers before and after, Wick and Manor measured beside so nothing they draw
   grows, and the entry's `Review:` line says where it stands. In the order of their weight:

   **a. The sun's shadows.** 7.7 ms of the GPU over 1,133 draws at six columns of sections and 15.0
   ms over 1,836 at eight, 1.4 and 2.3 ms of the CPU: the renderer culls instanced groups of mesh
   entities in blocks of 64 but draws a plain `DrawMesh` whole into each of the four cascades,
   behind the camera and outside every cascade included, so the shadows grow faster than the draws.
   Each cascade draws what its frustum holds and what can cast a shadow into it, a caster behind its
   far plane or outside its sides passed over; a cascade's draws of meshes sharing a material issued
   as one where they can be; and, where a game asks, the far cascades drawn every other frame or at
   a lower resolution, a setting with its cost. Done at `d1031bc3`, 14.2 to 2.3 ms of the GPU, and
   closed: the game read the scene again at 6, 8 and 10 columns and withdrew the two asks left here,
   since in Release the frame at 8 columns takes 7 to 8 ms waiting on the CPU, a call a cascade
   would save a quarter of a millisecond at most, and the far cascades drawn less often or smaller
   would save GPU time alone; what grows with the draws is the CPU's, 1.7 to 2.0 ms for the field's
   plan at 8 columns in Release, which is part d, so c and d carry the weight and come next in that
   order, then e to g.

   **b. Culling to the view without losing the field.** The camera's pass takes 3.2 and 5.3 ms and the
   occlusion's half-size depth 3.0 and 5.0 drawing every section, those behind the camera too, and a
   game cannot cull them itself, since the field gathers the frame's draws and a mesh left out of a
   frame leaves it, built again without it, its light gone until drawn unchanged eight frames. The
   window's passes cull to their own view inside the engine, and the field's gather keeps every draw
   the game made, so a game draws its world and the engine draws of it what each pass sees. Done at
   `d1031bc3`, each pass culling its own view and the field's gather untouched.

   **c. The occlusion pass at nothing.** `ambient_occlusion` costs 3.2 ms of the GPU and 2.3 of the
   CPU with its intensity at 0, drawing its half-size depth of every shadow caster for nothing where
   no other pass reads it. The pass is left out where its intensity is 0 and nothing reads its
   depth, and the profile says it was. Read at `0e982877` and answered otherwise: the pass draws the
   sun's contact shadows, which the model pass reads, so it stays, and what the profile called it
   was three things, the batches' gather at 1.3 to 1.5 ms of the CPU once a frame, the half-size
   depth at 1.16 ms of the GPU and the occlusion itself at 0.08, each a node with its number; done.

   **d. The field's plan for a still scene.** `cpu.scene_field` takes 2.3 ms at six columns and 3.9
   at eight with nothing changed, about two microseconds a draw a frame, since `SceneFieldPlan`
   compares every draw's instance with the frame before's. A draw that did not change costs the plan
   nothing, by a key the game's draw carries or a hash the plan keeps, measured on the still scene.
   Done at `2dd527f1`: a frame drawing the frame before's meshes in the same order is read by place,
   the plan 1,600 to 92 µs and the field's node 1.8 to 0.39 ms of the CPU; the gather's 330 µs and
   the bounce's node's 0.77 ms remain, noted.

   **e. The field's colors from the vertices.** `SceneFieldRenderer.Gather` gives the field each
   draw's material color times its texture's average and the model pass alone multiplies in the
   vertices' colors, so a section meshed as one mesh would bounce one color for every block, and the
   game draws 1,127 sections as 1,971 meshes, one a surface. The splat takes the vertices' colors as
   the model pass does, so a section is one mesh and one draw, some 43% fewer on this scene before
   any culling. Done at `b5805aa5`: the splat blends the vertices' colors at the cell's nearest
   point and the stamp takes their mean, for 0.03 ms of the GPU where the field is built every
   frame; the traced reflections still color a copy by its instance alone, noted. The game draws
   each section as one mesh with its blocks' colors in its vertices at `4d54769d`, 1,056 draws at
   eight columns where 1,836, its settled frame 8.9 ms in Debug.

   **f. A changed mesh's light.** Read in `SceneFieldPlan.cs` and §4 and not yet measured: the field
   keys a mesh by its vertex array, so a mesh uploaded again is a new one, stamped for eight frames
   as boxes in its color that give off none of its light, and a block placed beside a lamp inside a
   section's mesh would put the lamp out for those frames and the cascades built after. Measured
   first on the game's scene, a block placed beside a glowstone; then the stamp carries the mesh's
   emission, or the mesh stays in the field as it was until the one replacing it is still, whichever
   the measurement favor

   **g. The sky's reflection in a sealed room.** A chamber sealed in stone at noon reads (6.6, 13.3,
   24.4) in sRGB with the sky's environment map and (0.1, 0.1, 0.1) without it, a blue sheen on
   stone of roughness 0.9 brightest at a grazing angle: the model pass weighs the map's reflection
   by the ambient occlusion alone, and reflections rougher than 0.5 are not traced, so nothing says
   the sky is not there. The probes know, their rays that meet nothing bringing the sky and the rest
   none of it, so the gather writes each probe's share of sky, the model pass blends it at the
   surface as it blends the bounced light and weighs the map's specular and, where the bounce is
   off, its diffuse by it, and the chamber reads dark while an open field reads as it did; measured
   on the game's chamber and on Wick's doorway, and the game's dimming of the whole map by the light
   at the player's eyes goes.s.

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
    the 315 the renderer's step takes and gives back being lavapipe's own. Two things. The census
    runs at each step of the followed apps and not at their end alone, so the step line says at
    which step the thread handles are taken and that `ended` gives them back or not, naming the
    starter: SDL's threads, the audio device's under the dummy driver (Decision 25) and its timer,
    the CLI's listener, the asset workers, the physics' workers and the behaviors' compiler are the
    engine's own starters, and lavapipe's rasterizer threads the device's. And the starter named is
    read for a thread started and never waited on or detached, an `SDL_CreateThread` without its
    `SDL_WaitThread` or `SDL_DetachThread`, a handle kept after a join, and closed where the app
    ends, or said to be the driver's with the evidence. The bound stays. Settled when a Windows run
    passes whole.

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

## Replies

Item 2's part f. Measured first in a scene drawn offscreen frame by frame, as the game's edit is:
three walls in one mesh around a lamp drawn apart, which lights them by bouncing alone, the walls
replaced at a frame by a mesh of four, the fourth a block beside the lamp. The wall read 116 before
and fell to 69 six frames after, the floor 85 to 60, back by the tenth frame and on to 136 and 112
with the block's own bounce; the lamp never changed, since the game draws it apart, and the dip was
the walls leaving the field at once while their replacement stood in as a few boxes for its eight
frames. So the stamp carrying a mesh's light would not have touched it, and the measurement chose
the other: a still mesh replaced in place, by a mesh of other vertices drawn through the same matrix
where it was, stays in the field as it was until the replacement is still, the replacement not
stamped meanwhile, so one build takes the one out and puts the other in, which keeps an emitter
inside a replaced mesh lit as well. A mesh that moves keeps its vertices and is stamped as before,
and one kept for a replacement that never settles leaves after sixteen frames. The same scene
afterward held the wall at 114 to 116 and the floor rising from 85 until the replacement settled.
The scene is a test now, failing without the change with the wall's 107, 98, 91 down to 69, and two
unit tests hold the plan to keeping the replaced mesh, building the swap once, and letting go after
sixteen frames; RENDERING.md §4 says it with its numbers. Manor and Wick hold their times. The
suite 1,641 passed; on lavapipe 361 passed and 7 skipped with no validation error.
