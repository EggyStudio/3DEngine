# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `98a1edca`. Item 2's parts d and e, the lamps: a small emitter is carried through the
bounce as a light of its own, as the part named and the measure chose. Each emissive instance whose
box is no longer than twice the first cascade's probe spacing is a box light, its glow and its
surface's share of its box's, the 64 nearest the field's eye, each reaching where it lights a
surface by a fiftieth; emitters square to the axes that touch and glow alike are one light of the
box around them, the pressed faces left out of its surface, so the stack of four throws no ring; the
field flags the cells a carried emitter paints, so a ray that meets it leaves its glow out and a
reflection keeps it, the eight cells blended by hand near a flag. Its light at a point is Lambert's
sum over the edges of each face the point lies before, clipped at the point's plane, by Heitz's fit,
and past four half diagonals each face as a point, within 3% of the sum; how much of it a point sees
is a march through the field as a distance field, the way's clearance against the cone the light
spans, so a shadow's edge is as soft as the light is wide, the surface's own cells and the light's
box never counted, the share taken between steps as a sphere tracer finds the nearest pass, read
stepping out along the normal inside a thin wall, which took the 19:30 and 19:45 readings' steps and
rings away. The screen's probes march to the halves of each face of a light that gives a sixteenth
of the lights' light and keep sixteen levels of it, four bits a light in twelve words, the fainter
ones marched to their middles once and summed, and the model pass takes a pixel's lights from the
probes around it, marching itself only where no probe stands on a surface like the pixel's, a render
texture's or past the screen's probes, to the four brightest; a probe's ray's hit takes the two
brightest, so walls bounce a lamp on. Measured against the path-traced reference: the ring from 0.8
to 4.2 blocks reads 0.96 to 1.05 at every quality, the eighth harmonic 0.005 where it was 0.17 and
0.35, the pool the same from five to eighty blocks up; a ninth room, the block beside a wall half
its height and a stack of four, 0.052, 0.048 and 0.045 of its light where 0.200, 0.193 and 0.171;
the Cornell box 0.141, 0.129 and 0.117 where 0.203, 0.180 and 0.183 and the strip 0.256, 0.248 and
0.270 where 0.466, 0.473 and 0.448, every bound set by its rule, five tightened by a third or more.
Lavapipe crashed on an array indexed by the light, kept in its own memory since, and its hits let a
lamp light its own faces, shut by a half-cell margin, the two devices reading alike. The cost on the
RTX 4070: the Cornell box's panel 0.17 to 0.22 ms of the bounce and 0.05 to 0.07 of the model pass,
the guide's table 0.58, 0.76 and 0.99; a dark room at 1280 by 720 at High 0.35 ms for one block, 1.0
for sixteen and 2.2 for sixty-four, where 7.6 before the tiered marches, the faint lights summed and
the edge pixels taking the probes. Right: the cause measured before the remedy, the remedy measured
against a reference that was drawn for it, the rings and the stack mended at their causes and not
softened, the limit said in the guide, and the cost brought down three ways with each named. One
thing to know, the owner's eye to say: a probe keeps sixteen levels of how much of a light it sees,
so a penumbra wide on the screen could show as steps where four probes' blend does not hide them.
`SceneFieldTests.cs` stands at 791 lines of N 1.3's 800. The suite: 1,665 passed after `gi.slang`
went past 800 and the package was packed before `glow.slang`, both mended; on lavapipe 380 passed
and 7 skipped with no validation error. The game's session is asked to measure its room of
glowstones on this commit, and its 19:16 figures, 1.56 ms of the HDR pass and 0.81 of the bounce in
sixteen lamps' room, are read against the commit's 1.0. Item 2 has part c left; Verdict 30's census
of Manor's buffers comes first, then item 5's shares, then c. The game's session read the commit at
once: parts d and e hold in the game, the lobes 0.01 of the lamp's light or less at both cells and
every quality and the pool the same from 5 to 80 blocks, its two entries taken off; and two new
entries, the glow lights losing the GPU device in a closed room seen from twenty blocks off outside,
three runs of five, Xid 31, a uniform read out of range, which is Verdict 47 and comes before
everything, and the model pass's 0.67 ms for sixteen lamps where 0.2 was expected, which the part
keeps as its cost to bring down. At 23:40 the owner looked at the commit: the bounce looks awesome,
in their word, and with the screen's probes off it is wholly steady and hard, while with them on it
is smooth but changes as the camera turns, the penumbras' steps and the borders moving with the
view, noise for a second after a move, and light leaking where none should be; written as item 2's
part f, after Verdicts 47 and 30 and before c.

