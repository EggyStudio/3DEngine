# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `2094e704`. A reflection probe's capture is filtered on the GPU in the frame that
draws its sixth face, with nothing read back, and its reference frame is redrawn with the reason
measured, the CPU filter having overweighted the poles of its equirectangular image, a fault the
environment map's filter shares and item 3 takes (`3f597c01`). `./e3d eval` compiles C# against
the running program and runs it between frames, four tests and no trim warning (`075c5b3c`). A
scene spawn hands back every load it took, where `SceneSpawner.Spawn` loaded textures nothing
held (`2094e704`), which closes TODO.md's Scenes entry. Items 3 and 4 are settled, and the list is
refilled. No verdict is open.

Before them, a render texture draws into up to four images of their own formats at
once, with one depth, pipelines shared by targets of the same formats and a shader's outputs
read from its SPIR-V, rlgl's color blend switch is carried, and `shaders_deferred_rendering` is
written (`692cefee`), held under lavapipe and the validation layer in a container, which found a
device feature the code had assumed, and beside raylib's own program built here, which draws the
same frame. Every raylib example the engine carries is written, 216 of 221, the five missing out
by direction. The library is marked AOT compatible and its build has no trim warning, the asset
server's and the ECS's reflection mended and the console's and the script compiler's said at
their places with their reasons, the native publish naming AssimpNetter's own alone, and Pusher
published native drew its frames (`c3dddc1b`), which settled the trimmer's item.

Before them, Inter-Quake Models (`52304768`) and Model 3D files (`e5ea2a22`) are read
by readers of the engine's own, as raylib reads them, the first for the skeleton and clips
Assimp left out and the second from m3d.h under its license with no dependency added, each with
four tests on a file the tests write. `IsModelAnimationValid` compares bone counts and parents,
and names where a clip has them, a kept difference the comparison explains.

The norm has 43 rules, and this engine stands at 31 checked, 3 with places listed, none to take
and 9 by review.


## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What the next page says.** The run after `fb68cfad` is pushed shows whether macOS passes
   `AppLeakTests`, which the reviewing session reads and says here. The ports go on meanwhile.
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
3. **The environment map's filter, on the GPU as the probe's is.** `3f597c01` found the CPU filter
   of an equirectangular image overweighting its poles, the last mip's mean light 3.8 times the
   first's where the GPU's falls by a tenth, and the environment map keeps that filter. It is
   filtered as the probe is, its references redrawn with the reason measured as the probe's were
   (N 3.5), and TODO.md's entry on it leaves.
4. **A script compiled again while a game runs keeps the state the game was in**, from
   BevyCSharp's `16c4c1e` (SHARED.md). A behavior's fields and the entities it keeps survive the
   recompile where the new generation declares them, and a test changes a script mid-game and
   finds the game where it was.
5. **raylib's functions not carried**, 117 of 619 at `c3dddc1b`, read by kind from TODO.md's list
   for the ones a game calls outside the examples, each carried or its line of the comparison
   saying why not, as item 2 has it for an example's call.
6. **TODO.md's order** for everything else, and another game only when it is of a kind that uses
   what none of the seven has.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 23 are settled, and their numbers are not given again.

None open.

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

