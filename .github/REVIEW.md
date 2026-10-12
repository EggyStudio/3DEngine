# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `6c9284a7`, the game's hostile mobs, and `314b9d48`, the game's commit following item
9, and `feec07a2`, item 9: colors written in place by `UpdateMeshBuffer` reach the bounce and the
reference in the frame after; the store keeps the meshes whose colors were written and the field
takes them each frame, a still one among them pooled again with its colors as they are, and in each
built cascade not built again that frame the paint pass runs over its triangles alone against the
words the cascade's last build kept, which each build copies aside, about a megabyte a cascade,
writing the colors the passes read so no distance is found again; the ray scene writes the new
colors into the mesh's corners, which its structure does not read. A test colors a slab white with a
white wall on it, writes it red in place, and reads the reference trace it red and the wall's red
over green at 1.230 in the frame against 1.240 in the reference, within 2%, where without the ray
scene's half the reference traces white and without the field's half the wall's bounce stays white;
the light guide and RENDERING.md say it. The suite 1,703; lavapipe 410 with 10 skipped and no
validation error; the glowstone room lost no device in eight runs. Right, the paint alone run again
from the words a build keeps, rather than the build, and both halves proven by leaving each out. The
Reply is read and removed, and the game's entry on colors written in place leaves ASKS.md with it,
the game's session asked for the item's measure in its room, the floor and wall after a relight
against the reference at eight bounces and a relit section's cost against an upload's. Item 9 is
done. The game followed at `314b9d48`, a commit of its own to its chunk renderer and README, its
relight reaching the bounce since, as the entry asked. The page at 07:51: `e424550c`'s run has every
test job green, Linux in 14 minutes, macOS in 13 and Windows in 29 m 56 s, six minutes past the
third build's 23 m 44 s with the longer screen rays and the suite's growth since, within the limit
and watched on `6f4c98bb`'s run, whose Linux and macOS tests are green and whose Windows tests run;
the captures and the games' steps run on both. BevyCSharp's `eb82688b` is green on all three. Item
8, measured at 08:00 before a commit: the opaque pipeline without the discard saves nothing on the
development machine's GPU, which tests depth early under a discard, the hidden land's 0.35 ms being
the geometry of 289 columns of sections inside the frustum, so the patch goes in for the devices
that gain and the cost is culling's, the game's section flood fill first, its item text saying so.
The page at 08:23: `e424550c`'s run is green on every job, its examples job in nine minutes, the
first whole green run with the longer screen rays; `6f4c98bb`'s Windows tests took 33 m 44 s on the
same engine code and tests where `e424550c`'s took 29 m 56 s, four minutes of the runner's own
variance on top of the six the suite grew by since the third build, within the limit and watched, a
verdict if a run passes 36; its other test jobs are green and the games' steps run. The game
committed its hostile mobs at `6c9284a7`, arrows and explosions with 140 lines of tests. Next item
24, the glow lights' penumbra, then item 8's commit.

Before it, `6f4c98bb` came, the game's commit opening the post-processing window from its Voxel
window, and `c54d64eb`, item 10: each effect over the frame is read back beside its setter, the
tonemap, the exposure and the one that follows the scene, the grading, the vignette, FXAA, the depth
of field, the motion blur and the bloom, those of several values a tuple named as the setter's
arguments, and `DrawPostProcessWindow()` shows every one in an ImGui window of its own, drawn on a
call from a game's frame as its other windows are, the curve a choice by name and the rest sliders
and checks under headers, each calling its setter at once; a test reads every effect as it stands
unless set and as each setter leaves it, its bounds kept, and draws the window in a headless frame,
which changes none; the cheat sheet, the materials guide's effects section and the upgrading guide's
added list say them. The suite 1,702; the guide's blocks build on the package. Right, the readings
beside the writings and the window a game calls rather than one the engine opens. The Reply is read
and removed; the game's session is asked to open the window from its Voxel window, and the owner's
eye judges the look. Item 10 is done. Part h at 07:10: the owner's two captures of the penumbra are
no longer in the tree, and pictures in the same place and quality need a fresh copy of the owner's
save, which the game's session alone makes, so it is asked for them at this head, still and 60 and
600 frames after a step, the seam traced row by row and the band's blotches measured as on
`64fd2f14`; the working session's own reading meanwhile, from the engine's room of the saw at 1280
by 720 from the owner's place, is no seam with teeth and a smooth falloff across the red wall, its
isolines bending at two heights where the gather's blend between the probes' layers changes slope,
which reads as faint horizontal bands, and the blotches, the light less its blur, at 0.44% of the
light still and 0.37% after a step, correlated 0.16 at 8 and 16 pixels, against the game's 0.65% and
0.42 on `64fd2f14`. The game's pictures came at 07:20, drawn at this head through a fresh copy of
the owner's save, deleted again after: the seam keeps its teeth, 17 pixels at the top and the foot
still and 11 and 26 after a step, as at `8118e2c7`, and the blotches are 0.34% of the light with no
repeat at 8 pixels across, as at `56db892b`; so the blotches are down by half since the owner's
pictures and the teeth stand where the engine's own room shows none, which makes the seam the game's
scene's, its cause to be read in the game's pictures row by row after item 9, and the pictures went
to the owner's eye at 07:21 with the working session's reading beside them, the panels in the engine
session's scratch folder: across the seam the frame steps 26% in 20 pixels where the reference rises
smoothly over 170, the frame 0.79 to 0.88 of the reference left of the seam and 0.84 to 0.98 right
of it, the seam's middle rows within 2 pixels of a line and the teeth at its top and foot 11 to 17
and 17 to 26 pixels, 18 and 21 at `8118e2c7`, so neither the filter's correction nor the longer
reach touched them, and they are the glow lights' march past the pillar's ends, as the game first
read them; the blotches 0.34% of the light still and after a step, settled by 60 frames, half of
`e477bc1e`'s 0.65% and like `56db892b`'s 0.30%, with no repeat on the 8 or 16 pixel tile grid where
there was one, what remains running long down the rows along the room's creases and the shadow's
own. The teeth are item 24, after item 9: the glow lights' shadow past a box's edge is a step where
a penumbra belongs. The owner pushed at 07:00 and again at 07:24: `e424550c`'s run has its Linux and
macOS tests green, 14 and 13 minutes, its Windows tests running since 07:02 and its captures drawing
on macOS, and `6f4c98bb`'s run is in flight; the Windows time, with the third build and the longer
screen rays, is read next. The game opened the post-processing window from its Voxel window at
`6f4c98bb`, item 10's last piece, and took the mobs' entry out of ASKS.md as done. Next item 9,
colors written in place reaching the bounce, which the working session has taken, then item 24, the
glow lights' penumbra, then item 8.