Before it, `71050c8f` came, Verdict 46's three things. The table of raylib's examples is written
again from `HEAD` in a worktree of its own, so nothing of the tree's work in progress reaches it,
and gains the five programs of this engine's own that were opened by name with no row,
`shaders_bounce_rooms`, `shaders_cornell_box`, `shaders_reflections`, `shaders_scene_field` and
`shaders_subsurface`, the script's check passing on it; `NormTests.N_5_2` holds what of the rule the
suite can without raylib's list, every program `Program.cs` opens by name having its row, naming the
one without, which on the table before named those five, so a batch that adds an example and not its
row fails before it is committed, the script's own check staying the workflow's; and in the examples
job the two table checks, raylib's examples and raylib.h's functions, run last and under
`!cancelled()`, so a stale table no longer ends the job before the captures, the soak and the
measure, and a failing check still leaves the job red. Right, the table from `HEAD` and not the
tree, the part of the rule a test can hold held, and the checks last. NORM.md's cell for N 5.2 names
the test beside the workflow's check since. The suite on `HEAD` with the three files: 1,649 passed
and 6 skipped, the package's five and the docs script's, which want a packed package a new worktree
lacks. The next examples job judges Verdicts 30, 31 and 46 by its own lines, and a run green whole
makes 5.2 due (Decision 19). The box lights are light-right, the lamp's ring within 2% of the
path-traced reference and the ninth room from 0.347, 0.305 and 0.271 of its light to 0.061, 0.060
and 0.056; render-texture scenes, whose lights were ranked from a window's eye they lack, and the
cost are what remains before their commit. The owner looked at the tree at 19:30: the bounce works
very well, and the box lights' shadows have no penumbra and run in jagged steps, and a stack of
glowstones two by two throws a jagged ring of light, written into parts d and e with their reading.
At 19:45 the owner looked again: the shadows and the stack look better, and the light's falloff is
banded all over with dark jagged rings, the one thing left, written into the part. The run of
`e50fd247` is green on every test job, Windows a third time, and red in its examples job by the same
table; the run of `71050c8f` is in progress. Its examples job came, 66 minutes: the table check
passes, the captures draw 255 of 255 on Linux, the measure records a share for every pair but one
for the first time, so Verdicts 31 and 46 are settled, and the soak fails on Manor alone, naming it
and the measure as Verdict 30 asked, its buffers climbing from 499 to 573 in two minutes past a
bound of 527 with its memory flat, which Verdict 30 carries since as the cause to find; 5.2 waits on
that soak (Decision 19), and the shares go into the measured file (item 5).

