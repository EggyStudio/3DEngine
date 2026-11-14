# Review

Reviewed up to `c6bac6dc`, 264's part 7, MoveWorld, a program that moves its world by a whole step
saying so and the engine moving its field, its probes and their light, its histories, its shadows'
texels, the reflection probes, the particles, the physics' bodies and the sounds with it, a mesh at
its matrix plus the step the same still mesh, with the ambient's test of a panel too wide to be
carried, pushed at 13:06 on 2026-11-14 once the run before it had ended, green at 14:44 with the
four Windows jobs in 82, 47, 59 and 91 minutes, macOS in 55 and the Linux legs in 88 and 65;
WorldMoveTests reads 0.000 at Low on the three devices and 0.213 at the most at High on the RTX,
RenderOriginTests passes whole, 30 of 30, and the suite passed both legs, 2,431; it keeps the form
and carries the review's files. Before it `0567d8f4`, the rest of the independent read's findings
and the last of the origin's cost, mapped memory handed out as a span that can be written and not
read, each view pushed and found once a pass with a test that counts the pushes, stereo eyes,
projections set through rlgl, the modeling window's outlines, the casters' ranking, rays against
meshes, the picking and the glow lights' boxes far from the origin, each with a test failing first,
pushed at 09:59 on 2026-11-14 once the run before it had ended, its message written again before the
push to name no item, green at 11:40 with the four Windows jobs in 49, 50, 57 and 63 minutes, macOS
in 56 and the Linux legs in 89 and 64; RenderOriginTests passes whole on the three devices, 30 of
30, and the suite passed both legs, 2,425; it keeps the form and carries the review's files. Before
it `6075d306`, item 270, global illumination's lamps written into the sixteen entries its kernels'
array holds, the lights the model pass lights, with a test of twenty lamps leaving every byte as
sixteen do, pushed at 06:23 on 2026-11-14 once the run before it had ended, green at 07:46 with the
four Windows jobs in 83, 52, 45 and 77 minutes on slower runners, macOS in 55 and the Linux legs in
51 and 66; the suite passed both legs, 2,401; it keeps the form and carries the review's files. The
pushes before these stand in REVIEW-ARCHIVE.md's head records.

The package stands at 6.2.60 from `baa21ff4`, one package with its templates, published at 18:58 on
2026-11-12 by the pack run 43243318284 and tagged v6.2.60, carrying 259's two commits beside what
6.2.56 of `8561816b` carried, 257, the ceiling's line, the glow lights' history at 1,440 rows and
the dither. Next by Decision 113 the three chosen on 2026-11-12, 264, the engine drawing against the
eye with a call for a moved world, 258's list its map, then 265, the lighting's kernels written once
in the RadianceCascades module, then 255's trial of the lighting kept as functions, with 255's other
step, the other first-frame pipelines made together on the workers and the trial of the lighting
kept as functions, its first mend, the probes' kernels made together, at `c1243335`. The game is on
6.2.41 of `894542da` since its `068fd5f`. After them 250's further cuts, the hits' glow at 1.41 ms
of the screen pass's 3.04 in the Ore example. No budget by Decision 94; the door at 8:00 the one
target by Decision 95; a fault seen in motion is read in motion by Decision 107.

REVIEW-ARCHIVE.md beside this file holds, their numbers kept, the items done, the items closed or
parked, the verdicts settled, Decisions 1 to 93, the Replies before those here and the head records,
moved there on 2026-10-23 and again on 2026-11-09 so that this file holds the current work alone and
stays under 100 KB, read at each watch tick; a batch reads this file's head, its items and the
Replies (N 7.7), and the archive when an item, a verdict or a decision named here is wanted in full.

## Now

By the direction of 2026-10-04 the work here does not stop. A batch that ends is followed by the
next item in the order under the head with no wait for a reply, and global illumination comes before
every other engine item (Decisions 74 and 75).

1. **What the next page says.** Each push's run and each pack run is read by the reviewing session
    from GitHub's API, and a failure it names is a verdict here and comes first. The runs' record
    since 2026-10-04 stands in REVIEW-ARCHIVE.md under Items closed, parked or shortened, with this
    item as it stood.

Closed without a mend, recorded or parked, each in full in REVIEW-ARCHIVE.md under Items closed,
parked or shortened, and taken up again only as a new item: 142, the merge's gain past the first
bounce, closed at its two commits with the ray-query build's setting off; 212, the vestibule's door
at 8:00, its first part landed at `1c14b0bf` and its pocket mark parked; 228 and 230, the pocket
mark at 16 by 16 texels and four rays a texel, recorded with the patch kept outside the tree; 176
and 177, finer cascades inside a building and a history for the world probes, not needed as read;
143, 196, 198, 202, 216 and 231 recorded; 171 folded into 167 and 208 closed by Decision 94; 246, a
settled view's light by how the eye came to it, withdrawn as the game's script's own poses; 248, the
bounced light kept by depth texel with the probe's point moving in its tile, tried in two passes and
reverted as the screen probes' limit at shapes about a tile wide, its patches in the dump.

250. **The illumination's cost in the voxel game, read by pass, and the largest cut that holds the
    readings.** From the game's item 166 on 2026-11-10 and Decision 94, the cost recorded and cut
    after. Ore by day at High at 1280 by 720 is the scene, 8.89 ms a frame at the start. Landed.
    `cfcfda02`, a screen probe no longer marching to a faint glow light that sends it nothing, 0.31
    ms; `b7f176d9`, a ray's hit taking each face of a light past eight half diagonals as one point,
    0.11 ms; `56d77a29`, Ore's turn and walk by fixed steps; `765ca949`, the coarser cascades
    tracing half their probes a frame once the lights and the field have kept still for 16 frames,
    0.63 ms, the frame at 7.90. `afd5640f`, pushed at 13:19 on 2026-11-11, the faint lights at a hit
    taken as one point, each face's cosine from its own middle, where under a 32nd of the largest
    before them, 0.50 ms, the frame at 7.36, with gi.toggle points and a test in a hall of 49 lamps.
    What is left by the read of 2026-11-11, the rays' march and shading in the screen probes' pass
    at 1.67 ms and the marches at the hits and at the probes at about 0.8 each. The rules the item
    made. A form that holds no sum is never taken at a hit, the reference rooms the first gate, at
    Low and at High; a change of what is traced is read in process against the whole trace at the
    same frame, a light's change first; a turn or a walk played through e3d cannot judge under 2
    percent. The trials and readings as they stood on 2026-11-11 at 13:08 stand in REVIEW-ARCHIVE.md
    under Items closed, parked or shortened. Next Verdict 115, then the read of what is left before
    another cut. The screen hits' shading read by part after the cut, the pass at 3.04 ms. The hits'
    glow lights are 1.41 ms of it still and 0.97 of the finest trace's 1.31; the field's march after
    the screen's 0.59, past the reach 0.25, the screen's march itself 0.17; the probes read without
    their marches 0.04, the hidden lamps 0.04, the lamps and the sun 0.29. So what is left of weight
    is the head's own forms at the hits; no cut is picked, 254 and 255 coming first. The game's own
    reading on 6.2.60, recorded at 03:04 on 2026-11-14 from three runs at 1280 by 720 under the
    exclusive lock, a Reply in the game's REVIEW.md. Every scene is bound by the GPU. Global
    illumination takes 3.18 ms of the built room's 5.96 at Low and 6.19 of 8.76 at High, 1.82 and
    5.37 in flight; the model pass 1.90 and 1.66 in the room and 5.05 and 4.80 in flight, the
    largest stage there at Low; the field 1.2 ms in flight and nothing standing; the shadows 0.4 to
    0.6.

255. **A program's first run stands most of a minute at its fifth frame, its pipelines compiled one
    after another.** From the game's item 171 on 2026-11-11, read at 15:48. With the validation
    layer off and from a warm cache the game makes no pipeline over 100 ms after its first frame,
    314 ms; from an empty cache folder, which empties the driver's own cache under it too, its fifth
    frame makes 22 pipelines in 49.0 s of the driver's time, one after another, terrain.slang's
    depth pipelines in 14.9 and 12.1 s and mob.slang's in 19.1 s, and at the pool four terrain.slang
    pipelines compile in 15.5 to 15.7 s each, where the engine's trial with the driver's cache warm
    read 94 ms. So the layer explains the warm seconds and not the cold ones, and a player's first
    run, and each after a driver's update, has them. One read first, the trial of terrain.slang
    without the layer from an empty cache folder, to confirm the driver's first compile is the cost.
    Then the first frame's pipelines made at once on the workers with the frame waiting for them
    together, 49 s against the longest one's 19; and a read of the fragment's 1.3 MB of SPIR-V by
    function, the engine's shared lighting inlined into a program's own shader, to say what costs
    the driver a quarter minute before any cut. After Verdict 116 and 254. The cold read at about
    17:40 confirms it. With no layer and an empty cache folder the terrain pipeline compiles in
    12,099 ms, the sixth frame making 7 pipelines in 12,358 ms, and over the same folder a second
    time, the driver's cache warm and the engine's file missing, in 93 ms. The fragment read by
    function, inlined whole into its entry, 1,212,051 words, has the game's litStepped as 98.1
    percent and in it two whole copies of the engine's litLight at 47.2 percent each, the first from
    the game's line that calls lit on its unlit branch, where lit returns unlit and its litLight can
    never run; with that line returning unlit the fragment is 702,448 bytes where 1,303,784 and the
    driver's cold compile 5.9 and 5.2 s where 12.4 and 13.6. That line is the game's, its item 172.
    Of one copy, the reflection's trace is 17.8 percent of the module, the glow lights at the pixel
    11.4, the thin wall's twice 4.6. On the engine's side, the guide saying lit and litLight each
    carry the whole lighting; the first frame's pipelines made together on the workers with a test;
    and a trial of litLight and the reflection's trace kept as functions in the SPIR-V, read by the
    module's size, the driver's cold compile, the frame and the picture.

