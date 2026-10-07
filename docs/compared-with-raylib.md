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
  compute shaders, particles a compute shader steps, a distance field of the scene that ambient
  occlusion, the sun's contact shadows and particles read, and light that bounces and glossy
  reflections traced through it.
  [Materials, light and shadows](materials-light-and-shadows.md) and
  [Shaders and compute](shaders-and-compute.md) show them, and `models_reflection_probe`,
  `shaders_shadowmap`, `shaders_scene_field`, `shaders_cornell_box`, `shaders_reflections` and
  `shaders_compute_life` run them.
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
- **Color emoji in text**, a color font's pictures, colored layers or gradient paints drawn in their
  colors by `LoadFontEx`, where raylib's fonts are coverage alone, and a sequence the font joins, a family, a
  flag or a skin tone, drawn as its one picture, where raylib draws its characters apart.
  [Text and fonts](text-and-fonts.md).
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
- **Not all of raylib.** 520 of the 619 functions in `raylib.h` are carried, 84 percent, counted
  below. The rest have their counterparts in C#, file paths, directories, hashes, compression,
  memory and strings, each beside its counterpart below, and a few are left out, each with its
  reason.

## Where a call answers otherwise

A call of raylib's name here does what raylib's does, and a port of one of raylib's examples that
finds otherwise either brings the call to raylib's or, where the difference is kept, adds its line
here, with the reason.