Before it, item 2's part b came, the bounce's drift after a move, done, measured first in the game's
room drawn by the engine alone and the cause read before the remedy: not a coherent change where a
cascade moved, since a nudge of a fiftieth of a block drifted the same with no snap crossed, and
each screen probe's raw light changed by 18% of the mean with it. The normal a probe rebuilds from
the depth lies 1.6 degrees off its wall's axis, and a ray took each world probe's light from the one
texel of its octahedron it fell in, half the 16 rays on a wall square to the axes lying on texel
borders, so the rays flipped texel by texel as the camera moved, the filter made the flips the
blotches, the history averaged them, and the drift was the way back to the pattern that stands;
neither remedy the item named would have mended it, which the measure shows. Read between the four
texels around the ray, folded across the octahedron's edges as DDGI folds its border, the raw change
is 4.1%. The turn had a cause of its own: the trace read the frame before's faces at this frame's
corners, so each probe of a moved cascade took a neighbor's faces and the light bouncing again took
ten frames to find itself, and a turn moves all four cascades since they lead the eye; the lights'
buffer carries the frame before's corners, 64 bytes, and the trace reads the faces through them, the
screen's probes keeping this frame's since they read after the gather. The fifth frame after a walk
changes 0.07 levels where it changed 0.19, after a turn 0.06 where 0.84, a theory of the two at 320
by 180 holding each under 0.12 and failing without its own mend, lavapipe reading the same; the
slide test's crawl went with the flips, 0.33 levels a frame to −0.03, so it holds 0.15 absolute. The
cost 0.04 ms, the guide's table 0.41, 0.53 and 0.74. The path-traced rooms move both ways, the
Cornell box 0.170 to 0.203 of its light at Low and the strip 0.511 to 0.466, the eight views summing
4.59 where 4.62, and every bound of `BounceRoomsTests` is reset to 15% over its new reading by the
rule it was set by, five up and eight tighter, looked at here: the rule holds, and the Cornell box's
rise is the angular blur of reading between an 8 by 8 octahedron's texels, the thing part d's 256
directions would cut, so that remedy is read for the Cornell box's error as well as the lobes.
Right, the suspects measured and two cleared before anything moved, the fold DDGI's, and the faces
read where they were gathered. The owner's eye found this tree stable and good before the commit
(Decision 27). The suite: 1,652 passed, the Cornell box's two moved with the others; on lavapipe 373
passed and 7 skipped with no validation error. NORM.md's counts and SHARED.md's two rows went in
with it. Parts d and e next, together, the GPU with the game's session for their measures meanwhile.
The game's session measured d and e on the tree with bloom off, which had stood in the earlier
figures for a fifth, and parts d and e are rewritten on the measures: the finer cell did not mend
the lobes but took the pool away, e's own mechanism, and the pool's sharp edge at two and a half
blocks names the first cascade's directions; the game's `de36ead1`, its ring reading a point behind
the eye as off the picture, is the game's own. The engine's session then measured both parts on the
engine, GPU and lavapipe agreeing, and the cause is in neither the octahedron nor the field's hold
of the cube but in the probes' horizon, so the two readings this paragraph gave stand corrected and
parts d and e are one part with its measured cause and the remedy the measure names. The Windows
page of `3a83cec6` came: its tests pass whole, the leak test's hold held to app 70, so Verdict 40 is
settled after its two mends, and the job fails playing Slide, whose app stopped serving after its
first tap, Verdict 45; 5.2 waits on that alone.

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
   reads the same, the run of the game's `0ccf8f9a`, the first with the census at each step
   (`a063a336`), is green on Linux, on macOS and on the captures and red on Windows by the leak test
   alone, whose page names the steps that open the kept thread handles, the device's (Verdict 40),
   and the run of the game's `187de604` reads the same on Linux and on Windows, its macOS job and
   its captures green, so the Linux examples job waits on Windows alone. The run of `3a83cec6`, with
   Verdict 40's mends, passes its Windows tests whole, 1,608 with the leak test held to app 70 of
   its budget, in 22 m 53 s where the tests took 10 to 14 minutes before, the collection before each
   count the difference, so Verdict 40 is settled; the job then fails playing Slide, `frames.wait`
   finding no serving app after the first tap (Verdict 45), and its macOS job and captures are
   green. The runs of `817f7fd4` and the game's `de36ead1` are green on Linux, on macOS, on the
   captures and on Windows whole, the first Windows jobs green since the bump, Slide played through
   on both, so Verdict 45 is settled, a stop once in three; their examples job, the first to run
   since `22bbf15a`, fails at its first step, `build/examples-table.py --check` finding EXAMPLES.md
   out of date, and ends there before the soak and the measure (Verdict 46), so Verdicts 30 and 31
   are not yet judged; the run of `e50fd247` is green on Linux, macOS, the captures and Windows, the
   third Windows job green in a row, and red in its examples job by the same table check alone, and
   the run of `71050c8f`, with the table written again, is green on every test job and red in its
   examples job by the soak alone: the table check passes, 255 of 255 captures draw on Linux in
   1,177 seconds, the measure records every pair's share for the first time, one pair drawing no
   raylib frame, so Verdicts 31 and 46 are settled, and the soak names Manor, its buffers climbing
   from 499 to 573 in two minutes past a bound of 527 with its memory flat (Verdict 30), on which
   5.2 waits (Decision 19); the drive script says since `e50fd247` whether a game's process lives
   when a command finds no session, and its exit code and Windows' crash event when it ended. The
   page showed the leak test's progress line as its first of 198, `app 2`, where the line named its
   app all along; the page shows the last of a repeated line since `88673244`. Verdicts 40 and 44
   are carried out there, the leak test following four apps through every plugin's making, a failing
   frame written where the job uploads it with each surface's means, and four references drawn again
   after a drift of up to 2.16% on the RTX 4070 itself; the runs since read the references green and
   the leak test red. The examples job runs when Windows and macOS are both green in one run, which
   they are since `817f7fd4`. When every job is green the owner is told, since 5.2 is due (Decision
   19). With the batch that next touches `build/test.py`, it takes from BevyCSharp's `1f68fde8` the
   two cases of a theory whose names are cut to the same as one counted apart, which its page reads
   as one today (SHARED.md), and runs the game's `3DEngine.Game.Tests`, 22 tests of its light,
   meshes, body, saves and generation that need no window, beside its own through its `--project`,
   counted on the page apart from the engine's and reddening the run as the engine's do (ASKS.md).
   The engine's own despawn of what a state scopes is read against a soak of the world's entity
   indices across many transitions, which in BevyCSharp found Bevy 0.20.0 losing every index it
   despawned that way (SHARED.md), with the batch that next touches states. Each push's run is read
   by the reviewing session, and a failure it names comes first here.