Before it, `e424550c` came, item 22's first part: the screen's probes' rays reach twice as far as
the world's probes are apart where a ray ends in the finest cascade, and as far as before past it;
the wall behind the pillar at x 7 reads 0.79 of the reference where 0.68 and 1.01 without the pillar
where 1.05, a gap of 22 points where 15 was asked, every other point within 13, the 27 cases summed
3.778 where 4.006 and the corridor 3 to 9% under where 17, the bounce rooms' bounds set again 15%
over the new reads. Doubled everywhere the reach let two leaks in, which the suite and lavapipe
caught: a ray that went behind the depth and then off the picture was handed to the field past the
wall it had gone behind and lit a closed room with the lamp outside it at 92 levels where 8 at most,
so such a ray is handed over from the last point it stood in front of the depth, which the
reflections' trace shares; and a closed room near the field's side behind the eye took 0.0037 of the
sky where 0.0007, 0.0015 with the reach bounded to the finest cascade, under the test's 0.002. The
strip loses nothing by its own measure, its per-pixel difference 0.22 where 0.27 to 0.29 and its
room 4% over at 800 by 450 where 10, its signed mean going under because its over-bright ceiling
came down; the view that loses is the carried lamp's, 6% under where 3, where the world's own rays
read 8 and 16% under the reference at two of three probes, mostly in red, so hits on the red wall
its lamp lights are shaded short and the longer rays take more of them, a thing for the second part.
The pass costs 0.05 to 0.07 ms more at High in three of the rooms' views, 0.06 to 0.14 in the
corridor and 0.08 to 0.15 in the glowstone room, each the least of eight readings beside another
session's use of the GPU, and RENDERING.md says the trade with these numbers. The suite 1,700, one
transient unknown error from the driver in a particles test that passed twice alone with no fault in
the kernel's log, lavapipe 410 with 9 skipped and no validation error, the glowstone room losing no
device in eight runs. Right, the leaks found by the suite and closed at their cause, the reach
bounded to where it helps, and the strip read by its own measure before it was called a loss. The
Reply is read and removed. The second part is held to 3.778, the sum since the leaks' remedies, and
its first candidate is the working session's: pass over a probe of cascade 1 whose ray met a surface
before its interval rather than read it dark, so for the rays past the pillar's edge the one such
probe whose ray reaches the north wall within its interval carries the light the reference has,
measured by the gap at x 7 and the sum, and read against the Cornell box's ceiling the dark reading
guards. The second part was read ray by ray at 06:50 and closed with nothing kept, the item's text
saying why: the gap at 22 is the probes' resolution around a one-unit gap, one ray a texel past a
thin edge, and a finer cell is the knob a game has for it. Item 22 is done. Next item 10, the
post-processing window, which the working session has taken, then h's blotches and teeth by the
owner's eye on the penumbra pictures against the head of today, item 9 and item 8.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do. This
list is long on purpose, and a batch that ends is followed by the next item with no wait for a
reply. In this order, which the owner set on 2026-10-09 and added to on 2026-10-11: the runs first,
then the bounce's quality, the leak the owner saw first within it, then item 10, the post-processing
window, then subsurface scattering and what follows it, and item 7 for a wait.

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
   its exit code and Windows' crash event when it ended. The run of `b1e86aef`, with `214feb11`'s
   mend, is green on Linux in 12 m 9 s, 1,633 passed and 15 skipped, on macOS in 34 minutes and on
   the macOS captures, 255 of 255 in 3,335 s, and on Windows whole in 68 minutes, 1,626 passed and
   22 skipped in 39 m 21 s of tests, the leak test's frame 3.8 s (Verdict 48), its digest green and
   its examples job begun at 10:42 (Verdict 30); the run of `e477bc1e` is green on Linux in 12 m 28
   s, 1,635 passed, 1,750 MB at most where 1,631, on macOS, on the macOS captures and on Windows
   whole in 69 minutes, its examples job red by the measure alone as well, two pairs over, the
   billboard and the depth writing, the two loading examples holding this time; `b1e86aef`'s
   examples job is red by the measure alone, four pairs over their recorded share, the soak and the
   package's steps skipped after it (Verdict 49), so Verdict 30 is unread. The page showed the leak
   test's progress line as its first of 198, `app 2`, where the line named its app all along; the
   page shows the last of a repeated line since `88673244`. Verdicts 40 and 44 are carried out
   there, the leak test following four apps through every plugin's making, a failing frame written
   where the job uploads it with each surface's means, and four references drawn again after a drift
   of up to 2.16% on the RTX 4070 itself; the runs since read the references green and the leak test
   red. The examples job runs when Windows and macOS are both green in one run, which they are since
   `817f7fd4`. When every job is green the owner is told, since 5.2 is due (Decision 19). With the
   batch that next touches `build/test.py`, it takes from BevyCSharp's `1f68fde8` the two cases of a
   theory whose names are cut to the same as one counted apart, which its page reads as one today
   (SHARED.md), and runs the game's `3DEngine.Game.Tests`, 22 tests of its light, meshes, body,
   saves and generation that need no window, beside its own through its `--project`, counted on the
   page apart from the engine's and reddening the run as the engine's do (ASKS.md). The engine's own
   despawn of what a state scopes is read against a soak of the world's entity indices across many
   transitions, which in BevyCSharp found Bevy 0.20.0 losing every index it despawned that way
   (SHARED.md), with the batch that next touches states. The owner pushed everything through
   `ffa3ec48` by 18:33; the runs of `aa9fe773` and `e28064f7` are green on Linux in 17 and 13
   minutes and red on Windows after 87 minutes, the Rendering group and the suite at their 40-minute
   limit with 377 tests without a result and the leak test's frame at 4.3 s (Verdict 48), and on
   `e28064f7` the leak test's resident memory 92 MB past its bound (Verdict 51); the macOS job of
   `aa9fe773` is red by its first game's step, the WebP script wanting ImageMagick the runner lacks
   (Verdict 52), its tests green, and `e28064f7`'s macOS job green; the macOS captures of `aa9fe773`
   green; the runs of `ea9a23cd` and `ffa3ec48` wait in the queue. Each push's run is read by the
   reviewing session, and a failure it names comes first here.
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
   part f. At 10:05 on 2026-10-11 the owner, standing in a cave of the voxel game, saw the sky's
   light enter it by the heading alone, which is part g and comes first (Decision 28). At 10:55 the
   owner looked at the tree with g's mend and found it right at every heading and tier but one
   window at High, and found the indirect light noisy and spotted where a penumbra is wide, which is
   part h. The apparatus first: `BounceReference` traces a mesh's flat color times its texture's
   average and not the vertex colors the field and the model pass read, so the game's rooms of white
   concrete, one white material with each block's color in its vertices, read a third brighter in
   the reference than in the frame and the comparison cannot tell the bounce's error from the
   color's (ASKS.md, 2026-10-11); the reference takes a hit's vertex colors since `3c7fe822`,
   blended by the hit's barycentrics as the model pass blends them, a colored slab within 1% where
   0.58 apart, with a test. The order of work: h, with Verdict 49's steps that run whether the
   measure holds, then Verdict 49's pairs, Verdict 48's two remedies, f's second left thing, then
   item 10, item 9 and item 8, the others being done; the game's session measured g and h in the
   game and wrote them in ASKS.md, as it measured d at both cells and e along the distance, and
   reads g on `64fd2f14`.

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

   **c. The border between near and far was the field's last cascade's end, done at `e477bc1e` and
   its one left thing at `64fd2f14`.** Measured by the game's session on the hills at sunset with
   the sun under the horizon, the bounce at High over the light with the bounce off, which takes the
   environment map's: 0.56 to 0.80 of it from 60 to 96 blocks and 1.00 from 122 on, the step between
   96 and 102 blocks where `field.state` ends the last cascade; doubling the cell moves the step
   past the 148 blocks drawn, and the shadows taken to 200 leave it where it was, so the line is the
   field's and not the shadows'; by day the bounce and the map differ under 8% and no step shows.
   Past the last cascade a surface takes the map's unoccluded light, up to two thirds brighter than
   the bounce inside at a low sun, and nothing blends the one into the other. The near is blended
   into the far across a band at the last cascade's edge, the probes' light fading to the map's over
   the outer part of the cascade's reach as the light's cascades are merged into one another, and
   the field's distances the same where a pass reads them to the edge; measured by the luminance
   along the view's middle rows across the edge at sunset, and a test holds the step across it under
   a bound. Done at `e477bc1e`, wider than the measure asked by the measure's own account: every
   cascade's light fades into the next across its outer band, eight of its cells, the screen's
   probes' into the world's across the second's, and the last's into the light from beyond the
   probes, which the model pass and a reflection's hit take in its place, the field's occlusion, the
   sun's contact shadows and the glow lights' marches fading across the coarsest built cascade's
   band the same way; a canyon's floor seen as the game's hills were stepped from one row of pixels
   to the next by 15.1% at the last cascade's end, 14.3% at the screen's probes' and 9.6% at the
   third's at High, 17.7% and 17.4% at Medium and 26.0% at Low, and reads 2.9%, 3.0% and 1.0% with
   the fades, `CascadeEdgeTests` holding it under 6% at 320 by 180; 0.02 to 0.04 ms of the bounce.
   Left: the bounce's hold, the screen's hold and the second cascade read beside the finest take
   their cascade's place whether built or not, where the field's hold reads the coarsest built since
   the penumbra's test caught the last unbuilt in its scene, and an unbuilt last leaves the frame
   the environment's light where the bounce was; the three read as the field's hold reads, the
   second read skipped where its cascade is unbuilt, with a test of the bounce in that scene, before
   f's two left things. The game's session holds it on a flat world at sunset, the ask's view hiding
   the distance behind a hill since: the largest step from one row to the next 0.054 where 0.141,
   the line a band sixteen blocks wide; its entry is off. The left thing is done at `64fd2f14`: the
   three holds take the coarsest built cascade as the field's does, the second read skipped where
   its cascade is unbuilt, a closed room under an ambient light reading 0.54, 0.54 and 1.46 times
   its settled light in the three frames before its last cascade is built where 9.4, held under
   twice.

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

   **f. The screen's probes made the light depend on the view, done at `b1e86aef` but for the
   filter's offset.** Measured first: the view's share over the whole picture was a tenth of a
   percent and there was no second of noise or leak, the view living in the lamps' penumbra at the
   probes' footprint, a shadow's edge straying 60 to 90% between views turned by pixels. The remedy,
   the part's first: the probes march to the lamps from a point moved about their tiles in a round
   of sixteen frames, and the lamps' light is blended over the frames at each texel of the
   half-sized depth, reprojected by its surface and dropped where the surface, a lamp's place or the
   light's spread changed, the nearest lamp's falloff drawn again at each pixel; where no probe
   holds a pixel it marches to the faces' halves, so a shadow holds with the probes off. The strays
   0.36 to 0.89% where 3.1 to 7.9%, the line within 0.8% over a round, a lamp placed shown in its
   first frame, the cost within hundredths. Left: an app's first frames, before the field holds a
   wall, light a shadow's texels for some hundred frames, mended by the blend restarting where the
   field's cascade under the point was built this frame; and the probes' filter reads the frame
   before a pixel off, put right with the thin room's loss read for the filter's width. The owner's
   eye judges the look and the settling (Decision 27), the finer grid for the two brightest lamps
   the second way should it want more. The first left thing is done at `64fd2f14`: a blend younger
   than a round is dropped where the field's cascade under the texel was built again within the box
   around the meshes that came or went, the penumbra 0.5% from where it settles two rounds after an
   app's start where 6.3%, held under 2%; the filter's offset follows Verdict 48's remedies.

   **g. The sky's light leaked into a cave by the heading, done at `64fd2f14` but for two limits.**
   The owner, standing in a cave of the voxel game at 10:05 on 2026-10-11 and turning in place, saw
   the sky's light enter where none should: between headings of 258 and 282 degrees the cave is lit
   bright, the two faces of a block seen diagonally lit with it; between 219 and 322 the face turned
   to the right is lit a little and between 314 and 46 the face turned away a little, the faces
   named from a heading of 0 looking at a block to the front left, whose two seen faces they are; at
   the other headings the cave is dark. A leak that comes and goes with the heading alone, nothing
   in the world moving, is the view's. Measured by the game's session on 2026-10-11 in a closed room
   of stone on a flat world at hour 10, no opening and no lamp, the eye turned through 360 degrees
   in steps of six and each shot's mean luminance read (ASKS.md): on `e477bc1e` the headings 324 to
   36 and 234 to 306 read 14.4 to 16.3 levels with a patch at 82 and the others 0, the wall ahead
   and its block's faces turned toward the eye sky-blue and the floor black, the north wall going
   dark at heading 42 while half in view; the same with the screen's probes off and with their
   history off; 2.2 to 2.5 as played, with the light levels on; and 0 at every heading on
   `b1e86aef`, so part c's fade brought it and the screen's probes did not. Read from the code
   against those numbers: the cascades stand ahead of the eye, their middle 0.35 of their 64 cells
   along the view and snapped to 8 cells, so the eye stands 1.6 to 9.6 cells from every cascade's
   back side by the heading, where the hold's band is 8 cells, and the hold leans a surface's point
   along its normal by half a probe spacing, 4 cells more, so a wall facing the eye a few blocks off
   has its point in the last cascade's back band or past it, 3 to 19 units for a cell of 2, and
   takes the sky's unhidden light by up to all of it, while the floor's point leans up and stays
   inside, which is the picture seen, the snap turning which walls fall in the band with the
   heading. Before part c that wall took the last cascade's probes, which held it to a cell of its
   side. The mend is measured before it is chosen: the depth read at the surface and not at the
   leaned point; and the band kept from the sides the eye stands near, by the cascades placed so the
   eye stands a band and the snap from every side, which shortens the reach ahead, or by the fade
   past the last cascade going to the probes' light at its edge with the sky entering only by the
   share of it the probes see, as the specular's `skySeen` reads, rather than to the sky whole; each
   read against the numbers above and against the canyon's step and the sunset border of part c,
   which keep theirs. A test turns the eye through the closed room and holds the walls' light under
   a bound of the sky's at every heading. Measured by the engine's session in a stone room of its
   own, the walls read 0.011 to 0.015 at the lit headings on `e477bc1e` and 0.00003 on `b1e86aef`,
   gone with the sky off and with the last band read as a step, unchanged with the screen's probes
   or their history off, so the band across the side behind the eye is the source; of two mends
   measured, a lead of 0.25 that costs 13 blocks of reach ahead and the eye written into the field's
   info with every band ending six cells short of the eye on a side it stands near, the second is
   taken, dark at every heading with the canyon's step unchanged. The owner's eye at 10:55 on the
   tree with it: dark at every heading and every tier but High between 259 and 281 degrees, where
   the face turned to the right still takes the sky, so the remainder is measured at High alone in
   steps of a degree about 270 in the game's room and the engine's, High having four cascades of
   probes where Medium has three and Low two, and whatever band or read the fourth adds that the
   mend's six cells do not cover is found by the number and mended, the test turning the eye at High
   as well as Low. Done at `64fd2f14`: every band ends six cells short of the eye on a side it
   stands near or is none, the eye carried in the field's info, the hold read at the surface where
   that holds more than its leaned point, which was the owner's remainder at High, a pillar's face
   leaned past the side 2.2 cells behind the eye, and a probe whose rays meet more faces from behind
   than in front passed over, which darkened the probes deep in the rock the owner's capture showed;
   the engine's room under 0.0004 of the open floor's light at every heading where 0.11 to 0.14, the
   owner's tunnel 0.00 where 52.2 levels, the game's room 0.00 to 0.01 from 250 to 290 degrees at
   every quality, the canyon's step unchanged, with tests that turn the eye and place it by the
   side. Two limits left, measured and written in RENDERING.md: a probe's ray leaving the field
   through the side behind the eye takes the sky, a cave of 30 by 30 blocks under a hill reading 1.7
   levels at High, which a lead of 0.25 takes to 0.02 at some 13 blocks of reach, and at Low a cave
   wider than its last cascade's 16 units takes the sky at every heading, 27 levels; both after
   Verdict 49, by the measure. The game's session holds it on the commit: 0.0 at every quality from
   250 to 288 degrees and through the whole turn, the brightest patch 3 levels at most, where
   `e477bc1e` read 14.7 to 18.7 at High and 2.6 to 4.6 at Medium; its entry is off. The owner's eye
   judges the look (Decision 27).

   **h. The indirect light is noisy and spotted where a penumbra is wide, its first part done at
   `56db892b`.** The owner, at 10:55 on 2026-10-11 on the tree with g's mend, saw noise and spots of
   light across the surfaces where the light's penumbra is wide, a lamp far from what shadows it or
   a bounce arriving from part of the room, where the sharp shadows and the open floor read clean.
   Measured by the game's session on `e477bc1e` in a white room at hour 23, three glowstones in a
   row under the ceiling and a block floating two under the middle one, so the floor three below
   lies wholly in penumbra, read against a reference of 4,096 paths and eight bounces (ASKS.md): the
   floor under the block reads 0.76 of the reference and 0.78 at the penumbra's edge against 1.06
   beside it, the reference showing no shadow, the floor under the block a tenth brighter than
   beside it for its nearness to the lamps, where the frame draws a shadow with an edge a fifth
   darker than beside it; and after a step of one block a spot of light stands at the shadow's edge,
   5.5 levels over the ring around it from the first frame to the six hundredth, none there before
   the step, 8.2 to 8.6 levels with the history off, where the edge breaks into the probes'
   eight-pixel steps and the frames flicker by 0.1 to 0.2 levels, and no spot with the screen's
   probes off, where the shadow's edges show twice. So two things: the lamps' soft shadow is too
   dark for a lamp wider than what blocks it, the march's cone through the field keeping a penumbra
   the reference does not have, measured against the reference by the block's size and the lamp's
   row, and the screen's probes give a wrong sample at the shadow's edge after a move which the
   texels' blend then holds for six hundred frames, the drop conditions of part f not naming it, so
   a sample that disagrees with the frame's own march, or with its neighbors past a bound, is
   dropped rather than held, and with the history off the probes' eight-pixel steps show, which f's
   blend was to hide. Read from the engine as it stands, what samples the light there: a screen
   probe's sixteen rays, of which a wide penumbra has some hit and some miss, blended by the five by
   five filter and the frame before with its hold, and the lamps' penumbra marched from a point
   moved about the tile over sixteen frames and blended at each half-sized texel, which converges
   over a round where the surface stays and is dropped where it moved or a lamp changed; a spot is a
   texel or a tile whose sample disagrees with its neighbors' and is not yet averaged with them.
   Measured first, in the game's room and an engine room with a lamp whose shadow is wide and a
   bounce from a lit wall: the pixel-to-pixel difference across the penumbra over the reference,
   still and after a move, with the glow lights off to tell the lamps' penumbra from the bounce's,
   with the screen's probes' history off and with part f's moving point held still, so the share of
   each source is a number. Then the remedy by the number: where the surface is still, a longer
   blend, more frames of the round kept than a thirty-second; where the neighbors disagree, a filter
   that widens by their disagreement, weighed by depth and normal as the probes' filter weighs, so a
   lone bright sample is spread and not shown; and where a move drops the history, the frames after
   it blended from the probes around rather than the one, each read against the reference and the
   frame's cost, part f's bounds kept. A test holds the penumbra's pixel-to-pixel difference under a
   bound still and after a move. The owner's two captures of 13:50, in the repository's root and in
   no commit: `Penumbra_MergingZigZag.png`, a lamp's red light on a wall with a shadow's line drawn
   as a saw, a zigzag with teeth some pixels long where the line should be straight, which the owner
   reads as the merge's; and `Penumbra_NoisyFalloff.png`, a lit wall whose penumbra falls off in
   blotches of light and dark the size of the probes' tiles across the band, where the lit part and
   the dark part read smooth. Both are measured with the rest of h: the teeth's length and the
   blotches' size against the probes' tile of eight pixels and the half-sized texel of two, and
   against the sixteen levels a probe keeps a lamp's penumbra in, since a straight edge marched from
   a point moved about each tile in Bayer's order lands a tile's offset apart from one tile to the
   next, which is a saw with the tile's period, and four probes' shares weighed into a texel from
   marches made from four places give the band its patches; the remedy by the number, a texel's
   share taken by where each probe's march of that frame stood rather than averaged across the tile,
   or the brightest lamps' penumbra marched at the texel itself near the eye, as part f's second way
   offered, read against the reference and the cost. A test holds a straight shadow's edge within a
   pixel of a line and the band's pixel-to-pixel difference under a bound. The game's session
   measured both on `64fd2f14` (ASKS.md): a glowstone's shadow edge thrown by a column on a wall
   seen head-on, traced per row and fitted to a line, lies 1.8 pixels from it still and 1.5 after a
   step, 2.0 and 1.3 with the history off, a slow bow of 6 to 8 pixels' range and no teeth either
   way, and a torch's stairs at its reach are the game's own light levels, so a lamp's edge does not
   give the owner's saw; the wide penumbra's blotches, the light less its blur, are 0.65% of the
   light with the history on and 0.85% off, where the reference's noise is 6.5% and a pixel wide,
   and their correlation holds at 0.42 at 8 and 16 pixels, so they repeat on the tile's grid, and
   with the history off the band's edge breaks into beads every 19 pixels. The reviewing session
   read the saw as the probes' merge, an emitter too large for a box light lit through the cascades
   alone, and the game's session measured that too: a wall of magma six by six with a column four
   blocks before it throws a shadow 0.7 to 0.9 pixels from a line still and after a step, no teeth,
   and with the glow lights off the room is black, so a wall of emitting blocks is carried as box
   lights alone, each block its own and touching ones merged, and the cascades carry none of it; the
   reading was wrong for a wall of blocks, and the saw stands unreproduced in three setups. It is
   found in the owner's save, copied by the game's session as `owner-zigzag-copy`: an underground
   room with its floor at 54 and ceiling at 59, a red concrete wall at z -33, four glowstones at x 4
   and a white concrete pillar from floor to ceiling at x 6, whose shadow falls on the red wall as
   an upright seam near x 8, seen from 9.5, 55, -35.4 at heading 185 and pitch -3, which matches the
   owner's picture. Read on `64fd2f14`, the seam traced per row: its middle rows lie within 1.05
   pixels of its median, and teeth of 3 rows and more stand out at its top, rows 70 to 180, by up to
   34 pixels, and at its foot, rows 520 to 650, by up to 23, to either side by turns; 31 and 17
   after a step, 42 and 31 with the history off, 22 and 36 with the screen's probes off, 42 and 23
   with the game's light levels off, and the room black with the glow lights off, so the teeth are
   the glow lights' march past the pillar's ends, where it meets the floor and the ceiling, and not
   the screen's probes or the merge; the engine reads it there with the copy, `--world
   owner-zigzag-copy`, and the march's cone at the pillar's ends against the reference, before the
   mend. The shadow's depth is done at `56db892b`: two faults of the lamps' march, a joined row's
   level face halved along its short side on a tie so both targets lay behind the block, and a march
   inside a block's side stepped out along the floor's normal through the whole block, mended by
   halving the longer side on a tie and stepping across the march as well, the floor under the block
   0.980 of the reference where 0.742, the floor's mean stray 0.030 where 0.079, with a test. The
   game's session holds it on the commit in its room: 0.95 of the reference under the block where
   0.76, 1.01 at the edge where 0.78, 1.03 beside and 0.98 in front, and the spot after a step is
   gone with it, 1.6 levels over its ring before and after the step where 5.5, the frames after the
   step still, so the spot was the black row's edge held. The game read the owner's seam and the
   blotches on the same commit: the seam's teeth reach 13 and 9 pixels where 34 and 23, faint spikes
   left at the foot with the screen's probes off, the lit side over the dark 1.40 where the
   reference reads 1.27 and 1.81 before, the blotches 0.30% and 0.36% of the light with the history
   on and off where 0.65% and 0.85% with the tile's repeat gone, and the red wall 1.2 to 1.3 of the
   reference on both sides of the seam, which is a fourth thing of h. The wall's excess is measured
   with the bounce left out against the lamps' light alone: 1.01 beside the lamps, 1.33 a third
   across and 1.61 behind the pillar on `56db892b`, 1.18 on `64fd2f14`, the march passing 0.58 of
   the lamps' light where the reference passes 0.44, so the march's soft shadow is too light across
   a wide penumbra and `56db892b`'s halving along the longer side made it lighter there; the march's
   estimate of what passes an edge is read next against the share of the face seen, the tie left as
   it is. The wall is done at `8118e2c7`: each half of a light's face weighed by the face's area
   rather than the box's half size, the owner's wall 0.90 to 0.98 of its reference where 1.20 to
   1.27 and the engine room within 0.03, with a test of five points, the seam's middle 1.7 pixels
   off a line where 8.9. The dark side read so far in an engine room of the pillar: 0.84 to 0.97 of
   the reference with the pillar and 1.00 to 1.03 without, the lamps alone 0.90 to 1.07 and one
   bounce against two 0.86 to 0.99, the short growing bounce by bounce where the pillar stands, the
   same with the screen's probes off. Read further with the bounce alone, the whole reference less
   the direct one: with the pillar the bounce reads 0.70 to 0.95 of the reference's bounce along the
   wall, where the bounce is 45 to 86% of the light, and without it 1.01 to 1.07, unchanged by the
   screen's probes off, by a ray's hits marched to the faces' halves, by the walled-in finding off
   and by the probes at half the spacing, 0.78 to 0.92 with the pillar at a cell of 0.125, and
   already 0.86 to 0.99 at one bounce against two, so the short begins at the first bounce and
   grows: the world probes' bounce around a small, brightly lit occluder is under-gathered, which is
   the rooms' general bounce error rather than a penumbra fault, and it becomes a read of its own
   after Verdict 48's two remedies, which cost every push. Left, on `623bdc47`: the blotches' rest
   on the tiles' grid at 0.30% and the seam's teeth of about ten pixels, the owner's eye on them
   after the pillar's read, and the wall's dark side at 0.80 of the reference, which the game read
   on the commit with the lit side at 0.96 and lit over dark 1.51 where the reference reads 1.27,
   and the seam's rise for the owner's eye; the game's entry is off, its numbers here, and the copy
   of the owner's save is let go, made again on request. The owner's eye judges the look (Decision
   27).

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

8. **An opaque material's hidden fragments are rejected before they are lit; measured, the hidden
   land costs geometry, not fragments.** The model pass discards a fragment for its alpha cutoff, so
   the GPU's early depth test is off for every material and a fragment behind a wall is lit before
   the depth test throws it away: in the game's room the land outside, all of it behind the walls,
   costs the pass 0.6 ms, 1.42 ms at eight columns against 0.83 at two, and drawing the walls first
   saves nothing, 0.05 ms worse (ASKS.md, 2026-10-11). Either an opaque pipeline without the discard
   for materials whose alpha mode cuts nothing out, the masked and blended keeping theirs, so the
   early test rejects a hidden fragment before it is shaded, or a depth pass ahead of the model pass
   that every opaque draw writes, which costs a pass of its own and pays only where the shading is
   heavy; the first is tried first, since it adds no pass, and the second measured against it where
   the first leaves most of the cost. Measured by the model pass on the game's room at eight columns
   against two, with the desktop quiet, and on Manor and Wick, with the pipelines' count; a test
   holds the room's land to a share of the pass. Taken after item 2's parts f and c and before item
   3's remainder. Measured at 08:00 before a commit, the first way built, a specialization constant
   the alpha test reads, set by the pipelines of batches whose material is opaque, the batches split
   by it and the discard folded away, read on the entry's room on a flat world by the model pass's
   time, the least of three readings after 400 frames, old and new in turns twice with the desktop
   at nothing: with the glow off 1.145 and 1.161 ms at eight columns against 1.149 and 1.129, 0.80
   at two either way, and with it on 2.44 and 2.49 against 2.34 and 2.43; the hidden land's share is
   0.34 to 0.42 ms in every case, the glow lights' march per fragment on or off, so the old build
   rejected those fragments before shading them already, the development machine's GPU testing depth
   early under a discard and deferring the write alone, which is also why drawing the walls first
   saved nothing. The cost is the vertex and raster work of 289 columns of sections against 25, all
   inside the frustum, which a depth pass would add a pass to and not remove. The patch goes in all
   the same, 257 lines, since a device that turns the early test off under a discard, lavapipe and
   the tilers among them, gains what this one does not, measured on the macOS job's profile where it
   prints one and said in RENDERING.md with the reason; and what removes the cost is culling the
   sections the eye cannot see, the game's own flood fill over its sections through their open
   faces, as Minecraft's visibility graph does, which is the game's to build on what the engine has,
   said under its entry, and an occlusion culling in the engine from the frame before's depth, left
   until the game's culling is measured and falls short.

9. **A color stream written in place reaches the bounce, done at `feec07a2`.** The field paints a
   mesh's vertex colors as it stamps it and the ray scene packs them into its corners, both keyed by
   the mesh's vertices, so colors written in place by `UpdateMeshBuffer`, which the model pass draws
   at once, leave the bounce and the reference on the colors of the mesh's last upload: in the
   game's white room relit by its light levels, the frame's floor read 0.099 and its far wall 0.035
   for 2,100 frames, and 0.130 and 0.072 once each section uploaded anew with the same blocks and
   light, the reference's wall 0.002 and 0.043 with whole sections black in its picture, so a lamp
   placed in a dark room bounces off walls as dark as they were until a block changes in their
   section (ASKS.md, 2026-10-11). A write to a color stream marks the mesh's colors stale, and in
   the frame after the field repaints the colors of the cells it holds for the mesh without
   rebuilding their distances and the ray scene rewrites its corners' values, so a relight costs
   less than an upload, which would have the field build the mesh again. Measured on the game's
   room, the floor and the wall after a relight against the reference at eight bounces, and a relit
   section's cost against an upload's; a test writes a colored slab's colors in place and holds the
   bounce to the reference within the colored slab's 1%. Taken after item 2's parts f and c and
   before item 8, since a wrong bounce weighs more than its 0.6 ms. Done at `feec07a2`: the
   recolored meshes repainted in the built cascades from the words each build keeps, the ray scene's
   corners rewritten, the red bounce within 2% of the reference.

10. **A post-processing window in the overlay, which a game opens as it opens its performance window
    (Decision 28), done at `c54d64eb`.** The owner asked on 2026-10-11 for a tab of the ImGui
    overlay that opens a window of the tonemapping and coloring settings, as the voxel game's Voxel
    window opens its profile window. The engine's post-processing is set and not read:
    `SetExposure`, `SetAutoExposure`, `SetTonemap`, `SetColorGrading`, `SetVignette`, `SetFxaa`,
    `SetDepthOfField`, `SetMotionBlur` and `SetBloom` write `FrameEffects`, which is internal, so a
    game cannot show the values it has. The engine gains a reading of each setting beside its setter
    and a window of its own, drawn on a call from a game's ImGui frame as its other windows are,
    that shows and sets every one: the tonemapper by name from the `Tonemap` choices (Decision 17),
    the exposure and the automatic exposure's bounds and speed, the contrast, the saturation and the
    tint, the vignette and its radius, FXAA, the depth of field's distance, range and blur, the
    motion blur and its per-object kind, and the bloom's intensity and threshold, each a slider, a
    check or a choice that writes its setter at once, so the owner tunes the picture while looking
    at it. A test reads each setting back after its setter and draws the window once in a headless
    frame. The game's session is asked to open the window from its Voxel window as it opens the
    profile window, once the engine has it. Taken after item 2's parts g, c and f and before item 9,
    since the owner asked for it and it is small; the owner's eye judges the look (Decision 27).
    Done at `c54d64eb`: nine readings beside their setters and the window, with its test.

11. **The README's examples as one scrolling row a folder, the games' text under them gone, done at
    `706a1499`'s look, `4805676c` reverted at `aa9fe773`, the games' captions at `e28064f7`
    (Decision 29).** The owner asked on 2026-10-11 at 15:30. The README's Examples section holds a
    table of two columns and thirty-four rows, pictures of picked examples and games mixed, and
    under it eighty lines of prose and a block that explain the games. It becomes one table a folder
    of `3DEngine.Examples`, in the folders' order as the repository lists them, Audio, Benchmarks,
    Core, Ecs, Gui, Models, Physics, Scenes, Shaders, Shapes, Text and Textures, each a single row
    holding every example of the folder that has a capture under `.github/assets/examples`, the
    picture linking the program and the run name under it as the rows do today, with a heading line
    naming the folder before each, and a last table of the same shape for `games`; a single row
    wider than the page scrolls sideways on GitHub, which is the point, so no row is wrapped. The
    prose and the block between the table and the Guide heading that explain the games go, their
    facts living in the games' own READMEs and in the games table of EXAMPLES.md where they are not
    already. The README's walk and the examples' table check read the file as before, so each is run
    on the result, and the captures' links keep their raw form. Taken as a batch of its own after
    h's next commit, since it is small and the owner asked; The owner asked at 15:52 that the
    pictures be one size, where they differ today: every picture in the rows carries the same width
    and height on its tag, and the captures that are not 800 by 450, sixteen of 268, among them a
    square and a four by three, are taken again at that size where the program's window allows or
    padded to it by the capture script, so the tag distorts nothing; the examples' table check and
    the captures' comparison read the result. Done at `ee47849e`: twelve rows of 257 examples and a
    games row of thirteen, every tag 400 by 225, the sixteen captures of other sizes fitted to 800
    by 450 by the WebP script, the games' notes moved into each game's own README. The owner looked
    at the pushed page at 16:04 and the pictures still read as different sizes. GitHub renders every
    tag at one box, its own style giving each the full width of its cell, an automatic height and
    the tag's aspect, so the box is uniform; what differs is the picture inside it, since a capture
    of another shape was fitted inside sixteen by nine with black bars, which read as a smaller
    picture. The fit fills instead: the WebP script scales a capture of another shape so its shorter
    side fills the box and cuts the rest around the middle, or the example's window is opened at
    sixteen by nine where the cut would lose something that matters, so every picture fills its box
    edge to edge; the sixteen captures and the games' thirteen are fitted again so, and the page is
    read after the push. Done at `60f2999d`: the WebP script fills its size and cuts around the
    middle or from a named side, all 268 captures at 800 by 450 and filling their boxes. The owner
    looked again at 16:12: each picture is as wide as the name under it, a short name's tiny and a
    long name's large. GitHub squeezes a table to the page's width before it lets it scroll, and a
    cell shrinks to the one thing in it that cannot wrap, the name, the picture shrinking with it
    under the page's own rule of a picture no wider than its cell. Each cell takes a width of its
    own, `width="400"` on every `td`, which the page's sanitizer keeps as it keeps a picture's, so
    no column goes below it and the row keeps its full width and scrolls; a header row of
    non-breaking spaces would do the same and is the fallback should the attribute be stripped,
    which the rendered page says after the push. Done at `623bdc47`: every cell 400 wide in an HTML
    table a folder, the rendered page read after the push. The owner looked at the pushed page at
    16:20 and the pictures still differ. The rendered page keeps the cell's width, read here, so the
    squeeze is the browser's table layout: a cell's width is a preference, and when a table is wider
    than the page the browser shrinks each cell to its narrowest unbreakable content, the name, the
    picture with it, GitHub's table being a block capped at the page's width that scrolls only past
    that minimum. So the minimum comes from content: the run name under each picture, in its code
    tag, is padded with non-breaking spaces, the character itself and not the entity, to fifty-two
    characters, which the code font draws at some 426 pixels, past the picture's 400 on every
    platform's monospace, so no cell can shrink below its picture and the name reads as a caption
    bar as wide as it; the cell's width stays as the preference. The rendered page is read after the
    push. Done at `54c0e669`: every name padded to 52 characters, the README at 237 lines of prose,
    the rendered page read after the push. The owner looked at the pushed page at 16:27 and it
    works, and asked for the pictures a bit smaller: every tag 320 by 180 in place of 400 by 225,
    the cell's width 320 with it, and each name padded to 42 characters, which the code font draws
    at some 344 pixels, past the picture, the captures themselves unchanged at 800 by 450. Done at
    `3051fab8`: 320 by 180, cells of 320 and names of 42. The owner looked at the pushed pages at
    16:30: the look is right, and on a phone the caption under each picture is a tenth to a fifth
    wider than the picture, on a desktop a little wider. The caption's padded text sets the cell's
    width while the picture fills no more than its own size, so on a font that draws text wider, as
    a phone's does, the text overshoots the picture. The caption sets the width on purpose and the
    picture fills it exactly: each picture's tag and cell take a nominal width past the caption's
    widest rendering, so the cell is the caption's width on every device and the picture, no wider
    than its cell by the page's rule, fills it edge to edge; and the caption shrinks to the small
    tag, `<small><code>`, which the page keeps, since the padding cannot go below the longest name
    and the font is the other knob: the names padded to 28 characters in a small code caption, and a
    name longer than that, three of 268, broken at an underscore with a `<wbr>` tag, which the page
    keeps, so no piece exceeds 28 and its last piece is padded to 28, which the owner chose at 16:34
    over padding every name to the longest, the names being raylib's and not renamed; the code font
    draws 28 at some 204 to 210 pixels, every tag 220 by 124 and every cell 220, so the pictures
    come out about 205 wide, like BevyCSharp's as the owner asked, and the same width as their
    captions, a long name wrapping to a second caption line only where the table is squeezed; the
    rendered page read after the push on a desktop and a phone. `706a1499` carries the first form,
    32 and 260, and `4805676c` the change of 16:34, 28 and 220 with seven names broken by a `<wbr>`
    tag, and the owner looked at both on a phone at 16:38 and prefers the first, its pictures
    filling up to the caption's width where the second's fell short of it, so `4805676c` is reverted
    by a commit in the form and the gallery stands at `706a1499`'s look. Reverted at `aa9fe773`, the
    README byte for byte `706a1499`'s. The owner saw at 17:02 that the games' captions, `games/`
    before each name, break onto a second line, since a browser may break a line after a slash; each
    game's caption is the game's name alone, padded to 32 as the examples' are, the picture's link
    unchanged. Done at `e28064f7`, the thirteen names alone at 32. the owner's eye judges the look
    (Decision 27).

12. **The e3d skill left the `.claude` folder, done at `7a69c936` (Decision 30).** The owner asked
    on 2026-10-11 at 15:50 that `.claude/skills/e3d-cli/SKILL.md` go to the root or elsewhere, the
    `.claude` folder and long subfolders holding nothing else being unwanted. The document becomes
    `.github/E3D.md`, beside the other documents a session reads, its skill header, the name, the
    description and the allowed tools, dropped since it means nothing outside that folder, its body
    as it is; AGENTS.md's paragraph on driving the engine and its documents table name the new
    place, which the owner's ask is the word for, as do BUILDING.md, STYLE.md's line on the skills,
    the README's line on the commands, the two links in `docs/driving-with-e3d.md` and the
    workflow's path filters, which drop `.claude`; the folder goes with the move, and `.claude/`
    joins `.gitignore` so the tool's own lock file there never enters the tree. A batch of its own,
    taken when the owner types the ask in the working session, which changes AGENTS.md, the ignore
    file and the tool's own folder only on the owner's word given there and has asked the owner for
    it, a right the reviewing session holds. Done at `7a69c936` on the owner's word typed there:
    `.github/E3D.md`, every reference moved, the folder gone and ignored.

13. **`games/Pusher` is gone, done at `ffa3ec48` (Decision 31).** The owner asked on 2026-10-11 at
    17:18, finding it ugly and of no use. The folder goes with its capture under
    `.github/assets/examples` and its cell in the README's games row, and every place that names it
    follows: the test workflow, where Pusher is the game built and played from the package,
    published native and drawn, and played once more in a window on the runner's desktop, the three
    roles passing to the lightest of the 3D games by the time `build/play-game.sh` takes it,
    measured once across them, and the games' loops dropping it; `build/soak.sh`,
    `build/drive-game.sh` and the soak check's test, whose reading named for it takes another game's
    name; BUILDING.md's two paragraphs on a game built and shipped this way and the shipping guide's
    worked example, which name the replacement; TODO.md's two lines; the raylib comparison's size
    line, which gives the replacement's; the materials guide's timing line and RENDERING.md's table
    row, which go or give the replacement's; and Hopper's README, which no longer calls it a
    counterpart. SHARED.md's row on a game published native and played by CI is the reviewing
    session's and names the replacement once the commit does. The suite, the document and norm tests
    and the README walk on the result, and the examples' table check. Done at `ffa3ec48`: Slide in
    the three roles, the lightest 3D game by measure, every place that named Pusher following, and
    SHARED.md's rows naming Slide.

14. **The development machine's GPU is named nowhere a user reads (Decision 32), done at
    `e910f4f4`.** The owner asked on 2026-10-11 at 17:26 that the mention of their machine, the RTX
    4070, leave the code and comments a user sees. Seventeen files outside `.github` name it, the
    shaders' comments, the rendering tests' comments and the guides among them, each naming it where
    a measurement was taken; the measurement stays and the device becomes a plain phrase, the
    development machine's GPU or a laptop GPU of its class, so a reader knows the class of device
    and not the owner's. The documents under `.github`, RENDERING.md, ASKS.md and this file, keep
    the name since their measurements are read against one device, unless the owner says otherwise.
    Done at `e910f4f4` for the seventeen files; whether the documents under `.github` follow is
    asked of the owner at 23:59.

15. **`Engine3D` becomes `E3D` (Decision 32), done at `35322cfe`.** The owner asked on 2026-10-11 at
    17:26 that the flat API's class be renamed, with the namespaces and the collections that carry
    its name. The static class `Engine3D` in the namespace `Engine` becomes `E3D`, its partial files
    under `3DEngine/Api` named for it, the 63 test collections named for it, and every `using
    static` and every mention in the guides, the cheat sheet, the templates, the games, the examples
    and the README follow, 716 mentions in 486 files; the namespace `Engine` stays unless the owner
    says, since `E3D.E3D` reads badly, and the package and the projects keep their names as the
    owner's (Decision 2). A rename of the public surface, so the version is the owner's to number
    (Decision 5). A batch of its own, moving names alone, taken when no other batch is in flight.
    The rename touches thirteen files of `3DEngine.Game` by their `using static` line, which the
    game's session owns and the solution needs changed in the same commit, so the working session
    changes those lines and nothing else of the game's in the rename's commit, once the game's
    session has committed or set aside its edits to those files and said so. Done at `35322cfe` so,
    the old name left on the upgrading page and in its test alone; AGENTS.md's two mentions wait on
    the owner's word.

16. **The README's guide table opens with the cheat sheet's row, done at `ffa3ec48` (Decision 32).**
    The owner asked on 2026-10-11 at 17:26 for the row BevyCSharp's README has first in its guide
    table, the cheat sheet, every public function of the library on one line, grouped as the pages
    are, linking `CHEATSHEET.md` at the root. One line, with item 13's commit. The working tree
    holds the row at the table's head and the older cheat sheet row at its foot, so the one at the
    foot goes with it. Done at `ffa3ec48`.

17. **The radiance cascades said in the README and in a guide page of their own (Decision 33)., done
    at `2172090b`.** The owner asked on 2026-10-11 at 17:40. The README's prose says in a sentence
    or two that the engine lights its scenes with real-time global illumination by radiance
    cascades, light that bounces from every surface, with a line on what that brings a game, and the
    guide table gains the page's row after the materials guide's. The page,
    `docs/global-illumination.md` or a name the working session finds better, is for somebody using
    the engine and goes into the detail the materials guide, which holds the bounce in fifty-three
    mentions today, cannot: what radiance cascades are and why the engine chose them, the scene
    distance field the probes trace through and `SetSceneField`'s cascades, cell and budget, the
    world probes in cascades and their reach, the screen probes and what they add, emissive
    materials carried as box lights, the quality tiers of `SetGlobalIllumination` and what each
    costs on the development machine's GPU in plain words, how a glossy reflection and the sky's
    share read the probes, the limits a game meets, the field's reach behind the eye and a cave
    wider than the last cascade among them, and the tools, the bounce window, `gi.show`, `gi.state`,
    `gi.toggle`, `gi.probe`, `gi.rays`, and `gi.compare` with `gi.reference` for reading a scene
    against a path-traced reference; built on an example that runs, the bounce rooms or the Cornell
    box, with blocks that compile under the guides' check, and the materials guide pointing to it
    where it speaks of the bounce rather than saying it twice; RENDERING.md stays the renderer's own
    record. The cheat sheet's grouping follows the pages, so the page's functions group under it. A
    batch of its own after item 15, since the page names the API and the rename comes first; the
    owner's eye judges the text.

18. **The examples' counts under the Examples heading, and a count on each folder's heading
    (Decision 34), done at `043b3af1`.** The owner asked on 2026-10-11 at 19:35, as of BevyCSharp.
    Under the Examples heading, after its paragraph and before the gallery, a line says the counts
    the examples table's header holds, of raylib's 222 examples how many are written here and the
    one that does not apply, and beside it how many examples the engine has of its own past raylib's
    and how many games; and each folder's heading in the gallery carries the count of pictures in
    its row, as `### Core (50)`, the games' heading its thirteen, the form the working session finds
    reads best and the owner's eye judges. Written by whatever writes the gallery today, so the
    table check and the README walk read the result, the prose count kept. Done at `043b3af1`: the
    line of 221 written and one not applying beside 34 of the engine's own and 12 games, each
    heading its row's pictures, 255 in all, the script writing both and its check failing where
    stale.

