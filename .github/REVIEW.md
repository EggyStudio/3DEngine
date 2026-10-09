# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `45a1250c`. Three commits. Item 2's A1 (`0a43cf1a`): `gi.reference <png> <samples>`
path traces the window's view through the ray scene High builds, the first hit lit as the model pass
lights one with its specular share, every hit after as the bounce lights one, shadows by rays toward
the lights, the light given off at every hit, Russian roulette from the third bounce and the sky
where a path meets nothing, writing the PNG, the light as a PFM and an image of regions named by
what each pixel's first ray met, a floor, a ceiling, a wall by its color, a block's side or top, and
`gi.compare <png>` giving each region's mean light per channel against the reference's with a
picture of the difference; two tests hold the reference where the light is known, a closed glowing
box at 2.01 within 2%, which is the light given off over one less what is reflected, and a lone slab
under a lamp reading in the frame as in the reference to a hundredth of a percent. Right, and those
two tests are what make the reference a measure and not another picture. Its first reading of the
Cornell box, 1,024 paths a pixel in a second on the RTX 4070, has the frame 33% under the reference
over every region, the walls 47 to 50% under, the small block's side by the green wall 82% under
with its green at 0.041 against 0.242, and the glowing panel's underside 4% under. A third missing
everywhere and half on the walls reads as a factor or a loss in one place before it reads as an
artifact, so B compares a probe's merged light against the reference's radiance at the probe as well
as pixels against pixels, which A3's texture views make possible, and places the loss in the trace,
the merge or the gather before anything is tuned; the block's side by the green wall is the owner's
dim distance, to be read apart. Verdict 42 is settled (`d92c64dc`), the random values' tests in the
`Engine3D` collection, where the flat API's tests run one at a time. Verdict 40's steps are in
(`45a1250c`): for the twenty-first app the handles are read where the log marks each step of its
life, through a hook the logger tells of every line, and the steps go into the handle check's
failure; here an offscreen app takes 18 for its instance, 17 for its device and 1 for ImGui and
gives all back, and audio is no step, since it starts only when a sound plays, so the next Windows
page names the step that keeps the five. The suite: 1,581 passed; on lavapipe 311 passed and 4
skipped. A2, the level, is next.

Before them, Decision 23 came to be carried out, prose alone, and an eighth place with the seven:
DESIGN.md's NLayer row gives its reason alone, that raylib reads MP3; TODO.md's cost entry keeps the
smaller package for its reason and its scenes entry leaves SHARED.md to consider with no one named;
the version's commit in `NormTests` is called what it is; `build/test.py` runs on a contributor's
machine; and the three issue READMEs say each text is ready to be filed, the third, under
`build/mesa/ray-query-fragment`, found only because the matcher reads each run of lines together,
where a name broken across two lines had passed a line-by-line check. `N_4_7` reads every Markdown
file but the sessions' five, REVIEW.md still sought in the pages a game's author reads, and the
comments of every C# and Slang file, script, workflow and manifest, a web address's slashes passed
over, and reports the line a name begins on; put back as it was, that README fails at line 4. Right,
and the line-run reading is the better of the two checks, which BevyCSharp's flattens whole and so
shares. `build/pack.sh:29` holds the words in code and stays. The suite: 1,579 passed. On to item
2's A1, the reference. The runs since the push of 22:00 were read after: `a9380d7b` is green on
Linux, macOS and the macOS captures and red on Windows alone, the leak test failing on its handles
at app 42 as the test was built to, which rewrote Verdict 40 and settled 41, and `d7e764cd`'s macOS
job failed the random seed's test by a race, Verdict 42; `52c74240` and `7b983bd8` are running.

Before it, subsurface scattering's first batch came to be read, the diffusion, committed as far as
it stands alone as Decision 22 asks. A `ModelMaterial` and a mesh entity's `Material` take
`SubsurfaceRadius` in world units and `SubsurfaceColor` as each channel's share of it, a draw's
profile keying its batch; the batches that scatter are drawn again by `subsurface.slang` into two
half-float images, their diffuse light, which `litLight` works out apart from the specular with
`lit` adding the two as before, and their profile, kept where the scene's depth shows the surface;
`subsurface_blur.slang` spreads that light across and down onto the decoded frame, seventeen taps
each way, each color by a Gaussian a third of its share of the radius wide, a tap unmarked or
farther from the eye than the radius left out, and adds the spread light less the pixel's own, so an
unmarked pixel is never touched, the particles drawn after in a pass that keeps the frame and its
depth. The test draws two white spheres lit from the side, the left one's material scattering red
farthest, and holds the terminator's softening, red traveling farthest, the lit side held and the
unmarked sphere the same to the bit; the references are unchanged. Manor's 139 materials scattering
over 5 cm take `hdr_scene` 0.95 to 1.02 ms where it takes 0.42 to 0.46, one of them 0.65 to 0.70,
which the third batch's tiers answer; the guide, the upgrading page, the comparison page, the
cheatsheet, RENDERING.md §5 and TODO.md say what the second and third batches bring. Right. The
suite: 1,579 passed; on lavapipe 311 passed and 2 skipped. Before item 2's A1 comes the owner's
order of 2026-10-09 in item 1, seven places and N 4.7's matcher in one commit of prose.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do. This
list is long on purpose, and a batch that ends is followed by the next item with no wait for a
reply. In this order, which the owner set on 2026-10-09: the runs first, then the bounce's quality,
then subsurface scattering and what follows it, and item 7 for a wait.