| Call | raylib | 3DEngine | Why |
|---|---|---|---|
| `GetGamepadAxisMovement` on a trigger | -1 at rest to 1 | 0 at rest to 1 | SDL3 reads a trigger from 0, and a trigger that rests at 0 needs no shifting to be read |
| `AutomationEvent` and the file `ExportAutomationEventList` writes | Keys, mouse buttons and gamepad buttons by raylib's codes, an event's type a number raylib keeps to itself, and its four parameters an array | Keys by `Key`, which SDL's scancodes number, buttons by `MouseButton` and `GamepadButton`, the type an `AutomationEventType` numbered as raylib's, and the parameters `Param0` to `Param3`, so a file of raylib's plays its frames and types here and not its keys | A recorded key is the `Key` a program reads, and a list is a class, so the list `SetAutomationEventList` is given is the one recording adds to |
| A render texture drawn with `DrawTextureRec` or `DrawTexturePro` | Stored bottom up, as OpenGL draws, and drawn with its height negative to turn it upright | Stored top down, as Vulkan draws and an image is read, and drawn as it is | A target read back with `LoadImageFromTexture` is the right way up, as any texture is |
| `MouseButton.X1` and `X2` | `MOUSE_BUTTON_SIDE` and `MOUSE_BUTTON_EXTRA`, beside forward and back | Named as SDL3 names them, with no forward and back | SDL3, raylib's backend on the desktop as well, reports two extra buttons |
| `GetFontDefault` | raylib's own pixel font | ImGui's, ProggyClean | One atlas serves ImGui and the flat API's text |
| `LoadFont` and `LoadFontEx` with no code points | The 95 characters of ASCII | Latin-1, to `ÿ` | Text in Spanish, French or German draws its accents without naming them |
| `ImageRotate` of an image with no alpha | The corners the turn opens black, zero in an image of three channels, as `raylib_logo.png` is | Clear | An image here holds red, green, blue and alpha whatever its file held, so zero is clear |
| `DrawTextEx` with a font from a file, a quarter or more past its size | Its one bake scaled, so large text is soft or blocky | The file baked again at the size and drawn in the same boxes and advances, so large text is sharp and lies where raylib's does | A title drawn large from a font loaded small reads better sharp |
| A minimized window without `FLAG_WINDOW_ALWAYS_RUN` | The loop waits in `EndDrawing` for events until the window is restored | The loop goes on, drawing nothing, with the flag or without it | `./e3d` drives a minimized window, an audio stream fed from the loop keeps playing, and a game pauses itself by `IsWindowMinimized` |
| A window's samples before `SetConfigFlags` | One, and four with `FLAG_MSAA_4X_HINT` | Four, and one with `SetConfigSamples(1)` | Edges are smooth without a flag |
| A render texture's samples | One, so what is drawn into it has hard edges | The window's, resolved into the texture, so its edges are smoothed as the window's are, and one where `LoadRenderTextureEx` is given one sample | A scene drawn into a texture is edged as the same scene drawn to the window |
| A shape partly clear drawn into a render texture | Its alpha blended by the same factors as its color, so alpha 0.8 over an opaque texel leaves 0.84, and the texture drawn on the window shows what is behind it there, as `core_3d_camera_split_screen`'s bar darkens over black | Alpha laid over by alpha, so the texel stays opaque | A target drawn on the window shows what was drawn into it at the coverage it was drawn with, as a layer does in a picture of several |
| Drawing inside `BeginTextureMode` | Drawn into the target at once, so a call after `EndTextureMode` reads it | Drawn as the frame ends, before the window, each target in one pass cleared to the last `ClearBackground` inside it, with `UpdateTexture` and `UpdateTextureRec` in their places among the shapes | A frame's drawing is batched as the window's is, so `LoadImageFromTexture` reads a target as the last frame left it |
| `LoadImageFromScreen` | OpenGL's back buffer as it stands, what has been drawn of a frame inside one, and between frames what the driver left after the swap | The last frame presented, inside a frame as well, and for the first call of a run the window's size in the last clear color | A frame is drawn on the GPU as `EndDrawing` ends it, and frames are copied from the first call on, so a program that never reads the screen does not pay for a copy each frame |
| A cube texture a shader samples | Named by the material's `MATERIAL_MAP_CUBEMAP`, the shader's sampler given that map's number as an int | Given to the shader's `SamplerCube` with `SetShaderValueTexture`, and drawn as a 2D texture it draws white | A shader's textures are set by name here, whichever draw they are for, and a material holds the maps the model pass's own lighting reads |
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
| Culling before a program sets it | Back faces left out of everything from the start | Back faces left out of shapes drawn inside `BeginMode3D`, as raylib's, while 2D shapes and text draw both faces and a model the faces its material says, until `rlEnableBackfaceCulling`, `rlDisableBackfaceCulling` or `rlSetCullFace`, and then everything follows rlgl's | A 2D shape given clockwise is not lost, and a double-sided glTF material, a leaf or a sheet of cloth, draws both its faces without a call |
| `rlSetBlendFactors` and `rlSetBlendFactorsSeparate` while a custom blend mode is on | Taken at the next `rlSetBlendMode` | Taken by what is drawn after | What is drawn takes the factors last set, so the first frame of `textures_magnifying_glass`, which sets its factors after its mode, is masked as the rest are |
| `rlEnablePointMode` | OpenGL's polygon mode set to points, which a shape takes if rlgl draws its batch before point mode ends | Models drawn as points, and shapes filled | raylib's examples draw models so, and a shape there is points or filled by when rlgl next draws its batch |
| `GetGlyphIndex` | A glyph's index into the font's `glyphs` and `recs` arrays, which a program reads its advance, offset and atlas rectangle from | Not carried, a font keeping its glyphs by code point, and `GetGlyphInfo` and `GetGlyphAtlasRec` take the code point, its `Glyph` holding the offset, size, atlas coordinates and advance | ImGui's atlas, which the flat API's text and ImGui share, keeps its glyphs by code point |
| The normals of a glTF mesh under a node that scales it | Turned by the node and as long as one over its scale, 39.37 for a file in inches scaled to meters, so a shader that pushes a vertex along its normal pushes it that much further, as `shaders_cel_shading`'s outline does | Turned by the node and of unit length | A normal is a direction, so lighting and a shader read the same whatever unit a file was made in |
| A model drawn when the program has made no light | Unlit, its texture and color as they are | Unlit as raylib's, with the light its material gives off added | An emissive material, as a glTF file's lamp, glows before any light is made, as it does under them |
| `LoadShader` | A vertex and a fragment file of GLSL, either null for the default | One Slang file, its fragment stage drawing with the engine's vertex stage when it has no vertex stage of its own | Slang compiles to the SPIR-V that Vulkan reads, and one file holds both stages |
| `IsModelAnimationValid` | Whether the clip has as many bones as the model | Whether it has as many, each under the same parent, and each of the same name where the clip names its bones | A clip of another skeleton with as many bones would move the model's bones by the wrong ones, and an IQM file of clips alone names none |
| `GenTextureMipmaps` on a render texture | Mip levels made from what it holds | One level kept, the call leaving it as it is | Its image is the attachment drawing writes into, made with one level, and levels made from it once would go stale as it is drawn into again |
| `UpdateMeshBuffer` on colors or second texture coordinates a mesh lacks | Nothing written, the mesh having no buffer until rlgl loads one | The mesh given both, the rest of each white or zero | A mesh carries both where it has either, and rlgl's vertex buffers are not carried |
| A texture's filter before `SetTextureFilter` | `TEXTURE_FILTER_POINT`, every texel a sharp square | `TextureFilter.Bilinear`, blended between texels | A model's textures and a scaled image are smooth without a call, and pixel art sets `Point` |
| A model's color texture filtered between texels, by its filter or its mipmaps | The stored values, so halfway between black and white is 128 | The colors decoded from sRGB, filtered and encoded again, so halfway is 188, and a dark line on a light texture is thinner as the model recedes, as `shaders_lightmap_rendering`'s are | The model pass lights in linear light and reads a color texture decoded to it, as a lit picture needs, and 2D drawing reads a texture as stored, as raylib's does |
| A texture coordinate a fragment shader reads, where it falls exactly on a whole number of the shader's scale | OpenGL's, which in a quad's lower left triangle comes out a hair under it, so `shaders_eratosthenes_sieve`'s grid, which floors its coordinate times 1000, counts one row less there on 44 of the 50 rows of pixels where the coordinate is exact | Vulkan's, which comes out on it in both triangles | Each API's rasterizer rounds the last bit of what it interpolates its own way, below anything a program sets |
| A line drawn in the plane of a model's face, as `DrawGrid` on a floor at its height | Hidden on the pixels where OpenGL's depth for the line comes out behind the face's, so `shaders_lights_bloom`'s grid is dashed where it runs across the screen | Drawn whole, Vulkan's depth for it coming out no deeper than the face's in that example | Each API's rasterizer rounds depth its own way, and a line meant to show on a face is lifted off it to be sure of it in either |

