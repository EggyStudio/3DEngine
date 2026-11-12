# Review

Reviewed up to `54792d7e`, 264's mend for a program's shader, world a property over the place and
the origin and the lighting's point-taking functions taking world points beside their Placed twins,
with `80fe61d2`, part 2, the origin at the eye, and `a0af50a3`, part 1's second commit, every place
a pass reads in a buffer taken against the origin, the three held until the game's play on the local
package 6.2.60-local.264.3 read the four breaks of 264.2 gone and pushed together at 22:38 on
2026-11-12, one run on the last, 43312321044; the suite passed both legs on the last, 2,369; they
keep the form and carry the review's files. Before them `3405714a`, 264's first part begun, the
render origin laid through the vertex stages of the model pass, the shadows and the particles and
held at zero, with a test at 160 by 90 that reads every pixel within a level against an origin moved
64, -32, 128, pushed at 18:12 on 2026-11-12 once the run before it had ended, green at 20:22 with
macOS in 51 minutes, the Linux legs in 65 and 83 and the Windows jobs in 129, 84 and 67; the suite
passed both legs, 2,358; it keeps the form and carries the review's files. Before it `baa21ff4`,
Verdict 119's mend, the Windows suite dealt to three jobs with the rest of the Rendering part in six
processes of about 25 minutes each on the slowest runner read, pushed at 16:01 on 2026-11-12 with
`63b47a49`, item 266, the comments of 239 files swept by N 4.11 as widened with no line but a
comment changed, and `8513f710`, 259's second commit, the surface's point read at the centroid with
its test, one run on the last, green at 18:07 with macOS in 49 minutes, the Linux legs in 51 and 64
and the Windows jobs in 126, 54 and 72, which settles Verdict 119, and the pack run with publish for
259, 43243318284, started on it; the suite passed both legs on the second and third, 2,357, the last
changing no code; they keep the form and carry the review's files. Before them `aa0f1be7`, items 259
and 260, the blobs reported in the distance, the vertex color and uv2 read at the centroid where a
pixel's middle outside a triangle seen nearly edge on gave the surface's color a half float's most
at 4 samples a pixel, with light.terms, a pixel's light read a term at a time, and a test of walls a
hair beside the eye, 31 white pixels before and none after, pushed at 11:37 on 2026-11-12 once the
run before it had ended, its run red at 14:09 in the second Windows half alone, a part of the suite
ending at its time limit, Verdict 119, macOS in 55 minutes, the Linux legs in 66 and 83 and the
first Windows half in 130 green; the suite passed both legs, 2,356; on its local package the game's
sweep reads bloom's most by the origin at 13 levels where 142 and no step with a blob where 7; it
keeps the form and carries the review's files. The pushes before these stand in REVIEW-ARCHIVE.md's
head records.

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
    is the head's own forms at the hits; no cut is picked, 254 and 255 coming first.

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
    2026-11-12. The float API stays. Inside, a place is taken against the eye before it becomes a
    float the GPU sees. The eye is taken from each instance's place on the CPU and the
    view-projection pushed with the eye at nothing, so a vertex is placed in numbers as small as the
    view; the shadows' slices are made from the eye and the camera's axes; the matrices from this
    frame's clip space to the frame before's are made of eye-relative parts and the eye's move, for
    the temporal antialiasing, the motion blur and the ambient occlusion; every point from the depth
    is an offset from the eye, as 257 made the screen probes' and the glow lights'; the ray from a
    screen point is made in view space. 258's ranked list is the map and each of its entries is
    closed here or said left. A call beside it, the world moved by a whole offset, which shifts what
    the engine keeps by place, the scene field's cascades and their still meshes, the probes, the
    histories and the glow lights' keys, so a game that moves its origin has nothing built again,
    the voxel game's ask of 2026-11-12, and a mesh set at a new matrix by such a move is no mover.
    Held by a scene of each kind drawn at the origin and 4,450, 71,000 and 1,140,000 units out
    reading alike within a margin said for each, and by the voxel game's built scene on the package.
    A plan first as a Reply, the parts and their order, since it touches every pass. After 263, 259
    and the publish. An ask of the game's of 2026-11-12 belongs here, read on 6.2.56. Ambient
    occlusion at 1 lights one built scene by where it stands, the scene handed to the engine within
    450 units of its zero at both places by the game's own origin, the west wall 101 by the origin
    against 118 at the far place and the north wall 166 against 173, where at 0 the same rows read
    within 0.6; the land about the scene differs between the places, said as a condition, and no
    cause is given. Read with the ambient occlusion's entry of 258's list. The plan, accepted at
    16:02 on 2026-11-12. One render origin a frame, the window camera's eye, every place taken
    against it on the CPU in doubles, the view-projection pushed times the origin's translation,
    each grid kept in world numbers handed as the origin's place within its own cell. A model
    shader's world stays the world's own point, decided the same day, the engine's passes reading
    the point against the origin. Eight parts, each a commit with its test. The origin laid through
    with nothing moved, reading bit for bit; the origin at the eye, the instances, the immediate
    vertices, the particles, the culling and the eye handed; the shadows; the frame before's
    lookups; the points from the depth; the rays from the screen on the CPU; MoveWorld, a call that
    shifts what the engine keeps by place so a mesh at its new matrix is the same still mesh; and
    the ambient occlusion by place, likely the field's grid's phase and the sibling's item 137. The
    review's additions, every drawing test timed on both devices that draw on their CPU, the second
    part's cost read on Ore and in the voxel game from a local package, MoveWorld's documents
    listing what the program moves and what the call moves, and the origin's convention named for
    265's kernels. Its first two parts, recorded at 19:51 on 2026-11-12. The origin laid through
    with nothing moved at `3405714a` and `a0af50a3`, every pixel within half a level against an
    origin moved 64, -32, 128, 30 to 249 levels apart where one buffer or push is left against the
    world. The origin at the eye at `80fe61d2`, rounded to whole steps of 16 so a far place stays
    the program's float and the ray scene's top level is built again only as the eye crosses a step;
    a room at 4,096, 71,680 and 1,140,736 units reads the same as at the origin at every pixel on
    the GPU and on the device that draws on its CPU, 175 and 180 levels apart with the eyes set
    aside, and Ore at High costs the same, 7.41 ms. Packed locally as 6.2.56-local.264.2 for the
    game's read with its own origin off. The game's read of the local package 6.2.56-local.264.2,
    recorded at 20:14, finds a fault in the second part before it is pushed. The sun casts no shadow
    where the eye stands near the zero the engine is handed. The game's play stops at its check of a
    pillar's shadow, the line across it a flat 151.1 where 6.2.60 steps 46 levels; its built scene's
    shaded floor reads 8 steps over 3 levels, the figure with the shadows off, wherever the scene
    stands 16 blocks from the origin in use, and 26 to 28 where it stands about 300 off; and at
    1,140,000 with the game's own origin off a large shadow lies across a lit face. The engine's
    room test reads the far rooms against the origin's and would not see a shadow missing at both.
    `a0af50a3` and `80fe61d2` are held, the coder to say which brings it, mend it there and add a
    test of a post's shadow with the eye at the origin, near it, 300 off and across a step of the
    origin, a new local package after. The game's whole read of 6.2.56-local.264.2, recorded at
    20:43, main's game on 6.2.60 and on the package with its own origin off, and on the package with
    it on, the built scene at 16, 4,432, 71,000, 284,688 and 1,140,000. Mended by the render origin,
    the package against 6.2.60 with the game's origin off. The torn faces at 1,140,000 are whole,
    the land's spikes into the sky at 284,688 and 1,140,000 and the seams across the water at 71,000
    are gone, the walls' mottle at 71,000 is gone, and ambient occlusion at 1 reads every row as at
    0 at every place where 6.2.60 steps 1.58 to 2.38 on the west wall from 71,000 out. Broken by it,
    each the same in a cold and a warm play. The pillar's sun shadow, a flat 151.1; a creeper's
    front bands, a step of 5.9 where flat; a zombie's face stepping 8.6 levels in light at the eye's
    height where 0.2; and the water far darker, mirrored 115, 91, 126 where 171, 149, 168. Apart
    from the sun's shadow, the package with the game's origin off against on at one place differs in
    0.4 to 0.7 percent of the scene's pixels by over 8 levels at 4,432, 9.6 at 71,000, 10.1 at
    284,688 and about 26 at 1,140,000, in the walls' and the base's indirect light, a pixel along
    every edge and the foliage's light, no cause read. The play takes 308 s cold and 213 warm
    against 296 to 298 and 208 to 215. All of it is read again on the package of the mend that gives
    a program's shader its world points back. The mend read at 22:06 on 2026-11-12, `54792d7e` on
    `80fe61d2`, held with the two before it. ModelVertexOutput.world is a property over the place
    and the push's origin that moves the place when set, the push read by both stages; lights'
    twenty point-taking functions take a world point and take the origin off by placeOf, the origin
    a field of the lighting buffer of its own, which grows to 7,248 bytes, and the engine's own
    lighting calls their twins ending in Placed. The game's four shaders read against it, they hand
    world points to lightReaching, arriving and mirrored, move world on a copy in bits.slang and
    mob.slang, read no place from the buffer and import none of the modules whose functions take
    places, so they stand unchanged. The test of a post's shadow read by a program's shader, seven
    cases at 160 by 90, takes about 80 s on lavapipe, and the Windows parts of the run on `3405714a`
    had 23 minutes or more under their 70 each, the longest 47 m 22 s. Two things asked. A line over
    the world-taking spotLightReaching and pointLightReaching saying the light's position beside the
    point is the buffer's own place, in part 3's commit. And of the game on the local package
    6.2.60-local.264.3, besides the four breaks and the indirect light's difference, the sun
    shadow's edge on terrain at 71,000, 284,688 and 1,140,000 blocks with the game's origin off,
    where a world point's float has a grid of 1/128, 1/32 and 1/8 of a unit that placeOf carries and
    a place does not; if the edge steps, the guide tells a program that goes far to hand input.place
    to the Placed twins, the surface unchanged. The game's read of 6.2.60-local.264.3, recorded at
    22:39 on 2026-11-12, the game of `f201940` with its own origin on and off. The four breaks of
    264.2 are gone, each as on 6.2.60, the pillar's shadow a step of 47.3 where 46.0, the creeper's
    front 0.1, the zombie's face 0.2, the water mirrored 171, 149, 168 and plain 163, 181, 211; the
    play takes 309 s cold and 205 s warm where 296 to 298 and 208 to 215. On that the three commits
    went up. One check of the play fails on 264.3 alone, a broken block's bits by a tunnel's wall
    reading 85, 79, 85, 78, 85 where 28, and a sunlit block's bits are drawn dark. The cause is read
    in the game's bits.slang, which carries a bit's middle as the instance's translation and sets
    world to it, the instance's rows being places since `a0af50a3`, so the bit is lit the origin
    away. Decided by the review, the rows stay places, the precision far out coming of them, the
    game adds the push's origin there at the pin of the package that carries 264, and the guide and
    RENDERING.md say the instance's rows are against the origin, in part 3's commit. A villager
    drawn dark in the game's sheet 23-40 is most likely another variant of its clothes, the spawn
    command choosing at random, checked by the game's coder with a fixed variant. With the sun's
    shadows off the game's origin off against on differs as on 264.2 to the hundredth, 0.42, 9.59,
    10.14 and 25.55 percent of pixels over 8 levels at 4,432, 71,000, 284,688 and 1,140,000 blocks.
    The roof's shadow edge on terrain with the game's origin off stands as with it on at 71,000 and
    284,688 and at 1,140,000 stands higher with finer, irregular stripes, the sheets 23-45 and
    23-50. The coder to say which part to come changes each of the last two, the game reading each
    again after it, and the guide's sentence on world points far out written then. Answered by the
    coder at 22:40. The indirect light's difference is read again after part 8, which takes two
    things the plan left in world floats, the field's and the probes' grids falling at another phase
    against the scene and the field's build, field_splat taking each mesh's rows and the cascade's
    corner in world floats and SceneFieldPlan.OriginAround snapping in floats, both handed in
    doubles there. The shadow's edge at 1,140,000 is read again after part 3, which changes the
    cascades' fit; what stands higher after it is the world point's own grid, and the guide's
    sentence goes into the next part's commit. No function for an instance's world point, the guide
    giving the sum. The game's trial at 22:53 on 264.3 in a work copy. With bits.slang carrying the
    instance's translation plus the push's origin every check of the play passes, the tunnel's bits
    28, 28, 28, 27, 27 and the sunlit bits as on 6.2.60, 1,319 pixels over 8 levels apart where
    20,524. The dark villager was another variant of its clothes; with one variant the mobs read
    alike on 6.2.60 and 264.3, so the game's mob.slang reads right.

