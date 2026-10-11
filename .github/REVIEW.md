# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `b1e86aef`. Item 2's part f, the first way, built as the part chose and measured as
it asked. The screen's probes keep their tiles and march to the lamps from a point of the tile that
moves frame to frame, the middles of its four by four cells in Bayer's order, so a round of sixteen
frames marches from each texel of the half-sized depth under the tile once, a byte a lamp where
sixteen levels. The lamps' light is blended over the frames at each texel of the half-sized depth in
a compute pass ahead of the bounce, `gi_screen_glow.slang`, rather than in two targets of the
window: each texel takes each lamp's share from the four probes around it where they marched from,
weighed by distance, normal and the depth between, keeps a running mean over the round and a
thirty-second after, reprojected by its surface, and drops it where no texel held the surface, where
a lamp that reaches it is new to its place, the lamps kept in their places frame to frame, or where
after a round the frame's light lies past half again the round's furthest; of the nearest lamp a
texel keeps its share of a rough unhidden light which the model pass works out again at the pixel,
so a lamp's near falloff is drawn per pixel, and the pass reads two by two texels by distance and
normal. The blend was tried at the probes first and flickered 6% over a round as the points moved
with them, which put it at the texels. Where no probe holds a pixel it marches to the faces' halves,
so the shadow stays dark with the probes off where it read 0.3 to 1.0 of the line's most light.
Measured at 640 by 360: the penumbra's strays 0.36 to 0.89% for turns of one to eight pixels, where
3.1 to 7.9% before and 0.69 to 0.91% for a pixel marching itself, the line within 0.8% over a round,
a lamp placed shown in its first frame and within 1% a round on, a wall taken away shown in its
first frame; in the game's room the turn's share 0.42 to 0.58% against 0.37 to 0.45% with the probes
off; the cost 0.35, 1.08 and 2.77 ms for one, sixteen and sixty-four lamps where 0.30, 1.07 and
2.65, the game's room 0.1 ms cheaper and a frame with no lamp skipping the pass. `GlowPenumbraTests`
holds the turn, the round, the lamp placed, the wall taken away and the shadow with the probes off,
at 320 by 180; the strip's and the ninth room's bounds move with their reason, half-sized texels two
pixels wide at 160 by 90 beside a strip six hundredths of a block thick, the two within 0.1% and
0.25% at 1280 by 720. Right, the view's share taken out at the texel where the probes' places stay,
every drop condition named and tested, and the cost held within hundredths. Two things left, which
the part keeps: before the field holds a wall, in an app's first frames, every point sees the lamp
and a shadow's texels keep that for some hundred frames, which the blend's drop by a jump could not
take since a penumbra never finished a round so; the blend restarts where the field's cascade under
the texel's point was built this frame, which the field knows, measured by the frames a shadow takes
to darken from an app's start. And the probes' filter finds a probe's place in the frame before a
pixel off where the probe stands, which put right read the thin room 0.3 to 0.6% further from its
reference; the offset is put right and the thin room's loss read for the filter's width, which is
the knob and not the accident, its bound moved by the rule with the reason. The owner's eye judges
the look and the settling (Decision 27), the finer grid for the two brightest lamps the second way
should it want more. The suite: 1,674 passed; on lavapipe 386 passed and 8 skipped with no
validation error in 9.3 minutes where 8.1, which the Windows page reads against Verdict 48's
quarter; the game's glowstone room lost no device in eight runs. Part c next, then the two left,
item 9 and item 8.

Before it, `3c7fe822` came, the reference's colors, item 2's apparatus: `BounceReference` traced a
copy's color times its texture's average alone, so a mesh colored in its vertices, as the game's
sections of white concrete, was traced white and read 0.58 of its light apart from the frame. Each
corner of the ray scene carries its vertex's color in the w its structure's build leaves unread,
three sRGB bytes under a bit that keeps the word a whole float, white where a mesh has none, from
the colors the field and the model pass read; the reference and a ray's reflection in the model pass
take a hit's color as the copy's times its three corners' colors, linear, blended by the hit's
barycentrics as the model pass blends them. A slab colored in its vertices under a white material
reads in the frame as in the reference within 1%, as a slab colored in its material does, with a
test that wants the GPU's rays. Right, the colors carried in a word the structure already held
rather than a buffer added, and the reflection given them with the reference so the two read one
color. The suite: 1,671 passed; on lavapipe 383 passed and 8 skipped with no validation error; the
game's room seen from outside faulted in none of five runs by Xid. Part f next, measured in the
game's room against the reference, which can read that room since; then part c, item 9 and item 8.
The game's session read both fixes on a build at `3c7fe822`: the rod's largest drop 0.6% where 8.5%,
and its white room against a reference of eight bounces within 3% on the floor and 2% on the far
wall for a glowstone, where a reference of two bounces read 22 to 31% over, so that room is measured
at eight; both entries are off. And a new finding, item 9: colors written in place by
`UpdateMeshBuffer` reach the model pass and not the bounce or the reference, which keep a mesh's
colors from its last upload, so a lamp placed in a dark room bounces off walls as dark as they were.
The game's `cc888f33`, which says the limit in its README, is its own.

