# Building from source

How to build the engine, run its tests and run the examples.

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), a Vulkan 1.3 driver, and `slangc`,
which `build/fetch-slang.sh` downloads. The SDL3,
Assimp and Dear ImGui native libraries arrive with their NuGet packages, so nothing else is
installed by hand. macOS draws through MoltenVK, which the Vulkan SDK provides.

```bash
build/fetch-slang.sh                       # slangc, into build/tools/slang (once)
dotnet build 3DEngine.slnx                 # the engine, the generator, the tests and the examples
dotnet test 3DEngine.Tests                 # the suite
python3 build/test.py                      # the suite as the workflow runs it, ending in a page of what failed
dotnet run --project 3DEngine.Examples     # a window
```

The suite needs no GPU or display, because the tests that touch rendering use
`NullGraphicsDevice`. The examples need both. Without `slangc` the shader tests report as skipped,
and so do the render tests without a Vulkan device, each with its reason in the run's output.

`build/test.py` runs the suite as one process held to 40 minutes and 4 GB, which the Windows job
raises to 60 minutes (`--timeout-minutes 60`), since lavapipe compiles the shaders each app draws
with unless Mesa's cache holds them, 3.7 seconds an app where 0.84, and a cache left unsaved by a
failed job doubled the suite there from 22 minutes past 40. The build workflow restores that cache
before the suite and saves it after, whether the suite passed or not, keyed by the processor's model
and a checksum of its flags (`build/processor.sh`), since lavapipe compiles for the instructions of
the processor it runs on, and a runner of one kind that restored code another kind made died on an
instruction its processor lacks. The script writes what the suite prints to
`3DEngine.Tests/TestResults/output.txt` and ends with a page of at most 200 lines, also written to
`TestResults/digest.md`, which holds the counts, the failures by cause, the most frequent first,
each with its message, its first frames and some of its tests, and the lines the output repeated
most. A process that is lost, by a crash, a hang, its time or its memory, is said first on the page,
with the tests it was in and its last lines, and the suite runs again in parts, a process for each
area of thirty tests or more and one for the rest, so a part that is lost costs only its own tests.
`python3 build/test.py Rendering` runs one part, and `--parts` all of them.

On an NVIDIA GPU the script gives the suite a cache of the driver's own compiled pipelines in
`build/nvidia-cache`, of 2 GiB, through `__GL_SHADER_DISK_CACHE_PATH` and
`__GL_SHADER_DISK_CACHE_SIZE`, where either is not already set, and makes the folder, since the
driver makes none. The driver keeps its cache in the user's folder at 128 MiB, and a cache at that
size keeps no pipeline new to it, so each app the suite makes compiled each of its pipelines again.
On the development machine the suite took 12.6 to 13 minutes so, 7.2 with a cache of its own, and
26 with none. Two runs of four with the cache at its size, and one with a folder the driver could
not write in, failed a test each with `VK_ERROR_UNKNOWN` from a pipeline's creation, which fourteen
runs with a cache of its own or none did not. `dotnet test` run directly takes the user's.

```
build/               fetch-slang.sh, and the compiler it downloads under tools/
3DEngine/            the engine library, one folder per area
3DEngine.Generator/  the Roslyn source generators for behaviors and console commands
3DEngine.Cli/        the e3d command line, which ./e3d builds and runs
3DEngine.Tests/      xUnit tests, in folders matching the engine's
3DEngine.Examples/   programs that use the engine
.github/             the documents, and the images the README shows
```

## Running without a window

Every program built on the engine reads these flags, or the variables beside them.

| flag | variable | effect |
|---|---|---|
| `--serve` | `E3D_SERVE=1` | answers `./e3d` on a local socket |
| `--hidden` | `E3D_HIDDEN=1` | renders into a window that is never shown, so captures work and nothing appears |
| `--offscreen` | `E3D_OFFSCREEN=1` | no window and no display needed, rendering into images of the device's own, so captures work on CI and over SSH |
| `--headless` | `E3D_HEADLESS=1` | no window and no renderer, frames paced at 60 per second |
| `--frames N` | `E3D_FRAMES=N` | closes after N frames |
| `--frame-time S` | `E3D_FRAME_TIME=S` | each frame advances time by S seconds and reads no clock, so a run steps alike on every machine |
| `--samples N` | `E3D_SAMPLES=N` | a window is drawn with N samples a pixel where the program asks for none |
| `--seed N` | `E3D_SEED=N` | the random generator is seeded with N as the window opens, in place of the clock |

