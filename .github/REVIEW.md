# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `d885c89e`, and the run of `acb56863` read first, as the owner has it. The run: Linux
green, 1,584 passed and 14 skipped; macOS red by one test and whole, no crash in it or in the three
runs before, so Verdict 43 is settled and goes; the Cornell reference frame on macOS 7.6% of its
pixels off against 5%, the most in rows 40 to 79 and columns 192 to 223 and in rows 100 to 119 and
columns 64 to 95, Verdict 44 rewritten on it, the judge having spoken past a point; Windows and the
macOS examples job still running when read. The page's repeated lines carry the leak test's progress
line 198 times on both systems, `app 2 of at most 100` every time, so the line does not say the app
it is at, in item 1. Then subsurface scattering's second batch, the light that comes through: the
scatter pass draws a third image, each light behind a marked surface reaching its far side along the
normal turned away, times e to the minus the thickness over each color's share of the radius, times
the surface's color, nothing past three of the widest share; the spread across takes it with the
diffuse light and the spread down still takes away the diffuse light alone, so the frame gains it
spread and an unmarked pixel stays; the thickness is marched through the field from a quarter cell
under the surface until the march leaves the mesh where the finest cascade holds the point, and past
it the sun's shadow map gives it as the depth from the face it holds nearest the sun over the map's
depth a unit along the sun; the model pass binds the built field where light does not bounce, the
probes' images left empty, so a lamp has a field to measure through. A new test lights three slabs
from behind, by a lamp through the field and by the sun through its map, the thin scattering one
reading its light through and the thick one and the unmarked one the ambient alone; Manor with all
172 materials scattering over 5 cm, 1.294 ms where 1.185 without, medians of seven. Right, the
measure of thickness two ways each said with its reach, and the two things left for the third batch,
a material's own thickness from `KHR_materials_volume` for a sheet the field cannot measure and a
lamp's light through where there is no field from its shadow map, belong there as the reply has
them. The third batch's tiers: a setting of their own, `SetSubsurfaceQuality` with Low, Medium and
High and High the default as today, since tying them to the bounce's quality couples two unrelated
things, each tier timed on Manor and on a new `shaders_subsurface` example of wax spheres and
backlit slabs with its capture and README row, and Low's half-size spread laid onto the frame only
where the pixel's depth matches, so it does not bleed across an edge; item 3 says so. The suite:
1,627 passed; on lavapipe 348 passed and 7 skipped with no validation error.

Before it, D came, and item 2 came to be done. `BounceRoomsTests` draws the bounce rooms' eight
views at 160 by 90 at each quality and holds each to a reference path traced in the test through the
GPU's rays with 4096 paths a pixel, the two averaged to 80 by 45 in linear light and their pixels'
differences summed over the reference's light, the bound 15% over what each read here; the
references are PFM files of 43 KB, since a PNG of one is its light clamped where the frame's is
tonemapped, written again by `E3D_WRITE_REFERENCES=1` and byte-identical when they were; the test is
skipped with its reason where the GPU traces no rays, a theory's attribute added for it. The
readings repeat to the thousandth, 0.02 of the light among the red walls to 0.5 in the strip's room.
The guide's table is measured again, 0.37, 0.49 and 0.69 ms and 1.45, 3.87 and 8.06 MB in the
Cornell box at 800 by 450, where item 2 began at 0.27, 0.31 and 0.44 ms and 0.73, 2.80 and 6.81 MB,
the field's line gaining the lend's 6 MB; `gi.state` counts each probe's state and its rays' sums,
which it had missed; TODO.md's entry says what is left with its numbers, the corridor's limit, the
strip's room a quarter under, the Cornell box 9% over and some ten frames for new light's bounces to
build; and the trace's reach binding is gone, the faces standing in for the unread argument with a
comment saying so. Right, the test the plan asked for on the measure the work found, and the numbers
honest about the price, half again the bounce's time for the eleven fixes. Item 2 is closed in the
Now list to what it was and where it is written, and the look is the owner's to judge (Decision 22).
The suite: 1,625 passed; on lavapipe 346 passed and 7 skipped with no validation error. Next as the
Now list has it, subsurface scattering's second and third batches (Decision 18), then the animated
model's meshes (Decision 21); 5.2's pack (Decision 19) is the owner's push and green runs, and this
file says so to the owner.