2. **What the owner saw on 2026-10-10 (Decision 27), before item 3's remainder.** The owner looked
   at `shaders_subsurface` and at the voxel game and found four things, each measured before it is
   changed, each a commit of its own with its numbers before and after and a test that holds the
   measure, the voxel game's read through the testbed at eight columns, and the game's session asked
   to write what it measures of them in ASKS.md. The game's session measured b, c and d on the game
   and wrote them in ASKS.md with captures under `.github/assets/asks`, which go in with that file.
   The owner looked again at 17:20 on the tree: the bounce stable in motion, the lamp's lobes gone
   at a cell of 0.125, and a lamp's light at a distance aliased at its border and gone farther off,
   so b closes with its commit, d gains its lever and e is added. At 23:40 the owner looked at
   `98a1edca` and found the bounce awesome and the screen's probes its one unsteady part, which is
   part f. The order of work: f, then c, the others being done; the game's session is asked to
   measure d at both cells and e along the distance, in ASKS.md as before.

   **a. The dark ring at the terminator and skin's jagged edge, done at `9064685a`.** Measured
   first: no dark ring of its own but a bright line six degrees past the terminator, the thickness
   read as nothing at the sphere's edge as the light sees it, by the map's mean taking in the empty
   map beyond the edge and the field's march calling the part a sheet, and a march still inside at
   the reach returning the reach; mended by the mean of the depths in front alone, a light with a
   map measuring through it wherever it has one, the light faded out by the reach and the falloff as
   the spread's own Gaussian. Wax falls 84, 69, 60, 54, 50, 47, 43 from 88 to 100 degrees and skin's
   edge lies 0.04 of a degree from its neighbors where it ran 0.41; two theories hold them. The
   owner's eye judges the look (Decision 27). A gap kept: a Burley tail is longer than one
   Gaussian's, which a sum of Gaussians in the spread would follow.

   **b. The bounce's drift after a move, done at `817f7fd4`.** Measured first in the game's room
   drawn by the engine alone: a nudge of a fiftieth of a block drifted as a walk did with no cascade
   moved, and a screen probe's raw light changed by 18% with it, since a ray took a world probe's
   light from the one texel it fell in and half the rays on a wall square to the axes lie on texel
   borders, the normal rebuilt from the depth wavering 1.6 degrees; read between the four texels
   around the ray, folded across the octahedron's edges, 4.1%. The turn's own cause, the trace
   reading the frame before's faces at this frame's corners after every cascade moved with the eye,
   is mended by the frame before's corners carried in the lights' buffer. The fifth frame after a
   walk 0.19 levels to 0.07 and after a turn 0.84 to 0.06, a theory holding each under 0.12; the
   slide's crawl to −0.03, held at 0.15 absolute; 0.04 ms. The owner's eye found it stable and good
   (Decision 27). What drift is left lies on the floor before the glowing block, patches a tile wide
   where some of a probe's rays meet the block and the rest miss it, which is part d's ground.

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

   **d and e. The lamp's lobes, its pool's edge and its loss at a distance, done at `98a1edca`.**
   The measured cause: the world's probes stand above a floor at their spacing and their faces see
   none of a lamp lying on it, so the floor took the lamp from the screen probes' sixteen rays
   alone, which made the lobes, the edge at their reach and the loss where no ray reached. The
   remedy the measure named: a small emitter carried through the bounce as a box light, lit in
   closed form, seen through the field as a distance field for a soft shadow, touching emitters one
   light, the cells it paints flagged so no ray counts it twice, the 64 nearest the eye. The ring
   reads 0.96 to 1.05 of the path-traced reference at every quality with no lobes, the pool the same
   from five to eighty blocks, the ninth room and the Cornell box and the strip nearer their
   references by a third and more, 0.35 to 2.2 ms for one to sixty-four lamps. The owner's three
   looks at the tree, the shadows' steps, the stack's ring and the falloff's bands, were each
   measured and mended at the cause before the commit; the owner's eye on the commit judges the look
   (Decision 27). The game's session holds the part in the game, lobes 0.01 or less and the pool the
   same from 5 to 80 blocks, and measures the cost in its room of sixteen glowstones at 1280 by 720:
   the bounce 0.85 ms, near the 0.8 expected, and the model pass 0.67 ms where 0.2 was, three times
   it. What remains of the part is that cost: read where the model pass spends it, the closed form
   over every light in reach at every pixel where the probes' bits already say which lights a
   pixel's probes see, or the marches of the pixels no probe holds, and brought down by what the
   reading names, the pixel reading the lights its probes mark alone first, the game's room the
   measure, with the next batch after Verdicts 47 and 30.

   **f. The screen's probes make the light depend on the view: as the camera turns, the penumbras'
   steps and the lit borders move on the screen, the light is noisy for a second after a move and
   then holds with some noise left, and light leaks where none should reach.** The owner's eye at
   23:40 on `98a1edca` (Decision 27): with the screen's probes off the bounce is wholly steady under
   any motion and hard, with no penumbra; with them on it is smooth but the smoothness is the view's
   and not the world's. Read from the engine as it stands: a screen probe stands on a pixel of a
   grid fixed to the screen and traces from the surface under it, so as the camera turns the probes
   sweep across the world's surfaces and sample them in new places, the light blended between four
   probes eight pixels apart changes with where they fall, the lamps' penumbra a probe keeps in
   sixteen levels is read between probes that lie on either side of a shadow's edge, which is the
   steps, and the frame before's light carries the old places' light into the new until it has been
   averaged out, which is the second of noise; a leak is a probe that sees a lamp or a lit surface
   from where it stands while the pixel reading it, a step away across a depth edge or a corner,
   does not. Measured first, in the game's room of lamps and the Cornell box: the camera turned by a
   quarter of the probes' spacing, by half and by a whole, nothing else moving, and the picture's
   change read at each, with the screen's probes on and off, so the view's share of the change is a
   number; the penumbra's edge read along a line across it at each turn, its steps' height and how
   far they move; the frames after a turn read as part b's test reads them; and the leak read where
   a lamp stands behind a wall's corner, the lit pixels on the dark side counted. Then the remedies,
   in the order the measure names, each read against those numbers and the frame's cost: the probes'
   places kept with the world and not the screen, the grid jittered a share of its spacing each
   frame and the frame before's light brought forward by where each pixel's surface was, so what a
   probe samples averages over frames in one place in the world and a turn moves nothing but the
   picture; the penumbra read from the probes with the pixel's depth and normal weighing each, as
   the probes' light is weighed, and in more than sixteen levels, or the one or two brightest lamps'
   penumbra marched at the pixel itself near the eye, where the steps show, which the cost work of d
   and e left; and a probe's light kept from a pixel across a depth edge or a corner it does not
   share, which is the leak. The tests hold the picture's change under a quarter turn of the spacing
   to a bound with the probes on, the penumbra's edge within a pixel of where it stands with them
   off, and the leak's pixels under a count.

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
   four mends of the light through, and the first two of what remained at `63961dbb`, the material's
   own thickness from `KHR_materials_volume`, which caps the thickness measured and stands in where
   nothing measured, and a shadowed lamp's light through from its own map where the field is coarse.
   What remains of the item comes after item 2: the spread in render textures and probe captures, or
   the reason it stays out written with its number; and a test of the cap itself, the lesser of the
   measured and the given, which the example's leaf shows and no test holds, a sheet in a field of
   0.15 cells with two millimeters given read brighter on its front than with none.

