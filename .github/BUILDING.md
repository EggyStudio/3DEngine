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
`NullGraphicsDevice`. The examples need both. Without `slangc` the shader tests return early, so a
green suite on a machine without it says nothing about shaders.

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
`3DEngine.Tests/Rendering/OffscreenRenderTests.cs` draw this way and return early where there is
no device.

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

Linux is where the engine is developed and tested, and `.github/workflows/build.yml` builds and
tests it on Ubuntu for every push. Windows and macOS build from the same packages and are not
covered by CI.
