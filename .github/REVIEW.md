# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `23b4793b`, item 124, the mesh entity's shadow and bounce flags, read at 18:54 on
2026-10-25, carrying ASKS.md, REVIEW.md with a Shared line and SHARED.md, pushed at 20:15 on
2026-10-25 once the build run of `4c9cf7e4` went green whole, the fifth pack run started with
publish on at 20:15 for the game's remaining parts; both runs red within three minutes at 20:17 on
the runners' dotnet install, Verdict 92, their failed jobs run again at 20:20 and watched.
CastsShadows and BouncesLight stand on the ECS Material set true by its constructors as DoubleSided
is, a scene file's older entries reading them true since the reader starts from Material.Default,
MeshEntityDraws passing them to the draw and the group key gaining both so entities that differ
never share a batch; the guide's Mesh entities section shows a water entity casting no shadow, its
block building on the package. The test sets three cube entities over a floor under the sun, the
twin and the no-bounce cube shadowing the floor while the no-shadow cube leaves it lit above three
quarters of the clear floor, the field holding the twin alone, and fails with the flags off the
draw. 2,116 on the GPU in both orders, 33 on lavapipe with validation, the strict build clean,
PublicApi.txt updated. A trap noted, the ECS Material's constructors making DoubleSided true where
ModelMaterial defaults to single-sided, so a default entity draws both faces where a default
DrawMesh draws the front alone, the game copying the flag across and unaffected, item 126. After
`4c9cf7e4`, item 100's first part, the share of light a surface lets through, pushed at 18:27 on
2026-10-25 once the build run of `21a3fbc7` went green whole, its run green whole at 20:12, Windows
in 104 minutes, Linux in 38 and macOS in 33, carrying the review's files and DEVLOG.md with a Shared
line. LightThrough on ModelMaterial and the ECS Material sets the share of light that passes a unit
of a surface's depth, the field's colors carrying it in their alpha, the cascades' rays and the
screen probes' rays crossing up to four such surfaces, keeping the share for each unit of depth and
taking the rest of the surface's light, a crossing before a cascade's interval kept for the probe's
own faces alone in a second channel of the distances. One departure, a floor under a slab a unit
thick letting half through a unit taking 0.23 of the sky light the solid slab holds back at Low and
0.29 at High and an all-through slab 0.95 and 0.98, where the item said about half, since slanting
rays cross more of the slab a unit of depth and an infinite slab works out to a third by the
cosine-weighted mean, the test bounding it between 0.15 and 0.40 and the guide saying about a
quarter. A frame where nothing lets light through traces with gi_trace.slang built with
NO_LIGHT_THROUGH, a new loader variant tested; the bisect found shading into temporaries summed
after the ray cost 7% at Low even with the crossing compiled out, so the light is written at the hit
as before, 0.554 ms against 0.552; gi_screen shades in two places instead of three, taking 0.16 to
0.60 ms off High in the bounce rooms, the Cornell box 1.60 where 1.92 at 800 by 450 and 2.07 where
2.64 at 1080p, Medium 1.50 where 1.82, the frames within 2 of 255; the guide's table reads Low 0.55,
Medium 1.50 and High 1.60 ms at 6.28, 9.29 and 11.72 MB, the second channel 0.12, 0.37 and 0.56 of
them. 2,114 on the GPU in both orders, 71 on lavapipe with validation, the strict build clean, the
pack and the docs on the package passing, PublicApi.txt updated. Left for the second part, the grove
at both sizes, unreadable here with BevyCSharp's suite holding 40 to 90% of the GPU, the canopy
picture and note and the crossing's cost where leaves exist. After `21a3fbc7`, item 96's cut, the
edge march gated by the thin-wall test, read at 15:23 on 2026-10-25, carrying REVIEW.md, the grove
pair and a note, pushed at 15:37 on 2026-10-25 once the build run of `073f34c8` went green whole,
its own run green whole at 16:48, Windows in 71 minutes, Linux in 35 and macOS in 41. The model
pass's edge test gains byThinWall with its comment. At the grove at High at 1920 by 1080 over three
alternating rounds of two readings with other processes holding 14 to 38% of the GPU, as is 13.07
and 13.08, 12.11 and 12.11, 15.41 and 15.35 ms against gated 10.53 and 10.57, 11.13 and 10.84, 10.00
and 9.66, a saving of 1.0 to 5.4 ms within a round and about 3 on the mean, the earlier round 12.29
and 12.61 to 10.25 and 10.33 beside them; the frames alike, 70 of two million pixels differing by
over 3%. The rooms at Medium and High unchanged, the thin room's corner and foot tests holding, the
strict build clean, 2,112 plain and shuffled, 37 on lavapipe with validation and one skipped for ray
queries. The half-size gather stays in the scratchpad alone. After `073f34c8`, the soak's logs kept,
read at 14:16 on 2026-10-25, carrying REVIEW.md. Where any build/soak failed file exists, the soak
step copies build/sessions logs into the captures' sessions folder before soak-check runs, so the
game-captures artifact, uploaded always, keeps them; the workflow parses, the glob was tried both
ways, the rule and script tests pass, 63. After `12a15e44`, Verdict 91's diagnosis mend, read at
14:14 on 2026-10-25, carrying the review's files and DEVLOG.md. The soak's log in the job's artifact
reads Tumble serving after 8 s and then NO_SESSION, the window's size and the first inputs through
by the failed file, the game gone with exit code 0 of its own by the next frames.wait; Tumble's loop
ends by WindowShouldClose alone and sets no quality, nothing recent touched the CLI, the soak
scripts or Tumble, no game's name sits inside another's title and the session files are written on
the main thread, and the twenty-game soak three times locally as CI runs it played every game
through, Tumble with two turns and twelve readings each time. The evidence was lost since every way
a loop ends writes a line naming what asked, and CI kept neither build/sessions/Tumble.log nor those
lines, soak-check quoting warnings alone; soak-check.py quotes the closing lines among a log's last
three, a SoakCheckTests case covers an ended game, and a local soak reads app.quit asked by e3d with
its process and its parent's command line, so a recurrence names its asker. The strict build clean,
2,112 plain and shuffled. After `17da0eaa`, item 123, Low's thin-wall leak, read at 13:04 on
2026-10-25, carrying NORM.md, SHARED.md, the review's files, DEVLOG.md, the two devlog notes and a
captioned pair, pushed with `12a15e44` and `073f34c8` at 14:24 on 2026-10-25 once the build run of
`ac46c010` went green whole, the fourth pack run started with publish on at 14:24 for the game's
items 95 and 29, green whole at 16:55 with Windows in 147 minutes under the limit of 240, the twelve
games played through in 50 minutes and the captures in 58 and 61, the package 5.2.313 pushed to
nuget.org, the first since 5.2.291 after three red pack runs, the push run green whole at 15:33,
Windows in 69 minutes, Linux in 47 and macOS in 35. lights.slang's byThinWall reads, at a point 0.3
of the probes' spacing off the surface where another surface lies nearer than the pixel's own by
over 0.75 cell, the field one cell through that surface against its gradient, and where it reads 0
or more the wall is thin and the pixel marches to the probes, the model pass using it where no
screen probe stands alone, so Medium and High are untouched. In the game at 1080p at Low 0.5% of the
room's pixels march and 0.75% of the spawn's, slivers at a few block corners, the room's scene pass
2.6 to 2.8 ms before and 3.1 to 3.2 after, the spawn within its noise. BounceRoomsTests.ThinWalls
reads the thin room's floor along the back and right walls' foot at 480 by 270 at Low with the lamp
outside and without it, a ratio of 1.54 before and 1.00 after under a bound of 1.1, the file partial
at 768 lines; the rooms at Low view 1 0.103 and view 2 0.107 with its bound 0.12, the rest
unchanged. The remainder is a speck where the thin room's right and back walls meet its floor, which
marching every pixel leaves too, RENDERING.md and the note saying speck. The strict build clean,
2,111 plain and shuffled after a first run lost 80 tests to the device running out of memory 61 s in
with nothing of the coder's holding the GPU, 41 on lavapipe with validation. After `ac46c010`, item
122, the screen probes' spacing by the resolution, read at 11:41 on 2026-10-25, carrying NORM.md,
the review's files, DEVLOG.md and a captioned picture, the glowstone room at 4K at every 8 pixels
and every 16, alike by eye, pushed at 12:42 on 2026-10-25 once the build run of `c721746b` went
green whole, its run green whole at 14:21, Windows in 99 minutes, Linux in 59 and macOS in 33.
TileAt(quality, height) is the larger of the 1080p tile and that tile scaled by the height over 1080
and rounded, High 8 at 720p and 1080p, 11 at 1440p and 16 at 4K, Medium 24 at 4K, the window and its
render textures taking the window's height so their tiles agree and the UBO's tile reading the same.
Read offscreen before and after with another session's tests on the GPU, so the times run high
alike. The glowstone room's global illumination at High, 720p 8.3 to 8.3 ms, 1080p 13.7 to 13.1,
1440p 17.8 to 12.4 and 4K 35.9 to 13.3, the scene pass with the probes 2.6, 4.9, 7.6 and 16.2 ms
before; Low's glow-only pass, global illumination and the scene pass, 2.5 and 1.3, 2.9 and 2.7, 3.4
and 4.8, 4.5 and 10.5; the march's scene pass 13.8, 29.2, 49.5 and 107.7; the Cornell box's global
illumination 3.3, 3.8, 4.5 and 7.2 before and 2.4, 3.7, 3.1 and 3.7 after, the strip room at 4K 8.4
to 4.4. The rooms against their references, each traced at 1080p with 256 paths and read in 96 by 54
blocks, at 4K with the new spacing within 0.002 of their 1080p distances in every view, 0.040,
0.087, 0.162, 0.221, 0.007, 0.086, 0.049, 0.039 and 0.018 against 0.040, 0.087, 0.163, 0.223, 0.007,
0.085, 0.051, 0.040 and 0.019, a fixed 8 pixels at 4K reading 0.160, 0.084 and 0.047 at views 3, 6
and 7, the reading's test not kept. The guide carries the four rows and the rule, RENDERING.md the
reading, a unit check of TileAt at the five heights. The strict build clean, 2,110 plain and
shuffled, 7 on lavapipe with validation. After `c721746b`, item 103, the screen probes by quality,
read at 10:53 on 2026-10-25, carrying the review's files and two captioned pairs, the thin walls
room and the Cornell box at Low with and without the probes. SetGlobalIlluminationScreenProbes is
gone, ScreenProbesAt(quality) true at Medium and High; at Low GlowOnly gives the glow lights' probes
while one is lit; ScreenOff, gi.toggle screen and the window's box, leaves them out whole at any
quality, the glow-only probes with them; gi.state says no screen probes at Low with the glow probes'
MB and says when the toggle left them out. The guide's tier table carries the Cornell box's
readings, Low 0.60 ms and 6.16 MB, 1.53 without glow lights, Medium 1.92 ms and 8.92 MB, High 2.00
ms and 11.16 MB, the paragraph the room's 2.2, 1.2 and 13.3 ms; the upgrading entry for the call is
dropped, the call never having shipped in a release; RENDERING.md has the reading and the feature
test's comment no longer says Low has screen probes. The tests of the probes moved to Medium,
BounceView, the history theory, the slide crawl, the probes past the first cascade and the
render-texture theory, Low's room bounds for views 2 and 3 at 0.13 and 0.29 with the readings in the
comment, a unit check of ScreenProbesAt and a window draw test new. The strict build clean, 2,108
plain and shuffled before the Metal rename, 54 on lavapipe with validation, the docs' 2 blocks
building on a fresh pack. After `f36b49b3`, Verdict 90's mend, read at 10:49 on 2026-10-25, carrying
the review's files and DEVLOG.md, pushed with `c721746b` at 11:29 on 2026-10-25 once the build run
of `9a013b59` ended, the third pack run started with publish on at 11:29 for the game's items 95 and
29 and watched, its games job red at 13:08 on Tumble's soak, Verdict 91, every other job green when
it ended at 14:11, the three suites with macOS in 56 minutes, Windows in 109 and the captures in 71
and 77, the pack job skipped, the push run green whole at 12:36 with macOS in 33 minutes, Linux in
58 and Windows in 67, so the Metal mend holds. The cause differs from the verdict's reading. Item
121 named a local vertex in field_counted.slang, uint vertex = push.source + id.x, and vertex is a
Metal keyword the translation kept, so 45:21 is the declaration and 50:58 its use in the index; the
pointer and the mask were sound, and the shader indexed id.x with no such local before 121. The
local is index, with a comment saying why. MetalNamesTests compiles every built-in shader with
slangc and reads each stage's OpName and OpMemberName strings, refusing Metal's own words, vertex,
fragment, kernel, visible, stitchable, intersection, device, constant, thread, threadgroup,
threadgroup_imageblock, ray_data and object_data, a second test checking the SPIR-V reading itself;
a full cross-compile to Metal source on Linux would not have caught it, the translation writing the
source and Apple's compiler alone refusing it, where the names check fails on Linux first and named
field_counted.slang's compute stage on the old shader, 55 s, skipped where slangc is missing. The
strict build clean, 35 on the GPU and 19 on lavapipe with validation, no full suite on this commit
alone, the tree's full suite having passed at 2,108 with item 103 beside it and this commit sharing
no file with 103, so the run is its suite. After `9a013b59`, item 121, the counted mesh's color
stream and its range of a shared buffer, read at 09:02 on 2026-10-25, carrying the review's files
and a captioned picture. Checked on top of item 102 and Verdict 89's mend, since 121 and 102 share
SceneField.cs, the strict build clean, 2,108 plain and shuffled on the GPU in 8 minutes, 83 on
lavapipe with validation over the counted meshes, the scene field, the reference frames and motion,
the docs page's 10 blocks building on a fresh pack. One departure, the picture's wall lit by a model
shader of its own with a normal from ddx and ddy in its vertex colors, since the model pass's own
shader draws a positions-only counted mesh black, as the docs say. In the red wall's room the floor
before the wall reads 233, 233, 233 with no colors and 222, 192, 192 with the colors beside the
UInt2 stream, the block's near face 191, 191, 191 and 186, 175, 175. After `c2fd2a40`, Verdict 89's
mend, Tempo's autopilot on the song's clock, read at 08:36 on 2026-10-25, carrying the review's
files, SHARED.md, DEVLOG.md and the game's ASKS.md, pushed with `9a013b59` at 09:40 on 2026-10-25
once the build run of `f05ea96f` went green whole, the pack run started with publish on at 09:40 for
the game's items 95 and 29, both runs watched; the pack run's macOS suite and macOS captures red
within minutes at 09:45 and 10:36 on the counted corners' shader failing to compile under MoltenVK,
Verdict 90, every other job green when it ended at 11:45, Windows in 125 minutes under the limit of
240, the captures in 60 and the games in 73, the pack job skipped, so it packed nothing and the
third pack run followed the mend at 11:29, the push run red the same way on macOS at 09:45 when it
ended at 11:27, Linux green in 46 minutes, Windows in 107 and the examples in 10. Each note whose
time this frame's step of the song passed is played at its own time, clamped into the clock before
the step and the clock after, as a player on the beat is judged however long the frame, several in
one lane in one long step each played, a note less than half a step ahead played on this frame as
before, Run.Play taking the song time a lane is played at while keys and pads play at the clock; the
game stays on the music's clock with no fixed stepping. The miss came from a stall frame far longer
than the frames before running the music's clock past a note not yet due by more than the 0.15 s Bad
window, so the autopilot's press on the frame after was refused. On a fresh pack offscreen, the
drive reads perfect 381, miss 0, error 3 ms with no validation error, and 300 frames draw clean;
with a 320 ms stall every 23rd frame through the whole song the old game missed 88 of 381 at perfect
186 and error 44 ms and the mended one 0 at perfect 323 and 19 ms, and at a steady 4 frames a second
both miss 0, so it was the stall, as on the runner under its capacity notice. Item 121 was set aside
in a stash and goes back in, its checks passed before, 2,108 plain and shuffled and 41 on lavapipe.
After `f05ea96f`, item 102, the far cascade's slab, pushed at 07:50 on 2026-10-25 once the build run
of `a8f127ad` went green whole, its run green whole at 09:38, Windows in 108 minutes, Linux in 59
and macOS in 32, carrying NORM.md, SHARED.md, DEVLOG.md, the review's files, the game's ASKS.md
entry and two captioned pictures. The slab came from the field's resolve, not the reading, and both
are mended. A cell in front of one face and straight behind an opposite one within a cell is held as
a sheet half a cell either side of its middle, as a double-sided sheet is, and the flat world's
ground is four blocks between grass tops and bedrock bottoms, so at the far cascade's 73.75 every
cell above it qualified and the ground stood about 37 blocks thick with the eye inside it; the far
cascade's build holds such sheets half the last counted cascade's cell thick, ResolvePush's lent.y
and a sheet argument on RecordSceneFieldBuild. In the reading, as planned, the last counted cascade
traces to the far edge with Last set and the merge takes nothing above it, and fadesOn in
cascades.slang stops it fading into the far probes in gi.slang and lights.slang; the reading's
changes alone did not move the game's numbers, the sheet mend restored them. Bisected in the game by
gi.probe at the wall's cascade 3, up-rays meeting a surface within their interval and ground hits
shaded four times too dark from a sun march blocked overhead. On a scratch copy of the Debug build,
the flat world, the play check's glass at exposure 1 and far at the far plane, the frame read 168,
157, 106 and the middle 139, 12, 0 before against 185, 167, 126 and 154, 13, 0 with no far cascade,
and after the same as no far cascade; the 27 bounce rooms with far at infinity within 0.007 of
before except view 7 at Low, 0.108 to 0.069, its bound without far 0.09. The far cave at 90 blocks,
read either way, moves not at all, the tops 9, 13, 18 at High under every cascade alone and with the
merge off and 0, 0, 0 with the game's fog off, so that distant light is the game's fog and no
cascade's light reaches those tops. The far cascade costs the scene pass 0.2 to 0.3 ms at the grove
at 1920 by 1080, 8.0 to 8.3 against 7.8 to 8.0, and GI 0.25 ms, before and after alike, the game's 5
ms having been on 5.2.281 before item 96's cuts, so nothing is left to cut. Two tests, a
double-sided plane of 288 triangles, since a mesh under 16 is a stamped box, waiting
MobileSettleFrames after its move, whose cell a quarter of a far cell over the floor reads 1.59
where it read -1.84, and the grazing floor at Low keeping its difference with a far cascade set,
failing before at 0.108, the rooms' body moved into a Differs helper; a narrower far hit tolerance
tried and dropped, changing nothing once the sheet was mended. RENDERING.md's far cascade passage
rewritten with the readings. The strict build clean, 2,104 plain and shuffled on the GPU, 73 field,
reference frame and cascade edge tests on lavapipe with validation. Item 96 begins with a reading on
the game's 5.2.215 build at the cherry grove at 1920 by 1080, each part of the per-pixel read cut
out in turn to tell what runs per fragment from the variant's registers, the window's depth at half
size and the scene pass shading with overdraw pointing at a half-size pass over the window's depth
with a two by two depth and normal read, as the glow pass has. Next 100, the field's foliage share,
then 125, the scene pass's half, then 126, the materials' defaults, then 111, the shadow's penumbra,
then 112, the planar reflection, then 93, the colons out of the documents, then 91, DESIGN.md's
words on the loop and the ECS, then the glow light's non-number and the history's guard, then 95,
the e3d help, then item 89, the plant walls' acne, then 97, the light at the camera's point on
lavapipe. The branch `modeling` was deleted at 07:23 with its items all on main, N 7.5; the cloud
sessions stand by.

