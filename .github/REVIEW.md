# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `ed089e37`, Verdict 73's mend, and `1e89515c` before it, item 30's part (j), with the
review's commits between. Verdict 73, the test alone: the scene reference reads a file's time fresh
at each placing and its reload check asks at most twice a second by a static timer across every
world in the process, run before the spawn passes, so when the check falls in the step after the
write it respawns the first two copies from the new version and the third shares their new mesh,
which the old assertion, comparing the third with the first as it then is, took for a shared old
mesh, and when the check does not fall there it passes, which fits one runner of two and reproduced
here with 0.6 seconds between the last check and the step; the test keeps the first copy's mesh from
before the write and holds the third against that, and asserts that the file system gives back the
time set, so a file system's fault would fail with its own message; the scene reference tests pass
three runs in a row, the suite 1,737. Part (j): the screen march takes a met surface only where its
face, from the depth around the hit, turns toward the ray, and a face met from behind is passed as
the depth is, the ray going on and the field taking it from where it last stood in front; green over
red left of the wall's edge goes from 0.973 to 0.829 on the back wall against the reference's 0.736
and from 1.437 to 1.080 on the floor against 0.987, the back wall probe's ray from 1,375 percent
over to 57 under, met in the field at 2.26, and the floor probe's two from 229 and 401 over to 4 and
20; at 800 by 450 no view widens past 0.0001 and views narrow by up to 0.0015, the room in (j)'s
state widening 0.0002 as its floor past the edge goes from 1 to 14 percent under, the green having
covered what the probes read beyond their rays' reach, 15 to 52 percent under along the back wall
probe's ways; at 160 by 90 three views widen within their pinned bounds, the grazing floor at High
from 0.0551 to 0.0578 as the ceiling probes lose four rays each met on the screen at nothing on
their own lit ceiling, 55 to 460 percent over, which uncovers the sixteen fixed ways, the corridor
at Low from 0.2145 to 0.2203 toward High's 0.2263 where its floor tiles read 87 to 94 percent under
before and after, a term of the corridor's own, and the Cornell box at Low by 0.0006 spread over the
box; the reflections' floor and balls read as before within 1 percent and the wall 10 under in both
builds; the pass costs 0.01 to 0.03 ms more in the room and 0.09 at High in the hall; a test holds
the back wall probe's ray past the wall's end with the lamp and the wall in (j)'s state, failing on
the head at 1,375 percent and passing with the mend; RENDERING.md records it; checked were the
strict build, the suite, 1,737, and lavapipe under the required layer, 78 passed and 10 skipped. The
reviewer starts the pack run with publish on again as the check of items 37 and 38 and of Verdict
73. (h)'s first reading finds the game's mesher culling the stone faces against a lamp block drawn
as its own cube, leaving open edges around the lamp's footprint that the field's splat puts in
front, which would explain both the probe moved into the stone and its not being found walled in;
the field's cells and the probe's rays at that corner are read next. Right. Next (h)'s mend, item
36, the sampling term's candidates with (g), the band and the grazing ceiling's uncovered ways,
Verdict 72, item 35, the parallax and (c). The pack run with publish on started again at 00:38 as
39336354745 on `80625742`, the first one cancelled by the reviewer at 00:39 since it could not pack
and held the queue; its push cancelled the build run of `d2de2766` with its Windows suite in its
68th minute, so Verdict 72's figure comes from the build run of `80625742`, running with the pack
run at 00:43.

Before it, `8e3ff67a` came, item 37's rule and nothing else, with the review's commit between. The
notes script leaves out a commit whose every file lies under `.github/`, judged by the files each
commit since the start names, a commit touching that folder beside anything else staying in, as this
one does through the build folder and the tests; the test gains two such commits, one on the review
and one on a workflow, and the notes stay the engine's three; BUILDING.md says it. On the feed and
the history the notes start after `33fd0804`, where `5.2.92` was built, and hold 5 lines, the
engine's commits since and none of the review's; the script and norm tests pass, 48 of 48. Right.
The reviewer starts the pack run with publish on as the check of items 37 and 38 (Decision 49). (j)
and (h) were read by 23:36, each a fault of a line said under item 30, and their mends come first.
At 00:12 on 2026-10-20 the pack run with publish on, 39327786571 on `d2de2766`, had its Linux suite
and examples job green, its captures, games and Windows jobs running, and its macOS suite red on one
scene test (Verdict 73), so its pack job will not run and no package comes from it; the push run of
the same commit had its Linux, macOS and examples green with Windows in its 40th minute, Verdict
72's figure to come. The next pack run with publish on comes after Verdict 73's mend. Next (j)'s
mend, Verdict 73, (h)'s mend, item 36, the sampling term's candidates with (g) and the band, Verdict
72, item 35, the parallax and (c).

Before it, `c9e1a5ac` came, item 37, and `4af3cdde` before it, item 38, with the review's commits
between. Item 38: the pack job takes the identity token's write permission beside its read of the
contents, in the job alone, a step with the feed's login action and the user `EggyStudio` right
before the push answers a key that lasts an hour, the push of both packages takes it where it took
the secret, and the input's description, the head comment and BUILDING.md say the run publishes
through the policy with its values and needs no secret, nothing naming the secret any more; the YAML
parses and the run is its proof. Item 37: the notes script reads nuget.org's flat index for the
newest version of the minor in `build/version.txt`, as a number, takes the commit from that
version's nuspec and writes the commits after it, newest first, a sentence a line with the rule
against naming who decided kept, falling back to the minor's where the feed answers nothing, lists
no version of the minor, gives no nuspec or names a commit the checkout lacks, its first line saying
which commit the notes start after and why where it fell back, which the pack script prints and adds
to the run's summary; on the feed today it starts after `33fd0804`, 15 lines where the minor's would
be 105; the test runs it on a scratch history with a feed given as a folder, the newest package's
commit and the three fallbacks with their reasons, in the script tests' file, which N 1.4's list
has, since a new file under that folder failed the rule. Checked were the strict build and the
suite, 1,735 passed with the one failure of N 1.4 before the move and the test and the norm tests
passing after it. The review's own commits reach the notes, the first line today being the review's
record of `7fe22abc`, so item 37 widens by one rule, a commit whose every file lies under `.github/`
left out since the package carries nothing from there, which takes the review's and the workflows'
commits by what they touch, the README, the guides, the license and the notices staying in; it comes
in a small commit before (j), and the reviewer starts the pack run with publish on right after it,
the first package published through the policy carrying notes of the engine alone. Right. Next that
commit and the pack run, then (j) and (h) read, item 36, the sampling term's candidates with (g) and
the band, Verdict 72, item 35, the parallax and (c).

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
    as it is; AGENTS.md's paragraph on controlling the engine from the command line and its
    documents table name the new place, which the owner's ask is the word for, as do BUILDING.md,
    STYLE.md's line on the skills, the README's line on the commands, the two links in
    `docs/driving-with-e3d.md` and the workflow's path filters, which drop `.claude`; the folder
    goes with the move, and `.claude/` joins `.gitignore` so the tool's own lock file there never
    enters the tree. A batch of its own, taken when the owner types the ask in the working session,
    which changes AGENTS.md, the ignore file and the tool's own folder only on the owner's word
    given there and has asked the owner for it, a right the reviewing session holds. Done at
    `7a69c936` on the owner's word typed there: `.github/E3D.md`, every reference moved, the folder
    gone and ignored.

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
    cascades, the indirect light of every surface, with a line on what that brings a game, and the
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

24. **The glow lights' shadow past a box's edge is a step where a penumbra belongs; done at
    `2a14ab13`.** The owner's picture of 2026-10-11 at 13:50 showed a zigzag seam on the voxel
    game's wall where the lamps' light falls past a pillar, and the game's pictures drawn again at
    `6f4c98bb` show it unchanged by part h's work since: across the seam the frame steps 26% of the
    light in 20 pixels where the path-traced reference rises smoothly over 170, the frame 0.79 to
    0.88 of the reference on the dark side and 0.84 to 0.98 on the lit, the seam's middle rows
    within 2 pixels of a line and teeth of 11 to 26 pixels at its top and foot, where the march
    passes the pillar's ends. The cause is the glow lights' march: a ray from the surface to the
    halves of a lamp's faces either reaches or is stopped, so the shadow a box throws is hard at the
    march's resolution and toothed where its steps cross the box's ends, and the wall's bounce from
    the lamp, which is most of its light there, takes that edge. The remedy is the distance field's
    own penumbra: along the march the least of the field's distance over the distance traveled gives
    how far the ray passes from the occluder's edge, and the lamp's light is scaled by it from zero
    at a grazing pass to one past a cone's width, the cone set by the lamp's face's size over its
    distance as a soft shadow is, so the shadow falls off over the width the reference shows and the
    teeth, which are the step's, go with the step; measured on the game's seam row by row against
    the reference, the step's 20 pixels to become a falloff within a quarter of the reference's
    width and the teeth under 3 pixels, the glowstone room and the bounce rooms' 27 cases read for
    what moves, the march's GPU time before and after, and the owner's eye on the picture drawn
    again. Done at `2a14ab13`, by a cause other than the cone's: the march's way out ended at the
    step that left the surface, so a way grazing an edge counted half a cell under it; ended where
    it left, the rise in 20 pixels is 5 to 7% where 22 to 24 and no teeth remain.

25. **The macOS captures move to the pack run (Decision 38); done at `9a3e2e2a`.** The macOS
    examples job of `test.yml`, every example captured on the macOS runner's GPU through MoltenVK
    and checked to draw without a validation error, runs on every push and is the longest job by
    far, 53 to 56 minutes where the suites take 17 to 38, and a push's Linux examples job captures
    every example on lavapipe already. It moves to where the soak, the storm and the measure against
    raylib run, the pack run alone: the job leaves `test.yml` for `examples.yml` behind
    `inputs.whole`, with its runner, its caches, MoltenVK and its artifact as they are, the pack job
    still needing the examples workflow, and the workflows' head comments and BUILDING.md saying
    what a push runs and what a pack run adds. In the engine's session's batch with Verdicts 54 and
    55.

