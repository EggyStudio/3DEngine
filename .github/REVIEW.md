# Review

Reviewed up to `94097b62`, 242, a ray's hit marching the brightest glow light in reach and gathering
the rest as one, which brings the screen pass from 2.6 to 2.1 ms with the door and gallery views as
they read, pushed at 22:41 on 2026-11-09 with `47a5972b`, 241, a glow light's reach floored at half
a probe spacing and Sponza's lamps lighting their surroundings, warm and cool, and `86339f72`, 243,
Sponza's six views at the top of the README, one run on the last and the pack run with publish
started on it at 22:41, since the pack run tests what it packs; they keep the form, the last
carrying the review's files and the game's two asks. The game pins the package it publishes, the
first with the flicker mend and the lamps. Before it `ed469e90`, 237, the illumination's cycles
eased in the merge and the screen filter, pushed at 20:18 on 2026-11-09, its run green at 22:13 with
macOS in 45 minutes, Windows in 98 and 115, Linux in 104 and the examples in 10; it keeps the form.
Before it `c23cd2d4`, 226's fourth and last commit, green at 20:15 on 2026-11-09, whose pack run
published 6.2.8 at 21:08, the package 3DEngine, its twelve areas and the templates, tagged `v6.2.8`,
which settled Verdict 111. The release scene-packs holds intel-sponza.pack since 17:58 on
2026-11-09, so ./e3d scenes fetch works. nuget.org holds 6.0.11, 6.0.14, 6.0.91, 6.1.12 and 6.2.8,
the game pinned to 6.1.12, tagged `v6.1.12` at `8dba0d1c`; the version stands at 6.2, tagged
`v6.2.0` at `1c3c38d5`. Item 6's three package bumps wait on the owner's word since October. Every
push and run before `c23cd2d4` since 2026-10-23 stands in REVIEW-ARCHIVE.md under Head records.

Next 240, the bands on thin shafts, first their crawl in a turn read by a frame against the frame
before carried to it, then one lever, the silhouette's edge and the shaft's level, 245, the far
cascade's move stepping the light outside the field, 239, the finest cascade's arrival, 244, the
settings in the units a person types, 238, the caches from e3d, and 235, the illumination as its own
project, last; 6 waits on the owner's word. The pack run started on `94097b62` at 22:41 publishes
the package the game pins. No budget by Decision 94; the door at 8:00 the one target by Decision 95;
a fault seen in motion is read in motion by Decision 107.

REVIEW-ARCHIVE.md beside this file holds, their numbers kept, the items done, the items closed or
parked, the verdicts settled, Decisions 1 to 93, the Replies before those here and the head records,
moved there on 2026-10-23 and again on 2026-11-09 so that this file holds the current work alone and
stays under 100 KB, read at each watch tick; a batch reads this file's head, its items and the
Replies (N 7.7), and the archive when an item, a verdict or a decision named here is wanted in full.

## Now

The owner asked on 2026-10-04 that the work here does not stop. A batch that ends is followed by the
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
143, 196, 198, 202, 216 and 231 recorded; 171 folded into 167 and 208 closed by Decision 94.

6. **Three packages bumped, after 5.2 is packed (Decision 20).** Vortice.Vulkan 3.2.1 to 3.3.0,
   AssimpNetter 6.0.4 to 6.0.5 and StbImageSharp 2.30.15 to 2.30.16, in one commit with the suite
   run and the notices written again, on the owner's word typed into this session once the package
   is out, as AGENTS.md has it for a version; SDL3-CS stays on its preview and NVorbis on 0.10.5.

235. **The global illumination as its own project.** From item 225's split on 2026-11-09. The plan
    names Engine.Rendering.GlobalIllumination as a project on Rendering, and at the split the
    illumination stays in Engine.Rendering, since the model pass, the renderer's registration, the
    fog, the lighting buffer, the glow lights, the ambient occlusion, the bloom, the HDR and target
    nodes, the render plugin and the scene field's commands bind its types. The cut series that
    frees it begins with the binding types, the illumination binding, the read side of the GPU
    illumination and the screen probes, the screen glow and the settings, moved down into Rendering;
    the illumination's nodes and renderers registered by a plugin of its own; the model pass reading
    the binding alone; then the project. After 226 and the live path, when the illumination's items
    rest; each cut its own commit with the suite, the public types' namespace Engine throughout.

238. **The caches listed and cleared from e3d.** From the owner's question of 2026-11-09, whether an
    old cache of shaders keeps the picture old. The shader cache is keyed by the source's hash, the
    entry point, the stage and the compiler's version, so a changed shader is compiled anew, and the
    driver's pipeline cache is validated by the driver; neither can serve an old shader, which the
    record says. What the owner lacks is a way to see and empty them, an e3d command, cache, that
    lists the engine's cache folders under the user's cache with their sizes, the shaders, the
    pipelines and the scene packs by version, and cache clear that deletes them, the next run
    compiling anew; with it, a read of the pipeline cache's growth, since the examples' blob stands
    at 471 MB on the owner's machine, whether a run merges the driver's cache back whole each time
    or trims it, and a bound where it grows without one. Small, after 237. From 237's cold run on
    2026-11-09, the blob is written at the device's dispose alone, so a run that ends otherwise
    leaves its pipelines unwritten and the next run makes them again in seconds mid-frame, as the
    owner's two did; the read takes in when the blob is written, after a frame that made pipelines
    or at intervals as well as at dispose, and how a blob reaches 471 MB, whether the driver's cache
    is merged back whole each run.