Before it, `a8f127ad` came, item 108's third phase, with `ac1c6bad`, item 120, pushed at 06:43 on
2026-10-25 once the build run of `6a20d165` went green whole, its build run green whole at 07:41,
Windows in 57 minutes, Linux in 35 and macOS in 32, the pack run started with publish on at 06:43
for the game's item 95, its macOS job red at 07:33 on Tempo's autopilot, Verdict 89, every other job
green when it ended at 08:40, Windows in 82 minutes, the captures in 59, the games in 73 and macOS's
captures in 57, the pack job skipped, so it packed nothing and a pack run follows the mend.

Before it, `55a33ce9` came, item 116, a profile value's mean, with `6e29cfa0`, item 115, pushed at
03:05 on 2026-10-25 after the build run of `e746f98e` went green whole, its run green whole at
04:51, Windows in 106 minutes under the limit of 150, Linux in 45 and macOS in 31.

Items done and verdicts settled before 2026-10-23 stand in REVIEW-ARCHIVE.md beside this file, their
numbers kept, moved there at 19:30 on 2026-10-23 so this file holds the current work alone and is
read whole before a batch; the archive is read when an item or a verdict named here is wanted in
full.

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

89. **Thin two-sided masked faces carry no shadow acne.** Read under item 71 on 2026-10-24 in the
    voxel game: a wheat field's thin cutout walls shadow themselves in speckled dark and light
    stripes that shift as the eye turns, the map's bias of three texels along the normal being
    nothing for a face the sun grazes edge-on and seen from either side, while solid blocks, leaves
    and the water's top are clean. The mend chosen by reading in an engine room built as the game's
    field, the wheat's walls on farmland under the sun at 10 o'clock, against the path-traced
    reference by region: a receiver bias by the face's slope to the sun for two-sided masked faces,
    or the casters' side read from the sun's side, whichever leaves the wall's lit face even within
    a level and the shadow it casts on the ground where the reference has it, the covered-fault rule
    across the bounce rooms and the game's room at both sizes; a test reading a thin cutout wall's
    lit face for stripes, the variance along it under a bound, failing without the mend;
    RENDERING.md's line under the shadows, the voxel game's plants' shadows the first to gain. One
    commit, after items 74 and 72's cherry-picks.