19. **A push plays one game, and the pack run plays them all (Decision 35), done at `7798d5e5` and
    `958a3eb3`.** The owner chose on 2026-10-11 at 19:50, asked whether every push must play the
    thirteen games for an hour and offered one game or a benchmark instead, and a nightly schedule
    declined. The examples job on a push keeps the examples' table check, builds every game from the
    package, which catches what a consumer of the package would hit in a few minutes, and plays and
    soaks Slide alone, the lightest game and already the one the test workflow builds, publishes and
    plays in a window; the captures, the measure against raylib's programs, the thirteen games
    played, the whole soak, the resize and the README's walk move to the pack run, which the owner
    starts, so a release still proves everything and a push costs minutes rather than an hour and a
    half. Verdict 30's soak of Manor and Verdict 49's measure are then read on the pack run's page,
    and 5.2's pack (Decision 19) reads its own run green. The workflow's script and norm tests read
    the result, and BUILDING.md's paragraph on the jobs says which run does what. A workflow batch
    of its own after the third build. Done at `7798d5e5`: the examples job a workflow of its own,
    called with whole false by a push and true by the pack, the thirty steps found by the step
    script given the workflow's file. The pack waits on the examples job since `958a3eb3`, as
    Decision 19 wants.

20. **A vertex stream of the game's own beside the mesh's vertex, and a vertex of positions alone
    (Decisions 36 and 37), done at `958a3eb3` and `3ec1fdb3`.** The owner put
    `.ref/Voxel-Sandbox-main` in the tree on 2026-10-11 at 20:05, a game on the 3.1.0 package that
    fed its shader one packed float a vertex through an input layout it set itself and unpacked it
    in the shader, and asked for the same in both engines; the plan was approved at 20:40 and
    refined at 22:55, a position every mesh's and the packed data beside it, its full text kept by
    the reviewing session, this item being what it asks of the engine. First stage, one commit: a
    mesh may carry one stream of the game's own beside its vertices, in one of five formats, `UInt`,
    `UInt2`, `Float`, `Float4` and `UNorm8x4`, the types `MeshStream` with `From<T>` and `SizeOf`
    and `MeshStreamFormat` named as BevyCSharp's are, given to `UploadMesh` with the colors and
    second texture coordinates and written in place by `UpdateMeshBuffer` at index 9, a constant
    naming it, which writes the stream's buffer through a ring as the vertices are written and never
    uploads the mesh again; a shader of the program's own reads it as `CUSTOM0`, placed by its
    semantic as the others are, at binding 5 of the model pass's pipelines since the subsurface pass
    holds binding 4, the stream bound by a call of its own, the pipeline keyed by the format; a
    shader that reads it drawn with a mesh without it reads zero from a sixteen-byte buffer at
    stride 0 as the default color and coordinates are, zeros of the shader's own class, integer or
    float, which the reflection of the stage's inputs then says, since Vulkan wants the attribute's
    class to be the input's; the stream is kept beside the mesh's streams in the store and not
    inside them, since a mesh with streams carries both default buffers and one with the game's
    stream alone must not pay for those. A `uint` and not a `float` is the packing type, which the
    guide says and why. The guide says why the packed bytes are a vertex stream and not a storage
    buffer read by the vertex id, which the owner asked at 23:15: a stream travels with the mesh,
    uploaded once with its positions and indices and read by every draw of it with nothing else
    bound, where a buffer is bound to the shader or the material, so thousands of chunks would each
    need a binding a draw or one giant buffer with an offset a chunk; a shader's buffers are bound
    per shader and a mesh's stream per mesh, a draw with a shader of its own being a batch of its
    own here already; the GPU fetches the attribute by the vertex index as it fetches the position,
    where a buffer read needs the vertex id by hand and an offset an instance when instanced; the
    fetch is about as fast either way, so the gain is the binding model; and a buffer stays the
    right tool for what is not per vertex, a palette or atlas table every chunk shares, per-chunk
    constants, or what a compute pass also reads. Tests: the store refusing a wrong count and
    queueing the in-place write alone, the write of index 9 changing one value of four bytes, the
    reflection naming `CUSTOM0` with its class, and one Vulkan fact of two planes through one
    shader, the left with the stream and the right without, drawn at 64 by 32 on lavapipe. The
    example `shaders_packed_vertices` in the Shaders folder, a grid of cubes whose four bytes a
    vertex hold a palette index and a shade, captured and in the README's row and count. Second
    stage, one commit: a mesh uploaded from positions alone, `UploadMesh(Vector3[] positions, uint[]
    indices, MeshStream? stream, Color[]? colors)`, 12 bytes a vertex at binding 0 with no normal
    and no texture coordinate of its own, since a shader that unpacks four bytes has both in them;
    the store keeps a `ModelVertex[]` of the positions with zero normals and coordinates for the
    light field, the glow lights, the bounce reference, picking and the bodies, which then work
    unchanged, and uploads the positions alone as the vertex buffer; every pipeline that binds a
    mesh's vertex gets a variant with binding 0 at stride 12 and the normal and coordinate read as
    zero from a stride-0 default at binding 6, the model pass's own and custom pipelines, the solid
    and masked shadow pipelines and the velocity pass's, picked by the mesh at draw, the subsurface
    pass leaving such a mesh out since it shades with the engine's normal; `UpdateMeshBuffer` at
    index 0 writes its positions through the ring and indices 1 and 2 warn, a skin is refused and
    `UpdateMeshVertices` warns once; the engine's own shader lights such a mesh by a zero normal, so
    it wants a shader of the program's own, said in the guide and warned once where drawn with the
    engine's own, its shadow right either way. Tests: the store keeping the zero-normal array and
    uploading 12 bytes a vertex, and a Vulkan fact of a quad of positions alone with a `UInt` stream
    over a floor under one light, the quad's pixel the packed color and the floor darker under it
    than beside it, so the positions-only shadow pipeline is proven; the reference frames unchanged;
    RENDERING.md's bindings table gains binding 6. The example becomes the reference's chunk, a hill
    of voxels as positions and four bytes a vertex, the normal, the coordinate and the tile unpacked
    in the shader, with its shadow and a line of text giving the bytes a vertex. Then the voxel game
    takes the stream as its first user, its own item once this lands, 20 bytes a vertex where 44.
    Kept out and said: keying the field's readers on positions rather than the vertex array, a later
    change of five files. Done at `958a3eb3` and `3ec1fdb3` as written; the voxel game's adoption is
    the `Review:` line under its entry in ASKS.md.