`./e3d open <example>` starts an example with `--serve` and any of the others, and [E3D.md](E3D.md)
covers controlling it.

`build/capture-example.sh <example> <png|webp> [--hidden|--offscreen]` captures an example as the
README shows it. It gives the examples that wait for input their input (a gamepad, Enter, typed
text, taps and swipes), runs it until its scene has settled and its frame rate is measured, and
captures it. `build/capture-examples.sh <folder> <webp|png> [example...]` captures every example
with it, or those named, checking each to draw with no error from the validation layer and naming
any that did not in an error annotation. An example whose capture takes past `CAPTURE_SECONDS`, 300
unless set, is stopped and fails alone, `CAPTURE_MINUTES` ends the run where it is set, naming the
last example reached, and the script ends with a notice of how many drew and in how long, each
example's seconds written to `times.tsv` beside the pictures. The pack run runs it on Linux for the
gallery, in the build workflow's `captures` job, and on macOS in `captures-macos` for PNGs kept as
that job's artifact, and a new or changed example's capture in `.github/assets/examples` is taken
with `capture-example.sh`.
The gallery's captures are WebP at the size their window is drawn, raylib's 800 by 450 for an
example, which `build/webp.sh` encodes at quality 85 for a lit 3D scene and losslessly for flat
color, 2D shapes or text, with ImageMagick or with `cwebp` from the `webp` package. A capture of
another shape is filled to that size and cut, by `cwebp` itself where it is installed, the box of
the size's shape cut from the capture's own pixels and scaled, so a runner needs the encoder alone,
and by ImageMagick where it is not. The render tests' references stay PNG, since they compare
pixels.

`build/soak.sh <game> <program> <seconds>` plays one of the games through `./e3d` as a player left
at it would, restarting its level and spawning and clearing what it spawns, and reads
`memory.collect` every ten seconds into `build/soak/<game>.csv`, on a clock of its own beside the
play, so a slow device whose turns take minutes still gives a reading every ten seconds, and draws
the game at 320 by 180, which lets a device drawing on its CPU play more of it.
`build/soak-check.py` fails when anything it holds (the managed heap, the GPU's buffers, images,
descriptor sets, pipelines and memory, the entities and their ids) rises at its least in the second
half of the run past its least in the first by more than a little, as a leak does and a level
streamed in and let go does not, or a game could not be played through, naming the game, what
climbed with its numbers or the command that ended its soak, and the process's resident memory. A
game that streams its level is held instead to the most a whole walk through it holds, since a slow
device's two minutes see part of the level in each half and its least climbs with the world, as
Manor's buffers do, a vertex and an index buffer for each mesh its cells show, 158 at most on the
RTX 4070 and 162 on lavapipe, and its images, descriptor sets, entity ids and assets. Swarm's entity
ids are held to the most a turn holds with every frame counting the quarter second the clock holds a
slow device's frame to, since a turn there reaches later waves. The pack run plays every game for
two minutes at once this way, and a push Slide alone, and a ten-minute run of each holds level on
the desktop.

`build/examples-table.py` writes `.github/EXAMPLES.md`, a row for each of raylib's examples, from
the `examples_list.txt` of the raylib `build/raylib-bench/run.sh` pins, fetched once under
`build/raylib-bench/work`. An example is written when `3DEngine.Examples/Program.cs` opens it by
raylib's name, and otherwise its state is its line in `3DEngine.Examples/triage.tsv`, which
`--triage` starts for a new one from the functions it calls that the flat API lacks. It also writes
the README's gallery, a Markdown table a folder of the examples with the games last, each picture
the capture made small to 260 by 146 into `.github/assets/gallery` by ImageMagick or `cwebp`, with
the SHA-256 of the capture it was made from in `captures.tsv` beside them, since nuget.org prints an
HTML table as its text and draws a Markdown picture at its own size, and the line under the Examples
heading that counts them. A new or changed capture needs the script run again. `--check` fails where
the table, the gallery or its count is out of date, or a picture is missing, made from a capture
taken again since, or shown by no example or game, which the examples job runs on every push.