265. **The lighting's kernels written once.** Decision 112 on 2026-11-12. The radiance cascades'
    kernels, the trace, the merge, the ambient, the screen probes, their filter and the glow lights,
    go into the RadianceCascades module as Slang written once, each engine giving its bindings, its
    view's block and its field's lookups through a small interface the module names. First a read
    with the sibling's coder, as a Reply in both files, of where the two copies differ today and
    why, what is one engine's own and stays, and what the interface is. Then the move, a kernel at a
    time, each engine's tests and reference rooms holding before and after, the two engines'
    pictures of one room compared. After 264.

267. **A pixel on the picture's edge takes its normal from the neighbor inside.** From the sibling's
    `18364420` on 2026-11-12. There a neighbor clamped onto the pixel itself gave a step of nothing,
    taken as the nearer, and the surface's facing and every weight resting on it turned with a
    float's rounding along the picture's edge rows. Here depthNormal in gi_screen.slang and
    gi_screen_glow.slang takes the nearer of the texels on either side, sampled with the depth
    clamped at the edge. One read, the edge rows and columns of a view of the bounce rooms with the
    camera moved by a few float steps, and the mend where they move, the neighbor past the edge
    taken as missing, with the sibling's test. Small, with Verdict 119's mend or after it.

## Verdicts