91. **DESIGN.md says the loop and the ECS are both the engine's way, each where it fits, with the
    voxel game as the model (Decision 68).** DESIGN.md's opening and its fifth section say the ECS
    is underneath for a program that grows into it; the owner's word of 2026-10-24 is that both are
    the engine's way and a program chooses by what fits, the loop of flat calls for the frame, the
    drawing and the interfaces, where raylib's way reads best, and the ECS for the things that move
    and the data that lives across frames, where behaviors, queries, states and the scheduler read
    best, the ECS being this engine's own feature over raylib. So DESIGN.md's opening says both, its
    fifth section is written again as the choice with the voxel game, Voxel-Sandbox, named as the
    model of the split, the mobs, drops and sections of its items 94 to 96 as entities and its
    frame, drawing and interface in the loop; a guide page of its own, docs/the-loop-and-the-ecs.md
    or a name the coder finds better, says when each fits with a short example of each, a behavior
    that moves a thing and a loop that draws it, built on the ecs_behaviors example so its blocks
    compile under the guides' check, the guide table gaining its row after the first game's; the
    README's sentence on the ECS says the same in a line. The engine's own small games stay as they
    are. One commit, docs only, after item 90; the gaps the game's port finds come to this engine
    through ASKS.md as items ahead of its other work, by the Decision.

