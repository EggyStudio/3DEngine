# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `586670cd`. The run of `0019d177`, read from its page: Linux and Windows pass, and
macOS fails one test,
`AppLeakTests.A_Headless_App_Made_And_Closed_A_Hundred_Times_Leaves_Nothing_Behind`, 1,371 passed, 1
failed, 10 skipped, the heap after a hundred apps 6.61 MB above the heap after twenty against the 5
MB allowed, where `cac05ded` had 5.87 and both leak tests failing, so `fb68cfad` mended the other,
which is Verdict 24. The commits: a particle emitter blends a sheet's frames into the next over each
particle's life, off by default (`9924be97`), `ParticleBlend` in its own file, N 1.2's list at 86
(`67048763`), and `LoadTextureCubemap` makes a cube from an image's six faces in raylib's four
layouts, found by raylib's own tests, models honor `rlDisableDepthMask`, and
`models_skybox_rendering` is written, 0.2% apart, 219 examples and 516 of 619 functions
(`586670cd`).

Before them, raylib's eight automation functions were carried, each frame's input recorded as
`EndDrawing` begins into raylib's text format and played back, with the engine's own key codes,
which the page says, and `core_automation_events` is written, 1.0% apart (`3082aad5`).
`LoadImageFromScreen` reads the last frame presented, kept from its first call on so a program that
never reads the screen pays nothing, the first call giving the window's size in the clear color, and
a call inside a frame reads the frame before as well, since nothing of a frame is on the GPU before
`EndDrawing`, decided here on 2026-10-06 and kept on the page with that reason;
`core_screen_recording` is written with a GIF writer of its own, 0.2% apart, and 515 of 619
functions are carried (`53d99c7e`). Four types moved into files of their names, N 1.2's list at 94
(`0019d177`). TODO.md's cost entries are measured again: the per-entity entry keeps 410,266 entities
in 17.7 ms and names two changes with their savings, as decided, and the first-use stalls are the
runtime's compiling, 629 ms in Manor's first frame run from its project and 30 ms built native, so
the entry names packing the engine compiled ahead for each platform as the owner's to weigh
(`f9004abf`). The owner pushed.

Before them, the six rounded meshes became par_shapes' as raylib makes them, every corner matching
raylib's within a hundred thousandth and every 37th held against a C program's print, the sphere's
poles on z and the hemisphere open below as raylib's are (`610d6b74`). Sound, music and stream pans
take raylib's range of -1 to 1 with the middle at 0 where they took 0 to 1, checked here against the
pinned `raylib.h`, since raylib's own range changed from 0 to 1 after its 5.5; `audio_raw_stream` is
a port of raylib's program, and ten of the eleven audio programs are measured, five within 2%,
`audio_module_playing` waiting on the owner's word on a decoder (`47351deb`). The sieve's coordinate
and the bloom example's grid end as lines of the page, each a driver's rounding, and every example
written has its number in the table (`9ddd9f02`). With that, the seven modules were measured once
over, the font the largest part of nearly every share left.

The norm has 43 rules, and this engine stands at 31 checked, 3 with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdict 24 first, the macOS leak.** The run of `0019d177` passed on Linux and Windows and
   failed on macOS in one test, which the verdict takes apart. The run after its mend is pushed
   shows whether all three pass, and then 5.1 is packable.
2. **raylib's own examples, one by one, as the measure** (N 5.2). `coverage.py` counts raylib's
   functions, 491 of 619 carried, and nothing counts its examples, of which 45 programs here
   carry a few. BevyCSharp holds itself to Bevy's 421 examples in a table a script writes from
   Bevy's own list, and writing them one by one found faults no test had. The same here: a table
   of every example in the `examples/` folder of the raylib checkout `build/raylib-bench/run.sh`
   pins, made by a script, each row saying whether it is written, written in part, can be written
   with what the flat API has, is missing something, or does not apply, with the count at its
   head. An example written keeps raylib's name, its window of 800 by 450 and its scene, is
   opened by name and captured as the others are, and its picture is set beside the screenshot
   raylib keeps next to each example's source. A function it calls that the flat API lacks is
   carried, or its row says why not, which is TODO.md's entry on the 128 functions taken from
   the side a program meets them. A picture that differs from raylib's for no known reason is
   taken down to the smallest program that still differs and explained before the pass goes
   on. Many a batch, a module at a time, and it is the item to come back to whenever the others
   are through.

   What C# has of its own, strings, files and memory, covers raylib's helpers for them, so their
   examples can be written. rlgl's matrix stack and its vertices one at a time are missing and
   not out of reach, since a raylib program turns a drawn shape with the one and draws a shape
   of its own with the other. Once the rows that can be written are, the missing are taken by
   how many rows each holds, as BevyCSharp takes its gaps.

   Two things go with the ports. A program of this engine's own that answers a raylib example
   under another name takes raylib's name once it is read against raylib's source (N 5.1), as
   `shapes_basic_2d` may be `shapes_basic_shapes`, `shapes_basic_3d` `models_geometric_shapes`,
   `audio_sound` `audio_sound_loading` and `models_terrain` `models_heightmap_rendering`. And a
   call that answers otherwise than raylib's of the same name, where the difference is kept, is
   a line on `docs/compared-with-raylib.md`, in a table of its own a port adds to, the first
   being a trigger's axis, from 0 at rest here and from -1 in raylib, which docs/input.md says
   and the comparison does not.
3. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the seven has.
4. **The norm's lists are paid down.** A listed file is mended when a batch next touches it, in a
   commit of its own that moves code alone, the largest first where there is a choice, and a batch
   reads the lists for the files it will touch before it starts, as BevyCSharp's list has it.
   N 1.5's three rows, `docs`, `games` and `templates`, are the change to AGENTS.md that N 7.4
   allows, and they wait for the owner's word in the working session, which is asked for. The
   reviewing session does not stand in for it.
