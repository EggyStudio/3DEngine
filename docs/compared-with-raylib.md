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
  probes for the inside of a room, bloom over a frame that holds light past white, instancing,
  compute shaders, and particles a compute shader steps.
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

## Where a call answers otherwise

A call of raylib's name here does what raylib's does, and a port of one of raylib's examples that
finds otherwise either brings the call to raylib's or, where the difference is kept, adds its line
here, with the reason.

| Call | raylib | 3DEngine | Why |
|---|---|---|---|
| `GetGamepadAxisMovement` on a trigger | -1 at rest to 1 | 0 at rest to 1 | SDL3 reads a trigger from 0, and a trigger that rests at 0 needs no shifting to be read |
| A render texture drawn with `DrawTextureRec` or `DrawTexturePro` | Stored bottom up, as OpenGL draws, and drawn with its height negative to turn it upright | Stored top down, as Vulkan draws and an image is read, and drawn as it is | A target read back with `LoadImageFromTexture` is the right way up, as any texture is |
| `MouseButton.X1` and `X2` | `MOUSE_BUTTON_SIDE` and `MOUSE_BUTTON_EXTRA`, beside forward and back | Named as SDL3 names them, with no forward and back | SDL3, raylib's backend on the desktop as well, reports two extra buttons |
| `GetFontDefault` | raylib's own pixel font | ImGui's, ProggyClean | One atlas serves ImGui and the flat API's text |
| A window's samples before `SetConfigFlags` | One, and four with `FLAG_MSAA_4X_HINT` | Four, and one with `SetConfigSamples(1)` | Edges are smooth without a flag |
| A render texture's samples | One, so what is drawn into it has hard edges | The window's, resolved into the texture, so its edges are smoothed as the window's are | The window's pipelines, made for one count of samples, draw into it as they are |
| A texture's filter before `SetTextureFilter` | `TEXTURE_FILTER_POINT`, every texel a sharp square | `TextureFilter.Bilinear`, blended between texels | A model's textures and a scaled image are smooth without a call, and pixel art sets `Point` |

## raymath

raymath's functions are C#'s own where `System.Numerics` has them, and the package carries the
39 it has no counterpart for under raymath's names, which the cheatsheet's Math section lists. A
raymath `Matrix` is a `Matrix4x4`, its field `m(4r + c)` being `M(r+1)(c+1)`, so a translation is
in `M41` to `M43` in both and `MatrixMultiply(a, b)` is `a * b`. `RayMathTests` holds the rotations
and `MatrixLookAt` below to raymath's own arithmetic.

| raymath | C# | Where it answers otherwise |
|---|---|---|
| `Clamp`, `Lerp` | `Math.Clamp`, `float.Lerp` | |
| `Vector2Zero`, `Vector2One`, and the same of `Vector3` and `Vector4` | `Vector2.Zero`, `Vector2.One` and so on | |
| `Vector2Add`, `Subtract`, `Scale`, `Multiply`, `Divide` and `Negate`, and the same of the others | `+`, `-`, `*`, `/` and unary `-` | |
| `Vector2AddValue`, `SubtractValue`, and the same of the others | `v + new Vector2(value)` and so on | |
| `Vector2Length`, `LengthSqr`, `DotProduct`, `Distance` and `DistanceSqr`, and the same of the others | `v.Length()`, `v.LengthSquared()`, `Vector2.Dot`, `Vector2.Distance`, `Vector2.DistanceSquared` | |
| `Vector2Normalize`, `Vector3Normalize`, `Vector4Normalize`, `QuaternionNormalize` | `Vector2.Normalize` and the others' | A vector of length zero gives NaN, where raymath gives it back as it is |
| `Vector2Transform`, `Vector3Transform` | `Vector2.Transform`, `Vector3.Transform` | |
| `Vector2Lerp`, `Reflect`, `Min`, `Max`, `Clamp`, `Invert`, and the same of the others | `Vector2.Lerp`, `Reflect`, `Min`, `Max`, `Clamp`, and `Vector2.One / v` | |
| `Vector3CrossProduct`, `Vector3RotateByQuaternion` | `Vector3.Cross`, `Vector3.Transform(v, q)` | |
| `Vector3ToFloatV`, `MatrixToFloatV` | The fields, `X` to `Z` and `M11` to `M44` | |
| `MatrixDeterminant`, `Transpose`, `Invert`, `Identity`, `Add`, `Subtract`, `Multiply`, `MultiplyValue` | `m.GetDeterminant()`, `Matrix4x4.Transpose`, `Matrix4x4.Invert(m, out inverse)`, `Matrix4x4.Identity`, `+`, `-`, `*` | |
| `MatrixTranslate`, `Scale`, `RotateX`, `RotateY`, `RotateZ`, `LookAt` | `Matrix4x4.CreateTranslation`, `CreateScale`, `CreateRotationX`, `CreateRotationY`, `CreateRotationZ`, `CreateLookAt` | |
| `MatrixRotate`, `QuaternionFromAxisAngle` | `Matrix4x4.CreateFromAxisAngle`, `Quaternion.CreateFromAxisAngle` | The axis is to be of length one, which raymath makes it |
| `MatrixPerspective`, `MatrixOrtho`, `MatrixFrustum` | `Matrix4x4.CreatePerspectiveFieldOfView`, `CreateOrthographicOffCenter`, `CreatePerspectiveOffCenter` | Depth clipped from 0 to 1, as Vulkan clips it, where raymath's is from -1 to 1, as OpenGL's |
| `MatrixDecompose` | `Matrix4x4.Decompose(m, out scale, out rotation, out translation)` | |
| `QuaternionIdentity`, `Length`, `Invert`, `Multiply` | `Quaternion.Identity`, `q.Length()`, `Quaternion.Inverse`, `*` | |
| `QuaternionNlerp`, `QuaternionSlerp` | `Quaternion.Lerp`, `Quaternion.Slerp` | raymath's `QuaternionLerp`, not made of length one, is carried |
| `QuaternionFromMatrix`, `QuaternionToMatrix` | `Quaternion.CreateFromRotationMatrix`, `Matrix4x4.CreateFromQuaternion` | |

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