No verdict is open. Verdicts 56 to 119 are settled, said under each in REVIEW-ARCHIVE.md.

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
cross-references the other engine, each in full under Decisions in REVIEW-ARCHIVE.md.

94. **No millisecond budget.** Decided on 2026-11-08 at 08:50. There is no millisecond budget. A
   feature is first made to work and to perform well, and its numbers are worked on after or along
   the way as much as possible. So the GI budgets of Decision 84 and item 208's 4 ms at High are no
   gate, and a mend that brings a view to its reference lands by its figures, its cost measured and
   recorded beside them and cut after or along the way where a cut holds the readings, the quality
   levels keeping their names and not their milliseconds. Item 208 closes; the parked forms of items
   213 and 212 are read again by their figures alone, item 217.

95. **A stop rule, one target and one lever.** On 2026-11-08 at 08:5x, on the question why the light
   leaking had moved little in days, the direction was that the review does what it thinks right and
   reports it for approval. Approved at 08:49 on 2026-11-08, so three things hold for the GI work. A
   view inside its band lands and a figure that remains is recorded and not chased, unless it lies
   in a scene that is looked at, Sponza's named views here and the meadow in the bridge; one bounded
   read places a cause, and a second is the last before a mend is tried or the figure recorded. The
   vestibule's door at 8:00 is the engine's one target scene until it is in its band, the rest of
   Sponza within its bands already. And one structural lever is read before more patches, the
   probes' texels at 16 by 16 in the near cascades, since the carried probes' sliver, the corridor's
   over-read, the side wall's echo and the door's pocket parts all trace to an 8 by 8 texel, item
   218.