93. **No running sentence holds a colon, across the documents, the comments and the scripts.** The
    owner wrote at about 10:34 on 2026-10-24 that some documents join sentences with a colon and
    that STYLE.md forbids it, the devlog among them. STYLE.md's own check, the awk over the Markdown
    files outside their code and the grep over the comments and scripts, lists the hits, and each is
    read and recast as STYLE.md says, a sentence of its own or the same sentence recast, a label at
    a line's end or in a table left as it is, over the guides under docs, RENDERING.md, E3D.md,
    CHEATSHEET.md's comment lines, ASKS.md's entries, and the comments of the code and the scripts;
    the review's files are the reviewing session's own and are not touched. One commit, docs and
    comments only, after item 92.

95. **A verb of ./e3d with --help prints its help and opens nothing.** At about 11:11 on 2026-10-24,
    and once before at 10:0x, `./e3d open --help` passed --help and --serve through to the default
    example and opened core_3d_camera_free in a shown window on the owner's desktop, which they
    noticed. The client answers --help on any verb with that verb's usage from E3D.md's words and
    exits, before any process is started; a test of the client's open with --help starting no
    process. One commit, small, with the next batch.

97. **A point light at the camera's own point shades without black specks on lavapipe.** From the
    voxel game's reading of 2026-10-24, read at 12:21, its ASKS.md entry of 2026-10-24. The game
    moved its held light to the eye's point and the play job on lavapipe read the lit roof black at
    its probe, the frame shot through with black specks, where the GPU shaded it 109, 68, 29; the
    shadow cube, the contact shadows and the global illumination each off left the specks, and the
    light a third of a block under the eye made the frame clean, so the fault is in the engine's
    shading of a point light whose point is the camera's, a value undefined where the light's vector
    and the view's coincide or the light's point in view space is the origin, as item 90's normal
    was. The cause read on lavapipe with the validation layer, the mend a guard at the degenerate
    value as item 90's, and a test in the Linux suite lighting a bounce room by a point light at the
    camera's own point and reading the frame for black pixels, red before the mend. One commit,
    after item 95.

100. **The field lets a share of light through a cell marked as foliage.** From the voxel game's
    reading of 2026-10-24, read at 14:02, its ASKS.md entry of 2026-10-24 carrying the table of its
    readings. The game's rain forest floor at noon is near black where the source lights it at 13 of
    15, since the canopy shadows the sun and the field holds the leaves solid, so no sky light
    reaches the floor through the probes; the game lifts the floor by its sky level meanwhile. A
    cell written into the field can carry a transmittance, a share of the light passing it, set by
    the program for leaves and the like through the field's write, the cascades' march taking that
    share off a ray that crosses the cell rather than stopping and the cell's color bouncing the
    rest, the sun's shadow through the shadow map unchanged; the cost read on the bounce rooms and
    the voxel game's grove at both sizes, the readings against the references under the
    covered-fault rule, a test of a plane under a slab of foliage cells at a half reading about half
    the open plane's sky light, and the guide's page saying how a program marks foliage. One or two
    commits, after item 96 and before 93. The first part at `4c9cf7e4` on 2026-10-25, LightThrough
    on both materials with the rays crossing up to four surfaces and gi_screen shading in two
    places, the departure in the head; the second part, the grove's readings, the canopy's picture
    and the crossing's cost with leaves, after item 124.