21. **The colors of a mesh of positions alone serve the light field alone and reach no buffer, done
    at `86fba7ad`.** The game's measure at `b275f738`, in its entry in ASKS.md: a section of
    positions and eight packed bytes uploads 72% of the vertex bytes it did where 45% was expected,
    since the colors given for the field's tint, which must go with the upload until item 9, give
    the mesh on the device a colors buffer of four bytes a vertex and a zero second texture
    coordinate of eight, 32 bytes a vertex in four buffers. A mesh uploaded from its positions is
    drawn by a shader that unpacks its color from its stream, so its colors are the CPU's alone:
    `UploadMesh(positions, indices, stream, colors)` keeps them where the field, the glow lights and
    the bounce reference read them and gives `GpuMeshes` no colors and no second texture coordinates
    for such a mesh; a shader of the program's own that reads `COLOR0` or `TEXCOORD1` on it reads
    the defaults, white and zero, at stride 0 as a mesh without streams does, said in the guide's
    positions-alone paragraph and in the overload's remark. A store test that a positions-only
    upload with colors keeps them for the readers and queues no stream buffers, and the Vulkan fact
    of stage 2 unchanged. The game then measures its land again, 20 bytes a vertex and about 40 MB
    expected where 63. Done at `86fba7ad`; the game measures again.