Before it, `3105e73e` came, the end rod's step found against the path-traced reference: the lamps'
faces are summed in closed form within four half diagonals of the middle and as a point each past
them, and at that switch, 1.9 blocks from the rod's middle, a face fifteen sixteenths long and two
wide read 11% over the closed form as one point, so the floor's light stepped 8% between 1.85 and
1.9 blocks and the log of its light bent by 0.062; a block's square face read 7% over at its own
switch, where the comment claimed 3%, the closed form itself matching an integral of the face taken
numerically. Each face past the closed form is cut along its longer side into pieces about as long
as its shorter, four at most, a face about square into quarters, each a point, the cuts unrolled
with the count kept so no loop nests in the faces' loop, which this driver's fault forbids: the rod
within 0.6% at its switch, a block's face 1.6%, a thin slab 0.2%, so neither the switch by the
longest half side nor a blend over a band was needed. The rod's floor reads a reference of 16,384
paths a pixel within 2% to 1.6 blocks, past which its reach fades it as made, and its log bends
nowhere over 0.021 from one block to three; a test holds both and failed on the old form at 0.062.
The rooms read within 0.001, the game's room seen from outside faulted in none of eight runs by Xid,
and the model pass compiles on lavapipe in 0.35 s where 0.32. Right, the cause measured at the
switch itself and the cheapest form that meets the reference taken, the two the part offered set
aside by the numbers, and the driver's shape kept in mind. The suite: 1,670 passed; on lavapipe 383
passed and 7 skipped with no validation error. The rooms' 0.30 and 0.36 want no retake, the game's
own reading with the desktop quiet having closed the cost. The game's `6772c6f0`, oak doors, and
`1454ba6b`, flowing water, are its own. Next the reference's vertex colors, then part f, part c and
item 8.

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
   5.2 waits (Decision 19); the run of `98a1edca`, the lamps, is green on Linux in eight minutes, on
   macOS and on the captures, and red on Windows after 87 minutes, the Rendering group ended at its
   40-minute limit with nothing finished and the leak test lost at app 26 with 10.6 seconds a frame
   (Verdict 48), its examples job skipped; the run of the game's `9e9074dc` reads the same, Windows
   red in 87 minutes with 364 without a result and a frame of 11.9 seconds, its Linux job green in
   15 minutes where 8 before, macOS and the captures green, the examples job skipped; the drive
   script says since `e50fd247` whether a game's process lives when a command finds no session, and
   its exit code and Windows' crash event when it ended. The page showed the leak test's progress
   line as its first of 198, `app 2`, where the line named its app all along; the page shows the
   last of a repeated line since `88673244`. Verdicts 40 and 44 are carried out there, the leak test
   following four apps through every plugin's making, a failing frame written where the job uploads
   it with each surface's means, and four references drawn again after a drift of up to 2.16% on the
   RTX 4070 itself; the runs since read the references green and the leak test red. The examples job
   runs when Windows and macOS are both green in one run, which they are since `817f7fd4`. When
   every job is green the owner is told, since 5.2 is due (Decision 19). With the batch that next
   touches `build/test.py`, it takes from BevyCSharp's `1f68fde8` the two cases of a theory whose
   names are cut to the same as one counted apart, which its page reads as one today (SHARED.md),
   and runs the game's `3DEngine.Game.Tests`, 22 tests of its light, meshes, body, saves and
   generation that need no window, beside its own through its `--project`, counted on the page apart
   from the engine's and reddening the run as the engine's do (ASKS.md). The engine's own despawn of
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
   The owner looked again at 17:20 on the tree: the bounce stable in motion, the lamp's lobes gone
   at a cell of 0.125, and a lamp's light at a distance aliased at its border and gone farther off,
   so b closes with its commit, d gains its lever and e is added. At 23:40 the owner looked at
   `98a1edca` and found the bounce awesome and the screen's probes its one unsteady part, which is
   part f. The apparatus first: `BounceReference` traces a mesh's flat color times its texture's
   average and not the vertex colors the field and the model pass read, so the game's rooms of white
   concrete, one white material with each block's color in its vertices, read a third brighter in
   the reference than in the frame and the comparison cannot tell the bounce's error from the
   color's (ASKS.md, 2026-10-11); the reference takes a hit's vertex colors since `3c7fe822`,
   blended by the hit's barycentrics as the model pass blends them, a colored slab within 1% where
   0.58 apart, with a test. The order of work: c, then f's two left things, then item 9 and item 8,
   the others being done; the game's session is asked to measure d at both cells and e along the
   distance, in ASKS.md as before.

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
   it. The cost is brought down at `0f5cdbcf`, read by its parts in a room built as the game's: the
   pixel reads the lights its probes mark alone, the probes are weighed once, and a probe marks the
   lights at an eighth, the pass's share 0.25 ms where 0.47 and the bounce's 0.68 where 0.89 in the
   engine's room, two cheaper splits refused by the reference. The game's own reading on `0f5cdbcf`
   in its room, which it draws as section meshes of sixteen blocks with vertex colors, the GPU held
   at one clock: the pass gains 0.47 to 0.53 ms where it gained 0.67, and the bounce 0.75 to 0.85,
   twice the engine's room in the pass, read against each other at `0defd508`: the difference is the
   layout and the eye, the game's lamps two blocks apart under a low eye so its floor's pixels lie
   nearer more lamps, and on `214feb11`'s shaders the game's own room reads 0.30 ms of the pass in a
   copy of its build, the engine's built as the game's 0.36, and the game's own last reading with
   the desktop quiet, on a build carrying `0defd508`'s shaders with its eye a block higher than the
   engine's copy stood, 0.39 to 0.47 ms where 0.67 on `98a1edca`, the bounce 0.85, so the cost is
   done and the game's entry is off; the game's runs on the commits, counted by the kernel's Xids,
   lost no device. Found beside it: the land around the game's room costs the model pass 0.6 ms of
   its own, the pass discarding for an alpha cutoff and drawing no depth ahead of itself, so
   sections behind a wall drawn before it are shaded; the game's nearest-first order of its solid
   sections does not pay, 0.05 ms worse in the room and the same on the hills, since the depth test
   runs after the discard, so the remedy is the engine's, an opaque pipeline without the discard for
   materials that cut nothing out, or a depth pass ahead of the model pass, measured on the game's
   hills at eight columns, 1.42 ms against 0.83 at two, which the game's entry carries. And one more
   from the game on 2026-10-11: an end rod, a glowing box 2 by 15 by 2 sixteenths of a block, lights
   the floor with a drop of 7 to 9% from one pixel to the next along a closed curve 2.3 to 2.7
   blocks from its axis, nearest along the diagonals, at every quality and with the screen's probes,
   their filter and the history each off, black with the glow off, and for no glowstone, torch or
   lantern, where the path-traced reference shows no step. Read from `glowLightIrradiance`: a
   light's faces are summed in closed form within four half diagonals of its middle and as points
   past them, within 3% of the closed form for a block, where a face fifteen sixteenths long and two
   wide seen from the side at that distance is no point, so the two forms meet in a step; the switch
   goes by the longest half side, or the long face is cut into squares each a point, or the two
   forms are blended over a band, which the rod's floor against the reference named at `3105e73e`:
   the faces past the closed form cut into points about as long as they are wide, the rod within
   0.6% at the switch where it read 11% over, its floor within 2% of the reference and its log
   bending nowhere over 0.021, with a test; the game's entry stays until it has read it.

   **f. The screen's probes made the light depend on the view, done at `b1e86aef` but for two
   things.** Measured first: the view's share over the whole picture was a tenth of a percent and
   there was no second of noise or leak, the view living in the lamps' penumbra at the probes'
   footprint, a shadow's edge straying 60 to 90% between views turned by pixels. The remedy, the
   part's first: the probes march to the lamps from a point moved about their tiles in a round of
   sixteen frames, and the lamps' light is blended over the frames at each texel of the half-sized
   depth, reprojected by its surface and dropped where the surface, a lamp's place or the light's
   spread changed, the nearest lamp's falloff drawn again at each pixel; where no probe holds a
   pixel it marches to the faces' halves, so a shadow holds with the probes off. The strays 0.36 to
   0.89% where 3.1 to 7.9%, the line within 0.8% over a round, a lamp placed shown in its first
   frame, the cost within hundredths. Left: an app's first frames, before the field holds a wall,
   light a shadow's texels for some hundred frames, mended by the blend restarting where the field's
   cascade under the point was built this frame; and the probes' filter reads the frame before a
   pixel off, put right with the thin room's loss read for the filter's width. The owner's eye
   judges the look and the settling (Decision 27), the finer grid for the two brightest lamps the
   second way should it want more.

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
   go into the file with the next batch, the one `none` recorded as the page has it. Done at
   `1fa88e8b`, 212 shares in `3DEngine.Examples/measured-ci.tsv`.