239. **The finest cascade's arrival after a cut.** From item 237's measure on 2026-11-09. Seven
    frames after a cut the finest cascade arrives at its new place as its build ends, and the frame
    drops, 32 percent of its pixels moving, since the arriving probes have no light from before to
    ease from and the screen probes follow at once, their kept light clamped to the frame's spread,
    which is also what takes the indirect light away within two frames of a light that went out and
    stays. The first lever is the arriving cascade starting from the light the cascade above shows
    for the same places, which the eased merge holds already, so that the arrival is a refinement
    and not a step; unclamping the kept light for the frames after an arrival is the second, which
    fades leaked light rather than removing it and risks ghosting in a turn that moves cascades, so
    it is taken only where the first does not hold the step under the test's bound. Read on the same
    script as 237's, the walk-and-turn test holding its settling within five frames. After 240.

240. **Bands on thin shafts from the screen probes.** From the owner's recording of 2026-11-09 at
    18:04 and item 237's read of it. On the atrium's octagonal shafts the screen probes' 8-pixel
    tile columns lie across the faces, with dark single probes on the faces and the wall behind, and
    in a turn the bands move in tile steps with the picture and not with the stone; they stand in a
    still view too. The read by normal and plane is in place already, screenProbesHere weighing each
    of a pixel's four probes by its nearness, the eighth power of the normals' agreement and the
    probe's distance against the pixel's plane extended to it, so the cause lies in how that read
    acts where a face narrower than a tile has no probe at all. A probe stands at its tile's middle,
    fixed in the window, so a face none of the middle columns falls on has no probe at any height
    and no wider ring finds one; its pixels take the neighbor face's probe at 45 degrees, a normal
    weight of 0.06, or the wall's behind, the sliver of weight normalized to full light, and under a
    thousandth the pass switches whole to the world's probes. In a turn the middle columns sweep
    across the faces, the face holding probes changes in tile steps and the kept light starts anew,
    and the 5 by 5 filter finds a thin face's like probes in one column alone, so the kept light's
    clamp to the frame's spread may bite as well. One bounded read first, by Decision 95, on the
    atrium's left shaft at noon, still and in a turn, the matched weight's map with the share of
    shaft pixels under a quarter and under a thousandth, the faces with no probe, the probes' own
    light on the faces against the reference at their points, and in the turn the share of shaft
    probes starting anew a frame. The first lever is in the read, the matched weight taken as a
    confidence, a ramp toward the world's probes in place of a sliver normalized to full and a hard
    threshold, which is anchored in the world and does not move with the grid, its cost recorded
    under Decision 94; the second is structural, the probe's point moved within its tile from frame
    to frame over a round, or the bounced light kept per depth texel as gi_screen_glow.slang keeps
    the glow lights', which a turn moves with the world and not with the window's grid, taken only
    where the first does not hold the bound. Read by the shaft at noon against the path-traced
    reference, the pair by N 4.9, and a test of a thin eight-sided column before a wall at a
    different light whose faces hold within a bound of the reference in a still and while the eye
    turns by a fraction of a tile a frame, the faces' light steady as the window's grid slides
    across (Decision 107). After 242, since the owner sees it. Read on 2026-11-09 at the atrium's
    left shaft at noon, the cause named above does not hold there. Every face of the octagon holds
    probes; the matched weight's median over the shaft's 13,000 pixels is 0.83, 6.4 percent lie
    under a quarter and none under a thousandth, the low weights the silhouette's edges and the
    joins between faces a few pixels wide. Held still against the reference the view reads 1.10, the
    shaft's faces 1.17 and 1.36, and its columns in rows 270 to 390 stand at 1.5 to 1.9 with bumps
    of 10 to 25 percent a tile apart, each probe's own estimate differing from its neighbor's on one
    face; the left silhouette's five pixels read 2.6 to 3.5, the low weight made whole with the
    light of the probes behind. In a turn of half a tile a frame 11 percent of the frame's probes
    and 13 to 16 of the shaft's start anew each frame, a probe that crosses onto another face
    failing the history's test of a like surface, normals 45 degrees apart at a dot of 0.71 against
    the 0.9 asked, and starting from one frame's estimate. So the bands on a face and their
    worsening in a turn are 246's matter and go with its mend, and this item keeps two things, the
    silhouette's edge by the first lever, the matched weight as a confidence easing toward the
    world's probes, and the shaft's level, 1.5 to 1.9 of the reference in those rows, placed by one
    read of a face probe's terms before it is mended or recorded. From 246's read, a settled probe's
    light is the mean of its sixteen places, so the bands held still are one probe's mean differing
    from its neighbor's a tile apart and time does not average them out. One lever is tried for them
    under Decision 95 after 246's two parts, the probe's point moved within its tile over the round
    so that a tile's light is its footprint's mean, or the filter gathering like probes along a
    face, whichever the shaft's columns read flatter against the reference; with it the silhouette's
    edge and the shaft's level as above. A frame against its pose held shows a lag or a level and is
    blind to these bands, which stand in the pose held as they stand in the turn; what the eye takes
    as noise is a pattern fixed in the window sliding over the stone. So the bands' measure in
    motion is the difference between a frame and the frame before carried to it by its depth and the
    two cameras, the strip a turn brings in and what a turn uncovers left out, read by sponza.turn
    on the atrium's left shaft and on the whole frame with the screen probes on and off, the share
    past 4 and past 12 levels and its map; that read comes first, and where it finds no more with
    the probes on than off, the item's bands are recorded and the fault waits on the owner's
    recording. Then the one lever, judged by that share in the turn and by the shaft's columns
    against the reference held still. The read in motion at 23:45 on 2026-11-09, by
    build/flicker.py, which carries each picture into the next by the turn's rotation, the same at
    any depth for a turn about the eye. The atrium at noon turned half a tile a frame over 40 frames
    with the exposure held reads 8.7 percent of the frame past 4 levels and 1.47 past 12 with the
    screen probes on and 5.2 and 1.05 with them off; the left shaft 4.1 percent past 4 on the mean
    and 16.7 at most with them on, 0.9 and 2.1 off, and the map shows vertical streaks fixed in the
    window where the stone slides past the tile columns. So the probes add a flicker of 4 to 12
    levels fixed to the tile grid, a face's light taken at the one point a tile's middle happens to
    lie on. The filter along a face, the second pass's blend at 7 by 7 with twice the falloff, left
    the shaft's columns as they were held still, 1.583 of the reference where 1.584 and 1.39 to 1.95
    from column to column, and read worse in the turn, 5.5 where 4.1, so it is reverted. The one
    lever is the probe's point moved within its tile over the round, so that a face takes the mean
    of the points a tile's footprint lays on it and the mean changes smoothly as the stone slides,
    behind a switch until it is judged. It is judged by the shaft's share in the turn against the
    0.9 and 2.1 with the probes off, and by the shaft's columns held still against the reference; it
    must hold a still view's frames within a level from one to the next, 237's walk-and-turn test
    and the rooms' readings, its cost recorded (Decision 94). Where a tile straddles two faces its
    point changes face from frame to frame, so the history found on a probe's own face among the
    twelve around, written and dropped under 246, may be needed with it. Where the lever fails
    these, it is reverted and the bands recorded as the screen probes' limit at shapes about a tile
    wide.

