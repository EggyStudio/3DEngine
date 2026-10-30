# Review

Reviewed up to `1703c54e`, item 97, pushed by the owner's hand at 16:54 on 2026-10-26 while the
sessions were paused until 2026-10-30, its run green whole at 19:33 with Windows in 99 minutes. The
ninth pack run started with publish on at 18:41 on 2026-10-30, since the owner sees item 97's specks
in the voxel game and its package lacks the mend; the run watched. Before it on 2026-10-26, each
green but where said: `987f07ae` (Verdict 94's mend) with `969a1b08` (item 129); `7ff7bbdd` (130),
red on the examples job alone, Verdict 94; `b05b32da` (89); `0a4ad922` (the README's quick links);
`5528c55c` (95); `d207627e` (91), `bb542fa4` (93) and `0754b975` (128), the eighth pack run
publishing 5.2.326 at 13:13; `2f5237f2` (127); `78946b22` (112), the seventh pack run publishing
5.2.322 at 09:14; `7af16fcd` (Verdict 93's mend); `45edfbf6` (111); `a16227a7` (100), the sixth pack
run publishing 5.2.319 at 04:21 on its second attempt. Each commit's detail stands in its item in
REVIEW-ARCHIVE.md, its devlog note and its own words. On 2026-10-30 at 18:0x the owner wrote the
README's new words (item 133) and the focus on radiance cascades (Decision 74), questions asked
back, and asked that the sessions spend fewer tokens at the same quality, N 7.7.

Next 134, the swapchain on a resize, and 133 when the owner answers on its mark, then the cascades
in order, 135, 131, 136, 137, 138, 139, 140, 141 and 142 (Decision 75); 132, 143 and 144 wait behind
them.

Items done and verdicts settled before 2026-10-23 stand in REVIEW-ARCHIVE.md beside this file, their
numbers kept, moved there at 19:30 on 2026-10-23 so this file holds the current work alone and is
read whole before a batch; the archive is read when an item or a verdict named here is wanted in
full.

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
   15 minutes where 8 before, macOS and the captures green, the examples job skipped; the scripted
   play script says since `e50fd247` whether a game's process lives when a command finds no session,
   and its exit code and Windows' crash event when it ended. The run of `b1e86aef`, with
   `214feb11`'s mend, is green on Linux in 12 m 9 s, 1,633 passed and 15 skipped, on macOS in 34
   minutes and on the macOS captures, 255 of 255 in 3,335 s, and on Windows whole in 68 minutes,
   1,626 passed and 22 skipped in 39 m 21 s of tests, the leak test's frame 3.8 s (Verdict 48), its
   digest green and its examples job begun at 10:42 (Verdict 30); the run of `e477bc1e` is green on
   Linux in 12 m 28 s, 1,635 passed, 1,750 MB at most where 1,631, on macOS, on the macOS captures
   and on Windows whole in 69 minutes, its examples job red by the measure alone as well, two pairs
   over, the billboard and the depth writing, the two loading examples holding this time;
   `b1e86aef`'s examples job is red by the measure alone, four pairs over their recorded share, the
   soak and the package's steps skipped after it (Verdict 49), so Verdict 30 is unread. The page
   showed the leak test's progress line as its first of 198, `app 2`, where the line named its app
   all along; the page shows the last of a repeated line since `88673244`. Verdicts 40 and 44 are
   carried out there, the leak test following four apps through every plugin's making, a failing
   frame written where the job uploads it with each surface's means, and four references drawn again
   after a drift of up to 2.16% on the RTX 4070 itself; the runs since read the references green and
   the leak test red. The examples job runs when Windows and macOS are both green in one run, which
   they are since `817f7fd4`. When every job is green the owner is told, since 5.2 is due (Decision
   19). With the batch that next touches `build/test.py`, it takes from BevyCSharp's `1f68fde8` the
   two cases of a theory whose names are cut to the same as one counted apart, which its page reads
   as one today (SHARED.md). The game's tests are its own repository's since Decision 41. The
   engine's own despawn of what a state scopes is read against a soak of the world's entity indices
   across many transitions, which in BevyCSharp found Bevy 0.20.0 losing every index it despawned
   that way (SHARED.md), with the batch that next touches states. The owner pushed everything
   through `ffa3ec48` by 18:33; the runs of `aa9fe773` and `e28064f7` are green on Linux in 17 and
   13 minutes and red on Windows after 87 minutes, the Rendering group and the suite at their
   40-minute limit with 377 tests without a result and the leak test's frame at 4.3 s (Verdict 48),
   and on `e28064f7` the leak test's resident memory 92 MB past its bound (Verdict 51); the macOS
   job of `aa9fe773` is red by its first game's step, the WebP script wanting ImageMagick the runner
   lacks (Verdict 52), its tests green, and `e28064f7`'s macOS job green; the macOS captures of
   `aa9fe773` green; the runs of `ea9a23cd` and `ffa3ec48` wait in the queue. Each push's run is
   read by the reviewing session, and a failure it names comes first here.

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
   batches, each measured, what of its first batch stands alone committed before item 2 begins; done
   at `42b3e9ce`.** A material gains what skin, wax, marble and a leaf have, a subsurface color and
   a radius in world units with a thickness scale for its thin parts, set in the flat API as the
   material's other fields are and read from a glTF file's `KHR_materials_volume` thickness where it
   has one. First, the diffusion: light that enters leaves nearby, so the lit light of the marked
   pixels is spread along a profile of the material's color and radius in a separable screen-space
   pass over the HDR frame, the specular kept out of it where the frame has it apart, masked so an
   unmarked pixel is never touched and the spread never crosses a depth edge, with a test of a lit
   sphere whose terminator softens and bleeds the color where the unmarked sphere beside it does
   not, read at pixels as the occlusion tests read theirs. Second, the light that comes through: a
   thin part lit from behind shows the light on its front, the thickness toward the light read from
   the scene's distance field, which has it for nothing where the field is fine, and from the sun's
   shadow depth where it is coarse, with a test of a thin sheet lit from behind brighter on its
   front where it is thin than where it is thick. Third, the tiers: sample counts and a half-size
   pass at Low and the full at High, as the bounce is tiered, the kernels in Slang so the bridge
   runs them in BevyCSharp once proven here, the GPU cost of each batch on Manor and on the test
   scene in RENDERING.md and the comparison page, the guide's section and TODO.md's entry. Each
   batch a commit of its own with its numbers, and what does not pay a measured share stays
   described. The second batch is in at `d885c89e`, the light that comes through. The third brings
   the tiers as a setting of their own, `SetSubsurfaceQuality` with Low, Medium and High and High
   the default, each timed on Manor and on a new `shaders_subsurface` example with its capture and
   README row, Low's half-size spread laid onto the frame where the pixel's depth matches, and the
   two things the reply of `d885c89e` left for it, a material's own thickness from
   `KHR_materials_volume` and a lamp's light through from its shadow map where there is no field.
   The third batch is in at `94c4bf3b` with the tiers and four mends of the light through, and the
   first two of what remained at `63961dbb`, the material's own thickness from
   `KHR_materials_volume`, which caps the thickness measured and stands in where nothing measured,
   and a shadowed lamp's light through from its own map where the field is coarse. What remains of
   the item comes after item 2: the spread in render textures and probe captures, or the reason it
   stays out written with its number; and a test of the cap itself, the lesser of the measured and
   the given, which the example's leaf shows and no test holds, a sheet in a field of 0.15 cells
   with two millimeters given read brighter on its front than with none. The remainder is in at
   `fca652a1`, the cap's test, and `42b3e9ce`, the reason the spread stays out of render textures
   and a probe's faces, with its numbers.

4. **The animated model's meshes in the world (Decision 21), after subsurface scattering; done at
   `82b23f8c`.** TODO.md's "Models are partial" says an entity an `AnimatedModel` draws keeps its
   copy's meshes out of the world, so it is given a capsule or a box and a mesh or hull collider on
   it is refused with a warning. The copy's meshes become the entity's meshes in the world, posed
   from the same joints as its wires and a body made from it are, so `CreatePhysicsConvexHull` and a
   mesh collider take them as they take any mesh, with a test of an animated entity whose hull
   follows its pose, the guide's section on animated models and the entry updated with what it then
   says. Read at 18:55 on the working session's question: the body is made once from the meshes as
   they are posed when it is made, as a body made from a posed model through the flat API is, shaped
   as last posed and drawn and staying so, and the test reads it bent after the arm's clip has
   played to its end, a ray down at x -0.8 hitting near y 1 where it misses at rest; a body remade
   as the pose changes, a hull or a BVH built an entity a frame and a dynamic body losing its
   velocity at each, is not made until a game asks for it in ASKS.md, and the guide's section says
   which a body is.

6. **Three packages bumped, after 5.2 is packed (Decision 20).** Vortice.Vulkan 3.2.1 to 3.3.0,
   AssimpNetter 6.0.4 to 6.0.5 and StbImageSharp 2.30.15 to 2.30.16, in one commit with the suite
   run and the notices written again, on the owner's word typed into this session once the package
   is out, as AGENTS.md has it for a version; SDL3-CS stays on its preview and NVorbis on 0.10.5.

7. **A game is written meanwhile.** When the items above wait on a run or on the owner, the next
   game of `games/` is written, as the owner asked on 2026-10-07, a later game finding nothing new
   being the point of each. On 2026-10-20 at 09:42, with the week's readings ended and the verdicts
   waiting on runs, the thirteen games' use of the engine was read: hinges, kinematic boxes and a
   heightmap are taken by Manor, Summit and Rally, and no game uses a continuous body, distance or
   ball joints, ropes and chains, a slider joint with its motor or a joint destroyed while the game
   runs. The next game is Pinball, a fast ball as a continuous body on a tilted table, flippers on
   hinges driven by their motors, the plunger a slider with its motor, bumpers and slingshots
   kicking the ball, many contacts a second, a score with its sounds and a tilt that nudges the
   table, played through e3d by its keys with a drive asserting a ball launched, flipped and
   drained, chosen first since a fast ball on thin rails is where a missing sweep shows at once; the
   rope-and-bridge puzzle, planks joined by ball and distance joints, ropes as chains of them and a
   joint broken past a bound, is the game after. Rally has no README where every other game has one
   in the games' form; it gets one in a commit of its own before Pinball's batch, documents alone.
   Rally's README done at `ea7b1b83` and Pinball at `2271cdea` on 2026-10-20, as the head says.
   Pinball found two things of the engine's: the plunger driven home with a force of 600 went from
   rest to 33 units a second in one step and passed through the ball before it, both continuous,
   since the physics library finds a step's contacts from the velocities the step begins with and
   the motor's acceleration comes after in the solver, so neither sweep sees it, where held to 120
   it speeds up over its travel with the ball riding it and leaves at 21; and at a torque of 400 the
   flipper stood 18 degrees past both its limits, 3 at 60, the limit a spring a motor can push into.
   Neither is mended in the engine today; the physics guide says both beside the continuous body's
   call and in the hinge's paragraph, a line each for a game's author, and E3D.md says that the key
   command answers once the key is let go, so a status read right after it reads the key up, one
   commit, documents alone, before the next game. The rope-and-bridge puzzle is next: a load carried
   across gaps on bridges the player joins from planks, planks joined by ball and distance joints,
   ropes as chains of them, a joint broken when its force passes a bound so a bridge breaks where it
   is weak, played through e3d by placing planks with a drive asserting a crossing and a break. The
   document lines done at `1189291d` on 2026-10-20; the rope-and-bridge puzzle begun. The soak's
   Pinball turn mended at `5cd706d9`; Span, the rope-and-bridge puzzle, in the tree on item 40's
   call. Span done at `f74e087f` on 2026-10-20, as the head says, fifteen games; the fall it met was
   its own design, said there. By 11:12 the coder had read what the games use: of the 717 statics in
   the surface listing, 465 are called by no game, most of them raylib's helpers over math, images,
   text and files, but whole areas stand untouched, the weld joint, the ball joint's limits, convex
   hulls and capsules, the bodies in a sphere, the sphere cast, an impulse at a point and a
   contact's impulse, triggers, the character's height and a distance joint's range; animation
   beyond one clip, the blending and layers and the animated model component; audio pushed by the
   program, the stream's pitch, pan, volume, pause and resume, the processors and waves; meshes and
   textures changed at run time; spot lights, a light's direction and shadows, the shadow map's
   size, the tonemap, depth of field, color grading and contact shadows; the 2D shader, scissor and
   blend modes; touch and the gamepad's sensors; and the storage values. The third game is a siege,
   knocking down welded towers: towers of convex-hull and box blocks welded together, broken where
   the joint's force passes a strength, a weld's reading mixing force with torque where no test or
   game has tried it; a catapult arm on a hinge with its motor throwing a capsule or a hull; a blast
   finding what is near with the bodies in a sphere and pushing each piece with an impulse at a
   point; a block's damage from the contact's impulse, aiming with the sphere cast and a trigger for
   the goal; hundreds of blocks waking and sleeping; a spot light on the tower with shadows and a
   tonemap and depth of field chosen; the scripted run asserting a throw, a weld broken and the
   tower down. A ragdoll game, which would cover the ball joint's cone, capsules and the animation's
   blending and layers, waits on a skinned model, which the games have none of, and a
   procedural-sound toy is narrower; both stand after. Siege done at `f3949ba6` on 2026-10-20, as
   the head says, sixteen games, the weld's reading holding no surprise once item 40's fix was in,
   item 42 found and mended on its way. The fourth game, said at 12:32: the ragdoll, since the
   examples' fetched raylib resources hold a skinned and animated robot model the examples already
   draw, which a game may carry with its notice; a figure that walks and runs by the animation's
   blending and layers, an upper body aimed on its own layer, and falls as a ragdoll of capsules on
   ball joints with cone limits and hinges at the knees and elbows when struck, stands up again by a
   blend from the pose it lies in, and takes the character's height setting; the scripted run
   asserts a walk, a fall and a rise. The sound toy stands after it. By 12:36 the robot model's way
   was read: it comes carried in the game's resources with its notice beside it, as the games' font
   comes with its license, since raylib's license file at the pinned commit lists it as CC0 by its
   author and a game carries what it loads where the examples fetch raylib's files under raylib's
   many licenses; it is 1.6 MB, byte-identical to the fetched copy, the largest file a game carries,
   in the tree as a plain blob since nothing is routed through LFS, which the reviewer allows and
   tells the owner, who may have it fetched in the game's build instead. The model has 43 joints and
   clips for idling, walking, running, a punch, death and standing; the ragdoll's pose is a
   one-keyframe animation the game fills from the bodies each frame, getting up a blend from it to
   standing, and the upper body a layer over the walk from the torso. Tumble done at `39103995` on
   2026-10-20, as the head says, seventeen games and no engine finding. The fifth, the sound toy: a
   program pushing its own samples through the audio stream, the stream's pitch, pan, volume, pause
   and resume, the mixed and stream processors, and waves loaded and read as samples, as an
   instrument played by keys with an echo and a filter as processors and a recording saved as a
   wave, the scripted run asserting a note played, a processor's effect read in the samples and the
   wave saved and read back. Tones done at `fd484ead` on 2026-10-20, as the head says, eighteen
   games, with item 43 as its finding. The list is through: Pinball, Span, Siege, Tumble and Tones,
   five games for five of the areas the coverage reading named, two engine faults found and mended
   on the way, items 42 and 43, and two reads added, items 40 and 41. The next game comes when the
   items above wait again.

8. **An opaque material's hidden fragments are rejected before they are lit; measured, the hidden
   land costs geometry, not fragments; done at `c81eca54`.** The model pass discards a fragment for
   its alpha cutoff, so the GPU's early depth test is off for every material and a fragment behind a
   wall is lit before the depth test throws it away: in the game's room the land outside, all of it
   behind the walls, costs the pass 0.6 ms, 1.42 ms at eight columns against 0.83 at two, and
   drawing the walls first saves nothing, 0.05 ms worse (ASKS.md, 2026-10-11). Either an opaque
   pipeline without the discard for materials whose alpha mode cuts nothing out, the masked and
   blended keeping theirs, so the early test rejects a hidden fragment before it is shaded, or a
   depth pass ahead of the model pass that every opaque draw writes, which costs a pass of its own
   and pays only where the shading is heavy; the first is tried first, since it adds no pass, and
   the second measured against it where the first leaves most of the cost. Measured by the model
   pass on the game's room at eight columns against two, with the desktop quiet, and on Manor and
   Wick, with the pipelines' count; a test holds the room's land to a share of the pass. Taken after
   item 2's parts f and c and before item 3's remainder. Measured at 08:00 before a commit, the
   first way built, a specialization constant the alpha test reads, set by the pipelines of batches
   whose material is opaque, the batches split by it and the discard folded away, read on the
   entry's room on a flat world by the model pass's time, the least of three readings after 400
   frames, old and new in turns twice with the desktop at nothing: with the glow off 1.145 and 1.161
   ms at eight columns against 1.149 and 1.129, 0.80 at two either way, and with it on 2.44 and 2.49
   against 2.34 and 2.43; the hidden land's share is 0.34 to 0.42 ms in every case, the glow lights'
   march per fragment on or off, so the old build rejected those fragments before shading them
   already, the development machine's GPU testing depth early under a discard and deferring the
   write alone, which is also why drawing the walls first saved nothing. The cost is the vertex and
   raster work of 289 columns of sections against 25, all inside the frustum, which a depth pass
   would add a pass to and not remove. The patch goes in all the same, 257 lines, since a device
   that turns the early test off under a discard, lavapipe and the tilers among them, gains what
   this one does not, measured on the macOS job's profile where it prints one and said in
   RENDERING.md with the reason; and what removes the cost is culling the sections the eye cannot
   see, the game's own flood fill over its sections through their open faces, as Minecraft's
   visibility graph does, which is the game's to build on what the engine has, said under its entry,
   and an occlusion culling in the engine from the frame before's depth, left until the game's
   culling is measured and falls short. The game culled its sections at `ab9cd9b7`, a cave's frame
   from 16.2 ms to 4, so the engine's occlusion culling stays out until a game of another shape
   asks.

71. **A program leaves the screen probes out of its global illumination, and the radiance cascades'
    faults the owner sees in the voxel game are read and mended (Decision 54).** The owner wrote at
    about 01:00 on 2026-10-21 that the voxel game is not to enable the screen probes, which give a
    pixelated look even where none is meant, that the cascades without them are the better option
    for the game's art, and that the cascades still show faults: light from a light block held off
    by two columns leaks through the thin gap at their corner; the penumbra of edges on the ground
    is not smooth and shows zig-zags; the screen probes shift on movement and are not consistent on
    the voxel game; there seems to be z-fighting at points between the global illumination and the
    opaque textures, if not fixed already; and there is noise, visible in the bounce rooms 3 to 6 or
    so. Two parts. First, the call: the screen probes are a part of the method a program cannot
    leave out but by the `gi.screenprobe` command and the bounce window, so the flat API gains
    `SetGlobalIlluminationScreenProbes(bool enabled)`, on by default as today, with its cheat sheet
    line through the script, the surface listing, the global illumination guide's paragraph on when
    to leave them out, a pixel-art or voxel look among the reasons, the upgrading entry, and a test
    that a frame without them draws the cascades' light alone; one commit, after item 67, since the
    game waits on it. Second, the faults, after item 70: the game's coder writes an ASKS.md entry
    with a repro of each, the world's seed, the place, the hour and a capture, and the engine's
    coder reproduces each in the engine's own scenes, the bounce rooms or the Ore sample's voxel
    field, with a capture, reads it to its cause and says the cause in RENDERING.md beside the terms
    already named, then mends each where a form passes the rule that no view's distance to its
    path-traced reference widens at either size and costs no more than it gives, one commit a fault,
    and names as a term where the method stops what no form passes; the glow lights' bound first,
    right after the call and before item 68, since the game's entry reads the model pass at 1.8 ms
    with the screen's probes and 12.9 ms without with forty glowstones in view, so the game cannot
    leave the probes out until it is bounded; then, after item 70, the edge leak, which the entry
    reproduces in a room whose halves meet along one edge, 43 levels about the edge without the
    probes and 3 with the edge closed, the kind of leak the fifth term took on; then the zig-zag
    edges, stepped square without the probes and wavy with them; then the noise, which the game did
    not see and the owner saw in the bounce rooms 3 to 6; the z-fighting, which the game did not
    see, read in the bounce rooms; and the screen probes' shift last, small in the game's numbers
    and left out there. A sixth term found by the call's reading on 2026-10-21 joins the second
    part: without the screen's probes the model pass takes the glow lights at each pixel, marching
    to each in view, 0.31 to 1.01 ms at Low on the Cornell box's four small emitters where the
    screen pass left them in texels for 0.7 ms less, so a voxel world of many light blocks may lose
    in the model pass what the call saves; read with the game's figures from its entry and bounded
    by a form whose cost does not grow with the lights in view, a list of the glow lights a screen
    tile sees or a coarse glow pass of its own without the probes, under the same rule. Each mend's
    capture in the record, the game's captures made again on the package that brings them. The first
    part done at `10503abe` on 2026-10-21, the call, as the head says; the glow lights' bound next
    in the form said at 01:44 and taken, the screen probes' pass kept glow-only, its geometry and
    glow texels written and no indirect ray traced, the model pass reading the glow texels with the
    bounced light from the cascades, so the lights are marched once a tile as before and a pixel's
    cost does not grow with them, the room's glow within the rule against the probes-on frame, the
    costs read with 4 and with 40 emitters, and a capture of the field of 40 in the glow-only form
    beside the per-pixel form and the probes-on frame in the record, so the owner judges whether the
    glow texels bring the look back. The glow lights' bound done at `0a4928d3` on 2026-10-21 in that
    form, Ore at night from 9.67 to 2.50 ms with 64 lights lit and the capture smooth, as the head
    says; the edge leak, the stepped edges, the noise, the z-fighting and the shift remain, after
    item 70. A seventh term from the game's entry of 06:39 on 2026-10-21, first among the faults
    left: a lantern's or a torch's light lies in soft round blotches about 40 pixels across standing
    still on a room's ceiling and walls, which the owner saw in the game's captures and called
    fireflies; the game's reading in a room of oak planks with a hanging lantern and two wall
    torches at hour 21 on 5.2.190, the ceiling's luminance less a Gaussian blur of 20 pixels
    counting spots of 40 pixels or more at 4 levels or more over the blur, 8 spots at High with the
    largest 10 levels over and the same 60 frames later, 3 with the screen's probes toggled off, 3
    at Medium, 5 at Low with 18 over, none with global illumination off, 4 with the lantern alone
    and 5 with the torches alone, a glowstone in the lantern's place leaving the ceiling smooth, and
    the frame still over 120 frames; so the blotches are the light of the small emitters carried as
    lights, in the cascades and in the screen's probes alike, where a full emissive block's light is
    smooth, read to the probes that gather a small emitter's light near a surface, and mended under
    the rule or named. The Windows suite of `2f57b341` logged 117 validation warnings that a
    dispatch reads the field's faces image, bound with the 8-bit unsigned format, through a shader
    declaring the image 32-bit unsigned, every load undefined to the whole image; read with the
    blotches and the leak first, since an undefined read of the faces could be either. The faces
    image warning mended at `fade1730` on 2026-10-23, the sampled integer images given their r8ui
    format with a test over every built-in shader's integer images, the capture pixel-identical on
    the 4070 so the blotches have another cause, read next in an engine room of the game's kind at
    the game's spot measure. The blotches read at 23:43 in an engine room of the game's kind, a
    plank shell at night with a lantern under the ceiling and two torches on the walls, field cell
    0.25, High: round spots of 20 to 40 pixels on the walls and the ceiling away from the emitters,
    still over 60 frames, nearly gone with the screen probes off and brought mostly by a torch
    against a wall; a probe at a spot's center reads 0.643 against a reference of 0.079 over its
    hemisphere while its neighbor reads 43 percent under, since one of its sixteen rays meets the
    field on the blur of torch and wall a few centimeters wide where the glow light's bound does not
    hold and the torch's light reaches the hit from no distance, and the tiles' sixteen places are
    fixed so the same probes catch the patch every frame and the filter's 5 by 5 blend spreads it to
    its footprint. Form approved: the tiles' places turn each frame so a tile holds each place once
    in sixteen frames, and the filter's history, a fifth of this frame against four fifths of the
    last within this frame's spread, carries a hot patch's light across the probes and frames, the
    frame's count reaching the screen pass through a second uint4 of its push at no cost in rays; if
    spots over 4 levels remain, a ray's hit lit by a glow light nearer than a cell to its box takes
    that light at a cell's distance at nearest, taken only if the covered-fault rule holds; each
    step measured in the room and the bounce rooms 1 to 8 by the spot count at the game's measure
    and each view's distance to its path-traced reference at both sizes, which must not widen, with
    the model and screen passes' GPU times; a device test of a room with a small torch counting the
    ceiling's spots under a bound, the GI and reference tests unchanged, and RENDERING.md gaining
    the term, a small emitter's near field read by a few fixed rays. The reading's scene is scratch
    and goes in no commit. Step 1 measured at 00:02 on 2026-10-24 and reverted, failing the rule:
    over 16 frames at 160 by 90 the thin walls' view widens by 0.006 at High, 0.013 at Medium and
    0.016 at Low with single frames at Low up to 0.099 against the test's 0.09, view 3 at High by
    0.004 and view 6 at Low by 0.003 while views 1, 4 and 7 narrow, and a still view flickers, the
    99th part of pixels moving by 4 to 5 percent of the mean light a frame in the Cornell box
    against 0.03 percent held, the candidate RENDERING.md already records as dropped; the spots in
    the scratch room fell from 8 to 5 and the GPU time held level. Step 2 is measured on its own
    over the held places, a probe ray's hit taking a glow light at a cell's distance at nearest by
    moving the hit straight away from the light's box. Step 2 read at 00:07 and not kept, failing
    the rule: the strip's room alone moves and widens, 0.207, 0.233 and 0.213 to 0.259, 0.263 and
    0.279 at Low, Medium and High at 160 by 90 and 0.101 to 0.169 and 0.109 to 0.180 at 800 by 450,
    every other view within 0.001, the scratch room's spots from 8 to 6 with its distance a hair
    wider, since the light a small emitter throws on the wall it is mounted on lies within a cell of
    it and a hit moved out a cell drops it from every probe ray meeting the wall there,
    RENDERING.md's term for the strip. A third step is read: a hit within a cell of a glow light's
    box takes the light's irradiance as its mean over a cell square about it on its plane, 2 by 2
    points at a quarter cell, keeping the energy step 2 dropped while lowering the peak one fixed
    ray catches; kept only if the rule holds, else the blotches are named as the term in
    RENDERING.md with the three steps' numbers and the item's blotches part stops there. Step 3 read
    at 00:13: it holds the rule to three places, every view level at 160 by 90 but the strip's room,
    which narrows from 0.233 to 0.217 at Medium and 0.213 to 0.201 at High, and at 800 by 450
    narrows by 0.016 at High and 0.008 at Low with the rest level, the Cornell box at Low wider by
    0.00006 at five places, the GPU times level; but the blotches stay, the scratch room's spots
    from 8 to 7 and the round spots on the wall and ceiling as before. So the blotches are named as
    the term in RENDERING.md, a small emitter's near field read by a few fixed rays, with the three
    steps' numbers, and the blotches part of the item stops there; step 3 is kept as a mend of its
    own for the strip room's near field, committed apart from the term with its numbers, since it
    narrows a view and holds the others at the test's precision. The item's other faults follow, the
    edge leak, the stepped edges, the noise, the z-fighting and the shift. The near field mend at
    `d303032d` and the term at `5af77a40` on 2026-10-24; the blotches part done as a term, the edge
    leak next. Read by the coder at 02:38 on 2026-10-24 in Blockout at Medium, field 2 cascades of
    0.25: round spots about 40 px across on every wall, the floor and the stairs at all headings,
    its lamp a point light of 7 half a meter under the ceiling with no glow light, so outside the
    sixth term's small emitter; read as the blotches' fourth form after the edge leak, the ceiling's
    hot patch a bright surface caught by a few fixed probe rays, against the term's words first and
    a mend under the covered-fault rule only where the words do not cover it. The edge leak read at
    03:2x on 2026-10-24 in the game's room at High, 4 cascades of 0.25: over 500 by 400 px about the
    edge's foot the brightest 21 levels open against 2 closed and 0 in the reference, the glow
    light's own march, since the signed field interpolates to zero or above along the line where two
    blocks meet so a march through the edge never goes inside, the cone estimate reading a grazed
    half-seen edge as a faint fan and a march starting within a tenth of a block crossing before its
    first step as the bright fan at the foot; a march-side share d over cone removed the fan alone
    at 0.05 to 0.33 ms and is dropped, and a resolve-side rule holding inside a cell outside every
    surface whose neighbors along two axes lie inside and whose diagonal lies outside, the
    checkerboard two blocks meeting at an edge leave, reads the open edge as the closed one at no
    cost per frame; the bounce rooms at both sizes and the build's cost running before a commit,
    under the covered-fault rule. The edge leak mended at `a8f34b35` on 2026-10-24 under the rule,
    the numbers in the head; the stepped edges next, the Blockout spots read on the way, then the
    noise, the z-fighting and the shift. The Blockout spots read at 04:2x on 2026-10-24 as the
    screen probes' own, smooth with gi.toggle screen off: three probes on the wall 48 px apart read
    0.40, 0.40 and 0.46 where the hemisphere's reference reads 0.57 to 0.61, each texel's one fixed
    ray meeting or missing the bright ceiling half a block over the point light, the filter
    spreading the differences into still spots; the sixth term's mechanism under another source, so
    its words widen to a bright patch narrower than a probe's texel read by a few fixed rays, the
    small emitter's near field and the ceiling over a point light its two forms, no new mend, one
    small commit. The term widened at `2dd40fe4` on 2026-10-24, the lamp 0.3 under the hall's
    ceiling of 3.2 beside an emissive panel; the blotches closed under the term in both forms. The
    stepped edges read at 04:2x on 2026-10-24 in the entry's lamp and pillar scene: the leaked
    light's toothed sides went with the edge leak's mend, and the lamp's shadow steps only under
    gi.toggle screen off, the path the game's capture took, where each pixel marches to the glow
    light's face halves itself, while the glow-only texels read smooth and the probes on waver
    mildly as the game ships; no mend, a line under the glow lights in RENDERING.md with the
    reading. The engine's shadow there reads wider and softer than the reference's, a reading no
    entry names, kept here for a later item. The line at `17f13440` on 2026-10-24; the stepped edges
    closed. The noise next. The noise read at 04:2x on 2026-10-24 in the bounce rooms 3 to 6 at
    High, 800 by 450, with the probes on and left out beside the references: the screen probes'
    still spots, nothing moving, two frames 60 apart matching spot for spot and a still view
    changing 0.03% at most; view 4's sunlit floor under the window a bright patch narrower than a
    far wall probe's texel, 9 spots against 6 without the probes, view 6's strip the near field, 16
    against 7, views 3 and 5 their own edges either way; no mend, the term gaining the line and the
    sunlit floor as its third form. The z-fighting next. The noise line at `783213f3`. The
    z-fighting's first reading at 04:3x: no coplanar faces fight in the bounce rooms' nine views,
    and two patterns of the global illumination look like it stretched, the screen probes' scallops
    and tile patches on the grazing floor's view with the probes on, and sawtooth teeth along the
    walls' junctions at the world probes' spacing with them off; weighed in that order, and the
    game's own scene read at the owner's view for a true coplanar fight and the shadow map's acne
    before any mend. Read in the game itself at 04:29: no coplanar faces fight, and what the owner
    saw is the shadow map's, the acne of thin two-sided plant walls shadowing themselves in speckled
    stripes that shift as the eye turns, and the map's texels stepping on the pond, both standing
    with global illumination off and gone with the shadows off, the bounce rooms' pattern under a
    level; named in RENDERING.md under the lights and shadows, the plants' acne item 89, the pond's
    steps the game's own grid of eight a block by its Decision 29. The shift next. The z-fighting's
    line at `239bd67e` on 2026-10-24; the shift last. The shift read at 05:2x in the game's plank
    room with a lantern and two torches at hour 23: voxel.flicker's half-block steps change a frame
    by 0.27 to 0.30 levels with the probes and one frame by 0.73, the ceiling's round spots standing
    at new places after the step while the lamps move by parallax alone, nothing of it with the
    probes unread; the term's line on motion at `a2b4fe0f` on 2026-10-24, no mend. Item 71 done, its
    parts the near-field mend, the edge leak's mend, the sixth term with its three forms and its
    lines on the stepped edges, the noise, the z-fighting and the shift, held for the push.

72. **The sun's shadows and the scene field reach the far plane through a fill cascade each, not
    counted, with a distant ambient past the field's edge (Decision 55).** The owner asked shortly
    before 03:17 on 2026-10-21 why the global illumination in the voxel game ends at a range while
    the clouds go on to the far plane, and proposed that the last cascade fill from where it ends up
    to the far plane without counting in the cascade count, for the shadow maps as well as the
    field. The reading: the field's cascades each double the one inside them at the same probe
    count, four of 0.25 reaching 128 across, so the last ends 64 from the eye where the game draws
    384 across, and a surface past it reads no probe and is lit by the sun and the sky alone, the
    border scrolling with the eye; the sun's cascades fit the camera up to the shadow distance, 150
    by default, and nothing past it has a shadow, the contact shadows through the field reaching
    only as far as the field. The mend in three parts. First, the shadows: a fill cascade from the
    shadow distance to the camera's far plane, one more depth render of the scene at the tile's
    size, its texel covering meters where the surfaces are a few pixels wide, taken with
    `SetShadowFill(bool)`, on by default, and not counted in the cascades; a test that a shadow
    falls at twice the shadow distance with it and none without. Second, the field: a far cascade
    whose cell is sized so its extent reaches a distance the program gives, the camera's far plane
    by default, in place of doubling, with the same probe count and merged into the stack as the
    others, so a ray leaving the counted cascades reads it before the sky, taken with a parameter of
    `SetSceneField`, `farCascade`, off by default and not counted; its rebuild under the budget as
    the others, and a reading of the field's cost and memory with it in the Cornell box and in Ore
    at the game's distances. Third, the distant ambient: a surface past the last cascade takes a sky
    and ground hemisphere term in the field's absence, lit by the sun's shadow where the shadows
    reach, faded in over the outer tenth of the field's extent, so the field's edge never shows as a
    line on movement; a test reading a floor across the field's edge with no step in its light over
    a pixel, and a capture of Ore from a height with the field's edge in view, before and after.
    RENDERING.md says the three, the guide's sections on shadows and on the field say the calls, the
    cheat sheet lines through the script, the surface listing and the upgrading entries. After item
    71's second part. Three commits, one a part. Given at 02:33 on 2026-10-24 to the cloud session
    on `modeling` after item 2's commit, the tests on the null device and lavapipe there, Ore's
    capture from a height the local coder's at the cherry-pick; its order against item 71's second
    part is the cherry-pick's, no upgrading entries (Decision 64). The first part on `modeling` as
    `e164ccaa` under the owner's identity after the branch's merge of main at `147a40bc`, 18 files,
    read at 03:11 on 2026-10-24: the fill cascade fitted to the sphere about the views' slice from
    the shadow distance to the far planes, drawn into a depth image of its own of one tile after the
    map and bound beside it, the white texture in its place with no fill, its matrix and texel width
    in the lighting buffer's slot after the counted cascades' and read as the cascade after the
    last, blended across the last's outer fifth, the contact shadows counting the cascades alone; at
    the default camera its texels 3.8 units against the last cascade's 0.11 and its bias 11.7, said
    in RENDERING.md, the overlay's shadow line and models.draws naming it; a square's shadow at
    twice the distance dark with the fill and lit without, two fit tests, and three ground tests
    past the distance setting the fill off since they hold the cascades' end; 521 passed on
    lavapipe. The field's far cascade under way. The third part's phrase read with the cloud session
    at 03:2x on 2026-10-24: the ground half of the hemisphere term takes the sun through the shadow
    map at the point, the sky half untouched by the shadow, the fade across the last cascade's outer
    eighth as CascadeEdgeTests holds it, the item's tenth a round word. The shadow read not at the
    surface's own point, where every face turned from the sun reads shadowed, but at the ground the
    face looks at, the point moved along its normal by a cell of the coarsest built cascade, nine
    taps more for the pixels past the probes alone, so a hill's shaded side takes the sunlit ground
    before it as it does inside the field; the cloud session's proposal, taken at 03:2x. The second
    part on `modeling` as `0e908bd` under the owner's identity, read at 03:48 on 2026-10-24:
    SetSceneField's farCascade and SceneFieldConfig.FarCascade add a far cascade of 64 cells a side
    after the counted ones, its cell as wide as reaching the distance ahead takes, held to the far
    plane, at least twice the last counted cascade's and rounded to a sixteenth of that so a far
    plane read from the matrix gives the same cells each frame, 0 by default; planned, built,
    stamped and budgeted as the others, finest first, with a cascade of probes after the quality's
    following the one before's directions; field.state naming it and the field's memory; a
    first-cascade probe toward a black wall 40 units off reading 1.14 of a sky of pi with it against
    3.14 without; on lavapipe the Cornell box's global illumination 222 to 263 ms for a third more
    rays and Ore's 445 to 509 for a quarter more, the memory 27 to 32.5 MB; 940 passed. The distant
    ambient next. The third part on `modeling` as `54d7c3e` under the owner's identity, read at
    04:18 on 2026-10-24: past the probes a sky over a ground, the sky's light from above on the
    share of the sphere over the surface's horizon and the ground's color, taken as the surface's
    own, times the sky's and each directional light's light on a floor, the sun's share read through
    the map at the point moved along the normal by a coarsest cell, nine taps for the pixels past
    the probes that face some ground, floors skipping it, faded across the last cascade's outer
    eighth; a test along a floor and a wall of 420 units across the last cascade's end, the wall's
    largest pixel step 0.88% where 2.32% before and the wall past the field 2.8% darker where 6.3%,
    the floor 0.27% either way, held under 1%, 1.5% and 4%; 954 passed on lavapipe. The item is
    whole on the branch, `e164ccaa`, `0e908bdf` and `54d7c3e`, to cherry-pick with Ore's capture
    from a height. The branch's own run of `54d7c3e7`, read at 05:15: SlangCompilerTests' model pass
    sets test fails on the three systems, the lights' set holding one binding more than its list
    since the first part bound the fill cascade's map at 37 and the list was not moved; a fix commit
    on `modeling` from the cloud session before the cherry-pick. The Windows Rendering part there
    was lost in GlowPillarTests at the runner's hang limit, Verdict 86's second commit. The fix on
    `modeling` as `14338c8f` after its merge of main at `22f5c0c2`, under the owner's identity; on
    main as `d03a0634`, `00968660`, `fbb0bf89` and `34e9a8a5` on 2026-10-24, held for the batch's
    push. Ore's picture at `522c662a`, the pair with the field at 2 cascades of 0.25 since Ore's own
    field reaches past its land; item 72 done, held for the push. The branch's run of the fix
    `14338c8f`, read at 06:50: Linux and macOS green with the two tests, Windows lost in
    GlowPillarTests at the runner's limit, which the branch lacks Verdict 86's mends for. The owner
    added at 10:59 that in a black cave the global illumination's qualities move where the ambient's
    fog color begins, near at Low and farther at High, and at a distance even High shows it; that is
    the counted cascades' reach by quality and the sky past them, which the far cascade answers once
    the game sets it with the package, a cave's far rays meeting the far cascade's cells before the
    sky, so the game's record carries the far cascade's setting with item 27.

74. **The scene field's builds are budgeted in triangles a frame, so many meshes arriving at once do
    not stall one frame.** Found by item 69's reading on 2026-10-21 at 05:07: with 500 sections of
    1,024 triangles pooled in one frame, the field's builds took 77 to 81 ms in that frame, 512,000
    triangles splatted into four cascades at once under a budget of four cascades, which is the
    splat's cost for so many triangles in one frame whatever kind of mesh they come from, so a game
    streaming hundreds of sections in at once, as the voxel game does on a teleport or a first load,
    sees that hitch. The mend: the field's budget counted in triangles a frame beside the cascades,
    `SetSceneField` taking it with a default read from the Cornell box and Ore so a frame's builds
    stay under a few milliseconds on the device the readings name, the pending meshes built over as
    many frames as the budget takes, nearest the eye first, and the cascades a frame as today where
    fewer triangles are pending; the guide's paragraph on the budget, the cheat sheet line through
    the script, the surface listing and the upgrading entry. Tests: 500 meshes pooled in one frame
    built over several with no frame's field time over the budget's worth, read through the
    profile's mark on the null device's counts and on lavapipe, and the nearest built first. One
    commit, after item 72. Given at 01:28 on 2026-10-24 to the cloud session on `modeling` after
    item 88, the test on the null device's counts and lavapipe there, the default's device readings
    the local coder's at the cherry-pick; its order against item 72 is the cherry-pick's. On
    `modeling` as `629c1643` under the owner's identity, 13 files, read at 02:32 on 2026-10-24: a
    cascade's build a job spanning frames, clear, splats nearest first up to the budget with a mesh
    at least, resolve, the passes reading the cascade as last built meanwhile and the unsplatted
    meshes stamped as boxes, a newcomer joining and a splatted leaver restarting the job, a finer
    cascade gone dirty taking a coarser job's place; SetSceneField's fourth argument and
    SceneFieldConfig.TriangleBudget, field.state answering the frame's splatted and waiting
    triangles, the default 32,768 the least power of two above Ore's 18,824 a frame; the builds in
    SceneFieldPlan.Builds.cs under N 1.3; tests of the 500 sections under the budget, on lavapipe
    5,565 ms at once against 312 at most over 27 frames; 159 lighting tests there. To cherry-pick
    after 88's, the default's device readings at it. The branch's run of `54d7c3e7` on macOS fails
    the device test of the five hundred meshes, the field times reading 0.0 ms there so the slowest
    frame is asked to be under 0; the test skips with its reason where the device gives no field
    time, or reads the counts alone there; a fix commit on `modeling` from the cloud session before
    the cherry-pick. On main as `4c629a7c`, held. The default read on the 4070 at 05:3x on
    2026-10-24 with the frames unlimited: the 500 sections take 41.8 ms in one frame unbounded, a
    slowest frame of 4.0 to 4.4 ms over 14 frames at 65,536 and 2.5 to 6.5 over 27 at the default,
    every frame a sixth of the whole at most; Ore at 4 cascades of 0.5 splats 18,824 a frame within
    the default, gpu.scene_field 0.62 to 0.68 ms either way; the Cornell box 180 triangles at 0.42
    ms, nowhere near. The device numbers go into the guide's budget paragraph in the picks' batch.
    The macOS test's fix in `14338c8f`, picked as `34e9a8a5`. The device numbers in the guide at
    `3a2ccc0b`; item 74 done, held for the push.

132. **A pass records two thousand unmoving draws for well under a millisecond.** From the voxel
    game's item 139, read at 15:14 on 2026-10-26, its ASKS.md entry of 2026-10-26. At the game's
    open view at 12 columns each section draw costs 3 to 5 microseconds of the main thread a pass,
    the depth pass about 670 draws, the camera 780, the mirror 420 and the cascades 400, 9.4 of the
    main thread's 13 ms, and at 16 columns the main thread bounds the frame at 21 ms against the
    GPU's 16.5, the sections unmoving between frames. The recording's cost read a pass and a draw on
    the game's open view at 8, 12 and 16 columns and on item 108's thousand meshes, then brought
    down by the batches of a pipeline and material recorded as one indirect multi-draw over an
    instances' buffer, or by a pass's commands kept while its set of draws and their meshes are
    unchanged and recorded again when they change, or by secondary command buffers recorded on
    workers, as the reading shows, so a frame's four passes over two thousand unmoving sections cost
    the main thread well under a millisecond each; the frames the same in picture, the references
    holding, the game's view read again with the package. One or two commits, after item 97 and
    before 131.

143. **Shadow casters are culled to each cascade's slice swept toward the sun.** From the voxel
    game's reading of a voxel engine in Rust on 2026-10-30, its ASKS.md entry of 2026-10-30, behind
    the cascades of Decision 75 with item 132. At 128 blocks the game's cascades draw about 1,300
    sections at 12 columns, 3 ms of the CPU and 2.6 of the GPU, where a hull of each cascade's slice
    swept toward the sun cut the reference's shadow triangles by half. Read by the cascade draws and
    the shadow pass at the village and the spawn before and after. One commit.

144. **A mesh over shader buffers packs its positions to 16 bits within its bounds and its indices
    to 16 bits.** From the voxel game's reading of a voxel engine in Rust on 2026-10-30, its ASKS.md
    entry, behind the cascades of Decision 75 with item 132. The game's sections are meshes over
    shader buffers at 24 bytes a vertex, 12 of float positions, 8 packed and 4 of color, so the ask
    is positions as 16-bit normalized within the mesh's bounds with 16-bit indices, a vertex from 24
    bytes to 18 and the indices halved, packed on workers; read by the game's mesh memory and the
    upload ms a frame while streaming. One or two commits.

131. **A thin two-sided face holds a narrower band in the scene field.** From item 129's reading of
    2026-10-26. The field holds a two-sided face below zero within half a cell either side, so at a
    cell of 0.25 two walls half a unit apart leave no gap and a probe between them reads a surface
    at its start, taking the walls' own light where the reference reads a tenth to a half; the wheat
    room's front walls read 44% over the reference close and 14% far at High with the screen probes,
    13 to 18% with them off. Read for the band's width a thin face needs, a narrower band for
    two-sided faces with the march still meeting them from either side, or a finer cascade where
    such walls stand, the cost on the bounce rooms and the voxel grove at both sizes and the leak
    tests holding, the closed room, the lamp room and the slab's tint; then the wheat room's walls
    within the world probes' spread of the reference, the Cornell panel's thin emissive edge read
    with it, the room pictured before and after. One or two commits, after item 97. Decision 75's
    second item, after 135; the march's step held to the narrower band near a sheet, or a finer
    first cascade where such faces stand, the choice by the reading; a debug view of a cascade's
    probes and the field's slice comes with it, and each reading's figure is kept as a test bound,
    for every item of the plan.

133. **The README's words under the header are the owner's.** The owner wrote on 2026-10-30 that
    below the README's header stands "A C# engine on SDL3, Vulkan, ImGui and Slang. Raylib's design
    over a Bevy-style ECS and scheduler, scripted with its own Unity-style behaviors. Implementing
    Radiance Cascades. [e3d.cs]", in place of the tagline there; STYLE.md quotes the tagline and
    follows, and every test or check that reads the README's first paragraph; the bracketed e3d.cs
    is a question put to the owner, a link to the e3d script or a plain mark, and stands as written
    until answered. One commit, small, documents alone.

134. **The swapchain is made again on a resize before the first frame.** From the voxel game's item
    142, read on 2026-10-30, its ASKS.md entry. A window opened at 1280 by 720 and sized to 2560 by
    1440 by the program before its first frame keeps a 1280 by 720 swapchain from the first frame at
    2.35 s until a present reports it out of date at 10.86 s, the game's loading screen drawn into
    the smaller swapchain for eight seconds; the renderer takes the resize event and makes the
    swapchain again before the first frame it presents, a test resizing before the first frame and
    reading the swapchain's extent, the window guide's line. One commit, small, after item 133 and
    before 132's mend.

135. **The screen probes stand in the world and keep their light from frame to frame.** Decision
    75's first item, from the owner's complaint of 2026-10-24 and RENDERING.md's sixth term. Sixteen
    fixed rays a probe, one a texel, so a patch narrower than a texel lights one probe and not its
    neighbors, and the probes stand on the screen's tiles, so the spots ride the view as it moves.
    The probes are anchored in the world, placed from depth on a world grid at their spacing and
    kept from frame to frame, several rays a texel over frames through a history that follows the
    anchors, the filter weighted by the probes' planes. Read by the plank room's spots, 8 today by
    the game's measure, a pixel's change frame to frame at its 99th part on a still and a panning
    view, the bounce rooms' distances to their references held, and the cost at High at 1080p. One
    or two commits, after item 134.

136. **The path-traced reference knows light through.** Decision 75's third item, worked before the
    pocket and the light-through items whose readings need it. gi_reference.slang has no light
    through, so leaves cannot be read against truth. A path passes a leaf by its share as the
    material's LightThrough gives it, in the reference and in gi.compare. Read by the canopy and the
    grove's floor against it. One commit, after item 131.

137. **A vertical face in a narrow canopy pocket takes the light that reaches it.** Decision 75's
    fourth item, from the voxel game's item 135. The cause is read first, likely RENDERING.md's
    third term, the coarse cascades' probes moved out of a space narrower than their spacing and the
    first cascade's rays meeting leaves before their interval. Each cascade's probes are placed into
    open space within their cell by the field, and the first cascade's rays carried past their
    interval where no coarser probe holds the space. Read by the pocket's face and the corridor's
    walls, 67 to 69% under today, against the reference of the item before. One or two commits.

138. **A crossing of leaves takes its transmission from the leaves' depth along the ray.** Decision
    75's fifth item. Item 100's slab, 20 across and 5 up, takes 0.32 of the sky's light at High
    against the geometry's 0.41, and the canopy costs 2.49 ms at High against 1.53 solid; the shares
    a march takes in half-cell steps against the depth a way crosses are read first. Each crossing's
    transmission comes from the leaves' depth along the ray, found from the field's sign changes and
    applied once a sheet, the march run only on rays that meet a leaf. Read by item 100's slab table
    within 5% of the geometry at Low and High and against the reference, and the canopy's cost. One
    or two commits.

139. **The field follows a changed world cell by cell, and a moving emitter lights what it passes.**
    Decision 75's sixth item. A changed mesh, a section after a block breaks, marks its whole
    cascade dirty, built again over frames within the triangle budget with its still meshes stamped
    as boxes meanwhile, and a moving emissive mesh lights nothing past itself unless small enough to
    be a glow light. Only the cells of the changed mesh's box are splatted again from the meshes
    over them, and moving emitters are splatted into an emission layer each frame. Read by the
    frames from a break to a settled field and one break's cost at 12 columns in the voxel game, and
    a moving lava block lighting a wall against the reference. One or two commits.

140. **Each quality is a GPU budget at a size, read at 1080p, 1440p and 4K.** Decision 75's seventh
    item. No quality has been read above 1080p and the screen probes' count grows with the window.
    Each quality is set by a GPU budget at a size, the screen probes held near 1080p's count with an
    upsample above it. Read by gpu.global_illumination at three sizes and three qualities in the
    grove and the bounce rooms, with their distances to the references. One commit.

141. **The far cascade costs a slice a frame.** Decision 75's eighth item, from the voxel game's
    items 27 and 141. The far cascade costs 1.6 to 2 ms with six-block cells at the render distance,
    the cause read first. The far probes are refreshed a slice a frame, and rays go only from probes
    whose counted rays left the field. Read by the cave's far light as at `f05ea96f` and its cost
    before and after. One commit.

142. **The merge's gain past the first bounce holds at one.** Decision 75's ninth item, outside the
    eight. RENDERING.md's first term reads the merge's gain at 1.08 to 1.27 a bounce. The cause read
    and the merge held at one, the bounce rooms' distances to the references and the Cornell box's
    bounces read before and after. One commit, after the eight.

## Verdicts

Verdicts 56 to 94 are settled, said under each in REVIEW-ARCHIVE.md; none is open.

## Decisions

Decisions 1 Commits stay local; 2 The package is `3DEngine` on nuget.org, the owner's own, numbered
from 5.0; 3 AGENTS.md's bullet and table row on SHARED.md are the owner's; 4 TODO.md's two Entities
entries are closed as decided; 5 A commit that takes something out of the public surface is the
owner's to number; 6 AGENTS.md's bullet and table row on NORM.md are the owner's, with the exception
N 7.4 makes; 7 A run that fails says what failed in a page, and the reviewing session is given no
log; 8 The page's repeated lines are warnings and errors; 9 `GetFontDefault` stays ImGui's
ProggyClean; 10 A render texture takes the window's samples; 11 The package ships no ReadyToRun
images, the author's own publish being the way; 12 Per-object motion blur and ImGui viewports are
wanted, after the standing items; 13 Audio falls back to a device of silence where there is none; 14
A document a game's author reads names no one who decided; 15 Text is shaped whole; 16 Global
illumination comes in four phases, a scene distance field first; 17 Every tonemapper BevyCSharp
offers is in 3DEngine; 18 Subsurface scattering is the next large item, the first of Decision 16's
spectral experiments; 19 5.2 is packed when every job is green, before subsurface scattering lands;
20 Three packages are bumped after 5.2; 21 The animated model's meshes in the world follow
subsurface scattering; 22 The bounce's quality comes before subsurface scattering goes on; 23 No
document or comment a reader sees names the owner or a session; 24 Content streamed on the go stays
in the ledger, and the browser waits for a game that asks; 25 A run that shows no window makes no
sound; 26 A game's session writes what the engine lacks in `.github/ASKS.md`; 27 The owner's eye on
2026-10-10 opens item 2 again, four things seen; 28 The owner's eye on 2026-10-11: the sky's light
leaking into the game's cave by the heading is a glitch to be found and fixed, and the overlay gains
a post-processing window; 29 The README's gallery is one scrolling row of pictures a folder, sorted
by folder, and the games' text under the table goes; 30 The e3d skill leaves `.claude` for
`.github/E3D.md`, and the gallery's pictures are one size; 31 `games/Pusher` goes; 32 The GPU named
nowhere a user reads, `Engine3D` renamed `E3D`, and the guide table opening with the cheat sheet; 33
The radiance cascades are said in the README and in a guide page of their own; 34 The examples'
counts stand under the Examples heading and on each folder's heading; 35 A push plays one game, and
the pack run plays them all; 36 A vertex stream of the game's own, in both engines; 37 A position is
every mesh's, and the packed data a stream beside it; 38 The macOS captures run in the pack run
alone; 39 No development on either engine until every job is green on every push and pack; 40 Two
workflows an engine, build and pack, the same shape in both; 41 The voxel game moves to its own
repository, Voxel-Sandbox, and a tiny sample stays under `games/`; 42 Decision 39's gate is the new
workflows; 43 Prose is technical and generic; 44 Development resumes on all three fronts, the
radiance cascades first; 45 The README's centered parts are markdown inside a centering block, since
nuget.org draws a README without its HTML; 46 The code fixes project stays; 47 A package's release
notes are the commits since the package last published on nuget.org; 48 The pack workflow publishes
through nuget.org's trusted publishing; 49 The reviewer starts the pack runs when a package is due;
50 The review's files are committed by the batch that comes next, and by the reviewer at most once a
day; 51 Modeling in both engines, from the kernel to the modifiers, grid prototyping first; 52 The
Actions bill is not weighed; 53 Animated captures as tooling for documentation and devlogs; 54 The
voxel game leaves the screen probes out, and the cascades' faults the owner sees are read and
mended; 55 A fill cascade to the far plane for the shadows and the field, not counted; 56 A
screenshot on F2 in every program; 57 What the engine learns from the voxel game; 58 F1 hides the
interface and F3 shows the debug overlay, in both engines; 59 A denoiser for the ray-traced tier of
global illumination, beside the cascades, in both engines; 60 What the owner saw in the voxel game
on 2026-10-23 that is the engine's; 61 The owner's cloud sessions take GPU-free items on branches of
their own; 62 Branches are named for their work, never claude/ or a random string, and work stays on
main where one session writes; 63 Every commit carries the owner's identity, and the cloud sessions'
branches were deleted for not carrying it; 64 Until a full release, both engines are used by nobody;
65 The two cheat sheets have one form; 66 A devlog under `.github/devlog`, with AGENTS.md's row for
it; 67 Holographic Radiance Cascades read and set aside until a three-dimensional form exists; 68
The loop and the ECS are both the engine's way, each where it fits, each in full under Decisions in
REVIEW-ARCHIVE.md.

69. **The window runs under X11 on Linux for the present.** The owner wrote at about 10:29 on
    2026-10-24 that the window looks better with SDL_VIDEO_DRIVER=x11 and asked that the engine
    enforce X11 for the present, with the reason that SDL3 draws its frame through libdecor, whose
    GTK plugin, installed on the owner's machine, draws a GTK3-style frame with rounded top corners,
    square bottom corners and its own drop shadow inside the app. Item 92; the user's own
    environment variable keeps precedence, and a session without X11 falls back to what SDL finds.

70. **A session saves the pictures of its work in the repository, N 4.8.** The owner wrote at about
    18:5x on 2026-10-24 that BevyCSharp's session had kept none of the day's radiance cascade
    captures, which they would have loved to see, the session answering that no document said the
    pictures in progress should be saved, and asked for the rule written where the sessions read it
    and the same in the three repositories, as the voxel game does and the engine partly did. N 4.8
    says it, one text in both engines' NORM.md and the game's, the engines' pictures under
    .github/assets/readings/ and the game's under .github/devlog/, each a WebP named by the date and
    the item in the commit of its work with the commit's sentence as its caption, DEVLOG.md
    generated from them.

71. **The devlog written as the work goes.** The owner asked on 2026-10-25 at 05:13 that the
    sessions write the devlog more often than at a day's end, though not at every commit. N 4.8
    gains the cadence, an item that changes what is seen ending with at least one captioned picture,
    a pair before and after where it mends and one a phase where it is built in phases, and
    DEVLOG.md regenerated by the review as each item is archived, the coder's next commit carrying
    it as it carries the review's files; the three devlogs were regenerated at 05:16 with the day's
    pictures and items. The same in BevyCSharp (Decision 64) and Voxel-Sandbox (Decision 47).

72. **The screen probes have no public switch.** The owner wrote to the engine's session on
    2026-10-25 that the screen probes stay on and that off is for debugging alone and not for a
    player, meaning the voxel game's option in its settings, and asked the review to decide whether
    the engine's public call SetGlobalIlluminationScreenProbes goes with it. It goes. A switch a
    program can set is a surface for a quality ladder, and the engine's global illumination quality
    is that ladder; a pass kept alive for a debugging toggle alone, the glow-only screen pass, is
    weight the engine need not carry before a release; and the owner asked why three switches stood
    for one thing. Where a game later needs the probes' cost cut, it is a quality level and not a
    switch. Amended at 09:39 on the owner's word, asked whether the probes are heavy and what a weak
    machine or a 4K screen gets. The glow-only screen pass stays as the Low quality's path, the
    cascades alone with the glow lights through the tiles at 1.2 ms in the room against 2.2 with the
    probes, and the probes come at Medium and High, so the quality ladder is the one knob and no
    switch stands anywhere; item 122 reads the probes at 1440p and 4K and scales their spacing with
    the resolution. Item 103 is the commit, the game's item 131 its side.

73. **The devlog carries notes beside the pictures.** The owner asked on 2026-10-25 at about 12:01,
    shown the table of the screen probes' cost at four resolutions from 3DEngine's item 122, that it
    be written in a devlog or something along those lines. N 4.8 gains the note, a reading worth the
    devlog's reader with no picture to carry it, a table or a few sentences, saved beside the
    pictures as a Markdown file named the same way, and DEVLOG.md carries it on its day under the
    pictures; the generator reads them, and the first note is 3DEngine's
    2026-10-25-item-122-resolution.md. The same in BevyCSharp (Decision 65) and Voxel-Sandbox
    (Decision 49).

74. **The engine's work focuses fully on global illumination by radiance cascades and the framework
    that supports it.** The owner wrote on 2026-10-30, with the README's new words naming the engine
    as implementing radiance cascades, to focus on GI radiance cascades fully for the engine and
    build the framework up to support that, asking for questions. The reviewer asked which comes
    first among the look, the cost and the framework, and whether the voxel game's ask of item 132
    runs beside it; until the answers, item 132's reading finishes as a reading and item 131, the
    field's band at a thin face, is next, the items of the cascades' look and framework laid out
    after the answers.

75. **Global illumination comes before every other engine item.** The owner answered on 2026-10-30
    at 19:3x, "Concentrate on GI for 3dengine". So the radiance cascades and the framework that
    carries them, the scene field, the cascades, the screen probes, the merge, the references, the
    readings and the debug views, are the engine's work until the owner says otherwise; the voxel
    game's item 132, the passes' recording, waits behind it, and the small items 133 and 134 go
    first since they are hours. The faults and limits recorded so far seed the plan, the screen
    probes' noise and shift on movement, the field's band over-lighting thin walls (131), a vertical
    face in a narrow canopy pocket taking about no light, the light-through crossing reading 22%
    under the geometry at five units up and its cost, the far cascade's cost in the cave, the
    quality ladder's cost at 1080p, 1440p and 4K, the field's update when the world changes, and a
    path-traced reference that knows nothing of light through; the coder proposes the order and the
    steps in a few lines each, the reviewer writes the items. The plan came the same evening and
    stands as items 135 to 142 with 131, in the order 135, 131, 136, 137, 138, 139, 140, 141, 142.

## Replies

Shared: a mesh that replaced a still one in place is not stamped as boxes while a cascade the
budget left for a later frame still holds the one it replaced, where a voxel game's section stamped
as its boxes filled the air above its ground in those cascades and a room's indirect light went for
a frame eight after a block broke (`SceneFieldPlan`).

Shared: a game reads the force a joint held its bodies with over the last step,
`GetPhysicsJointForce` (`fdcfd435`), the impulse Bepu keeps for the joint's own constraint over the
length of a substep, which a box of 3 kilograms hanging on a distance joint reads as its weight
within a percent at one substep and at four.

Shared: the screen's probes are looked up along the normal turned to the eye as the shading's is,
so the back of a two-sided face takes the probes standing on it, where along its front's normal it
took none (item 129). In a field of thin two-sided walls half a unit apart, two cells of a quarter,
the half-cell bands of the field's two-sided faces meet and a screen probe's rays take their own
wall's light, 44% over a path-traced reference, which item 131 reads.