`build/raylib-bench/compare.py <group, example or all>` builds raylib's own program of each written
example, as `run.sh` builds raylib, with SDL3's headers and the engine package's SDL3 library,
linked under the name a program loads it by (`libSDL3.so.0`, which the package does not bring), runs
it and the example here to the same frame, and writes the share of pixels apart into
`3DEngine.Examples/measured.tsv`, which the table shows. raylib's programs draw through SDL's
offscreen driver with OpenGL, which Mesa gives on a machine with no GPU (`libegl1`, `libegl-mesa0`
and `libgl1-mesa-dri` on Ubuntu), and the pictures are compared with Pillow (`python3-pil`). The
pack run's captures job measures every pair with `--against 3DEngine.Examples/measured-ci.tsv`, the
shares its own device recorded, and fails where one stands more than a point above its share,
leaving out a pair whose line in `triage.tsv` marks it as moving by the clock or the device. A pair
it measures for the first time is listed in the run's summary, to be recorded in that file. A run
where raylib's programs drew no frame for any pair records nothing and fails, saying why the first
drew none.

`build/storm.sh <program> <png>` resizes a program a frame apart through odd sizes, minimizes and
restores it and moves it to each monitor there is, under the validation layer, then captures a frame
and fails unless it is drawn at the size last asked for with nothing reported. The pack run puts
each game and five examples through one.

`--offscreen` needs a Vulkan device and nothing else. Mesa's lavapipe, which runs on the CPU, is
one (`mesa-vulkan-drivers` on Debian and Ubuntu), and CI renders with it. The render tests in
`3DEngine.Tests/Rendering/OffscreenRenderTests.cs` draw this way and are skipped where there is
no device.

### Timing a program

`e3d command profile` gives where a frame's time goes, averaged over about a second, and
`e3d command profile.slowest` the slowest frame since it was last asked with every stage, system
and render phase measured in it, among them `render.fence` (the GPU finishing the frame that last
used the slot), `render.acquire` (waiting for an image to draw into) and `render.present`. A
program is timed with `--offscreen`, which presents nothing, since a window's frames are paced by
the desktop as well as the program.

On a Wayland desktop with NVIDIA's driver (615, under GNOME 50) a window's frames were held back
for 0.5 to 2 seconds at a time, in some runs every second or two and in others once in fifteen
seconds, in `render.present` and after a swapchain was made again in `render.acquire`, shown or
hidden, focused and not covered, in every present mode, while the engine's own work in those
frames took a few milliseconds. `vkcube` on the same desktop showed
it in one run of two. The same programs through XWayland (`SDL_VIDEO_DRIVER=x11`) and offscreen
show none of it, a walk through `games/Manor`'s estate having no frame over 45 ms either way. It is
the desktop's doing, and the engine says so in its log once it has seen three such waits in ten
seconds, naming `SDL_VIDEO_DRIVER=x11` as what avoids it.

## The package

```bash
build/pack.sh        # Release build, `e3d shaders` into build/shader-cache, then dotnet pack
```

The package lands in `build/package` and carries:

- the library, with SDL3's native libraries through the `SDL3-CS.Native` dependency;
- the built-in shaders, as content files that land in `source/shaders` beside a game's program;
- those shaders compiled to SPIR-V by `e3d shaders`, landing in `source/.slang-cache`, so a game
  loads them with no `slangc` of its own;
- the generator as an analyzer, so `[Behavior]` and `[Command]` code in a game is generated as it
  is here.

A game's own shaders still need `slangc` (through `ENGINE_SLANGC` or `PATH`), or a cache it
compiles the same way with `e3d shaders <folder> <cache>`. A game outside this repository built
from a local pack points a `nuget.config` at the folder, which maps `3DEngine` to the folder alone
(`packageSourceMapping`), as the one below and the games' do, so the version on nuget.org is not
taken in its place, and `dotnet add package` is given the version, since without one it takes the
newest version nuget.org lists before the mapping applies.

