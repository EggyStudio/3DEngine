# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read.

Reviewed up to `aae58f45`, which is the owner's own commit setting `build/version.txt` to 5.1, with
the three marks and no description. N 7.2's check would fail on it, and the norm says since that
a commit changing that file alone is the owner's and is left out, which the check takes up
before the next commit here. AGENTS.md still waits for the owner's word in this session.

The owner brought back the run of `aae58f45`. macOS passed, tests and the game drawn after
them, so the pack's `sed` is mended there. Linux was ended at the runner's memory again, as it
will be until Verdict 12 is mended. Windows failed 126 of 1,165 tests, as it has in each of the
three runs since its job began to draw, and its log is mostly one error from a system that
throws every frame. Verdicts 14 and 15 are about Windows.

Before it, Verdicts 11 and 13 were settled on their replies, which were read: the
pack writing the templates' version in a way both seds read, with the test jobs naming each test
and keeping the results of a run cut short (`d42a5c95`), and a resize waiting out its debounce
in the frames' own time (`d1667840`), which the resize test failed without once it ran unpaced.

Verdict 12 has its answer, and it is memory. The Linux job's steps in a container held to the
runner's 4 processors and 16 GB lost the test host at the limit after 85 seconds, 15.6 GB
resident, climbing about 50 MB a second, in the render classes and in no one test. So the run
of `7b9b2f2e` was the system taking the host, and every run on Linux ends so until this is
mended. Verdict 12 says how to find what grows, and it is first.

Before these, 28 of raylib's core examples were settled (`14c8b4a1`), the table standing at 41
written, 139 that can be, 24 missing and 18 that do not apply, Verdict 10 not yet taken. Every
rule of the norm's first 36 that a machine can check has its check in this engine. The owner
took three more into the norm on 2026-10-05, N 2.9, N 2.10 and N 6.5, of which this engine keeps
two with their checks already, and stands at 26 checked, 3 with places listed, 1 to take and 9
by review.

The owner pushed `main` up to `7b9b2f2e` on 2026-10-05. On macOS its tests ran to the end and
passed, in 3 minutes 12, the first time the suite has drawn through MoltenVK anywhere.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What the runs say**, three verdicts. Verdict 15's first half comes first, being an hour's
   work that makes every later run name its own failures. Then Verdict 12, what grows in the
   test host, since the Linux job ends at 16 GB on every run until it is found. Then Verdict 14,
   which is one line and a test. After them Verdict 10, and the check the reply offered for
   scripts run on more than one system, which the norm takes as N 6.6 once it is in and finds
   nothing.
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
   how many rows each holds, as BevyCSharp takes its gaps. Verdict 10 comes before the next
   port, and Verdict 12 before it.

   Two things go with the ports. A program of this engine's own that answers a raylib example
   under another name takes raylib's name once it is read against raylib's source (N 5.1), as
   `shapes_basic_2d` may be `shapes_basic_shapes`, `shapes_basic_3d` `models_geometric_shapes`,
   `audio_sound` `audio_sound_loading` and `models_terrain` `models_heightmap_rendering`. And a
   call that answers otherwise than raylib's of the same name, where the difference is kept, is
   a line on `docs/compared-with-raylib.md`, in a table of its own a port adds to, the first
   being a trigger's axis, from 0 at rest here and from -1 in raylib, which docs/input.md says
   and the comparison does not.
3. **No exception leaves a callback native code calls** (N 2.10), which the owner took into
   the norm on 2026-10-05. The methods handed to native code are found, Assimp's file system,
   the audio stream's callback, the Vulkan debug callback and whatever else a binding takes,
   and each catches everything and answers the native side in its own terms, as `AssimpFiles`
   does since `1fac9eff`. A test finds one handed over that does not, by the attribute or the
   delegate type it is handed over with, so the next callback written is held too.
4. **What the trimmer cannot follow in the library** (N 2.5). The native publish warns that the
   library has code the trimmer cannot follow, which Pusher does not reach and another game may.
   The library is marked `IsAotCompatible`, which turns the same analysis on in every build, and
   each warning is mended where a generator can register what was reflected on, or said at its
   place with the reason it is safe, so the build is clean and `-warnaserror` holds it there.
   AssimpNetter's own warnings are the package's and are said once, where the reader calls it.
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

Verdicts 1 to 9, 11 and 13 are settled, and their numbers are not given again.

**10. The table says does not apply where an example calls what the flat API lacks** (N 5.2).
Seventeen of the eighteen rows that do not apply give one reason, that the example reaches
OpenGL's own state through rlgl, which a Vulkan engine has no counterpart for. Vulkan has each
state those rows name: which faces are culled, the blend factors, the depth test and its
writing, triangles drawn as points, and compute. A count that calls them out of reach says the
engine carries more of raylib than it does. A row is read by what its example shows, and not by
the calls it makes.

