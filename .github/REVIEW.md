# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `90681ba8`. The shader cache's two writers are mended: each writes its entry under a
name of its own, the path with the process and a GUID after it ending in `.partial`, moves it over
the path, keeps what is there when the move fails against another writer's entry, whose bytes are
the same, and removes its own file either way; two tests race it through a barrier helper that
reports every exception and never hangs, the writer from two threads a hundred times and the real
compile ten times against an empty cache, both failing on the old writer with the job's error
(`90681ba8`), which is Verdict 34's mend, proved at 17:03 and 17:23 by the Linux and macOS jobs of
its run, so the verdict is settled. The owner decided on 2026-10-07 that global illumination comes
in four phases (Decision 16), items 2 to 4, and the session began phase 1, the cascaded scene
distance field, at 17:00. The four runs were in progress at 17:00, the Windows jobs of `3afcc4d0`,
`0c19c335` and `6412daca` in the games' step since 15:31, 16:08 and 16:11, and `38f68412`'s run on
all three systems. The suite: 1,505 passed, none skipped.

Before them, Arabic's marks came to be put on their letters and its pairs kerned by the font's GPOS
table inside shaped runs: what GSUB and GPOS share moved into `GlyphLayout`, which
`GlyphSubstitution` and the new `GlyphPositioning` extend, single and pair adjustments, marks on a
base, on a ligature's component and on another mark, the contexts and the extension, with cursive
attachment and device tables left out; a shaped line's keys carry where each glyph is drawn from the
pen and how far it moves it (`PlacedKey`), which `DrawTextPro`, `ImageDrawTextEx` and
`MeasureTextEx` read; a letter's marks are shaped in HarfBuzz's order and the Arabic plan runs in
its stages with `liga`, `clig`, `rclt` and `mset`; the zero width non-joiner and the isolates join
nothing; and 328 shapings of 52 words on seven of the machine's fonts came out as HarfBuzz's glyphs
at its positions, held by `arabic-marks.ttf`, six tests and the `arabic_marks` frame (`38f68412`),
which settles item 4, the three batches of Decision 15 in. The suite: 1,503 passed, none skipped.

Before them, text read right to left came to be drawn and measured in the order it is read:
`TextDirection` is a reduced UAX #9 over grapheme clusters, each line a paragraph of its first
strong letter's direction, the weak types, the neutrals and the levels resolved by the rules' names,
trailing white space at the paragraph's level, the runs reversed from the highest level down,
mirrored brackets turned, embeddings and isolates passed over, and `TextKeys` orders a line only
where it holds a character read right to left, so Latin takes the path it took and measures as
raylib's (`0c19c335`). Arabic is joined: a run of Arabic clusters at one level is shaped in stored
order and then reordered, `ArabicJoining` holding Unicode 16's joining types, the forms and the
presentation forms with the eight lam-alef, `GlyphSubstitution` applying plans of features under a
script with a mask a position, lookup flags honored through GDEF's classes and filtering sets, a
font without the positional features drawn by the presentation forms it maps, with two test fonts
from `make-color-test-fonts.py`, a reference frame each, and 25 of the machine's Arabic fonts
joining through `e3d eval` (`6412daca`); batch three, marks and pairs through GPOS, is under way.
The runs: macOS passed the leak test at `3afcc4d0`, `0c19c335` and `6412daca`, which settles Verdict
32; the three Windows jobs were in the games' step at 16:40; Linux passed the first two and failed
one test at `6412daca`, a race of the shader cache under parallel tests, which is Verdict 34; the
examples job waits on Windows. The suite: 1,492 passed, none skipped.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdicts 30, 31 and 33 first, the five Windows jobs.** macOS passed every run and Verdict 32
   is settled; Linux and macOS passed `90681ba8`, so Verdict 34 is settled; the Windows jobs of
   `3afcc4d0`, `0c19c335`, `6412daca`, `38f68412` and `90681ba8` were all in the games' step at
   17:37, the oldest since 15:31 with 180 minutes, Verdict 33's proof; the examples job, which
   carries the guides' blocks and Verdicts 30 and 31, runs once a run's three test jobs pass, which
   `90681ba8`'s will if its Windows job does. Each push's run is read by the reviewing session, and
   a failure it names comes first here.