264. **The engine draws against the eye, and can be told the world moved.** Decision 110 on
    2026-11-12. One render origin a frame, the window camera's eye rounded to 16 units, every place
    taken against it on the CPU in doubles; the plan and its parts stand under Replies, and the item
    as it stood until 00:15 on 2026-11-14, with every read of the game's on the local packages, is
    in REVIEW-ARCHIVE.md under Items closed, parked or shortened. Done and on main, parts 1 to 4,
    `3405714a`, `a0af50a3`, `80fe61d2`, the mend for a program's shader `54792d7e`, the shadows
    `ed3cd9ca`, the tests' bounds `bca57e93` and the frame before `68e7085f`. Done and pushed at
    09:59 on 2026-11-14, part 9's second commit `0567d8f4`, the rest of the independent read's
    findings, a write-only span for mapped memory and each view pushed once a pass; at 05:01 the
    cost's mend `010671f7` with the read's first finding; at 03:25 part 9's first commit `5b6696b2`,
    findings 2, 4, 7, 8 and 13. Done and pushed at 00:44 on 2026-11-14 under Verdict 121's mend
    `d102cd72`, part 5, the points from the depth, `6cfda4dd`, and part 6, the rays from the screen
    on the CPU, `3768c988`. What was decided on the way. A model shader's world stays the world's
    point, a property over the place and the origin, and lights' point-taking functions take world
    points beside their Placed twins; the instance's rows are places and a shader that reads a
    translation as a world point adds the push's origin, which the game's bits.slang does at its
    pin; a camera keeps its fields, no eye and way to look, a program far out keeping its numbers
    small by moving its world, which the guide says; a test that compares two pictures holds a count
    of pixels past a small level and a loose largest, read on the RTX, Mesa 26.2.4 and the runner's
    Mesa 25.2.8. Done and pushed at 13:06 on 2026-11-14, part 7, MoveWorld, `c6bac6dc`, read by the
    game on 6.2.60-local.264.10 at 13:43 with its origin's step written against the call, the step's
    frame 35 to 44 ms at Low, a flight's own level, where 228 to 242, and 61 to 74 at High where the
    cap of 250, the light unchanged after it, the device's rays staying on through three steps,
    about 25 ms of the field's and the illumination's CPU left in the step's frame at High. Traced
    by the engine's coder and reported at 18:39 on the game's 264.10 build at High, two flights
    across the step at 8,192 and two across none, the step frame's illumination call takes 13.0 and
    8.3 ms, its ray recording 8.8 and 5.9 of that, the device rays' scene making its buffers again
    larger 6.4 and 3.6, and the glow lights' joining grid 3.1 and 1.2. The same buffer making recurs
    through the flights, 29 and 24 calls against 6 and 9 in the flights across none, so that part is
    item 272's growth of the ray scene with the land met, the step only meeting it, and the field's
    10.6 to 15.8 ms is part 8's Move, 4.0 ms for 3,000 still meshes and 12.8 for 6,000 in the
    coder's probe. To come, part 8, and what a draw of the game's sections pays since the origin,
    which the game's sampled profile put under ModelRenderer.Draw as binding, 0.25 ms on its 659
    draws, the sections being mesh entities each of its own mesh with a shader that keeps positions;
    two probes of the engine's read level, so it is found on a probe built as that room is or in the
    game itself, and mended in a commit of its own. Part 7, MoveWorld, against the game's figures on
    6.2.60 recorded at 03:03 on 2026-11-14, the frame of a step of the game's origin at 228 to 250
    ms where a flight crossing none peaks near 40, every section's mesh uploaded again, the field
    holding again after 14 to 26 frames, and its contract, the call made with the step before
    anything moves, every mesh at its matrix plus the step the same still mesh, so the step's frame
    costs what any other does and the field and the light change by nothing. Part 8, the ambient
    occlusion by place, with the field's build in doubles, the splat's rows against the cascade's
    corner and the plan's snap, the grids' phase, the ball put back in the fog room, a tied cell of
    the field's color pass broken by a rule with a test that two runs read alike, and what the
    independent read names of the field, its way ahead, moving meshes' stamps and the far cascade's
    size. Part 9, the rest of that read,
    ~/.cache/claude-reviewer/audits-2026-11-13/engine-origin-gaps.md, 17 findings, four checked by
    the review, the field's way ahead taken through a world-size inverse, a frame with no window
    camera keeping the origin at the world's, the penumbra's grain hashing a world point, an
    immediate shape's corners built in world floats, with the mirrored view, the reflection probes'
    gather and the others it names; first a Reply saying of each whether it holds, each mend with a
    test that fails first, one far scene at 1,140,736 plus 0.3 with a parented pair, a mirror plane,
    a reflection probe, shadow softness, the field ahead and a moving lamp, and the standing far
    tests moved off the eighths. The game reads again after part 8, the indirect light with its
    origin off against on; on 264.6 with the camera's target on the grid it reads 0.12, 0.24, 0.35
    and 2.71 percent of pixels at High at 4,432 to 1,140,000 and 6.24 at Low at the last, one wall
    under a roof brighter with the origin off, the figure part 8 has to beat, and the game saw the
    mirror slide by 2.5 pixels and the penumbra's grain stand in stripes there, part 9's, and
    rehearsed its pin on 6.2.60-local.264.6 at 00:33 on 2026-11-14, the play, the built room at
    three qualities and two sizes, the picking and the lens passes, with the game's origin on
    nothing past the sun's shadow edges, 0.28 percent of pixels at the most, and with it off at
    1,140,000 the mottle, the streaked fog and the banded blur of 6.2.60 gone. What 264 costs a
    frame, read by the game and recorded at 01:57 on 2026-11-14, on 6.2.60-local.264.6 against
    6.2.60, three runs each under the exclusive lock. The immediate list's upload goes from 0.02 ms
    to 2.8 to 3.5 ms a frame, the game's sections reaching the engine through it, with the model
    batches +0.5 to +0.9 ms and the scene's CPU +0.3; the built room at Low gains 0.72 ms on its
    median and the flights about 8 ms on their 99th part, High in the room unchanged. Mended at
    `010671f7`, no frame reading mapped memory back, and read again by the game on
    6.2.60-local.264.8 at 06:14, the immediate upload and the model batches at 6.2.60's figures and
    the built room's median at Low 5.99 to 6.01 against 6.00 to 6.02. Read once more on
    6.2.60-local.264.9 at 11:08, the frames level with 6.2.60 or under it and the window's depth and
    the shadows within 0.02 ms; the scene's CPU stage stays +0.26 to +0.31 ms in the game where the
    engine's probe reads level, the GPU taking it up, and the game's coder sampled the process on
    both builds, all the growth in the driver's own time under the set binds and the indexed draws
    with their counts equal. The engine's coder bisected it to `80fe61d2`, part 3, the commit before
    it reading 0.43 ms and it 0.66 to 0.68. Reported at 18:39 on 2026-11-14, 264.9 with
    RenderOrigin.Override set to the world's origin stays at 0.665 and 0.637 against 0.691 and 0.639
    plain, so the cost is code part 3 added that runs whatever the origin's value; builds of
    `a0af50a3`, `80fe61d2` and `0567d8f4` with counters in the device's record wrappers read the set
    binds, the layouts, the pushes, the pipelines and the draws over 600 frames of the game, alike
    counts naming the driver's state and unlike ones the commit's calls. Read at 19:03, the counts
    are alike, part 3 making about 3 percent fewer calls of the same kinds in the same proportions,
    512.1 set binds a frame against 528.6, 1,036.2 indexed draws against 1,069.6 and 380.8 pipelines
    against 393.1, `0567d8f4` the same with its pushes at 35.9 where 380.8, so each call costs the
    driver more. Reported at 19:22, the draw list's lock is ruled out, the game's draws recorded on
    the scene pass's own thread with that thread's lock time level, 4,206 ms of 20 s on 6.2.60 and
    4,096 on 264.9, NoteEye 8.7 of it and the view memo 15, and `0567d8f4` with no eye noted reads
    0.816 and 0.740 ms against 0.669 and 0.779 as it stands and 0.503 and 0.461 on `a0af50a3`. Next
    the render thread's native stacks sampled 400 times on `a0af50a3` and on `80fe61d2`, where in
    the driver the added time falls and whether it waits there. The review's panel of six readers,
    at ~/.cache/claude-reviewer/audits-2026-11-14/driver-cost-panel.md and sent at 19:28, leaves the
    CPU's own state, `80fe61d2` itself with the override handing the driver `a0af50a3`'s calls and
    arguments but for the low bits of the pushed view; so that build read with the override, every
    timing pinned to the P-cores' threads 0 to 15 of the i9-14900HX, both builds with
    DOTNET_EnableAVX=0 for the wide registers left dirty at the driver's entry, and the draw loop's
    compiled code compared, five runs each by turns on builds without counters. Each part is
    reported with RenderOriginTests on the three devices and the Windows part its tests join.

