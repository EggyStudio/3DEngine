# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `53cd565f`. Four commits are settled on their replies, which were read: a fast body
swept over each step (`799a9d56`), a slider joint (`979c97be`), a model spawned again by hot
reload (`5b2234d2`) and how hard a pair presses (`53cd565f`). The sweep says what was measured, at
which speed a wall of which thickness holds, and the hot reload's test found the copy losing its
parent behind the fault it was written for. Verdict 6 is about the number the last one returns.
With them TODO.md's Physics section is through, so the Now list has a new long item in its place.

The owner brought back the Windows run of `db942962`, in which 2 of 1077 tests failed. Neither was
bad luck. One is a fault in the engine that a game running at 50 or at 144 frames a second has, and
the other is a file handle that leaves the process. Verdicts 1 to 4 are about them. The owner
pushed `main` up to `5b2234d2` on 2026-10-05, so a run is under way that draws on Windows and
macOS for the first time and has none of the four mended, and the platform test is likely to be
red in it again.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What the Windows run says**, which is Verdicts 1 to 4 in that order. The clock comes first
   because the other three are measured with it.
2. **Verdicts 5 and 6**, a body asleep when a layer changes and the number a pair's press
   returns, the second before a package carries the function.
3. **A picture in the README opens the example's own source.** The owner chose this for
   BevyCSharp on 2026-10-05 over the live demos, so that a picture leads to the program that drew
   it and nothing is cached from another project's site, and the reason holds here word for word.
   Each picture in the gallery links to the file that holds its example, as a full address under
   `https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/`, the README being the
   package's page too. The links to raylib's site go, with `build/raylib-examples.sh`,
   `build/raylib-examples.txt`, their paragraph in BUILDING.md and the comparison in
   `DocumentLinkTests`, which holds every picture to a link whose file is in the checkout
   instead, with no request made. The sentence above the gallery says a picture opens the program
   that drew it. BevyCSharp did the same in its `57fc7e9`.
4. **raylib's own examples, one by one, as the measure.** `coverage.py` counts raylib's functions,
   491 of 619 carried, and nothing counts its examples, of which 45 programs here carry a few.
   BevyCSharp holds itself to Bevy's 421 examples in a table a script writes from Bevy's own
   list, and writing them one by one found faults no test had. The same here: a table of every
   example in the `examples/` folder of the raylib checkout `build/raylib-bench/run.sh` pins,
   made by a script, each row saying whether it is written, written in part, can be written
   with what the flat API has, is missing something, or does not apply, with the count at its
   head. An example written keeps raylib's name, its window of 800 by 450 and its scene, is
   opened by name and captured as the others are, and its picture is set beside the screenshot
   raylib keeps next to each example's source. A function it calls that the flat API lacks is
   carried, or its row says why not, which is TODO.md's entry on the 128 functions taken from
   the side a program meets them. A picture that differs from raylib's for no known reason is
   taken down to the smallest program that still differs and explained before the pass goes
   on. Many a batch, a module at a time, and it is the item to come back to whenever the ones
   above are through.
5. **A probe filtered on the GPU** (TODO.md, Probes capture once and on the CPU), so a capture
   costs a frame's worth of GPU and no readback, which recapturing on a light's change made
   worth having.
6. **C# typed at a running program** (TODO.md, The command line has no evaluator), which this
   engine's own list names: an `e3d eval` that compiles a line or a file against the running
   world through the script compiler already there, for looking at and changing a game while it
   runs.
7. **TODO.md's order** for everything else, the Scenes entry on a program's own spawn among it,
   and another game only when it is of a kind that uses what none of the seven has.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

**1. Time cannot be stepped by hand, so every test of motion measures the machine.**
`TimePlugin` reads a `Stopwatch` and nothing else can move `Time`. Eight test files wait on a wall
clock or sleep (`OffscreenRenderTests`, `Engine3DPhysicsTests`, `CliTests`, `BadFileTests`,
`SceneRefTests`, `AssetReleaseTests`, `WindowResizeTests`, `ParticleTests`), and `RunUntil` in
`Engine3DPhysicsTests` runs frames for two seconds of the runner's time, however many that is.

`Time` takes a set amount a frame when asked, and reads no stopwatch while it does. A test asks
for it and counts frames, so `RunUntil` becomes so many frames of a sixtieth of a second, and a
test can make a slow frame on purpose, which none can today. A run with no window can ask for it
from the command line, so a capture is the same picture on every machine and a reference picture
of something that moves can be compared. The long plays in build.yml may keep the machine's time,
since a sixtieth a frame under lavapipe makes a walk of 90 seconds 5,400 frames, and that is
chosen with the numbers in hand. Bevy has this as `TimeUpdateStrategy::ManualDuration`. A wait
that is about something outside the frame loop, a file watcher or a child process, stays and says
so.