A release is made by the `pack` workflow (`pack.yml`), run from the Actions tab on GitHub. It runs
the build workflow (`build.yml`), the one each push runs, with its `pack` input set, which adds to
what a push runs every example captured for the gallery and measured against raylib's program
(`captures`), every game played, captured, soaked and resized and the README followed on Linux
(`games`), every example captured on the macOS runner's GPU (`captures-macos`), and every game
played on Windows and macOS. Only once every job passes does it pack with `build/pack.sh <version>`
and keep the package as the run's artifact, and with its "publish" box ticked it pushes both
packages to nuget.org through nuget.org's trusted publishing, with no key stored. A policy on
nuget.org, owned by the user EggyStudio, names the repository owner EggyStudio, the repository
3DEngine, the workflow file `pack.yml`, no environment, the push scope and the packages `3DEngine*`,
and the run's `NuGet/login` step gives it the run's identity for a key that lasts an hour. A push
runs the three systems' tests, Slide played
on each, and the Linux `examples` job, and leaves the rest to the pack run. The version is
`build/version.sh`'s, the major and minor written in `build/version.txt` and, as the patch, the
number of commits since that file last changed, so each commit counts the patch up by one and
changing `5.0` to `5.1` starts it again at `5.1.0`.

The package's release notes are the commits since the newest package of the minor on nuget.org was
built, each one's sentence a line and the newest first, so a package's page says what changed since
the package before it. `build/release-notes.sh` reads the versions from nuget.org's index of the
package and the commit that version was built from from its nuspec's repository element, writes the
commits after it from the history into `build/artifacts/release-notes.txt`, and says which commit
they start after, which the pack run's summary shows. Where nuget.org answers nothing, lists no
version of the minor, or names a commit the checkout does not have, the notes are the commits since
`build/version.txt` last changed, and the script says why. A commit whose every file lies under
`.github/` is left out, since the package carries nothing from there, so the review's records and
the workflows' changes stay off a package's page while the README and the guides it carries do not.

### The public surface

`3DEngine/PublicApi.txt` lists every public type of the engine and every member a game can reach,
a line each, read from the built assembly. The suite fails while the two differ, naming the lines
added and removed, so a commit that adds, removes or reshapes anything a game calls carries the
change to that file and is read as one, which the patch number alone does not say. Once a change
to the surface is meant, `build/api.sh` writes the file again:

```bash
build/api.sh         # 3DEngine/PublicApi.txt from the built engine, and what changed in it
```

A pack made locally without a version is a version of its own, `0.1.0-preview.` and the time,
since NuGet reads a version once and keeps it. A game asks for the newest with `Version="0.1.0-*"`, and after a pack restores with
`dotnet restore --force-evaluate`, since a restore that sees nothing changed in the project keeps
the version it chose before. `games/Slide` is such a game, built this way in CI, and
`./e3d open games/Slide/bin/Debug/net10.0/Slide` controls it as it does the examples.

### A program on a local package

`build/pack.sh` packs the engine into `build/package`, with the templates beside it of the same
version. Installed from that file, the templates make a project that asks for that version and
takes it from the folder, through a `nuget.config` they write when given the folder:

```bash
build/pack.sh                                                    # in the checkout
dotnet new install build/package/3DEngine.Templates.<version>.nupkg
dotnet new 3dengine -o path/to/Hello --package-folder "$PWD/build/package"
```

Without the templates, a console project wherever the program is to live
(`dotnet new console -n Hello`) gets a `nuget.config` that sends `3DEngine` to that folder, rather
than to the version on nuget.org, and everything else to nuget.org, with the folder's path in
place of the one shown:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="engine" value="path/to/3DEngine/build/package" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="engine">
      <package pattern="3DEngine" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

```bash
dotnet add package 3DEngine --version "0.1.0-*"
```

The program at the top of the README then goes into `Program.cs`. `build/readme-walk.sh <package
folder>` follows the README's commands with both templates and these steps with that program, each
in a new folder, and CI runs it.

### Shipping a game

