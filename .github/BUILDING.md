# Building from source

How to build the engine, run its tests and run the examples.

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), a Vulkan driver, and `slangc`,
which `build/fetch-slang.sh` downloads. The SDL3,
Assimp and Dear ImGui native libraries arrive with their NuGet packages, so nothing else is
installed by hand. macOS draws through MoltenVK, which the Vulkan SDK provides.

```bash
build/fetch-slang.sh                       # slangc, into build/tools/slang (once)
dotnet build 3DEngine.slnx                 # the engine, the generator, the tests and the examples
dotnet test 3DEngine.Tests                 # the suite
dotnet run --project 3DEngine.Examples     # a window
```

The suite needs no GPU or display, because the tests that touch rendering use
`NullGraphicsDevice`. The examples need both. Without `slangc` the shader tests report as skipped,
and so do the render tests without a Vulkan device, each with its reason in the run's output.

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

Every program built on the engine reads these flags, or the variables beside them:

| flag | variable | effect |
|---|---|---|
| `--serve` | `E3D_SERVE=1` | answers `./e3d` on a local socket |
| `--hidden` | `E3D_HIDDEN=1` | renders into a window that is never shown, so captures work and nothing appears |
| `--offscreen` | `E3D_OFFSCREEN=1` | no window and no display needed, rendering into images of the device's own, so captures work on CI and over SSH |
| `--headless` | `E3D_HEADLESS=1` | no window and no renderer, frames paced at 60 per second |
| `--frames N` | `E3D_FRAMES=N` | closes after N frames |

`./e3d open <example>` starts an example with `--serve` and any of the others, and the skill at
`.claude/skills/e3d-cli/SKILL.md` covers driving it.

`--offscreen` needs a Vulkan device and nothing else. Mesa's lavapipe, which runs on the CPU, is
one (`mesa-vulkan-drivers` on Debian and Ubuntu), and is what CI renders with. The render tests in
`3DEngine.Tests/Rendering/OffscreenRenderTests.cs` draw this way and are skipped where there is
no device.

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
(`packageSourceMapping`), as the README's and the games' do, so the version on nuget.org is not
taken in its place, and `dotnet add package` is given the version, since without one it takes the
newest version nuget.org lists before the mapping applies.

A release is made by the `pack` workflow, run from the Actions tab on GitHub. It runs the tests on
Linux and Windows through `test.yml`, the same workflow `build.yml` runs on each push, and only
once both pass packs with `build/pack.sh <version>` and keeps the package as the run's artifact.
With its "publish" box ticked it pushes the package to nuget.org through the `NUGET_API_KEY`
secret. The version
is `build/version.sh`'s: the major and minor written in `build/version.txt`, and as the patch the
number of commits since that file last changed, so each commit counts the patch up by one and
changing `0.1` to `0.2` starts it again at `0.2.0`.
`build/readme-walk.sh <package folder>` follows the README's steps in a new folder, and CI runs it.

Each pack is a version of its own, `0.1.0-preview.` and the time, since NuGet reads a version once
and keeps it. A game asks for the newest with `Version="0.1.0-*"`, and after a pack restores with
`dotnet restore --force-evaluate`, since a restore that sees nothing changed in the project keeps
the version it chose before. `games/Pusher` is such a game, built this way in CI, and
`./e3d open games/Pusher/bin/Debug/net10.0/Pusher` drives it as it does the examples.

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

Linux is where the engine is developed and tested, and `.github/workflows/test.yml`, which
`build.yml` runs for every push, builds and tests it on Ubuntu, drawing on lavapipe. Its Windows
job builds it and runs the tests that need no device, since the runner has no Vulkan device. macOS builds from the same packages
and is not covered by CI.