241. **Sponza's lamps light their surroundings, warm and varied.** Decision 106 on 2026-11-09. The
    lamps of the galleries and the vestibule, the pack's glowing bulb material turned on with L,
    bloom as if they lit the place and light nothing around them, so each lamp's emission goes into
    the scene as the glow lights carry an emitter's light, at a power where the gallery's vault
    above a lamp and the floor under it read lit in the evening views against the path-traced
    reference, the emission's scale the example's; the bulbs' one shared color becomes a color a
    lamp, warm as a flame's, the lamps varying in warmth around it with a few cooler ones, by a
    material a lamp node or an override a node, seeded so a run draws the same lamps; and the bloom
    is read on the lit evening views with the metered exposure, since a properly lit surround lowers
    the exposure and the halo with it, and its intensity and threshold are changed only where the
    glass's halo still reads as a blot rather than a soft halo once the surround is lit. Read by the
    vestibule at 20:00 and a gallery view with the lamps on, pairs with their difference panels by N
    4.9, and the owner shown. After 237's commit, before 240. The read on 2026-11-09 changes the
    item, since the power is not the cause but the glow lights' reach. A glow light reaches where a
    surface takes a three-hundredth of what it emits, so the reach scales with the emitter's size,
    and Sponza's bulbs, 0.07 to 0.09 units across, reach about 2.7 units and fade before that while
    the vestibule's lamp hangs 3.3 over the floor; against references at 16,384 paths the frame
    carries 0.03 of the reference's lamp light in the door and gallery views at 20:00, the bulbs'
    own pixels agreeing, so the emission is counted alike and goes nowhere. The mend, approved the
    same evening, is a floor on a light's reach, where its light falls to a three-hundredth of what
    it gives at half a probe spacing of the first cascade, which floors a small emitter's reach at
    8.7 units in Sponza and leaves a wider light its own; at an intensity of 300 the frame comes to
    0.95 of the reference in the door view and 1.31 in the gallery, nearer than a whole spacing's
    1.10 and 1.49. Its cost is the screen pass from 1.3 to 2.7 milliseconds at 800 by 450, every
    probe marching to all 23 lamps, and in a voxel game a floor of about 17 blocks for a torch; a
    cap on the lamps a probe marches to is item 242, right after. The look, the coder's
    recommendation to the owner and the review's choice until the owner says otherwise, is 2,400 to
    3,000 K with three lamps cooler at an intensity of 300, since under the meter the intensity
    matters little once the reach is right, the pools of light the owner liked at 3,000 under the
    old reach being the cut-off's, and a night view metered lower changing the frame little; the
    sheets are in the dump at 19:38 and 19:40. The temporary knobs in the tree come out before the
    commit. The owner's word the same evening, to the coder's session, was to take the reach fix
    with the bulbs at 300 and the 2,400 to 3,000 K colors, and make it cooler too, not only warm. So
    a third of the lamps, 7 of 23, lie between 4,000 and 5,500 K and the rest between 2,400 and
    3,000, seeded, the owner shown the pair at 19:44 in the dump with difference panels of the door
    and gallery at 20:00. The engine's reach is the greater of a light's own and the floor measured
    at half a probe spacing of the first cascade, said in the remarks and the illumination guide;
    the example gives a material a lamp at 300 with the knobs out; a test puts a bulb a tenth of a
    unit across four units over a floor and reads the point under it within 20 percent of the closed
    form, failing at nothing without the floor; RENDERING.md has the entry; the default capture,
    lamps off at noon, stands. The commit follows the suite. Done at `47a5972b`, pushed at 22:41 on
    2026-11-09.

242. **A probe marches to the brightest few lamps alone.** From item 241's read on 2026-11-09. With
    the reach floored at half a probe spacing, every screen probe marches to all of Sponza's 23
    lamps and the screen pass doubles, from 1.3 to 2.7 milliseconds at 800 by 450, and a voxel
    game's torches at a floor of about 17 blocks would cost the same way. A probe marches to the few
    lamps that give it the most light at its place, the rest left out or taken as their mean, with
    the count read against the reference in the door and gallery views at 20:00 and in the voxel
    game's torch-lit cave; where the frame holds within the bound of 241's reading, it stands, and
    the cost comes back toward the old. Right after 241, before 240. Beside the cap, an unbiased
    form, the fainter lamps marched in turns by tile and frame as the rays' places are, scaled so
    that a round's mean is every lamp's light, the kept light and the window's filter averaging the
    turns, read against the cap by the same views. And 241's floor follows the field's first cell,
    MeasuredAt times ProbeSpacing times the first cell over the root of Faint, half of eight cells
    by the cell over 17.3, so a game's Field Cell setting sets how far a torch lights, 8.7 units at
    a cell of 0.125 and 17 at 0.25, which the guide says beside the setting. Read on 2026-11-09, the
    cost lay at the hits and not at the probes' faint lamps, every ray's hit marching two lamps and
    two groups, so neither the cap nor the turns is kept and a hit marches its brightest and one
    group, the screen pass 2.09 ms where 2.64 with the door at 0.954 and the gallery at 1.316 of
    their references as before, and the gallery held still with the lamps on moving 0.043 percent of
    its pixels by 12 levels a frame where 0.044 with two marches. Done at `94097b62`, pushed at
    22:41 on 2026-11-09.

