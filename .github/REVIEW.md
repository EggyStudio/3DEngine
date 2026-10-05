# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `ee3b47dd`. Verdicts 1 to 3 are settled on their replies, which were read: a clock a
test steps (`966c2c88`), a kinematic body keeping its parent's pace (`15fa305a`) and one clamp
(`ee3b47dd`). The engine before the change measured where the model had put it, 1.75 against 1.74
at 144 frames a second, 2.37 at 50 and 0.10 against 0.08 at 30, and holds 2.00 within a hundredth
at every rate after it, uneven frames among them. The reply found what the model's rule had left
out of its wording, that the steps run behind the frame's clock by what is not yet stepped
through. Fifteen steps after a frame of a quarter second cost Swarm 1.8 ms against 0.85. No
tolerance was widened and no wait added in their place, and the old test stands at its tenth.
Verdict 7 is about one number in the second.

Of the Windows run of `db942962`, Verdict 4 is left, the file handle that leaves the process. The
owner pushed `main` up to `5b2234d2` on 2026-10-05, before any of this, so the run of that push
draws on Windows and macOS for the first time and is likely to show the platform test red again.
`Config.FrameSeconds` and `Time.FrameSeconds` are new public lines and
`PhysicsSettings.MaxStepsPerFrame` is gone, which Decision 5 leaves with the owner.

[NORM.md](NORM.md) is new, and item 4 of the Now list is about it.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **Verdict 4**, the last of what the Windows run says, which is in hand.
2. **Verdicts 5, 6 and 7**, a body asleep when a layer changes, the number a pair's press
   returns, the second before a package carries the function, and the distance past which a
   parent's move is a placing.
3. **A picture in the README opens the example's own source** (N 4.5). The owner chose this for
   BevyCSharp on 2026-10-05 over the live demos, so that a picture leads to the program that drew
   it and nothing is cached from another project's site, and the reason holds here word for word.
   Each picture in the gallery links to the file that holds its example, as a full address under
   `https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/`, the README being the
   package's page too. The links to raylib's site go, with `build/raylib-examples.sh`,
   `build/raylib-examples.txt`, their paragraph in BUILDING.md and the comparison in
   `DocumentLinkTests`, which holds every picture to a link whose file is in the checkout
   instead, with no request made. The sentence above the gallery says a picture opens the program
   that drew it. BevyCSharp did the same in its `57fc7e9`.
4. **The norm's checks.** [NORM.md](NORM.md) is new, at the owner's wish of 2026-10-05: the
   rules both engines keep, numbered, each with its reason and what checks it, 36 of them and
   DESIGN.md's eleven sections by reference. One batch gives the rules their checks here. A class
   `NormTests` has a test for each rule the table under Conformance calls `to take` for 3DEngine
   and a test or a setting can check, named for the rule as `N_1_3` is for N 1.3, its message
   beginning with the rule's number. A rule existing code does not keep gets its list,
   `build/norm/<number>.txt`, written from the test's own finding so the first list is exact, the
   test failing for a place not listed and for a line that no longer applies. One more test holds
   NORM.md and `NormTests` to each other. No file is rearranged in this batch. The lists are what
   is left, and a place on a list is mended when a batch next touches it.

   N 3.3 and N 3.4 start at what Verdicts 1 and 4 leave. N 2.8 finds `BepuUtilities` and
   `SDL3-CS.Native` missing from D 8's table, and `Microsoft.CodeAnalysis.CSharp` there under a
   shorter name. N 1.5 finds `templates/`, `games/` and `docs/` missing from AGENTS.md's table,
   whose rows are added without asking, as N 7.4 says. N 2.5, N 2.7 and N 5.2 are steps of the
   workflow and more than this batch, and keep `to take` until their own, N 5.2 being item 5. The
   count of each list goes under Replies, and the table in the norm is brought up to them. A rule
   read as wrong is answered under Replies with a line beginning `Rule:`.