## Names and shapes

The flat API's functions, types, fields and enum members take raylib's names, at the commit
`build/raylib-bench/run.sh` pins, and their arguments in raylib's order. Where one answers to
another name or takes another shape, it is here with the reason.

| raylib | 3DEngine | Why |
|---|---|---|
| `KeyboardKey`, each key `KEY_SPACE` and the rest | `Key`, each key by raylib's name, `Key.Space`, valued by SDL's scancodes | A C program writes `KEY_SPACE` without its enum's name and a C# one writes the name at every key, so it is the short word, as raylib's `MouseButton` and `GamepadButton` are |
| `TraceLogLevel` | `LogLevel` | raylib's constants read `LOG_INFO`, as `LogLevel.Info` does, and the engine's own log is leveled by the same enum |
| `Camera`, raylib's other name for `Camera3D` | `Camera3D` alone | `Camera` is the component a camera entity holds in the ECS, whose components have Bevy's names |
| `Mesh` | `ModelMesh`, a handle to a mesh's buffers on the GPU | `Mesh` is the component a mesh entity holds, and raylib's holds its arrays on the CPU, where a mesh here keeps its vertices on the GPU |
| `Material` | `ModelMaterial`, a color, maps and the model pass's values | `Material` is the component a mesh entity holds, and a model's material here is drawn by the model pass rather than by a shader and its maps |
| `Transform`'s `translation` | `Position` | `Transform` is also the component an entity is placed by, whose fields scene files name and the sibling engine's `Transform` names the same, so a level saved before reads as it did |
| `Vector2`, `Vector3`, `Vector4`, `Matrix`, `Quaternion` | `System.Numerics`' `Vector2`, `Vector3`, `Vector4`, `Matrix4x4` and `Quaternion` | They are C#'s own, which the runtime computes with SIMD |
| `FilePathList` | `string[]` | An array keeps its count |
| A pointer and its count, as `DrawLineStrip(points, pointCount, color)` | One array or span, `DrawLineStrip(points, color)` | An array or a span knows its length |
| `UploadMesh(Mesh *mesh, bool dynamic)` | `UploadMesh(vertices, indices)`, which gives the mesh | A mesh here is its buffers on the GPU, so it is made from the vertices rather than filled and then uploaded |
| `SetShaderValue(shader, locIndex, value, uniformType)` | An overload for each type of value | C# chooses the overload by the value, so its type is not given twice |
| `DrawTextEx` and the other text calls, of a line in Hebrew or Arabic | Drawn in the order it is stored, from left to right, each Arabic letter as it stands alone | Drawn in the order it is read, a run read right to left from right to left, its numbers from left to right and its brackets turned, and Arabic's letters joined in the forms the font gives them, their marks put on them, their pairs kerned and each joined to the next by its exit and that one's entry where the font positions them, the line measured by the glyphs it is drawn with | A line in a script written right to left reads as it is written, and a line of none is drawn and measured as raylib draws it |
| `DrawTextEx` and the other text calls, of a letter and a combining mark stored after it, as e and U+0301 | Drawn as the letter and the mark's own glyph, or the font's `?` where it has none | Drawn as the one character Unicode has for both, é, where the font has it, as HarfBuzz composes them | Text whose accents are stored apart from their letters, as some file names and input methods give it, reads as written |
| `TraceLog(logLevel, text, ...)` | `TraceLog(level, text)` | A C# program formats its text with an interpolated string |
| `UnloadDroppedFiles(files)` | `UnloadDroppedFiles()` | The list of dropped files is the engine's, which the call empties |
| `GetGamepadButtonPressed` with none pressed, `GAMEPAD_BUTTON_UNKNOWN` | `null` | The buttons are numbered from 0 as SDL numbers them, so no value is left for none |
| `GAMEPAD_BUTTON_LEFT_TRIGGER_2`, `GAMEPAD_BUTTON_RIGHT_TRIGGER_2` | Not carried, a trigger read as an axis, `GamepadAxis.LeftTrigger` from 0 to 1 | SDL3 reports a trigger as an axis alone |
| `Model.skeleton`'s `currentPose` and `boneMatrices` | Not carried | A model's pose lives with its skinned meshes on the GPU, posed by `UpdateModelAnimation` |
| `ModelAnimation` of a bone count | `ModelAnimation` with its `Bones` | A clip is checked against the model it is played on by its bones' names and parents |
| The ECS's mesh entities and particles inside `BeginVrStereoMode` | No ECS, and no particles | Drawn once, through the camera | They are drawn through the cameras that draw a target, where the shapes, lines, text and models drawn in 3D are drawn for each eye |
| `DrawModelPoints`, `DrawModelPointsEx`, `UnloadModelAnimation` | Carried, from raylib 5.5, which raylib 6 left out | A program of 5.5's calls them, and they take nothing from the rest |
| An argument's name, as `posX` and `startPos` | C#'s name for it in places, as `x` and `start` | A call's arguments are given in order, which is raylib's, and a program naming one takes the name the cheatsheet gives |

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

