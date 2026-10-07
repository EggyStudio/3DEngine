# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `80227981`. Glossy surfaces reflect, the first part of phase three: a fragment under
a roughness of 0.5 traces its mirror ray in the model pass, where it has its own normal and
roughness, through the window's half-size depth of this frame in 12, 16 or 24 steps by the quality
and five halvings (`traceScreen` in `gi.slang`, which the screen probes share), a surface met on the
screen reflecting the frame before's picture through the frame before's camera, blurred down its
mips by the roughness and faded toward the field's shading at the picture's edge, the window drawn
through the HDR frame while light bounces and its scene copied at half size with mips after the
model pass (`RecordKeepFrame`); a ray that leaves the picture or meets nothing on it goes on through
the field, its hit shaded with the cascades' light; a miss, and a surface growing rough from 0.25 to
0.5, leaves the probe's or the environment's reflection, the ambient lights' specular fading the
same way; `shaders_reflections` with its 800 by 450 capture, the `reflections` frame, a test of a
polished floor reflecting a red block and one of a mirror showing a block behind the camera through
the field alone, and the guide's numbers with the command that took them, the scene's pass at 0.29
ms with the floor polished and 0.20 ms with it rough at `Medium` on the RTX 4070 (`80227981`). The
frame before is kept with no depth, so a surface hidden in it shows what hid it for a frame, which
item 2 takes with the ray-query path. Verdict 35 is written: the model pass shader stands at 917
lines, and N 1.3's test counts the C# alone. The suite: 1,524 passed; lavapipe under validation
passed its 61 reference, field, bounce, reflection, particle, occlusion and bloom tests.

Before them, light came to bounce between surfaces through the field as hybrid Radiance Cascades,
phase two of Decision 16, which settled its item, the list renumbered: `SetGlobalIllumination` and
`Config.GlobalIllumination` take `Off`, `Low`, `Medium` or `High` and turn the field on at four
cascades where it is off; a cascade of world probes lies every eight cells of the field's cascade of
the same number, each tracing an octahedron of directions over its interval, the first from the
probe to twice the spacing and each after from its spacing to twice that, `Low` at 4 by 4 and 8 by 8
directions, `Medium` adding 16 by 16, `High` 8 by 8 then 16 by 16 in four cascades; the splat's
second dispatch paints each cell its nearest triangle's color and the light it gives off, a hit
sends on that color under the sun where the field reaches it, the unshadowed point and spot lights
and the frame before's bounce, and a miss in the last cascade brings back the environment map; the
merge runs far to near and passes over an upper probe the field hides from the lower; six faces of
irradiance a probe feed the next frame's bounce and the model pass's fallback; screen probes every
16, 12 or 8 pixels trace the first interval again on the occlusion pass's half-size depth, 16 rays
over the hemisphere through the depth and then the field, filtered 5 by 5 by normal and distance,
standing on texel middles since NVIDIA and lavapipe round a boundary apart; the model pass blends
the four around a pixel in place of the diffuse light of the environment map, the ambient lights and
the reflection probes; the halves are packed through `PackHalf2x16` by `spirv_asm`, since `f32tof16`
declared capabilities lavapipe's validation rejects; a double-sided sheet thinner than a cell gets a
second bit in the cell's word and half a cell of thickness, so rays stop at Manor's floors, with a
test and `scene_field_cascade` written again; `shaders_cornell_box` with its 800 by 450 capture, the
`cornell_box` and `lit_room` frames, four tests of the bounce and two of the frames, the guide's
table of the three qualities measured on the RTX 4070 with the frame rate unlimited through
`profile` and `gi.state`, 0.19 ms and 0.70 MB at `Low`, 0.27 ms and 2.76 MB at `Medium`, 0.34 ms and
6.72 MB at `High`, the comparison page, the cheat sheet's line and the listing (`3e00ac64`). Verdict
33's third part is mended in a commit of its own: the opening's answer goes to
`captures/<game>-opened.json`, the watcher starts before the opening and the Windows games' step has
110 of the job's 180 minutes (`5057c3cb`), unproven until a push. The suite: 1,521 passed; lavapipe
under validation passed its 54 reference, field, bounce, particle and occlusion tests.

Before them, a signed distance field of the scene came to stand around the camera, phase one of
Decision 16: `SetSceneField` and `Config.SceneField` build it from the shadow-casting meshes in
cascades of 64 cells a side, each twice as coarse and as wide as the one before, in one 3D image of
half floats with a uniform buffer saying where each lies; a cascade is rebuilt when the eye passes
its grid of eight cells or a still mesh comes or goes, the budget's number a frame, finest first; a
build is a workgroup a triangle taking each cell within four of it by an atomic minimum of its
distance in 1024ths of a cell above a bit for in front, and a mesh that moves or is skinned is
stamped as its box into bricks each frame; three faults of sign found by reading the field back are
mended and held by tests, the nearer of two meshes winning a cell, a triangle claiming a cell only
within 60 degrees of straight behind it, and a plane's open edges a narrow wedge; `ao.slang` reads
occlusion through the field beside the screen's and traces the sun's soft contact shadow, which the
model pass applies to the shadowed sun, and particles collide with the field where it holds them and
with the depth elsewhere, which closes that TODO entry; `field.show`, `field.state` and
`field.rebuild`, two reference frames, the `shaders_scene_field` example, the guide's section,
RENDERING.md's, and the costs from an RTX 4070 on the comparison page, stamping 0.014 ms, the finest
cascade 0.25 ms to build and the occlusion pass 0.073 ms where it took 0.036 (`f2d99b65`), which
settled its item. The field's kernels and its sampling module are plain Slang over buffers and one
image, which the bridge can compile as they are once BevyCSharp reads the field. The suite: 1,514
passed, none skipped. The Windows job of `3afcc4d0` was cancelled at the job's 180 minutes in the
games' step at 18:31 with nothing on the page, which Verdict 33 reads as the opening's answer read
through `$(...)` while the game e3d started holds the pipe, as BevyCSharp's step hung the same day.