4. **The animated model's meshes in the world (Decision 21), after subsurface scattering.**
   TODO.md's "Models are partial" says an entity an `AnimatedModel` draws keeps its copy's meshes
   out of the world, so it is given a capsule or a box and a mesh or hull collider on it is refused
   with a warning. The copy's meshes become the entity's meshes in the world, posed from the same
   joints as its wires and a body made from it are, so `CreatePhysicsConvexHull` and a mesh collider
   take them as they take any mesh, with a test of an animated entity whose hull follows its pose,
   the guide's section on animated models and the entry updated with what it then says.

5. **The first shares recorded from the workflow's own device.** The examples job's first green run
   puts every pair measured for the first time into notices, which the public listing of the job's
   annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's own,
   so the run after holds every pair to them and a share can only fall. They came with the examples
   job of `71050c8f`, job 114279020552, every pair but `audio_sound_multi`, whose raylib program
   drew no frame there, from 0.0 for `models_tesseract_view` to 14.4% for `audio_amp_envelope`, and
   go into the file with the next batch, the one `none` recorded as the page has it.

6. **Three packages bumped, after 5.2 is packed (Decision 20).** Vortice.Vulkan 3.2.1 to 3.3.0,
   AssimpNetter 6.0.4 to 6.0.5 and StbImageSharp 2.30.15 to 2.30.16, in one commit with the suite
   run and the notices written again, on the owner's word typed into this session once the package
   is out, as AGENTS.md has it for a version; SDL3-CS stays on its preview and NVorbis on 0.10.5.