265. **The lighting's kernels written once.** Decision 112 on 2026-11-12. The radiance cascades'
    kernels, the trace, the merge, the ambient, the screen probes, their filter and the glow lights,
    go into the RadianceCascades module as Slang written once, each engine giving its bindings, its
    view's block and its field's lookups through a small interface the module names. First a read
    with the sibling's coder, as a Reply in both files, of where the two copies differ today and
    why, what is one engine's own and stays, and what the interface is. Then the move, a kernel at a
    time, each engine's tests and reference rooms holding before and after, the two engines'
    pictures of one room compared. After 264. The read takes the bridge's item 137 with it, recorded
    at 23:15 on 2026-11-12, a closed room's near wall stepped against the grids reading 0.57 to 1.02
    of its reference there by where the third cascade's probes fall behind the eye, and says whether
    a hit across a cascade's band here takes the next cascade's probes where none of them sees the
    point. The mend, the bridge's item 141, is written once in the module after the move, the room's
    test held in both engines. The two copies are read side by side in the RadianceCascades module's
    .github/KERNELS.md, pushed at 00:10 on 2026-11-14, the starting point here, read and corrected
    in this item's Reply. It shows the band's read alike in both engines, probelight's
    bouncedSeenAtBoth reading 0 past a cascade's band where none of the next cascade's probes sees
    the point, and leaves one lead open, a sky texel of four rays marked early in the trace by
    whether through carries a nothing share. The review's design read,
    ~/.cache/claude-reviewer/audits-2026-11-14/kernels-design-and-research.md, gives each of its
    twelve questions a recommendation with the code's lines, its research with sources; the read
    with the sibling's coder takes it in, saying of each part what it takes.

267. **A pixel on the picture's edge takes its normal from the neighbor inside.** From the sibling's
    `18364420` on 2026-11-12. There a neighbor clamped onto the pixel itself gave a step of nothing,
    taken as the nearer, and the surface's facing and every weight resting on it turned with a
    float's rounding along the picture's edge rows. Here depthNormal in gi_screen.slang and
    gi_screen_glow.slang takes the nearer of the texels on either side, sampled with the depth
    clamped at the edge. One read, the edge rows and columns of a view of the bounce rooms with the
    camera moved by a few float steps, and the mend where they move, the neighbor past the edge
    taken as missing, with the sibling's test. Small, with Verdict 119's mend or after it.

268. **The documents and scripts name no item, verdict or decision of the review by number.** From a
    sweep of 2026-11-13 by N 4.11 as widened that day, its list at
    ~/.cache/claude-reviewer/audits-2026-11-13/docs-item-references.md, lines 30 to 90 for this
    repository. RENDERING.md carries 33 such numbers, ten of them bare, a reading told as what an
    item left or what stood before an item; three scripts' words and one data file's comment cite a
    verdict; a line of RENDERING.md names another session's tests on the GPU and a comment of
    .gitignore says who reads and prunes the dump; a test's string holds three middle dots where its
    twin in the sibling writes escapes. Each is written to say what stands, a reading keeping its
    date and figures, a decision told by what was decided, no line but a document's or a comment's
    words changed and the test's string kept the same characters. The words "the review's records"
    for the files under .github stay. One commit, after 264's last part and before 265.

269. **The depth of field draws a small bright emitter's blur as a sparse grid of texels.** From the
    game's rehearsal on 2026-11-14, alike on 6.2.60 and on the local package of `3768c988`, so no
    part of 264. Sea lanterns out of focus under SetDepthOfField 12, 3, 0.6 show as grids of white
    texels with dark between, where a blurred light is a soft disk; the sheet is
    00-33-item-264-6-rehearsal.png in the game's dump of that day, the third column. One read, which
    of the gather's taps or its half-size image leaves the gaps, then the mend with a test of a
    small emitter out of focus reading as a filled disk. After 264 and 268.

271. **The cascades' rays traced by the device, as a backend beside the field.** Decision 115 on
    2026-11-14. What stands, at High where the device has ray queries a probe's ray can be traced
    through the meshes in place of the field behind a switch, a reflection the field misses is
    traced there, and the reference traces through the same structures. To do, after 265, in the
    shared kernels. One interface a ray is traced through, a way and a reach in and the meeting's
    distance, surface, color and emission out, with two forms, the field's march and the device's
    ray query, the cascades, the merge and the gather knowing neither. A setting chooses the form at
    every quality, the device's ability its limit, the field the form on a device that draws on its
    CPU. The field stays where the contact shadow, the light through a surface and the rough
    reflection read it, each said whether it could take the device's rays as well. First a read as a
    Reply, each form's cost by cascade on Sponza, the bounce rooms and a scene of many moving
    meshes, the structures' upkeep counted, and each against the traced reference, so the default is
    chosen on figures; the voxel game's reading of the switch as it stands today is the first of
    them. Its tests hold the two forms within a bound of each other in the rooms and run the
    device's form where the device has it, the field's on the runners. The first reading, the voxel
    game's at High on an RTX 4070 Laptop of 8 GB, recorded at 13:08 on 2026-11-14, three sessions
    under the exclusive lock, the sheet 12-37-gi-field-against-device-rays.png in the game's dump.
    The switch as it stands traces every world probe's ray through the meshes after marching it
    through the field for its reach, so it reads the field against the field and the device's rays,
    not either alone, and does not reach the screen's probes. gpu.global_illumination reads 6.02 to
    6.08 ms against 6.14 to 6.19 standing in the built room, 0.12 less, and 5.69 to 5.75 against
    5.35 to 5.39 in a flight, 0.34 more; nothing else moves past the spread. The pictures part by
    0.4 to 1.6 percent of the reference's mean light between the two ways, the meshes nearer the
    reference indoors and the field nearer under leaves, the game's blocks being whole so no thin
    wall stands in a view. Each way stands far further from the traced reference than from the
    other, 1.03 of its light under a canopy, 1.29 in the built room lit by one glowstone and 2.2 in
    it unlit, so this scene's distance from the reference lies in the cascades and not in where
    their rays are traced. The read here takes a scene with thin walls and cloth, Sponza, where the
    two ways should part.

272. **The ray scene keeps the structures of meshes it no longer places, and says it traces when it
    does not.** From the voxel game's reading of the field against the device's rays on 2026-11-14,
    on the local package of `f4b1bdd9`, at High on a device of 8 GB. The ray scene holds six or
    seven times as many meshes as it places copies, 5,714 for 858 after one step of the game's
    origin, and grows from 260 MB standing in the built room to 1,185 to 1,361 MB; at the second
    step the device has no memory for it, the log says the device's rays are left off and the field
    traces what they would, and they stay off for the session. From then `gi.toggle meshes on` still
    answers that the probes' rays are traced through the meshes, and `gi.state` names the switch on
    no line. Each step builds the ray scene again for about 210 ms of the CPU, whichever way the
    probes trace, High holding it for the reflections the field misses. To do. A mesh no copy has
    placed for a count of frames gives its structure back, with a test that a world whose meshes
    come and go holds the ray scene's memory level. The switch's answer and `gi.state` say what is
    traced, the device's rays left off for memory among it. With MoveWorld a step uploads no mesh
    again, so its build should fall away, read by the game on its package. The ray scene's buffers
    made again larger as land is met, 29 and 24 makings over 4 ms in a flight across the step at
    High and 6 and 9 across none, by the engine's trace reported at 18:37, grow in steps that cut
    the count, read again after the mend. After part 8, the draw list's binds, 273 and 274, before
    265.