26. **Two workflows an engine, `build.yml` on a push and `pack.yml` by hand (Decision 40), committed
    at `534a1c2b`, done on 2026-10-18 with the pack run of `4d5db9df` whole green after the push run
    of `683e6e9d`.** `test.yml` and `examples.yml` fold into `build.yml`, which has `push`,
    `pull_request` and `workflow_call` with one input, `pack`: the jobs `linux`, `windows`, `macos`,
    `digest`, `examples` (needs `linux`, 90 minutes, the push half of today's examples job,
    `step.py` given `--workflow .github/workflows/build.yml`), and pack-only `captures` (the Linux
    captures and the raylib measure, 150), `captures-macos` (today's `macos-examples`, 160) and
    `games` (the twelve games played, soaked and stormed from the package and the README walked,
    150); the Windows and macOS jobs keep Slide played offscreen, native and in a window, the README
    walk and the first game on every push, and play the other eleven in the pack run alone behind
    `if: ${{ inputs.pack }}`, Decision 35 on every system, their limits `${{ inputs.pack && 180 ||
    100 }}` and `${{ inputs.pack && 120 || 75 }}`. `pack.yml` keeps `workflow_dispatch` with
    `publish` and runs `build` (uses `build.yml` with `pack: true`) then `pack` (needs `build`, 30
    minutes, today's pack job whole, the version read in it). Both files: `permissions: contents:
    read`, concurrency `build-${{ github.ref }}` cancelling in progress and `pack-${{ github.ref }}`
    not, the paths filter as today, a head comment saying what a push runs and what the pack run
    adds, a time limit on every job with its reason (N 6.8). Every line naming the old files
    changes: `build/step.py:6-7`, `ScriptTests.cs:99, 103` (`build.yml`, so `captures-macos` joins N
    6.6's scan), `.github/BUILDING.md:25, 81-82, 182-191, 315-316, 335-341, 354-359`,
    `.github/TODO.md:352`, `docs/shipping-a-game.md:56-57`, `docs/compared-with-raylib.md:60`,
    `README.md:256-258`. Checked by `ScriptTests` and `NormTests`, a push run green with the three
    pack-only jobs skipped and the Windows job near 55 minutes, and a pack run green with its
    package, which are Decision 39's gate (Decision 42). The norm's and SHARED.md's lines are the
    reviewer's and ride with the commit. Done on 2026-10-18, the push run of `683e6e9d` whole green
    on 2026-10-17 and the pack run of `4d5db9df` whole green at 14:14 with its package, Decision
    42's gate.

27. **The voxel game leaves for its own repository (Decision 41), done at `b40a788d`.** One commit:
    `3DEngine.Game/` and `3DEngine.Game.Tests/` removed, `3DEngine.slnx:12-13` with them,
    `AGENTS.md:93-94` (the `games` row covers every game; the owner's word under N 7.4 is Decision
    41), `docs/global-illumination.md:42, 130` and `docs/materials-light-and-shadows.md:390` citing
    a voxel game built on the engine, Voxel-Sandbox, with the numbers as measured,
    `.github/RENDERING.md:2140` saying where the game lives; the tests' comments, `.gitignore:83`
    and the asks' captures stay. Checked by the strict build, `NormTests` and `grep -rn
    "3DEngine\.Game"` finding only the channel's files. The gate was met on 2026-10-18 (Decision 42)
    and the game's build has been green in its repository since 2026-10-17, so the item is open from
    14:19. Done at `b40a788d` on 2026-10-18, the two projects, the solution's lines and AGENTS.md's
    rows gone, the guides and RENDERING.md citing the game's repository; item 28 is open from 15:05.

28. **A tiny sample, `games/Ore`, a voxel landscape, a character and the light that bounces, done at
    `4c6630a4`.** A game like the others (`Ore.csproj` and `nuget.config` as Wick's, a `Program.cs`
    of some 150 lines, `OreCommands.cs` with `ore.status`, `ore.autopilot`, `ore.warp` and
    `ore.night`, a README in the games' form): a 48 by 48 landscape from an inline value noise,
    heights 1 to 12, grass, dirt, stone and ore, its faces against the air meshed into positions,
    normals and vertex colors of a plain mesh, since global illumination reads a mesh's colors, the
    ore faces in a second mesh with `Emissive` and `EmissiveIntensity`, the ground a static physics
    model, a first-person character through `CreatePhysicsCharacter`, `MovePhysicsCharacter` and
    `JumpPhysicsCharacter` with mouse look as Manor's, `SetSceneField(4, 0.5f, 2)` and
    `SetGlobalIllumination(GlobalIllumination.High)` with G stepping the quality as Wick does,
    `DrawBounceWindow` while B is held, a sun and N for night. It joins the lists of `build.yml`
    (the build-all, soak and storm lists and the Windows and macOS play loops), gets a
    `build/drive-game.sh` case (a walk, a jump, a capture), a `build/soak.sh` case, a capture step
    in the `games` job and `.github/assets/examples/ore.webp` through `build/examples-table.py`,
    thirteen games where twelve, BUILDING.md's count and the guide's see-also. The name is the
    owner's, Ore unless told otherwise. Checked by `NormTests`, `build/drive-game.sh Ore`, the soak
    and the storm, and the pack run playing it on three systems. Done at `4c6630a4` on 2026-10-18,
    with one change said, bare stone from height 9 up so the hills show ore; its last check is the
    pack run playing thirteen games on three systems. The last check is met on 2026-10-18, Ore
    played from the package on Linux by the games job of `32ce6f4e`'s pack run, on Windows by that
    run's suite and on macOS by `1398ed24`'s pack run's suite.

29. **The register: STYLE.md's rule and the sweep (Decision 43), done at `683e6e9d`, the figures
    Decision 43 adds at `7b2486a5`.** STYLE.md gains a section, Register: prose uses the technical
    and generic term, the engine's own where it has one, and no figure of speech or casual
    paraphrase, with the pairs the owner named and their replacements: the light that bounces
    becomes global illumination, radiance cascades where the technique is meant and indirect light
    where the effect is; gives off light and glowing become emissive; driving a running program
    becomes controlling or scripting it through the command line, and a drive a scripted run or a
    course. The colon joins it: STYLE.md's rule, which allows a colon before a list, tightens so
    that a running sentence holds none, the sentence recast or split where one appended an
    elaboration or a list, as in "makes the program the README opens with: a window with a turning
    cube"; a colon stands before a bulleted list, a code block or a command, and as a label in a
    table, and STYLE.md's check of colons joining clauses reads prose lines of Markdown as well as
    comments. The sweep covers README.md, `docs/`, AGENTS.md, every Markdown under `.github`, the
    comments of the code, the scripts and the workflows, and the sentences a program prints; 72
    files hold the first pair some 170 times, 20 documents the drive words some 80 times, and a
    rough count finds a colon inside a sentence on some 230 prose lines of the documents and 375
    comment lines of the library and the examples. `docs/driving-with-e3d.md` becomes
    `docs/command-line.md`, titled for the command line, its links following, which
    `DocumentLinkTests` holds; job and script names stay as identifiers (`build/drive-game.sh`, the
    captures named `-driven`) with their comments reworded, since their renames reach workflows and
    tests. The REVIEW.md, NORM.md and SHARED.md lines are the reviewer's and are swept already.
    Checked by `NormTests`, `DocumentLinkTests` and `git grep -i` for the pairs finding the
    channel's history alone. One commit. The game's repository did this first at its `b87e386`, and
    its STYLE.md's Register section and colon rule, with their checks, are the text to take, with
    the engine's examples in place of the game's. Two things it found of the engine's:
    `DrawBounceWindow` titles its window "Light that bounces", which becomes "Global illumination",
    and the API logger's warnings put the exception after a colon, which becomes a full stop. Open
    again on 2026-10-17 at 20:34 for the figures Decision 43 adds, a likeness in place of a
    description and an idiom in place of a term. In the manner of raylib stands in
    `3DEngine/Api/E3D.cs:4`, `.github/DESIGN.md:116` and `3DEngine.Examples/Program.cs:3`; out of
    the box in `3DEngine/Assets/Textures/TexturesPlugin.cs:61`; under the hood in
    `3DEngine/Assets/Models/ModelLoadExtensions.cs:47`; in the wild in
    `3DEngine/Assets/Models/Assimp/AssimpModelReader.Meshes.cs:224`; a great deal of in
    `3DEngine.Examples/Shaders/ShadersMandelbrotSet.cs:87`; a handful of in
    `.github/ARCHITECTURE.md:245`, `.github/TODO.md:227` and `build.yml:1197`; and whatever a sweep
    for such figures finds beside them, STYLE.md's Register section carrying the rule with the
    owner's example. `AGENTS.md:93` and the game's files leave with item 27, and the reviewer's line
    in NORM.md is reworded here. Done at `7b2486a5`, the ten places reworded and what the sweep
    found beside them, on the fly, for free, first-class, the -style likenesses and the figurative
    wire up, the README's opening and the package descriptions saying what the engine does and
    naming raylib for its function names and rlgl's batching alone, two AGENTS.md lines the owner
    approved in the working session, and a word in its literal sense kept, wires drawn, curves end
    to end and motion back and forth.

30. **The radiance cascades look as good as they can (Decision 44), the week's work, one part a
    commit.** The owner said on 2026-10-18 at 22:25 that the light the cascades give does not yet
    look as good as it can, and made it the week's focus; the owner's eye of 2026-10-10 and
    2026-10-11 (Decisions 27 and 28) named what was wrong then and items 22 to 24 mended three
    things since. First the reading, in a commit of its own: the examples that show the cascades and
    `games/Ore` captured on the development machine's GPU and on lavapipe at the frames their
    captures take, each thing that looks worse than it should named here as a part of this item with
    its picture, what it should look like and where in the cascades it comes from, the dark
    terminator rings, the spokes, the leaks, the noise of a wide penumbra and whatever else the
    pictures show, and the reviewer orders the parts with the owner's eye. Then each part is mended
    on its own, measured before and after and held by a test as Decision 27's were, the figures in
    WHOLE or the tests' comments (N 3.5), the pictures taken again where they change. The voxel game
    takes the mends through the package, so a pack run follows the parts that change the light. Read
    at `4fa51a9c` (RENDERING.md, "What the light looks like, read on 2026-10-18"), six parts, here
    in the reviewer's order, the most seen first and the parts of one mechanism together, the
    owner's eye to reorder: (a) Ore's ground beside the ore, the world probes giving it nothing
    since the first cascade's probe stands a unit above the ground and the block's light reaches it
    level or from below, the floor's upward face reading 0 against the reference's 0.028, so the
    screen probes alone light it and their filter blends the tiles into patches, the cliffs losing
    their orange alike; (f) the lamp outside leaking through the thin walls' corner, the world
    probes taking its light through walls thinner than their spacing, which the screen probes hide
    but at a tile's edge, a tick 3.25 times the wall; (b) and (c) the screen probes' placement, each
    tile's probe at the tile's middle up to 8 pixels off a corner lifting it from 0.60 to 0.76, and
    their rays toward the open side taking the first cascade along their way, the block's front 65
    percent over; (d) and (e) the world probes' blend, a dark patch at the moved wall's foot falling
    where the reference rises and patches of 0.84 to 1.14 across the far wall. Each part is mended
    on its own, one commit, measured before and after with `gi.compare` against the reference at the
    frame named and held by a test on that measure, the pictures of RENDERING.md's passage taken
    again as they change; the four things seen that are not the cascades' stay as read, the
    terminator's crescent being Decision 27's subsurface part. Toward (a), read at `29eb2eca`:
    `GlowLights.Carries` (`GlowLights.cs:57`) carries an emitter only where its box's longest side
    is within twice the first cascade's spacing, so Ore's one landscape-wide ore mesh is never
    carried and the ground beside each ore is left to the screen probes' sixteen rays a tile. Part
    (a) done at `de92a62b` on 2026-10-19, a mesh of many emitters cut into its parts and carried as
    glow lights by the faces each shows, the ground beside Ore's ore within 2 percent of the
    reference where it read 0.03 to 1.74 of it in patches, held by two glow light tests, the limit
    of 64 lit emitters said; parts (f), (b) and (c), (d) and (e) follow. Part (f) done at `f40133c0`
    on 2026-10-19, a pixel with no screen probe on a surface like its own reading the world's probes
    it sees through the field, the tick from 3.25 to 1.02 times the wall beside where the reference
    has 1.10, held by a bounce room test, at 0.07 to 0.09 ms of the scene pass; the corner's band
    past the screen probes still reads 1.41 against 0.96 since the world probes' own rays take the
    lamp through walls thinner than a cell, a remainder noted in the reading for the parts to come;
    parts (b) and (c), (d) and (e) follow. Read toward (b) and (c) on 2026-10-19 at 01:52, before a
    mend: the two causes the reading named explain a part of each. With the screen probes' light
    from beyond zeroed the block's front reads 0.151, so the rays meeting nothing in reach carry
    0.056 of the 0.082 excess and the hits the rest, and reading the light past the first bounce at
    the hits through the traced read changed nothing at 0.2 ms, reverted; the first cascade's probe
    in the angle of the floor and the block reads 58 percent over in its own rays' hits and its
    faces, the room's other first-cascade probes 14 under to 18 over and the second cascade's above
    the block 2 over, so the excess sits where probes read each other's light past the first bounce
    in a concave angle and the rays from beyond carry it to the block. At the corner, from the red
    wall out a hundred pixels of the back wall against the far wall, the frame runs 0.65 to 1.02,
    the frame without screen probes 0.49 to 1.02 and the reference 0.44 to 1.03, so the screen
    probes darken the corner less than the world probes across a band ten tiles wide, which a tile's
    middle 8 pixels off cannot account for, and neither the filter, the history nor 64 rays a probe
    in place of 16 moves it, a bias and not noise. The next step is an instrument in a commit of its
    own, `gi.screenprobe <x> <y>`, giving for the screen probe at a pixel each of its 16 rays'
    light, met on the screen, in the field or from beyond, against the path-traced light along the
    same ray, so the rays and the term at fault are named before a mend is chosen; then (b) and (c)
    in one commit where one cause serves both, measured and held as before, and where the cause is
    the world probes' light past the first bounce in concave angles, (d) and (e) are read for the
    same before their own mend. The instrument came at `d8b01672`, `gi.screenprobe` reading a screen
    probe's sixteen rays against the reference along each way and over its cell, and named two terms
    at fault in both (b) and (c), the light from beyond read around the ray's start and not its end,
    and the light past the first bounce at hits in a concave angle at about twice the reference,
    which is the mechanism (d) and (e) are read for. The mend of (b) and (c) reads beyond at the
    ray's end, from the probes around that point weighed by how far their rays reached toward it,
    then measures what the concave-angle term leaves, with (d) and (e) read for the same before
    their own mend, separate commits where the mends differ. Read on 2026-10-19 at 03:06, before a
    commit: the beyond term mended in the tree, the read taken around where the ray ended from the
    probes whose reach covers a point the ray passed, the screen march handing the field the point
    where it went behind the depth and the last point on the picture where it leaves it, which the
    reflections share, takes the block front's rays past their reach from 97 percent over to 3 and
    the far wall of the carried lamp's room from 0.84 of the reference to 1.00, that room's frame
    from 0.944 to 0.989 and the Cornell box's from 1.064 to 1.033, so part (e)'s level is mended by
    it and its patches are not, the wall's blocks spanning 0.85 to 1.12 of their mean where they
    spanned 0.94 to 1.12 before, a bright patch mid-wall and a dark band along its right edge, since
    the rays past their reach carry the world probes' blend from where they end and the wall
    inherits the grid's variation, and the floor at the moved wall's foot (d) moves from 0.171 to
    0.192 against 0.213, nearer and unmended; the faces of (b) and (c) do not move yet, the block's
    front at 1.65 with the pocket between floor and block carrying the concave-angle excess, its
    first cascade probe 85 percent over in its upward face against a one-bounce reference where its
    neighbors read 4, and the corner at 0.77 against 0.60, where the second cascade's nearest probe
    stands over the tall block with 182 of its 233 rays met before their interval began, which no
    visibility weighting recovers, and its screen hits 88 over by the concave term. A third term is
    the sampling, a probe's sixteen ways fixed by its normal and the filter blending tiles of the
    same ways, the reference along those ways against the whole hemisphere reading 22 percent over
    at (c), 26 under at (b), 21 under on (d)'s floor and 46 over on (e)'s wall. The reviewer's call
    is one commit a mechanism: the beyond term's commit at once, held by its test of the rays past
    their reach within 15 percent of their cells at the block front and the far wall, the corner
    left out of it with the reason in the comment, the figures above in the message and in
    RENDERING.md with part (e)'s level said mended and its patches not, its picture retaken, and
    (b)'s and (c)'s faces said unmoved, the slab test's tint bound widened from 2 to 3 percent at
    1.212 against 1.240 with the reason, the change reading each ray past its reach as the reference
    does along its way while the upper wall's probe's sixteen fixed ways miss the slab's red their
    cells hold, which is the sampling term (N 3.5); then the pocket's term, the light past the first
    bounce at hits in a concave angle, for (c) and (b)'s hits; then the corner's world probe over
    the tall block; then the sampling term, read with the instruments before its mend. The beyond
    term done at `33018b8b` on 2026-10-19, the block front's stray rays from 97 percent over to 3,
    (e)'s level from 0.84 to 1.00 with its patches left, both frames nearer their references, held
    by a bounce room test, the faces of (b) and (c) unmoved as said; next the pocket's term with
    `gi.probe` extended to place a face's excess in its own hits or the merge from the cascade
    above. The instrument extended at `ad5d3692` reads the pocket as the sampling term in the world
    probes and not a concave-angle effect, one way of 64 carrying three quarters of the probe's
    excess with the panel's hot spot standing for its whole texel every frame. Three mends were
    offered, each texel's ray turned within its texel from frame to frame on a stratified pattern
    with a history of the probes' light held by hysteresis and reset where a cascade moves, which
    removes the bias at the cost of a history volume a cascade and some flicker as the eye moves;
    256 rays for the first cascade as the two above have, which reduces the bias to a quarter at
    four times the first cascade's trace; or several rays a texel each frame averaged, no history
    and no flicker at a cost in rays. The reviewer's order is to measure the 256 rays first, one
    constant, the instruments saying how much of the pocket and of the faces it takes and the
    illumination pass's time and Ore's frame on lavapipe saying its cost, and then to mend with the
    turned rays and the history, which removes the bias rather than dividing it, the hysteresis
    measured so a lamp brought in reaches its light within a few frames and a still view keeps its
    calm, the several rays a texel kept as the fallback where the flicker is unacceptable; the 256
    rays stay only where they alone bring the faces within the test's bound at a cost the runner
    bears. Measured on 2026-10-19 at 04:09 and reverted, the first cascade at 256 rays took the
    block's front from 1.65 to 1.40 of the reference and the pocket probe's upward face from 93
    percent over to 27, left the corner at 0.77, moved the Cornell frame from 1.033 to 1.029 and the
    carried lamp's room from 0.989 to 0.979, and cost the illumination pass 1.29 to 1.38 ms in the
    box and 145 to 165 ms of Ore's 249 ms frame on lavapipe, a quarter of the bias left for a fifth
    more of the pass, so it is not kept and its figures go in the mend's text. A plain hysteresis
    would fail the two tests that hold the indirect light's response to a lamp brought in or put out
    within two frames, which a history blending a fifth of each frame once lagged by seven. The mend
    goes as a variant the reviewer approves: each texel's ray cycles through fixed sub-positions of
    its texel, one a frame, four by four for the first cascade's texels and two by two for the
    coarser cascades', each probe offset in the cycle; the merge keeps the last light seen at each
    sub-position and writes their mean, so the bias falls to a grid as many times finer at one ray a
    texel and a still view is constant by construction; a change is judged at the same sub-position
    against what it held a cycle before, by a share and an absolute floor to be measured, and resets
    the texel to the new light, so a lamp brought in or put out answers in one frame and the two
    tests stand; the reach the visibility weights read takes the same slots; memory is the cycle's
    count times the merged volume, some 4 MB a cascade. Its weak case, a lamp moving every frame
    resetting the texels near it to one sub-position's light while it moves, is measured with the
    carried lamp, and the mend is held by a test of the pocket's face and the two faces at their
    frames, the still view's change from frame to frame and the two-frame tests kept, the thresholds
    and the costs in its comment. Built and read on 2026-10-19 at 04:41, uncommitted, the
    sub-position variant takes the pocket probe's upward face from 93 percent over to 15 and its own
    rays' hits from 59 over to 6 under, the block's front from 1.65 to 1.41 and the corner from 0.77
    to 0.75, the two-frame tests standing, at four times the world probes' memory, 6 to 25 MB in the
    Cornell box at High; the 15 percent left on every face is the corner's term from the cascade
    above. Its flaw is the fill window: a change is judged only at a sub-position seen since the
    last reset, so in the sixteen frames after one a change landing on an unseen sub-position goes
    unseen, the emissive box's walls appearing at frame 7 reading 0.063 where 1.0 is read without
    it, the share logic then overshooting by 64 percent at frame 10 and settling by 17 where a test
    reads at 11, and judging an unseen sub-position against the reset's sample would reset every
    penumbra and edge texel each frame; a grazing floor view reads 0.188 against its bound of 0.15
    besides. Three ways were offered, a change signal from the CPU comparing this frame's lights,
    sun, sky, glow lights and rebuilt field regions with the last and resetting the texels within
    reach of what changed, a slower reset, which fails the two-frame tests, and several rays a texel
    each frame with no history. The reviewer chooses the rays, two by two a texel every frame,
    option 3: it quarters the bias as the 256 rays did, the block's front at 1.40 beside the slots'
    1.41 with the corner's term left for its own mend either way, and it has no history, no fill
    window, no flicker as the eye moves and no noise around a carried lamp, no signal to keep from
    the CPU and no memory grown fourfold, at a cost measured, 1.29 to 1.38 ms of the pass on the GPU
    and 145 to 165 ms of Ore's on lavapipe; the slots stay out of the tree, their figures kept in
    RENDERING.md as the way to the rest of the bias if the eye asks for it after the corner's term
    is mended, four by four rays being too dear for lavapipe. The grazing floor view's 0.188 is
    placed before the commit. The sampling mend done at `9b37e326` on 2026-10-19, four rays a
    direction in the first cascade taken together, the pocket probe's faces within 30 percent of the
    reference where its upward face read 93 over, held by a test, the block's front from 1.65 to
    1.39 and the corner unmoved at 0.77, at a tenth of a millisecond more of the pass on the GPU;
    the figures said of Ore's pass on lavapipe came from timestamps that claim more than the frame
    and are not trusted, the box's pass there going 87 to 100 ms and Ore's frame 249 ms either way.
    The corner's mend done at `b4543ab5` on 2026-10-19, screen rays past the first cascade sent the
    full 2.4 as those within it, the corner's screen probe from 86 percent over its hemisphere to 5
    and the corner from 0.77 of the reference to 0.68 against 0.60, held by a test, at 0.04 ms of
    the pass; what remains at (b) is the field's color blending the white wall into the red near the
    back wall and the light past the first bounce in the concave corner, said in RENDERING.md. The
    sampling term read on 2026-10-19 with the screen probe instrument at fourteen probes, the
    reference along a probe's sixteen fixed ways 39 percent under to 149 over the hemisphere, worst
    on the Cornell floor, the probes' own light 33 under to 46 over, often the other way since light
    past their reach is a texel's average, every floor probe sharing one set of ways since the frame
    comes from the normal, so the filter blends like with like and averages none of it away. Four
    mends tried, the ways turned per frame among sixteen places, which makes a still view shimmer by
    2 to 4 percent of its pixels' value a frame; turned per tile over four by four places, and over
    two by two, calm, the Cornell front 1.39 to 1.20 and the corner to 0.64, and at the bounce
    rooms' 160 by 90 views 2 and 6, the thin walls and the strip, move against their references by
    up to 0.04, five tests off by up to 0.024 and the wick frame apart in 3.2 percent of pixels; and
    thirty-six ways where sixteen, calm, the front 1.22, the pass 1.43 to 2.43 ms. Each gains in the
    Cornell box and each takes the far wall of the carried lamp's room from 0.99 of the reference to
    0.89 or 0.92, where the fixed ways' overread had covered the screen hits landing at 1.3 on the
    thin green wall's dark side while the reference sees the lit side behind it, a thickness fault
    of the screen hits. The reviewer's choice is no mend of the sampling term, since a term whose
    bias covers another fault's cannot be measured until that fault is mended, and widening five
    tests' bounds for a view moved by a mend is not N 3.5's measurement. The readings and the four
    mends' figures go into RENDERING.md in one commit with no shader change, and the order goes on
    to the screen hits behind thin walls, (e)'s level, then (b)'s color blend and concave term,
    (c)'s light past the first bounce and (d), the sampling term measured again after them with the
    tiled ways and the thirty-six as its candidates, recorded at `0d17cecc` the same day with no
    shader change. The screen's hits behind thin walls mended at `9c44401f` the same day, a ray
    meeting the surface only where the depth does not leap across the halving's step, the far wall's
    probe from 51 percent under the reference along its ways to 2 over, the wall's level to 1.06
    with the sixteen ways' overread uncovered, (d)'s foot from 0.187 to 0.201 against 0.213, the
    Cornell box and the bounce rooms' views as they were, held by a test, the march in
    `screenmarch.slang` under N 1.3. (b)'s blend read on 2026-10-19 with each hit's color in place
    of its light in the screen probe's report, the corner probe's red-wall hits within 0.22 of the
    back wall given a green of 0.15 to 0.16 where the red wall's is 0.012, 0.08 further out and the
    wall's own at 0.4, the field's normal right there; those hits read with the wall's own color
    would fall from about 0.56 to 0.34 against the reference's 0.25 over their cells, so the blend
    is most of their excess and the light past the first bounce the rest. The cause is in the field,
    each cell painted with its one nearest triangle's color, and a cell on the corner's diagonal as
    far from the red wall as from the white holding whichever won, which the trilinear read over 0.3
    cells takes in and no distance test can leave out, the diagonal cells passing every one; the
    paint pass knows each cell's nearest face direction in eight bits of its `uint`, and the resolve
    pass writes color alone. The reviewer's choice is the mechanism's own mend, the resolve pass
    writing each cell's face direction as one byte a cell, about 0.8 MB at High, and the hit
    shading, field and screen hits and the world probes' alike, blending only the cells whose face
    looks the hit's way, eight loads a hit where one filtered read was, falling back to the filtered
    read where no cell faces the hit's way; the reflections' read of the frame before is not taken
    for the diffuse light, since screen probes feeding back through the frame and specular
    highlights entering the diffuse light are a change of method that needs a measured reason of its
    own, read after the blend's mend if the concave term still shows. One commit, measured at the
    corner probe's red-wall hits, the corner over 50 pixels in, the Cornell frame, the carried
    lamp's room and a world probe near a corner, with the pass's cost at both qualities and the
    image's memory, held by a test. That mend done at `a52b125b` the same day, the faces in two
    single-byte images, the hit shading keeping cells whose face is within 60 degrees of the hit's
    normal with the filtered read as its fallback, the corner probe's red-wall hits from up to 0.105
    of green to 0.007 and from 24 to 139 percent over the reference across their ways to 12 to 31,
    the corner over 50 pixels in 0.66 against 0.60, the pass within 0.02 ms, held by a test; what
    remains at (b) is the light past the first bounce in the concave corner, the hits' red 1.36 to
    1.40 against 1.07 to 1.11 across their ways. Read and recorded at `12ef6f13` the same day with
    no shader change, the light past the first bounce at those hits 40 to 50 percent over the
    reference and the whole excess, the probes' own light within 7 to 9 percent along the wall's
    normal, the excess from reading probes 1.3 to 1.9 units out in more open light at a point in the
    corner where two fifths less arrives, which a blend of probes 2.4 apart cannot draw. The
    reviewer's choice is to measure the occlusion factor first, on the light past the first bounce
    alone, as the ratio of the hit's openness to the openness where the probes stand, read from the
    field's distances along a few cones, so an open hit beside open probes changes nothing, with any
    constant in it measured against the reference and said (N 3.5); it is judged with the screen
    probes on, as the frame has them, at the corner, the pocket (c), the wall's foot (d), which
    reads under the reference and must not fall, the carried lamp's far wall and the open middle of
    both rooms, which must not move, with the pass's cost at both qualities; the probes' weighting
    by the hemisphere they share is not taken, since these probes see the hit and their light is
    right where they stand, and a finer cascade is the last resort by its cost. The red wall's
    under-read of 0.86 to 0.95 with the screen probes on is placed in the same reading before the
    factor is judged, and was at 10:29, on the upper wall the sampling term, three red-wall screen
    probes reading 12 and 25 percent under their hemispheres on the upper wall and 35 over on the
    lower while each reads about 28 percent over the reference along its sixteen ways, the ways
    missing the bright directions toward the lit ceiling, so the upper wall's hits stand in the open
    room, the factor must read near one there and the upper wall's frame figure must not fall.
    Measured and not kept, recorded at `75d7af37` on 2026-10-19 with no shader change: the factor
    mends the corner's hits and the block's front and moves the open middle, the foot and the upper
    wall in every variant, and it reads how near a surface is and not how bright, a face lit by the
    red wall losing nine tenths of its light beside it, so it is rejected. Its reading found that
    the corner's first 12 pixels read 1.00 to 1.02 of the reference and the wall 50 pixels in 0.93,
    so (b)'s frame figure is that wall's under-read, the sampling term's, and that the frame's open
    middle is right by two errors cancelling, the world probes' excess of 9 to 16 percent at their
    own hits against what the sixteen fixed ways miss. Neither can be judged in the frame while the
    other stands, and (c) and (d) are figures of the frame, so the order changes: the sampling term
    comes next, its two candidates, the ways turned for each tile and the thirty-six ways, measured
    at this head with the distance to the path-traced reference as the judge at 800 by 450 by
    `gi.compare` and at 160 by 90 by the bounce rooms' views, a candidate out where it widens any
    view's distance at either size, the test's measured differences pinned again only where a view's
    distance shrinks and with the per-view figures as the reason (N 3.5), the thirty-six ways' cost
    said at both qualities, and if both fail the figures recorded and the choice made again. After
    it the light past the first bounce at the world probes' own hits, read by `gi.probe` against the
    reference independent of the frame, which is (c)'s term and what remains of (b)'s hits, then
    (d). Measured and both out, recorded at `3904d0f0` the same day with no shader change: each
    widens four views' distance to their references at both sizes, the thin walls, the strip, the
    grazing floor and the window, where the walls that read over with the fixed ways read further
    over, the sixteen ways' under-read covering the world probes' excess at their own hits on those
    walls, while the Cornell box, the corridor and the carried lamp's room narrow and the far wall
    comes to 0.93 and 0.94 with its blocks within 0.87 to 1.07 and 0.94 to 1.04 of their mean; both
    are better samplers held back by that excess. Next the light past the first bounce at the world
    probes' own hits, read by `gi.probe` against the reference independent of the frame, the term
    named by what carries it before a mend is proposed. Named at `e298f4c4` the same day with no
    shader change: near the walls the world probes' first bounce reads at or under the reference and
    the light past it 14 to 39 percent over, a gain of 1.08 to 1.27 a bounce in the irradiance a
    probe's hit reads from the probes around it, compounding through the probes' own hits; and apart
    from it the merge reads over at the first bounce in the pocket and around the carried strip
    through the cascade above, by up to 2,521 percent along those ways. The gain is named by where
    it shows and not yet by its cause, which is one of three, the constant that turns the faces into
    irradiance and irradiance into the hit's light, the probes read standing in more light than
    reaches the hit on its wall, or a face holding the wall's own outgoing light read back into it,
    so one more reading comes first, at one hit on the thin wall, one on a grazing wall and one on
    the red wall, the irradiance the hit reads against the reference's irradiance at that point from
    one bounce and the same read 0.3 off the wall, and the constant alone in a closed white room
    under the sky at one bounce, which tells the three apart; then the mend of the cause found, one
    commit, measured at the seventeen faces and the frame as before; then the merge's first-bounce
    excess at the pocket and the strip as its own term; then the sampling term's two candidates
    again, then (c) and (d). Read at 11:58 on 2026-10-19 with a scratch test that stays out of the
    tree, recomputing a hit's read on the CPU and tracing the reference's one-bounce irradiance at
    the hit, 0.3 off it and at each probe's own place: the constant holds, a white floor under a
    uniform sky reading 1.00 of the reference at the floor and off it and the probe's faces 1 to 2
    percent over; the wall's own light is ruled out, the read against the probes' own places 0.97 to
    1.07 on the red and the white walls alike and the red wall's gradient the smaller; and the cause
    is the gradient, the probes right where they stand and standing where 11 to 64 percent more
    light arrives than at the hit, 1.13 at the thin wall whose probes are 0.7 to 1.2 off, 1.15 at
    the Cornell back wall's middle and 1.56 by its corner with the second cascade's planes 1.1
    behind the wall and 1.3 before it, 1.63 at the grazing walls, 1.08 under the window's ceiling,
    the thin room at 1.08 a bounce and the Cornell box at 1.27 differing in that alone. The lean of
    0.5 recomputed at 0.1 and 0 brings the grazing walls to 1.07 and 0.86 and raises the window's
    ceiling and the back wall off it, and moves the Cornell walls not at all, since no probe stands
    near them; the facing weight dropped takes 3 to 10 percent off everywhere. The reviewer's choice
    is the probes placed against the walls, a probe whose grid spot lies behind a wall within its
    spacing moved to the wall's front so every wall has a plane of probes beside it, the move rule
    widened from its 0.45, measured together with the lean toward none, each kept by what the
    measurement says; a finer spacing near surfaces stays the last resort. Conditions: the blend
    weighs a moved probe by where it stands or the move is held within what the grid's weights bear,
    said which and why; the facing rule holds for a moved probe; a still view's frame-to-frame
    change and the carried lamp's moved wall are read for a probe that crosses a wall as it moves;
    and it is judged at the seventeen faces by bounce, the frame's figures, the bounce rooms' views
    at both sizes against their references by the standing rule, the screen probes' corner hits,
    which read the same probes, and the pass's cost at both qualities, with a test holding the gain
    at the faces. The thin room's low point, whose probe sits in a pocket and reads 0.23 the other
    way, is the pocket's term, (c), after it. Measured by 13:08, against the head at both sizes, at
    seventeen faces by bounce, at the wall hits by the recompute and in still views: the probes
    crossing the walls toward the eye with tent weights by where each stands works mechanically and
    is out, widening the grazing floor by up to 0.034 at both sizes, the strip, the corridor and the
    window, moving the gain little, 1.14 to 1.27 to 1.09 to 1.19, and reading the Cornell corner's
    hits worse, 1.22 to 1.60 and 1.56 to 1.70, since probes brought within 0.3 of a concave corner's
    walls stand where the reference itself is 1.6 to 1.8 of the light at the hit; the lean at none
    takes the gain to 1.05 to 1.17 in the Cornell box, 1.00 at the grazing walls, 1.05 under the
    window and 1.03 to 1.06 at the strip, narrows the window by up to 0.066, the strip by up to
    0.045 and the thin room, lifts the wall's foot from 0.200 to 0.219 against 0.215, and widens the
    grazing floor at Low alone by 0.08, because the blend toward the ambient light past the last
    cascade takes its point by the same lean and at Low the grazing room's far wall sits in the last
    cascade's band, taking the closed room's absent ambient light. Under measurement the lean at
    none with that blend keeping a half-spacing lean as a constant of its own, with and without the
    crossing, and the moved wall's frames across its restamping. Measured and recorded at `69c7e8cc`
    the same day with no shader change: the refinement narrows or holds every view but the grazing
    floor at Low, which widens from 0.092 to 0.140, and the carried lamp's room at Low by 0.007,
    lifts the foot to 0.219 and 0.209 against 0.215, which is (d), and takes the gain to 1.00 to
    1.08 everywhere but the Cornell corner's 1.17 and the pocket's 1.12, the merge's; the grazing
    room reads 9 percent under its reference at Low before any mend, its wall in the last of Low's
    two cascades with no cascade above to lend light, and the gain covered that. Two errors cancel
    in that room at Low as they did in the open middle, so by the standing principle Low's shortfall
    is read and mended first as a term of its own, the light the last cascade's rays and the blend
    toward the ambient give a closed room beyond their reach, judged at Low by the rule on the
    grazing room, the carried lamp's room, the Cornell box and the thin room and with High unmoved;
    then the refinement is measured again, expected to pass the rule once the room stands on its
    own, one commit each. The crossing is out for good, the corner's gradient being steeper within
    0.3 of the walls than a spacing out. After them the merge's first-bounce excess at the pocket
    and the strip, the sampling term's two candidates, (c) and (d). Read by 13:49 with a scratch
    switch holding the device's rays at every quality for the two instruments alone, out of the
    tree: the last cascade's reach is not the term, Low's last cascade reaching 2.4 to 9.6 and every
    way meeting a surface within it in that room, and the ambient blend fixed at one moving the room
    one point; of the seventeen points between Low's 9 under and High's 8 over, about six lie at the
    first bounce in the screen probes, Low's reading 9 to 23 percent under along their ways through
    their field hits and their reads beyond reach alike with a tile of 16 pixels where High's is 8,
    and High's the same probes 1 to 33 over, and eleven lie past the first bounce, where the world
    probes read within about 8 percent of the reference at Low and 12 to 37 over at High, so High's
    gain covers the room's first-bounce shortfall and part of Low's, and the refinement at Low
    lowers a part that is near right; the Cornell box, the thin room and the carried lamp's room
    read alike at both qualities. The reviewer's order: the screen probes' first bounce at Low
    first, a term of its own with a one-bounce reference, named by what carries it, the march's step
    or reach at the wider tile, the hits' direct light or the read beyond reach from Low's two
    cascades, and mended in one commit judged at Low on the four rooms with High unmoved; then the
    refinement measured again, with a reading of why Low's world probes carry little of the gain
    High's carry, so the lean's part at each quality is known before it is set to none for both,
    since a mend that lowers a near-right term at one quality is not yet the gradient's mend. Named
    by 14:06: not the tile, which moves the frame at one bounce by a point at either quality, not
    the hits' direct light, within 3 percent in every arrangement, but the read beyond reach, Low's
    second cascade sending one ray through each of its 64 texels, the sampling term four rays a
    texel mended in the first cascade at `9b37e326`, since 16 texels a side there make Low read as
    High. The mend measured sends four rays a texel where a cascade has 8 a side and one where 16,
    every probe 256, High unchanged by construction and to the last digit, Low's frame at one bounce
    from 5 under to the reference, and at every bounce Low's grazing room nearer its reference by
    0.018 and 0.037 at the two sizes, the other three rooms within 0.004, and the window wider by
    0.041 and 0.054 at Low and 0.040 at Medium, the strip by 0.013 and the grazing floor at Medium
    by 0.025, each widened view reading as High reads it region by region, the window's distance
    0.303 against High's 0.311 and the strip's 0.150 against 0.149, Low's one-ray sampling having
    covered High's own excess on the walls the gain lifts; at 0.05 ms on Low and 0.08 on Medium. The
    reviewer takes it, since a view that widens to High's own figure for its room uncovers a fault
    that stands at High already and is next in the order, which is the covered-fault principle and
    not a widening the rule is against; the test's bounds at Low and Medium are pinned at the new
    figures with the reason per view that each equals High's for that room (N 3.5), RENDERING.md
    takes the reading and the figures, the scratch switches stay out, one commit. Done at `a27fbdd0`
    the same day, the test's Low and Medium figures pinned with both figures per view. The lean's
    part is read there: the probes stand alike at both qualities and hold the reference at their
    places at High, 7 to 15 percent under at Low where their near-level rays end at the last
    cascade's reach with nothing past, so the half-spacing lean reads toward the brighter far plane
    and none reads the wall-plane probe, which at Low carries the missing light beyond as well.
    Measured on `a27fbdd0` and recorded at `50afce3c` the same day with no shader change: the
    refinement passes its check, the window and the strip narrowing at every quality, the foot at
    the reference, the gain 1.00 to 1.17, no flicker across the moved wall's restamping, and the
    grazing floor at Low still wider, its room 12 percent under, which by the rule names Low's own
    term, what a last cascade's ray that ends in mid-air takes in a closed room larger than its
    reach, the far wall 11.8 across where the reach is 9.6 and nothing past it at Low where High's
    third cascade lends the light. The reviewer's choice is that no ray of the last cascade ends in
    mid-air inside the field: its interval ends at the field's edge and not at a fixed reach, a ray
    going on until it meets a surface or leaves the field, where it reads the sky as any ray leaving
    the field does, so the last cascade hands over to nothing because there is nothing left to hand
    to; not a third cascade at Low, which changes what Low is, and not the same cascade's probes
    read at the ray's end, which is a feedback with a gain of its own. Judged at Low and Medium by
    the rule at both sizes on the four rooms, the grazing room's probes' faces at the first bounce
    against High's, High's rooms unmoved, and the cost at every quality in the Cornell box, the
    carried lamp's room and Ore's landscape, whose last cascade's rays leave the field, since the
    march is longer only where the room is; held by a test, one commit. Then the refinement once
    more, expected to hold the grazing floor at Low, its own commit; then the merge's first-bounce
    excess at the pocket and the strip, the sampling term's two candidates, (c) and (d). Done at
    `14ae5a02` the same day, the last interval's end the field's edge, held by a cascade edge test,
    Medium and High unmoved, Low's grazing room from 4 percent under to 7 over against High's 8 with
    its view at High's own figure, the one widening, under the covered-fault principle, at 0.03 to
    0.15 ms where rooms are larger than the old reach. Measured on `14ae5a02` by 16:02: the
    refinement holds the grazing floor at Low, 0.104 to 0.075 and 0.086 to 0.046, narrows or holds
    every view at 160 by 90 at every quality and every view at 800 by 450 but the carried lamp's
    room, wider by 0.002 at High and at Low there while narrower by as much at the other size, its
    red and back walls nearer and its far wall further over, the level 1.061 to 1.075, which is the
    fixed ways' overread the lean's lower read there offset; the foot at the reference, the gain
    1.00 to 1.17, still views calm and the moved wall settling over two frames. The reviewer takes
    it under the covered-fault principle, the widening lying in the far wall's own term, the
    sampling term's, which stands in the order with the level of 1.075 as its figure to mend, and
    the room narrowing at the test's size; the measure's repeatability on the head read twice is
    said beside the figures, so a difference of 0.002 is known for what it is. Done at `257006c7`
    the same day, the reads at the surface and the lean kept for the ambient blend alone with its
    reasons in the remark, the bounds pinned with both figures per view, the measure shown to repeat
    exactly, and the wall's foot at 0.214 and 0.219 against 0.215, which is (d) done. Read by 16:47
    with a scratch test at High, the light the merge lent along each way marked as taken from the
    cascade above against the reference from the probe's place, and each probe above with its own
    light along the way against the reference from its own place: two causes, neither of the three
    named. Around the strip the excess is the probe's own rays and not the merge, one texel of each
    of the two probes carrying 3.7 and 3.5 against 0.41 and 0.17, three of its four rays meeting
    nothing and one meeting the strip itself 3.3 out, where the hit is lit by the strip's own glow
    light from no distance at about 14.8 and stands for a quarter of the texel, which the reference
    leaves out since a carried emitter's emission does not light the surface it is carried for; it
    read as the merge's because the texel's mark said most of its rays met nothing, and the probes
    above hold their places' light right along those ways. At the pocket it is the merge's parallax,
    one way lending 0.650 against 0.250 where all four rays met nothing within reach, the probe
    above that carries most of its weight, 1.28 away and higher, right where it stands and meeting
    the ceiling 1.6 from the lamp along that way where the pocket's same way meets it 2.7 from the
    lamp, where the reference is a quarter as bright, the probes above lending their view from where
    they stand in place of the lower probe's against a near and strongly varying bright spot. The
    reviewer's order: the strip's term first, a hit on a mesh carried as a glow light taking none of
    that light's own light, one commit held by a test, judged at the strip's two probes by bounce,
    the strip's view at both sizes and the glow tests; then the sampling term's two candidates
    again, since their term stands in every frame and its patches are the most seen, with the far
    wall's 1.075 as their figure; then the pocket's parallax, read for its mend's form and cost
    first, the lower probe's rays carried to where each probe above's interval begins so the merged
    ray is one ray, or what else fits, since it is a term of the merge's method and not a fault of a
    line; then (c). Read further by 17:32: the strip's term is a leak through a thin face and not a
    light lighting its own surface, a probe's ray passing through the strip, 0.06 thick and held
    about its middle by the field, and meeting the wall 0.02 behind it, which the strip lights from
    almost no distance, where the reference's path stops at the strip and leaves its emission out; a
    margin on the glow light and a test of the hit's normal left the excess standing. The mend
    measured, a light whose box the ray passed through within a cell before the hit giving the hit
    none of its light by a slab test cast back along the ray, takes the texel's own light from 3.70
    and 3.49 to none and the two probes' faces at the first bounce from 209 and 204 percent over to
    5 and 9, moves no view outside the strip's room, and widens the strip's view at every quality by
    0.026 to 0.044, the room reading 14 percent under where it read as its reference, its walls 12
    to 18 under and its floor 18, the two probes 18 and 24 under past the first bounce where they
    read 12 and 6 over; a first form leaving out any light within half a cell behind the hit dropped
    the strip's halo as well and is out. So the leak's excess covered an under-read of the room's
    light past the first bounce that stands at every quality and is named by where it shows and not
    by its cause. The reviewer's ruling is to record the mend and read the under-read first, by the
    principle as first set, the covered fault mended before the one that covers it is judged, since
    the widening lands at no figure already known and the room would read 14 percent under for
    anyone until the under-read's turn; the leak's mend stays in the tree and is judged again after
    it. The reading: the strip room's light past the first bounce at its probes' own hits by bounce
    and by where the hits stand, against the reference, with the leak's mend in, so the under-read
    is named by what carries it, the strip's light carried as glow lights at the probes' hits, the
    probes' reads at the surface in that room, or the merge. Named by 17:37: the merge's part is too
    small to count, the ways from the cascade above holding at most 0.17 of a face's weight and 0.01
    to 0.08 of light against faces of 0.5 to 0.7; at the probes' own hits the light past the first
    bounce reads 18 to 24 percent under on the faces the walls take, and the first bounce there, the
    strip's light at the hits, is uneven, 19 under on one face, 5 to 18 over on most and 47 to 133
    over toward the front wall and the back; and at hits on the room's surfaces, read as the code
    reads them with the probes holding one bounce, the floor's middle reads 0.71 of the reference
    with the probes' own light at 0.64, the ceiling 0.87 with 0.85, the back wall 0.75 to 0.85 with
    0.95 and a gradient of 0.79 to 0.88 running the other way, the probes standing where 12 to 21
    percent less of the strip's light arrives than at the wall beside it, and the front wall and the
    floor by the back 1.52 and 1.69 with the probes over and the gradient over. So two terms carry
    it, the strip's light carried as glow lights as the probes' rays gather it from the surfaces it
    lights, 36 percent under in what the floor's probes gather from above and 15 at the ceiling's,
    over toward the front wall and the floor by the back, and the reads at the surface beside the
    strip where the gradient reverses; the floor's middle and the back wall carry most of the room's
    14. The reviewer's order: the glow light's light at a hit first, a first-bounce term with a
    one-bounce reference, read at hits on the ceiling, the floor, the front and the back walls at
    their distances and angles to the thin emitter against the reference, so the light a glow light
    gives a surface is named by its form, its falloff with distance, its angle to the thin face or
    its box's extent, and mended in one commit judged at those hits, the two probes' first bounce,
    the strip's view and the other glow rooms; then the leak's mend judged again by the rule; then
    the reversed gradient beside the strip, which is the gradient's term in the other direction and
    goes with the sampling term's candidates and the parallax as the merge's and the read's terms;
    then (c). Named by 17:45 with screen probes in the strip's room at one bounce against the
    strip's exact unhidden direct light in closed form: none of the three forms, the falloff moving
    at most 4 percent at the front wall, the angle nothing within four half diagonals where each
    face's light is exact, and the box cut into points past that within 1.6 percent; what carries it
    is the visibility march's cone, which runs from the hit to the box's middle as wide as the box's
    mean half side, 0.35 for the strip, the same in every direction, so from directly above or below
    the strip, whose middle lies 0.05 from the wall it is mounted on, the march runs along that wall
    within a cell and reads it as a blocker, about 0.6 seen, though the wall stands behind the strip
    and never between; the hits read the strip's light 28 percent under at the floor, 30 at the
    ceiling, 12 and 24 at the side walls and 38 to 39 directly above and below. The mend under
    judgment, one mechanism in the march, is a cone whose width toward a surface a step passes is
    the box's own half-width across the way toward that surface, its support along the field's
    normal at the step, 0.03 toward the strip's wall and near what it was for a cube, the march to
    the faces' halves keeping its own cone; the floor from 28 under to the reference, the ceiling
    from 30 under to 10, the side walls 10 and 21 under, a few hits in the ceiling's back corners 35
    under remaining, to be read as their own figure. The leak's mend is set aside out of the tree
    while this one is judged by the rule. Judged on `33fd0804` by 19:51, the scripts recovered and
    the ten references at 800 by 450 traced anew at 2,048 paths: the cone's mend repeats its figures
    at the room's hits, moves no view but the strip's, and widens the strip's by 0.003 to 0.005 at
    both sizes at every quality, High among them, the back wall and the floor nearer and the side
    walls and the ceiling further over, the two probes' first bounce at the leak's texel unchanged
    at 209 and 204 percent over and their light past the first bounce a point or two higher, the
    Cornell box unmoved but the pocket's panel-lit faces; so the widening is the leak's excess no
    longer offset by the hits' under-read, the room having read level at the head because the leak's
    excess at the probes' first bounce covered the hits' 28 to 39 under. Both mends together widen
    the strip's view by 0.019 to 0.036, the room 12 to 13 percent under, the probes' first bounce 6
    and 10 over and their light past the first bounce 17 to 23 under, so the leak's mend uncovers
    the room's light past the first bounce as before and the cone takes about a point of it. The
    cone costs 0.23 ms at High in the Cornell box, 1.44 to 1.67, and 0.08 in the strip's room, the
    field's gradient's six reads a step about 0.13 of it and the narrower cone's longer marches the
    rest at Low; forward differences, three reads where six, would change its figures and be judged
    again. The reviewer's ruling: both mends stay in the tree uncommitted, and the strip room's
    light past the first bounce is read first with both in, so neither covers it, and named by its
    cause, the reads at the surface beside the strip where the gradient reverses, what remains of
    the probes' gathering of the strip's light at their own hits after the cone, or another; if a
    line's fault, it is mended first and the two mends follow by the rule; if the gradient's, a term
    of the method, the leak and the cone are taken together under the covered-fault principle, the
    strip's test pinned at the new figures with the reason naming that term, which then stands in
    the order with the sampling term's candidates and the parallax. The cone's commit, when it
    comes, measures forward differences against the six reads first and takes the three where the
    hits' figures hold within their bound, the cost said either way. Named and recorded at
    `132ac219` the same day with no shader change: neither of the two, but the band of light the
    strip puts on the wall it is mounted on, narrower than a quarter texel, which the probes' four
    rays at their fixed quarter-middles pass over, so the floor's probes gather the band's light at
    a tenth of the reference; a term of the method, in the sampling term's family, shown by the
    strip set half a unit off its wall reading within 2 percent of a fresh reference with any
    shaders. So the leak and the cone are taken together under the covered-fault principle as the
    ruling said: the cone first in forward differences, three reads where six, which hold the hits
    within two points of the six reads' figures at about the same cost, its test holding the
    ceiling's hits over the strip against the closed form; then the leak with its remark on the
    reference leaving a thin emitter's emission out; the strip's test pinned at the new figures,
    0.235, 0.246 and 0.245 where 0.216, 0.213 and 0.209, the reason naming the band with both
    figures; the band in the order beside the sampling term's candidates and the parallax, the side
    walls' 7 to 8 percent under noted as their own. The cone done at `fb1d1f95` the same day in
    forward differences, held by a test at the ceiling over the strip, at 0.15 to 0.19 ms on High's
    pass in the Cornell box. The owner saw three things at 21:16 on 2026-10-19, playing the voxel
    game on `5.2.92` with the day's work in it, and they are the item's parts (g), (h) and (i), each
    read in the game first, with the game's coder giving the world, the place, the hour and the view
    where each shows, and then mended as one mechanism a commit as the parts before: (g) with the
    screen probes on the indirect light looks like depth fighting, a flicker read frame to frame in
    a still view at the 99th part of its pixels and by tile, the screen probes' jittered places, the
    depth leap test of `9c44401f`, the thickness of the march or the filter's blend across tiles
    among the candidates; (h) light leaks a little, read where it shows against the field's cells
    there, a thin wall held about its middle, a probe inside a solid, the merge's parallax or the
    band's family among the candidates; and (i) when a block is broken the indirect light goes blank
    for a frame and shows again with the field's next build, read in the frames around the break,
    whether the field is cleared before its new build is ready or the probes read a field that is
    not yet stamped, mended so the light of the frame before stands until the new field is read. At
    21:19 the owner added two from the bounce rooms example, read the same way: (j) in the carried
    lamp's room with the light in its third state and the green wall in its second, green blooms
    over the wall's edge into what lies beyond it, read at the edge's pixels against the reference,
    the filter's blend across a depth or normal edge, the field's color blend at the wall's end
    after `a52b125b`, or a screen ray's thickness at the edge among the candidates; and (k) in the
    ninth room the corners carry a light border, which the owner sees in many places and from the
    screen probes too, read at a corner's pixels frame by frame and tile by tile against the
    reference, the filter blending tiles across the corner's two normals, the probes read at the
    surface on both walls at once, or the hits' facing rule among the candidates, (b)'s corner
    reading of the day beside it. The order: the leak's commit, then (i), (g), (k), (j) and (h), two
    read as one where they share a cause, then Verdict 72's reading and mend, then item 35, then the
    sampling term's candidates with the band beside them, the parallax and (c). The leak's mend done
    at `fa607cb3` the same day, the strip's test pinned at 0.234, 0.245 and 0.244 with the band as
    the reason, the two probes' first bounce at 7 and 10 percent over where 211 and 206, the
    emissive blocks' room at High moved by 0.0004 where a probe's rays cross a neighbor's box. (i)
    done at `03e0bd36` the same day, a mesh that replaced a kept one in place not stamped as its
    boxes while a cascade the budget left for later still holds the one it replaced, the pair of
    blank frames gone in the game, held by a test. (g) read by 22:08 in the game at the birch trunk:
    five screen probes on the trunk read 4 to 55 percent under their hemispheres where the
    hemisphere's light along the trunk varies little, each tracking the reference along its own
    sixteen ways within 24 to 30 percent, so the fault is in which ways there are and not in what a
    ray brings, one way meeting sunlit grass deciding a probe's green and the ways beyond reach the
    blue, and neighboring probes casting the same ways err alike, so the patches stand still and
    span many tiles, which the filter's blend cannot even out; the jittered places would move them,
    the depth leap and the march's thickness touch one to six rays a probe at 0.005 of light at
    most, and the filter is what fails to hide them. So (g) is the sampling term, the screen probes'
    sixteen fixed ways, and goes with its two candidates, the ways turned per tile and the
    thirty-six, measured as one with the band; and since the gain that held the candidates back is
    mended and this term is what the owner sees most, the candidates move up. The order: (k), (j)
    and (h) read, then the sampling term's candidates with (g), the band and whatever of the three
    shares the cause, measured by the rule and committed where one holds, then Verdict 72's reading
    and mend, item 35, the parallax and (c). (k) done at `7fe22abc` the same day, the glow pass's
    blend with the frame before taking only texels whose normal lies within 60 degrees of the
    point's, the corner's line of dots gone, held by a test, every view holding or narrowing; it was
    the glow pass's and none of the three candidates. The order: items 38 and 37, then (j) and (h)
    read, item 36, the sampling term's candidates with (g) and the band, Verdict 72, item 35, the
    parallax and (c). (j) and (h) read by 23:36, each a fault of a line with its own cause and
    neither the sampling term's. (j): in the carried lamp's room with the lamp in its third state
    and the green wall in its second, green spreads past the wall's edge onto the back wall and the
    floor, green over red 0.934 where the reference has 0.736 and 1.351 where 0.987, gone with the
    screen probes off and standing with the history, the glow and the merge off, so it is the screen
    probes' own rays, the filter spreading it; a back-wall probe's ray past the wall's end is called
    met on the screen at 3.02 and shaded as the wall's lit face, 3.26 in green where the reference
    along the way holds 0.22, since the march takes a point within two steps behind the depth as met
    unless the depth leaps, and a ray passing behind the wall's lit face crosses no leap, so it is
    met from behind on a face whose normal points its way, the third candidate; a turn-away after
    the march takes the green down and then takes the world probes' light beyond reach, the back
    wall reading 32 percent under where 6, so the mend belongs in the march, a face met from behind
    counting as passing behind the depth, the ray going on and the field taking it from where it
    last stood in front. (h): in the game's case at night, a warm patch on the grass outside a stone
    wall against whose inside a glowstone stands, the probe whose grid spot is the corner where the
    wall's inner face, the floor's top and the glowstone meet was moved along the field's normal
    into the stone and kept there since the field read outside, holding light at 2,450 percent over
    its reference on one face and not marked as holding nothing as the probes inside the ground are,
    so the grass outside blends it in through a block of stone, the candidate of a probe inside a
    solid, and the owner's faint smudge with the screen probes on is likely this probe through the
    light beyond their reach; the mend is in the placing, a moved spot judged by the rays that leave
    it or a probe kept out of a solid at a corner. The reviewer's order: (j)'s and (h)'s mends
    first, one commit each by the rule with a test, since each is a fault of a line the owner sees;
    then item 36, the sampling term's candidates with (g) and the band, Verdict 72, item 35, the
    parallax and (c). (j) done at `1e89515c` on 2026-10-20, a face met from behind passed as the
    depth is, the green over the edge from 0.973 to 0.829 against 0.736 on the back wall, held by a
    test, three views at 160 by 90 wider within their bounds and the grazing ceiling's four rays met
    on their own ceiling uncovered as the sampling term's. Next (h)'s mend.

31. **An example shows raylib's picture and none of the engine's making, the gamepad's controller
    first (Decision 44), done at `29eb2eca`.** The owner said on 2026-10-18 at 22:25 that the
    gamepad example shows a controller of the engine's own making where raylib's example shows its
    picture, and asked for the right one, and for the other examples to be checked alike.
    `CoreInputGamepad.cs:25-26` loads raylib's `ps3.png` and `xbox.png`, which
    `build/fetch-raylib-resources.sh` fetches for a capture and a build copies beside the program,
    so what the owner saw is read first, the example run as the owner runs it, with a controller and
    from the package, and the picture it shows found, a stand-in drawn where the file is missing, a
    placeholder texture, or the capture's frame without a controller; then the example shows
    raylib's pictures wherever it runs, shipped under the examples' resources with raylib's license
    for them noted where the other fetched files' is, or fetched where the example is built, as the
    file's license allows; and every example is read for a picture of the engine's making standing
    where raylib's resource belongs, each replaced by the resource raylib's example loads, listed
    here. One commit, the captures taken again where they change. Done at `29eb2eca` on 2026-10-18,
    the two pictures kept under CC0 with the notice, SDL3's PlayStation names read, every other pad
    on the Xbox picture, the capture taken again, and the sweep finding no other stand-in.

32. **Particles that live in their emitter's space and stand where the program puts them, for the
    game's stars (ASKS.md, 2026-10-18), done at `f5035acd`.** The voxel game's stars are fixed
    points on a sky that turns with the hour, asked for as a particle effect parented to the sun's
    and the moon's places (its item 6), and the package's particles cannot be that: `Particles.Run`
    (`3DEngine/Rendering/Particles/Particles.cs:52`) takes the translation of the emitter's world
    matrix alone, so its rotation and its parent's turn reach no particle, each starts at a random
    point within the radius and is stepped on the GPU in world space, keeping its place in the world
    as the emitter turns. Two things. An emitter may keep its particles in its entity's space, so
    they move and turn with its transform and its parent's, the simulation's positions local and the
    world matrix applied where they are drawn; and a program may place particles at positions it
    gives in that space, living until it removes them, beside the random birth within a radius. Both
    reach the flat API and the entity API as the emitter's other settings do, an example shows a
    field of fixed points turning with a parent, a test holds a particle's place through a parent's
    turn, and the particles' guide says both; the game moves its stars to them when a package brings
    them, and `ASKS.md`'s entry carries the item's number. After item 31 and before item 30's parts,
    being a day's work that the game's item waits on, the owner's eye welcome on the order. One
    commit. Done at `f5035acd` on 2026-10-18, `Local` and `Points` on the emitter with the
    rotation's call in the flat API, two tests, an example of a turning sky and the guides; the game
    takes it from the next package.

33. **A setting turns the sun's contact shadows off, or leaves them out where the shadow map holds
    the point (ASKS.md, 2026-10-18), done at `a2df7bb4`.** The voxel game draws its sun's shadows
    stepped along the blocks' pixels (its item 7), and the sun's light at a point is the shadow
    map's share times the contact shadow's (`modelpass.slang:583`), the contact shadow traced toward
    the sun through the scene's distance field in the occlusion pass at half the window's size, out
    to 32 cells of the finest cascade, blurred with the occlusion and read by the fragment's place
    in the window (`modelpass.slang:293`, `ao.slang`), on wherever the field is built and the sun
    casts shadows (`AmbientOcclusionRenderer.ContactShadows`), and off only with the field, which
    global illumination needs; measured on a pillar one block across under a low sun, it darkens the
    grass 0.4 to 0.7 of a block past the hard edge. One thing. A setting of the lighting, reached
    from the flat API and the entity API as the shadow distance is, turns the sun's contact shadows
    off, or leaves them out where the shadow map holds the point and keeps them past its reach, with
    the reason in the guide; a test holds a hard edge with them off and the field on; the game turns
    them off when a package brings it. The far cascade's texel at the game's distance, 1.27 of a
    block's pixel where the near ones are 0.11 and 0.38, is the lesser thing the entry notes and no
    item. After item 32. One commit. Done at `a2df7bb4` on 2026-10-18, `SetContactShadows` with On,
    PastShadowMap and Off, a test holding a hard edge with the field on and the figures in its
    comment, the guides following; the game takes it from the next package.

34. **An ImGui draw command binds the texture it names, so a window draws a program's `Texture2D`
    (ASKS.md, 2026-10-18), done at `c6914ab3`.** The voxel game draws its items' icons once into an
    atlas (its item 9), which its hotbar draws as a texture and its inventory window, drawn with
    ImGui, cannot: `ImGuiRenderNode.Run` binds the font atlas's set once before its loop over the
    draw commands (`3DEngine/Gui/Vulkan/ImGuiRenderNode.cs:172`) and draws every command with it,
    the command's texture id read by none, so an image of a program's texture draws a part of the
    font atlas; the game draws the icons' pixels as 11,087 rectangles meanwhile, half a millisecond
    a frame with the window open. One thing. A `Texture2D` gives an ImGui texture id, through the
    flat API and the entity API as the fonts are reached, and the ImGui pass binds a descriptor set
    for each command's texture as ImGui's own Vulkan backend does, the font atlas's where a command
    names none, with a test drawing a program's texture in an offscreen window and reading its
    pixels, and the overlay guide saying it. After item 33. One commit. Done at `c6914ab3`,
    `GetImGuiTextureId` and the pass binding each command's texture's set, a test reading the drawn
    texture beside the atlas's text, the guides following; the game takes it from the next package.

35. **The README's centered parts are markdown inside one centering block (Decision 45), and
    nuget.org draws them.** The heading, the description and the banners at the README's head stand
    as markdown inside one `<div align="center">` with blank lines inside it, no `<h1>`, `<p>` or
    `<img>` of their own, and every other HTML line of the README is read for whether markdown says
    it, the tables of pictures staying HTML unless thumbnails at the shown width make a markdown
    table render alike on GitHub and nuget.org, measured first; checked on GitHub's rendering and on
    nuget.org's page of the next version uploaded, the gallery's script writing the same. One
    commit, after the cone's and the leak's and before the sampling term's candidates, with the
    game's README left as it is, since no package carries it.

36. **A draw through a program's own shader is culled by its mesh's box where the program says the
    vertex stage keeps the positions (ASKS.md, Voxel-Sandbox's fourth entry).** Read in the game at
    {now} on 2026-10-19: its terrain draws through its own shader, whose vertex stage keeps the
    mesh's positions, and the model pass gives a batch with a program's own shader no box to cull by
    (`ModelRenderer.Instances.cs:108`, `batch.Custom < 0`), so every pass draws it whole
    (`ModelRenderer.Culling.cs:104, 115`), the nearest shadow cascade, which holds the first 7.7 of
    the 96 blocks shadowed, drawing 563 sections against the camera's 627 at 8 columns of 16 and
    2,021 against 2,251 at 16, so the game's frame on the development machine's GPU holds 60 a
    second to 8 columns and falls to 21 at 16, the shadows at 23.5 ms and the main pass at 12.9
    where the illumination stays near 2, and the game holds its render distance at 8. One commit: a
    program may say of a shader that its vertex stage keeps the positions, so a batch drawn with it
    takes its mesh's box and is culled by it in every pass as the engine's own are, the default as
    it is since a shader may move vertices, said in the remark and the shaders guide; held by a test
    of a batch with such a shader culled from a cascade it lies outside of; measured in the game,
    the draws per pass and the frame at 8 and 16 columns before and after. After the sampling term's
    commit and before Verdict 72's reading; the game's entry gets its Review line.

37. **A package's release notes are the commits since the package last published (Decision 47).**
    The pack run's notes are the commits since `build/version.txt` last changed, the whole minor
    each time, 89 lines at `5.2.92`. The notes take the latest version of the package the feed
    lists, from nuget.org's index of the package and that version's nuspec, whose repository commit
    names where that package was built, and are the commits from that commit to the head, newest
    first, each commit's sentence a line as before; where the feed answers nothing, lists no version
    of the minor, or names a commit the checkout does not have, the notes are the minor's as before
    and the summary says why; the pack job's step says which commit the notes start from. Held by a
    test of the script on a scratch history with a feed answer given to it, and read on the next
    package's page. One commit, with item 36 and before Verdict 72's reading. Done at `c9e1a5ac` on
    2026-10-19, 15 lines where the minor's 105 against the feed's `5.2.92`, and widened the same
    day: a commit whose every file lies under `.github/` is left out, since the package carries
    nothing from there, in a small commit before (j). The rule is in at `8e3ff67a`, the notes at 5
    lines of the engine's alone.

38. **The pack run publishes through nuget.org's trusted publishing and no stored key (Decision
    48).** The pack job gains the identity token's write permission beside its read of the contents,
    a step with the feed's login action, `NuGet/login@v1`, given the nuget.org user name the policy
    belongs to, `EggyStudio` unless the owner says another, which answers a short-lived key, and the
    push step takes that key where it took the secret, the input's description and the head comment
    saying the run publishes through the trusted publisher policy and needs no secret, BUILDING.md's
    publishing section saying the same with the policy's values, workflow `pack.yml`, no environment
    and the package pattern; the step with the secret goes. Checked by a pack run with publish on,
    whose push the feed accepts, the first the owner starts after the policy stands. One commit, the
    pattern `3DEngine*`; at 22:54 the owner asked whether the pack could upload by itself, so this
    comes first, right after the reading in hand, with item 37, and items 36, Verdict 72 and the
    rest after. Done at `4af3cdde` on 2026-10-19; the first pack run with publish on is its check.

## Verdicts

Verdicts 1 to 55 are settled, the last two, the drawing leak test's resident bound and the Windows
suite's time, on the runs of `82b23f8c` on 2026-10-12, the leak test green on four Windows suites
and the suite's loss at its limit traced to a shader cache never saved after a failed job, mended at
`9a3e2e2a`; their numbers are not given again. Verdicts 56 to 62 are settled on the runs of
`fc5fdc97`, `534a1c2b`, `2a02ba3e` and `683e6e9d` on 2026-10-17, and 63 and 65 on the runs of
`704952c5`, 66 on the pack run of `a41cc801`, 67 on the pack run of `5a2d7df3`, 69 on the runs of
`1398ed24` and `5a2d7df3`, 64 and 68 on the runs of `5a2d7df3` and `a12206af` on 2026-10-18, and 70
on the runs of `a12206af` and `ad5d3692` on 2026-10-19, said under each. Verdict 71 is settled on
the runs of `33fd0804` and `fa607cb3` on 2026-10-19, said under it. Verdicts 72 and 73 are open.

56. **The pack run's examples job fails in the strategy game's step on a wait without a timeout.**
    On the pack run of `82b23f8c`, 2026-10-12, the step built Tactics from the package, played its
    opening and, after the order at tile 6 5, asked `frames.wait 300` with no `--timeout`, so the
    client gave up at its 30 seconds while the game, on lavapipe under the validation layer, was
    still running the 300 frames, and the step ended with exit code 6; Tactics.log holds no line at
    a warning or worse, so the game was well and the client was impatient. Manor's and the rally's
    steps give their long waits `--timeout 600`, and line 330 of `examples.yml` is the one wait of
    100 frames or more without it. One thing: `--timeout 600` on that wait, and the pack run after
    the push. Settled when the pack run's examples job passes the step. Settled on the pack run of
    `fc5fdc97`, 2026-10-17, the examples job passing the strategy game's step.

57. **Slide's process ends in its startup on the push's examples job, with nothing logged.** On the
    push of `82b23f8c`, the puzzle games' step opened Slide offscreen and asked `frames.wait 10`,
    and the client found the session unreachable, the process having ended after accepting the
    connection; its log ends at the renderer's resize complete, 1.09 seconds in, three offscreen
    images of 480 by 270 made, no exception and no warning, so the process died outside managed code
    or was killed. The pack run's examples job failed before the puzzle step, so the step has run
    once on this code. On the push of `cc473437` the Windows job's games' step found Slide's session
    gone at `input.touch`, the process still running, so it had stopped serving, its log ending at
    the same resize complete 1.27 seconds in; and on the push of `0c63cab5` the macOS job's step
    found a swipe up or down sliding no tile, the board unchanged, the log ending there too. Three
    systems, three shapes, one game, and the puzzle step passed on `fca652a1`'s push between them,
    so it is a race in Slide's own startup or in the scripted touch and not a system's. Slide is the
    flat API alone, `InitWindow` then a 128-pixel font, four sounds and the best score's file loaded
    before its loop, so the CLI server serves while no frame runs yet, and the step's first command
    meets a game that has not begun drawing. Read and mended at `b3895bd4`, two of the three: the
    macOS swipe was the script's drag read as a plain drag on a device under five frames a second,
    the game's gestures timed on its wall clock, and the script sets a sixtieth a frame before its
    gestures; the Windows session gone was a read of a session file while the game replaced it,
    which Windows refuses, answered as no session, and the read tries again up to four times ten
    milliseconds apart; the Linux exit is not reproduced, and the script asks the runtime for a dump
    off Windows so the next death names its thread. Settled when the puzzle step passes on two
    pushes and no Linux death comes, or the death's dump names its cause and that is mended. One
    push's puzzle step green of two, `ddb40469`'s, with no Linux death. The pack run's Windows job
    played every game green too. Settled on the push of `fc5fdc97`, 2026-10-17, the second puzzle
    step green with no Linux death.

58. **The macOS suite of the pack run is lost to a crash in MoltenVK's pipeline creation.** On the
    pack run of `cc473437`, 2026-10-13, the suite crashed after 1 minute 38 seconds with 1,392
    passed, a bus error on a runtime thread inside `vkCreateGraphicsPipelines`, reached from the
    model renderer's pipeline for the bloom's scene draw during `SceneFieldTests`' particles
    bouncing off a wall behind the camera, the dump written and read; the script's rerun by
    namespace passed every test, 1,640 with 25 skipped in 15 minutes, and the macOS suite passed on
    the push of the same code an hour earlier and on the four runs before. So the crash is the
    driver's, Metal compiling a pipeline under MoltenVK on the runner, seen once, and the job fails
    as a lost suite should. Read on the next macOS runs: a second crash in the same call names which
    pipeline and what the suite held, 1,202 MB here; one crash stays a note. Settled at two green
    macOS suites. One green macOS suite of two, the push of `ddb40469`. Settled on the push of
    `fc5fdc97`, 2026-10-17, the second green macOS suite, the pack run's a third.

59. **The drawing leak test's resident baseline is read before the heap census of the same app, so
    the census' footprint counts as growth.** The Windows suite failed the test's resident bound on
    the pack run of `9a3e2e2a` and on both runs of `b3895bd4`, having passed it on the five runs
    between, and the series the message carries since `9a3e2e2a` is the same each time: 10 apps 303
    to 312 MB, 20 apps 235 to 247, 30 apps 300 to 313, then a rise of some 20 MB to the hundredth,
    322 to 335. The twentieth app's figure is the low one because the resident memory is read before
    the collections, and the census of the twentieth app, `dotnet-gcdump` attached to the process,
    comes after the read and leaves its own footprint of some 60 MB in the working set for the rest
    of the run, so the difference of the last against the twentieth holds the census and the creep
    together, 75 to 88 against a bound of 50, and whether it passes depends on how much the census
    happens to cost on that runner. The heap is flat at 38 MB and no device object outlives its app,
    so no leak is in it. One thing: the baseline is the resident memory read after the census, the
    thirtieth app's or the twentieth's read again once the census is taken, said in the test's
    comment, and the message says both figures; the bound stays at 50 for the creep alone, which the
    series puts at 20 to 35 MB over eighty apps, and a creep near the bound is then a reading of its
    own. Settled at three green Windows suites in a row. Mended at `ddb40469`, the baseline read
    after the census. One green Windows suite of three, the push of `ddb40469`, the test passing
    with the baseline after the census. Two green Windows suites of three, the pack run of
    `ddb40469` the second. Settled on the push of `fc5fdc97`, 2026-10-17, the third green Windows
    suite in a row, the pack run's a fourth.

60. **Summit's reference frame was captured gray on macOS.** On the pack run of `b3895bd4`,
    2026-10-13, `ReferenceFrameTests`' frame of Summit differed from its reference in 77% of its
    pixels, gray from top to bottom, (83, 91, 79) in the bottom third against the reference's lit
    (186, 237, 161), every pixel of the lower two thirds apart, and the same test passed on the push
    an hour before and on every run since the reference came. The test loads the level and captures
    after a fixed settle of ten frames, with no `ready` of its own where the probe's capture waits
    on `IsReflectionProbeReady`, so a runner slower that minute captured the island before its
    meshes, its field or its light were ready to draw. One thing: the capture waits on the scene's
    readiness, every mesh uploaded, every pipeline made and the field settled, through a predicate
    the engine already answers for its pictures, with the settle after it, and the same for every
    reference frame captured after a fixed settle alone; the failing frame, kept under the results'
    reference-failures, is read where it was uploaded. Settled at three green macOS suites in a row.
    Mended at `ddb40469`, every reference frame waiting on `E3D.IsSceneLoaded` and its own ready
    before its settle. One green macOS suite of three, the push of `ddb40469`. Two green macOS
    suites of three in a row, the push and the pack run of `fc5fdc97` on 2026-10-17; the pack run of
    `ddb40469` between them was lost at Verdict 61's test, its reference frames unread. Settled on
    the push of `534a1c2b`, 2026-10-17, the third green macOS suite in a row.

61. **The script test's grow mode races its own two limits on a slow runner.** On the pack run of
    `ddb40469`, 2026-10-16, the macOS suite failed
    `TestScriptTests.A_Lost_Process_Is_Said_First_And_The_Suite_Runs_Again_In_Parts` in its grow
    mode: the test runs `build/test.py` on a stand-in that grows, with `--timeout-minutes 0.1` and
    `--memory-mb 200`, and expects the page to say the suite ended at its memory limit, and the page
    said it ended at its time limit after 6 seconds holding 196 MB, so the stand-in on that runner
    grew to within 4 MB of the memory limit when the six seconds ran out; the hang mode beside it
    passed, as did the other 1,641 tests, and the test has passed on every run before. One thing:
    the two limits are set so the memory limit comes first on any runner, the stand-in growing in
    larger steps or the test's time limit at a minute, and the test's comment says the race. Settled
    at two green macOS suites. Mended at `fc5fdc97`, the grow mode given a minute and the stand-in
    growing in random blocks. Settled on the runs of `fc5fdc97`, 2026-10-17, the push's and the pack
    run's macOS suites green.

62. **A graphics pipeline is made with an empty entry point name, and a lamp's light leaks into a
    closed room.** On the pack run of `534a1c2b`, 2026-10-17, the Linux suite failed one test of the
    1,684 run, the `GlobalIlluminationTests` fact that a lamp casting shadows bounces no light into
    a room whose walls close it out, twice over: the room read 232 where under 8 was asked, and
    under N 3.7 the engine logged five validation errors, `vkCreateGraphicsPipelines` given `pName
    ""` for a vertex stage whose module's one entry point is `main`. The push run of the same commit
    passed the suite on Linux two hours earlier and every suite since `fc5fdc97` passed the test, so
    the name is empty on some runs and not others, which is a read of the entry point's name that
    can come back empty, from a shader's reflection or a cache's entry, before or while the pipeline
    is made; a pipeline with no vertex entry draws nothing, so the lamp's shadow pipeline missing is
    what let its light into the room. Two things: where the engine takes the entry point's name
    from, read for what returns an empty string and when, and an empty name made a failure where the
    shader is compiled or loaded, with the shader named, rather than a validation error at the
    pipeline; and the suite's Linux job read for what differs between the push run and the pack run
    of one commit, the Mesa cache restored from the other run's job among it. Settled at three green
    Linux suites in a row after the mend. Mended at `2a02ba3e`: the entry point's and the
    application's names handed to Vulkan null-terminated on the stack where unpinned arrays were, an
    empty entry point refused where the shader is made, and a test making pipelines under forced
    compacting collections. Settled on the Linux suites of the push of `2a02ba3e` and of the push
    and the pack run of `683e6e9d`, 2026-10-17, three green in a row after the mend.

63. **Slide's process ends in its startup on the Linux examples job, after the swapchain's resize,
    with nothing logged.** On the push of `2a02ba3e`, 2026-10-17, the examples job failed at Slide's
    build and play in its fifth minute: `frames.wait 10` ended with 4, the session accepting the
    connection and saying nothing, its process ended, and the log's last lines the offscreen frames
    made at 480 by 270, the swapchain resized and the resize complete, as Verdict 57's Linux death
    ended on `88673244` and `c6579710` and never again through the runs since, which settled 57 on
    two green puzzle steps. The death leaves a dump since `b3895bd4`, the runtime's under
    `captures/dumps`, which the job uploads in `example-captures`, and that artifact needs a
    signed-in download. Verdict 62 found a pointer into unpinned managed memory handed to Vulkan,
    which a collection moved before the driver read it; a process that dies with nothing logged in
    the frames after the swapchain is made is what another such handoff does, so it is the first
    suspect. Two things: the dump read, its faulting thread and frame named here; and every handoff
    of managed memory to a native call read for its pinning, the span-to-pointer conversions
    Vortice's types make, SDL's calls given strings or arrays, the audio and input paths, with a
    test under forced compacting collections where one is found unpinned, as
    `PipelineEntryNameTests` is. Settled when the dump's cause is mended and the puzzle step passes
    on three pushes in a row. Read the same day without the dump: every `fixed` block of the Vulkan
    device, every create-info pointer, the native callbacks, SDL's icons, audio and strings,
    Assimp's file callbacks and ImGui's fonts hold their memory pinned or copied for as long as the
    native side reads it, the font bake from memory proven so under forced compacting collections of
    every generation, so no second handoff of Verdict 62's kind stands; and forty openings of Slide
    on lavapipe under the validation layer, each as `build/drive-game.sh` opens it, died in none,
    which leaves the dump to name the thread, SDL's audio thread the first suspect since the first
    sound plays at the opening click. On the pack run of `683e6e9d`, 2026-10-17, the examples job
    failed the same way in its fifth minute, `frames.wait 10` after a gesture ending with 4, the
    process ended and the log ending at the resize, where the push run of the same commit an hour
    before played Slide through, so the death is two of the four examples jobs under the new
    workflow, each after the click or the drag that plays the first sound, and two of them passed.
    What the page says excludes more than it names. The error names a dump when one is left and
    named none, so the runtime's crash path was not taken, which `DOTNET_DbgEnableMiniDump` takes
    for a fault on any thread, managed or not, saying so on standard error on the way, and the log
    holds that stream since `e3d open` sends both to it; a managed exception no one caught would
    have printed its lines there as well, and every end of the main loop that logs, the quit event
    and `AppExitPlugin`'s exit, logged nothing. What is left is a kill from outside the process,
    `SIGKILL` from the kernel's out-of-memory killer or from a `Kill`, twelve `dotnet build` runs
    having left their MSBuild nodes and compiler server alive in the step; a fault whose handler
    could not run, a stack overflow on a thread the runtime does not own, SDL's audio thread or
    lavapipe's rasterizer threads; or an end the engine takes without a line, the automation's
    window close, the main-loop driver's pump returning false and `CloseWindow` itself, which log
    nothing. So the artifact's dumps hold nothing, and the dump is asked no more. Three things in
    its place, each cheap. Every way the main loop ends logs one line naming it, the exit key, the
    driver's pump, the automation's close, `app.quit` with its caller and the quit event,
    `CloseWindow` logs that it was reached and `ProcessExit` the exit code, so a silent log means a
    kill. `build/drive-game.sh` on Linux and macOS opens the program through a wrapper that waits on
    it and writes its wait status, so `whereabouts` says the exit code or the signal there as it
    does on Windows, 137 for the kernel's kill, 139 for a fault and 0 for an end of its own. And a
    death on Linux adds to the error the kernel's last lines (`sudo dmesg | tail -n 30`, which the
    runner allows) and the processes holding the most memory (`ps -eo pid,rss,etime,args --sort=-rss
    | head`), and the step shuts the build servers down (`dotnet build-server shutdown`) after the
    twelve builds and before Slide is played. Settled as before, the cause mended and three green
    puzzle steps in a row. Mended at `7b2486a5`, the three instruments in place. Every way the main
    loop ends logs its reason the first time `WindowShouldClose` turns true, the exit key, the
    driver's pump or the automation's close, the quit event says whether SDL or the window asked,
    `app.quit` logs the process that sent it, the request carrying a caller field, on Linux with the
    parent process and its command line, `CloseWindow` logs its call and `ProcessExit` the exit code
    last, E3D.md saying a log that stops with none of them ended in a kill or a fault;
    `build/drive-game.sh` on Linux and macOS starts the game itself with `--serve` from the
    checkout, its output in its log, under a shell that waits on it and writes its wait status,
    readiness a `frames.wait 0` answered within a minute, and `whereabouts` names 0, 134, 137, 139,
    143 and the other signals; a death on Linux adds the kernel's last thirty lines and the ten
    processes holding the most memory to the step's output and the error; and the script shuts the
    build servers down after its own build, which covers the examples job's twelve. On the working
    machine Slide played through with its log ending in the quit, the close and exit code 0, and a
    `kill -9` sent after the resize gave exit code 137 with the log ending at the resize and no
    dump, the run's signature exactly, so a kill stands as the first suspect and the next death
    names its sender or the kernel's reason. On the pack run of `7b2486a5`, 2026-10-17, the examples
    job died the same way with the instruments in place, and they say what the page could not
    before. Slide's process ended with exit code 132, signal 4, `SIGILL`, an illegal instruction, so
    neither a kill nor a fault of memory; the kernel's last thirty lines hold no out-of-memory kill,
    the balloon driver naming 16 GB; the log ends at the resize as before; and no dump was left, so
    the runtime wrote none for this signal raised in native code, and the crash path the earlier
    reading relied on does not cover this death. In the same run the games job's Summit ended the
    same way within its first sixty frames, said by `e3d command frames.wait 60` through `step.py`
    with no exit code, since that step opens the game through `e3d open` and not through
    `build/drive-game.sh`; the push run's examples job of the same commit, on another runner an hour
    before, played Slide through. An illegal instruction in a process of this kind is one of two
    things, an instruction the runner's processor lacks in code made for another, or a trap, the
    `ud2` a compiler writes for a path it was told is unreachable, which LLVM does when built with
    `LLVM_UNREACHABLE_OPTIMIZE` off, as distribution builds often are, and lavapipe compiles every
    shader variant through that LLVM on the drawing thread as the first draw with it comes, Slide's
    after its first gesture and Summit's as its scene loads, where the suites, drawing other
    variants, have not died. Mesa's shader cache the jobs carry between runners is not it, since
    Mesa keys the cache on the processor's family and feature bits (`lp_disk_cache_create`, Mesa
    24.0 through 25.2), so a cache made on one kind of processor is not run on another. Two things
    name the instruction on the next death. The game runs with core dumps allowed, `ulimit -c
    unlimited` in the wrapper's shell and the kernel's `core_pattern` set in the job to a file under
    `captures`, since the runner's pipes cores to a disabled apport, and a death that left a core
    prints, through `gdb -batch` on the core and the program, the faulting thread's native frames
    and the instruction at its program counter (`x/i $pc`), into the step's output and the error;
    and every opening on Linux prints the processor's model name and flags (`lscpu`), on a pass as
    on a death, so the runs say which processors die. The game steps of the games job that open a
    game through `e3d open` directly, Summit's among them, open it as `build/drive-game.sh` does,
    through the wrapper that writes the wait status, so their deaths say their code too. Settled as
    before. The two asks are done at `2a9dbdcb`, `build/open-game.sh` opening every game a Linux or
    macOS job plays under a shell that writes how it ended and, on Linux, prints the processor at
    each opening and has gdb read the core a death leaves, which the drive, the step's page, the
    soak check and the storm say; an outside `kill -ILL` was found not to end a .NET game, the
    runtime's handler taking a signal another process sends, so the runner's SIGILL was a faulting
    instruction of the process's own. The next death names it. Settled as before. On the push of
    `2a9dbdcb`, 2026-10-17, the examples job died at Slide at 23:05 and the instruments spoke. The
    process ended with exit code 132 on an AMD EPYC 7763, and gdb read the core, the faulting thread
    one other than the main thread, its frames without a symbol and its return address zero, and the
    instruction at its program counter `vgf2p8affineqb $0x0,%xmm4,%xmm3,%xmm3`, followed by
    `vpandn`, a store of a 256-bit register to the frame at 0x520 and `vpcmpgtb`. `vgf2p8affineqb`
    is a GFNI instruction, a Galois-field affine transformation, which LLVM emits for a shift of a
    vector of bytes where the target has GFNI, and which Zen 3, the EPYC 7763, has not, Zen 4 and
    Intel's Ice Lake having it; code with no symbol and a frame of that shape is what lavapipe's
    compiled shaders look like, so a shader was compiled for a processor with GFNI and run on one
    without. Two readings. The code was made on another runner and reached this one through Mesa's
    shader cache, which the jobs restore from any earlier run's save (`restore-keys:
    mesa-examples-Linux-`), Mesa keying the cache on its own capability bits, which have no GFNI
    bit, so two processors of one family whose bits agree share the cache; or LLVM on this runner
    was told the processor has GFNI, by its CPUID under the hypervisor, and compiled for it there.
    The captures job of the pack run of `7b2486a5` had ten of 256 examples die the same death,
    `models_heightmap_rendering`, `models_first_person_maze`, `models_cubicmap_rendering`,
    `models_stress`, `models_reflection_probe`, `models_skybox`, `models_terrain`,
    `models_mesh_generation`, `physics_boxes` and `textures_mipmaps`, every one drawing textures
    with mipmaps, while 228 drew, said with no exit code since the examples open by name through
    `e3d open`. Three things tell the readings apart and mend the first. The death's error says
    whether `gfni` stands among the processor's flags, from the line every opening prints, and
    whether Mesa's cache served the variant, `MESA_SHADER_CACHE_SHOW_STATS=1` printing the hits and
    misses at exit into the log; every cache of Mesa's shaders the workflow keeps, the Linux and
    Windows suites', the examples', the captures' and the games', is keyed by the processor's model
    name and a hash of its flags, read by a step before the cache step into `GITHUB_ENV` (`lscpu` on
    Linux, `Get-CimInstance Win32_Processor` on Windows), so no runner restores code another kind of
    processor made, which is right whichever reading holds; and the examples open through the
    wrapper as the games do, so a capture's death says its code and its instruction. If the deaths
    go on with the caches keyed so, the generator is on the runner and the second reading holds, to
    be taken up then. Settled as before. The three asks are done at `c08343d5`, every Mesa cache
    keyed by the processor's model and flags through `build/processor.sh`, the examples opened
    through `build/open-game.sh` with a capture's death saying its code and instruction, and the
    ending line saying whether `gfni` is among the flags and what Mesa's cache counted where the
    program ended of itself. The next runs compile cold and say which reading holds. On the push and
    the pack run of `704952c5`, 2026-10-18, the first with every Mesa cache keyed by the processor
    and so compiled cold, both examples jobs played Slide through, in 9 minutes each, two green
    puzzle steps toward the three, and the first evidence for the cache reading; the games and
    captures jobs of the pack run are read when they end. The pack run's games job passed at 01:32
    the same day, every game played on cold caches, Slide among them, the third green puzzle step in
    a row; the captures job, where ten examples died on `7b2486a5`, is read when it ends, and its
    passing settles this verdict with the cache reading as the cause, the mend being the keyed
    caches. Settled on the runs of `704952c5`, 2026-10-18. With every Mesa cache keyed by the
    processor and compiled cold, the push run's and the pack run's examples jobs played Slide
    through, the games job played all twelve, and the captures job drew all 256 examples where ten
    had died, three green puzzle steps in a row and no death anywhere; and the captures job's page
    named its processor, an AMD EPYC 9V45 with `gfni` among its flags, the second kind in the runner
    fleet beside the EPYC 7763, which lacks it, both of one family and sharing every bit Mesa's key
    holds. The cause is the first reading, a shader compiled on a runner of the one kind, carried in
    Mesa's cache that the jobs restored from any earlier run, and run on a runner of the other, and
    the mend is the keyed caches; the three instruments stay, so a death that comes again names
    itself.

64. **The hundred-app leak test reads the working set on Windows, which the system trims, so its
    baseline fell in a trough and the test failed with nothing leaking.** On the pack run of
    `683e6e9d`, 2026-10-17, the Windows suite failed one of the 1,661 tests it ran, `AppLeakTests`'
    hundred offscreen apps that draw, the resident memory at the last app 320.5 MB against 230.4 at
    the twentieth, 90 over the 50 allowed, while the heap stood at 38 MB from the tenth app to the
    hundredth, no Vulkan object was alive after the last and the threads were 23 to 25 throughout;
    the push run's Windows suite of the same commit, an hour before, passed it. The ten readings run
    291, 230, 290, 299, 296, 311, 315, 313, 254 and 321, swinging 60 to 90 MB between neighbors with
    no climb, and the figure is `Environment.WorkingSet` (AppLeakTests.cs:165, 186), the working set
    Windows trims when it likes, which the drawing leak test's resident bound met on 2026-10-12 and
    BevyCSharp's memory guard met in its Verdict 14, mended there by reading the private bytes
    committed, which Windows cannot trim. The mend is one thing. The test's resident figure on
    Windows becomes the private bytes, `Process.PrivateMemorySize64`, the commit charge
    `GetProcessMemoryInfo` answers, with the reason in the test's comment, the figure Linux and
    macOS read staying; and whether the bound of 50 stands is said with the series the new figure
    gives on the Windows suites, under N 3.5. Settled at three green Windows suites in a row after
    the mend. Mended at `7b2486a5`, the test reading `Process.PrivateMemorySize64` on Windows and
    the working set elsewhere, the series naming its figure, the bound of 50 kept since no
    measurement here calls for another, to be read against the Windows suites' new series. On the
    push and the pack run of `7b2486a5`, 2026-10-17, the Windows suite failed the test again with
    the private bytes read, and the figure climbs where the working set swung, 740 MB at the tenth
    app to 849 at the hundredth on the push and 724 to 836 on the pack, 1.1 MB an app from the
    fortieth on with no fall, the heap flat at 38 MB, no Vulkan object alive and the threads 24
    throughout, so the mend found what the trimmed figure hid, 110 MB of commit charge that a
    hundred apps leave behind on Windows and the Linux suite's resident set does not show. Whose it
    is comes first. On Windows the Vulkan loader unloads the ICD, lavapipe's with LLVM inside, at
    the last instance's destruction and loads it again for the next app, where Linux's `dlclose`
    leaves a library with unique symbols in place, so what LLVM and Mesa allocate at load and never
    free would be paid a hundred times on Windows alone; the collector's committed regions, which it
    gives back over time rather than at once, are another; the engine's own per-app native
    allocations, the ImGui context, SDL's init and quit and the Slang session, a third. One thing.
    The test prints beside the private bytes the collector's committed bytes
    (`GCMemoryInfo.TotalCommittedBytes`) and runs the hundred apps once with one Vulkan instance of
    its own held open throughout, so the ICD stays loaded; if the climb goes with it, the cost is
    the driver's load and the test holds that instance, saying why, and if it stays, the engine's
    native allocations are read app by app for what is not freed. Settled at three green Windows
    suites in a row after the mend. Done at `c08343d5`, the collector's committed bytes printed
    beside the private bytes and the hundred apps run once more inside a held Vulkan instance, both
    runs under the same bounds; on Linux the held run took 48 seconds against 64, so the loader
    reloads lavapipe at each app there too. The next Windows suite says whose the climb is. On the
    push and the pack run of `704952c5`, 2026-10-18, the macOS suite failed the new twin at its
    first line, `vkCreateInstance` answering `ErrorIncompatibleDriver`, since MoltenVK's loader
    lists itself as a portability driver and gives an instance only to a caller that asks for the
    portability enumeration extension with its flag, as the engine's instance does, and the bare
    instance asked for nothing; the hundred apps without it passed there as before. One thing more.
    The twin's instance is made as the engine's is, `VK_KHR_portability_enumeration` with
    `VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR` where the loader offers the extension, so the
    twin runs on the three systems; the Windows suites of these runs, still running, say whose the
    climb is. The twin's instance is made as the engine's at `d6b08853`, the portability extension
    and flag asked where the loader lists them. On the push and the pack run of `704952c5`,
    2026-10-18, the Windows suite failed both runs of the hundred apps, the plain and the held, each
    climbing alike, 725 MB at the tenth app to 828 at the hundredth and 729 to 823, the collector
    holding 84 to 87 MB committed throughout and the heap 38, so the climb is neither the
    collector's nor the driver's load, which the held instance kept, and is the process's own native
    memory, about 1 MB an app, on Windows alone. One thing more. The test reads the private bytes at
    each stage of one app's life, after SDL's init, the instance, the device, the first frame, the
    close and SDL's quit, averaged over the hundred, so the stage whose bytes do not come back names
    the leak, SDL's Windows backend, the audio device, the loader's layers or the engine's own
    allocations, and that allocation is then found and freed, or, where it is a library's and not
    the engine's, bounded with its cost said. Settled at three green Windows suites in a row after
    the mend. Done at `91ec5de1`, the private bytes read at each step of an app's life and averaged
    over the eighty after the twentieth, the device's making and disposal split into finer steps,
    and on Windows the heaps' allocated and committed bytes beside the collector's; the headless
    apps initialize no SDL and open no audio device, so those two candidates are out and the loader
    with its layers and driver, the renderer's shaders and the engine's allocations remain. The next
    Windows suite names the step. On the push and the pack run of `a41cc801`, 2026-10-18, the
    Windows suite failed both hundred-app tests again, the private bytes 744 MB at the twentieth app
    and 816 at the hundredth on the push run, 744 and 827 on the pack run, 0.9 and 1.0 MB an app,
    the heap at 38 MB from the thirtieth app on, the collector committing 65 to 89 MB, no Vulkan
    object alive, and, read for the first time, the process's heaps flat, 148 to 150 MB allocated of
    385 to 394 committed in 8 heaps at every reading from the tenth app to the hundredth, so no
    `malloc` in the process holds the climb, the C runtime's, the loader's, the layer's or
    lavapipe's, and the collector does not. The step line says what an app's life takes and gives
    back, averaged over the eighty, the instance 4.7 MB, the device 6.8, the renderer's shaders
    100.5, the frames drawn 85.1, the device's destruction giving back 152.6, the instance's 31.2,
    the close 2.1 and the end after a full collection 8.8, and it sums to 3.2 and 3.8 kept an app on
    the two runs where the ten readings climb 0.9 and 1.0, so some 2.5 MB an app is given back
    between an app's last reading and the next app's first, which no step holds, the stacks of the
    app's threads that end after it, the tenth-app readings being taken once they have, or memory a
    library frees late. The ten readings also swing, 679 at the fortieth app on the pack run and 714
    at the eightieth on the push run against 745 to 790 around them, 60 to 100 MB with the heaps and
    the collector flat, so a difference of two readings carries that much either way and the bound
    of 50 is read through it. What is left is memory committed outside every heap, by `VirtualAlloc`
    or a pagefile-backed section, and the process holds four kinds of it, the code and data LLVM
    compiles for lavapipe's shaders, the runtime's own code and loader heaps, the stacks of threads,
    and sections mapped. One thing more. On Windows the test sweeps the process's address space with
    `VirtualQuery` at the twentieth and the hundredth app, summing the bytes committed by the
    region's type, private, mapped or image, and by whether its protection is executable, with the
    count of regions in each class, and says the classes on the series' line with each one's growth
    from the twentieth to the hundredth, since LLVM's compiled code is private and executable, the
    runtime's is mapped and executable under its double mapping, a thread's stack is private, not
    executable and under a guard page, and data of either is private and not executable, so the
    class that grew names the owner; and the step line's last reading is taken once the app's
    threads are gone, as the tenth-app readings are, so its kept figure is the series'. What follows
    the reading is said here when it is in. Settled at three green Windows suites in a row after the
    mend. Done at `0af1c9cd`, the sweep on 64-bit Windows at the twentieth app and the last, the
    committed regions summed by type, private, mapped or image, and by what they hold, code by an
    executable protection, a stack by a guard page in its allocation, data otherwise, the kind grown
    most said first on a line of the series; each app's last reading taken once its threads have
    ended, waited for half a second at most, since on Linux the threads of two apps in eighty
    outlived two seconds and on Windows a longer wait for each of a hundred apps would pass the five
    minutes a test is allowed, the apps whose threads outlast the wait counted on the line; and what
    changes between an app's last reading and the next app's first a step of its own, so the steps
    sum to the tenth-app readings' climb. On Linux the steps summed to 0.04 MB given back an app
    with 0.32 between apps. The next Windows suite names the kind. On the push and the pack run of
    `0af1c9cd`, 2026-10-18, the Windows suite failed both tests again, 741 MB at the twentieth app
    to 824 at the hundredth on each, and the two new readings agree with each other. The steps sum
    to the series, 1.08 and 1.07 kept an app, with 2.3 and 2.2 given back between apps and the
    threads of one or two apps in eighty outlasting the half second, so no step and no stack hides
    the climb. The sweep names the kind. Private data grew from 712 MB in 2,901 regions at the
    twentieth app to 792 in 3,367 at the hundredth on the push run and from 712 in 2,908 to 790 in
    3,400 on the pack run, 80 MB in some 470 regions, six regions and a megabyte an app, 170 KB a
    region on average; mapped data grew 0.7 and 2.8 MB, mapped code 0.2, and private stacks, 1.3 MB
    in 50 regions, image data and image code, 86.7 and 136 MB, did not move, and private code is one
    empty region at both readings, so LLVM's compiled code is gone when a reading is taken. The
    climb is memory committed with `VirtualAlloc` for data and never given back, outside every heap,
    not a stack, not code and not a module, by whoever allocates that way, LLVM and lavapipe for the
    data of a compile or a scene, the runtime for its loader heaps and bookkeeping, the layer or the
    loader, and on Windows alone, since Linux and macOS hold. One thing more. For the apps the test
    follows, the twenty-first, thirty-first, forty-first and fifty-first, the sweep runs at every
    step, and the test says the private data regions each step added, their count and their sizes,
    and which of them still stand at the app's end and at the end of the app after it, by the step
    that added them, so the step whose regions stay names the owner by what it does, the layer's
    load at the instance, LLVM's compiles at the shaders and the frames, the physics at its plugin,
    and the sizes say the allocator, 64 KB pieces of a loader heap or the odd sizes of sections;
    where the step alone does not name it, a trace of `VirtualAlloc` with its stacks under Windows
    Performance Recorder on one app is the step after. Settled at three green Windows suites in a
    row after the mend. Done at `4827b705`, the sweep at every step of the four followed apps, by
    allocation, each step's new allocations with their sizes and how many stand at the app's end and
    after the next app, on the series an app a line. An allocation that stood before the app and
    grew within it, as a reservation committing more of itself does, is not among those added, so
    where the lines show few allocations added while the series climbs, the growth is inside
    standing allocations and the reading then says the allocations whose committed kilobytes grew
    over the app, by step. The next Windows suite names the step and the sizes. On the push of
    `4d5db9df`, 2026-10-18, the Windows suite failed the two tests as before, and the twentieth
    reading fell in a trough, 661 MB against 724 at the tenth and 746 at the thirtieth, so the
    sweep's baseline was taken there, private data 630 MB in 3,522 regions at the twentieth and 793
    in 3,303 at the hundredth, 162 MB more in 219 fewer regions, which says the swing of the
    readings is private data as well, committed and given back within allocations that stand, the
    regions joining as the holes fill. The heaps and the collector being flat, the swing and the
    climb belong to an allocator of its own that reserves and commits by itself, and one such is in
    the process on Windows alone. The Khronos validation layer's 64-bit Windows build links mimalloc
    in place of the C runtime's malloc, by its `layers/CMakeLists.txt`, which says the other systems
    do not for their instability under it, and the loader loads the layer with each app's instance
    and unloads it with the instance's destruction, in the held twin as in the plain one, since the
    held instance enables no layer; mimalloc reserves segments of megabytes, commits them in pieces
    and gives pieces back on a delay, and a library unloaded takes none of that with it. So the
    per-step lines are read for allocations added as the instance is made and standing after the
    instance is destroyed, with mimalloc's sizes, and where they say so, the test holds the layer
    loaded through the hundred apps, the held instance enabling it, and the plain test's bound is
    read with the layer's unload no longer in the figure, the layer's own leak said in the test's
    comment with its version; and the Windows suite takes the layer from LunarG's latest SDK
    (`build.yml:196`) where Linux pins 1.4.363.0 in `build/fetch-validation-layer.sh` so a change of
    layer comes in a commit, so the Windows version is pinned alike in the same mend. Done at
    `351cc461` so far as the suite has not spoken, the held instance enabling the validation layer
    wherever the loader lists it, so the layer stays loaded through the hundred apps in the held
    twin, and the Windows job pinned to the SDK's 1.4.363.0 as Linux is; on Linux the plain test's
    instance takes 77.4 MB and gives 148.2 back with 0.08 kept an app, and the held twin's instance
    steps fall under 0.05, so the held layer serves each app's instance. The next Windows suite says
    the step and the sizes and whether the held twin passes beside the plain test, and then the
    plain test's bound is read with the layer's unload out of it and the layer's leak said in the
    test with its version. On the pack run of `4d5db9df`, 2026-10-18, the Windows suite passed both
    tests, the first pass since the figure became the private bytes, by the readings' swing meeting
    the bound of 50 this once and by no mend; it counts for nothing toward the three green suites
    the mend needs. On the push of `351cc461`, 2026-10-18, the Windows suite failed one test of the
    two. The twin holding an instance with the validation layer enabled passed, and the plain test
    failed as before, its private bytes 737 MB at the twentieth app and 836 at the hundredth,
    private data 709 MB in 2,836 regions to 807 in 3,319, 98 MB more, with the layer's library
    absent at both readings, the image code 101 MB where the twin's readings hold 136 with the layer
    loaded, so the failing test is the plain one. The layer's unload with each app's instance is the
    leak, about a megabyte an app on Windows at 1.4.363.0, the layer's own allocator keeping what it
    reserved when the loader lets the library go, and the engine keeps nothing, as the twin says.
    The page lost the four per-step lines under its limit of 4,096 characters, the message ending in
    the regions' line, so the step was not read and did not need to be; and the heaps' allocated
    bytes climbed from 148 to 158 MB across the hundred apps on this run, 0.1 MB an app, where they
    stood flat before, which the test watches once the layer is out of the figure. The mend is one
    thing. The two leak tests run their hundred apps without the validation layer on every system, a
    Debug instance leaving the layer off where a test asks it, with the reason in the test's
    comment, the layer's Windows build at 1.4.363.0 keeping about a megabyte an app on its unload,
    measured here, and the twin passing beside the plain test on this run; so the plain test
    measures the engine and the driver's reload, as it was meant to, the twin the engine alone, and
    `E3D_REQUIRE_VALIDATION` does not count them. Where a Debug instance cannot leave the layer off,
    both tests hold the layer as the twin does. And the failure's message keeps the lines that name
    the cause, the regions and the per-step lines, before the series, within the page's limit.
    Settled at three green Windows suites in a row after the mend. On the pack run of `351cc461` the
    same day the Windows suite said the same, the plain test alone failing with 95 MB of private
    data kept and the layer's library absent at the readings, the twin passing, so the two samples
    agree. Mended at `08b104dd`, both drawing leak tests making their hundred apps with the
    validation layer off on every system through an internal switch of the device, the reason and
    the figures in their comment, the held instance holding the driver alone again, and the
    failure's message naming where a climb lies before its series within the page's limit; settled
    at three green Windows suites in a row after it. On the push of `4c6630a4`, 2026-10-18, both
    memory bounds held on Windows with the layer off, the suite red on the handles of one test
    (Verdict 68), so it is no green suite for the row. On the pack run of `08b104dd`, 2026-10-18,
    the Windows suite passed whole, the first green Windows suite after the mend, one of three. The
    row of three green Windows suites counts from the runs of `1398ed24` on, Verdict 68's mend being
    in the same tests. On the push of `1398ed24`, 2026-10-18, the Windows suite failed the twin
    alone, the hundred apps beside a held instance of the driver, by 9.7 MB, private data 609 MB in
    3,470 regions at the twentieth app and 669 in 2,772 at the hundredth, no allocation a followed
    app's steps added standing once the next app had ended, so the growth lies inside standing
    allocations as the falling count of regions says, while the plain hundred apps held their bound
    with the layer off. The twin's held instance keeps lavapipe loaded through the hundred apps,
    where the plain test lets the loader unload and load it with each, so what the twin measures
    beyond the plain test is the driver kept in the process, whose compiler's state grows with each
    app's shaders compiled, lavapipe's own and no business of the engine's bound, or the readings'
    swing, which the series would tell and the page lost under its limit, the regions and the four
    allocation lines standing first and the series after. The mend is one thing, with its message.
    The twin's private bytes are said and not judged, the twin judging the heap floor and the Vulkan
    objects as the plain test does, its comment saying that beyond the plain test it measures the
    driver kept loaded, with this run's figures, the plain within 50 and the held 59.7 with nothing
    new standing; and a memory failure's message, as a handle failure's, puts the series after every
    ten apps and the heaps before the followed apps' allocation lines, within the page's limit. The
    row of three green Windows suites counts from the mend's runs. Mended at `5a2d7df3`, the twin's
    private bytes said and not judged with the reason and the figures in its comment, the plain test
    judging as before, and a memory failure's message ordered with the series and the heaps first
    within 3,500 characters; settled at three green Windows suites in a row after it. On the push of
    `5a2d7df3`, 2026-10-18, the Windows suite passed whole with the mend in, the first of three.
    Settled on the push and pack runs of `5a2d7df3` and the push run of `a12206af`, 2026-10-18,
    three green Windows suites in a row with the layer off and the twin's figure said.

65. **The games job's soak judges Manor and Swarm as standing still, and both move on.** On the pack
    run of `683e6e9d`, 2026-10-17, the games job's soak of the twelve games, two minutes each on
    lavapipe at once, failed two. Manor's images climbed at their least from 81 to 90 past a bound
    of 87, its descriptor sets from 82 to 95 past 90, its entity ids from 399 to 478 past 448 and
    its assets from 81 to 95 past 93, its resident memory 548 MB at the first reading kept and 573
    at the last; Swarm's entity ids climbed from 108 to 130 past 128, its resident memory 460 then
    462. The check's own comment says what Manor does, its walk on the runner's four cores entering
    rooms the first half never reached, so the least climbs with the world, and the WHOLE table
    gives its buffers the most a whole walk holds, 162, and nothing else, where the images, the
    descriptor sets, the entity ids and the assets of a streamed level climb for the same reason.
    Swarm's soak sets wave 3 at every turn, so a count that still climbs across turns is what a turn
    leaves behind, drops, projectiles or the effects of the dead, 22 ids in two minutes, or the
    wave's own size varying from turn to turn. Two things follow. Manor's four figures are measured
    over a whole walk on the development machine's GPU and on lavapipe, as the buffers were, and
    written into WHOLE with the command that measured them (N 3.6), or the soak's course walks the
    level whole before the readings judged begin. Swarm is read for what a turn at wave 3 leaves
    alive after the next turn's reset, a leak of entities mended where one is found, and otherwise
    its entity ids given the most a turn holds in WHOLE the same way; the soak passed on the pack
    run of `fc5fdc97`, so the climb depends on how far the device gets in two minutes. Settled when
    the games job's soak passes on a pack run. Mended at `7b2486a5`. Manor's images, descriptor
    sets, entity ids and assets are given the most two whole walks of its autopilot held at 320 by
    180 with a collection every 60 frames, 92, 95, 539 and 96 on the GPU and on lavapipe alike, the
    second walk holding no more than the first, the buffers' 162 kept; Swarm's entity ids, which
    count the most ever alive at once and only rise, are given 152, the peak of thirty turns at a
    quarter-second frame, where a slow device's frame reaches Time's clamp and a turn of 300 frames
    covers 75 game-seconds into waves 4 to 6, 100 at a sixtieth, and thirty resets left the same 27
    entities, so nothing leaks; `SoakCheckTests` holds the run's numbers passing, each figure
    failing past its bound and the same climb failing for a game without a whole-walk entry, and
    BUILDING.md's soak paragraph says so. On the pack run of `7b2486a5` the games job ended at
    Summit's death (Verdict 63) before the soak ran, so the mend is unproven there. Settled on the
    pack run of `704952c5`, 2026-10-18, the games job passing with the soak of the twelve games,
    Manor's and Swarm's figures within what a whole walk or turn holds.

66. **The measure against raylib failed and its page says nothing of which pairs, the step's page
    holding the programs that ended well.** On the pack run of `704952c5`, 2026-10-18, the captures
    job drew all 256 examples and then failed at the measure against raylib's program, `compare.py`
    ending with 1, which it does where a pair's share of differing pixels passed the one recorded
    for it by more than its room or a pair is no longer drawn; the script prints the pairs to its
    output and annotates nothing, so the page's one error is `step.py`'s, which under Verdict 63's
    third ask lists how each program opened in the step ended, 21 lines of programs that ended of
    their own with their processor and cache counts, and GitHub's 4,096 characters for an annotation
    ran out before the command's last lines, so a reader not signed in learns which examples ended
    well and not which pairs failed (N 6.7). Two things. `compare.py` says its failures as an error
    annotation of its own, the pairs past their room with the share measured and the share recorded,
    and the pairs no longer drawn; and `step.py` names only the programs that ended other than of
    their own or still run, after the command's last lines, so an ending with exit code 0 costs the
    page nothing. Settled when the measure's page names its pairs on a pack run, and the pairs are
    then read for whether the runner's processor draws them otherwise. Mended at `a41cc801`, the
    pairs said as the script's own errors with their shares and room, in at most ten under the
    limit, and the step's page naming only the programs that ended other than of their own or still
    run, after its last lines. Settled on the pack run of `a41cc801`, 2026-10-18, the measure's page
    naming its one pair as the script's own error, `models_directional_billboard` 7.9 percent apart
    where 5.5 was recorded, past its room of 1 point, and no program named, every one having ended
    of its own; the pair is Verdict 67's.

67. **The raylib measure's pair `models_directional_billboard` stands 7.9 percent apart where 5.5 is
    recorded, on a runner whose processor the page does not say.** On the pack run of `a41cc801`,
    2026-10-18, the captures job's measure failed on that pair alone, 7.9 percent of its pixels
    apart from raylib's frame where `3DEngine.Examples/measured-ci.tsv:71` records 5.5, past the
    point of room every pair has (`build/raylib-bench/compare.py:209`), the other pairs holding and
    every one of the 256 examples drawn. The record was measured on a runner on 2026-10-11 or
    before, and the measure held to it on the pack run of `fc5fdc97` on 2026-10-17 and failed on the
    two pack runs since, `704952c5` with its cause lost under the page's limit (Verdict 66) and this
    one on the pair. Two things changed between. Since `704952c5` each runner draws with shaders
    compiled for its own processor (Verdict 63), where before the cache restored was one processor's
    for every runner, so a frame whose pixels depend on the rounding LLVM's code for a processor
    gives, the direction a billboard faces chosen from an angle near a boundary among them, may
    differ between the two processors the fleet has and no longer be hidden; and the runner image's
    own Mesa, which raylib's program draws with, is whatever its packages hold on the day. The page
    says neither the processor nor its flags, which the game steps' pages do. The mend is one thing,
    with its reading. The measure's page says the processor the runner drew on and whether gfni is
    among its flags, as the game steps do, beside its pairs and its count, so a pair's share is read
    with the processor that drew it; the pair is read on the working machine for what in its frame
    turns on rounding, the billboard's direction at the frame taken first, and where it does, the
    record gives the pair the room the two processors' shares measure, in the third column with the
    reason above it, or the frame taken moves off the boundary and the picture is taken again; and
    where it does not, raylib's frame is compared between the two runs for what Mesa drew otherwise.
    Settled when the measure holds on a pack run, and said here again where a run on the other
    processor fails it. Done at `4d5db9df`, the measure's errors and summary saying the processor
    and its gfni flag and both pictures of every pair kept under `captures/measure`; and read on the
    working machine, where the pair's 45 frames from 196 to 949 are 0.5 to 0.6 percent apart and the
    billboard's direction stands at 7 from frame 118 to 212 with the capture's frame among them, so
    nothing in the frame turns on rounding and the two shares are what the runners' Mesa draws. The
    next two pack runs, one on each processor, say which program's picture moved and whether the
    processor moves it. On the pack run of `0af1c9cd`, 2026-10-18, the measure failed on the same
    pair alone, 7.8 percent apart, the page saying no processor yet; the runs of `4d5db9df` say it.
    On the pack run of `4d5db9df`, 2026-10-18, the measure held on every pair, this one within its
    room, the 256 examples drawn in 1,257 seconds where the two failing runs took 1,565 and 1,572,
    so the pair holds on one runner and not on another, as a processor's drawing would have it; and
    the page of a run that holds says no processor, since `compare.py` prints it to the log and the
    summary, which need a sign-in, and to its errors alone. One thing more, small. The line
    "Measured on <model>, gfni ... its flags" is a notice as well, on every run, so a passing run's
    page says which processor the pairs held on, and the pictures kept under `captures/measure` on
    this run are the holding side of the comparison the next failing run completes. Done at
    `87cd9415`, the processor a notice on every run before any pair is measured, the pairs measured
    for the first time sharing the other nine notices. On the pack run of `351cc461`, 2026-10-18,
    the measure held on every pair again, so the pair has held on two pack runs and failed on two,
    the processor said on neither of the holding ones, which `87cd9415`'s notice ends from the next
    pack run on. On the pack run of `08b104dd`, 2026-10-18, the measure failed on the pair alone,
    7.9 percent apart, and the page said the processor, an AMD EPYC 7763 without gfni among its
    flags, the 256 examples drawn in 1,571 seconds as on the two failing runs before, where the two
    holding runs drew them in 1,257 and 1,247, so the pair fails on the fleet's 7763 and holds on
    its other kind, the 9V45 with gfni, at 2.4 points apart between the two processors' drawings of
    the same frame, a rounding difference the working machine, with gfni, draws as the 9V45 does.
    Both pictures of the pair from this run and from the holding run of `4d5db9df` are in those
    runs' captures artifacts under `measure`, for the owner's eye. The mend is one thing. The record
    gives the pair a room in its third column measured over the two processors, 3 points over the
    5.5 recorded to hold the 7.9 the 7763 draws with slack, with the reason above it naming the two
    processors and their shares, as `compare.py` provides for a pair whose picture moves between
    runs. Settled when the measure holds on a pack run on each kind of processor, the page naming
    it. Mended at `32ce6f4e`, the pair given three points of room in the record's third column with
    the two processors and their shares in the comment above it; settled when the measure holds on a
    pack run on each kind of processor, the page naming it. On the pack run of `32ce6f4e`,
    2026-10-18, the measure held on every pair on an AMD EPYC 9V74 with gfni, a third kind the fleet
    shows, the page naming it, the 256 examples drawn in 1,361 seconds, so the kinds with gfni hold
    and the 7763 without it is the side left; settled when the measure holds on a pack run on a
    7763, the page naming it. On the pack run of `1398ed24` the same day the measure held on every
    pair on AMD EPYC 9V74 80-Core, gfni among its flags, the page naming it, another holding run on
    a kind with gfni; the 7763 is the side left. Settled on the pack run of `5a2d7df3`, 2026-10-18,
    the measure holding on every pair on an AMD EPYC 7763 without gfni, the page naming it, the pair
    within its room of 3 on the kind that drew it 7.9 three times, as the kinds with gfni held
    before.

68. **The leak test's handle count passed its allowance by one at the 89th app on Windows, Thread
    handles of ended threads kept some four an app.** On the push of `4c6630a4`, 2026-10-18, the
    Windows suite failed one of the two hundred-app tests on its handles, 2,413 at the 89th app
    where 2,143 stood at the twentieth and 2,412 were allowed, the allowance being 200 and one
    Thread handle an app for the driver's (`AppLeakTests.cs:91, 167`), while both memory bounds held
    with the layer off (Verdict 64); the pack run of `08b104dd` the same hour passed both tests on
    Windows, so the count crosses the allowance on one run and not another, by a handle, the climb
    being some four handles an app against the two and a half the allowance spreads over eighty. The
    followed apps say where the handles go. The twenty-first kept 7, 5 Thread and 2 Event, its
    Thread handles opened at `LightingPlugin built`, 1, `PhysicsPlugin built`, 3, and `started`, 3,
    with `ended` giving 3 back and the driver's steps netting 1, so some four Thread handles an app
    belong to threads started at those steps, the engine's or the driver's started lazily there,
    which ended and whose handles stay, the process's threads standing at 23 to 25 throughout; the
    count is read after a full collection with its finalizers and after the threads ended, so the
    handles are held by something that lives, a `Thread` object kept after its thread's end or a
    handle duplicated and not closed. The page lost the test's name and the series of handles after
    every ten apps under its limit, the handles-by-step lines of four apps standing first in the
    message. The mend is one thing, with its words. The threads started at the lighting plugin's
    building and at startup, and whatever holds them after they end, are read on the working machine
    with the census at each step, the reference kept to an ended thread dropped where one is and the
    handle closed where one is duplicated, so a closed app keeps no Thread handle but the driver's;
    the allowance stands, since what it absorbed was a climb and not a swing; and the handle
    failure's message puts the series of handles after every ten apps and the kinds kept first, the
    followed apps' steps after and cut to the limit. Settled at three green Windows suites in a row
    after the mend. Read at `2e4187e3` so far as the working machine can, which reads no Thread
    handles: on Linux the followed apps' threads start and end by name at the steps said, the
    physics plugin's three being Bepu's dispatcher workers, disposed at the close, and nothing
    starts at the lighting plugin or at startup on 32 cores, so the handles Windows keeps from those
    steps are most likely threads a four-core runner's pool or lavapipe starts there; a heap census
    grows no Thread object across eighty apps, so nothing managed holds an ended thread, and the
    runtime's bookkeeping, the pool or a native library does. On Windows the census names each kept
    handle's thread, ended or running, with the step that opened it, and the next Windows suite says
    whose they are; the mend follows it. Seen beside it, `PhysicsPlugin.cs:47-58` warms the physics
    world up in two identical blocks one after the other since `118b4adc`, the second returning at
    the warm-up's once-a-process guard and costing a pool item an app, and the second block goes
    with the mend. On the push of `32ce6f4e`, 2026-10-18, the Windows suite failed the plain test's
    handles again, 2,423 at the 91st app where 2,147 stood at the twentieth and 2,418 were allowed,
    the handles after every ten apps climbing 2,147, 2,181, 2,218, 2,261, 2,298, 2,335, 2,376 and
    2,416, some 3.8 an app, with the threads flat at 24 to 26, and the census named the kept
    handles. Every followed app kept four Thread handles of unnamed threads that had ended, opened
    at the device's making, where nine unnamed threads start, and the twenty-first kept one more,
    the runtime's background collector's thread, running, opened at the lighting plugin and the
    process's once. The nine are lavapipe's, which starts its rasterizer and compute threads with a
    device and names none, where on Linux the device's making starts two named pool workers and the
    GPU driver's three, and the handles of four of them stay open after the threads end, a handle a
    thread, which is lavapipe's Windows build leaving its threads' handles unclosed, Mesa's leak and
    not the engine's. The allowance counts the driver's thread handles by the net of the handle
    counts across the driver's steps, which came to 1 where the census shows 4 kept, since the
    engine's own threads' closings fall in those steps too. The mend is one thing, with the warm-up.
    The allowance's count of the driver's thread handles an app is taken from the census, the kept
    Thread handles of ended unnamed threads opened at the driver's steps in the followed apps, the
    most over them, with the comment naming lavapipe's Windows build and the four; the allowance of
    200 stands; and the second warm-up block of `PhysicsPlugin.cs` goes. Settled at three green
    Windows suites in a row after the mend. Mended at `1398ed24`, the driver's thread handles an app
    counted from the census, the comment naming lavapipe's four, the allowance standing and the
    second warm-up gone; settled at three green Windows suites in a row after it. On the pack run of
    `32ce6f4e` the same day the Windows suite passed the handles by its twentieth baseline's chance,
    before the mend, a pass that counts for nothing toward the three. On the push of `1398ed24`,
    2026-10-18, the handle bound held on Windows with the census's allowance, both tests, the suite
    red on the twin's memory bound (Verdict 64), so no green suite yet for the three. On the push of
    `5a2d7df3`, 2026-10-18, the Windows suite passed whole, the handles within the census's
    allowance, the first green Windows suite of three. Settled on the same three Windows suites, the
    push and pack runs of `5a2d7df3` and the push run of `a12206af`, 2026-10-18, the handles within
    the census's allowance on each.

69. **The leak test's heap floor on macOS rose 6 MB from one trough among flat readings, and the
    figure carries the collector's leavings.** On the pack run of `32ce6f4e`, 2026-10-18, the macOS
    suite failed the plain hundred-app test on its heap floor's rise, 6.02 MB where 5 is allowed,
    the heap after every ten apps reading 79.19 at the twentieth, 77.22, 70.81 at the fortieth,
    77.18, 77.25, 76.90, 76.88, 76.83 and 76.85 at the hundredth, so the least of the first half
    fell in the one trough and the least of the second on the plateau, the heap otherwise flat and
    2.3 MB lower at the end than at the twentieth, the resident memory falling from 840 MB to 768
    and no Vulkan object alive; the suites of the day before this one passed it. The figure is
    `GC.GetTotalMemory(true)` after a collection (`AppLeakTests.cs:218`), the bytes the heap holds
    with the fragments the collector left where it did not compact, which on macOS swing by 6 MB
    between collections of the same live set, as the floor's own comment says a census found 0.25 MB
    more alive across such a swing, and the least of each half guards against a crest and a trough
    of two readings and not against a trough alone. The mend is one thing. The heap is read as the
    bytes a full blocking collection promoted, `GC.GetGCMemoryInfo(GCKind.FullBlocking)` after the
    forced collection, the live set without the leavings, as BevyCSharp's soak reads its live
    figure, and the floor's rise is judged on it with the bound of 5 kept until the series says
    otherwise (N 3.5), the reason in the test's comment. Settled at three green macOS suites in a
    row after the mend. Mended at `1398ed24`, the heap read as the bytes a full blocking collection
    promoted, the floor judged on it with its bound of 5, 13.3 MB flat from the twentieth app on the
    working machine; settled at three green macOS suites in a row after it. On the push of
    `1398ed24`, 2026-10-18, the macOS suite passed whole, the first green macOS suite after the
    mend, one of three. On the pack run of `1398ed24` the same day the macOS suite passed whole
    again, the second of three. On the push of `5a2d7df3` the same day the leak tests held on macOS
    and the suite was red on another test (Verdict 70), so the row stands at two green macOS suites
    until the next. Settled on the push and pack runs of `1398ed24` and the pack run of `5a2d7df3`,
    2026-10-18, three green macOS suites with the live heap floor, the push of `5a2d7df3` between
    them red on another test with the leak tests holding.

70. **A script generation whose system threw a type of its own stayed alive after twenty collections
    on macOS, once.** On the push of `5a2d7df3`, 2026-10-18, the macOS suite failed one test of
    1,649, `ScriptGenerationTests`' unload of the first generation after the script is compiled
    again (`ScriptGenerationTests.cs:68`), the load context's weak reference alive after twenty
    rounds of a collection and its finalizers, where the test had passed on every suite of the day
    and on Linux in this run, the file unchanged since 2026-10-12. The generation's types are
    reachable from something that lives past the recompile for a while and not always, which a weak
    reference and twenty collections cannot shake, and three holders of a thrown exception fit that,
    the thread that threw it, which keeps its last thrown exception until it throws again or ends,
    the logger's entries where an entry holds the exception object and not its text until the queue
    drains, and the schedule's catch where it keeps the exception past the frame; a static of the
    engine would hold it on every run and does not. The mend is one thing, with its reading. The
    holders of a system's exception are read, the schedule's catch, the logger's entries and the
    thread's last thrown exception, and whichever keeps the object past the frame that logged it
    drops it, the logger keeping text, the schedule keeping nothing, and where the thread's own hold
    is what remains, the test ends the hold before it judges, a frame or a thrown and caught
    exception of the test's own on the schedule's threads, with the reason in its comment; and where
    the generation still lives after the collections, the test says what holds it by name where it
    can. Settled at three green macOS suites in a row after the mend. Read and mended at `a12206af`.
    The schedule's catch keeps nothing and the engine's logger keeps text, and a probe showed the
    thread's last thrown exception no holder on this runtime once the catching method has returned;
    the one holder found was `ScheduleTests`' listener on the process's `Engine.Schedule` logger,
    which kept the exception object of every line logged while its test ran in a collection beside
    the unload test's, and a scratch provider of that shape kept a generation alive on Linux, a
    cause that fits and is not proven the macOS one. The listener keeps the type's name, and the
    unload test names what may hold a generation that outlives its collections, the app's systems in
    its load context, the process's list of generated behaviors and the schedule logger's providers.
    Settled at three green macOS suites in a row after it. On the push of `a12206af`, 2026-10-18,
    the macOS suite passed whole with the mend in, the first green macOS suite of three. On the pack
    run of `a12206af` the same day the macOS suite passed whole again, the second of three. Settled
    on the push and pack runs of `a12206af` and the push run of `ad5d3692`, 2026-10-19, three green
    macOS suites in a row with the mend.

71. **The scene field's descriptor pool counts its storage images by hand, so the faces image made
    the resolve pass outgrow it and the macOS suite failed 75 tests with nothing wrong in the
    frames.** On the push run of `257006c7`, 2026-10-19, the macOS suite failed 75 of 1,688 tests in
    12 minutes 46 seconds, 1,582 passing, every failure with the render system throwing
    `ErrorOutOfPoolMemory` from the renderer in stage Last, so each test's own assertion failed on a
    frame not drawn and N 3.7 failed it a second time on the logged error, and three reference frame
    tests found the layer's error that a descriptor pool was destroyed while a command buffer still
    used it. The page counts 2,660 of the layer's lines naming binding 11 of a layout's first set
    with a count of one and 380 naming where storage images are found in the pool, and `a52b125b`
    gave the resolve pass two storage images for the faces at binding 11 (`field_resolve.slang:50`),
    while the scene field's pool is sized by hand, twelve storage buffers, six storage images and
    one uniform buffer (`GraphicsDevice.SceneField.cs:657`), as the illumination's, the cube
    filter's and the skinning's pools are; MoltenVK refuses an allocation past a pool's counts, as
    the specification allows, where the drivers the suite ran on before, Mesa's and the development
    machine's, allocate past them, and the layer's line on an allocation past a pool's counts is a
    warning, which the tests' list of validation errors does not hold, so the fault passed every
    check before the push. Two things follow. Every descriptor pool's counts are taken from the
    layouts it serves, counted by type from their bindings times the sets allocated, as the compute
    helper's already are (`Compute.cs:170`), so no pool is sized by hand, and a test under the layer
    allocates every layout from its pool and reads the layer's lines, warnings among them, for an
    allocation past a pool's counts, so the fault fails where the layer runs and not on one driver;
    and the renderer's path out of a failed allocation does not destroy a pool a submitted command
    buffer still uses, waiting for the device or leaving the pool to the frame's end. The pack run's
    macOS suite of the same commit failed the same 75 tests in 17 minutes 4 seconds. Mended at
    `33fd0804` the same day as the head says, every pool counted from its layouts by one helper, the
    layer's five lines on an allocation past a pool counted as errors, the renderer waiting for the
    device when a frame throws, and two tests, one of which failed before the mend under lavapipe
    and the layer. On the pack run of `257006c7` the Windows suite's test step took 115 minutes
    where the push run's took 60 for the same command at the same commit, its page counting 6,944 of
    the layer's lines on binding 11, and one part of the suite ended at its 60-minute limit in the
    hundred-app leak test at its 51st app, an hour in, with every test that ran passing; whether
    that is the fault's second face, the layer's lines slowing every allocation under it, or a slow
    runner, the pack run of `33fd0804`'s Windows suite says, and if it is as slow with no line to
    log, the leak test's time on Windows is a verdict of its own. The push run of `33fd0804` passed
    its macOS suite in 25 minutes on 2026-10-19 at 19:35, the first of three, and the pack run of
    `33fd0804` passed it in 42 minutes at 20:03, the second. Settled on the macOS suites of the push
    run and the pack run of `33fd0804` and the push run of `fa607cb3`, 2026-10-19, three green in a
    row after the mend, in 25, 42 and 21 minutes.

72. **The Windows suite runs in 45 to 48 minutes when it runs through, and twice today a run of it
    went past an hour on the same code, once cut by the job's limit.** On the push run of
    `33fd0804`, 2026-10-19, the Windows job's test step (`build/test.py --no-build --timeout-minutes
    60`) ran from 19:12 to 20:50 and was cut when the job reached its 100 minutes, writing no
    summary, while the pack run's Windows job of the same commit ran the same step in 45 minutes 22
    seconds with 1,674 passed and the whole job, the thirteen games played, in 75; the push run of
    `257006c7` ran it in 48 minutes 3 seconds, and the pack run of `257006c7` took 115 minutes with
    one part lost at its 60-minute limit in the hundred-app leak test at its 51st app, which Verdict
    71 took for the pool fault's face. With no fault left to log, a part still ran an hour or more
    on one runner of two, so the time is a term of its own, a test that hangs or crawls on some
    Windows runners, the hundred-app leak tests the first to read since the one part that was seen
    lost stood in them. The mend is read before it is chosen: the cut job uploaded its artifacts, so
    the part's `output.txt` says which test ran when the hour ran out and what each test before it
    took, read against the pack run's for the same tests; then the slow or hanging test is mended
    where it is, a wait that has no bound given one, a hundred apps made fewer or cheaper if their
    cost is the runner's, and the test script says in its summary, when a part is lost, the test it
    was in and the time each test took, so the next such run reads itself. The job's limits stay as
    measured, 100 on a push and 180 on the pack run, since the suite that runs through fits them.
    The push run of `fa607cb3` ran the suite in 32 minutes 18 seconds, 1,674 passed, the whole job
    in 40, on 2026-10-19 at 22:02. Settled at three Windows suites in a row under 60 minutes after
    the mend.

73. **A scene file written again with a later time was not read anew on the macOS runner.** On the
    pack run of `d2de2766`, 2026-10-19 at 23:46, the macOS suite failed
    `SceneRefTests.Copies_Of_A_File_Share_Its_Mesh_Until_The_File_Is_Written_Again` at its line 91,
    the third copy's positions the first mesh's where a new version of the file is read anew, 1,661
    passed and 32 skipped in 16 minutes 2 seconds, while the push run's macOS suite of the same
    commit passed it, so it shows on one runner of two. The test saves a scene, places two copies,
    which share one mesh, sets the file's write time five seconds later and places a third, which
    should read the file anew; the scene reference keeps each file's write time and reads the file
    again where the time it reads is later (`SceneRef.cs:90, 124`). The mend is read before it is
    chosen: whether the time the runner's file system gave back after the set was the one set,
    whether the reference read the time before the set in a frame of its own or caches it for a
    while, and whether a copy placed in the same step as the set is read before the step reads
    times; then the test or the reference mended where the reading points, the test waiting for the
    step that reads times or the reference reading the time at each placing, with the reason in the
    remark. Read and mended at `ed089e37` on 2026-10-20, the reload check's twice-a-second timer
    respawning the first copies in the step after the write where it fell there, the test holding
    the third against the first's mesh from before the write and asserting the file system gives
    back the time set. Settled at three green macOS suites in a row after the mend.

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
    2026-10-09, judging global illumination on the Cornell box and on Wick as not yet the best and
    naming banding, light across edges, a bounce too soft and wide, and one too dim at a distance; a
    path-traced reference the engine draws itself is the measure, over Bevy's Solari in BevyCSharp
    and over the eye alone, a level of the hard cases is made since no project checks the bounce,
    and a debug window with gizmos and texture views shows it; subsurface scattering's first batch
    is committed as far as it stands alone and goes on after.

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
    game's session's to commit. Since 2026-10-17 (Decision 41) the game lives in its own repository,
    Voxel-Sandbox, and its session writes its entries here still, headed by that name; the game's
    files are its repository's.

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

38. **The macOS captures run in the pack run alone.** On 2026-10-12 at 18:30 the owner chose to move
    the macOS examples job, every example captured on the macOS runner's GPU, from each push to the
    pack run, beside the soak, the storm and the measure against raylib, which run only there, so a
    push answers in the suites' time and a GPU-only regression is still caught before a package is
    made, at the cost of sitting on main until the next pack run. Item 25 carries it.

39. **No development on either engine until every job is green on every push and pack.** On
    2026-10-16 the owner, after four pushes and four pack runs in a row failing each on a different
    job while the code stood still, stopped development on both engines: the working sessions mend
    what the verdicts name and nothing else, every push must be green on every job, three in a row,
    and the pack run after them, before an item is taken again, on BevyCSharp as on 3DEngine.

40. **Two workflows an engine, build and pack, the same shape in both.** The owner found on
    2026-10-17 that the two engines' workflow sets differed, five files in BevyCSharp and four here
    with graphs that looked unlike each other, and ordered each engine down to two: a build workflow
    on an ordinary push and a pack workflow by hand, consistent between them, the pack run keeping
    every check that ran only there. Written as item 26 here and item 23 in BevyCSharp, where the
    tester zips and the scene packs stop being workflows and become runs on the owner's machine. The
    norm's "the workflow" names both, and a rule the one it means.

41. **The voxel game moves to its own repository, Voxel-Sandbox, and a tiny sample stays under
    `games/`.** The owner, having uploaded the engine's package 5.2.43, ordered on 2026-10-17 that
    `3DEngine.Game` go to the Voxel-Sandbox repository checked out under this one, replacing the old
    game there in place, committed the same way as the engines with the same `AGENTS.md`, `.github`
    files and two workflows, on the published package; the owner chose the old game replaced rather
    than kept beside, and the sample under `games/` rather than the project reborn. The game's
    session makes the move; the engine's session removes the projects after it (item 27) and writes
    the sample (item 28); the game's session keeps writing ASKS.md here (Decision 26); the game's
    repository has a REVIEW.md and a NORM.md of its own.

42. **Decision 39's gate is the new workflows.** The owner chose on 2026-10-17 that the rework of
    item 26 starts that day as the first work on both engines, and that the green lights Decision 39
    asks are then asked of the new workflows: a green push and a green pack run under them on each
    engine before engine and game features resume. Until then the working sessions mend what
    verdicts name and do item 26, and in BevyCSharp item 23, alone; on 2026-10-17 at 15:19 the owner
    added the register sweep (item 29, BevyCSharp's 25) to what may be done before the gate, since
    it touches prose and no behavior. Met for this engine on 2026-10-18 at 14:14, the pack run of
    `4d5db9df` whole green with its package after the push run of `683e6e9d` whole green the day
    before, so Decision 39 is lifted for 3DEngine and development resumes with item 27 then 28, the
    verdicts open, 64 and 67, mended as they come; the Windows leak tests passed on that run by
    their readings' swing, said under Verdict 64, which stands open.

43. **Prose is technical and generic.** The owner said on 2026-10-17 that phrasings such as "the
    light that bounces" and "how to drive the game" read as casual and do not fit. A feature is
    named by its technical term, the engine's own where it has one, and never by a figure of speech
    or a paraphrase: global illumination, and radiance cascades for the technique, where the light
    bounced; emissive where a block gave off light; a running program controlled or scripted through
    the command line where it was driven, and a scripted run or a course where a drive. The owner
    added at 14:37 that the colon is overused, an elaboration or a list appended to a sentence after
    one where a sentence would do, in documents, comments and logs alike, which STYLE.md should have
    covered; its rule tightens so that a running sentence holds no colon. STYLE.md carries both
    rules with these pairs in each repository, every document, comment and message a reader sees is
    swept (items 29 here, 25 in BevyCSharp, 4 in Voxel-Sandbox), and an identifier keeps its name
    unless the rename is cheap and the thing is new, as the game's `build/drive.sh` is; a new name
    follows the rule. On 2026-10-17 at 20:34 the owner read the game's README's opening, "A voxel
    game in the manner of Minecraft, and the ground floor of a larger one", and asked that no
    phrasing of the kind stand either. A likeness is not a description and a figure of speech is not
    a term, so a thing is said by what it is and does, another product is named only where a rule or
    a number is taken from it, as the game's sizes, speeds and light levels are, and a plan is not
    written into a description. The idioms the three repositories hold go with it, in the manner of,
    the ground floor of, out of the box, under the hood, at a glance, a handful of, in the wild, a
    great deal of, the knob, the plumbing and a long way among them, items 29 here, 25 in BevyCSharp
    and 5 in Voxel-Sandbox; a word in its literal sense stays, a box a joint leaves or a road to a
    signpost.

44. **Development resumes on all three fronts, the radiance cascades first.** On 2026-10-18 at
    22:25, with every run green and the pack run of `a12206af` whole green at 22:16 with its
    package, the owner resumed development on both engines and the voxel game. For this engine the
    week's focus is the radiance cascades, whose light does not yet look as good as it can (item
    30), and beside it the examples whose pictures are of the engine's making where raylib's
    resource belongs, the gamepad example's controller first (item 31). The voxel game takes the
    cascades' mends through the engine's packages, after the engine's commits. Decision 39 stands
    lifted (Decision 42), and the verdicts open are mended as they come.

45. **The README's centered parts are markdown inside a centering block, since nuget.org draws a
    README without its HTML.** The owner wrote at 20:47 on 2026-10-19, having uploaded the packages
    to nuget.org, that the README's centered elements need not be HTML: a centering block, `<div
    align="center">`, holding plain markdown, the heading, the text and the banners, with a blank
    line after the opening tag and before the closing one so the markdown inside is rendered,
    centers on GitHub, and on nuget.org, which draws a package's README without its HTML and shows
    such blocks as their source, the content stands as markdown; the tables of pictures stay HTML,
    since a markdown table cannot size its pictures, unless the script that writes them makes
    thumbnails at the shown width, which is measured on GitHub and nuget.org before it is taken. The
    README is the package's, so what nuget.org draws is what a reader of the package sees.

46. **The code fixes project stays.** The owner asked at 22:34 on 2026-10-19 what
    `3DEngine.CodeFixes` is for and whether it could be folded into the engine or removed, and on
    the answer said to keep it. It is the editor's fixes for the generators' four diagnostics, one
    file, apart because a code fix needs Roslyn's workspace layer, which the compiler does not load,
    so Roslyn keeps fixes out of the analyzer's assembly and the engine's library, which targets the
    runtime, can host neither; it ships in the package beside the generator. BevyCSharp's twin stays
    with it.

47. **A package's release notes are the commits since the package last published on nuget.org.** The
    owner wrote at 22:45 on 2026-10-19 that the release notes include everything and the next
    package appends them to the older ones: the notes a pack run writes are the commits since
    `build/version.txt` last changed, so every package of a minor carries the whole minor's notes,
    104 lines at `0.5.105` and 119 at `0.5.120`, 89 at the engine's `5.2.92`. So a package's release
    notes are the commits since the package last published on nuget.org, whose commit the feed's own
    record of the latest version names, read by the pack run from the feed, and the whole minor's
    where the feed answers nothing or lists no version of the minor, a reader of the notes being
    told what changed since what they could have had.

48. **The pack workflow publishes through nuget.org's trusted publishing.** On 2026-10-19 at 22:51
    the owner, making nuget.org's trusted publisher policies for the two repositories, quoted the
    feed's notice that API keys are strongly discouraged for automated publishing and trusted
    publishing is the way for GitHub Actions, and so the pack workflow publishes through it: the
    policy on nuget.org names the package owner `EggyStudio`, the repository owner `EggyStudio`, the
    repository, the workflow file `pack.yml`, no environment, the push scope and the package
    pattern, and the pack job proves it is that workflow with its identity token, takes a
    short-lived key from the feed's login action and pushes with it, so no key is stored in the
    repository. The two policies stood active on nuget.org at 22:53, one a repository, each with the
    package owner and repository owner `EggyStudio`, the workflow `pack.yml`, the push scope and its
    pattern, the feed holding the permanent GitHub ids of the owner and the repository for its
    check.

49. **The reviewer starts the pack runs when a package is due.** The owner wrote at 22:57 on
    2026-10-19 that the reviewer is to start the pack workflow as well, so no one waits on the owner
    for a package and a pack can be made and checked as part of the review; the owner's GitHub login
    on the machine carries the workflow scope, and the pushes were the reviewer's since 22:20. So
    the reviewer starts a pack run with publish on when a package is due: after a commit the voxel
    game or the other engine waits on, after the mend of a fault that made a pack run red, at the
    end of a day's batch, and when the owner asks, and not after every commit, since a pack run is
    more than an hour of the runners' time; the run is read and recorded as every run is, and its
    package is what the game and the other engine take.

## Replies

Shared: a mesh that replaced a still one in place is not stamped as boxes while a cascade the
budget left for a later frame still holds the one it replaced, where a voxel game's section stamped
as its boxes filled the air above its ground in those cascades and a room's indirect light went for
a frame eight after a block broke (`SceneFieldPlan`).