96. **The flicker recordings.** Two F2 recordings and three screenshots of scenes_sponza's balcony
   view came into the dump of 2026-11-08 at 11:08 to 11:09 with the report that it flickers still, a
   basic flicker through the first 76 pictures, the hour switched forth and back from 77 to 85, and
   then a flicker right after movement or a light recalculation that settles after a time, more
   visible at steep angles and thin surfaces. Measured here picture to picture, the idle 76 pictures
   move 1.3% of the pixels by 12 levels or more at some time and 0.36 by 25, along the right wall's
   steep edge, the arches' edges and undersides, the columns and the far windows; after the hour's
   change 6.4 and 1.4% move over the next 24 pictures, settling in about a second and a half; the
   second recording's largest step moves 0.6% at the same places. So the flicker reported is small
   in pixels and lies where the readings of 205 and 210 did not look, the steep-angle edges a held
   view's whole-frame figure averages out. Item 219, first in the order since it is in the scene
   that is looked at.

97. **Sponza's lamps.** Reported on 2026-11-08 at 11:17, the lamps in scenes_sponza do not work
   properly, since the light housing should be transparent and let the light through and does not,
   and in the corner of the room there is a light spot that looks bad. Asked with it, that the
   housing be fixed so the light shines through it, the lights off by default with a key toggling
   them on and off, so that in the dark the corner's wooden door area reads pure dark in the tests.
   Item 220, first in the order as a direct ask in the scene that is looked at.

98. **The night sky.** Reported on 2026-11-08 at 11:20, the night sky looks bad, never updated from
   the voxel game's sky that was taken over, while the day with its sun disc looks very good, with
   screenshots of the lamp and the night sky from a build put in the dump (11-15-26 and 11-18-18,
   the balcony at 02:00). The night sky there is a field of large white blobs over the whole sky
   with a glowing white disc many times the moon's size. Item 221, after 220.

99. **The difference beside a pair.** Reported to the BevyCSharp session at about 13:50 on
    2026-11-08, on item 116's meadow pair, no change was visible in the comparison, as in the times
    before, and the ask was that where there is a difference a third image to the right show it.
    From here a reading's pair in this engine too carries a third panel, the after's luminance less
    the before's at a gain the caption names, brighter in orange and darker in blue on black (N 4.9,
    BevyCSharp Decision 82), from item 221's pairs on; a pair whose two frames differ to the eye
    says so in its caption and may leave it. The earlier readings stand as they are.

100. **Version tags.** Decided at about 14:00 on 2026-11-08, after every commit of the Unity
    Essentials repositories was tagged as `v2.0.30` and the like, the three projects take tags too,
    with the review's questions answered at 14:1x. The form is `v6.0.91`, the letter v before
    `build/version.sh`'s number. Tags go on each major or minor change and on the commits packages
    were built from, since a tag on every commit clutters the list and is unnecessary, the tags
    marking releases some time apart with meaningful changes and not commits apart. The major and
    the minor are the sessions' to raise, each session working on the project deciding when the time
    is right to raise the minor or the major, every commit counting as a patch, with full liberty
    once a step is done and meaningful changes were made. N 6.3 holds the rule and the reviewer sets
    the tags as it pushes. `v6.0.0` marks the commit that set 6.0 and `v6.0.91` the commit
    `f14a9530` the package of 6.0.91 was published from on 2026-11-04, both set at 14:2x.

101. **No middle dot.** Decided at 17:57 and 18:00 on 2026-11-08, the middle dot is not used, the
    README's top links separated by a space alone and the dot nowhere else; the reviewer changed the
    README's first line in the tree on that decision, for the next commit to carry, and every other
    middle dot in the repository is item 224's. The rule goes into STYLE.md's Punctuation as the
    shared text has it everywhere.

102. **One repository, many projects.** The engines' future was thought through with the review at
    about 18:20 on 2026-11-08, after Unity Essentials, where every feature is a repository of its
    own. The points made were that the engines are somewhat cluttered and should be modular,
    extendable, stable and easy to use, that splitting everything into repositories adds clutter and
    makes commits awkward, that the shared modules' repositories are right, that the engine's own
    content could be made modular the same way inside its repository, and that one running process,
    edited and seen as it changes, is worth thinking about; the review's recommendation was approved
    at 18:30. The direction is that each engine stays one repository and becomes one solution of
    many projects, each an area with its own public surface and its own plugin, packed as its own
    package at one version for all, as Bevy's crates are in Bevy's one repository; the dependency
    direction is held by project references and a norm test over them; the flat API stays unchanged
    on top and a game names the packages it needs; the names are dotted .NET names and not com.;
    there is no editor, and the live path is a watcher that recompiles a changed shader, reloads a
    changed asset or scene and recompiles a changed behavior into the running process, the engine's
    own code by restart. It is scheduled after the current items and done as one mechanical batch
    per engine with the public API held identical before and after and the documents' tables
    redrawn. Items 225 and 226, BevyCSharp Decision 85.