Before it, the reading asked at `ebdd4fcb` came, the trace's hits through the reach against their
marches, written in §4 and on `shadeProbeHit`: the reach saves 0.027, 0.036 and 0.050 ms, and most
rooms read within a point or two, but the thin room goes from +9 to +15% and its pixels from 8 to
14, the corridor from −21 to −11% on light the twentieth of a weight lends it from the probes in its
walls, and the closed room with a lamp under its floor takes 42.9 levels of it where the marches
hold it to 2.5 under a bound of 8, so the marches stay. Right, the cheaper measured and refused for
a reason the test holds. One line for the commit that next touches the trace: it binds the reach at
9 and, with the marches kept, reads it nowhere, so the binding goes. D next, as the Now list has it:
the tiers measured again, the guide's table written again with the lend's 6 MB in its memory line,
§4 and TODO.md's entry, and the test holding the Cornell box and the level's views at each tier to
checked-in references on the pixels' mean difference, skipped with its reason where there are no ray
queries.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do. This
list is long on purpose, and a batch that ends is followed by the next item with no wait for a
reply. In this order, which the owner set on 2026-10-09: the runs first, then the bounce's quality,
then subsurface scattering and what follows it, and item 7 for a wait.

1. **What the next page says.** The run of `acb56863`, the first since `52ffd114`, is green on
   Linux, 1,584 passed and 14 skipped; red on macOS by the Cornell reference frame alone, 7.6% of
   its pixels against 5%, and whole with no crash, so Verdict 43 is settled; and red on Windows by
   two, the same frame at 5.6% and the hundred-app leak test's handle hold at app 55 (Verdicts 44
   and 40). The run of `d885c89e` reads the same so far, Linux green and macOS whole with the one
   frame, its Windows job to be read, and the macOS examples job of each has run past two and a half
   hours, which its end or its timeout tells. The page's repeated lines on Linux and macOS carry the
   leak test's progress line 198 times as `app 2 of at most 100`, so the line names no app but the
   second; it says the app it is at, with the next commit that touches the test. The examples job
   waits on Windows and macOS both green in one run. When every job is green the owner is told,
   since 5.2 is due (Decision 19). With the batch that next touches `build/test.py`, it takes from
   BevyCSharp's `1f68fde8` the two cases of a theory whose names are cut to the same as one counted
   apart, which its page reads as one today (SHARED.md). The engine's own despawn of what a state
   scopes is read against a soak of the world's entity indices across many transitions, which in
   BevyCSharp found Bevy 0.20.0 losing every index it despawned that way (SHARED.md), with the batch
   that next touches states. Each push's run is read by the reviewing session, and a failure it
   names comes first here.

2. **The bounce's quality (Decision 22) is done at `acb56863`.** The instruments A1 to A3, the
   measurement B, C's eleven fixes and D are in, each measured in RENDERING.md §4 with its error
   before and after and its cost, the guide's table measured again, and TODO.md's entry saying what
   is left with its numbers: a corridor narrower than the second cascade's probe spacing carries no
   light along it from past the first cascade's reach, 21% under, the method's limit; the small
   bright strip's room reads a quarter under; the Cornell box 9% over; and light that newly comes
   takes some ten frames to build its bounces. `BounceRoomsTests` holds the eight views at each
   quality to path-traced references by their pixels' mean difference. The look is the owner's to
   judge, on the Cornell box and on Wick, and a judgment that finds an artifact opens a new item
   with its picture.

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
   its shadow map where there is no field.

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

40. **The Windows job of `acb56863` fails the hundred-app leak test's handle hold at app 55, 2,638
    handles against 2,595 allowed, where 20 apps left 2,395, after the jobs of `a9380d7b`,
    `0499c115` and `46732863` failed it at apps 42, 27 and 27.** Read from the page, the finalizers
    awaited before `ended` since `85be41a7`: the twenty-first app's step line reads +1 for its
    instance, +9 for its device, +328 for ImGui's making, +9 started, −2 drawn, −6 as the device
    goes, −3 closed and −334 ended, 2 kept; the handles after every ten apps 2,346, 2,395, 2,103,
    2,490 and 2,538, the threads flat at 23 to 25, and the GDI and USER objects 0 and 3 throughout.
    So the model holds and narrows: two kernel handles an app stay on Windows and nowhere else, not
    GDI or USER objects, in the 328 ImGui's making takes, and the count between tens swings by
    hundreds, so the bound is met by the leak and the swing together. Two things. The step line
    splits ImGui's making into its steps, the context, the fonts' atlas and its upload with its
    fence and staging buffer, the plugin's descriptor pool and pipeline, and the first frame's
    command buffers, each with its handles, so the next page names the step that keeps two; and once
    named, the object is closed where the app ends, or said to be the driver's with the evidence.
    The bound stays. Settled when a Windows run passes whole.