1. **What the next page says.** The run of `a9380d7b`, pushed at 22:00, is green on Linux, on macOS
   whole and on the macOS captures, and red on Windows alone, where the leak test fails on its
   handles at app 42 as it was built to (Verdict 40), so the examples job, which carries the guides'
   blocks and Verdicts 30 and 31, waits on Windows alone. `d7e764cd` before it lost macOS to the
   random seed's test by a race (Verdict 42). `52c74240` and `7b983bd8` are running. When every job
   is green the owner is told, since 5.2 is due (Decision 19). Each push's run is read by the
   reviewing session, and a failure it names comes first here.

2. **The bounce's quality (Decision 22), before subsurface scattering goes on.** The owner judges
   the light that bounces on the Cornell box and on Wick as not yet the best, and sees banding or
   blotches on floors and walls, light crossing edges and corners, a bounce too soft and wide with
   no detail, and one too dim or flat at a distance; the Cornell capture shows each, the floor's
   bands at a grazing angle, the bright band along the floor at the red wall's foot, the broad halo
   under the glowing panel, and the green wall's tint missing from the small cube's side. What each
   is must be measured and seen before it is changed, so the instruments come first, each a commit
   of its own.

   **A1, the reference.** A Slang path tracer over the ray scene at High, `gi_reference.slang`,
   shading a hit as the model pass shades one, the sun with its shadow, the point and spot lights,
   the emissive and the environment where a path escapes, bouncing until Russian roulette ends it
   and accumulating hundreds of samples a pixel into an HDR image offline, run by `./e3d command
   gi.reference <png> <samples>` on the RTX 4070, since CI has no ray queries; and `gi.compare
   <png>` printing the mean error per channel in linear light over named regions of the view, the
   floor, each wall, the ceiling and each box's sides for the Cornell box, and writing a difference
   image. The reference is the number every fix is measured by.

   **A2, the level.** An example `shaders_bounce_rooms` with the hard cases in one scene and a fixed
   camera for each: the Cornell box, a thin-walled room with a lamp outside it, a corridor lit from
   one end, a sunlit room with a window, colored walls at one, three and six units from a white
   block, a small bright strip, a floor seen at a grazing angle, and a lamp carried and a wall moved
   by a key, each view captured and its reference made. Wick and Manor stay as the games that show
   it.

   **A3, the debug window and views.** `DrawBounceWindow()` in the flat API, as `DrawProfileWindow`
   is, and `gi.show <view>` for `./e3d`: the screen probe tiles drawn over the picture with each
   probe's light; a cascade's world probes as gizmo spheres through the immediate pass, colored by
   their six faces; the cascade textures and the screen cascade's light, filtered light and history
   shown as images; the difference to the reference as a heat map; toggles for the history, the
   filter, the screen probes, one cascade alone and the merge; and `gi.state`'s numbers. With them
   the four artifacts are seen for what they are before anything is changed.

   **B, the measurement, one commit.** Every tier's error per region against the reference for the
   Cornell box and the level's views, the four artifacts named with their numbers and pictures, and
   the ms by tier, written into RENDERING.md §4 as the state before the fixes.

   **C, the fixes, chosen by the measured gain, each a commit with its numbers.** Candidates, taken
   or left by what B says: the gather's weights at edges, a probe across a corner or behind a plane
   weighed to nothing and a probe placed off an edge; the merge's bilinear fix, tracing from the
   probe toward each parent's interval rather than one line a parent, with the intervals overlapped
   and blended at a cascade's boundary against rings; probe visibility from the traced distances, as
   DDGI weighs them, in the world probes' blend against light through walls; more directions and
   rays where the error says, the first cascade at 64 directions and the screen probes at 32 rays at
   High, and tiles of 4 pixels at a tier above if it pays; the field's first cascade at 128 cells at
   High for the near interval; and the filter and history tuned by the spread, the stability test
   already there holding the crawl. What does not pay a measured share stays described with its
   number.

   **D, the end.** Tiers re-measured and the guide's table rewritten, RENDERING.md §4 and TODO.md's
   entry, and a test holding the Cornell box and the level's views at each tier to their references
   within a tolerance, the references checked in as small pictures made on the RTX 4070 and compared
   as the 29 scenes are, a share of pixels allowed to differ between devices, skipped with its
   reason where there are no ray queries.