6. **Three packages bumped, after 5.2 is packed (Decision 20).** Vortice.Vulkan 3.2.1 to 3.3.0,
   AssimpNetter 6.0.4 to 6.0.5 and StbImageSharp 2.30.15 to 2.30.16, in one commit with the suite
   run and the notices written again, on the owner's word typed into this session once the package
   is out, as AGENTS.md has it for a version; SDL3-CS stays on its preview and NVorbis on 0.10.5.

7. **A game is written meanwhile.** When the items above wait on a run or on the owner, the next
   game of `games/` is written, as the owner asked on 2026-10-07, a later game finding nothing new
   being the point of each.

8. **An opaque material's hidden fragments are rejected before they are lit.** The model pass
   discards a fragment for its alpha cutoff, so the GPU's early depth test is off for every material
   and a fragment behind a wall is lit before the depth test throws it away: in the game's room the
   land outside, all of it behind the walls, costs the pass 0.6 ms, 1.42 ms at eight columns against
   0.83 at two, and drawing the walls first saves nothing, 0.05 ms worse (ASKS.md, 2026-10-11).
   Either an opaque pipeline without the discard for materials whose alpha mode cuts nothing out,
   the masked and blended keeping theirs, so the early test rejects a hidden fragment before it is
   shaded, or a depth pass ahead of the model pass that every opaque draw writes, which costs a pass
   of its own and pays only where the shading is heavy; the first is tried first, since it adds no
   pass, and the second measured against it where the first leaves most of the cost. Measured by the
   model pass on the game's room at eight columns against two, with the desktop quiet, and on Manor
   and Wick, with the pipelines' count; a test holds the room's land to a share of the pass. Taken
   after item 2's parts f and c and before item 3's remainder.