243. **Sponza shown at the top of the README.** The owner on 2026-11-09, asking that the Sponza
    scene be shown at the top of this README as the modeling is at the top of BevyCSharp's. The
    bridge's README has, inside its centered head under the badges, one animated picture,
    .github/assets/showcase_3.gif, and under it a line naming the program and the command that runs
    it; this README gets the same in the same place, an animated picture of scenes_sponza under
    .github/assets in the bridge's form, which is six still views two seconds each at 1,280 by 720
    in 2.4 MB, here six of the example's views at hours that show the engine, the atrium at noon,
    the door at 8:00, a gallery and the vestibule at 20:00 with the lamps on after item 241 among
    them, taken on the GPU at 1,280 by 720, with the line under it naming scenes_sponza, the command
    that runs it from the examples' project and the fetch that comes first; the README's words above
    it stand, and the picture is linked by the raw address as the examples' captures are. One commit
    after 241's, before 242; the picture's frames into the dump with a captions line by N 4.10. Done
    at `86339f72`, pushed at 22:41 on 2026-11-09.

244. **Post-processing settings in the units a person types.** Decision 108 on 2026-11-09.
    scenes_sponza sets its grade, its bloom and its metering through formulas, 2 to a power less 1,
    a decode from sRGB, 0.05 plus 0.9 times a share, 2 to the camera's EV less 14 and the log of a
    meter's constant, which a reader should not need to set a look. Three are constants and are
    written as their numbers, the bloom's intensity of 0.19 and threshold of 0.09 and the metering's
    compensation of -0.79, their derivation in the guide alone. Three are the surface's. The
    exposure that follows the scene takes its limits in EV at ISO 100 beside the multipliers, in a
    function of its own named as the session finds best, so the example says -1 and 14, the
    multipliers kept for a program that sets no camera exposure. The scatter runs from 0 to 1 over
    the range it spreads, a tight glow to a wide haze, and which bloom is drawn, the added one or
    the scattered, is chosen by an argument or a function of its own and no longer by a scatter of
    0, the getters following. SetLiftGammaGain gains a form of three numbers, how far the shadows,
    the middle and the highlights move brighter with no hue, beside the three vectors. The bloom's
    threshold stays in linear light, 1 for white, as the owner confirmed. One function a setting and
    one unit, a short form only where it is a part of the long one in the same unit; the surface
    changes outright, nothing kept for upgrading before a release, with the public listing, the
    cheat sheet, the guide's section and its worked numbers and every example that calls them, no
    capture changing since each new form sets what the old form's formula set. A test holds that,
    the EV limits against the multipliers at two camera exposures, the scatter at its ends and its
    middle, and the three numbers against the vectors. Three more places of the same kind, on the
    owner's second yes at about 22:11 on 2026-11-09, each a commit of its own after the first. The
    sRGB curve is written out by hand in scenes_sponza and twice in the voxel game, and a gamma of
    2.2 stands for it in five more examples, where the engine has a color from a temperature and
    nothing between linear light and sRGB; a pair of functions decodes an sRGB value to linear light
    and encodes it back, for a channel and for a color, named as the session finds best, and the
    examples that mean the curve call it, a port of a raylib program keeping what raylib's has.
    scenes_sponza works the sun's height out again from the day sky's own path, a cosine and a sine,
    to scale the sun's illuminance by it with a floor; the day sky takes one strength and scales it
    by the sun's height itself, as an option of SkyDay, so the example states a strength and an
    hour, its frames at 8:00, noon and 16:00 unchanged. The voxel game places the scene field half
    the finest cascade ahead of its eye by the engine's 64 cells times its own cell size; the engine
    gains the option, the field's center held that far ahead of a camera, so no program writes the
    engine's cell count out, the game taking it at its next pin (its item 167). A test for each, the
    pair against the curve's known values and round trips, the sun's light by hour against the
    example's formula, the center against the product. SHARED.md has the row. After 239, before 238.

245. **The far cascade's move steps the light of what stands outside the field.** From the voxel
    game's ask of 2026-11-09 in ASKS.md, read for its item 166. In the game's grove a walk moves the
    far cascade a cell and the near clouds, drawn at a height of 120 with no shadow and lit through
    the model pass, lose about 15 levels in that one frame; settled, the same patch reads 158, 178,
    205 and 144, 161, 184 at two poses 2.3 blocks apart and 164, 184, 210 and 169, 185, 209 with the
    far cascade off, so the light there depends on where the far cascade lies and not on the place
    alone. One bounded read first, by Decision 95, at the cloud patch's world point before and after
    the move, the terms the model pass adds and, of the far cascade, the probes it blends, their
    weights and the border band's share, to say whether the step is the band's blend toward the
    light past the field moving with the cascade, the probes' own light changing with their new
    places in open air, or the hold across the last cascade's band. The mend follows the read, the
    light at a world point continuous as the cascade moves a cell, by a band or a blend anchored in
    the world and not in the cascade's grid, or by the moved cascade's light eased from what it held
    there before, which 239's lever does not reach since no cascade lies above the far one. The test
    is a surface outside the field above open land with the far cascade on, its light steady within
    a bound across a move of a cell and the frame after it (Decision 107), and the game's walk
    recorded again at the next pin. After 240, before 239.

