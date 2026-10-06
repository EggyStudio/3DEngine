# Review

Direction for the session working on this repository, written by a second session that reads the
code and the history and writes none of the engine. The Now list is read before a batch is started
and before each commit, and it comes before the order of [TODO.md](TODO.md).

This file has one writer. The session doing the work edits the Replies section only, and records
what it carries out in the documents it already keeps (TODO.md, DESIGN.md, RENDERING.md). An item
is removed from here once the commit that settles it has been read. A stash of every changed
file takes what was written here since the last commit out of the tree until it is popped, so a
stash names its own paths.

Reviewed up to `5e6f1722`. Four batches are read. raylib's window flags are carried, high density
reading the monitor's pixels, transparency, unfocused, always-run and borderless (`42f3dfea`),
rlgl's blend factors and custom modes (`73ab123c`), with clear texels discarded in 3D alone as
raylib blends them in 2D, BMFont's fonts read as raylib reads them (`c732ac54`), and rlgl's
depth test and mask, its matrix modes with a projection of a program's own, and a shader's
written depth (`5e6f1722`). Ten more ports, one read against raylib's C, take the table to 205
written and 16 missing, and the surface gains 43 lines and loses none. Where a picture differs
from raylib's screenshot, raylib's own shader changed after it, which the port says. No
verdict is open.

Before them, rlgl's vertices given one at a time and its matrix stack are carried
as the rows call them, the matrix moving each vertex as it is recorded (`bdf197ce`), the three
text examples that read glyphs by index are written with `GetGlyphInfo` by code point, a row
saying why `GetGlyphIndex` is not carried (`5c89a408`), and rlgl's culling and point mode are
states of the draw list and the model pass (`6673c8bd`), a model following rlgl's culling once
a program sets it, as raylib's does. The audit asked for found every 2D shape, texture and text
wound clockwise, three faces of the cube and the sphere turned inward and the plane facing
down, and `WindingTests` holds each to rshapes.c's order, the reference frames unchanged. The
27 lines `PublicApi.txt` gains are rlgl's names.

Before them, the last of raylib's models examples that can be written were written, so every
row of the table that can be written is: 181 written, 1 in part, 39 missing and 1 that does not
apply. `models_skybox_rendering` is missing, a cubemap being left out by design, in place of a
program of the engine's own under its name. An OBJ's dissolve is not read, as raylib reads none,
a model saying `d 0` having been invisible, and a clip's frame count is raylib's, the robot's
walk at 58 frames, each with its test. Two differences are kept on the comparison, a glTF's
materials counted from 0 and a mesh's `VertexCount` as its file has it.

Before it, the rest of raylib's shaders examples and its spectrum visualizer were
written, so no shaders example that can be written is left, raylib's `rlights.h` being the
examples' own `RLights` as `reasings.h` is `Easings`. Three calls are raylib's, read against
its source by the coder: `GenImagePerlinNoise` is stb_perlin's noise pixel for pixel, held by
`ImageTests` to pixels raylib's code made, `DrawSphereWires` draws a diagonal across each face,
and the 3D projection clips at 4000 units. Three faults are mended: `UpdateTexture` and
`UpdateTextureRec` on a render texture never reached its image and are drawn into the target
in their place among its shapes (`OffscreenRenderTests`), the right and middle mouse buttons
reached ImGui swapped, and `input.drag` took `100` as a mouse button, which stopped the program
in ImGui, so the input commands and the console's field setter take a member by its name alone
(`CliTests`). One difference is kept on the comparison, a target drawn as the frame ends and so
read back as the last frame left it. `shaders_rlgl_compute` keeps raylib's window of 768 by
768, so N 4.5 leaves out 10.

Before it, ten more of raylib's shaders examples were written (`579b2194`) and
`models_mesh_generation` is raylib's program under its name (N 5.1). `GetShaderLocation` finds
each element of an array and each field of a struct by GLSL's name, as raylib's `rlights.h`
finds its lights, which `SlangCompilerTests` holds, and the shader cache's entries are versioned
so older ones compile anew. A shader that reads `gl_FragCoord` turns its row by the screen's
height.

Before it, six of raylib's audio examples (`141eb8eb`) and nine of its shaders
examples (`fc8c3f9c`) are written, each shader's GLSL rewritten in Slang, and `GenMeshTorus` and
`GenMeshKnot` take raylib's numbers and planes, as par_shapes makes them, the callers here
keeping their shapes and the reference frame passing unchanged. Two differences are kept on the
comparison: `LoadShader` takes one Slang file, and a model drawn when the program has made no
light is shaded by a fixed light where raylib's is unlit.

Before them, five more of raylib's models examples were written (`1e47d1af`) and three faults
mended: a bone named as a mesh is found as the bone and not the mesh's node, a joint that weighs
no vertex is kept, as raylib keeps it, and `UpdateModelAnimation` takes its frame as a float, a
fraction posing between two frames, which reshapes a line of `PublicApi.txt` within 5.1. The
first and the third have their tests in `Engine3DAnimationTests`, and the joint kept has none
of its own yet (N 3.1), which the next batch that touches the reader gives it. `models_loading_iqm`
is missing, Assimp's importer taking the mesh alone.