The norm has 44 rules, and this engine stands at 35 checked, none with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **The next run's Windows job, Verdicts 33, 30 and 31.** Verdict 33's third part is mended at
   `5057c3cb` and proved by the first Windows job to play the twelve games; the Windows jobs of
   `0c19c335`, `6412daca`, `38f68412` and `90681ba8` end at their 180 minutes as `3afcc4d0`'s did
   and are not read; the examples job, which carries the guides' blocks and Verdicts 30 and 31, runs
   once a run's three test jobs pass. Each push's run is read by the reviewing session, and a
   failure it names comes first here.
2. **Phase 3: specular, its glossy reflections in.** A fragment under a roughness of 0.5 traces
   its mirror ray through the window's depth and on through the field, reading the frame before's
   picture or the cascades' light, the probes and the environment the fallback as it grows rough
   (`80227981`). Left: a hardware ray-query path through Vulkan's ray query extension for the
   field's misses where the GPU has it, behind the same quality tier, measured; and the frame
   before's depth kept beside its picture, so a surface hidden in that frame reflects the field's
   shading and not what hid it, which TODO.md records until then.
3. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the twelve has.
4. **The first shares recorded from the workflow's own device.** The examples job's first green
   run puts every pair measured for the first time into notices, which the public listing of the
   job's annotations gives; those shares go into `3DEngine.Examples/measured-ci.tsv` as the device's
   own, so the run after holds every pair to them and a share can only fall.
5. **The scripts' shared watch for apps made one after another.** `039bd788` says the scripts'
   watch is one stream for the process, which holds for apps that overlap, while the watch is let go
   with the last app watching it, so a hundred apps made one after another make a hundred FSEvents
   streams on macOS, read on the way to Verdict 32. The watch is kept for the process once made, or
   the sentence says what holds, whichever the macOS leak test's census argues for. The larger
   things BevyCSharp has and this engine lacks (saves, data in files of its own, files that outlive
   a renamed type, C# typed at a running app) stay `to consider` in [SHARED.md](SHARED.md), as the
   owner decided, and BevyCSharp's cheatsheet written from documentation by a tool stays to consider
   as well.

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
    Windows job of `3afcc4d0` was cancelled at the job's 180 minutes in the games' step at 18:31,
    its annotations holding the tests' notice alone, and the four jobs after it sit in the same
    step, so no budget fired. The cause reads from the script and e3d together. `drive-game.sh`
    reads the answer of `./e3d open` through `$(...)`, which ends when every holder of the pipe's
    write end has closed it; e3d on Windows starts the game through `cmd.exe /c start /b`
    (`Launch.cs`), and a process made that way is handed every inheritable handle e3d holds, the
    pipe among them, whatever the line redirects, so the substitution waits for the game to end, the
    game runs until a stop the script has not reached, and the budget's watcher, started after the
    opening, never runs; BevyCSharp's Windows step hung the same way the same day, read the same,
    and its mend at `070e5e0` writes each answer to a file. Three things: the opening's answer is
    written to a file under the captures folder and read from there, `$(...)` never wrapping a
    command that starts a program, which the SHARED.md row carries; the watcher starts before the
    opening, so a game that hangs in its opening is stopped at its budget with `as far as its
    opening` on the page; and the games' step takes `timeout-minutes` under the job's 180, so a wait
    ends the step with the page written and not the job with nothing. The four jobs still running
    end the same way and are not read again. Mended at `5057c3cb`: the opening's answer goes to
    `captures/<game>-opened.json` and is read from there, the watcher starts before the opening, and
    the step has 110 of the job's 180 minutes; Pusher played through the script on the working
    machine. Clearing the inherit flag of e3d's own handles before it starts `cmd.exe`, offered in
    the reply, is not asked for while nothing waits on them. Unproven until a push. Settled when a
    Windows job plays the twelve games.

35. **`modelpass.slang` has 917 lines, and N 1.3's test counts the C# alone.** Read from the
    tree at `80227981`: the model pass shader grew by its reflections to 917 lines, over the 800
    N 1.3 allows a source file, and `NormTests.N_1_3` reads the `.cs` files of the library, the
    tests and the examples, so the 110 Slang files are outside it, where BevyCSharp's test counts
    its Rust bridge beside its C#. Two things: the test counts the Slang shaders as it counts the
    library's C#; and the shader comes under 800, its reflections or its lighting moved into a
    module as `gi.slang` is, or is listed in `build/norm/1.3.txt` with its reason and the
    Conformance row says so. Settled when the test counts the shaders and passes.

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

Verdict 35 is mended. `NormTests.N_1_3` counts the Slang shaders of the library, the tests and the
examples as it counts their C#, and `modelpass.slang` comes to 411 lines. The lights' descriptor
set, its structs and its bindings went into `lightset.slang`, which `modelpass` passes on to a
program's own model shader as before, and what reads the set went into `lights.slang`: each
light's arrival, the shadow maps, the specular model, the probes and the environment, and the light
that bounced and the traced reflection. `modelpass` imports `lights` without passing it on, so a
program's shader sees no more names than it did, which `pbr.slang`'s own `PI` showed matters. Every
shader of the examples, the games and the templates that imports `modelpass` compiles, and no Slang
file is over 800 lines, so nothing is listed.