A game is shipped with `dotnet publish`, as a folder with the .NET runtime in it or as one native
executable through native AOT, which starts at once and needs nothing installed:

```bash
dotnet publish -c Release -r linux-x64 -p:PublishAot=true -o publish    # or win-x64, osx-arm64
```

`games/Slide`, the lightest of the 3D games to build and draw, publishes and runs this way, trimmed
or native, on Linux, Windows and macOS in CI (`build/play-native.sh`), with its models, its sounds
made as waves, its font and the images its tiles' faces are drawn into, the engine's shaders
compiled ahead in the package (`e3d shaders`, above). Behaviors, scene components and console
commands register through code the generator writes, from module initializers, so nothing is found
by a search the trimmer could break. The library is marked `IsAotCompatible`, so every build runs the
trimmer's analysis over it and `-warnaserror` keeps it clean. The console's commands that read a
component by reflection (`entity.add`, `entity.set`, a tool for development) and the compiler for
behavior scripts, which loads assemblies at run time and so works only in a build that is not
native, say at their places why they are safe. A native build runs the behaviors compiled into it,
and leaves the compiler and Roslyn out, since the plugin starts it only where
`RuntimeFeature.IsDynamicCodeSupported`, which the AOT compiler takes as false, and Slide's
executable is 14 MB. The warnings a publish prints come from the AssimpNetter package alone (IL2104 and IL3053), whose loader binds Assimp's native functions to delegates by
reflection, and which a native game's models are read through all the same.

## The generator

`3DEngine.Generator` targets netstandard2.0 and references Roslyn 4.14, because the compiler refuses
an analyzer built against a newer Roslyn than its own, and every .NET 10 SDK carries at least that
version. The engine compiles the same generator sources into itself as well, against the newer
Roslyn it references for compiling behavior scripts while an app runs, so a script is generated the
same way a project is.

## Shaders

Built-in shaders live in `3DEngine/Shaders` and are copied to `source/shaders` beside every program
that references the engine, where `AssetServer` reads them. They are Slang, compiled to SPIR-V by
`slangc` when they are loaded and cached in `source/.slang-cache`.

`build/fetch-slang.sh` (or `build/fetch-slang.ps1` on Windows) downloads a pinned release into
`build/tools/slang`. The engine looks for the compiler in `ENGINE_SLANGC`, then on the `PATH`, then
in `build/tools/slang/bin` above the running program and above the working directory, so a program
run from anywhere in the checkout finds it.

## Platforms

| Platform | Window and input | Graphics |
|---|---|---|
| Linux | SDL3 (Wayland or X11) | Vulkan |
| Windows | SDL3 | Vulkan |
| macOS | SDL3 | Vulkan through MoltenVK |

Each is meant on x64 and arm64, for which the package's dependencies carry SDL3, Dear ImGui and
Assimp natively, and `PackageContentsTests` fails a package where one of them is missing.