22. **The wall before the pillar blends in probes standing at the pillar's dark face; the screen's
    probes reach the pillar themselves; done at `e424550c`, its second part read and closed.** Read
    at `82b40646` and measured at `f889a559`: a wall two units before the pillar reads 0.68 of the
    reference's bounce with the pillar and 1.05 without, a gap of 37 points, 47 to 51 with the
    screen's probes off; probes at half the spacing narrow it to 15, and the merge's trace in place
    of the dark reading, measured and not taken, lifts both rooms and leaves it at 34 while taking
    the 27 bounce-room cases from 4.006 to 4.298 of summed error. The gather sees the eight probes
    around a surface by a traced bit each and weighs them by how near they stand and whether they
    face it, and the probes at the pillar's dark face pass both, their light being right for where
    they stand and wrong for the wall. Measured both ways at 05:05 and chosen so. The screen's
    probes' rays to twice the first spacing take the gap at x 7 from 37 points to 22, 0.79 with the
    pillar and 1.01 without, x 7.5 from 35 to 21 and every other point within 13, doubled depth
    steps changing nothing and three or four spacings overshooting to 1.75 of the reference at x
    9.5; the 27 cases sum to 3.739 where 4.006, the corridor from 17% under to 2 to 9%, the strip
    falling to 7 to 9% under where it held within 2, and the carried lamp to 7.5% under where 5; the
    GPU's cost at High in the bounce rooms 0.03 to 0.06 ms in most views and 0.11 to 0.13 in the
    corridor. The gather weight, each probe's reach along the surface's normal and four leaning ways
    against its distance, moves x 7 from 0.68 to 0.72 alone and to 0.80 with the reach, and with the
    screen's probes off the gap stands at 0.54 to 0.56, so the probes at the pillar's face are not
    the gap's main cause and the weight is not taken. The reach at two spacings is taken for the
    rooms' gain, the marks of 15 points and a tenth of a millisecond missed and said: the gap at 22,
    the corridor's cost at 0.13, the strip and the lamp a few points further under, written in
    RENDERING.md as the trade, the glowstone room's GPU time read before the commit, and the strip
    read for why the longer rays take from it. What remains is the probe beside the wall reading its
    face toward the pillar 21% low, of which the merge's dark reading was half, which is the item's
    second part: that face's shortfall read ray by ray against the reference and mended where the
    cause is the engine's, measured by the gap at x 7 and the 27 cases' sum, which must not rise
    above 3.778, the sum since the first part's leak remedies. The first part is done at `e424550c`.
    The second part was read ray by ray at 06:50 and closed with nothing kept: passing over a
    cascade-1 probe whose ray met a surface early takes the probe's face toward the wall from 21%
    under to 7, but the frame does not follow, x 7 reading 0.83 with the pillar and 1.05 without,
    the same 22 points, and the 27 cases rise to 3.859, since the wall's pixels read their own
    screen rays out to four units and the probe's face no longer sets the gap; 64 screen rays read
    lower still and finer glow marches at a hit change nothing, so what the rays bring is short
    rather than few. What is left is the probes' resolution: the floor in the one-unit gap between
    the lamp and the pillar is lit by the pillar's bright west face, the wall sees it past the
    pillar's edge, and probes two units apart cannot hold it; and the carried lamp's red wall is the
    same, the probe beside the green inner wall reading 0.11 toward the lamp's corner where the
    reference has 0.84, its one ray in that texel meeting the green wall's unlit end face half a
    unit off while the reference's cone takes in the lit corner past the edge, one ray a texel
    sampling a steep light past a thin edge. The gap at 22 stands as that limit; a game that needs
    the light in such a gap takes a finer cell, which stands the probes closer, as probes at half
    the spacing read the gap at 15 here.