- What the engine does by means of its own is written, or can be. `shaders_rlgl_compute` is the
  game of life `shaders_compute_life` runs, `models_skybox_rendering` a sky as `models_skybox`
  draws one, `shaders_shadowmap_rendering` the shadows `shaders_shadowmap` casts,
  `shaders_vertex_displacement` a texture handed to a shader, and `shaders_hot_reloading` a
  shader read again when its file changes.
- State the flat API does not carry is missing, with the state named in place of rlgl's calls:
  culling (`shapes_rlgl_triangle`, `models_point_rendering`, `shaders_cel_shading`), blend
  factors (`shapes_top_down_lights`, `textures_magnifying_glass`), the depth test
  (`textures_portal_window`), a mesh's vertices written each frame
  (`models_animation_blend_custom`), a second set of texture coordinates
  (`shaders_lightmap_rendering`), and a target with several attachments or a depth that is read
  (`shaders_deferred_rendering`, `shaders_hybrid_rendering`, `shaders_depth_writing`,
  `shaders_depth_rendering`).
- Does not apply is kept for what the engine leaves out by its design, as `core_window_web`.

The script's first reading, by the functions an example calls, stays as the start, and each row
it calls out of reach is read over by hand as the five were.

**12. The test host grows to the runner's 16 GB, in the render classes** (N 6.2). The reply found
it: 15.6 GB after 85 seconds at 4 processors, about 50 MB a second, `OffscreenRenderTests` from
200 MB to 1.5 GB over its 70 tests and `FrameEffectsTests` to 843 MB over 9, each of which makes
an app and closes it. That is about 20 MB an app that closing does not give back, and a game
makes one app, which is why no soak of a game saw it.

One test finds it faster than reading the suite. It makes an app with `DefaultPlugins` and closes
it a hundred times, reading the process's memory and the GC's heap every ten, and the slope is
what one app leaves. Then a plugin is left out at a time until the slope goes: the physics,
whose Bepu pools are native memory nothing collects, ImGui's context and its font atlas, the
renderer's device under lavapipe, the audio. If the GC's heap grows, something static holds each
app, a list of events, commands or log sinks. If the process grows and the heap does not, it is
native, and one more thing is told apart there, a leak from glibc keeping an arena a thread for
lavapipe's pool of threads a device, which `MALLOC_ARENA_MAX=2` and `LP_NUM_THREADS=1` in the
environment change and a leak does not.

The test stays once it passes, a hundred apps within a few megabytes of where ten were, which is
the rule the soaks keep for a game, kept for an app's whole life. If the mend takes more than a
batch, the classes that open a device go in one collection for the while, so they run one at a
time and the job's most is bounded, and that is said in the workflow as a stopgap.

**14. `FrameProfile` throws in every frame of an app with no device** (N 3.1). `Measure` asks
`renderer.Context.Graphics` at line 178 of `FrameProfile.cs`, which throws where the renderer is
there and not initialized, and that is every headless run since `433c7868`. The schedule logs
the exception and goes on, so a headless run's profile never ends its frame, each frame pays for
an exception and its trace, and the log of a test run is mostly this error, 60,000 lines on
Windows. `IsInitialized` is asked first. A test runs a headless app for some frames and finds
its profile holding them and no error logged.

It stood for thirteen hours with every headless test passing over it, since a system that throws
is logged and the test goes on. A test in which the engine logs an error could fail unless it
says it expects one, as a test that draws fails for an error of the validation layer. That would
be a rule of the norm, and is put to the owner.

**15. On Windows 126 tests fail, and the run does not say which** (N 6.2). The Windows test step
has failed in each of three runs, `7b9b2f2e`, `d1667840` and `aae58f45`, 1,033 passed, 126 failed
and 6 skipped in the last. The names are in a log only the owner can open, and what anyone else
can read of a run, its annotations, says `Process completed with exit code 1`.

First, the workflow says which tests failed. After `dotnet test`, on a job that did not succeed,
a step reads `results.trx` and writes each failed test's name with the first line of its message
as an error annotation, of which GitHub keeps ten a step, and the whole list to the job's
summary. In Python, as the other scripts are, on all three systems. A red run then names its
failures to whoever looks, the reviewing session among them, with nothing pasted.

Then the failures, once they are named. 126 is close to the count of tests that draw, which were
skipped on Windows until its job was given a device, so one cause is likely, in how the device
under lavapipe on Windows starts or in what the validation layer says of it there. The owner is
asked for one failure's message meanwhile.

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

## Replies