273. **A glow light taken at a probe's hit reads several times the traced reference's first
    bounce.** From the voxel game's read of its built room on 2026-11-14, at High on the local
    package of `f4b1bdd9`, the sheet 14-02-built-room-light-by-cascade.png in the game's dump. The
    room, lit by a sea lantern set into its wall and a little sky down a passage, reads 2.2 times
    the reference's light, and 1.29 times with a glowstone set into its ceiling. The excess is the
    probes' indirect light, 1.4 to 2.3 times the reference's rest at four points, where the glow
    lights' direct term at a surface reads 1.1 to 1.3. Cascade 0 carries it, cascade 0 alone reading
    2.09 where each coarser one alone reads 0.95 to 1.21. A probe of cascade 0 at the room's middle
    meets a surface where the reference's path does in 58 of 64 ways and reads 257 percent over at
    one bounce, the glow light taken at its rays' hits; other probes read 188 to 605 percent over,
    2.9 to 7 times, and the bounces past the first add to it, the room falling to 1.49 with them
    off. With the glow lights off both the probes' first bounce and the reference's are nothing. It
    does not follow the probes' places, the room built again in rock a block and four blocks off
    reading 2.24 and 2.17. Not the screen's probes, the filter, the history, the specular or the
    merge, each left out changing it by under a tenth. To do, first a read as a Reply, what a glow
    light sends to a hit, its form, its faces and its power against the emitter's as the reference
    traces it, in a room with an emitter set flush into a wall, one standing free and one hung in
    the air, the game's coder testing the same in its rooms; whether an emitter with faces buried in
    what stands around it sends as the whole of it would. Then the mend with those three rooms held
    to the reference at one bounce within a bound said there, and the direct term's 1.1 to 1.3 read
    beside it. After part 8, ahead of 272 and 265, the kernels taking the mended form with them. The
    cause confirmed by the game's test at 14:29 on 2026-11-14, the room built in rock with the
    lantern at four places, each against its own reference at one bounce. A probe's first bounce
    reads 252 percent over with one face of the lantern showing, 84 with two, 1 under with five and
    6 over with six, the whole frame 116, 26, 1 and 2 percent over. Where every face of an emitter
    shows the cascades read the reference within a few percent, and the whole excess is the light of
    faces that lie against what stands around them. The direct term at a surface over-reads less,
    1.14 to 1.35 with one face. The game hands each lamp as a whole cube of six faces and tries
    handing the faces that show; the mend here takes no light from a face that cannot send it,
    whatever a program hands, and the two must not take the light off twice. The coder's read from
    the code and the mend's shape, accepted the same afternoon, and the game's trial recorded at
    15:10 on 2026-11-14. A hit makes one march toward where its way enters the emitter's box and
    then sums every face it lies before, the buried sides among them; a surface halves each face and
    marches to each half but stops a cell short. The mend, a hit taking each face under its own
    march to that face's middle and the surface's halves stopping at the face, up to three marches
    where one for the brightest light at a hit, its cost read. The bar for it, from the game handing
    a lamp's showing faces alone on 6.2.60-local.264.10, the sheet 15-09-lamps-by-their-faces.png,
    the rock room 7 percent over its reference where 116, the built room 1.19 where 2.21 and 1.04
    where 1.29 lit; a lamp showing two faces reads 14 percent dark there, touching boxes of one
    light being joined at a share of their surface, so the mend's rooms say what a program's mesh of
    one, two and three faces of a box reads. The game keeps whole cubes and leaves undrawn only a
    lamp that shows no face. The game's last read of its room, recorded at 15:59 on 2026-11-14, the
    sheet 15-59-built-room-light-not-the-lantern.png. The room walled up reads exactly nothing at
    every pixel at High and at Low, so nothing comes through the rock. With the lantern out and the
    passage open the room's light is the game's own floor of the sky's light by sky level, every
    term of the model pass nothing there. So with the lamps mended the built room should read about
    1.2 of its reference with the game's floor and 1.08 without, the bar its package is read
    against. The review's design read of the twelve kernel questions, at
    ~/.cache/claude-reviewer/audits-2026-11-14/kernels-design-and-research.md and done at 20:09 on
    2026-11-14, gives this mend's form in its part 11, the field read at cell middles, faces decided
    again only on the field's change near the light, a cleared face's light absorbed and not moved;
    its plan's Reply says what it takes and what it leaves, the read at `c6bac6dc` and three of its
    claims checked by the review. Taken by the coder at 20:11 in place of the marches a hit makes,
    the light kept at 80 bytes, one pass on the GPU reading each light's faces from the field at
    cell middles and taking the open ones into the mask every reader decodes, a light with none open
    given no reach, faces decided again only on a light's events and the stored mask applied each
    frame, joined groups keeping their members' faces on the union's at a share of surface, and the
    tests building the closed layout the game's mesher makes and the culled one, the half-sunk
    lamp's reading recorded as it falls.

274. **The scene pass's thread waits on the shader store's lock, about 0.5 ms a frame.** From the
    engine's traces of the voxel game on 2026-11-14, read for item 264's binds with tiered
    compilation off. ShaderStore.Get, a program's shader looked up by its id under the store's lock,
    is the largest share of that thread's lock time on 6.2.60 and on 264.9 alike, 1.79 s of every 20
    s, so it is no part of 264's cost and stands on its own. First a read as a Reply, how many times
    a frame the scene pass asks the store and from which calls, and which other thread holds the
    lock while it waits and for what. Then the mend the read names, the lookup taken once a batch
    where it is taken a draw, or a read that takes no lock against a table the writers replace
    whole, with a test that a shader registered while another thread draws is found and none is
    lost, and the game's cpu.hdr_scene read before and after. After 273, before 272.

275. **The deep audit's findings.** From the review's read-only audit of 2026-11-14 at `c6bac6dc`,
    ~/.cache/claude-reviewer/audits-2026-11-14/3DEngine-deep-audit.md, sixteen areas each read by a
    finder and every finding put to three verifiers, one reading the code, one its reach and one the
    records. 96 held, 3 of high severity, 52 of medium and 41 of low, five known already and four
    dropped, each with its file and line, its scenario and a direction for the mend. The review
    checked the three high ones against the code. The scripts' and the workflows' checks for the
    validation layer look for the words Validation Error, which the pinned layer's messages do not
    carry, the engine logging an error as its category and the message alone, so no game, example,
    storm or native run fails on the layer's error. The mesh entities are gathered in Render before
    the transforms are carried down the tree in the same stage, so a parented mesh draws with the
    world matrix of the frame before. A body the flat API makes has no entity and is never blended
    between steps, so GetPhysicsBodyTransform moves in steps of the fixed rate. First those three,
    each with a test that fails first, the layer's first since it hides every other run's errors,
    its test feeding a line as the pinned layer writes it. Then a Reply saying of each other finding
    whether it holds at the head, and the mends a few to a commit in the report's order, those of
    the lighting and the field taken with the items they touch, glowLightShown's face mask with 273;
    a finding that does not hold says why. The three high ones after part 8's commit, beside the
    binds' session; the medium ones after 272 and ahead of 265, the low ones after 265.

## Verdicts

No verdict is open. Verdicts 56 to 121 are settled, said under each in REVIEW-ARCHIVE.md.

## Decisions