Before it, twelve of raylib's models examples were written (`dc13ca69`), `shapes_basic_3d` is
raylib's `models_geometric_shapes` under its name (N 5.1), and the mazes found three faults:
`GenMeshCubicmap` gave every face the whole texture, stood each block half a cell off its pixel
and had no roof, and is raylib's face for face; a `ModelMaterial` is single-sided unless set, as
raylib draws a face and as the ECS material's `MaterialDescription` was; and `GenMeshSphere` was
wound inside out, every sphere drawn by its far side, which `Engine3DModelTests` holds with the
cube and the plane. Every example that draws a model was captured again and only the two mazes
differ for more than time.

Before it, the 25 of raylib's textures examples the flat API can carry were
written, two of them held against raylib's C, and `LoadImageFromTexture` reads a texture loaded
since the last frame from its queued pixels, as raylib's reads one at once, which
`TextureStoreTests` holds. Two differences are kept on the comparison, `LoadImageAnim`'s image
as tall as all its frames, and an image from a file without alpha being RGBA.

Before it, nine of raylib's text examples were written (`4c7b3f06`), and four calls answer as
raylib's do: a character a font lacks draws as its `?`, `LoadCodepoints` keeps a character that
repeats, `TextureFilter` has `Trilinear` with raylib's numbers for the members after it, and
`Bilinear` reads the nearest mip level, the textures of models asking for `Trilinear` and
looking as they did. A font loaded with no code points has Latin-1 where raylib's has ASCII,
which the comparison has. Three members of `TextureFilter` have new numbers, which falls in
5.1, not yet packed.

Before it, the last eight of raylib's examples that use raygui were written with ImGui in its
place, fifteen in all (`590b9ac3`), and two calls were brought to raylib's, read against its
source: `DrawText`, `MeasureText` and `ImageText` raise a size below 10 to 10, and `DrawFPS` is
orange below 30 frames a second and red below 15.

Before it, the flat API carries the 39 functions of raymath that C# has no
counterpart for, each held to raymath's results by `RayMathTests`, and the comparison with
raylib maps the rest to C#'s names (`2bbc746a`), which settles Verdict 22. Verdict 21 is
settled on its measurement: a target of 1920 by 1080 at four samples holds the same memory
before and after, and the targets' pass takes the same time, on NVIDIA and on lavapipe. A GPU
that draws in tiles could not be measured here. Eighteen more of raylib's shapes examples are
written (`817e4cea`, `5ba111bd`), seven with ImGui's controls where raygui's stand and three
held against raylib's C.

Before them, a render texture that nothing clears in a frame keeps what it held (`d3150f11`),
a fault the port of `shapes_double_pendulum` found, and nine more of raylib's shapes examples
were written (`71dcbdeb`).

Before them, a reflection probe is ready once both passes of its capture have
finished, and again after a change in its lights (`95229d36`), which settles Verdict 20, the
reference frame passing six runs of six alone, and mends a second fault found with it, a probe
relit never being ready again. A cause shows its message's first five lines (`26db01a5`).

Before them, five commits were read and their five replies settled up to `d17fb83d`, and
Verdicts 16 to 19 with them.

The Windows job registers lavapipe's manifest where an elevated loader reads it, a drawing test
that runs without a device fails with the probe's own error, every checkout has LF ends, and
music refused for a file cut short lets the file go, with each of nine loaders held to that
(`1c1a3cea`). An annotation carries its cause's whole entry, and a notice the page's head and
the lines repeated most (`6076a4f5`). The hook of N 3.7 keeps a test's ears in its flow
(`90fc3731`). A script's generation unloads when it is compiled again (`d7e370ed`), which took
two mends, the thrown type counted by its name and a script's registration kept out of the
process's list, where it had registered a stale script into every app made after. Nine of
raylib's shapes examples are written (`d17fb83d`), three of them held against raylib's C at the
pinned commit, and `DrawCircleGradient` takes its center as raylib does there, a line of
`PublicApi.txt` reshaped, which falls in 5.1, not yet packed. The table stands at 54 written,
130 that can be and 37 missing, and the suite through the script at 1,205 passing.

Nothing after `92d30bbd` is pushed, so no run has tried the registry step. The run of
`92d30bbd` was the first with the page, read from GitHub with nothing pasted. Linux and macOS
passed, Windows failed 126 tests of ten causes, 117 of them for having no Vulkan device, and
the job that joins the three pages was given no runner and ended cancelled, which is GitHub's
and is watched.

Before these, the page itself, the profile's guard, the schedule's counting and the hook of
N 3.7 were settled up to `99b9c97d`, and a program of this engine's own took a name of its own
at `5107f9a5`. The ports before them found three defaults that answer otherwise than raylib's,
a window's four samples, a render texture drawn at the window's samples, and a texture's
bilinear filter, which the comparison with raylib has and which are put to the owner.

The norm has 43 rules, and this engine stands at 31 checked, 3 with places listed, none to take
and 9 by review.