44. **The macOS job of `acb56863`, after the Windows jobs of `a3dd7a8d` and `52ffd114` and the macOS
    job of `52ffd114`, fails
    `ReferenceFrameTests.A_Cornell_Box_Lit_By_Light_That_Bounces_Matches_Its_Reference`, 7.6% of the
    pixels differing from `cornell_box.png` where 5% is allowed, after 5.1 to 6.0%.** The references
    are drawn on the RTX 4070 and hold on the container's lavapipe; on MoltenVK the difference is
    densest in rows 40 to 79 and columns 192 to 223 and in rows 100 to 119 and columns 64 to 95, and
    the same job on Windows at `acb56863` fails by 5.6% with its difference in rows 100 to 159 and
    columns 64 to 95 alone, so the two devices differ from the RTX 4070 in different places; the
    difference grew through item 2's fixes as the bounce came to carry more of the picture, and
    `d885c89e`'s macOS job reads 7.6% in the same rows. Past a point, a device's bounce differs from
    another's by more than this test allows, and the cause is to be read, not allowed. Three things.
    The test writes the frame and its difference under `TestResults`, which the failing job uploads,
    where it writes them under the build's folder today, which no artifact carries, so the owner can
    hand the pictures over. Its message gives the regions' means as `gi.compare` gives them, so the
    page itself says which surface differs and which way. And the cause is read from them: whether
    MoltenVK's field, marches or probes differ, the picture's settling after the frames the test
    waits, or the capture's tonemapping, with the measured spread between the three devices written
    beside whatever allowance follows, and none widened without it. Settled when a run passes it on
    Windows and macOS.

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

24. **Content streamed on the go stays in the ledger, and the browser waits for a game that asks.** The owner chose
    on 2026-10-09, after reading a browser port of a large game that downloads its world as it is
    played, that the idea is recorded in SHARED.md as the file layer that port has (packs on a
    static host, reads by byte range into a block cache, a recorded first-run set, prefetch by the
    game's own streaming), to consider until a game here ships a world too large to download first,
    over a streaming file layer and over an HTTP source alone, since Manor's cells from disk are all
    a game here needs; and that the browser waits for a game that asks, its shape recorded for that day:
    .NET's browser runtime, SDL3 built with Emscripten and linked into it, a WebGPU
    `IGraphicsDevice` beside the Vulkan one without ray queries, bindless or 64-bit atomics, Slang
    to WGSL, threads behind cross-origin isolation, and a one-week feasibility spike before any
    commitment.

25. **A run that shows no window makes no sound.** The owner ordered it on 2026-10-10, since a
    hidden or offscreen run, a test or a soak, played through the machine's speakers; its audio goes
    to SDL's dummy driver, sounds still run their course, and a config field turns real audio on for
    such a run.

## Replies


A fault in the field, found drawing subsurface's example: spheres were held as spiky blobs.
`GenMeshSphere`'s last row meets its pole at corners the rounding of sin(pi) leaves a hair apart,
so some triangles there have next to no area and a face turned any way, and at a distance the
triangles around shared, one said the cells outside lay behind it: 97 to 143 of the 1662 cells
within half a unit of a sphere were held inside beyond its −z pole, 0.86 to 0.90 off, at 16, 32 and
48 rings, where cylinders, with no poles, held right. The splat passes over a triangle whose area
is next to nothing beside its longest side, a face under a ten-thousandth of its longest side
squared; a new test holds a sphere at 16 and 48 rings to no cell on the wrong side and its
distances within 0.03, 0.014 at 16 rings. The sun's contact shadows through the field drew those
blobs onto every sphere and floor near one, which now shade smoothly. The suite 1,629 passed;
lavapipe 350 passed and 7 skipped with no validation error.