Decisions 1 Commits stay local; 2 The package is `3DEngine` on nuget.org, the owner's own, numbered
from 5.0; 3 AGENTS.md's bullet and table row on SHARED.md change by the project's direction alone; 4
TODO.md's two Entities entries are closed as decided; 5 A commit that takes something out of the
public surface is numbered by the project's direction; 6 AGENTS.md's bullet and table row on NORM.md
change by the project's direction alone, with the exception N 7.4 makes; 7 A run that fails says
what failed in a page, and the reviewing session is given no log; 8 The page's repeated lines are
warnings and errors; 9 `GetFontDefault` stays ImGui's ProggyClean; 10 A render texture takes the
window's samples; 11 The package ships no ReadyToRun images, the author's own publish being the way;
12 Per-object motion blur and ImGui viewports are wanted, after the standing items; 13 Audio falls
back to a device of silence where there is none; 14 A document a game's author reads names no one
who decided; 15 Text is shaped whole; 16 Global illumination comes in four phases, a scene distance
field first; 17 Every tonemapper BevyCSharp offers is in 3DEngine; 18 Subsurface scattering is the
next large item, the first of Decision 16's spectral experiments; 19 5.2 is packed when every job is
green, before subsurface scattering lands; 20 Three packages are bumped after 5.2; 21 The animated
model's meshes in the world follow subsurface scattering; 22 The bounce's quality comes before
subsurface scattering goes on; 23 No document or comment a reader sees names a person or a session;
24 Content streamed on the go stays in the ledger, and the browser waits for a game that asks; 25 A
run that shows no window makes no sound; 26 A game's session writes what the engine lacks in
`.github/ASKS.md`; 27 A look on 2026-10-10 opens item 2 again, four things seen; 28 Seen on
2026-10-11, the sky's light leaking into the game's cave by the heading is a glitch to be found and
fixed, and the overlay gains a post-processing window; 29 The README's gallery is one scrolling row
of pictures a folder, sorted by folder, and the games' text under the table goes; 30 The e3d skill
leaves `.claude` for `.github/E3D.md`, and the gallery's pictures are one size; 31 `games/Pusher`
goes; 32 The GPU named nowhere a user reads, `Engine3D` renamed `E3D`, and the guide table opening
with the cheat sheet; 33 The radiance cascades are said in the README and in a guide page of their
own; 34 The examples' counts stand under the Examples heading and on each folder's heading; 35 A
push plays one game, and the pack run plays them all; 36 A vertex stream of the game's own, in both
engines; 37 A position is every mesh's, and the packed data a stream beside it; 38 The macOS
captures run in the pack run alone; 39 No development on either engine until every job is green on
every push and pack; 40 Two workflows an engine, build and pack, the same shape in both; 41 The
voxel game moves to its own repository, Voxel-Sandbox, and a tiny sample stays under `games/`; 42
Decision 39's gate is the new workflows; 43 Prose is technical and generic; 44 Development resumes
on all three fronts, the radiance cascades first; 45 The README's centered parts are markdown inside
a centering block, since nuget.org draws a README without its HTML; 46 The code fixes project stays;
47 A package's release notes are the commits since the package last published on nuget.org; 48 The
pack workflow publishes through nuget.org's trusted publishing; 49 The reviewer starts the pack runs
when a package is due; 50 The review's files are committed by the batch that comes next, and by the
reviewer at most once a day; 51 Modeling in both engines, from the kernel to the modifiers, grid
prototyping first; 52 The Actions bill is not weighed; 53 Animated captures as tooling for
documentation and devlogs; 54 The voxel game leaves the screen probes out, and the cascades' faults
that are seen are read and mended; 55 A fill cascade to the far plane for the shadows and the field,
not counted; 56 A screenshot on F2 in every program; 57 What the engine learns from the voxel game;
58 F1 hides the interface and F3 shows the debug overlay, in both engines; 59 A denoiser for the
ray-traced tier of global illumination, beside the cascades, in both engines; 60 What was seen in
the voxel game on 2026-10-23 that is the engine's; 61 The cloud sessions take GPU-free items on
branches of their own; 62 Branches are named for their work, never claude/ or a random string, and
work stays on main where one session writes; 63 Every commit carries the repository's author
identity, and the cloud sessions' branches were deleted for not carrying it; 64 Until a full
release, both engines are used by nobody; 65 The two cheat sheets have one form; 66 A devlog under
`.github/devlog`, with AGENTS.md's row for it; 67 Holographic Radiance Cascades read and set aside
until a three-dimensional form exists; 68 The loop and the ECS are both the engine's way, each where
it fits; 69 The window runs under X11 on Linux for the present; 70 A session saves the pictures of
its work in the repository, N 4.8; 71 The devlog written as the work goes; 72 The screen probes have
no public switch; 73 The devlog carries notes beside the pictures; 74 The engine's work focuses
fully on global illumination by radiance cascades and the framework that supports it; 75 Global
illumination comes before every other engine item; 76 A reading's picture explains itself, and a
plain capture or pair stays where it shows the thing; 77 Every picture a session makes goes to the
repository's dump folder; 78 Six ideas for later, thought about and not scheduled; 79 Decision 75
means no drift, not that foundation work waits; 80 The version's major and minor move by the
reviewer's judgment; 81 The Sponza scene comes to the engine as BevyCSharp has it; 82 What a
scriptable render pipeline has, written down for later; 83 The shadows' 34 ms in Sponza is a fault
to read before the look; 84 The cascades' look is to match the reference Unity scene; 85 The sky's
model is a module, the drawing stays in each engine; 86 Vulkan 1.4; 87 The look is read by its most
visible error, not by matching the sections; 88 The example's fog comes off until it is dark where
it should be; 89 Stability in flight before the fog; 90 No one is named in a picture; 91 Anisotropic
filtering at 16 everywhere; 92 The play of scenes_sponza on 2026-11-04; 93 Each README
cross-references the other engine; 94 No millisecond budget; 95 A stop rule, one target and one
lever; 96 The flicker recordings; 97 Sponza's lamps; 98 The night sky; 99 The difference beside a
pair; 100 Version tags; 101 No middle dot; 102 One repository, many projects; 103 Comments on the
code's own terms; 104 The door lands at the price of a synthetic bound; 105 No readings or costs in
comments; 106 Sponza's lamps light their surroundings, warm and varied, and the bloom follows the
lit surround; 107 A fault seen in motion is read in motion; 108 Post-processing settings in plain
numbers, each in full under Decisions in REVIEW-ARCHIVE.md.

109. **One package on nuget.org, and Templates.** On 2026-11-11 at about 16:30, after one package
    was chosen for the sibling, the several packages were not favored in 3DEngine either, acceptable
    if they are the best choice there, the preference being one package alone, or two with the
    second holding heavy binaries, and the question put was whether several modules must mean
    several packages too; at about 16:40, among the review's options, one package plus Templates was
    chosen. The review's ground for it. What the split gives, the areas, their layers and the norm's
    test, comes of the projects and not of the packages; a game names 3DEngine alone and no one
    takes a part; fifteen ids are fifteen uploads and listings a publish, five of them 38 minutes
    behind on 2026-11-10 and a half-listed version failing a restore before; and the whole package
    is under 5 MB, so nothing asks a package of binaries. So the areas stay projects, one 3DEngine
    package carries every area's assembly and the natives, 3DEngine.Templates stays apart as dotnet
    new asks, and the thirteen area ids get no further version, the owner unlisting them. Decision
    85's packages at one version, as 235 and the split made them, give way to this. Item 256.

110. **Places far from the origin, the engine draws against the eye and can be told the world
    moved.** Chosen on 2026-11-12 at about 05:40, on the review's question after item 257, among
    positions in doubles throughout, a call for the origin alone, and this, the engine keeping its
    float API, drawing everything against the eye inside, and giving a game a call that moves the
    world's origin by a whole offset with nothing of the light built again. The review's ground.
    What 257 found was no one pass's fault, every vertex, shadow, lookup of a frame before and point
    from the depth standing in world floats, and a rule at the core costs less than a mend a pass,
    with no break of the API where doubles would change every call that takes a place. Item 264 does
    it, and 258's list is its map.

111. **The lighting kept as functions, measured first.** Chosen on 2026-11-12, among this, a
    deferred lighting pass, and keeping the lighting inlined behind caches and workers. Every model
    shader carries the whole lighting today, 1.3 MB of fragment and 12 s of a first compile in the
    voxel game's terrain before its second copy went, and each feature of the light adds to every
    shader. The forward pass stays, the lighting compiled as functions a shader calls and not as a
    copy in each, 255's trial, measured by the compile's time and the frame's; a deferred pass is
    taken up only if that is not enough.

112. **The lighting's kernels written once.** Chosen on 2026-11-12, among this, two copies with a
    check of their parity, and one engine's lighting alone. The kernels of the radiance cascades
    stand twice, here and in the sibling, ported by hand, and two faults of one night were found and
    mended in both. They go into the RadianceCascades module, written once in Slang, each engine
    giving its own bindings, so a mend lands in both. Item 265 here and the sibling's item 138.

113. **The order of the three.** Decided on 2026-11-12, they are taken after the dither's grain and
    the blobs in the distance, the two things in view, and the publish, and not beside them nor
    parked. Then 264, the places, then 265, the kernels written once, then 255's trial for 111.

114. **An editor as a module both engines use, later.** The direction of 2026-11-12 is that the
    editor is no focus yet and will be. The aim is a full framework in which ImGui frames for editor
    tools are built easily, with helpers that show data such as the ECS and assets in diagnostics
    windows and a way for a shell to have a surface for an editor layout, a module used by both
    engines, made in ImGui as the sibling's is, giving an engine the functions to build an editor on
    or a way to show its data in one. It is thought through further before it is taken up, so no
    item is made and nothing is built. It refines the split's "no editor", none inside the engine
    and one beside both. Until then the two engines stay on one ImGui binding at one version, and
    the engine's ImGui area and its console stay as they are. Added the same morning, of how it sits
    and of what there is. The editor module provides functions and drawing calls that the engine
    uses in its own ImGui scope, in the same process and as ImGui calls alone. So the engine hosts,
    the module owning no window, no ImGui context and no process. The sibling's editor is not taken
    as the module's design; it is progress to learn from, and the module's own design is set out
    when the module is taken up.

115. **Hardware ray tracing is a backend of the radiance cascades, chosen by what the device can
    do.** Decided on 2026-11-14. The engine stands on SDL3 and Vulkan for modern hardware and
    techniques, so a device that traces rays is used for it. The cascades' rays are traced through
    the meshes' acceleration structures where the device has ray queries and that way is chosen, the
    better picture, and through the scene's distance field elsewhere, on a device that draws on its
    CPU and on the runners among them. The field stays beside the hardware way wherever another part
    of the lighting needs it. Which way a program takes is its choice by a setting, with the
    device's ability as the limit. What each way costs is measured before the default is set. It is
    written once in the shared kernels, after they have moved. Item 271, BevyCSharp Decision 92.

## Replies