9. **A color stream written in place reaches the bounce.** The field paints a mesh's vertex colors
   as it stamps it and the ray scene packs them into its corners, both keyed by the mesh's vertices,
   so colors written in place by `UpdateMeshBuffer`, which the model pass draws at once, leave the
   bounce and the reference on the colors of the mesh's last upload: in the game's white room relit
   by its light levels, the frame's floor read 0.099 and its far wall 0.035 for 2,100 frames, and
   0.130 and 0.072 once each section uploaded anew with the same blocks and light, the reference's
   wall 0.002 and 0.043 with whole sections black in its picture, so a lamp placed in a dark room
   bounces off walls as dark as they were until a block changes in their section (ASKS.md,
   2026-10-11). A write to a color stream marks the mesh's colors stale, and in the frame after the
   field repaints the colors of the cells it holds for the mesh without rebuilding their distances
   and the ray scene rewrites its corners' words, so a relight costs less than an upload, which
   would have the field build the mesh again. Measured on the game's room, the floor and the wall
   after a relight against the reference at eight bounces, and a relit section's cost against an
   upload's; a test writes a colored slab's colors in place and holds the bounce to the reference
   within the colored slab's 1%. Taken after item 2's parts f and c and before item 8, since a wrong
   bounce weighs more than its 0.6 ms.

## Verdicts

Verdicts 1 to 29, 32 to 39, 41 and 42 are settled, and their numbers are not given again.