246. **A settled view's indirect light depends on how the eye came to it.** From the voxel game's
    second ask of 2026-11-09 in ASKS.md, read for its item 166 on 6.1.12. In the game's grove one
    place settled after a walk and settled again after a teleport differs on 0.35 percent of the
    window past 12 levels, and settled in another run on 0.69 percent and 0.25 past 25, in the
    screen probes' 8-pixel tiles, while a settled view's frames are the same to the bit, so each
    arrival holds one estimate of the light and keeps it. A turn made from a settled place matches
    its settled poses within 0.06 percent at 0.75 and 3 degrees a frame, so nothing lags, and what a
    walk shows is one held estimate after another. Noise standing in tiles that changes whenever the
    eye moves is the likeliest reading of the owner's words that the game's illumination has looked
    noisy the same way for weeks (Decision 107). One bounded read first, on the head with 237's
    mend, in scenes_sponza's atrium and in one open example, a view settled eight times by different
    ways, the spread of each pixel across the eight as a map with the share past 6, 12 and 25
    levels, the same with the screen probes off, and which state holds the estimate, the kept light
    and its weight once a view is still, the frame's clamp, the rays' places stopping or repeating,
    or the field's build order. The mend follows the read toward one settled light for a view
    whatever came before, the error averaging out over the frames a view is held, the rays' places
    going on while it is still and the kept light a mean over them whose weight falls as frames
    gather up to a bound, so the tiles' spread shrinks with every frame held and a walk shows less
    of it. The test settles one view three ways and holds the three within a bound of each other and
    of the path-traced reference, and holds the spread falling over the frames held; the cost is
    recorded (Decision 94). Read together with 240's, whose second lever it may be; after 242's
    commit. From 240's read the same day, the estimate held differs from one probe to the next on a
    face by 10 to 25 percent held still, and in a turn a probe that crosses onto another face of a
    thin shape starts anew from one frame's estimate, a sixth of a shaft's probes a frame. So the
    mend has two more parts for the read to weigh. A probe whose four around the place it came from
    straddle faces looks for its history on its own face further out before it starts anew. And a
    probe that starts anew begins from light that is steady, the world's probes' at its point or its
    like neighbors' in the frame, and moves to its own mean as its frames gather, the same
    confidence as 240's weight in a pixel, so that motion shows a softer light for some frames where
    it shows noise today. Read on the head at 23:11 on 2026-11-09, the path's difference does not
    show there. Sponza's atrium at noon settled eight ways, fresh and by a cut from each of seven
    views, with the exposure held, spreads no pixel past 6 levels with the screen probes on or off;
    four walks to one pose and a teleport differ from the cut's arrival by 1 to 6 levels on 1.4 to
    2.5 percent of the pixels and none past 6; the Ore example by day warped to from four places
    differs past 12 on none. Held still the rays' places go on through their sixteen and the screen
    filter's whole cycle stands for a probe's light, the mean of the sixteen gathered afresh each
    cycle, so a settled view forgets how the eye arrived. The game's 0.35 and 0.69 percent are
    6.1.12's, which lacks 214 to 237, or the grove's leaves that let light through, which no scene
    in the tree has; the game repeats its walked-to against teleported-to read at its pin of the
    package from `94097b62`, and where it still shows, a grove-like scene with such leaves goes into
    the tree for the read. The item keeps the restarts in motion, 11 percent of the frame's probes a
    frame in a turn of half a tile, in two parts, a probe finding its history on its own face
    further out where the four around straddle faces, and a probe that starts anew beginning from
    steady light and moving to its own mean as its frames gather. The test is a thin eight-sided
    column in such a turn, the share of its probes starting anew and its pixels' move from frame to
    frame against the settled poses, each under a bound, the game's sprint read beside it at the
    pin. The spread maps go into the dump with their captions by N 4.10. The two parts were written
    and measured at 23:38 on 2026-11-09 and move nothing. A column 0.6 across at 4 units before a
    lamp-lit wall, turned half a tile a frame, differs from its poses held on 0.00 to 0.01 percent
    past 12 before and after, the restarts lying on the silhouette and the joins and not showing;
    Sponza's atrium turned by the example's new sponza.turn differs from its poses held on 11.3
    percent past 4 levels before and 11.5 after, its screen probes' own light on 38.9 and 39.0 and
    on 6.0 and 5.9 past 12. What a turn does is a lag and a blur of about 4 levels, the kept light
    and the cycles resampled between four probes of the frame before each frame, the share of probes
    holding a whole cycle falling to 58 percent within 20 frames; keeping more of them whole, 94 to
    98 percent, lies further from the poses held, 44.9 where 38.9, an older mean carried along. So
    both parts are dropped, their patch in the dump with its caption, the lag recorded and not
    chased, and reading the history at a probe's own point or by depth texel stays a form for later.
    Closed as read, but for the game's read at its pin; the motion's fault is looked for under 240
    by a measure a held pose cannot stand in for.

## Verdicts