## raylib's functions C# has

A program in C# does what these do with .NET's own, so they are not carried. A function that
gives back memory a C program frees has nothing to free here, since the garbage collector frees an
array or a string nothing holds.

| raylib | C# | Where it answers otherwise |
|---|---|---|
| `UnloadFileData`, `UnloadFileText`, `UnloadDirectoryFiles`, `UnloadRandomSequence`, `UnloadImageColors`, `UnloadImagePalette`, `UnloadCodepoints`, `UnloadUTF8`, `UnloadTextLines`, `MemFree` | Nothing | |
| `MemAlloc`, `MemRealloc` | `new byte[size]`, `Array.Resize(ref array, size)` | |
| `FileRename`, `FileMove`, `FileRemove`, `FileCopy` | `File.Move`, `File.Delete`, `File.Copy` | |
| `FileTextReplace`, `FileTextFindIndex` | `File.WriteAllText(path, File.ReadAllText(path).Replace(search, replacement))`, `File.ReadAllText(path).IndexOf(search)` | |
| `DirectoryExists`, `IsPathFile`, `IsPathDirectory` | `Directory.Exists`, `File.Exists`, `Directory.Exists` | |
| `GetFileExtension`, `IsFileExtension` | `Path.GetExtension`, and its result compared with `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)` | raylib's `IsFileExtension` takes several extensions split by `;` |
| `IsFileHidden` | `File.GetAttributes(path).HasFlag(FileAttributes.Hidden)` | |
| `GetFileLength`, `GetFileModTime` | `new FileInfo(path).Length`, `File.GetLastWriteTimeUtc(path)` | A time, where raylib's is seconds since 1970, which `new DateTimeOffset(time).ToUnixTimeSeconds()` gives |
| `GetFileName`, `GetFileNameWithoutExt`, `GetDirectoryPath`, `GetPrevDirectoryPath` | `Path.GetFileName`, `Path.GetFileNameWithoutExtension`, `Path.GetDirectoryName`, `Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path))` | |
| `GetWorkingDirectory`, `ChangeDirectory`, `MakeDirectory` | `Directory.GetCurrentDirectory`, `Directory.SetCurrentDirectory`, `Directory.CreateDirectory` | |
| `IsPathAbsolute`, `IsFileNameValid` | `Path.IsPathFullyQualified`, `name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0` | |
| `LoadDirectoryFiles`, `LoadDirectoryFilesEx`, `GetDirectoryFileCount`, `GetDirectoryFileCountEx` | `Directory.GetFileSystemEntries(path)`, `Directory.GetFiles(path, "*.png", SearchOption.AllDirectories)`, and their `Length` | raylib's filter is extensions split by `;`, where a search pattern is one |
| `CompressData`, `DecompressData` | `DeflateStream` of `System.IO.Compression` | |
| `EncodeDataBase64`, `DecodeDataBase64` | `Convert.ToBase64String`, `Convert.FromBase64String` | |
| `ComputeMD5`, `ComputeSHA1`, `ComputeSHA256` | `MD5.HashData`, `SHA1.HashData`, `SHA256.HashData` of `System.Security.Cryptography` | The digest as bytes, where raylib's is unsigned ints |
| `TextCopy`, `TextIsEqual`, `TextLength`, `TextFormat` | `=`, `==`, `Length`, an interpolated string `$"Score: {score}"` | `Length` counts UTF-16 units, where raylib's counts bytes |
| `TextSubtext`, `TextFindIndex`, `TextInsert`, `TextInsertAlloc`, `TextAppend` | `Substring`, `IndexOf`, `Insert`, `+` | |
| `TextReplace`, `TextReplaceAlloc`, `TextRemoveSpaces` | `Replace`, `Replace(" ", "")` | |
| `GetTextBetween`, `TextReplaceBetween`, `TextReplaceBetweenAlloc` | `IndexOf` for each end, then `Substring`, or `Remove` and `Insert` | |
| `TextJoin`, `TextSplit`, `LoadTextLines` | `string.Join`, `Split`, `Split('\n')` | |
| `TextToUpper`, `TextToLower` | `ToUpperInvariant`, `ToLowerInvariant` | |
| `TextToSnake`, `TextToPascal`, `TextToCamel` | `JsonNamingPolicy.SnakeCaseLower.ConvertName`, and the words of `Split('_')` joined with their first letters raised, the first lowered again for camel | |
| `TextToInteger`, `TextToFloat` | `int.Parse`, `float.Parse`, or `TryParse` | raylib's stops at the first character that is not a number's and gives 0 for none, where `Parse` throws and `TryParse` answers false |
| `LoadUTF8`, `CodepointToUTF8` | `string.Concat(codepoints.Select(char.ConvertFromUtf32))`, `Encoding.UTF8.GetBytes(char.ConvertFromUtf32(codepoint))` | |
| `GetCodepointCount`, `GetCodepoint`, `GetCodepointNext`, `GetCodepointPrevious` | `text.EnumerateRunes().Count()`, `Rune.DecodeFromUtf16(text.AsSpan(index), out var rune, out var length)`, `Rune.DecodeLastFromUtf16` | A length in UTF-16 units, where raylib's is in bytes |