7. **A game is written meanwhile.** When the items above wait on a run or on the owner, the next
   game of `games/` is written, as the owner asked on 2026-10-07, a later game finding nothing new
   being the point of each.

## Verdicts

Verdicts 1 to 29, 32 to 39, 41 and 42 are settled, and their numbers are not given again.

30. **The soak fails on Manor, whose buffers climb from 499 to 573 in two minutes past a bound of
    527 while its memory holds at 523 to 532 MB.** The soak's error names the game and the measure
    since the examples job of `71050c8f`, as this verdict first asked when `22bbf15a`'s named no
    game, and the other seven games pass it. Read first what grows: the device's census by kind over
    Manor's two-minute walk here, the soak's CSV beside it, so the buffers are named as the cells
    the walk opens, streamed from disk as the house is entered, which would make the growth the
    world's and the bound the plan's count of cells, or as buffers made each frame or each cell and
    never given back, a leak, which is mended where it is made; then the soak run in the workflow's
    image on four cores, where the runner draws under five frames a second, if the count here
    differs. The bound stays as it is until the cause says otherwise. Settled when an examples job
    passes the soak, and 5.2 is due with it (Decision 19).

47. **The glow lights lose the GPU device: a glowstone placed in a closed room seen from twenty
    blocks off outside it throws `ErrorDeviceLost` at the frame's submission within a few frames,
    three runs of five, none with the glow lights off, the kernel logging NVRM Xid 31, a fault of
    the memory unit reading through the constant cache.** Measured by the game's session on
    `98a1edca`, the room of white concrete 11 by 6 by 11 with the eye at the spawn at hour 10; a
    glowstone in the open, one sealed in a box of three blocks, and the same room with the eye
    inside it before the lamp lost nothing in the runs tried. A fault through the constant cache is
    a uniform read out of its range: the lights' uniform of 64 indexed past its count or below zero,
    the count read where the buffer was not written for that view, or a probe's words of the glow
    read for a pixel whose probe lies past the grid, which the eye outside the room, every pixel of
    it on a surface the lamp lights through no probe that sees the lamp, or past the screen's
    probes, would reach where the eye inside does not. Before everything else: the case built as the
    engine's own in `GlowLightsTests`, the room, the lamp and the eye twenty blocks off, run under
    the validation layer with its GPU-assisted checks on, which name an out-of-range read and the
    shader's line, and until it does, every dynamic index into the lights' uniform and the probes'
    words clamped to what was written and the count written for every view that reads it; the cause
    named in the reply with the line. Settled when the case runs twenty times in the engine and in
    the game with no loss and no validation error, and the test holds it.

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