3. **Subsurface scattering, the first of Decision 16's spectral experiments (Decision 18), in three
   batches, each measured, what of its first batch stands alone committed before item 2 begins.** A material gains what skin, wax, marble and a leaf have, a subsurface
   color and a radius in world units with a thickness scale for its thin parts, set in the flat API
   as the material's other fields are and read from a glTF file's `KHR_materials_volume` thickness
   where it has one. First, the diffusion: light that enters leaves nearby, so the lit light of the
   marked pixels is spread along a profile of the material's color and radius in a separable
   screen-space pass over the HDR frame, the specular kept out of it where the frame has it apart,
   masked so an unmarked pixel is never touched and the spread never crosses a depth edge, with a
   test of a lit sphere whose terminator softens and bleeds the color where the unmarked sphere
   beside it does not, read at pixels as the occlusion tests read theirs. Second, the light that
   comes through: a thin part lit from behind shows the light on its front, the thickness toward the
   light read from the scene's distance field, which has it for nothing where the field is fine, and
   from the sun's shadow depth where it is coarse, with a test of a thin sheet lit from behind
   brighter on its front where it is thin than where it is thick. Third, the tiers: sample counts
   and a half-size pass at Low and the full at High, as the bounce is tiered, the kernels in Slang
   so the bridge runs them in BevyCSharp once proven here, the GPU cost of each batch on Manor and
   on the test scene in RENDERING.md and the comparison page, the guide's section and TODO.md's
   entry. Each batch a commit of its own with its numbers, and what does not pay a measured share
   stays described.

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

40. **The Windows job of `a9380d7b` fails the hundred-app leak test on its handles at app 42, as the
    test was built to, 2454 where the twentieth app left 2242 and the allowance is 200, and the hang
    of `8ac5912a`, `12b1f0c3` and `d7e764cd` is gone with the budget.** Read from the page. The
    series is the measurement asked for: handles 2204, 2242, 2289 and 2341 after ten, twenty, thirty
    and forty apps, some five an app, while the threads hold at 24 and 23 and the Vulkan objects,
    the instance and the device among them, stay at none, so what is kept is a kernel object and no
    thread, Windows alone, where Linux and macOS hold at the second app's count. The reply named
    SDL's audio subsystem, started and quit by count with no final quit, as the first to read, and
    the ImGui context the second; with no Windows machine here, the page is the laboratory. One
    thing: for one app after the warm-up, the test prints the handle count after each step of the
    app's life, SDL's video and audio subsystems started, the instance made, the device made, the
    ImGui context made, the frame drawn, and each undone in the shutdown, so the next page says
    between which two steps the five stay, and the leak is then read in that step's code and mended.
    The hot reload test's wait in seconds is in and held. Settled when a Windows run passes whole.

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

## Replies

Item 2's A2, the level. `shaders_bounce_rooms` holds eight rooms forty units apart, so no room's
lamps or field reach another, each with a camera that 1 to 8 pick: the Cornell box, a closed room of
walls a tenth of a unit thick, under the field's cell, with a dim lamp inside and a bright one that
casts shadows outside its wall, a corridor sixteen units long lit by the sun through its open end,
a room the sun lights through a window four units wide, white blocks with a red wall on their side
one, three and six units off, the sun on each wall's face toward its block and none on the block's
face toward it, a strip 0.06 thick giving off thirty times white in a closed dark room, a room
twelve units across whose floor is seen from a third of a unit above it, and a room whose lamp L
carries between three places and whose inner wall M moves. `build/bounce-rooms.sh <folder>
[samples] [qualities]` captures each view, traces its reference at `High` and compares each
quality's frame with it. Its first run, 1,024 paths a pixel, under a second a view: over every
region the frame is 1% under on the outdoor blocks, which the sun lights, 21% in the corridor, 30%
to 33% in the Cornell box, 54% to 58% in the thin room, 60% in the carried lamp's room, 66% to 67%
in the twelve-unit room, whose floor the ceiling's hot spot above the lamp lights by its bounce in
the reference, 73% to 75% in the window's room, and 87% in the strip's room, whose walls read 0 in
the frame, the strip under the field's cell; the qualities differ by a few points at most. B
places the loss. The README's gallery and the guide have the level. The suite: 1,581 passed.