103. **Comments on the code's own terms.** A comment of scenes_sponza was read at about 19:00 on
    2026-11-08, "The frame graded as that scene's HDRP profile grades it, by the look of its AgX -
    Powerful.exr, a lookup build/unity-lut.py writes from that strip, a gamma of w 0.225, and a
    bloom of intensity 0.25, threshold 0.333 and scatter 0.7, worked out as HDRP works them out",
    and found to read badly, to read as an em dash does, and to lean too far on outside knowledge
    and on the sessions' knowledge of work in progress, in scenes_sponza explicitly and probably
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
    the test, since the direction of that morning put the work first and the figures recorded. The
    NaN was the test's, a division by the ways that take the cascade above where a carried probe has
    none. The other forms, 3/4 with the lion at 1.112, 15/16 with the door at 1.174, and marks from
    the third cascade up with the door at 1.156, are recorded in the Replies.

105. **No readings or costs in comments.** Decided at about 05:20 on 2026-11-09, on scenes_sponza's
    comment on the far cascade of 100 that carried the lion's end wall's reading from 34.6 to 15.1
    times its reference and the mend's 0.6 ms, a comment need not name the progress or the
    millisecond count, a comment being no protocol. N 4.11 widens so that a comment carries no
    reading of before and after, no cost figure and no account of the trials, which are the review's
    records and RENDERING.md's; a number stands in a comment only where the code's choice depends on
    it, with its reason. Item 233.

106. **Sponza's lamps light their surroundings, warm and varied, and the bloom follows the lit
    surround.** Asked on 2026-11-09, the lamps having bloomed without lighting anything, that the
    lights in Sponza contribute more light to the environment, that their colors be warm and vary in
    warmth with some cooler, and that the bloom, which looks too strong against an unlit surround,
    be judged once the environment is properly lit and the exposure has adjusted, when it would
    normalize. Item 241. Later that evening the look was chosen with the coder's session, the reach
    fix with the bulbs at 300 and colors of 2,400 to 3,000 K, and cooler lamps as well, not warm
    alone, which became a third of the lamps between 4,000 and 5,500 K.

107. **A fault seen in motion is read in motion.** Reported at 17:25 on 2026-11-09, the voxel game's
    illumination has looked noisy the same way for weeks. The review's readings since 2026-10-24
    were settled stills against references, which a fault in motion passes, and the recordings of
    Sponza handed in on 2026-11-08 and 2026-11-09 placed 219's and 237's faults within hours. So a
    fault named while moving is read from a recording, one handed in where there is one and a
    scripted walk and turn offscreen where not, picture to picture, the standing, the walking, the
    turning and the two seconds after a stop each given their share of pixels moved; a global
    illumination item's test holds a turn or a walk where its fault showed in one; and the review
    asks for a recording when a fault is named without one. The review's rule, put up for approval
    as Decision 95's precedent allows; items 240 and 239 here and the game's 166 go by it. A frame
    set against its own pose held shows a lag or a level and is blind to a pattern fixed in the
    window that slides over the world, so the measure of motion is a frame against the frame before
    carried to it by its depth and the two cameras, learned on 2026-11-09 when 246's two parts moved
    nothing by the first measure. Approved on 2026-11-11, asked by the review with N 7.9, N 7.10 and
    N 7.11.

108. **Post-processing settings in plain numbers.** Asked at 21:59 on 2026-11-09, on scenes_sponza's
    lines for the lift, gamma and gain, the bloom, its scatter and the followed exposure, whether
    the public API could be set with real numbers in place of formulas with the extended forms kept,
    and whether that is good or bad. The review answered that it is good as a change of units and
    not as a second easy function beside each, since two functions for one setting are two truths
    with one getter, that three of the six formulas are constants to write as numbers, that the
    followed exposure's limits belong in EV, the scatter on 0 to 1 with the bloom's kind chosen
    apart, and lift, gamma and gain in three numbers beside the vectors, and that the threshold
    stays in linear light. The answer at about 22:03 was yes, to be done as the review described and
    preferred. Item 244, after the illumination items by Decision 75. The answer was yes again at
    about 22:11 to three more places of the same kind the review found, the color curves written by
    hand, the sun's strength by its height and the field's center ahead of the camera; a fourth, a
    function for easing toward a target, was named as the owner's call and is left out until asked.

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