Verdicts 56 to 111 are settled, said under each in REVIEW-ARCHIVE.md; none is open.

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
2026-10-10 opens item 2 again, four things seen; 28 The owner's eye on 2026-10-11, the sky's light
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
The loop and the ECS are both the engine's way, each where it fits; 69 The window runs under X11 on
Linux for the present; 70 A session saves the pictures of its work in the repository, N 4.8; 71 The
devlog written as the work goes; 72 The screen probes have no public switch; 73 The devlog carries
notes beside the pictures; 74 The engine's work focuses fully on global illumination by radiance
cascades and the framework that supports it; 75 Global illumination comes before every other engine
item; 76 A reading's picture explains itself, and a plain capture or pair stays where it shows the
thing; 77 Every picture a session makes goes to the repository's dump folder; 78 Six ideas for
later, thought about and not scheduled; 79 Decision 75 means no drift, not that foundation work
waits; 80 The version's major and minor move by the reviewer's judgment; 81 The Sponza scene comes
to the engine as BevyCSharp has it; 82 What a scriptable render pipeline has, written down for
later; 83 The shadows' 34 ms in Sponza is a fault to read before the look; 84 The cascades' look is
to match the owner's Unity scene; 85 The sky's model is a module, the drawing stays in each engine;
86 Vulkan 1.4; 87 The look is read by its most visible error, not by matching the sections; 88 The
example's fog comes off until it is dark where it should be; 89 Stability in flight before the fog;
90 No one is named in a picture; 91 Anisotropic filtering at 16 everywhere; 92 The owner's play of
scenes_sponza on 2026-11-04; 93 Each README cross-references the other engine, each in full under
Decisions in REVIEW-ARCHIVE.md.

94. **No millisecond budget.** The owner wrote on 2026-11-08 at 08:50, "There is no ms limit budget.
   We first make it work and performant and focus on crunching the numbers after or along the way as
   much as possible." So the GI budgets of Decision 84 and item 208's 4 ms at High are no gate, and
   a mend that brings a view to its reference lands by its figures, its cost measured and recorded
   beside them and cut after or along the way where a cut holds the readings, the quality levels
   keeping their names and not their milliseconds. Item 208 closes; the parked forms of items 213
   and 212 are read again by their figures alone, item 217.

95. **A stop rule, one target and one lever.** The owner wrote on 2026-11-08 at 08:5x, asked why the
   light leaking had moved little in days, that the review should do what it thinks right and tell
   them for approval. The owner approved at 08:49 on 2026-11-08 ("Do it"), so three things hold for
   the GI work. A view inside its band lands and a figure that remains is recorded and not chased,
   unless it lies in a scene the owner looks at, Sponza's named views here and the meadow in the
   bridge; one bounded read places a cause, and a second is the last before a mend is tried or the
   figure recorded. The vestibule's door at 8:00 is the engine's one target scene until it is in its
   band, the rest of Sponza within its bands already. And one structural lever is read before more
   patches, the probes' texels at 16 by 16 in the near cascades, since the carried probes' sliver,
   the corridor's over-read, the side wall's echo and the door's pocket parts all trace to an 8 by 8
   texel, item 218.

96. **The owner's flicker recordings.** The owner put two F2 recordings and three screenshots of
   scenes_sponza's balcony view in the dump of 2026-11-08 at 11:08 to 11:09 and wrote that it
   flickers still, a basic flicker through the first 76 pictures, the hour switched forth and back
   from 77 to 85, and then a flicker right after movement or a light recalculation that settles
   after a time, more visible at steep angles and thin surfaces. Measured here picture to picture,
   the idle 76 pictures move 1.3% of the pixels by 12 levels or more at some time and 0.36 by 25,
   along the right wall's steep edge, the arches' edges and undersides, the columns and the far
   windows; after the hour's change 6.4 and 1.4% move over the next 24 pictures, settling in about a
   second and a half; the second recording's largest step moves 0.6% at the same places. So the
   flicker the owner sees is small in pixels and lies where the readings of 205 and 210 did not
   look, the steep-angle edges a held view's whole-frame figure averages out. Item 219, first in the
   order since it is in the scene the owner looks at.

97. **Sponza's lamps.** The owner wrote on 2026-11-08 at 11:17 that the lamps in scenes_sponza do
   not work properly, since the light housing should be transparent and let the light through and
   does not, that in the corner of the room there is a light spot that looks bad, and that the
   housing should be fixed so the light shines through it, the lights off by default with a key
   toggling them on and off, so that in the dark the corner's wooden door area reads pure dark in
   the tests. Item 220, first in the order as the owner's direct ask in the scene they look at.

98. **The night sky.** The owner wrote on 2026-11-08 at 11:20 that the night sky looks really bad,
   never updated from the voxel game's sky that was taken over, while the day with its sun disc
   looks super good, and put screenshots of the lamp and the night sky from a build in the dump
   (11-15-26 and 11-18-18, the balcony at 02:00). The night sky there is a field of large white
   blobs over the whole sky with a glowing white disc many times the moon's size. Item 221, after
   220.

99. **The difference beside a pair.** The owner wrote to the BevyCSharp session at about 13:50 on
    2026-11-08, shown item 116's meadow pair, that they saw no visible changes in the comparison, as
    in the times before, and asked that where there is a difference a third image to the right show
    it. From here a reading's pair in this engine too carries a third panel, the after's luminance
    less the before's at a gain the caption names, brighter in orange and darker in blue on black (N
    4.9, BevyCSharp Decision 82), from item 221's pairs on; a pair whose two frames differ to the
    eye says so in its caption and may leave it. The earlier readings stand as they are.

100. **Version tags.** The owner wrote at about 14:00 on 2026-11-08, having tagged every commit of
    the Unity Essentials repositories as `v2.0.30` and the like, that the three projects take tags
    too, and answered the review's questions at 14:1x. The form is `v6.0.91`, the letter v before
    `build/version.sh`'s number. Tags go on each major or minor change and on the commits packages
    were built from, since "doing every commit clutters it and is unnecessary as we want to focus
    the version tags to releases time apart with meaningful changes and not commits apart". The
    major and the minor are the sessions' to raise, "each session working on the project can decide
    when the time is right to rise that number in minor or major as each commit counts as patch",
    with "full liberty" when "a step is done and meaningful changes were done". N 6.3 holds the rule
    and the reviewer sets the tags as it pushes. `v6.0.0` marks the commit that set 6.0 and
    `v6.0.91` the commit `f14a9530` the package of 6.0.91 was published from on 2026-11-04, both set
    at 14:2x.