23. **A mesh casts the sun's shadow and stays out of the light field, and a moving mesh's stamp
    costs less; done at `6a36b2b5` and `089ac771`.** The game's entry of 2026-10-12 in ASKS.md: its
    mobs, four to six pieces each drawn with `DrawMeshInstanced` at matrices that change as they
    walk, casting shadows, are stamped by the field as boxes in every frame they move, 17 walking
    mobs taking `cpu.scene_field` from 0.8 ms to 2.9 and `gpu.scene_field` from nothing to 1.0, 76
    taking them to 4.8 and 3.9 with the 256 boxes of `SceneFieldPlan.MaxShapes` filled and the
    farthest pieces left out, 16 to 28 microseconds of the CPU and 13 to 15 of the GPU a walking
    piece a frame, where 76 standing cost 1.1 and nothing. First, the flag: `CastsShadows` on a
    material decides both the shadow and the field, so a material gains a second switch,
    `BouncesLight`, true by default, which keeps every scene as it is, and a mesh drawn with it
    false casts its shadows and is left out of the field, the glow lights and the bounce reference,
    said in the materials guide beside `CastsShadows` with the mob as its case, a test that such a
    mesh casts a shadow and stamps no box, and the game's measure again with its mobs so, which
    should read the standing cost; the game's own color is then lost to the bounce for mobs, which
    is the right trade for a crowd. Second, the cost of a walking mesh, read in a reproduction of
    the engine's at 03:15, a land of 400 sections and 200,000 triangles with mobs of five pieces
    drawn instanced in a Debug build: the stamp itself costs the GPU half a microsecond a box, 0.06
    ms for 85 boxes and 0.12 for 256, under the 3 asked already; the GPU's 13 to 15 microseconds a
    box in the game are cascade rebuilds, since a piece that stands eight frames settles into the
    still field and marks its cascades dirty and marks them again when it walks on, so mobs that
    walk 90 frames and stand 45 rebuild a cascade every second frame with 17 and every frame with
    76, 3.6 and 6.2 ms; and the CPU's 8 microseconds a box at 76 is 0.91 ms of the plan's order,
    which once any mesh moves leaves the read-by-place path and rebuilds a dictionary keyed by every
    drawn instance, the land's included, 0.60 ms of the shapes, 0.29 of the bricks and 0.28 of the
    buffers. So the remedy is two things and no cheaper stamp: the read-by-place path keeps going
    when a slot's mesh is the same and only its matrix moved, which takes the order to near nothing,
    and the shapes' loop is trimmed toward 5 microseconds a box; and a mesh whose instances were
    seen moving settles into the still field only after a longer stillness, its length chosen by
    measure and said in RENDERING.md, so a pausing mob rebuilds no cascade, as a still instance
    leaving while its vertices are drawn or its slot moved rebuilds none, a settle costing a few
    milliseconds of GPU against half a microsecond a frame for its box. Measured in the reproduction
    and then by the game in its village with 17 and 76 mobs with `BouncesLight` on, 76 walking and
    pausing to cost the field under 0.5 ms of GPU and under 2 of CPU. Grouping a mob's pieces as one
    box is not needed. Done at `6a36b2b5` and `089ac771`, the game measuring its village with the
    bounce on after.