2. **Global illumination, phase 1: the scene distance field (Decision 16).** A cascaded signed
   distance field in clipmaps around the camera, built on the GPU from the static meshes' triangles
   and stamped with the moving bodies' shapes coarsely each frame, the cascade count, the cell size
   and the update budget in the config and each frame's cost read by the timestamps. It is used at
   once: ambient occlusion traced through it beside the screen-space pass, soft contact shadows of
   the directional light, and particles colliding with the world rather than with what the window
   shows, which closes that TODO entry. A reference frame a cascade, a test of the field's distances
   against the meshes it was built from, the costs on the comparison page, and the guide's section.
3. **Phase 2: Radiance Cascades, hybrid.** Cascades of probes aligned with the SDF clipmaps, each
   cascade's probes storing radiance over its interval of distance, the first cascade's intervals
   traced through the depth buffer and the ones above through the field, direct light and emissive
   surfaces injected at the hits, merged from the far cascade to the near one, and the result
   sampled a pixel as diffuse indirect light beside the environment map's. A quality tier in the
   settings from a few cascades at low angular resolution to the full set, the memory and the frame
   time of each measured and written, and the examples' Cornell box and a lit room of a game as the
   reference frames. Nothing is baked, and every light is dynamic. The kernels are written in Slang,
   which the bridge compiles too, so BevyCSharp runs them once they are proven here.
4. **Phase 3: specular.** Glossy reflections traced through the field with the cascades' radiance
   at the hit, screen-space reflections where the field is too coarse and the reflection probes as
   the fallback, chosen by roughness; and a hardware ray-query path through Vulkan's ray query
   extension for the field's misses where the GPU has it, behind the same quality tier, measured.
5. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the twelve has.
6. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.
7. **The scripts' shared watch for apps made one after another.** `039bd788` says the scripts'
   watch is one stream for the process, which holds for apps that overlap, while the watch is let go
   with the last app watching it, so a hundred apps made one after another make a hundred FSEvents
   streams on macOS, read on the way to Verdict 32. The watch is kept for the process once made, or
   the sentence says what holds, whichever the macOS leak test's census argues for.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 29, 32 and 34 are settled, and their numbers are not given again.

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

33. **The run of `913e78e0` fails on Windows in every game's opening, and the page says no more
    than `did not open`.** Read from the page: the suite passed there, 1,452 with 9 skipped,
    `build/play-game.sh` drew Pusher from the package under the validation layer, and then
    `build/drive-game.sh` failed for all eight games at `./e3d open`, each error `<game>: did not
    open.` and nothing after it, since `--quiet` keeps e3d's own refusal off the page and the
    script's last warnings came from a log that had none or is not where the script looks,
    `build/sessions/<game>.log`. e3d refuses an opening with a sentence, `could not be started` with
    the error, or `did not start serving within` its patience naming the log it wrote, and neither
    reached the page, so the cause of the first opening of the games through a session on Windows is
    unread. Two things. The script's error for `open` carries e3d's refusal, its code and its
    sentence, and the last lines of the log e3d names, read from that path, which N 6.7 asks of a
    failure; and the cause is found with the run of the mend, the eight failing alike pointing at
    what they share, the session a game serves and e3d waits for on Windows, and not at a game.
    Mended at `1c848a20`: the cause was e3d's, which on Windows added its `cmd.exe /c start` line to
    `ArgumentList` as one argument, so .NET escaped each inner quote with a backslash that cmd.exe
    keeps, `start` took the escaped title for the program and the log's path was none; the line goes
    to cmd.exe as written through `Arguments`, the script keeps e3d's answer as JSON and fails with
    its code, its sentence and the last five lines of the log it names, and a path that is not there
    is refused with `NOT_FOUND` where e3d died making the log's folder from it. Unproven until a
    Windows job runs. The run of `1c848a20` opened every game, and Windows ran out of its 75 minutes
    in the games' step with no error on the page, so each game has a budget, `DRIVE_MINUTES`, past
    which a watcher stops it and one error says how far it got by its last status, the games after
    it played on, the errors written to the script's own output so one said inside `$(ask ...)`
    reaches the page (`596535ce`); the twelve games timed on lavapipe gave each twelve minutes and
    the Windows job 180 (`3afcc4d0`), which the run of `3afcc4d0` tries with twelve games. The
    Windows jobs of `3afcc4d0`, `0c19c335` and `6412daca` were in the games' step at 16:40, each
    with 180 minutes. Settled when the Windows job plays the twelve games.

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