101. **No middle dot.** The owner wrote at 17:57 and 18:00 on 2026-11-08 that the middle dot is not
    used, the README's top links separated by a space alone and the dot nowhere else; the reviewer
    changed the README's first line in the tree on their word, for the next commit to carry, and
    every other middle dot in the repository is item 224's. The rule goes into STYLE.md's
    Punctuation as the shared text has it everywhere.

102. **One repository, many projects.** The owner wrote at about 18:20 on 2026-11-08, thinking with
    the review about the engines' future after Unity Essentials, where every feature is a repository
    of its own, that the engines feel "a bit cluttered" and should be "modulated extendable and
    stable and easy to use", that splitting everything into repositories "clutters" and makes
    commits "weird", that the shared modules' repositories are right, that the engine's own content
    could be modulated the same way inside its repository, and that a "one process approach", one
    running process edited and seen as it changes, is worth thinking about; at 18:30 they approved
    the review's recommendation. The direction is that each engine stays one repository and becomes
    one solution of many projects, each an area with its own public surface and its own plugin,
    packed as its own package at one version for all, as Bevy's crates are in Bevy's one repository;
    the dependency direction is held by project references and a norm test over them; the flat API
    stays unchanged on top and a game names the packages it needs; the names are dotted .NET names
    and not com.; there is no editor, and the live path is a watcher that recompiles a changed
    shader, reloads a changed asset or scene and recompiles a changed behavior into the running
    process, the engine's own code by restart. It is scheduled after the current items and done as
    one mechanical batch per engine with the public API held identical before and after and the
    documents' tables redrawn. Items 225 and 226, BevyCSharp Decision 85.

103. **Comments on the code's own terms.** The owner read a comment of scenes_sponza at about 19:00
    on 2026-11-08, "The frame graded as that scene's HDRP profile grades it, by the look of its AgX
    - Powerful.exr, a lookup build/unity-lut.py writes from that strip, a gamma of w 0.225, and a
    bloom of intensity 0.25, threshold 0.333 and scatter 0.7, worked out as HDRP works them out",
    and wrote that it "sounds just bad", reads as an em dash, and "is taking too much liberty of
    outside knowledge and in-process session knowledge", in scenes_sponza explicitly and probably
    everywhere else. The rule from it, in STYLE.md's Register and as N 4.11, is that a comment
    stands on the code's own terms. It says what the code does and why in the engine's words; a
    number taken from elsewhere stands with the reason it is right here, what it makes the frame or
    the program do, and the source is named once in the documents' references and not in the
    comment; the file names of outside assets, another product's profile or process as the
    explanation, "as X works it out", and the facts of the sessions' work are not written in a
    comment. Item 227, BevyCSharp Decision 86.

104. **The door lands at the price of a synthetic bound.** At 22:06 on 2026-11-08 the review told
    the session to land item 228's form, the pocket mark at 7/8 on 16 by 16 texels with the fully
    enclosed probes unmarked, which puts the door at 8:00 at 1.050 and the lion at 1.044 of the
    every-face reference, the door at noon at 1.013 and the lion at 0.985, the rooms back in their
    bounds and the corridor at 0.156 against 0.18, the first form to land the target scene of
    Decision 95; the price is the grazing floor's synthetic room at 0.063 against its bound of 0.06,
    0.003 past, where 16 by 16 alone reads 0.059, and the bound moves to 0.065 with its reason in
    the test, since the owner's words of the morning put the work first and the figures recorded.
    The NaN was the test's, a division by the ways that take the cascade above where a carried probe
    has none. The other forms, 3/4 with the lion at 1.112, 15/16 with the door at 1.174, and marks
    from the third cascade up with the door at 1.156, are recorded in the Replies.

105. **No readings or costs in comments.** The owner wrote at about 05:20 on 2026-11-09, reading
    scenes_sponza's comment on the far cascade of 100 that carried the lion's end wall's reading
    from 34.6 to 15.1 times its reference and the mend's 0.6 ms, "I also dont think we need to name
    the progress and ms count as these are just comments and not protocols". N 4.11 widens so that a
    comment carries no reading of before and after, no cost figure and no account of the trials,
    which are the review's records and RENDERING.md's; a number stands in a comment only where the
    code's choice depends on it, with its reason. Item 233.

106. **Sponza's lamps light their surroundings, warm and varied, and the bloom follows the lit
    surround.** The owner on 2026-11-09, having seen the lamps bloom without lighting anything,
    asked that the lights in Sponza contribute more light to the environment, that their colors be
    warm and vary in warmth with some cooler, and that the bloom, which looks too strong against an
    unlit surround, be judged once the environment is properly lit and the exposure has adjusted,
    when it would normalize. Item 241. Later that evening the owner chose the look with the coder's
    session, the reach fix with the bulbs at 300 and colors of 2,400 to 3,000 K, and cooler lamps as
    well, not warm alone, which became a third of the lamps between 4,000 and 5,500 K.

107. **A fault the owner sees in motion is read in motion.** The owner wrote at 17:25 on 2026-11-09
    that the voxel game's illumination has looked noisy the same way for weeks. The review's
    readings since 2026-10-24 were settled stills against references, which a fault in motion
    passes, and the owner's own recordings of Sponza on 2026-11-08 and 2026-11-09 placed 219's and
    237's faults within hours. So a fault named while moving is read from a recording, the owner's
    where there is one and a scripted walk and turn offscreen where not, picture to picture, the
    standing, the walking, the turning and the two seconds after a stop each given their share of
    pixels moved; a global illumination item's test holds a turn or a walk where its fault showed in
    one; and the review asks the owner for a recording when a fault is named without one. The
    review's rule, told to the owner for approval as Decision 95's precedent allows; items 240 and
    239 here and the game's 166 go by it. A frame set against its own pose held shows a lag or a
    level and is blind to a pattern fixed in the window that slides over the world, so the measure
    of motion is a frame against the frame before carried to it by its depth and the two cameras,
    learned on 2026-11-09 when 246's two parts moved nothing by the first measure.