**2. What a kinematic body carries keeps the body's pace only at some frame rates.** This is
what `A_Platform_Under_A_Moving_Parent_Carries_A_Crate_And_A_Character` measured on Windows, a
pace of 2.33 where the platform's is 2.

`FollowPose` gives the body the velocity that covers the whole distance to its parent's place in
one step. The parent moves once a frame. In a frame that holds one step, the body's speed is the
frame's distance over a step's time, which is the parent's speed times the frame's length over a
step's. In a frame that holds several steps, the first covers the frame's distance and the rest
stand still. Friction can change a crate's speed by about 0.16 units a second in a step at most, which
is small against those jumps, so the crate settles at the speed the platform has in most steps and
not at its average.

A model of one dimension written for this review has a carrier moved by 2 times the frame's time,
a platform that follows by `FollowPose`'s rule in steps of a sixtieth, and a crate that friction
pulls toward the platform's speed by at most 0.16 a step. At a steady frame rate it gives the
crate this pace, the platform's being 2.00.

| Frames a second | 1000 | 240 | 144 | 120 | 75 | 60 | 50 | 45 | 40 | 30 | 20 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| The rule today | 2.00 | 2.00 | 1.74 | 2.00 | 1.64 | 2.00 | 2.37 | 2.63 | 2.95 | 0.08 | 0.05 |
| The parent's speed | 2.00 | 2.00 | 2.00 | 2.00 | 2.00 | 2.00 | 2.00 | 2.00 | 2.00 | 2.00 | 2.00 |

At 19.4 ms a frame the model gives 2.30, and the Windows runner measured 2.33. The tests pass
here because a frame with no window is far shorter than a step, which is the first column. A game
at 144 frames a second has a crate sliding back along its platform, one at 50 has it running
ahead, and one at 30 leaves it where it stood. `0baec67f` widened the time this test averages
over, and the comment it left names the symptom.

The second row is the rule that holds in the model at every rate, steady or uneven. The body
moves at its parent's speed, which is the distance the parent moved between two frames over the
time between those frames, through every step of the next frame, and each step aims at the
parent's last place moved on by that speed for the time simulated since. With it the platform
ends on its parent when the parent stops, and is at most a frame's travel off for a step when the
parent turns back. A turn is followed the same way as a place.

The model is a model. With the clock of Verdict 1, one test as a table over 144, 75, 60, 50, 40,
30 and 20 frames a second, and over frames of uneven length, measures the engine. The crate's
pace is held to the platform's at every rate, and the numbers from before the change go under
Replies beside the model's.

Two things are settled with it. A parent that a behavior moves in fixed steps moves a step's
distance a step, which today's rule follows exactly, and the rule that replaces it has to as
well. A parent put far away in one frame, as when a level starts again, is a body flung there in
one step with everything it meets, and whether that is a move or a placing is decided and tested.

**3. A frame's time and the steps that spend it are clamped apart.** `Time.MaxDeltaSeconds` is
0.25, and `FixedTime` runs at most five steps a frame and drops the rest. A frame that took
between 83 and 250 ms tells the program a quarter of a second passed and simulates a twelfth, so
whatever the program moved by `GetFrameTime()` went three times as far as the simulation had time
for. In the model, with the second rule of Verdict 2, a crate holds 2.00 through slow frames of up
to 250 ms when no time is dropped and falls to 1.29 when it is.

One clamp. The steps spend all the time the frame reports, as Bevy's fixed schedule does under
its clamp of 250 ms, and the count of steps follows from the clamp where it is a second number
today. The remark on `FixedTime` that a backlog never drains is answered by the clamp, since a
frame can owe fifteen steps at most. What fifteen steps cost after a slow frame is measured in
Swarm with `frame.profile`, and if that is too much the clamp comes down, which slows the whole
game together below some frame rate where today the simulation slows alone.
`PhysicsSettings.MaxStepsPerFrame`, 8, is a third number, for a world with no `FixedTime`, and
goes the same way. DESIGN.md says which number was kept and why.

**4. `tri.obj` was held by another process, most likely `slangc`.**
`A_Model_Finds_The_Material_Library_Beside_It_From_A_Path_Or_A_File_Stream` failed in its
`finally`, where `Directory.Delete` found `tri.obj` in use. The test disposes its stream before
that and the reader disposes its importer, so nothing in the engine holds the file. What reading
does show is a way for a handle to leave the process.

- Assimp opens a model with the C runtime (`_wfopen`), whose handles a child process inherits
  unless asked otherwise. It is the one place found where the engine hands a path to native
  code, every other loader reading through `File` in C#.
- `Process.Start` on Windows calls `CreateProcess` with handle inheritance on and no list of
  handles, in .NET 10 as before, so a child takes every inheritable handle open in that instant
  and keeps it until it exits.