Linux is where the engine is developed and tested, and `.github/workflows/build.yml`, which runs for
every push, builds and tests it on all three. Ubuntu 24.04, named rather than the newest so lavapipe
changes only in a commit, draws on lavapipe, under the validation layer of LunarG's SDK at the
version `build/fetch-validation-layer.sh` pins, since Ubuntu's own predates an extension lavapipe
offers. Windows draws on lavapipe too, from Mesa's Windows build, with LunarG's loader and
validation layer of the same version, pinned in the workflow, and macOS draws on its GPU through
MoltenVK, with the loader and the layer from Homebrew. On each the render tests and the reference frames run under the validation layer,
`E3D_REQUIRE_VULKAN` and `E3D_REQUIRE_VALIDATION` failing them where the device or the layer does
not start rather than letting them skip, and `build/play-game.sh Slide` builds a game from the
package and draws 300 frames of it offscreen, failing on an error the layer reports.
`build/play-native.sh Slide` then publishes it native from the package, linked by the runner's own
toolchain, Visual Studio's on Windows and Xcode's on macOS, as the examples job does on Linux, and
draws it the same way, a failure named in an error annotation with the compiler's or the linker's
own lines. On Windows and macOS `build/drive-game.sh <game>` then plays Slide through `./e3d` on a
push, and each of the thirteen games in the pack run, offscreen at 480 by 270, as the Linux jobs play
them, each asserting its walk or its win, so a game's input, its sound and the session `./e3d`
controls are tried there, and a game that fails says why in an error annotation naming the system.
Slide's gestures are timed on the game's clock, so its scripted run sets each frame to a sixtieth of
a second (`time.frame`) before it swipes, and a device that draws slowly sees the swipe it would see
at 60 frames a second. A game that dies leaves a dump, Windows' own on Windows and the .NET
runtime's in `captures/dumps` elsewhere where the runtime writes one, which the error names. On
Linux and macOS a game is opened by `build/open-game.sh`, as the Linux jobs open every game and
every example, under a shell that waits on it and writes how it ended, its exit code or signal. On
Linux each opening prints the processor's model and flags, the line saying how a game ended says
whether `gfni` is among them and what Mesa's shader cache served (`MESA_SHADER_CACHE_SHOW_STATS`),
and gdb reads the core a game that dies leaves, the faulting thread's frames and the instruction at
its program counter, which the error carries. Each game has twelve minutes there (`DRIVE_MINUTES`),
past which it is stopped and its error says how far it got by its last status, and Slide is played
once more in a window on the runner's desktop (`build/drive-game.sh Slide window`) with four
minutes, so SDL's window and the swapchain are tried on both systems. They then follow the README in
a new project (`build/readme-walk.sh`) and build and run every step of the first game
(`build/first-game.sh`) from the same package, as a newcomer on either system does. Each job runs
its tests through `build/test.py`, whose page ends the step's log and is the job's summary. Each
cause is also an error annotation with its whole entry, and a notice has the page's head and the
lines repeated most, since a reader who is not signed in to GitHub reads a run's annotations and
nothing else, so a red run says what failed to whoever opens it. A last job, `digest`, puts the
three pages into one, each cause with the systems it was seen on. Each job has a time limit. Each
builds with `-warnaserror`, so a warning fails the commit that wrote it, and a warning that is right
to keep is turned off where it arises, with its reason. A Vulkan instance asks for portability
devices where the loader offers them, and a device of the portability subset, as MoltenVK is, has
the subset enabled. On macOS the job installs `dotnet-gcdump` and names it in `E3D_GCDUMP`, with
which `AppLeakTests` counts the heap's objects by type after its twentieth app and its hundredth, so
a failure there names the types that grew. The Linux job fetches the commit the 5.1 package was
packed from, alone, so `UpgradingTests` holds `docs/upgrading.md` to every name the public surface
lost since.

On Linux, the `examples` job, which runs after the Linux tests on every push and in the pack run,
then checks the package as a player and a reader meet it. It packs the engine, publishes Slide
native and draws it, builds the examples, the guides' code and every game on the package, plays
Slide through `./e3d` and soaks it, builds the first game's steps and checks the tables against
raylib. In the pack run the `captures` job captures every example and measures it against raylib,
and the `games` job plays, captures, soaks and resizes every game and follows the README.
`build/play-native.sh Slide` publishes a game as native code from the package and draws 300
frames of it under the layer, so a type the library reaches by reflection that the native
compiler left out fails there. The native compiler needs clang and zlib's headers, which the
workflow installs beside lavapipe. `build/examples-on-package.sh` builds every example in a
project of its own outside the repository, on the package alone with warnings as errors, as a
reader copying one into a game of their own builds it, and `build/docs-on-package.py` builds every
C# block of the guides under `docs/` the same way, each in a file of its own. A fragment goes in a
method after the lines a `<!-- compiled with: -->` comment right above its fence gives, declaring
what it takes from the page around it, and a block a `<!-- not compiled: ... -->` comment marks,
with its reason, is left out, so a block that calls what the surface no longer has fails the run
with its page and line. `docs/first-game.md` is built step by step by `build/first-game.sh`
instead. Each step of those three jobs runs its script
through `build/step.py`, as GitHub's bash would run it, and a step that fails without an error of
its own is given one, naming the step, the command that failed with its exit code, the step's last
lines, each game it opened that ended other than of its own or still runs, and the last lines at a
warning or worse of each session log it wrote.