108. **Post-processing settings in plain numbers.** The owner asked at 21:59 on 2026-11-09, reading
    scenes_sponza's lines for the lift, gamma and gain, the bloom, its scatter and the followed
    exposure, whether the public API could be set with real numbers in place of formulas with the
    extended forms kept, and whether that is good or bad. The review answered that it is good as a
    change of units and not as a second easy function beside each, since two functions for one
    setting are two truths with one getter, that three of the six formulas are constants to write as
    numbers, that the followed exposure's limits belong in EV, the scatter on 0 to 1 with the
    bloom's kind chosen apart, and lift, gamma and gain in three numbers beside the vectors, and
    that the threshold stays in linear light. The owner answered yes at about 22:03 and "Do it as
    you said and preferred". Item 244, after the illumination items by Decision 75. The owner
    answered yes again at about 22:11 to three more places of the same kind the review found, the
    color curves written by hand, the sun's strength by its height and the field's center ahead of
    the camera; a fourth, a function for easing toward a target, was named as the owner's call and
    is left out until asked.

## Replies

241 written on 2026-11-09, Sponza's lamps. The lamps lit nothing around them because a glow light's
reach lay where a surface takes a 300th of the light it emits, a few of a bulb's widths, short of
the floor under a lamp hung higher. A glow light now reaches at least as far as a surface takes a
300th of what one half the first cascade's probe spacing from it takes (GlowLights), which brings
the door and gallery views at 20:00 to 0.95 and 1.31 of a path-traced reference's light where 0.03.
The example gives each lamp's bulb a material of its own at 300 times the pack's emission, two
thirds of them warm between 2,400 and 3,000 K and a third cool between 4,000 and 5,500 K, seeded,
as the owner chose, and keeps the meter and the bloom, the halo soft once the walls are lit. A test
hangs a bulb a tenth of a unit across four units over a floor and reads the point under it within a
fifth of its lower face's closed form, which reads nothing without the floor. The example's first
view, lamps off at noon, draws as before, so its capture stands. The voxel desert's torches light
three times as much of the land, so far from the origin it reads 1,301 black pixels where 1,036
near it and 4,281 where 3,982 before, the excess the same, and its bound is 600 pixels more, a
quarter of a percent of the frame, which its share of the black allowed before (N 3.5). RENDERING.md
has the read. The suite 2,313 in order and shuffled.

243 written on 2026-11-09, Sponza at the top of the README. The README's centered head has under
its badges .github/assets/showcase.webp, six of scenes_sponza's views at 1,280 by 720 taken on the
GPU, two seconds each, the balcony at 7:00, the atrium and the look up at noon, the arcades at 16:00
and the gallery and the door at 20:00 with the lamps on, linked by the raw address, with the line
naming the example, the fetch and the command under it and the scene's attribution under that. The
door at 8:00 reads near black at that size, about a third of its reference's light, so the look up
at noon stands in its place. The example draws its text inside BeginUi, so F1 hides it for such
pictures, its attribution at the window's bottom at any size, and its capture and the gallery are
made again. WebP rather than a GIF, as the gallery is, since a GIF's 256 colors band the light. The
suite 2,313 in order and shuffled.

242 written on 2026-11-09, the cost of 241's floor. The time lay at the probes' rays' hits, not at a
probe's fainter lamps: a probe marching only those over a share of its glow light, or marching them
in turns by tile and frame, cost what marching every one costs and read the same against fresh
references, while every hit marched its two brightest lamps and two groups of the rest once every
lamp reached it. A hit now marches its brightest lamp and the rest as one group (probelight.slang),
which under the GPU's lock takes the screen's probes in Sponza's gallery at 20:00 to 2.09 ms where
2.64 and the first three traces to 2.32 where 2.79, and Ore's screen's probes at night to 3.50 where
4.27, with the door, the gallery and Ore at night reading their references as before, and the
gallery held still moving 0.043% of its pixels by 12 levels a frame where 0.044%. The cap and
the turns are not kept. Wick reads its light at Low offscreen with four wicks, so no torch-lit cave
is read here. The guide says beside SetSceneField that the field's cell sets how far a small light
reaches. RENDERING.md has the read. The suite 2,313 in order and shuffled.

246 written on 2026-11-10, read and closed but for the game's read at its pin. Settled eight ways in
one session, a cut from each view, four walks stepped a frame at a time and a teleport from 60 units
off, Sponza's atrium at noon with the exposure held differs by 6 levels at most at any pixel, the
walks by 1 to 6 on 1.4 to 2.5% of the pixels, and Ore by day warped to from four places by nothing
past 12, so on the head a settled view holds no estimate of how the eye came to it. Held still, the
rays' places go on through their sixteen and each probe's whole cycle stands in, the mean of them,
the same each cycle, which makes the frames the same to the bit and forgets the arrival. The two
motion parts, the history read from the twelve around and a probe starting anew from its like
neighbors' blend, moved nothing, Sponza's turn against each pose held reading 11.3% of the pixels
past 4 levels before and 11.5% with them and the screen probes' light 38.9% and 39.0%, and the
whole cycles held through a turn kept the stand-in at 94 to 98% where it fell to 58% but read 13.6%
and 44.9%, so all three are dropped, their patch in the dump. The lag of about 4 levels a turn
shows against a pose held is recorded. sponza.turn turns the example's camera a set angle a frame
for a count of frames, a pose a frame, and build/flicker.py --turn carries each picture of such a
turn into the next by the turn's rotation, the strip it brings in left out, with a mask carried
along, which item 240's read uses. The suite 2,313 in order and shuffled.