111. **The sun's shadow softens with the distance from its caster.** From the voxel game's item 43
    at 22:29 on 2026-10-24, its ASKS.md entry of 2026-10-24; the look the owner aims at has a warm
    low sun with long soft shadows, and the package has no soft-shadow call beside
    SetShadowDistance, SetShadowFill and SetShadowMapSize. The shadow map gains a penumbra widening
    with the distance between the receiver and its caster, read from the map's depths about the
    sample as percentage-closer soft shadows do, with the sun's angular size as its scale through
    SetShadowSoftness(size) and a default that keeps the present look, the contact shadows
    unchanged; the cost read on the voxel game's grove and the bounce rooms against their references
    with the softness at the default and at the sun's size, and a test reading a pole's shadow
    sharper at its foot than at its tip; the guide's shadows paragraph and the cheat sheet's line.
    One commit, after item 100 and before 93.

112. **A flat surface mirrors the scene through a planar reflection a program sets.** From the voxel
    game's item 42 at 22:46 on 2026-10-24, its ASKS.md entry of 2026-10-24; the look the owner aims
    at has calm water mirroring the sky and the shore, and the engine's traced reflection makes no
    mirror of a flat surface, the game's pool reading a blur of a red wall where its image would
    lie, while the box probes suit a room. The engine takes a mirror plane from a program,
    SetReflectionPlane(height, normal) or one on a material, draws the scene into it from the
    mirrored camera at a lower resolution with the shadows and the light reused and clipped at the
    plane, and gives the material the texture to sample by the screen's place, with a fade by
    distance and a roughness blur, usable from the transparent pass; the cost read on the game's
    river at the grove and on Summit's water, a test of a plane under a lit cube reading the cube's
    image mirrored, and the guide's page on reflections saying when the plane, the probes and the
    trace each fit. One or two commits, after item 111 and before 93.

125. **Why the scene pass halves when a game's meshes are entities, and whether DrawMesh can have it
    too.** From the voxel game's reading for its item 95 on 5.2.313 at 17:35 on 2026-10-25. With
    three of a section's seven parts moved from DrawMesh to mesh entities and the same sections,
    draws, triangles and pixels in every view, the grove's GPU scene pass at 1920 by 1080 at High
    fell from 19.8 to 21.2 ms to 9.8 to 10.3, the frame from 35 to 46 ms to 23.2 to 24.0, and the
    CPU's recording of the passes more than halved, cpu.shadows 8.6 to 11.9 ms against 4.5 to 6.1,
    MeshEntityDraws costing 0.25 to 0.33 ms; the captures match within an RMSE of 0.0011. The GPU's
    half is unexplained, the draws being the same. The coder reads where the time goes in both paths
    with the GPU's timestamps and the pass's draw order, the draw order and the state changes
    between draws, the per-draw uniforms and descriptor writes, the depth prepass's coverage or the
    batches the entity path builds, and names the cause; where DrawMesh lacks what the entity path
    has, it gains it, so the engine's games and the game's four parts still on DrawMesh take the
    same frame, read on the grove copy and the thousand-mesh program of item 108 before and after.
    After item 124 and before the rest of item 100. One commit with a reading first. A line from the
    game's run at 18:09: with its sections as entities Vulkan's validation notes, twice a run on a
    Mesa without maintenance4, a pipeline whose vertex stage declares an output at location 11 that
    its fragment stage never reads, the write discarded, the terrain shader's vertexMain paired with
    a fragment stage of the engine's in some pass, likely the shadow or the prepass; the reading
    says which pass pairs them so, and whether the pairing is part of the cost.

126. **The ECS Material and ModelMaterial agree on their defaults.** From item 124's reading of
    18:54 on 2026-10-25. The ECS Material's constructors set DoubleSided true where ModelMaterial
    defaults to single-sided, so a default mesh entity draws both faces where a default DrawMesh
    draws the front alone, the one path costing a face more than the other for the same mesh and the
    two guides describing one material two ways; the voxel game copies the flag across and is
    unaffected. The ECS Material's defaults become ModelMaterial's, DoubleSided false among them,
    with a test holding every shared field's default equal between the two, the scene reader's
    Material.Default following, and the behaviors guide saying a mesh entity's material defaults as
    a model's does; no upgrading path, under the rule of 2026-10-23. After item 125 and before item
    100's second part. One commit, small.

## Verdicts

Verdict 92 is open. Verdicts 56 to 91 are settled, said under each in REVIEW-ARCHIVE.md.

92. **The runs of `23b4793b` are red on the runners' dotnet install, not on the engine.** Read at
    20:20 on 2026-10-25 from the two runs' jobs, the push run 40225189195 and the pack run
    40225189793. Every suite on the three systems failed in a minute at setup-dotnet, asked for
    10.0.x and resolving 10.0.403, which the install script could not find as an SDK, nor the
    runtime 10.0.14; the commit changes neither global.json nor the workflow, the release feed's own
    index says the latest SDK is 10.0.402 at this minute, and the run of `4c9cf7e4` installed two
    hours before without a word, so the action and Microsoft's feed disagreed for a while on a
    release being published. Nothing to mend in the tree; the failed jobs of both runs were run
    again, and the verdict settles when they go green. If they fail the same way, the next attempt
    waits an hour; pinning the SDK is not taken, since 10.0.x is what the three systems' runners
    share. The second attempt at 20:20 failed the same way in a minute on all three systems; the
    third waits an hour, at about 21:25. At 20:38 the feed served the 10.0.403 archives for the
    three systems, so the failed jobs were run again then, ahead of the hour. That attempt failed
    the same way at 20:39, the install script still not finding the SDK though the archives answer;
    the next attempt waits for the hour, at about 21:25.

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

7. **A run that fails says what failed in a page, and the reviewing session is given no log.** The
   owner chose it on 2026-10-05, after a log pasted into the reviewing session ended it. The suite
   runs whole as it does, and in parts only after a process is lost, which the owner chose over
   parts on every run. A test in which the engine logs an error fails unless it says it expects that
   error, with a list of the tests that log one today. The norm has these as N 6.7, N 6.8 and N 3.7,
   and the reviewing session reads a run's jobs and annotations from GitHub. On 2026-10-20 the
   Windows suite was set to run in its parts from the start at `14890db6` under Verdict 72, since
   lavapipe's Windows build keeps no shader cache and a whole run there is lost at its hour on every
   slow runner, 121 minutes with the parts run again where the parts alone take 62; the coder raised
   it against this decision's second sentence, and the reviewer put it to the owner, the Windows job
   standing as committed until the owner's word, Linux and macOS running whole as before.

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