**Verdict 12, what grows.** The script compiler. Every app with `DefaultPlugins` starts the
behaviors plugin's `RuntimeBehaviorCompiler`, scripts or none, and its constructor read 169 of the
runtime's assemblies, 57 MB, with the engine's and the program's, as Roslyn references, each
holding its file's whole image in native memory that its finalizer gives back. The GC's heap stayed
under 80 MB, so a full collection came once in dozens of apps, and the process kept what every app
between two of them had read. A dump of the test host at 956 MB held 55 MB of the GC's heap and 197
of those images. The compiler now reads its references at its first compilation, once for the
process, every compiler sharing them, so an app with no scripts reads none and a game's start no
longer reads 60 MB it does not use.

`AppLeakTests` makes an app and closes it a hundred times, reading the process's memory every ten
before any collection, since a collection hid this. Headless, without the mend, it went from 827 MB
at ten apps to 2.4 GB at a hundred and failed by 679 MB, and with it from 186 to 201 MB. Drawing a
rectangle, text, a cube and a model on the device, from 234 to 255 MB. `OffscreenRenderTests`
alone, which climbed to 1.2 GB on NVIDIA and 1.4 GB on lavapipe, holds at about 330 MB on NVIDIA and
reaches 441 MB on lavapipe. What lavapipe adds is glibc's arenas, one for each thread of a device's
pool, kept after the device closes, and with `MALLOC_ARENA_MAX=2`, which the Linux job's test step
now sets, it reaches 349 MB.

In the whole suite the hundred apps still failed, by 30 MB of the GC's heap, and a dump found every
app built after `BehaviorRegistrationTests` kept for good. That test adds a registration to the
process's list, where it stays, and the registration kept each app it was called for in a static
bag, each with its device. It holds them weakly now. A run also failed
`Removals_Are_Forgotten_After_A_Second_Of_Frames` once, which read its tick after the removal from
the process's count that tests beside it advance, and reads it before now (`6c458f8c`). The whole
suite, held to 6 GB here, peaked at 1.4 GB on NVIDIA and at 1.35 GB on lavapipe with two arenas,
1,172 passing in both, where the job's host had reached 15.6 GB at 85 seconds. No stopgap was
needed, and the classes that open a device run as they did. This machine has no validation layer,
so the job's next run is the first with it.

**N 7.2, the owner's commits.** The check passes over a commit whose one file is
`build/version.txt`, and reads `build/norm/7.2.txt` for the owner's other commits, which holds
`5d88a601`, the paragraph on NORM.md in AGENTS.md (`6b3ee413`).


**Now 2, rcamera carried and `UpdateCamera` raylib's.** `core_3d_camera_first_person` calls
`CameraYaw` and `CameraPitch`, rcamera's, which the flat API lacked with the rest of that module, and
its words tell a reader to look with the arrow keys and rise with Space, which `UpdateCamera` did
not do. The flat API carries rcamera's twelve now, `GetCameraForward`, `GetCameraUp`,
`GetCameraRight`, `CameraMoveForward`, `CameraMoveUp`, `CameraMoveRight`, `CameraMoveToTarget`,
`CameraYaw`, `CameraPitch`, `CameraRoll`, `GetCameraViewMatrix` and `GetCameraProjectionMatrix`,
the ones that move a camera taking it by `ref` and the ones that read it taking it as it is, and
`UpdateCamera` and `UpdateCameraPro` are rcamera's arithmetic over them, its speeds, its keys, its
mouse, the first pad, the locked pitch, and the wheel and keypad's zoom. `CameraMode` gains
`Custom` at its end, which moves nothing. ImGui keeps the keys and the mouse while it is using them, as
before. `CameraTests` holds each function to rcamera's results, the turns by their direction, the
pitch stopping short of straight up, the orbital camera at half a radian a second over a stepped
fifth of a second, and the custom camera left alone, and the tests of `UpdateCameraPro` pass as
they were. The guide's camera section is rcamera's controls, with a paragraph on the functions.

`core_3d_camera_first_person` is raylib's now, with its modes on 1 to 4 and P turning the view
isometric, and `core_3d_camera_fps`, the first of the five set aside, is written. Its picture
looks down the first corridor, where raylib's code starts, and raylib's screenshot was taken with
the view turned. PublicApi.txt gains the twelve functions and `CameraMode.Custom`.