Item 250, read on 2026-11-10. Ore by day at 1280 by 720, offscreen and unpaced, on the RTX 4070
alone under the lock held exclusively, each reading the mean of 600 frames after 200 to settle. At
High the frame takes 8.91 ms: gi_screen 3.50, gi_trace0 1.58, hdr_scene 1.36, trace1 0.94, trace2
0.38, trace3 0.23, gather 0.23, merge 0.21, glow 0.19, filter 0.07; Medium 7.41, gi_screen 3.38;
Low 5.41, gi_screen 1.30, its glow lights alone; Off 0.39. Each part left out at High in turn: the
glow lights off, the frame takes 3.33 ms, gi_screen 0.83 and trace0 0.37, so the 69 small emitters
carried as lights, the 64 nearest lit, are most of the screen probes' cost and much of the first
cascade's; the screen probes off, 21.77 ms, hdr_scene 17.92, the model pass's own way without them dearer,
why not yet read; the filter off, 8.96 ms, no saving. The cuts go one at a time from
the screen probes' glow lights, each held to its reading.

Item 250, the first cut. A faint glow light that sends nothing to a screen probe is no longer
marched to, since what it sends is the march's weight, so the picture is the same. Read on the
copy with the hits' rough form, at High the frame went from 8.47 to 8.17 ms and gi_screen from
3.33 to 3.01, the shot 0.006 percent past 6 of 255 against it. The reading against the head's own
form follows once the lock is free. GlowLightsTests and the suite's two legs pass on it.

Item 250, the first cut read on the head. The frame went from 8.88 and 8.89 ms to 8.56, 8.59, 8.58
and 8.59 ms, gi_screen from 3.48 to between 3.17 and 3.20. The second cut, a probe's ray's hit
taking each face of a glow light past eight of its box's half diagonals as one point at its
middle: the frame 8.47 and 8.47 ms against those four, gi_screen 3.14, the shot and the still
0.006 percent past 6 of 255 against the base, as two of the base's stills stand against each
other, GlowLightsTests' rooms passing. A face about square taken as one point from four half
diagonals read 8.49 ms, no more, and is left out.

Item 250, what each pass does a frame, Ore by day at High at 1280 by 720 after these cuts, from
gi.state and the profile. Ore sets its field as 4 cascades with no far cascade, so no cascade
traces in parts.

| Pass | ms | Holds | Traces each frame | Spread over frames |
|---|---|---|---|---|
| gi_screen | 3.16 | 160 by 90 probes, one each 8 pixels | all 14,400, 16 rays each, 230,400 rays, and each probe's march to each of the 64 lit glow lights, the bright by face halves, the faint to their middle | none; the filter keeps each probe's light over frames |
| gi_trace0 | 1.59 | 512 probes, 8 by 8 texels of 4 rays, 256 rays each | all 512, 131,072 rays | none; 8 probes' rays carried moving |
| gi_trace1 | 0.91 | as trace0 | all 512, 131,072 rays | none; 38 carried moving |
| gi_trace2 | 0.36 | 512 probes, 16 by 16 texels of one ray | all 512, 131,072 rays | none; 5 carried moving |
| gi_trace3 | 0.23 | as trace2 | all 512, 131,072 rays | none; a far cascade traces a quarter a frame, which Ore has not |
| hdr_scene | 1.47 | the window's pixels | each pixel, its reflections below roughness 0.5 in 24 steps, then the field | none |

Every cascade traces the same count of rays each frame, yet their cost falls fourfold from the
first to the fourth, so a ray's cost is in what it meets and shades near the eye more than in its
count. The coarser cascades, 1 to 3 at 1.5 ms together, trace whole every frame though their
light changes slowest; the far cascade's quarters a frame are the way already in the engine to
spread them. That cut changes what moves, so it waits on ore.turn and is read by the turn and the
rooms.

Item 250, the reads' commands. Ore takes ore.turn, a turn by degrees a frame for a count of
frames, and ore.walk, a step the way the player faces a frame for a count of frames, each column's
top stood on rather than walked, and ore.status gives the eye and the point it looks at in the
invariant culture. Two runs at a fixed frame time read alike to the thousandth: a degree a frame
for 90 frames turned the view from facing -z to +x, and 0.1 a frame for 120 frames went from 24.50
to 36.73, the 0.23 past twelve the step down the columns from 9 to 5, alike in each run.

Item 250, the coarser cascades in halves. The cascades past the first, the far cascade aside, now
trace half their probes a frame while they lie where they lay the frame before and no change of the
lights or the field lies within the 16 frames a change takes to settle, those whose coordinates add
up to the frame's parity, the other half keeping its rays' light of the frame before, and a probe
carried with its rays moving traces every frame for its cycle. Ore at High at 1280 by 720, under the
lock held alone, 600 frames each: 8.53 and 8.54 ms traced whole, 7.90 and 7.91 in halves, 0.63 ms
saved; gi.state sampled 100 times read the coarser cascades in halves in all 100 standing and in 87
of 100 on the autopilot's walk, 12 whole, after sections were built. On a path through the rooms
frame by frame against the whole trace, with a second whole trace as the floor reading 0.000: the
still and a turn the same to the pixel, a walk of four passes along the big room's diagonal at most
0.030 percent of pixels past 6, the cut with 251's lift 0.873 on its second frame. The first form
spread through changes too, and the suite found what the rooms' reads did not: the panel's room in a
render texture at Low kept a tenth of its light ten frames after the panel went dark, 6.1, 5.9, 3.9
down to 0.6 where traced whole it fell to 0 the frame after, the half keeping the light of before
handing it back through the light past the first bounce; and the sun put out over the open court
differed from the whole trace on the frame after by 40.6 percent of pixels past 6 and arrived a
frame later. Waiting for the change to settle, the panel's room falls to 0 the frame after and
every frame after the sun's step reads as the whole trace's, so a change of light costs nothing and
Decision 95 has no cost to record. gi.toggle spread off traces every probe each frame and gi.state
says which cascades trace in halves; docs/global-illumination.md says both. The test settles the
court in halves and whole, finds them alike, puts the sun out, finds the cascades whole through the
settling and in halves after, and every frame after the step alike; spread through the change, its
first frame differs and the test fails, as the panel's test failed in the suite. The saving goes
where a program's lights and field keep still: each change traces the coarser cascades whole for the
16 frames after it, so Ore's walk halved them in 87 of 100 samples, a game stepping its sun every 83
frames traces them whole for 16 of each 83, and one that streams its land as the player moves, a
section built every few frames, halves them little while moving and fully once it stands; a game
reads its own share by gi.state on the package that carries the spread.

Item 250, the faint glow lights at a hit. The read by part of the screen probes' pass, Ore at High
at 1280 by 720 under the lock held alone, found the glow lights' forms at the probes' rays' hits the
largest cost left: with the hits given no glow light the frame went from 7.93 to 4.77 ms, the
screen probes' pass from 3.17 to 1.67 and the first cascade's trace from 1.61 to 0.33; their two
marches a hit about 0.5 and 0.3 of it, the reach checks over 64 lights nothing measurable, the
bookkeeping for the marches nothing either. A count at the hits in Ore: 29 to 36 lights in reach of
a hit, of which 26 to 29 take the faces, 2 to 6 the pieces or the closed form; the brightest 4 give
87 to 93 percent of a hit's light and the brightest 8 97 to 99; over a 16th of the largest 3.8 to
5.5 lights with 3.5 to 5.5 percent left, over a 32nd 4.9 to 6.8 with 1.6 to 2.7, over a 64th 5.9 to
7.8 with 0.5 to 1.3. A light giving a hit under a 32nd of the largest of the lights before it now
takes one point at its box's middle, glowLightPoint, the faces' formula with one distance for every
face and each face's slant toward the hit taken from that face, and the rest their own forms, in
one pass, since a light read before the largest takes its own form at worst and a light on its
point gives under a 32nd of the largest of all; two passes, the largest found first, took the frame
to 8.25 ms. The first point took each face's slant from the middle and gave a hall of glowing
blocks on its floor half the tail's light, the blocks lighting the floor at a grazing angle that
the middle, lower than the top face, halved; the slant from each face kept it. The frame 7.86 and
7.86 ms against 7.36 and 7.36, the first cascade's trace 1.59 against 1.32, the screen probes' pass
3.14 against 3.04 to 3.06. GlowLightsTests and BounceRoomsTests pass, 79 with the norm and the
listing. gi.toggle points off takes every light by its own form, a word of the lights the change
signature leaves out, so the switch begins no carried cycle again; gi.state says which. The test
settles a closed hall lit by a 7 by 7 grid of small glowing blocks at Low and at High with the
points on and off: the mean apart 0.30 and 0.41 levels at Low, 0.24 and 0.27 at High, where the
faint lights given nothing lie 1.02 and 0.91 apart and fail its bound of 0.6, and none taken as a
point moves nothing and fails its other half.

