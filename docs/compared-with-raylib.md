# Compared with raylib

This engine takes raylib's design, and a reader who knows raylib can ask why not raylib itself.
This page says what is the same, what this engine adds, what it costs, and what was measured on one
machine, with the command that measures it again.

## The same

- **One flat API.** A program calls static functions named as raylib names them, `InitWindow`,
  `DrawTexture`, `LoadModel`, imported with `using static Engine.Engine3D;`, and a raylib example
  reads almost line for line in C#.
- **A loop the program owns.** `while (!WindowShouldClose())`, `BeginDrawing`, `EndDrawing`, with
  nothing to subclass and no callbacks to register.
- **A cheatsheet.** [CHEATSHEET.md](../CHEATSHEET.md) has every function on one line, grouped as
  raylib groups its own, and a test keeps it equal to the code.
- **Examples by name.** `dotnet run --project 3DEngine.Examples -- core_basic_window` runs one,
  named as raylib's are, each with a capture in the README.

## What it adds

- **C#, with no binding layer.** The API is written in C#, so its types are C#'s (`Vector3`,
  `string`, `Span<T>`), memory is the runtime's to free, and a game's code and the engine's are one
  language, read and stepped through together.
- **A Vulkan renderer** with metallic-roughness materials and their maps, shadows from a sun in
  cascades and from spot and point lights, an environment map lighting from all around, reflection
  probes for the inside of a room, bloom over a frame that holds light past white, instancing and
  compute shaders.
  [Materials, light and shadows](materials-light-and-shadows.md) and
  [Shaders and compute](shaders-and-compute.md) show them, and `models_reflection_probe`,
  `shaders_shadowmap` and `shaders_compute_life` run them.
- **An ECS under the flat API.** `[Behavior]` structs whose methods a source generator turns into
  systems run in the same frames as the loop, so a program grows into entities when it needs them.
  [Behaviors and the ECS](behaviors-and-the-ecs.md) and `ecs_behaviors`.
- **Physics** over BepuPhysics, with a character controller, joints, triggers and contacts.
  [Physics](physics.md) and `physics_boxes`.
- **Skeletal animation** from glTF and FBX through Assimp, blended between clips.
  [Models and animation](models-and-animation.md) and `models_animation`.
- **Scene files and prefabs**, a level saved as JSON with scenes placed inside scenes.
  [Scenes](scenes.md) and `scenes_level`.
- **Behaviors compiled while the game runs**, from a scripts folder through Roslyn.
- **Dear ImGui inside the frame**, between `BeginDrawing` and `EndDrawing` with no setup.
  `gui_imgui_window`.
- **A program driven from the terminal.** `./e3d` asks a running program what is in its world,
  presses its keys and captures its frames, in a window never shown.
  [Driving a program with e3d](driving-with-e3d.md).
- **Native builds.** A game publishes as one native executable through .NET's native AOT,
  `games/Pusher` at 11 MB ([BUILDING.md](../.github/BUILDING.md#shipping-a-game)).

## What it costs

- **Desktop only.** raylib runs on the web, on phones and on small boards such as the Raspberry
  Pi. This engine runs on Linux, Windows and macOS (through MoltenVK), and CI draws only on Linux.
- **A runtime or a larger binary.** A program needs the .NET runtime, or is published native at
  about 11 MB, where raylib's bunnymark links statically into under 1 MB beside SDL.
- **Vulkan.** A GPU and driver with Vulkan 1.3 are needed, where raylib draws through OpenGL 3.3,
  or OpenGL 1.1 and 2.1 on old machines.
- **Younger and less proven.** raylib has more than a decade of users and ports behind it, and this engine
  is early, used for small games, with its own list of what is missing in
  [TODO.md](../.github/TODO.md).
- **Not all of raylib.** 491 of the 619 functions in `raylib.h` are carried, 79 percent, counted
  below. The rest are mostly what C# already has, file paths, directories, hashes, compression and
  string functions, with VR stereo, automation events, the audio processors and some image and
  shape variants, which TODO.md names with reasons.

## Measured

Taken on 2026-10-04 on an Intel Core i9-14900HX with an NVIDIA GeForce RTX 4070 Laptop GPU (driver
615.71.09, Linux 7.2). raylib at commit `30fa673` (after 5.5) built in C with `-O2` and its SDL3
backend, drawing through OpenGL 3.3, and this engine's Release build drawing through Vulkan. Both
run in a hidden 800 by 450 window with no frame rate cap, and search for the most that holds 60
frames a second with the same steps (`StressRamp`): growing by half until a frame passes a
sixtieth of a second, then halving the gap to within about 3 percent.

| | raylib | 3DEngine |
|---|---|---|
| Sprites, `textures_bunnymark` (32 by 32, one texture, each a `DrawTexture`) | 141,882 in each of three runs | 212,822 to 243,226 over three |
| Cubes turning each frame (`DrawModelEx` each in raylib, mesh entities in `models_stress`) | 6,403 in each of two runs | 294,024 to 314,537 over two |
| Functions of `raylib.h` carried | 619 | 491 (79 percent) |

raylib's counts repeat exactly from run to run, and this engine's move by about a tenth, with
.NET's compiler and garbage collector in the frame. The cubes are not like for like. raylib's default shader draws them unlit with no shadow, one draw
call each, which is the plain raylib way, and a raylib program could instance them with
`DrawMeshInstanced` and a shader of its own. `models_stress` lights them by a sun with a shadow and
two point lights, beside eight skinned arms, and the engine batches entities sharing a mesh into
instanced draws by itself. RENDERING.md
([§6](../.github/RENDERING.md#beside-raylib)) keeps these numbers, beside what each part of a frame
costs, as they change.

```bash
build/raylib-bench/run.sh
```

builds raylib for the measurement in a scratch folder, runs both pairs and counts the functions,
with `build/raylib-bench/coverage.py` naming those left out by the section of `raylib.h` they are in.
raylib is not a dependency of the engine.

## See also

- [The window and the frame](window-and-frame.md), where the guide starts
- [DESIGN.md](../.github/DESIGN.md), the rules the flat API follows