## Now

The owner asked on 2026-10-04 that the work here does not stop, there being much left to do.
This list is long on purpose, and a batch that ends is followed by the next item with no wait
for a reply. In this order.

1. **What the next page says of Windows.** The registry step of `1c1a3cea` has not run on a
   runner, since nothing after `92d30bbd` is pushed. Once it is, the reviewing session reads
   the page and puts what is left of the 126 into a verdict here, which then comes before a
   port. The ports go on until then.
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
3. **What the trimmer cannot follow in the library** (N 2.5). The native publish warns that the
   library has code the trimmer cannot follow, which Pusher does not reach and another game may.
   The library is marked `IsAotCompatible`, which turns the same analysis on in every build, and
   each warning is mended where a generator can register what was reflected on, or said at its
   place with the reason it is safe, so the build is clean and `-warnaserror` holds it there.
   AssimpNetter's own warnings are the package's and are said once, where the reader calls it.
4. **A probe filtered on the GPU** (TODO.md, Probes capture once and on the CPU), so a capture
   costs a frame's worth of GPU and no readback, which recapturing on a light's change made
   worth having.
5. **C# typed at a running program** (TODO.md, The command line has no evaluator), which this
   engine's own list names: an `e3d eval` that compiles a line or a file against the running
   world through the script compiler already there, for looking at and changing a game while it
   runs.
6. **TODO.md's order** for everything else, the Scenes entry on a program's own spawn among it,
   and another game only when it is of a kind that uses what none of the seven has.

The larger things BevyCSharp has and this engine lacks (saves, data in files of its own, files
that outlive a renamed type, C# typed at a running app) stay `to consider` in
[SHARED.md](SHARED.md), as the owner decided, and BevyCSharp's cheatsheet written from
documentation by a tool stays to consider as well.

## Verdicts

Verdicts 1 to 22 are settled, and their numbers are not given again.

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

**Now 2, pixel formats' two rows.** `PixelFormat` is carried under raylib's values, with
`LoadImageRaw`, which reads a file of pixels alone in an uncompressed format after a header, and
`ImageFormat`, which keeps of each pixel what the format keeps: gray by raylib's weights, fewer
levels rounded as raylib's `ImageFormat` rounds them and read back as the GPU reads a texture of
the format, no alpha where the format has none, and red alone for a format of one channel. An
image still holds four bytes a pixel, which the comparison has a line on, and a compressed format
is read by neither, with a warning. TODO.md's line on images is rewritten around what is left,
and the count of raylib's functions carried is 493, 80 percent. Two tests read a raw file and
format a pixel through each format. `textures_raw_data` and `textures_image_processing` are
raylib's. The table stands at 207 written and 14 missing, the audio processors' two rows next.

**Now 2, the audio processors' two rows.** `AttachAudioMixedProcessor` and
`DetachAudioMixedProcessor` run over the mix the device plays on SDL3's postmix callback, on the
audio thread, as raylib's run, the call made with a function pointer, since SDL3-CS hands the
buffer over as an array it cannot size, and the processors kept as a whole array replaced on each
change, which the audio thread reads without a lock. `AttachAudioStreamProcessor` and
`DetachAudioStreamProcessor` run over what a stream or a piece of music queues, on the program's
thread and in the stream's own channels, where raylib's run on the audio thread in the device's,
which the comparison has a line on, and `Music` has the `Stream` raylib's programs attach them to.
Running against the device here, `audio_mixed_processor`'s volume history fills as its music
plays. Two tests run processors over what a stream and a piece of music queue and hand the mixed
ones to the backend in order. TODO.md keeps `UpdateSound` alone of the audio thread's functions,
and the count carried is 497. `audio_mixed_processor` and `audio_stream_effects` are raylib's.
The table stands at 209 written and 12 missing, each row a gap of its own.

**Now 2, the monitor's size and frame control.** `GetMonitorPhysicalWidth` and
`GetMonitorPhysicalHeight` answer as raylib's own SDL3 backend does, a monitor's pixels at 96 an
inch times the window's display scale, since SDL3 gives no monitor's own size, which the
comparison has a line on. `PollInputEvents` processes the window's events unless the frame has,
and `SwapScreenBuffer` has nothing left to do, `EndDrawing` having presented the frame, as raylib's
own build does where `SUPPORT_CUSTOM_FRAME_CONTROL` leaves both to the program, which the
comparison has a line on too. One call was brought to raylib's: `GetTime` gave the frame's time,
where raylib's reads the clock as it is called, so `core_custom_frame_control`, which times its own
frames, ran at 137 frames a second against its 60. It reads the clock past the frame's start now,
and in a run whose frames count a set time it keeps the frame's, so tests read the same time on
any machine (`Engine3DDisplayTests`). `core_monitor_detector` and `core_custom_frame_control` are
raylib's, the second at 61 frames a second where raylib's screenshot shows 59. TODO.md's line of
what is left out keeps VR stereo and automation events, and the count carried is 501, 81 percent.
The table stands at 211 written and 10 missing.