## Not carried

| raylib | Why |
|---|---|
| `SetLoadFileDataCallback`, `SetSaveFileDataCallback`, `SetLoadFileTextCallback`, `SetSaveFileTextCallback` | The flat API reads files beside the program or in the working directory, and the asset server, which loads a level's models, textures and scenes, reads through the sources a program gives it with `AddSource`, an archive of its own among them |
| `ExportDataAsCode`, `ExportImageAsCode`, `ExportFontAsCode`, `ExportMeshAsCode`, `ExportWaveAsCode` | They write a C header for a C program to compile its data into, where a .NET program embeds a file as a resource of its assembly or ships it beside itself |
| `SetShapesTexture`, `GetShapesTexture`, `GetShapesTextureRectangle` | Shapes are drawn untextured, by the immediate pass's own shader, so there is no texture they are cut from |
| `ImageMipmaps` | An image has one level, and a texture's levels are made on the GPU by `GenTextureMipmaps` |
| `GetPixelColor`, `SetPixelColor` | They read and write a pixel through a C pointer in a format, where an image here is RGBA bytes, read by `GetImageColor` and written by `ImageDrawPixel` |
| `LoadFontData`, `GenImageFontAtlas`, `UnloadFontData` | A font's glyphs are kept by code point in ImGui's atlas, which the flat API's text shares, so there is no glyph data apart from a font |
| `GenMeshTangents`, `GetShaderLocationAttrib` | The vertex layout is fixed and has no tangents, since the model pass works a normal map's frame out per pixel from how the surface changes across the screen |
| `UpdateSound` | It writes into a sound the audio thread is playing, which the audio backend does not open to the program. An `AudioStream` is fed from the program's thread instead |
| `GetGlyphIndex` | A font keeps its glyphs by code point in ImGui's atlas, and `GetGlyphInfo` and `GetGlyphAtlasRec` take the code point, as the row above among the calls that answer otherwise says |

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
| Functions of `raylib.h` carried | 619 | 520 (84 percent) |