50. **The review's files are committed by the batch that comes next, and by the reviewer at most
    once a day.** The owner wrote on 2026-10-20 that the review's commits had become nine of every
    ten in the history and were to stop, and then that the reviewer is to edit the review's files in
    the tree and commit nothing, as it did before 2026-10-19, with one commit a day at most where
    that makes sense. So REVIEW.md, SHARED.md and NORM.md stay edited in the tree and the coder's
    next batch carries them, as AGENTS.md has it; the reviewer commits them in a commit of their own
    only at a day's end where no batch carried them, never for a reading, a run or a package. The
    coder adds one sentence saying so to COMMITS.md in the next batch, in the paragraph on what a
    commit carries, naming the review's files, the batch that comes next and the one commit a day at
    most, and no one who decided it (N 4.7).

51. **Modeling in both engines, from the kernel to the modifiers, grid prototyping first.** The
    owner wrote at about 23:47 on 2026-10-20 that the engines are to support modeling too, beyond
    the primitives they have, Blender's kind, possible in a game and all of it from the command
    line, simple to use so it is the more powerful: grid prototyping as the essential tool of world
    design, with textures of different colors showing a grid of about a meter, stairs, and windows
    through boolean operations; processing texture coordinates and adding textures to surfaces; and
    later selecting a face, insetting and pulling it, merging, beveling and Blender-like modifiers;
    the reviewer to shape the best solution, adding and removing as it sees fit, and to ask what
    needs asking. The reviewer's shape, the same in both engines by SHARED.md's rows and each
    engine's own underneath: items 59 to 66, an edit mesh of faces of any count with a selection and
    a recipe of operations that is its history, its undo and its saved file, built into the engine's
    meshes and colliders; the grid materials, world-aligned texture coordinates and booleans by
    binary space partition, written in the engine since the dependency table takes no library for
    it; the commands, one an operation in the recipe's words, and an ImGui window a program draws
    with face picking by ray and a drag to extrude, no editor application in 3DEngine and the
    editor's panel in BevyCSharp; texture coordinate projections and materials by slot; a game
    blocking out a level from one recipe; the face and edge operations; a modifier stack applied at
    the build; and glTF written by the engine and read into an edit mesh, so Blender opens what the
    game made and the game cuts what Blender made. Each engine's coder takes the items after its
    current work, item 58 and the feature test's sixth part in 3DEngine and item 37 in BevyCSharp,
    in the items' order, the two in parallel to one text, the words of the recipe and the commands
    the same in both. The owner is asked the game's name, Blockout failing a word, whether the glTF
    export comes earlier than last, and whether a dependency for robust booleans is wanted later
    should the partition's limits show.

52. **The Actions bill is not weighed.** The owner wrote at 00:06 on 2026-10-21 that the bill need
    not be considered, the runs being free of charge for them. So a push's build run is left to run
    beside a pack run started at the same time and is never cancelled for its cost, macOS stays in
    every push, the three systems' suites run on every push as they do, and the reviewer's reading
    of the month's minutes stands as a reading and not as a reason; a run is still cancelled where
    it is doomed, so its figures are not waited for, and a push still waits for the run before it to
    end, so no figure is lost.

53. **Animated captures as tooling for documentation and devlogs.** The owner wrote at about 00:29
    on 2026-10-21 that it is to be possible to capture, or to make animations from several captures,
    with timings such as a second or 250 ms between the pictures, to have more tooling to show
    things in the documentation or in devlogs. So item 70: a recorder in the engine taking frames at
    a step for a length and writing a GIF or an animated PNG by encoders of the engine's own, a
    client step for it, and a script assembling an animation from captures on disk, with one
    animation in the README as its first use; the same in both engines.

54. **The voxel game leaves the screen probes out, and the cascades' faults the owner sees are read
    and mended.** The owner wrote at about 01:00 on 2026-10-21 that the voxel game is not to enable
    the screen probes, whose look is pixelated even where that is not meant, that the radiance
    cascades without them are the better option for the game's art, and that the cascades still have
    graphical faults, a light block's light leaking through the thin gap at the corner of two
    columns that hold it off, penumbrae on the ground with zig-zags, the screen probes shifting on
    movement, z-fighting at points between the global illumination and the opaque textures, and
    noise, seen in the bounce rooms 3 to 6. So item 71: a call that leaves the screen probes out,
    which the game takes as its default, and the faults reproduced from the game's repros in the
    engine's own scenes, read to their causes and mended one by one under the rule the readings
    keep, or named where the method stops.

55. **A fill cascade to the far plane for the shadows and the field, not counted.** The owner asked
    shortly before 03:17 on 2026-10-21 why the global illumination ends at a range in the voxel game
    while the clouds reach the far plane, took the cause to be the scene field's cascades not
    reaching it, and proposed that the last cascade be followed by one filling up to the far plane
    that does not count in the cascade count, for the shadow maps, which they had never understood
    to end short of the far plane, and for the field alike, asking to be corrected if it is not
    feasible. The reviewer answered that the reading is right, the field reaching 64 from the eye
    where the game draws 192, that a fill cascade is what engines do for the sun and costs one depth
    render at low resolution, that the field can take a far cascade sized to the far plane in place
    of doubling at the cost of one cascade with coarse cells that suit far land, and that the
    surfaces past the field want a sky and ground ambient faded in at the edge so the border never
    shows; the owner said yes. So item 72, after the cascade faults.

56. **A screenshot on F2 in every program.** The owner wrote shortly before 03:42 on 2026-10-21
    that, as in Minecraft, a screenshot is to be taken in the game with F2, the same for BevyCSharp,
    the captures put in the root or a subfolder. So item 73 in this engine, item 47 in BevyCSharp
    and item 40 in the voxel game: a built-in key writing the frame into a screenshots folder beside
    the program, named by the time as the source names its own, movable and off by a call.

57. **What the engine learns from the voxel game.** The owner asked shortly before 06:54 on
    2026-10-21 what the engine can learn from the voxel game, naming the darker lower hemisphere of
    the skybox overlaid with the horizon's color as an engine fix since Hopper has the same dark
    lower sky, and what else there could be, and said to do it as the reviewer sees fit and write it
    down. So items 75 to 77: the sky below the horizon in the fog's color after item 74, a command
    reading a rectangle of the last frame for the drives after it, and a day sky driven by the hour
    with the sun, the moon, the stars and the dusk from the game's numbers after item 60. F2, the
    recorder, the readback, the GPU mesh, the glow lights' bound, the cascade faults, the field's
    budget and the fill cascades already came from the game's asks; the resource pack reader, the
    in-wall overlay and the block lighting stay the game's own; the water's surface is judged when
    the game's exists.