Item 255, read and the first of its mends. The driver's own cache stands under XDG_CACHE_HOME, so
an empty one is a player's first run: there the voxel game's terrain pipeline took 12,099 ms to
compile without the layer, and with that folder's driver cache warm and the engine's file missing
93 ms. Half of the 12 s is a second copy of litLight in the game's shader, from its line calling lit
on its unlit branch, which can never reach the light: with unlit there the fragment went from
1,303,784 to 702,448 bytes and the cold compile from 12,365 and 13,627 ms to 5,865 and 5,242. That
line is the game's; the shaders guide now says lit and litLight each carry the whole lighting, so a
shader calls one once and unlit on an unlit branch. The cold first frame of shaders_bounce_rooms
from an empty cache folder took 28,594 and 30,102 ms, its 26 pipelines made one after another on
the frame's thread, the probes' nine kernels about 22.5 s of the driver's time, gi_screen 6.1 and
the trace's four builds 3.2 to 6.2. The probes' kernels are now made together on workers at the
first frame that traces them, the frame waiting for all: the first frame 12,322 and 12,148 ms, the
driver's time 30.9 s spread across them. The test draws the Cornell box's first frame with the
kernels made one after another and together, finds one compile in the driver at a time the first
way and two at once the second, a first compile on a worker waiting for a second to reach it, and
the two frames the same.

Item 264, the plan. Every place in world floats meets one of two hubs, the instance ring written
once a frame and shared by every view (ModelNode.cs 55), and the view-projection pushed with each
batch (ModelNode.cs 250 and 669). So the frame has one render origin, the window camera's eye as
given, and every place is taken against it on the CPU in doubles, where the difference of two floats
is exact. Each view pushes its view-projection times the origin's translation, worked in doubles,
so a vertex is placed in numbers as small as the view, and every place a pass reads in a uniform is
handed against the origin the same way: the lights' positions (LightingUbo.cs 268,
GlobalIlluminationRenderer.cs 507), the probes' middles (LightingUboPrepare.cs 197), the field's
origins and eye (SceneField.cs 589), the glow lights' middles (GlowLights.cs 250) and the
reflection probes'. A grid kept in world numbers, the field's cells and the shadows' texels, is
handed as the origin's place within its own cell, worked in doubles, so its snap holds as the eye
moves.

One question is the programs' and so not mine to settle. ModelVertexOutput.world is a program
shader's world point, which a shader of its own may map a texture by. Kept as it is, the vertex
stage hands it as the origin plus the point against it, in floats as now, and the engine's own
passes read the point against the origin, which toEye holds exactly once the instance is against the
origin. Turned into the point against the origin, every program shader that reads world as a world
place changes. I would keep it, the documents saying its precision is a float's at its distance and
that a program far out keeps its own origin. Decided on 2026-11-12 in my session: world stays the
absolute world point, so no program shader changes.

The parts, each its own commit with its test, in this order.
1. The origin with nothing moved. The rebasing is laid through the CPU and the shaders with the
origin held at zero, every place, uniform, cull box and view-projection taking it, and every
reference frame and test reading bit for bit as before. This carries the most files and changes no
picture, so a fault in it shows as a frame that moved.
2. The origin at the eye. The instance ring and the ECS chunks' copies (ModelRenderer.Instances.cs
90, MeshEntityDraws.cs 648, rebased at the copy so a reused chunk stays right), the immediate
vertices (DrawList.cs 378), the particles (particles.slang 66), the sky's translation to nothing
(E3D.Environment.cs 107), the culling's boxes and planes (ModelRenderer.Culling.cs 20 to 92), the
per object velocity's two rows (ModelDrawList.cs 338), and the eye handed where EyeOf now inverts a
view-projection to find it (LightingUboPrepare.cs 336 and its callers). Skinning poses in a model's
space and needs nothing.
3. The shadows. The cascades fit against the origin and snapped against the world's lattice by the
origin's place within a texel in light space (Shadows.cs 121 to 196), the spot and point lights from
their place against it (Shadows.cs 249 and 304), each light's view-projection pushed times the
origin.
4. The frame before. The temporal antialiasing (TemporalAntialiasing.cs 162), the motion blur
(Bloom.cs 517), the ambient occlusion's history (AmbientOcclusion.cs 273), the reflections' history
(LightingUboPrepare.cs 105) and the screen probes' and glow texels' lookups (gi_screen_filter.slang
119, gi_screen_glow.slang 294) take this frame's inverse against the origin times the move of the
origin since the frame before times that frame's view-projection against its own origin, small
numbers that need not cancel, and keep the point and the last eye against the origin.
5. The points from the depth. ao.slang 83 and its normals and horizons, screenmarch.slang 101 and
its march, gi_screen.slang's worldAt where 257 left it, fog_scatter.slang 114, particle_step.slang
95, the reflections' ScreenCamera (lights.slang 607), and the eye's distance in decode, dof and
subsurface_blur through inverseFromEye; gi_reference.slang 599 and the debug views alike.
6. The rays from the screen on the CPU. GetScreenToWorldRayEx (E3D.Physics.cs 517) and
Vector3Unproject (E3D.RayMath.cs 198) take their way from the rotation alone and leave from the
camera's place, and GetWorldToScreenEx (E3D.Camera3D.cs 23) takes the point less the camera's place.
7. The world moved. A call, MoveWorld(offset), that a program makes in the frame it moves every
place it hands the engine by the same whole offset. It shifts what the engine keeps by place: the
field's plan, its built and changed regions, its jobs and every set that knows a mesh by its matrix,
rekeyed by the matrix plus the offset (SceneFieldPlan.cs 94 and 184 to 237, SceneField.cs 84 and
89), so a mesh at its new matrix is the same still mesh and none is taken for a mover for
MobileSettleFrames, the game's lead; the world probes' origins (GlobalIlluminationRenderer.cs 88),
the glow lights' places rekeyed (GlowLights.cs 451), the reflection probes' places, the histories
of 4 shifted by the offset, the particles' positions on the GPU by a pass, the physics' poses and
the frames before them (PhysicsWorld.cs 28 and 70), and the audio's voices. The field's texels hold
only where the offset is a whole count of every cascade's cell, the far one's among them; another
offset marks the field to be built again, which the call's documents say. Since a rekeyed mesh must
match to the bit the matrix the program hands next frame, the offset is a whole number of units
and is added in doubles, and the test moves a world of a thousand meshes and reads the field's
still count and its builds through the move.
8. The ambient occlusion by place, the game's second ask, which is no precision since the scene was
handed within 450 units of zero at both places. Its likeliest cause to read first is the field's
grid, the room's walls falling at another phase of the cells at each place; the same room is drawn
at fractions of a cell apart and read, and if its occlusion follows the phase the term is mended to
read alike at every phase, with that test. It can go before 1, standing apart from the rest.

The test of each part draws a scene of its kind at the origin and at 4,450, 71,000 and 1,140,000
units out and reads it alike within a margin said for each: a lit room under the sun's shadows, the
glow-lit wall, a mirror's march, the ambient occlusion, a camera turning under temporal
antialiasing, particles and fog, and a picked point; at 1,140,000 a float steps by an eighth, so the
places handed are a whole number of units there. The cost is read on Ore at High before and after 2,
the instance ring and the chunks being rebased each frame on the CPU. Each part's account goes in
its own Reply.

Item 264, part 8, the field far out, its tie and the occlusion by place.

The splat's rows. A build hands the splat each mesh's rows with their translation taken against the
cascade's corner in doubles, and the corner as nothing (`SceneFieldRenderer.Instances`), so a mesh
far out and turned is put in the cells with no float of the world's size in it, where before its
corners were placed in world floats on the GPU and then less the corner, each rounding to the step
there. The repaint takes its rows the same way. The plan's snap stays in floats, since a cascade's
corner is a whole count of eight of its cells, a power of two of the eye's units for every counted
cascade, and the eye over a power of two and the half added are exact at the field's sizes, so the
corner is the float the doubles would give.

The independent read's three of the field.
- The way ahead (finding 3) is found through the view made with the eye at nothing and added to the
  eye once (`AheadOf`). Through the world view-projection's inverse it read 0.33 off at 71,680 and
  8.70 of its 8 units at 1,140,736; now within the float's step at the eye at every distance.
- A moving mesh's stamp (finding 9) keeps its world matrix and inverts it taken against the frame's
  origin in doubles (`StampRows`). Through the float inverse taken against the origin after, the
  stamp's box sat 0.022 off at 71,680 and 0.376 at 1,140,736, on a mesh 0.6 across; now within a
  thousandth.