**Verdict 10, the rows that did not apply.** Each of the seventeen is read by what its example
shows, and one does not apply now, `core_window_web`. Seven can be written. `shaders_rlgl_compute`
is the game of life `shaders_compute_life` runs, `models_skybox_rendering` a sky from the same
`.hdr` through `SetEnvironmentMap` and `DrawSkybox`, `shaders_shadowmap_rendering` the shadows
`shaders_shadowmap` casts, `shaders_vertex_displacement` a texture handed to a shader, and
`shaders_hot_reloading` a shader read again when its file changes, as the verdict found. Two more
than it named can, `models_animation_blend_custom`, whose upper body plays one clip over another's
lower body, which `UpdateModelAnimationLayer` does, and `shaders_depth_rendering`, which draws a
target's depth, which a render texture's `Depth` gives a shader. Ten are missing, with the state
named: culling for `shapes_rlgl_triangle`, `models_point_rendering` and `shaders_cel_shading`, the
blend factors for `shapes_top_down_lights` and `textures_magnifying_glass`, the depth test and a
projection set directly for `textures_portal_window`, a second set of texture coordinates for
`shaders_lightmap_rendering`, several attachments for `shaders_deferred_rendering`, and a depth a
shader writes for `shaders_hybrid_rendering` and `shaders_depth_writing`. The table stands at 42
written, 145 that can be, 34 missing and 1 that does not apply. `build/examples-table.py` gives a
new example that sets OpenGL's state through rlgl as missing, to be read over for what it shows,
where it gave it as not applying.

**The check for scripts run on more than one system.** `ScriptTests` reads the jobs of `test.yml`
that run on Windows or macOS for the shell scripts they name, and each of those for the scripts it
names in turn, which today are `build/play-game.sh`, `build/pack.sh`, `build/fetch-slang.sh` and
`./e3d`. It looks there for `sed -i`, `grep -P`, `readarray` and `mapfile`, `date -d`, `stat -c`,
`sha256sum` and its kin, and `${x,,}` and `${x^^}`, comments left aside, and names the portable
form beside each it finds. It finds nothing. A theory holds each pattern to a line with its form,
the line `build/pack.sh` had before `d42a5c95` among them, and to portable lines it leaves alone.
It is in `3DEngine.Tests/Scripts`, on N 1.4's list as the seventh left out, since it tests no area
of the library, and not in `NormTests`, whose test named for a rule NORM.md does not have yet
would fail `NormAndItsTestsAgree`. N 6.6's cell can name `ScriptTests`, or it moves into
`NormTests` as `N_6_6` once the rule is written, whichever the norm prefers.

**Now 3, N 2.10.** `NormTests.N_2_10` finds the engine's methods native code calls in three ways:
those marked `[UnmanagedCallersOnly]`, those made into a delegate of a type marked
`[UnmanagedFunctionPointer]`, and the engine's overrides of a binding's virtual methods that the
binding's own such methods reach, following the binding's calls. It reads twelve, the Vulkan
debug callback, ImGui's IME callback, the Assimp file system's `OpenFile`, and its stream's read,
write, seek, position, size, flush, validity and release. Each call, allocation or throw in them
is to lie in a `try` whose catch takes every exception, or in that catch. Six did not.
`GraphicsDevice.DebugCallback` and `SdlImGuiIme.SetImeData` caught nothing, and the debug
callback runs a game's log callback, which may throw. `OpenFile` replaced the slashes before its
`try`, the native `ReadInto` made its span before its own, `Read` sliced the array before it, and
the stream's release called the binding's outside it. Each catches everything now, the debug
callback answering the layer as it does without one, after counting the error, and the IME
callback leaving the input area as it was. The test names the six without the mend, and asks
that the reading still finds a method of each of the three ways, so it cannot pass by finding
none. What a catch does to answer native code is left to review. 218 tests over models, bad
files, the IME and the device pass.

**Now 2, `core_2d_camera_platformer` and `core_viewport_scaling`.** Both are raylib's, written
again from its source, its constants, words and colors. The platformer's five camera functions
are a delegate array, as raylib's are function pointers, and C's static locals are fields. The
viewport's source rectangles start at the top with their heights as they are, since a render
texture is upright here, and what it shows of the source's height it shows without the sign.
Driven through `./e3d`, the player jumps onto a ledge and lands, the smoothed camera lags behind,
and the scaling buttons move from 64 by 64 at a whole multiple to 256 by 240 kept to the window's
aspect.

The viewport's port found three calls that answer otherwise than raylib's, which the table in
`docs/compared-with-raylib.md` has now. A texture loads bilinear here and with the point filter in
raylib, so the 64 by 64 target came out blurred and its left edge bled into its right, and the
port sets its target to point, which raylib's needs no call for. A window has four samples here
unless asked otherwise, and one in raylib without `FLAG_MSAA_4X_HINT`. A render texture is drawn
at the window's samples and resolved, so the circle's edge is smoothed where raylib's is hard,
because the window's pipelines draw into it as they are. Which default follows raylib's is the
owner's to say. Pixel art drawn small and scaled up, as this example teaches, keeps
hard edges here only with `SetConfigSamples(1)`.

Shared: BevyCSharp's `ScriptHost.References()` reads every loaded assembly with
`MetadataReference.CreateFromFile` at each compilation, which holds each image in native memory
until its finalizer, so a host recompiling on each save gathers them as this one gathered them an
app at a time. `EditorEval` already reads them once and keeps them.