24. **Content streamed on the go stays in the ledger, and the browser waits for a game that asks.**
    The owner chose on 2026-10-09, after reading a browser port of a large game that downloads its
    world as it is played, that the idea is recorded in SHARED.md as the file layer that port has
    (packs on a static host, reads by byte range into a block cache, a recorded first-run set,
    prefetch by the game's own streaming), to consider until a game here ships a world too large to
    download first, over a streaming file layer and over an HTTP source alone, since Manor's cells
    from disk are all a game here needs; and that the browser waits for a game that asks, its shape
    recorded for that day: .NET's browser runtime, SDL3 built with Emscripten and linked into it, a
    WebGPU `IGraphicsDevice` beside the Vulkan one without ray queries, bindless or 64-bit atomics,
    Slang to WGSL, threads behind cross-origin isolation, and a one-week feasibility spike before
    any commitment.

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

Verdict 47. The cause is the loop nested in the glow lights' march, `glowMarch` in `glow.slang` at
line 221 of `98a1edca`, which read how far under a wall's edge the way passed at each step inside
one; inlined into the world probes' trace, `gi_trace`, it lost the RTX 4070's device where their
rays met the room's walls and marched through them to the lamp. Reproduced on the game's binary in
three runs of three, and as the engine's own case in `GlowLightsTests`, the room of blocks, the lamp
and the eye twenty blocks off outside, in two of three, the kernel logging Xid 31 through the
constant cache at 0x20_00000000 for each. Bisected on a copy of the game's build: the glow light at
the screen probes' hits and in the model pass are not it, the world probes' hits are, and within
them the march; with no loop nested, with a hard share, or with the march held to eight steps none
of three lost it, and with the nested loop held to two steps, or kept from unrolling, the device was
still lost. The validation layer's GPU-assisted checks found no read of a uniform out of its range
there and kept the device, and named an older fault besides: `field_stamp` read the last run of a
brick's shape places as a whole `uint4` up to 12 bytes past its buffer's end, which is sized to
whole elements since. The depth is read once after the march, at the step that lay deepest inside:
no loss in twenty runs of the engine's case and twenty of the game's, the test holding it, and the
rooms read as they did within 0.002 of their light, the Cornell box 0.139 at Low where 0.141. Held
as the verdict asks besides: a kept glow light's index is clamped; the counts are written from
zeroed bytes for every view each frame, and the lights' buffer bound where no light bounces, never
written and so holding what its memory held before, is written as no lights; the probes' words are
read for the probes a pixel clamps into the grid, as their light is. Found on the way, and the
reason the bisect's first edits to `glow.slang` changed nothing: the shader cache's key did not
follow `__exported import`, so an edit to `glow.slang` alone, which `gi.slang` exports, left every
shader importing `gi` on its old SPIR-V; the key follows it, with a test. The suite: 1,668 passed;
on lavapipe 463 passed and 7 skipped with no validation error, the scene tests among them.

Verdict 30. The census first: `memory.buffers` gives the device's buffers alive by the file and line
of the engine that made each and what it is for, the compiler filling in the maker through
`CreateBuffer`. Over Manor's two-minute walk here, beside the soak's CSV, the buffers that moved
were `MeshStore`'s, a vertex and an index buffer for each mesh, 176 meshes at sixteen cells and 259
at twenty-three, falling again as cells were let go: the cells the walk opens, the world's, and no
leak. What made them many was the spawner, which de-indexed a model's meshes again at every spawn
into arrays of their own, where the renderer keeps a mesh's buffers by its positions array, so every
cell that placed a model uploaded a copy of it. A scene's mesh is de-indexed once and shared by
every entity spawned from it, with a test, and Manor's buffers over a walk went from 385 to 668 to
96 to 158, 29 meshes at sixteen cells. The soak in the workflow's image, lavapipe on one core with
the package packed there, played two turns in two minutes as the runner does, and its least still
climbed, 133 to 149 against a bound of 143, as the walk entered the house and its models loaded.
That is the world's growth the verdict names, so the bound for a game that streams its level is the
most a whole walk through it holds, 158 over every point of the route on the RTX 4070 and 162 on
lavapipe, which `soak-check.py` holds Manor's buffers to with the same slack in place of the first
half's least, with tests that the same climb fails a game that streams nothing and that Manor's past
its walk fails. Settled when an examples job passes the soak.