24. **The glow lights' shadow past a box's edge is a step where a penumbra belongs.** The owner's
    picture of 2026-10-11 at 13:50 showed a zigzag seam on the voxel game's wall where the lamps'
    light falls past a pillar, and the game's pictures drawn again at `6f4c98bb` show it unchanged
    by part h's work since: across the seam the frame steps 26% of the light in 20 pixels where the
    path-traced reference rises smoothly over 170, the frame 0.79 to 0.88 of the reference on the
    dark side and 0.84 to 0.98 on the lit, the seam's middle rows within 2 pixels of a line and
    teeth of 11 to 26 pixels at its top and foot, where the march passes the pillar's ends. The
    cause is the glow lights' march: a ray from the surface to the halves of a lamp's faces either
    reaches or is stopped, so the shadow a box throws is hard at the march's resolution and toothed
    where its steps cross the box's ends, and the wall's bounce from the lamp, which is most of its
    light there, takes that edge. The remedy is the distance field's own penumbra: along the march
    the least of the field's distance over the distance traveled gives how far the ray passes from
    the occluder's edge, and the lamp's light is scaled by it from zero at a grazing pass to one
    past a cone's width, the cone set by the lamp's face's size over its distance as a soft shadow
    is, so the shadow falls off over the width the reference shows and the teeth, which are the
    step's, go with the step; measured on the game's seam row by row against the reference, the
    step's 20 pixels to become a falloff within a quarter of the reference's width and the teeth
    under 3 pixels, the glowstone room and the bounce rooms' 27 cases read for what moves, the
    march's GPU time before and after, and the owner's eye on the picture drawn again. After item 9.