**Now 3, the environment map's filter on the GPU as the probe's is.** The CPU filter's fault was
measured again here, by a test of the old code in a worktree of `2094e704`: a cap of light 0.2
radians across at the zenith grew from 0.39 of the mean light at the mirror mip to 1.19 at the
roughest, where the same cap on the horizon fell from 0.39 to 0.35, the pyramid of the
equirectangular image averaging its rows alike. The environment map is now filtered by the probe's
stages. `EnvironmentMap` holds the image decoded to half floats, an eight-bit one through a table of
256, and an `environment` node ahead of every pass uploads a map it has not seen with its mips and
records `RecordEnvironmentFilter`. `env_gather.slang` resamples the image into a source cube twice
the target's width, four samples a texel, each read from the image's mip whose rows are as far apart
as the samples, and into the sky's cube, then the probe's mips, prefilter and irradiance stages run
on that source. The probe filter's code is shared as a `FilterRun`, the cube it fills is a
`FilteredCube`, with an irradiance buffer or none for the sky, and a source cube of each width is
made once. The model pass reads the environment's irradiance from a storage buffer at the lights'
set's binding 14, so the lighting buffer carries 144 bytes less. `SetEnvironmentMap` on images of
256, 1024 and 4096 texels across took 33, 87 and 917 ms on this machine's 32 threads, and takes 0.2,
2.3 and 10 ms, timed by `./e3d eval` with a stopwatch around the call on `models_reflection_probe`,
and the frame that uploads a 4096 image spends 55 ms of CPU copying it to staging, as
`profile.slowest` reads it.

A test puts the same cap at the zenith and on the horizon and holds their mean light within 5% at
every mip. The old filter fails it from mip 1, measured on `2094e704` in a worktree. The CPU tests
of the filter's numbers move to the GPU, read back from the renderer: a uniform sky at every mip and
its irradiance from four sides, a sky lit from above, the top of the image as the +Y face with
roughness blurring toward the horizon, and an HDR sun. The decoding stays a unit test, with a test
that an image wider than 4096 is halved and a pixel that is not a number goes dark. No reference
moved past its tolerance: the 96 render and reference tests pass against their references and their
2% unchanged, `environment_and_sky` among them, so none is redrawn and no tolerance changed (N 3.5),
and `models_reflection_probe` and `models_skybox` are captured again, 2.5% and 8% of their pixels
moved, the spheres' shading and the frame counter. The 101 render, reference and filter tests pass
on lavapipe under the validation layer in the container. Pusher drew 200 frames from the package
with `build/play-game.sh`, and Summit and Manor ran 120 frames hidden with nothing in their logs.
The suite: 1,335 passed, 0 failed, 1 skipped. RENDERING.md's paragraph and the flat API's remarks
say so, and TODO.md's entry keeps the probe's capture on a change alone. The timings of `eval` in
the guide name `profile.slowest`, which measured them, and the skill and the code's remarks drop
theirs, as N 3.6 has it.

Shared: an environment filtered from a cube rather than from its equirectangular image's mips, which
BevyCSharp's Bevy does from a cubemap.

**Now 4, a script compiled again keeps the game where it was.** A compile still runs on the file
watcher's timer, and leaves the new generation pending, which `App.BeginFrame` swaps in before any
stage runs, as it applies the state transitions between stages: the last generation's systems out,
the new one's in, and every component and resource of a type the last generation declared made again
as the new generation's type of the same name (`ReloadedScripts`). The swap ran on the timer's
thread before, into the live list of the stage running, which `RunSequential` walks as a span, so a
reload in the middle of a frame could skip a system or run one twice. A field kept keeps its value,
one added starts as the new type's constructor leaves it, an engine or .NET value, an `Entity` among
them, is carried as it is, one of the script's own types is made again the same way, an enum by its
value's name, and an array or a `List<T>` element by element. A component or resource whose type the
new generation no longer declares is removed, and the empty store of each old type is forgotten, its
static cache slot and release with it, so nothing of the world keeps the old generation loaded. A
state machine is the program's, on its own enum, which a reload leaves as it is. Deleting every
script swaps in no generation, dropping the last one's systems and what it declared. BevyCSharp's
`16c4c1e` writes each component as a scene does and reads it into the new type, through its schemas;
this engine has no schema for a script's plain fields, so the carry goes by the fields themselves,
by reflection on the scripts' assemblies alone, said at its place for the trimmer. A test spawns a
counter behavior from a script's startup, with an entity, an enum, an array, a list of the script's
own struct, a component of a type the second version drops and a resource of the script's own,
counts five frames, rewrites the script to count in tens with its enum's values in another order and
a field more on each type, and finds the counter at 15, the entity, the mood by name, the array and
the list carried, the dropped component gone, the resource's value kept and its new field as its
initializer left it, and the first generation collected. With the carry left out the test finds two
`Counter` types. The suite: 1,336 passed, 0 failed, 1 skipped.