5. **raylib's own examples, one by one, as the measure** (N 5.2). `coverage.py` counts raylib's
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
   on. Many a batch, a module at a time, and it is the item to come back to whenever the ones
   above are through.
6. **A probe filtered on the GPU** (TODO.md, Probes capture once and on the CPU), so a capture
   costs a frame's worth of GPU and no readback, which recapturing on a light's change made
   worth having.
7. **C# typed at a running program** (TODO.md, The command line has no evaluator), which this
   engine's own list names: an `e3d eval` that compiles a line or a file against the running
   world through the script compiler already there, for looking at and changing a game while it
   runs.
8. **TODO.md's order** for everything else, the Scenes entry on a program's own spawn among it,
   and another game only when it is of a kind that uses what none of the seven has.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 3 are settled, and their numbers are not given again.

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

**7. A far jump is ten units, whatever a unit is and whatever the frame took.**
`ParentFollowers.PlaceBeyond` places a body whose parent moved more than 10 units in a frame. A
game whose unit is a centimeter has a lift at 7 meters a second placed every frame, carrying and
pushing nothing. At the clamp of a quarter second any parent faster than 40 units a second is
placed for that frame, and what rode it is left behind, so a slow frame undoes for a fast
platform what Verdict 2 mended. No distance tells a move from a placing for every game. The
number becomes a setting of the physics, in units and documented as one, and a program that knows
it is placing a parent says so with a call, which a level starting again uses. A test carries a
crate on a platform at 60 units a second through a frame of a quarter second. In the same file,
`Observe` makes a set and an array every frame for any world with a parent in it, which are kept
and used again as `_wanted` is.

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
6. **AGENTS.md's bullet and table row on NORM.md are the owner's, with the exception N 7.4
   makes.** They approved both on 2026-10-05 in the reviewing session, with the plan for the
   norm. A working session that commits a change to its instruction file only on the owner's
   word in its own session is right to, and waits for that word.

## Replies

**Verdict 4, a handle that leaves the process.** Assimp opens nothing itself now. `AssimpFiles`,
an `IOSystem` of the binding's, hands it every file it asks for as a C# stream, which Windows
opens uninheritable. `LoadModel` gives Assimp the file's name alone and opens it and the files
beside it from its folder, so a path with letters outside ASCII no longer crosses into native code
either. The asset server gives a loader the reader the file came from, and the model's `.mtl` and
`.bin` are read from that reader under the model's folder, which ends `SpoolToTempFile`. An OBJ
held in an in-memory reader finds its library, and a glTF with its buffer in a `.bin` of its own
reads the same positions from a folder and from a reader as the arm with its buffer inside. A
build with native AOT loads `torus.obj` with the color and the texture its `.mtl` names, so the
callbacks hold there. `TestFolder` is the one folder of the 22 test files, held by the class and
disposed by it, the folders that were made and deleted inside a test among them. A delete is tried
again for two seconds, and a file still held is then named with its holders from the Restart
Manager, the test's own process marked as such. `FileHandleTests` loads a model from a path, a
glTF, a model through the asset server, a texture, an image, a wave from WAV and from Ogg, music
once unloaded, a font, a scene and a shader, and finds none of their files under `/proc/self/fd`
or, on Windows, refused to a writer that shares nothing. A test of the check first finds a file it
left open held. macOS can tell neither, so those tests skip there and say why.

**Verdict 5, a body asleep when a layer changes.** The trigger had the same fault, as the verdict
thought. A crate resting on a floor stayed in the air in all five ways the theory changes it, the
crate's layer, the floor's, which layers collide, the floor made a trigger and the crate made one,
and falls through in each now. A body whose layer or trigger changes is woken, and a static wakes
the bodies the broad phase finds within its bounds, which for a sleeping body are kept in its tree
of statics. A change to which layers collide wakes every sleeping body on the two layers, gathered
from the sleeping sets before any is woken. A setter given what a body already has changes and
wakes nothing, so a scene's collider setting its layer each time it is read costs nothing. The
flat functions' remarks say a change wakes.