## Verdicts

Verdicts 1 to 53 are settled, the last three, the soak, the measure and the picture script, on
`fdf27346`'s examples job on 2026-10-12, and their numbers are not given again. No verdict is open.

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

28. **The owner's eye on 2026-10-11: the sky's light leaking into the game's cave by the heading is
    a glitch to be found and fixed, and the overlay gains a post-processing window.** The owner
    asked at 10:05 on 2026-10-11, standing in a cave of the voxel game: the leak between headings of
    258 and 282 degrees and the faint ones between 219 and 322 and between 314 and 46, which is item
    2's part g, and a tab of the ImGui overlay that opens a window of tonemapping and coloring
    settings as the performance window is opened, which is item 10. At 10:55 the owner looked at the
    tree with g's mend: right at every heading and tier but High between 259 and 281 degrees, where
    the face turned to the right still takes the sky, which g keeps, and the indirect light noisy
    and spotted where a penumbra is wide, which is item 2's part h. The order is this file's: g
    first, then h, then item 2's remainders, then item 10 before item 9.

29. **The README's gallery is one scrolling row of pictures a folder, sorted by folder, and the
    games' text under the table goes.** The owner asked at 15:30 on 2026-10-11, which is item 11:
    the examples' pictures in one-row tables that scroll sideways rather than the grid, one a folder
    of the examples with the games in their own, and the long text explaining the games under the
    table taken out.

30. **The e3d skill leaves `.claude` for `.github/E3D.md`, and the gallery's pictures are one
    size.** The owner asked at 15:50 and 15:52 on 2026-10-11: the skill document out of the
    `.claude` folder and its empty subfolders, to the root or elsewhere, which is item 12 with
    AGENTS.md's line changed on that word; and every picture in the README's rows the same size,
    which item 11 takes.

31. **`games/Pusher` goes.** The owner asked at 17:18 on 2026-10-11, finding it ugly and of no use,
    which is item 13: the game, its capture and its cell gone, and its three roles in the test
    workflow, the game built and played from the package, published native and drawn, and played in
    a window on the runner's desktop, passed to the lightest 3D game.

32. **The GPU named nowhere a user reads, `Engine3D` renamed `E3D`, and the guide table opening with
    the cheat sheet.** The owner asked at 17:26 on 2026-10-11, which are items 14, 15 and 16: the
    development machine's GPU out of the code and comments a user sees, the flat API's class renamed
    with its files and the test collections, and the README's guide table given the cheat sheet's
    row as BevyCSharp's has it.

33. **The radiance cascades are said in the README and in a guide page of their own.** The owner
    asked at 17:40 on 2026-10-11, which is item 17: the README's prose saying the engine lights its
    scenes by radiance cascades, and a page of the guide going into their detail, built on an
    example that runs.

34. **The examples' counts stand under the Examples heading and on each folder's heading.** The
    owner asked at 19:35 on 2026-10-11, as of BevyCSharp, which is item 18: a line of how many of
    raylib's examples are written and how many the engine adds, and each folder's heading in the
    gallery carrying its count.

35. **A push plays one game, and the pack run plays them all.** The owner chose at 19:50 on
    2026-10-11, asked whether every push must play the thirteen games, which is item 19: on a push
    the games are built from the package and Slide alone is played and soaked, and the captures, the
    measure, the games, the soak, the resize and the README's walk run on the pack run; a nightly
    schedule was offered and declined.

36. **A vertex stream of the game's own, in both engines.** The owner put a game built on the 3.1.0
    package in `.ref` at 20:05 on 2026-10-11, which fed its shader one packed float a vertex and
    unpacked it there, asked whether such a stream belongs in a game engine and how both would do
    it, and approved the plan at 20:40, which is item 20 here and item 18 in BevyCSharp: one stream
    a mesh in five formats, read as `CUSTOM0` here and at `TEXCOORD8` and up there, beside the
    vertex in both and as the vertex alone here, where BevyCSharp keeps positions since Bevy's
    pipeline reads them.

37. **A position is every mesh's, and the packed data a stream beside it.** The owner refined
    Decision 36 at 22:55 on 2026-10-11, approved at 22:58: a position is mandatory, as Bevy has it,
    since shadows, motion vectors and everything around a mesh read it, and a game's packed four
    bytes ride in a stream of its own beside it; so item 20's second stage is a vertex of positions
    alone, 12 bytes, in place of a mesh that was the stream alone with its bounds given and shadow
    pipelines of its own, and the voxel game's vertex is positions and four or eight packed bytes in
    both engines.

## Replies

Item 8 is in the commit after `314b9d48`. An opaque material's batches draw with pipelines that set
specialization constant 0, which folds `alphaTested`'s discard away, and a test draws an opaque and
a masked copy of one quad in one frame, the one whole and the other cut. It saves nothing on the RTX
4070, as measured before, and RENDERING.md says so with the reason; the macOS job's profile will be
its first reading on a tiler.
