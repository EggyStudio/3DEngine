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
- **Not all of raylib.** 501 of the 619 functions in `raylib.h` are carried, 81 percent, counted
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
| `LoadFont` and `LoadFontEx` with no code points | The 95 characters of ASCII | Latin-1, to `ÿ` | Text in Spanish, French or German draws its accents without naming them |
| A minimized window without `FLAG_WINDOW_ALWAYS_RUN` | The loop waits in `EndDrawing` for events until the window is restored | The loop goes on, drawing nothing, with the flag or without it | `./e3d` drives a minimized window, an audio stream fed from the loop keeps playing, and a game pauses itself by `IsWindowMinimized` |
| A window's samples before `SetConfigFlags` | One, and four with `FLAG_MSAA_4X_HINT` | Four, and one with `SetConfigSamples(1)` | Edges are smooth without a flag |
| A render texture's samples | One, so what is drawn into it has hard edges | The window's, resolved into the texture, so its edges are smoothed as the window's are | The window's pipelines, made for one count of samples, draw into it as they are |
| Drawing inside `BeginTextureMode` | Drawn into the target at once, so a call after `EndTextureMode` reads it | Drawn as the frame ends, before the window, each target in one pass cleared to the last `ClearBackground` inside it, with `UpdateTexture` and `UpdateTextureRec` in their places among the shapes | A frame's drawing is batched as the window's is, so `LoadImageFromTexture` reads a target as the last frame left it |
| `LoadImageAnim` | One frame tall, the other frames after it in memory | As tall as every frame, stacked from the top | An image's pixels are always its size here, so every image call reads all of it, and a frame is a rectangle of it |
| `ImageFormat` | The image's pixels stored in the format, which a program then reads them by | Each pixel keeps what the format keeps, and is stored as four bytes still | Every image is RGBA, the one format drawing and the GPU take as it is |
| An image from a file without alpha, as a PNG of RGB | Kept as three bytes a pixel, so the corners `ImageRotate` adds are black | Four bytes a pixel, so they are clear | Every image is RGBA, the one format drawing and the GPU take as it is |
| The materials `LoadModel` reads from a glTF or Model 3D file | raylib's default material at 0, the file's from 1 | The file's from 0, and a white one of its own for a mesh with none | A model's materials are the file's, so `Materials[i]` is the file's material `i` |
| `VertexCount` of a mesh from an OBJ file, `GenMeshSphere`, `GenMeshHemiSphere`, `GenMeshCylinder`, `GenMeshCone`, `GenMeshTorus` or `GenMeshKnot` | Three vertices of its own for each triangle, three times `triangleCount` | Vertices shared between the triangles that meet at them, as Assimp joins a file's and the generators make them | A mesh draws and collides the same with fewer vertices to send |
| rlgl | A layer of its own, with its batch, its matrix modes and OpenGL's state | `rlBegin` to `rlEnd` with `rlVertex2f`, `rlVertex3f`, `rlTexCoord2f`, `rlNormal3f`, `rlColor4ub`, `rlColor4f`, `rlSetTexture` and `rlCheckRenderBatchLimit`, the matrix stack's `rlPushMatrix`, `rlPopMatrix`, `rlTranslatef`, `rlRotatef`, `rlScalef`, `rlMatrixMode`, `rlLoadIdentity`, `rlMultMatrixf` and `rlSetMatrixProjection`, recorded into the frame's draw list, and the switches of culling, point mode, blending, blend factors and depth testing and writing. Its projection moves shapes, text and rlgl's vertices, and models are drawn through the camera of `BeginMode3D`. `rlOrtho`, `rlFrustum`, the viewport, the framebuffers and textures of its own, which `LoadRenderTexture`'s target with its depth and its images of several formats stands for, wires and line width are not carried | raylib's examples call no more of it but the framebuffers, which a render texture here stands for with its depth and its several images |
| `GetMonitorPhysicalWidth`, `GetMonitorPhysicalHeight` | The monitor's own size on GLFW, and on raylib's SDL3 backend its pixels at 96 an inch times the window's scale | Its pixels at 96 an inch times the window's scale, as raylib's SDL3 backend reckons it | SDL3, which the engine stands on as that backend does, gives no monitor's own size |
| `PollInputEvents`, `SwapScreenBuffer` | The polling and the buffer swap, which a raylib built with `SUPPORT_CUSTOM_FRAME_CONTROL` leaves to the program | The polling, once a frame, and nothing, `EndDrawing` having presented the frame | The engine polls and presents each frame as raylib's own build does, so a program written for the other runs as it was meant to |
| `AttachAudioStreamProcessor` | Run on the audio thread as the mixer reads the stream, over its samples in the device's two channels and rate | Run on the program's thread as the stream or the music queues them, in the stream's own channels and rate | A stream's samples are queued from the program's thread here, and a processor reads the game's state as the loop does |
| `rlSetMatrixProjection` | OpenGL's projection, depth from -1 to 1 and up the screen | A projection as `System.Numerics` makes one, depth from 0 to 1, turned for Vulkan's clip space as the camera's is | A program here makes its matrices with `System.Numerics`, `MatrixFrustum`'s counterpart among them |
| Culling before a program sets it | Back faces left out of everything from the start | Shapes and text draw both faces, and a model the faces its material says, until `rlEnableBackfaceCulling`, `rlDisableBackfaceCulling` or `rlSetCullFace`, and then everything follows rlgl's | A double-sided glTF material, a leaf or a sheet of cloth, draws both its faces without a call, and a shape given clockwise is not lost |
| `rlSetBlendFactors` and `rlSetBlendFactorsSeparate` while a custom blend mode is on | Taken at the next `rlSetBlendMode` | Taken by what is drawn after | What is drawn takes the factors last set, so the first frame of `textures_magnifying_glass`, which sets its factors after its mode, is masked as the rest are |
| `rlEnablePointMode` | OpenGL's polygon mode set to points, which a shape takes if rlgl draws its batch before point mode ends | Models drawn as points, and shapes filled | raylib's examples draw models so, and a shape there is points or filled by when rlgl next draws its batch |
| `GetGlyphIndex` | A glyph's index into the font's `glyphs` and `recs` arrays, which a program reads its advance, offset and atlas rectangle from | Not carried, a font keeping its glyphs by code point, and `GetGlyphInfo` and `GetGlyphAtlasRec` take the code point, its `Glyph` holding the offset, size, atlas coordinates and advance | ImGui's atlas, which the flat API's text and ImGui share, keeps its glyphs by code point |
| A model drawn when the program has made no light | Unlit, its texture and color as they are | Shaded by a fixed light from above, from about a third in its shadow to full | A shape reads as solid with no light made, and the first light made takes over |
| `LoadShader` | A vertex and a fragment file of GLSL, either null for the default | One Slang file, its fragment stage drawing with the engine's vertex stage when it has no vertex stage of its own | Slang compiles to the SPIR-V that Vulkan reads, and one file holds both stages |
| `IsModelAnimationValid` | Whether the clip has as many bones as the model | Whether it has as many, each under the same parent, and each of the same name where the clip names its bones | A clip of another skeleton with as many bones would move the model's bones by the wrong ones, and an IQM file of clips alone names none |
| `GenTextureMipmaps` on a render texture | Mip levels made from what it holds | One level kept, the call leaving it as it is | Its image is the attachment drawing writes into, made with one level, and levels made from it once would go stale as it is drawn into again |
| `UpdateMeshBuffer` on colors or second texture coordinates a mesh lacks | Nothing written, the mesh having no buffer until rlgl loads one | The mesh given both, the rest of each white or zero | A mesh carries both where it has either, and rlgl's vertex buffers are not carried |
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
| Functions of `raylib.h` carried | 619 | 501 (81 percent) |

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