Item 264, part 1, its first commit, the render origin laid through the vertex stages with nothing
moved. RenderOrigin is one a frame, set by the renderer as the frame begins and held at the world's
own origin; a test sets another through RenderOrigin.Override. The instance ring's translations are
taken against it in doubles once each instance's block is culled in world numbers, and every
view-projection pushed to the model, shadow, subsurface and particle stages is pushed times the
origin's translation, worked in doubles, with the origin beside it, 80 bytes where 64 (ViewPush).
The vertex stages hand a new varying, place, the point against the origin, and world as the origin
plus it, so a program's shader reads world as before; the way to the eye is taken from place, and
the slopes under an orthographic camera too. At the origin zero every value is the one before, an
instance less zero and a fourth row plus zero being themselves and world being place plus zero.
RenderOriginTests draws a scene at 160 by 90 against the world's origin and against 64, -32, 128,
a floor, a wall, a block, a ball whose light scatters under its surface, still lit particles, and a
sun, a lamp and a spot light casting shadows, and reads every pixel within a level; the largest
difference is 0.10 of a level on the RTX 4070 and 0.13 on lavapipe. With the shadow pass's push
left against the world's origin it reads 249 levels apart at the most and 294 pixels past 4, and
with the particle stage's middle left so, 114 and 79. It takes 48 s on lavapipe here held to four
cores with its pipelines made cold and 16 s with them kept; the Windows runner's time comes from
this push's run in the next Reply. Part 1 comes in two commits, this one and the next, which hands
every place a pass reads in a uniform against the origin, the lights, the shadows' matrices, the
probes, the field, the glow lights, the reflections' matrices and the ray scene, with the passes
reading place, and names the convention for 265 in its message; RenderOrigin's remarks hold it now.
The guide's lines on a program's own vertex stage, which hands place beside world, come with part 2,
where the origin first moves. The suite passed both legs, 2,358.

Item 264, part 1, its second commit, every place a pass reads in a buffer taken against the render
origin, held still at the world's own. Each buffer is worked out in world numbers as before and
taken against the origin in doubles where it is written. The lighting buffer at its upload
(LightingUboPrepare.Placed), its lamps' and probes' places, the shadows' matrices, the reflections'
four and the mirror's plane. The field's corners and eye, and the stamps' boxes, whose rows take a
place into each box. Global illumination's lamps, the corners its cascades lay at the frame before,
the sun's cascades and the glow lights' middles and box, once they are compared with the frame
before's in world numbers. The screen probes' view, the frame before's against this frame's origin.
The ray scene's copies, against whose origin the reference tracer takes its camera, lamps and probe.
The occlusion's and the fog's inverse and eye, the fog's height, and the field's and probes' views.
The model, subsurface and particle passes read place, and the points lights.slang and
subsurface.slang take are named so. Two things keep world numbers and meet places. The penumbra's
grain is turned by the place plus the origin, handed beside the shadow's softness, so it holds to
the surface. The particles, kept in world numbers on the GPU, are taken against the origin, handed
in the step's view, where they meet the field. The convention for 265, named in the commit, is that
a place a kernel reads is the world point less RenderOrigin, every point and matrix handed so, a
matrix to clip space as the origin's translation times the world's; a kernel may take its places as
small and finds a world point as the origin plus the place, the origin handed where it needs one.
RenderOriginTests runs at Low and at High. The High scene adds fog, the occlusion, the floor glossy
and mirrored in its plane, a reflection probe, a lantern carried as a glow light and the probes'
rays traced through the device's rays, and each pixel's light is bent as l / (1 + l) before its
level, so the floor's light past 1 is read. Every pixel reads within a level, the largest difference
0.21 at Low and 0.42 at High on the RTX 4070 and 0.39 and 0.37 on lavapipe; drawn twice at the
world's origin, the High scene read 0.05 apart on the RTX with its light clamped at 1. Left against
the world's origin, global illumination's lights read 30 and 69 levels apart at Low and High, the
lighting buffer 111 and 187, the fog 47 at High, and the ray scene's copies 3.8 at High with 901
pixels past a level. Particles thrown onto the floor were taken out of the test. A particle bounces
where the field reads within a hundredth of a unit of the surface, which the origin's rounding can
move by a frame, and they read 6.8 levels apart on a few pixels where they bounced; and a step left
without the origin misses the field and bounces off the depth, which holds in world numbers, so the
test would not have caught it. Its times on lavapipe here held to four cores are 41 s at Low and 31
s at High with the pipelines made cold and 17 and 22 s with them kept; the Windows runner's come
from the run after the push. The suite passed both legs, 2,359. 3405714a's test is in its run,
43266966531, still going, and its Windows time comes in the next Reply.