raylib's counts repeat exactly from run to run, and this engine's move by about a tenth, with
.NET's compiler and garbage collector in the frame. The cubes are not like for like. raylib's default shader draws them unlit with no shadow, one draw
call each, which is the plain raylib way, and a raylib program could instance them with
`DrawMeshInstanced` and a shader of its own. `models_stress` lights them by a sun with a shadow and
two point lights, beside eight skinned arms, and the engine batches entities sharing a mesh into
instanced draws by itself. RENDERING.md
([§6](../.github/RENDERING.md#beside-raylib)) keeps these numbers, beside what each part of a frame
costs, as they change.

The scene's distance field, which raylib has nothing like, was timed on the same machine on
2026-10-07 in `shaders_scene_field` offscreen at 800 by 450, from `./e3d command profile`. A frame
that stamps its moving crate spends 0.014 ms of the GPU on it, a frame that builds its finest
cascade 0.25 ms (`./e3d command field.rebuild 400` builds one every frame), and the occlusion pass
takes 0.073 ms with the field where it took 0.036 without. The light that bounces takes 0.19 ms of
the GPU at `Low`, 0.27 at `Medium` and 0.34 at `High` in `shaders_cornell_box`, measured on
2026-10-07 the same way with the frame rate unlimited (`./e3d eval "SetTargetFPS(0)"`), and the
guide's table has the memory each takes.

```bash
build/raylib-bench/run.sh
```

builds raylib for the measurement in a scratch folder, runs both pairs and counts the functions,
with `build/raylib-bench/coverage.py` naming those left out by the section of `raylib.h` they are in.
`coverage.py --check`, which the build workflow runs, fails where this page's two tables of the
functions not carried, or its counts, differ from that list.
raylib is not a dependency of the engine.

## See also

- [The window and the frame](window-and-frame.md), where the guide starts
- [DESIGN.md](../.github/DESIGN.md), the rules the flat API follows