## Replies

**Now 2, global illumination's first phase, the scene's distance field (Decision 16).**
`SetSceneField(cascades, cellSize, updateBudget)` and `Config.SceneField` build a signed distance
field of the meshes drawn into the window that cast shadows, in cascades of 64 cells a side around
the window's eye, each twice as coarse and as wide as the one before, stacked along z in one 3D
image of half floats that a pass binds once with a uniform buffer of where each lies. A cascade's
corner moves on a grid of eight of its cells, and a cascade the eye has passed, or that a still mesh
came into or left, is built again, the budget's number a frame, finest first, keeping its old place
in the uniform buffer until then (`SceneFieldPlan`, unit-tested on the CPU). A mesh drawn the same
for eight frames is still, its triangles kept once in their mesh's own space in one buffer, and a
build is a workgroup a triangle taking each cell within four cells of it by an atomic minimum of
its distance in 1024ths of a cell above a bit for in front, then a pass turning the words into
distances (`field_splat.slang`, `field_resolve.slang`). A mesh that moves, a skinned one, and a
still one whose cascades are not yet rebuilt are stamped each frame as their box into the bricks
they come within the band of, last frame's bricks stamped again to put them back
(`field_stamp.slang`). Reading the field back on the way found three faults of sign, each mended
and each held by a test: of two meshes as near, as a crate on the ground, the one a cell is
behind wins, so the crate's inside is inside; and a triangle puts a cell behind it only within 60
degrees of straight back, so a cell in the plane of a turned wall's side above it, or above a
pillar's rim, is not put inside by a face the way runs along, and the ground plane's open edges put
only a narrow wedge below them inside. Three things read it. `ao.slang` adds an occlusion read
along the normal and four leaning ways beside the screen-space one, and traces the sun toward it
through the field for soft contact shadows, whose share rides in the occlusion image's green and
multiplies the shadowed directional light in the model pass, so the pass runs for the shadows alone
where the occlusion is off. `particle_step.slang` steps a colliding particle's move through the
field where it holds the particle and meets the depth elsewhere, which closes TODO.md's particle
entry's hole, a particle passing through what the window does not show. `field.show <cascade>` draws
a cascade over the window as the field holds the scene, `field.state` says where each lies, and
`field.rebuild <frames>` builds every frame so `profile` times a build. Tests hold the field's
distances read back against a cube's exact ones within the band and the band beyond it, no cell away
from every mesh near a surface in a courtyard of the shapes that broke it, a crate on the ground
inside, the plan's placement, settling, budget and bricks, and particles thrown at a wall behind the
camera coming back with the field and gone without it. Two reference frames, `scene_field_lit` and
`scene_field_cascade`, draw a corner lit through the field and its first cascade. `shaders_scene_field`
is a courtyard with a pushed crate and a fountain whose drops bounce off what the camera does not
see, captured for the README. On a laptop's RTX 4070, from `./e3d command profile` in that example, a
frame stamping its crate spends 0.014 ms of the GPU on the field, building the finest cascade every
frame 0.25 ms, and the occlusion pass 0.073 ms where it took 0.036 without, on the comparison page and
in the guide's new section, with RENDERING.md §4 and TODO.md's entries saying what is built and what
is left. The render tests, the field's among them, pass on lavapipe under the validation layer. The suite: 1,514 passed, none skipped.

Shared: the field's kernels and its sampling module are plain Slang over storage buffers, one 3D
image and a uniform buffer, with no type of this engine's, so BevyCSharp's bridge can compile them
as they are once its phase two reads the field.