30. **The soak fails on Manor, whose buffers climb from 499 to 573 in two minutes past a bound of
    527 while its memory holds at 523 to 532 MB.** The soak's error names the game and the measure
    since the examples job of `71050c8f`, as this verdict first asked when `22bbf15a`'s named no
    game, and the other seven games pass it. Read at `5e240044` by `memory.buffers`, the device's
    census by the file and line that made each buffer: the buffers that move are `MeshStore`'s, a
    vertex and an index buffer a mesh of the models the cells show, let go as cells go, the world's
    and no leak, and many because the spawner de-indexed a model again at every spawn, so each cell
    uploaded its own copy; a scene's mesh is shared by every entity spawned from it since, Manor's
    walk at 96 to 158 buffers where 385 to 668, and the soak bounds a game that streams its level by
    the most a whole walk holds, 162 for Manor, since on the runner the walk enters rooms the first
    half never reached. Settled when an examples job passes the soak, and 5.2 is due with it
    (Decision 19).

48. **The Windows job of `98a1edca`, the first with the lamps' shaders, runs 87 minutes and ends
    with 1,262 passed and 363 without a result: the Rendering group reaches its 40-minute limit with
    nothing finished, and the leak test is lost at app 26, a frame taking 10.6 seconds where it took
    1.2 to 2.6 the run before.** The run of `9e9074dc` after it reads the same, 87 minutes, 364
    without a result and a frame of 11.9 seconds, with `34f39fcb`'s march; its Linux lavapipe job
    took 15 minutes where `98a1edca`'s took 8 and the run before that 7, so lavapipe pays
    everywhere, doubled on Linux and eightfold on the Mesa the Windows job installs, an older LLVM
    or fewer threads, on what `98a1edca` added: `glow.slang` and the loops over 64 lights in the
    probes' passes and the model pass, which a compiler unrolling them turns into code many times
    the size, and each app of the leak test compiles anew. Read at `214feb11`: the seconds go to the
    compile of the model pass's fragment shader, 142 KB of SPIR-V before the lamps and 293 KB after,
    0.23 s to 0.90 s for each app on lavapipe, the glow code inlined at four places and unrolled in
    each, which every app paid whether it drew a lamp or not; the hit shaded once, one way through
    `glowLightsAt` and the glow loops kept from unrolling bring it to 177 KB and 0.32 s, the
    Rendering tests on lavapipe here within 8% of their time before the lamps. The passes compiled
    once a process and a lampless app spared them are kept for a Windows job that still runs long.
    Settled when a Windows job runs its tests whole in the time the run before `98a1edca` took,
    within a quarter, and 5.2 is due with it (Decision 19).

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

Item 2, part c. The light that bounced is read from the finest cascade holding a point and fades
into the next cascade's across its outer band, eight of its cells, a probes' spacing; the screen's
probes fade into the world's across the second cascade's band; and the last cascade's light fades
into the light from beyond the probes, the environment's and the ambient lights' unhidden, which the
model pass and a reflection's hit take in its place (`bouncedHold`). A surface's depth in a cascade
is its point's leaned off it as the probes read it, so a floor on a cascade's lower side lies half a
spacing inside rather than outside. The field's occlusion, the sun's contact shadows and the glow
lights' marches fade across the coarsest built cascade's band the same way (`fieldHold`); taken from
the last cascade whether built or not, the hold read none in a scene whose last was not built and a
glowing block's penumbra moved 16% with a turn, which the penumbra's test caught. Measured on a
canyon's floor under a sky with no sun, seen from twenty units up and twelve degrees down, the
largest change from one row of pixels to the next at 640 by 360: at High 15.1% at the last cascade's
end, 14.3% at the screen's probes' and 9.6% at the third cascade's, at Medium 17.7% and 17.4%, at
Low 26.0%, and with the fades 2.9%, 3.0% and 1.0%; `CascadeEdgeTests` holds it under 6% at Low and
High at 320 by 180, where the parent reads 25% and 15%. The cost 0.02 to 0.04 ms of the bounce in
`shaders_bounce_rooms` and 0.007 of the model pass. The game's seed 1 from 0.5, 100, 0.5 facing east
at hour 18.2 shows a forest and a coast under the fog in a build of `b1e86aef`, not the ask's grassy
hills, and its light over the light with the bounce off climbs smoothly from 0.35 at 36 blocks to
1.0 at 112 both before and after, so the game's session is asked to read its own view on this
commit. The two left in part f follow, then item 9 and item 8. The suite: 1,676 passed; on lavapipe
388 passed and 8 skipped with no validation error; the game's glowstone room lost no device in eight
runs.