Item 264, part 2, the origin at the eye. The renderer stands the render origin at the window
camera's eye rounded to a whole count of 16 units along each axis (RenderOrigin.Near), where the
plan said the eye as given, for two reasons. A whole origin leaves a place far out exactly the float
the program gave, the difference of two floats of the same step, and the ray scene's top level,
built again whenever its copies' records change, is built as the eye crosses a step and not each
frame the camera moves, a build that waits on every pass before it. A place near the eye stays under
14 units, so the precision near it is a float's at that size. Each camera's eye is noted with its
view-projection where BeginMode3D or a camera entity makes it (ModelDrawList.NoteEye), with the
view-projection made with the eye at nothing, and every view of a noted camera is pushed as that
times the way from the origin to the eye, worked in doubles (RenderOrigin.Seen), since the
view-projection made in floats far out carries the rounding of its translation, which no origin
taken after it takes back. The rest of the part's list. The culling's boxes are found from the
instances against the origin and its planes from the pushed view, and the translucent order reads
the same. The immediate pass's 3D shapes of the engine's own shader, drawn through a noted camera,
are uploaded against the origin and pushed from the eye; a shader of the program's own keeps the
world numbers it was given, as a model shader's world does. The moving entities' rows now and the
frame before are taken against the origin and their two views pushed from their eyes, the frame
before's without its jitter. The particles' placing rows are handed against the origin, so a
particle in its emitter's space is placed in small numbers, and the eye they are sorted from in that
space is found with the emitter's matrix against the origin. The eye is handed where the frame noted
one in place of inverting a view-projection. The ECS chunks need nothing more, being copied into the
ring and rebased there with the rest. The sky's cube is an instance and follows. The guide's section
on moving vertices says how a stage of a program's own places a vertex and that world is the place
plus modelPush.origin. Left to the later parts as planned are the shadows' cascades fitted in world
floats (3), whose sun shadows are the one difference left in a trial room with them, the frame
before (4) and the points from the depth (5); a probe's faces and the mirror's view are pushed from
their float matrices without an eye, as is a matrix a program sets through rlgl. RenderOriginTests
adds a room drawn at 4,096, 71,680 and 1,140,736 units out with the origin at the eye, its floor,
wall and blocks, a mesh entity, a lamp, the sun, the immediate pass's cubes and line and particles
in their emitter's space on eighths of a unit, the lights without shadows and global illumination at
Low, and reads it against the room at the world's origin within a tenth of a level at every pixel.
It reads 0.000 at all three on the RTX 4070 and on lavapipe, every pixel the same, since every place
handed is the float the room at the origin hands. With each view pushed from its float matrix, the
eyes set aside, it reads 0.14, 175 and 180 levels apart. Its places are whole counts of 1,024 so the
field's cascades and the probes lie alike about it; at 4,450 and 1,112.5, off the field's snap, a
trial room's white block read about 4 levels apart on its face from the room at the origin, the
field's grid falling at another phase against the room, which is part 8's question and the sibling's
item 137. In a trial room with the sun's shadows at 320 by 180, the room at 1,140,736 read a mean of
7.3 levels apart with 4,205 pixels past 16 with the origin held at the world's own, and 0.028 with
30 past 16 with it at the eye, all 30 in the sun's shadows. Ore at High costs nothing measurable,
its frame 7.42 and 7.39 ms on a0af50a3's local package against 7.41 and 7.41 on this one's, run in
turn on the GPU held alone. The test's times on lavapipe here held to four cores are 26 s at 4,096
with the pipelines made cold, 12 and 13 s at the others. The suite passed both legs, 2,362. Packed
unpublished as 6.2.56-local.264.2 in build/package, from this commit, for the game's reading at
4,432, 71,000 and 1,140,000 with its own origin off; a0af50a3's as 6.2.56-local.264.1 in my work
folder for the cost alone. In 3405714a's run, 43266966531, the second and third Windows jobs were
green in 84 and 67 minutes, the Rendering part's six processes in 40, 26, 9, 9, 42 and 11, the first
and fifth the long ones; the digests name no RenderOriginTests among the slowest, and its own time
comes from the logs once the first job has ended.

Item 264, the sun's shadow the game lost on 6.2.56-local.264.2, found and mended in a commit after
80fe61d2. The game's own shaders hand the engine's lighting functions points in world numbers,
terrain.slang lightReaching at its pixel's middle made from input.world, mob.slang arriving and
lightReaching at input.world and terrain.slang mirrored at it, and move world on a copy of their
input before it is lit, bits.slang to an item's middle and mob.slang to a point an eighth of a block
out. Since a0af50a3 those functions took places and litLight and lit read place, matching the
lighting buffer handed against the origin. With the origin held at the world's own at a0af50a3 a
world point and a place were the same numbers, so the handed points read right and a world moved on
a copy was passed over, the item and the mob lit at their own faces; with it at the eye at 80fe61d2
the shadow was read the origin away from the point, 16 units near the engine's zero, where it fell
on nothing, and 300 units out, where it fell as a wrong patch. A floor drawn with a shader built as
the terrain's reads 153 levels where the post's shadow falls against 153 beside it at 0, 8 and 24
units and across a step of the origin, and 119 against 149 at 300, on 80fe61d2; on a0af50a3, run in
a clone of it, the handed points read the shadow at 74 against 153 and the moved world is passed
over. The engine's own shader read the shadow throughout, which the room tests drew with. The mend
keeps what a program's shader had. ModelVertexOutput.world is a property, the place plus
modelPush.origin, which set moves the place, so lit and litLight light the point a program moved it
to, and the push is read by both stages. lights.slang's twenty functions that take a point take one
in world numbers again and take the origin off first (placeOf, the origin a field of the lighting
buffer of its own), and the engine's own lighting calls their twins ending in Placed, which take
places. They are arriving, cascadeReached, lightReaching in its two forms, mirrored, groundReached,
spotLightReaching, pointLightReaching, fromLight, probeAt, boxProjected, screenHold, bounced,
bouncedAndSky, byThinWall, bouncedSeen, glowLights, skySeen, probesReflected and tracedReflection,
whose eye is a point too, each beside arrivingPlaced and the rest. A look for the same break
elsewhere finds the functions of scenefield, gi, glow, probelight, roughreflect, lightthrough and
cascades, about sixty, which take their data beside the point, the field's info, a glow light or
global illumination's lights, against the origin as the model pass binds it, and the lighting
buffer's places and matrices themselves, a light's place among them; a program's shader reaches
those by importing their modules or reading the buffer, no shader of a program here or of the game
calls or reads one, and twins of them would run through every kernel, so the guide says they take
places, input.place or placeOf(world). The fog's are its kernels' own. The guide and RENDERING.md
say what a program's shader hands each. RenderOriginTests adds a post and a block on a floor drawn
with a shader of the program's own, the eye at the world's origin, 8, 24 and 300 units off and
moving across a step, one shader handing the lighting input.world for the sun's shadow and one
lighting each point as the point a unit off, its world moved; the floor in the post's shadow reads
under three fifths of the floor beside it, and for the moved world the other way about. With the
mend it reads 74 against 153 at every place and across the step, and the moved world 74 and 153
swapped; on 80fe61d2 it fails as above, and its moved cases fail on a0af50a3. Its times on lavapipe
here held to four cores are 32 s for its first case with the pipelines made cold, 16 s for the first
with the moved world's shader made cold, and 6 to 7 s for each other; the Windows runner's come from
the run after the push. The lighting buffer grows by the origin's 16 bytes to 7,248, which its
packing test names. The suite passed both legs, 2,369. Packed unpublished as 6.2.60-local.264.3 in
build/package from this commit for the game's play.