- The far cascade's size (finding 10) follows the far distance the camera gives where its
  projection is made, carried with its eye (`ViewEye.Far`, `WindowView.Far`), BeginMode3D's 4,000
  and a camera entity's `Far`. Read back through a float inverse it read 3,971.9 at the world's
  origin, 3,917.6 at 71,680 and 8,901.6 at 1,140,736, the hundredth the inverse loses at any
  distance enough to turn the far cell across a size; now 4,000 at each. One eye noted twice
  through one view-projection, as the modeling window notes the window's eye beside BeginMode3D,
  keeps the far distance either gave (`ModelDrawList.NoteEye`), where two notes unequal by the far
  distance alone let the eye go, and the suite's first leg read the window's outlines far out
  through the world's numbers, 0.055 off, which NotedEyeTests now holds. The far distance has one
  reader, the far cascade's cell, which it bounds (`SceneFieldRenderer.FarPlane`); no push, depth,
  plane or reprojection reads it, and a note without one says it was not given, the same
  view-projection's projection clipping at the same far plane.

The tie. A cell's word ends in seven bits of the mesh's own, from its matrix's turn and scale, its
color and its triangles (`Tie`), the distance, the rim, the side, the front and the way moved up by
eight, so of two meshes' faces as near a cell and looking the same way, as two boxes' sides in one
plane, the same one paints the cell in every run, every build and after the world moved, where the
thread that wrote last did. The bits leave the translation out, so a repaint after a move matches
the words the build kept.

Crossing a 16 line. The game's control hops of three blocks across its whole 16s, 7,680 and 7,696,
3,584 and 3,600 in the engine's numbers with the game's origin at 4,096, carry the field's middle,
eight units ahead of the eye at the game's ahead of 0.5, across 3,592 and 3,608, cascade 3's snap
lines, a cascade's corner being the multiple of eight of its cells nearest the middle, 16 for cascade
3's cells of 2, so it moves at 16k + 8. Cascade 3 shifts 16 and builds the slab it brings in, eight
of its cells deep across its 128 by 128 face, 82,028 and 401,810 triangles over 16 and 24 frames at
Low and 0.35 to 1.08 M over 12 to 34 at High (voxel-i264-10/ctl). Cascade 0 shifts too, two snaps,
a slab that ends inside a frame's budget and is no change to field.state, as the step runs' hop
across none crosses cascade 2's line, 3,604, alone. The eye crossed no line of the origin, which
rounds to the nearest 16, and where it does the field builds nothing, reading its meshes in world
numbers. The step's own hop crosses cascade 3's line too, 4,104 before the step, so its 29 frames
are the shift's and the step adds nothing.

What the step still costs at High. The field's 10.6 to 15.8 ms are the plan's move
(`SceneFieldPlan.Move`), every set it keeps by a mesh's matrix made again with the matrix moved,
4.0 ms for 3,000 still meshes and 12.8 ms for 6,000 on this CPU, the frame after it as any other
with no build and every mesh still still. The illumination's 10.8 to 14.1 ms are the device rays'
scene growing with the land a flight meets, item 272's. Traced across the step at 8,192 at High, the
step frame's call lasts 13.0 and 8.3 ms, 8.8 and 5.9 of it the rays' scene, mostly making its
buffers again larger (`CreateAddressedBuffer`, 6.4 and 3.6 ms), and the glow lights' joining of
touching boxes 3.1 and 1.2; the same buffers are made 29 and 24 times in the six seconds around the
step, before it as after, and 6 and 9 times in a flight across none over land met before. The step
adds no work of its own there, and 272's mend, the scene letting go of meshes no longer drawn and
growing in steps it keeps, takes it with the rest.

The occlusion by place. A room of blocks drawn at fractions of the finest cascade's cell of 0.25
apart, the camera moved with it, the sun casting no shadow so the shadow map's texels play no part,
at Low on the RTX 4070 against the room at no offset. At an eighth to a half of a cell 3 to 11 of
57,600 pixels differ by 2 levels or more, the edges of the blocks moving with the camera's
rounding, and the same with the occlusion off. The occlusion follows no phase of the field's grid,
since a whole wall lies on a cell's side in every cascade whose corner snaps to a multiple of 2, 4
and 8 units, the cascades of cells of 0.25 to 1. At a whole unit apart 4,741 pixels differ by 2
levels or more, 7 by 8 or more and 15.2 at the most, and 4,905 with the occlusion off, so it is the
indirect light, the coarse cascades' cells of 2 and of 4, whose corners snap to 16 and 32, meet a
wall at a whole unit at another phase, and the probes there gather it otherwise. That is the
field's own sampling, not precision, and no mend makes it alike at every phase short of cells of 1
over the same reach, eight times the cells of each coarse cascade, the game's 16.4 MB a cascade
going to 131 MB, or each coarse cascade built at two phases a unit apart and their light averaged,
twice its splat for every build. I leave it, and the game's two places within 450 units of zero
read alike where their walls fall at the same phase of the cells of 2.

RenderOriginTests whole on the three devices, all 31 passing on each, the most of each case and the
pixels past a level where any.

| Case | RTX 4070 | Mesa 26.2.4 | Mesa 25.2.8 |
|---|---|---|---|
| Room in fog, its ball back | 0.000 | 0.000 | 0.000 |
| Room in a texture, off and at Low | 0.000 | 0.000 | 0.000 |
| Room | 0.000 | 0.000 | 0.000 |
| Scene at Low and High, past 1 | 0.246, 0 | 0.393, 0 | 1.480, 2 |
| Shadowed room, past 4 | 10.73, 2 | 11.34, 2 | 25.43, 5 |
| Crossing, past 1 | 0.089, 0 | 13.21, 8 | 29.97, 14 |
| Mirror room, past 1 | 7.29, 6 | 7.18, 4 | 8.88, 6 |
| Turning room | 0.000 | 0.000 | 0.000 |

Each case as at c6bac6dc but the room on Mesa 25.2.8 and the room in fog on Mesa 26.2.4, a few
hundredths there, now none, the tie broken.

The tests, each failing without its mend. `SceneFieldTests` reads the way ahead of a camera at
the world's origin, 71,680 and 1,140,736, within 0.25 of 8 units along its view, where it read 0.33
and 8.70 off, and a moving mesh turned and scaled, six tenths of a unit across, stamped where it is
drawn within a thousandth there, where 0.022 and 0.376; `NotedEyeTests` reads the window's view a
`BeginMode3D` makes carrying its camera's far plane, 4,000 at each distance, where 3,971.9, 3,917.6
and 8,901.6; all on the CPU. `SceneFieldTests` draws a floor slab with blocks of six colors sunk
into its front, each block's front face in the slab's, twice, and reads the two frames alike at
every pixel; without the mesh's bits Mesa 26.2.4 read them 10,900 and 1,841 pixels apart in two runs
of three, the RTX 4070 alike each time, its threads keeping their order. RenderOriginTests' room in
a render texture keeps its three cases at global illumination off, which read its camera's place
in the frame, and gains one at Low at 1,140,736, which reads the field's build of its turned block:
without the rows 4.00 levels at 686 texels against the bound of 1.5, 0.000 with them on the three
devices. The fog room has its ball again, 0.000 at every distance on the three.

The tie's test starts two apps, 18.8 seconds on lavapipe; the room's case at Low two, 9.0 seconds,
where its cases at global illumination off take 1.6 to 1.7 each, 5.5 seconds the three. None moves
a Windows part. `SceneFieldTests` stays in its own process in the first half, `NotedEyeTests` in
Api in the second, and `RenderOriginTests` in its own in the third, gaining the one case, whose
time there the run after the push reads.

MoveWorld's words say what the game's read left it to guess (`E3D.MoveWorld`, and the guide's "Far
from the world's origin"). The meshes known by their matrices are every one the frame draws, mesh
entities' and `DrawMesh`'s and each of `DrawMeshInstanced`'s alike; the bodies and the sounds move
in the call itself, so a program that sets their places in the frame of the move calls it first and
sets them where they are after the step; and any whole step is taken, a teleport's of thousands of
units as well as a step back near the origin.

The binds the scene pass pays since the origin, read on the game's own room and not part of this
commit. Bisected on game-A's binary over each commit's assemblies and shaders, it comes with
80fe61d2, the origin at the eye, 0.427 ms of `cpu.hdr_scene` at a0af50a3 against 0.675, game-A
reading 0.425 to 0.459 on 6.2.60. It holds capped at 100 a second with the GPU idle, 0.62 to 0.64
against 0.37 to 0.47, so it is the driver's record calls' own cost, 147 to 290 µs a frame under
`vkCmdBindDescriptorSets` and 36 to 138 under `vkCmdDrawIndexed`, hidden by the GPU in every frame
read. The calls are the same kinds in the same proportions, about 3 percent fewer since the origin;
not the origin's value, 264.9 with its override at zero as slow; not the draw list's lock, level on
the one thread that both records and draws; not the noted eyes, 264.9 with `NoteEye` doing nothing as
slow. The panel's leads read next, pinned to the performance cores, the vector state, AVX off on
both builds, and 80fe61d2 itself with its override at zero.

The strict build is clean and the suite passed both legs, 2,443, in 25 m 25 s and 25 m 5 s,
the first run of both read before the noted eye's mend 2,440 and the two cases of the modeling
window's outlines far out. The package is 6.2.60-local.264.11.