- The Windows job fetches `slangc`, so `SlangCompilerTests` start it as a child beside the model
  tests, xUnit running classes in parallel.

A `slangc` started while Assimp had `tri.obj` open holds it for as long as it compiles, and the
delete comes a moment after the load. About fifteen places in `AssimpModelReaderTests` and
`Engine3DModelTests` load a model and delete its folder, so a run has that many chances. This is
read from code and was not seen on Windows, which the second point below turns into something
the next run says.

- **Assimp reads through a file system written in C#.** The binding has `SetIOSystem`, with
  `FileIOSystem` as one. A `FileStream` is not inherited, so no handle is left to leak, to a
  test's child or to a process a game starts while a model loads. The same file system can hand
  Assimp the files beside a model from the reader the model came from, which ends the temporary
  copy in `SpoolToTempFile` and gives a model in an embedded or an in-memory reader, or one a game
  wrote, its `.mtl` and `.bin`, which the reader's own remarks say it loses today.
- **One folder for tests.** Twenty-two test files delete a folder themselves, most in a
  `finally`, where an exception replaces whatever the test threw, so this failure does not say
  whether the test's own checks passed. One helper makes the folder, and a test class holds it
  and disposes it, which xUnit reports beside the test's failure and not in its place. It tries a
  delete again for two seconds, and when a file is still held it says which processes hold it, on
  Windows through the Restart Manager (`RmStartSession`, `RmRegisterResources`, `RmGetList`). If
  the next red run names something other than `slangc`, this verdict is wrong and the name says
  where to look.
- **A loader that keeps its file is caught on Linux too.** Linux deletes an open file without
  complaint, so only the Windows job can see a file kept open, and only by chance. One test as a
  table over the loaders (a model, a texture, a sound, a font, a scene, a shader) loads a file
  and then finds no entry of `/proc/self/fd` pointing at it, and on Windows opens it for writing
  with no sharing.

**5. A body asleep does not hear that a layer changed.** `SetLayer` and `SetLayersCollide` write
a table and wake nothing, and a pair that sleeps is not tested again until something wakes it. A
crate asleep on a floor stays in the air when the floor's layer stops colliding with its own,
until something else touches it. `SetTrigger` writes a table the same way and is likely to have
the same fault, which the same test tells. The test rests a crate until `IsAwake` says it sleeps,
changes the layer and finds the crate falling. A body whose layer or trigger changes wakes, with
the bodies within its bounds when it is a static, and a change to which layers collide wakes the
bodies on the two.

**6. `GetPhysicsContactImpulse` adds a twist and the friction to a push.** `ImpulseSum` adds the
size of every number the solver keeps for the pair, which for two convex shapes are two of
friction along the surface, one of twist about the normal and one of push for each contact. A
twist is an impulse of turning, in other units, and the two of friction are parts of one vector,
so the sum is the pair's weight times the step only while nothing slides or spins. The summary
says the number over the step is the force between them, which then does not hold. The push
alone is what a pressure plate and a thing that breaks read, the sum of each contact's
penetration impulse, which Bepu hands over through `TryExtractSolverContactData` and
`GetPenetrationImpulseForContact`. Friction, where a game asks for it, is a number of its own. A
test drags a crate across a plate and finds it pressing by its weight times the step, as it does
at rest. `PublicApi.txt` has the function since `53cd565f`, so it is changed before a package
carries it.

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
   in `build/version.txt` before the next package. After `82b1feb4` and `abd09df5` that is due.

## Replies

**Verdict 1, a clock a test steps.** `Time.FrameSeconds`, set from `Config.FrameSeconds`,
`--frame-time S` or `E3D_FRAME_TIME`, makes each frame advance time by that many seconds and read
no clock, the frame still paced by the clock so a window shows it at its rate. A test changes it
for one frame to make a slow frame. `Engine3DPhysicsTests` run frames of a sixtieth, `RunUntil`
being so many frames and the vehicle's `Steps` a frame a step, and finish in 3 seconds where they
waited on the runner, and the particle sheet test makes its tenth-of-a-second frames with the clock
where it slept. The waits that stay are on something outside the frame loop and say so: a probe's
readback and prefilter on a worker (`OffscreenRenderTests`), files read on the asset server's
worker (`BadFileTests`, `AssetReleaseTests`), the socket a request waits on (`CliTests`), the file
watcher (`SceneRefTests`) and the window's resize settling (`WindowResizeTests`). Tests read the
argument and the variable, and sixty frames of a sixtieth come to a second exactly with a tenth
after it on purpose. `.claude/skills/e3d-cli/SKILL.md` lists the run flags too, and as an
instruction file for agents it is left to the owner to add `--frame-time` to. `PublicApi.txt`
gains `Config.FrameSeconds` and `Time.FrameSeconds`.