Shared: a script compiled again keeps the game's state, here by fields where BevyCSharp goes by its
scene schemas, and the swap made between frames rather than on the compiler's thread, which
BevyCSharp's ScriptHost may check for, since a swap into a stage's live list of systems was the race
found here.

**Now 5, raylib's functions not carried.** Read by kind from `coverage.py`'s list, four have an
answer here that .NET's own library does not give, and are carried with a test and a line of the
cheatsheet each: `LoadRenderTextureEx`, a render texture of one format through the several-format
target of `692cefee`, `UnloadMaterial`, which unloads a material's five maps and its shader as
raylib's does where `UnloadModel` leaves them, `GetPixelDataSize`, raylib's bits a pixel with a
compressed format's block at least, and `ComputeCRC32`, whose .NET counterpart is in the
`System.IO.Hashing` package, so `PngWriter`'s table serves, held to CRC-32's check value of
`123456789`. `coverage.py` counts 506 of 619 carried, from 502. Each of the 113 left has its line in
`docs/compared-with-raylib.md`, checked name by name against `coverage.py`'s list: a table of
raylib's functions C# has, as raymath's has, with where each answers otherwise (a length in UTF-16
units, a hash as bytes, a parse that throws), the files, directories, hashes, compression, Base64,
memory, strings and code points, and the unloads the garbage collector does, and a table of those
left out with the reason, VR, automation events, which `./e3d`'s input stands for, the file
callbacks, which the asset server's `AddSource` stands for, the exports as C code, the shapes
texture, `LoadImageFromScreen`, `ImageMipmaps`, the cubemap, the pixel pointers, a font's glyph
data, the tangents and `UpdateSound`. TODO.md's entry says the count and leans on the page, and the
page's counts follow. The examples table is unchanged by them. The suite: 1,340 passed, 0 failed, 1
skipped.

**Now 6, TODO.md's order.** The first of its entries, per-entity work on the CPU, measured again
with RENDERING.md's own run of `models_stress` without arms (`./e3d command profile` once the search
ended): it held 410,266 entities, the count of 2026-10-04, in a frame of 19.9 ms, the example's loop
turning every entity 9.8 ms of it, `MeshEntityDraws` 5.9 ms, the first pass copying every instance
into the ring and finding its blocks' boxes 3.3 ms, and the model pass on the GPU 5.8 ms. Every
entity turns each frame there, so keeping a chunk unchanged saves nothing, and the copy goes only
when `MeshEntityDraws` writes into the renderer's mapped memory, across the two worlds, with its
blocks' boxes, for at most the 3.3 ms of a frame that holds 410,000 entities. That is not what
blocks a game, so the entry stays as it is and the entries after it are taken. The flat API's
entries are answered by item 5, and "Models are partial" reaches no program here, none building an
app without `InitWindow`.

The probe's entry is closed: `ReflectionProbe.Refresh`, which `SetReflectionProbeRefresh` sets,
captures a probe again that many seconds after each capture, so a door opening in its room or a
thing moving through it is seen. A refresh is one pass with the last capture bound, and leaves what
the probe is wanted as alone, so a probe ready for its placement stays ready, and a capture a
placement or a light needs goes before a refresh, the refresh of the probe captured longest ago
first, so one refreshed every frame keeps no other waiting. A test turns a wall's glow from red to
blue, finds a probe captured on a change still red after 30 frames, and red turned blue once it
refreshes, ready all the while. TODO.md's physics paragraph ended in a list of what is missing with
nothing in it, and ends without it. The 97 render and reference tests pass on lavapipe under the
validation layer, and the suite: 1,341 passed, 0 failed, 1 skipped.