58. **F1 hides the interface and F3 shows the debug overlay, in both engines.** The owner asked
    shortly before 07:05 on 2026-10-21 for F1 to hide the interface as Minecraft does, as an engine
    feature, F2 the screenshot, and F3 a debug overlay like the newest Minecraft's, sorted in parts
    logically with modules a game adds, world generation among them, showing the system, the camera
    and the renderer, memory, fps and frametime with a graph like the RivaTuner overlay's, both for
    BevyCSharp and 3DEngine, F1 hiding the interface and ImGui but not the F3 overlay so everything
    hides but the overlay over the renderer, and Shift+F3 showing the frame debugger too; and
    answered the reviewer's questions shortly before 07:06: the interface hidden by a scope, F3 the
    overlay's on its release alone with any chord left to the program, the look Minecraft's with no
    header, window styling or border, text on a transparent backing, and the order as the reviewer
    sees fit. So item 78 here and item 49 in BevyCSharp, the same in both, after item 71's second
    part here and after Blockout there, and the voxel game taking them when the package comes.
    Shortly before 07:10 the owner added that Minecraft hides its HUD only while the player's camera
    shows and not in the pause or any other screen, so hiding every panel at any point, even in
    menus, would be wrong; the scope gives that, the HUD inside it hidden and a screen drawn outside
    it never, which the items say.

59. **A denoiser for the ray-traced tier of global illumination, beside the cascades, in both
    engines.** The owner asked at 08:42 on 2026-10-21 why BevyCSharp has no denoiser, was answered
    that Bevy's Solari at 0.20 knows only DLSS Ray Reconstruction, which the bridge does not build,
    ReSTIR halving the noise meanwhile, and said shortly before 08:56 that a denoiser for Solari is
    necessary as the higher-definition solution for global illumination beside the radiance
    cascades, and that it is implemented for both engines in the future. So the engines carry two
    tiers, the cascades everywhere, which need no denoiser since they integrate over directions, and
    a ray-traced lighting with a denoiser where the GPU has ray queries, in BevyCSharp over Solari's
    output as its item 54 and here over a ray-traced lighting pass of the engine's own once the
    cascades settle, items 71, 72 and 74 mended, the path-traced reference the engine draws being
    the seed of such a pass; no item yet, written when the cascades are settled. The owner holds the
    source of H-Trace, a Unity asset received from its developer as a patron; it is read for the
    method's shape and its constants as the modpack's shaders were read by the voxel game, and
    nothing of its code enters either repository, the license being the owner's to use and not to
    publish, unless the developer grants it in writing, which the owner may ask.

60. **What the owner saw in the voxel game on 2026-10-23 that is the engine's.** The owner, having
    played the game, wrote of a visual fault with the height where the land below a line is lighter
    and above darker, the same fault the game's mobs had, and of the shadow cascades, the band
    between two cascades empty and a tree's long shadow at 19:30 cut off in the middle; the first is
    the model pass's normal turn reaching a program's own samples, item 82, and the second the
    cascades at a low sun, item 83, both after item 81 and before the rest of item 71. The game's
    own items of that list stand in its REVIEW.md.

61. **The owner's cloud sessions take GPU-free items on branches of their own.** On 2026-10-23 the
    owner opened a cloud session on each engine with 200 dollars of usage of their own, four cores,
    no GPU and no display, each able to push to one branch of its own and never to main, this
    engine's on `claude/eloquent-brahmagupta-ljq2ri`, with github.com refused by its network so
    slangc cannot be fetched and the shader and device tests skip until the owner allows the domain,
    and BevyCSharp's code, the model of the modeling items, not in its reach until the owner adds
    that repository to the session. A cloud session reads REVIEW.md, COMMITS.md, STYLE.md and
    NORM.md as a coder does, commits in COMMITS.md's form on its branch, edits none of the review's
    files, and reports to the reviewer in a few lines; the reviewer reads its commits from the
    branch, and the local coder cherry-picks them onto main as the first part of its next batch,
    taking the captures and the device tests the cloud cannot, so main keeps one writer and the
    pushes stay the reviewer's.

62. **Branches are named for their work, never claude/ or a random string, and work stays on main
    where one session writes.** The owner wrote at 20:16 on 2026-10-23 not to create branches named
    claude or the like and not to name them with random characters, but to work on main, or, where
    several sessions on one repository make a branch necessary, on a branch with a descriptive name
    and no claude/ prefix. So the cloud session's work moves to a branch named modeling, and its
    claude/ branch is deleted once its commits are on main; a branch exists only while a second
    writer needs one.

63. **Every commit carries the owner's identity, and the cloud sessions' branches were deleted for
    not carrying it.** At 20:27 on 2026-10-23 the owner saw that the commits on the cloud sessions'
    branches named Claude as their sole contributor, said no to it and asked for the branches to be
    deleted and gone for good; the reviewer deleted `claude/eloquent-brahmagupta-ljq2ri` and
    `modeling` and their workflow runs. N 7.6 holds the rule: every commit's author and committer
    are the owner's identity on every branch, a session setting it before its first commit, a commit
    with another identity pushed nowhere and rewritten first. The cherry-picks on main, `ffb15cb6`
    and `4c3f01ef` for item 59, carry the owner's identity. The cloud sessions were told to stop
    pushing, set the identity, rewrite the authors of every local commit not on main and push
    nothing until that is done, then push `modeling` again, which the owner confirmed in that
    session.

64. **Until a full release, both engines are used by nobody.** The owner wrote at 21:36 on
    2026-10-23 that an upgrading entry is not needed since nobody uses the engine, said "Drop" to
    the rule that every new public name has its line on 3DEngine's upgrading page, and added that
    until a full release both engines are to be considered used by no one. So a change of the public
    surface is made outright, with no entry for a name gained or lost, no deprecation and no path
    kept for an older caller; the package's release notes written from the commit messages are the
    record of what changed, and the upgrading page keeps its Changed section alone, its tests for
    names gained and lost going as item 85. The owner's word ends this at the full release.

65. **The two cheat sheets have one form.** The owner wrote at 21:39 on 2026-10-23 that the cheat
    sheets are to be coherent: 3DEngine's lines one-liners as BevyCSharp's are, and BevyCSharp's
    section headers as 3DEngine's, plain words without the code styling. So both pages are a `#
    Cheatsheet` heading, a paragraph saying what the page is and what writes it, then `## Group`
    headings in plain words, each group a guide link line and one fenced block, and in the block one
    line a member, the signature and then `//` and the first sentence of its documentation on the
    same line however long, no continuation lines and no sub-heading a type.