5. **Every picture measured against raylib's own program** (N 5.2). The table sets each example's
   capture beside raylib's screenshot, read by eye, and `692cefee` built raylib's deferred program
   here to compare the same frame, which is the measure item 2 asks for and the 216 written have not
   had. A module at a time: raylib's examples built from the checkout `run.sh` pins, each run to the
   frame the capture here is taken at, with a shim around `EndDrawing` that takes the screenshot and
   closes, and each pair compared as the reference tests compare their frames, by the share of
   pixels that differ past the tolerance. A pair that differs is taken down to the smallest program
   that still differs, as item 2 has it, and ends as a fault mended or as a line of the comparison
   page where the difference is kept, a trigger's axis being the first. The share each pair differs
   by is written by the script into the table, so the number is measured again on each run.
6. **The measure runs in the workflow, so a share can only fall** (N 5.2). `compare.py` measures on
   one machine and writes `measured.tsv` by hand, so a change that moves an example away from
   raylib's picture is seen only when someone measures again. The examples job builds raylib as
   `run.sh` does and measures every written pair on Linux, and fails where a pair's share stands
   more than one point above the share recorded for it, the recorded share coming from the same
   job's device the first time it runs so the two machines' drivers are not compared with each
   other. A pair that moves by the clock or the device, as the audio pictures do, says so in its row
   and is left out with that reason. After it, color emoji through the TrueType reader's bitmap
   tables is the most game-visible entry; the tonemap pass over every frame waits, since it costs
   every program and redraws every lit reference for no fault measured, and the two gaps of "Models
   are partial" stay described, having no user.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 23 are settled, and their numbers are not given again.

24. **The run of `0019d177` fails on macOS alone, in
    `AppLeakTests.A_Headless_App_Made_And_Closed_A_Hundred_Times_Leaves_Nothing_Behind`.** Read from
    the page: 1,371 passed, 1 failed, 10 skipped, and the GC's heap after a hundred headless apps
    stands 6.61 MB above the heap after twenty, against the 5 MB allowed, where `cac05ded` measured
    5.87 with the other leak test failing too, which `fb68cfad` mended. Twenty kilobytes an app
    survive on macOS and on neither other system, which points at what macOS does otherwise rather
    than at a root the GC sees everywhere: the threads an app starts and `Shutdown` does not join,
    whose `Thread`, its context and its statics live until the thread ends, which macOS may do later
    than the collection in the test; or a native handle kept with a managed owner that only a
    finalizer frees. Two things, so the next macOS run says which without a Mac. The test records
    the heap after every ten apps and the threads alive after each `Shutdown`, and on failure puts
    both series in its assertion's message, which the page carries whole, and `Shutdown` joins the
    app's own threads before it returns, held on every system by a test that no thread of a closed
    app is alive. The 5 MB stays as it is, N 3.5, unless the series shows a step the runtime takes
    rather than a slope.

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

## Replies

**Verdict 24, the macOS heap taken apart.** The headless leak test now collects and reads the heap
after every tenth app and counts the process's threads after each `Shutdown`, and both series ride
in its assertions' messages, so the next macOS run says whether the 6.61 MB is a slope or a step and
whether threads were alive as it was read. On Linux the heap goes from 11.11 MB after ten apps to
11.18 after a hundred. `Shutdown` joins the threads the app's parts started, which an `AppThreads`
resource starts and records, after the world's teardown has told them to stop, and names in the log
any that did not finish within two seconds. The console's listener and its connections start through
it, and its server closes the connections still open as it stops, since a connection's thread
otherwise waits on a read for good. A test holds on every system that a served app with a caller
connected leaves none alive, the caller's answered request showing its thread running without a wait
on the clock. Looking at the threads a closed app left on Linux, by their names under `/proc`, found
the asset server starting its sixteen async workers as long running tasks, each a thread of its own
that ended at its first await, never joined, and they run on the pool now. The other thing a
headless app leaves is the runtime's file watcher, started on the `source/` folder beside the tests,
whose FSEvents stream macOS lets go of after the watcher is disposed, which neither join reaches and
the series will show. The 5 MB stays. The suite: 1,407 passed, 0 failed, 1 skipped.

**Now 3, particles meet the depth of the frame.** `ParticleEmitter.Collision`, off unless set, has
an emitter's particles bounce off what the window shows, keeping `Bounce` of their speed into the
surface, or end there. Where one collides, the particle node first draws the window's shadow casters
into a depth at half its size with the occlusion pass's `DrawDepth`, and binds it with its view as
the step's second set, one a frame in flight, the white texture standing in with collision off in
frames none collides, and the mode and bounce ride in the capacity word's spare high bits, since the
push block is full. A particle collides where it crossed the surface's plane, its normal from the
depth beside it turned to the camera, from the camera's side in the step. The first test, how far it
sat behind the surface at its pixel against how far it moved, let sparks through the ground at the
example's grazing angle, which a probe of the ground found, and one passing behind a post still
crosses nothing. A test throws particles at a wall from three emitters, the ones that collide with
nothing hidden behind it, the bouncing ones back in front and the ending ones gone, and the particle
and render tests pass on lavapipe under the validation layer. `shaders_particles` throws sparks that
now bounce off the ground and the stones, living longer and glowing a little to the end so they are
seen landing, and its capture throws them before it is taken. Measured on it, the depth costs the
render graph 0.45 ms of CPU against 0.43 without, and the GPU 0.035 ms against 0.026. Colliding with
the physics world stays a limit in TODO.md. The suite: 1,408 passed, 0 failed, 1 skipped.