Item 264, part 3, the shadows. The cascades are fitted in places, to each camera's view as it is
pushed from its eye against the frame's origin, so their corners, middle and radius are as precise
far out as at the world's origin, and the middle is snapped to texels that lie in world numbers, the
origin's place within a texel in the light's view, worked in doubles, added before the snap and
taken after (RenderOrigin.Phase). The spot and point lights are fitted from their places, and every
shadow's view and projection takes a place against the frame's origin as fitted
(FrameShadow.Origin), so the shadow pass pushes it and the lighting buffer and the fog write it as
it is, and global illumination reads the frame before's sun cascades moved from that frame's origin
to this one's (RenderOrigin.Moved). Fitted in world floats far out, a cascade's texel changed with
the camera's every step, 138, 200, 124 and 161 sixteen-thousandths of a unit at 1,140,736 over four
steps of three eighths where the camera at the origin has 161 at each; fitted in places it is 161 at
every step. ShadowFitTests adds a camera at 4,096 and at 1,140,736 stepping across the light by
three eighths of a unit and across a step of the origin, which reads each cascade's texel as the
camera's at the world's origin and a world point by the camera landing on the same place in a texel
at every step; fitted in world numbers it fails at both, and with the origin's place left out of the
snap the point jumps by 0.19 of a texel at the step. RenderOriginTests adds the room far out with
the sun and the lamp casting shadows, within 24 levels at every pixel and no more than 16 pixels
past 4, the room falling at another phase of the texels, which lie in world numbers and divide no
whole count of 1,024 units; it reads 7.7, 7.7 and 10.7 levels at the most on the RTX 4070 and 7.8,
7.8 and 11.3 on lavapipe at 4,096, 71,680 and 1,140,736, against 7.6, 17.2 and 87.5 with 363 pixels
past 4 at the last on part 2's fit.

What keeps a place from one frame to the next when the origin steps by 16, as the review asked. The
antialiasing's, the occlusion's and the motion blur's histories are reprojected from clip space to
clip space and need no origin. The screen probes' frame before, its view and eye, and the
reflections' history view are kept in world numbers and handed against this frame's origin. The
world's probes stand in the field's cascades, whose corners are handed each frame, and keep their
light by their index; the glow lights keep theirs by their world middles; the particles are kept in
world numbers and placed by rows handed each frame; and the sun's cascades of the frame before are
moved from that frame's origin. RenderOriginTests adds a camera at High crossing a step, the origin
going from 0 to 16 two frames before the last, under temporal antialiasing with the shadows, still
particles and global illumination keeping their frames, against the same frames with the origin held
at the world's; it reads within a level at all but a few pixels, 0.073 at the most on the RTX 4070
and 8 pixels past a level on lavapipe at the floor's edge by the frame's side, where the places
round against the other origin, 13.2 at the most, the same frames with the origin held in both
reading 0.000 there. With the screen probes' frame before handed against the world's origin it reads
3.7 with 556 pixels past a level, so it is held to 32 pixels past a level and none past 32. No test
before it crossed a step. Its times and the shadowed room's on lavapipe here held to four cores are
15 s and 12 to 13 s each. The suite passed both legs, 2,375.

Beside them, lights.slang says over spotLightReaching and pointLightReaching that their light's
position is the buffer's place beside a world point, and the guide and RENDERING.md say the
instance's rows are places, their translation a world point once modelPush.origin is added, which
the game's bits.slang reads, a surface the survey of 54792d7e passed over, having looked at the
functions and the lighting buffer and not at what a vertex stage is handed.

On the Windows times the review asks for, 3405714a's run kept none, its digests listing a process's
slowest tests only where it ran out of time and its logs naming no test's time. A line in test.py
keeping each process's slowest tests in its digest whatever the result would give them; it is the
review's to choose.