66. **A devlog under `.github/devlog`, with AGENTS.md's row for it.** The owner asked at about 04:35
    on 2026-10-24 that the pictures the sessions made be saved with a text in a folder under
    .github; `.github/devlog/README.md` holds what landed by day, newest first, with the
    repository's captures and the items closed, written by the reviewing session and carried by the
    coder's commits like REVIEW.md, and the owner gave this repository's session the word for
    AGENTS.md's Documents row at about 04:55. At about 07:56 the owner asked that the devlog be
    `DEVLOG.md` at the repository's root, with no `.github/devlog` folder, the engine's pictures
    being its committed captures; `.github/devlog/README.md` goes and AGENTS.md's row names
    `DEVLOG.md`.

67. **Holographic Radiance Cascades read and set aside until a three-dimensional form exists.** The
    owner asked at about 09:16 on 2026-10-24 about Freeman, Sannikov and Margel's paper of May 2025,
    arXiv 2505.02041. It is a variant of radiance cascades for two dimensions: a probe grid that
    loses spatial resolution only along the direction a probe gathers from, per quadrant, so a small
    distant light's penumbra stays resolved where standard cascades blur it, and an acceleration
    structure that builds long ray intervals by merging short ones, so the cost is constant for a
    grid's size and volumetrics cost nothing more; single shot, noiseless, no history. On a 3080
    Laptop it takes 1.85 ms at 512 square and 7.67 at 1024 square, with checkerboard and moire
    artifacts for lights under eight times the base probe spacing, a four-pixel blur of shadows, and
    four output directions, so no specular. Its scaling in three dimensions is the paper's own
    limit, memory of N to the fourth for an N-cubed scene, shown as an experiment at 128 cubed
    without normals. So nothing of it is taken: the engine's cascades over the field with the screen
    probes stand, and the paper's one transferable idea, resolution kept across the gathering
    direction, is the shape of the sixth term's weakness, a bright patch narrower than a probe's
    texel, and is noted beside it for a later experiment on the glow lights' near field should a
    three-dimensional form be published.

68. **The loop and the ECS are both the engine's way, each where it fits.** The owner wrote at about
    10:24 on 2026-10-24 that the voxel game should move to the ECS where it fits, the ECS being this
    engine's own feature over raylib and a choice BevyCSharp's Bevy does not leave, and that the
    choice should not be to ignore it; the earlier reading that the design should reflect raylib
    stands for the frame, the drawing and the interfaces, and the ECS takes the things that move and
    the data that lives across frames. The owner answered the reviewer's questions: the voxel game
    moves its moving things, its sections as mesh entities and its player and states, piece by piece
    after its items 92 and 93; where the ECS falls short the engine grows first, each gap an ASKS.md
    entry taken ahead of other engine work; DESIGN.md says both with the voxel game as the model,
    and the engine's own small games stay as they are. Item 91 here, items 94 to 96 in the game.

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

## Replies

Shared: a mesh that replaced a still one in place is not stamped as boxes while a cascade the
budget left for a later frame still holds the one it replaced, where a voxel game's section stamped
as its boxes filled the air above its ground in those cascades and a room's indirect light went for
a frame eight after a block broke (`SceneFieldPlan`).

Shared: a game reads the force a joint held its bodies with over the last step,
`GetPhysicsJointForce` (`fdcfd435`), the impulse Bepu keeps for the joint's own constraint over the
length of a substep, which a box of 3 kilograms hanging on a distance joint reads as its weight
within a percent at one substep and at four.

Shared: a scene component or one of its fields keeps its saved levels across a rename by
`[FormerName]`, after BevyCSharp's, a type's former name alone placed in its namespace and one with
a dot taken as full, the registry answering by each former name after every current one, and the
generated reader trying a field's former names where its own is missing (item 45, the renames).
A component's layout is numbered by `[DataVersion]`, after BevyCSharp's, its static `Migrate`
called once with the version the file holds, written as a run of checks on it, the number written
under `"$version"` once past 1, a versioned type with no migration warned of (E3D200) with a fix
that adds one, and a component of a later version read as it is and saved back at that version
with the values this build did not read and the fields it knows set from the entity (item 45, the
versions). SHARED.md's row 120 can say both have it.

Item 103 is held on its cost. The voxel game leaves the screen's probes out by default
(`LightSettings.ScreenProbes`), so it runs on the glow-only pass the item takes away, the pass item
71's sixth term made for the game's entry. In the entry's room of forty glowstones at hour 23, all
in view, on a scratch copy of the game at `f05ea96f` at 1280 by 720 at `High` with the frames
unlimited and gnome-shell taking 62% of the GPU, `gpu.hdr_scene` read 1.29 and 1.19 ms with the
probes left out, 13.31 and 13.28 ms with them traced and unread, which marches each pixel to the
glow lights as the item has the probes left out do, and 2.24 ms with them read; the frame took 5.2
and 4.8 ms left out and 21.3 ms marched. The two frames show no difference that the eye finds.
Done as written, the game's default frame in a room of light blocks would take twelve milliseconds
more, or go back to the probes the game left out for their moving light. Two forms keep the cost,
for the review to choose between. One keeps the glow-only pass as the call's off, the global
illumination window's toggle renamed and moved as the item says, and the guide saying the call's
off leaves a pass for the glow lights alone while any is lit. The other gives the glow lights a
list of those each screen tile sees, a pass of its own with no probe placed, which is more than one
small commit.

Shared: a material lets a share of global illumination's light pass a unit of its depth
(`LightThrough`), the field's colors carrying it in their alpha and the probes' rays crossing such
a surface, keeping the share for each unit and taking the rest of its light (item 100). A frame of
none traces with a build of the trace that leaves the crossing out (`NO_LIGHT_THROUGH`), since the
trace's light written as the crossing writes it, summed over what the ray met and set after, cost
7% at Low with the crossing compiled out, where the light written at the hit cost nothing; the
screen's probes shading in two places where they shaded in three took 0.16 to 0.60 ms off High.

Shared: a mesh entity's `Material` carries `CastsShadows` and `BouncesLight`, true as its
constructors make them and a scene file's older entries read them, as `ModelMaterial`'s do for a
mesh drawn by a call, which leave it out of the shadow maps and the scene's field or of the field
alone; on the material rather than a component each, since a look is built once for all its
entities and a frame reads nothing more (item 124).

Shared: the model pass draws a view's opaque batches by the pipeline each draws with, in the order
each pipeline first comes and nearest first within it, each run between two batches that write no
depth apart (item 125). Small draws alternating between two pipelines, a voxel game's solid and
cut-out section faces through `DrawMesh`, bound a pipeline 315 times a frame and took the scene pass
13.1 to 14.1 ms on the GPU, where by pipeline 9.2 to 10.6 and nearest first within it 8.4 to 9.0;
the order costs 0.03 to 0.15 ms of recording for a thousand draws of one pipeline in Release.
