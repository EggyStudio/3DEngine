# Shaders and compute

A shader is a program of the GPU's own, written in Slang, which the engine compiles when it is
loaded. A shader of the program's own changes how 2D drawing or a model looks, and a compute shader
works through data on the GPU, as a simulation of many cells or particles does.

Shaders are written in [Slang](https://shader-slang.org), which reads as HLSL does. The engine's own
shaders come compiled with the package, and a program's own are compiled by `slangc` when they are
loaded, found through `ENGINE_SLANGC` or the `PATH`, and kept compiled in `source/.slang-cache`
beside the program. `e3d shaders` compiles them ahead, so a game ships with no `slangc`, as
[Building](https://github.com/EggyStudio/3DEngine/blob/main/.github/BUILDING.md) says. A shader that
does not compile leaves an invalid `Shader`, which `IsShaderValid` tells, with the compiler's
message in the log.

## A shader for 2D drawing

A shader for shapes, textures and text imports the engine's module, which gives it the vertex's
`VertexOutput` (position, texture coordinate and color), the texture being drawn as `boundTexture`,
and four values the program sets by slot, read as `param(slot)`. It has a fragment stage, which
colors each pixel. The `grayscale.slang` of the `shaders_scene_passes` example:

```slang
// Mixes the picture toward its brightness. param(0).x is how far, from 0 (color) to 1 (gray).
import engine;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    float4 color = input.color * boundTexture.Sample(input.uv);
    float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
    return float4(lerp(color.rgb, float3(gray, gray, gray), param(0).x), color.a);
}
```

`BeginShaderMode` draws everything after it with the shader until `EndShaderMode`. Post
processing is a scene drawn into a render texture, then drawn to the window through a shader, which
the example does with two:

<!-- compiled with:
float time = 0;
-->
```csharp
var wave = LoadShader("resources/shaders/wave.slang");
var grayscale = LoadShader("resources/shaders/grayscale.slang");
// All the way to gray, which holds for every frame after.
SetShaderValue(grayscale, 0, 1f);
var scene = LoadRenderTexture(380, 300);
// ...
// The same image through two shaders.
SetShaderValue(wave, 0, time);
BeginShaderMode(wave);
DrawTexture(scene.Texture, 10, 60, Color.White);
EndShaderMode();

BeginShaderMode(grayscale);
DrawTexture(scene.Texture, 410, 60, Color.White);
EndShaderMode();
```

`SetShaderValue(shader, slot, value)` with a slot from 0 to 3 sets `param(slot)`, a `float`, a
`Vector2`, `Vector3`, `Vector4` or an `int`. A value set before a draw is the one that draw uses,
so one shader draws with several in a frame, and a value set once holds for every frame after.
`LoadShaderFromMemory` compiles source from a string rather than a file.

A shader may write the depth of each pixel as well as its color, returning a struct with a
`float depth : SV_Depth` beside its `SV_Target`. What is drawn after is tested against that
depth, in 3D and in 2D once `rlEnableDepthTest` turns the test on outside a 3D mode.
`shaders_hybrid_rendering` raymarches a scene across a rectangle, each pixel's depth how far its
ray went, and rasterizes cubes into the same target after it, and `shaders_depth_writing` turns
the test around.

## Values by name

A shader may declare its own uniforms and textures at the top level, which the program finds by
name with `GetShaderLocation`. It returns -1 for a name the shader lacks.

```slang
import engine;

uniform float strength;
Sampler2D detail;
```

<!-- compiled with:
Shader shader = default;
Texture2D noise = default;
-->
```csharp
var strength = GetShaderLocation(shader, "strength");
var detail = GetShaderLocation(shader, "detail");
SetShaderValue(shader, strength, 0.5f);
SetShaderValueTexture(shader, detail, noise);
```

`SetShaderValueMatrix` sets a `float4x4` the same way. A name is looked up once, before the loop,
and the location kept.

A texture may be declared apart from its sampler, as HLSL writes it, a `Texture2D` read through a
`SamplerState`. The sampler has a location of its own, set with `SetShaderValueTexture` to the
texture whose filter and wrapping it samples with, and it samples as the white texture of an
untextured draw does, nearest and repeating, until it is set:

```slang
Texture2D detail;
SamplerState detailSampler;
// ...
return detail.Sample(detailSampler, input.uv);
```

## A shader for a model

A model takes a shader through its material, `model.Materials[0].Shader = shader`. Such a shader
imports `modelpass` instead, which gives it `ModelVertexOutput` (position, normal, world position
and texture coordinate), `baseColor(input)` for the material's color and texture, and
`lit(color, input)`, which lights a color by the frame's lights as the engine's own shader does. A
shader that takes `ModelStreamsOutput` in its place has the same in its `base` with the mesh's
`vertexColor` and second texture coordinate `uv2` beside them, white and zero where the mesh has
none, and its `baseColor(input)` multiplies the mesh's color in.
The `toon.slang` of the `shaders_model` example cuts that light into bands:

```slang
import modelpass;

uniform float bands;
uniform float4 rimColor;
uniform float3 viewer;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float4 color = baseColor(input);
    float3 n = normalize(input.normal);

    // The light the model pass would give, cut into steps.
    float3 shaded = lit(float3(1.0), input);
    float level = floor(saturate(dot(shaded, float3(0.333))) * max(bands, 1.0) + 0.5) / max(bands, 1.0);

    // Strongest where the surface turns away from the viewer.
    float rim = pow(1.0 - saturate(dot(n, normalize(viewer - input.world))), 3.0);
    // The base color is linear, so it is encoded for the frame, and the rim, a color the program
    // set, is added as it is.
    return float4(toDisplay(color.rgb * level) + rimColor.rgb * rimColor.a * rim, color.a);
}
```

Its uniforms are set by name each frame, as any shader's are:

<!-- compiled with:
Camera3D camera = default;
int levels = 4;
-->
```csharp
// A model shader of the program's own, its uniforms found by name.
var toon = LoadShader("resources/shaders/toon.slang");
var bands = GetShaderLocation(toon, "bands");
var rimColor = GetShaderLocation(toon, "rimColor");
var viewer = GetShaderLocation(toon, "viewer");

var knot = LoadModelFromMesh(GenMeshKnot(1.8f, 1.4f, 24, 160));
knot.Materials[0].Shader = toon;
// ...
SetShaderValue(toon, bands, (float)levels);
SetShaderValue(toon, rimColor, new Vector4(1f, 0.85f, 0.4f, 0.9f));
SetShaderValue(toon, viewer, camera.Position);
```

`lit` returns a color ready for the screen. A shader that works on the light itself, as this one
does, encodes its result with `toDisplay`.

`lit` lights the input by the normal it holds, so a shader may sample the light some other face
would take by handing it an input of its own, such as one with its normal turned up for the light
a face turned to the sky takes, wherever the point lies. On a double-sided material alone, whose
backs are drawn, a normal facing away from the eye is turned toward it first, so a back is lit as
its front, a sample's normal with it.

`litLight(albedo, input)` gives the light `lit` encodes, without the material's emission, in its two
shares, `diffuse` and `specular`, and takes the shadow of the light that casts one from its caller
where the program reads it itself. A shader that steps the sun's shadow along a grid of its own reads it once, at the middle
of the step the point lies in, through `lightReaching` from the `lights` module, and hands that read
over with how much of the point the shadow's cascades hold, which the sun's contact shadows go by:

```slang
import modelpass;
import lights;

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    float3 albedo = baseColor(input).rgb;
    float3 n = normalize(input.normal);
    // The middle of the eighth of a unit the point lies in, across the face.
    float3 middle = lerp(input.world, (floor(input.world * 8.0) + 0.5) / 8.0, step(abs(n), float3(0.5)));
    float held;
    LitLight light = litLight(albedo, input, lightReaching(middle, n, held), held);
    return float4(toDisplay(light.diffuse + light.specular), 1.0);
}
```

## Moving vertices

A model shader may have a vertex stage of its own, which takes the mesh's position, normal and
texture coordinate with a `ModelInstance`, hands them to `transformModelVertex` and changes what
it gives back. One that reads the mesh's color and second texture coordinate takes them as
`float4 color : COLOR0` and `float2 uv2 : TEXCOORD1` and hands them to `transformModelVertex`
before the instance, which then gives back a `ModelStreamsOutput`. Each input is fed by its
semantic, `POSITION`, `NORMAL`, `TEXCOORD0`, `COLOR0` and `TEXCOORD1`, so a stage may take them in
any order, and one that takes an input no stream gives is named in a warning. With `DrawMeshInstanced`, `SV_InstanceID` tells the copies apart, counted
from 0.
The `instancing.slang` of the `shaders_instance_hues` example gives each of ten thousand cubes
its own hue:

```slang
[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0,
    ModelInstance instance, uint id : SV_InstanceID)
{
    ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
    output.color = float4(hue(id / max(count, 1.0)) * 0.8 + 0.2, 1);
    return output;
}

[shader("fragment")]
float4 fragmentMain(ModelVertexOutput input) : SV_Target
{
    return float4(lit(input.color.rgb, input), 1);
}
```

A vertex stage that moves vertices, as grass in the wind does, casts the shadow of its mesh as it
was before the stage moved it, unless its material is masked. A masked surface drawn with a shader
of the program's own that has a vertex stage, or a fragment stage named `shadowMain`, is drawn into
the shadow maps, and into the depth ambient occlusion reads, by the vertex stage it is drawn with,
the program's own where it has one, run with the light in the camera's place, so a stage that reads
the camera's position casts the shadow of the surface as the light sees it. It is cut by the
program's `shadowMain` where it has one, and otherwise by the material's texture at the coordinate
the stage gives, at the material's cutoff, as the model pass cuts it. So plants whose stage gives
each corner its tile's coordinate in an atlas cast the shadows of their blades, and a shader that
samples an atlas of its own cuts the shadow by it as its fragment stage cuts the surface:

```slang
Sampler2D atlas;

[shader("fragment")]
void shadowMain(ModelVertexOutput input)
{
    if (atlas.Sample(input.uv).a < 0.5)
        discard;
}
```

A mesh drawn with a shader of the program's own is drawn whole in every pass, the camera's, the
depth's, each shadow cascade's and the lights', since its vertex stage may move the vertices past
the mesh's box. A stage that keeps the positions where the instance places them, as one that only
unpacks a voxel chunk's normals from its stream does, is said so with
`SetShaderKeepsPositions(shader, true)`, and a mesh drawn with it is then left out of each pass that
cannot see the mesh's box, as a mesh drawn with the model pass's own shader is, so a chunk outside a
shadow cascade's box is not drawn into it.

## A vertex stream of the program's own

A mesh may carry one stream of the program's own beside its position, normal and texture
coordinates, a value each vertex holds that the mesh's own streams do not say, such as a voxel's
face and texture packed into four bytes, a blend weight or a wind phase. It is a `MeshStream` of one
of five formats, `UInt`, `UInt2`, `Float`, `Float4` and `UNorm8x4`, four bytes read as 0 to 1, given
to `UploadMesh` beside the colors and second texture coordinates, and written in place by
`UpdateMeshBuffer` at `MeshStreamBuffer`, which writes the stream alone and uploads nothing else of
the mesh again:

<!-- compiled with:
ModelVertex[] vertices = [];
uint[] indices = [];
-->
```csharp
// Four bytes a vertex, a palette index in the low two bits and a shade in the eight above them.
var packed = new uint[vertices.Length];
for (int i = 0; i < packed.Length; i++) packed[i] = (uint)(i % 4) | 255u << 2;
var town = UploadMesh(vertices, indices, null, null, MeshStream.From(MeshStreamFormat.UInt, packed.AsSpan()));
// ...
packed[0] = 3 | 200u << 2;
UpdateMeshBuffer(town, MeshStreamBuffer, packed.AsSpan(), 0);
```

A vertex stage of the program's own reads it as `CUSTOM0`, of a Slang type of the format's class,
`uint` or `uint2` for a stream of integers and a float type for the rest, since Vulkan reads an
attribute of one class into an input of the other as nothing it defines. Bytes packed for the
shader are a `UInt` and not a `Float`, because a float's bits are read as a float, so a value whose
bits 23 to 30 are all set is not a number and one whose are all clear is a number so small a GPU may
read it as zero. The stage unpacks them into what it hands on, here the color the fragment stage
lights:

```slang
static const float3 Palette[4] = { float3(0.9, 0.3, 0.2), float3(0.2, 0.7, 0.4), float3(0.2, 0.5, 0.9), float3(0.9, 0.8, 0.3) };

[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0, uint packed : CUSTOM0,
    ModelInstance instance)
{
    ModelVertexOutput output = transformModelVertex(position, normal, uv, instance);
    float shade = ((packed >> 2) & 0xff) / 255.0;
    output.color = float4(Palette[packed & 3] * shade, 1);
    return output;
}
```

The model pass's own shader does not read the stream, and a stage that reads it drawn with a mesh
without one reads zero, so one shader serves meshes with the stream and without it.

The stream is a vertex stream rather than a `ShaderBuffer` read by the vertex's index, since it
travels with the mesh, uploaded once with its positions and triangles and read by every draw of
the mesh with nothing more bound, where a buffer is bound to the shader, so a few thousand chunks
of a world would each need a `SetShaderValueBuffer` before their draw, or one buffer of them all
with each chunk's offset carried into the shader. The GPU fetches the stream by the vertex's index
as it fetches the position, where a buffer is read by `SV_VertexID` by hand, with an offset for
each copy of an instanced draw; the fetch costs about the same either way, so what is gained is in
how it is bound. A buffer stays the way for what is not one a vertex, such as a palette or an
atlas's table every chunk shares, a chunk's own constants, or what a compute pass reads too.

A mesh whose normal and texture coordinate its stream packs, as a voxel world's chunk does, is
uploaded from its positions alone, `UploadMesh(positions, indices, stream, colors)`, twelve bytes a
vertex where a whole one is thirty-two, so its vertex with four packed bytes is sixteen. Its vertex
stage reads the normal and texture coordinate as zero and unpacks them, as the `packed.slang` of
the `shaders_packed_vertices` example does for a hill of voxels, the face a vertex lies on in the
low three bits, its corner of the face in the next two and its tile in the eight above:

```slang
[shader("vertex")]
ModelVertexOutput vertexMain(float3 position : POSITION, uint packed : CUSTOM0, ModelInstance instance)
{
    ModelVertexOutput output = transformModelVertex(position, Normals[packed & 7], Corners[(packed >> 3) & 3], instance);
    output.color = float4(float((packed >> 5) & 0xff), 0, 0, 1);
    return output;
}
```

The model pass's own shader lights such a mesh by a zero normal, so it is drawn with a shader of
the program's own, which a warning says once where it is not. Its shadow is cast by its positions
as any mesh's is, and the light field, the glow lights, picking and bodies read its positions with
zero normals. Its colors, where given, tint the light it bounces and are not given to the GPU,
which reads white and zero for a stage that takes `COLOR0` or `TEXCOORD1` on it. It takes no skin, `UpdateMeshBuffer` writes its positions at 0 and leaves its
normals and texture coordinates alone with a warning, and the pass that scatters light under a
surface leaves it out.

## Compute shaders

A compute shader has a `[shader("compute")]` function run over a grid of threads, and reads and
writes storage buffers the program makes. `LoadShaderBuffer` makes one, of a size in bytes or
holding an array, and `SetShaderValueBuffer` hands it to a shader by name. The `life.slang` of the
`shaders_compute_life` example steps Conway's Game of Life a generation a frame:

```slang
uniform uint width;
uniform uint height;
StructuredBuffer<uint> current;
RWStructuredBuffer<uint> next;

[shader("compute")]
[numthreads(16, 16, 1)]
void computeMain(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= width || id.y >= height)
        return;
    // ...
    uint cell = id.y * width + id.x;
    next[cell] = (neighbors == 3 || (neighbors == 2 && current[cell] == 1)) ? 1 : 0;
}
```

The program keeps two grids and swaps which it reads and which it writes each step.
`ComputeShaderDispatch` runs the shader over groups of its threads, here 16 by 16 a group, so the
groups cover the grid:

<!-- compiled with:
const int Width = 256, Height = 144;
uint[] start = [];
int step = 0;
-->
```csharp
var grids = new[] { LoadShaderBuffer<uint>(start), LoadShaderBuffer(Width * Height * 4) };

var life = LoadComputeShader("resources/shaders/life.slang");
SetShaderValue(life, GetShaderLocation(life, "width"), Width);
SetShaderValue(life, GetShaderLocation(life, "height"), Height);
var (currentAt, nextAt) = (GetShaderLocation(life, "current"), GetShaderLocation(life, "next"));
// ...
SetShaderValueBuffer(life, currentAt, grids[step % 2]);
SetShaderValueBuffer(life, nextAt, grids[(step + 1) % 2]);
ComputeShaderDispatch(life, (Width + 15) / 16, (Height + 15) / 16, 1);
step++;
```

A dispatch runs on the GPU before the frame being drawn, while the program goes on.
`UpdateShaderBuffer` writes into a buffer from the CPU, as the example does where the mouse draws
cells, and `ReadShaderBuffer` reads one back, waiting for the dispatches before it, which costs a
wait and is kept for what the CPU needs to know.

## Reading a buffer back without a wait

`ReadShaderBuffer` waits for the dispatches before it, which holds the program until the GPU has
caught up, a millisecond or more a read. That is for what the CPU needs at once. What it can take a
frame or two later, as the blocks a generator on the GPU worked out for a column of a world, is
asked for with `ReadShaderBufferAsync` and read once it is ready, the program going on meanwhile:

<!-- compiled with:
Shader generate = default;
ShaderBuffer cells = default;
var asked = new Queue<ShaderReadback>();
void Place(uint[] column) { }
-->
```csharp
ComputeShaderDispatch(generate, 512, 1, 1);
asked.Enqueue(ReadShaderBufferAsync(cells));
// ...
while (asked.Count > 0 && IsShaderReadbackReady(asked.Peek()))
{
    var readback = asked.Dequeue();
    var column = new uint[readback.Size / 4];
    GetShaderReadback<uint>(readback, column);
    UnloadShaderReadback(readback);
    Place(column);
}
```

The copy goes to the GPU with the frame, after every dispatch asked before it and before any after,
so the readback is ready a frame or two on, never in the frame it was asked in. Each holds a buffer
of its own until it is unloaded, which goes back to a pool the next readback of its size takes from,
so a game asking every frame keeps a few. `GetShaderReadback` on one not yet ready waits for it, as
`ReadShaderBuffer` waits. A size of 0 reads from the offset to the buffer's end.

## Drawing what a compute shader wrote

A shader that draws reads the same buffer as a `StructuredBuffer`, so what a dispatch wrote reaches
the screen with no copy through the CPU. The example's `life_draw.slang` colors each pixel from
the cell under it, drawn over one rectangle the size of the window:

```slang
uniform uint width;
uniform uint height;
uniform float cellSize;
StructuredBuffer<uint> cells;

[shader("fragment")]
float4 fragmentMain(VertexOutput input) : SV_Target
{
    uint x = min(uint(input.position.x / cellSize), width - 1);
    uint y = min(uint(input.position.y / cellSize), height - 1);
    return cells[y * width + x] == 1 ? float4(1.0, 0.878, 0.275, 1) : float4(0.078, 0.118, 0.157, 1);
}
```

<!-- compiled with:
const int Width = 256, Height = 144, CellSize = 4;
Shader draw = default;
int cellsAt = 0, step = 0;
ShaderBuffer[] grids = [];
-->
```csharp
SetShaderValueBuffer(draw, cellsAt, grids[step % 2]);
// ...
BeginShaderMode(draw);
DrawRectangle(0, 0, Width * CellSize, Height * CellSize, Color.White);
EndShaderMode();
```

A model shader drawn with `DrawMeshInstanced` reads a buffer the same way, each copy picking its
values by `SV_InstanceID`, so particles moved by a compute shader are drawn as meshes.

## A mesh a compute shader writes

A mesher in compute writes a mesh's vertices and how many there are into shader buffers, which
`LoadMeshFromShaderBuffers` makes a mesh of with nothing read back. The `shaders_compute_mesh`
example meshes a hill of voxels each frame as waves roll across it:

<!-- compiled with:
const int Capacity = 96 * 1024;
Shader mesher = default;
ModelMaterial material = new(Color.White);
-->
```csharp
var positions = LoadShaderBuffer(Capacity * 12);   // three floats a vertex
var packed = LoadShaderBuffer(Capacity * 4);       // four bytes a vertex its shader unpacks
var counter = LoadShaderBuffer(4);                 // how many vertices, a uint
var hill = LoadMeshFromShaderBuffers(positions, packed, MeshStreamFormat.UInt, counter, Capacity);
SetMeshBoundingBox(hill, new BoundingBox(new Vector3(-12, 0, -12), new Vector3(12, 12, 12)));
// ...
ComputeShaderDispatch(mesher, 3, 3, 1);
DrawMesh(hill, material, Matrix4x4.Identity);
```

The shader writes three floats a vertex, every three vertices a triangle with no indices, and the
count into the first four bytes of its buffer. One that adds each face's six vertices to the count,
as the example's `mesher.slang` does with `InterlockedAdd`, puts the faces wherever they fall. The
engine turns the count into the draw's arguments on the GPU before the frame draws, held to the
capacity, so a dispatch and the draw after it share the buffers with no copy and no wait. For a
frame of 500 such meshes under a sun that casts shadows that takes 0.02 ms of the GPU and 0.09 ms of
the CPU at 800 by 450 on an RTX 4070 Laptop GPU, as `./e3d command profile` gives it under
`mesh_counts`.

The mesh is drawn by `DrawMesh`, casts its shadow and is culled by the box `SetMeshBoundingBox`
gives it, drawn wherever it is until it has one. The model pass's own shader lights it by a zero
normal, as it lights any mesh of positions alone, so a shader of the program's own unpacks its
normal from the stream it reads as `CUSTOM0`, as the example's `packed.slang` does. Its vertices
are on the GPU alone, so picking and bodies pass it over, and a reflection traced through the
device's rays does not see it. `UnloadMesh` frees the mesh and not its buffers, and a buffer
unloaded first unloads the meshes over it.

Global illumination reads its triangles into the scene's distance field, in the color of a stream
of `UNorm8x4` where it has one, once the mesh has a box. A dispatch that binds the mesh's buffers
marks it written, its count is read back without a wait, and a frame or two later the field takes
as many triangles as the count says, written into the field's own buffer on the GPU from the
positions. A mesh written again every frame is taken as its box, as a mesh that moves is. Five
hundred meshes of 1,024 triangles took 500 dispatches and 2.6 to 3.4 ms of the GPU in the frame
they were taken in, once, and 48 MB of the field's buffer, where keeping room for each one's
capacity of 24,000 vertices would have held 192 MB.

## Writing a texture

A compute shader also writes a texture it declares as `RWTexture2D<float4>`, set with
`SetShaderValueTexture`, once the texture has reached the GPU in the frame after it is loaded. The
`shaders_compute_texture` example paints a moving plasma into one, a thread a texel
(`plasma.slang`), and draws it as any texture:

```csharp
var texture = LoadTextureFromImage(GenImageColor(256, 256, Color.Black));
var plasma = LoadComputeShader("resources/shaders/plasma.slang");
var timeAt = GetShaderLocation(plasma, "time");
SetShaderValueTexture(plasma, GetShaderLocation(plasma, "image"), texture);
// ...
// Painted after each frame for the next, which the dispatch runs on the GPU ahead of,
// since a texture reaches the GPU in the frame after it is loaded.
SetShaderValue(plasma, timeAt, (float)GetTime());
ComputeShaderDispatch(plasma, 256 / 8, 256 / 8, 1);
```

A render texture's colors are written the same way. Where the GPU cannot store to a color's format,
as some cannot store the window's eight bits a channel in BGRA order, the shader writes a stand-in of
a format it does store, which the dispatch fills from the render texture first and copies into it
after, each channel into its own, two copies of the image a dispatch. The shader writes a mipmapped
texture's first level, and the dispatch makes the smaller levels again from it.

## See also

- Examples: [`shaders_postprocessing`](../3DEngine.Examples/Shaders/ShadersPostprocessing.cs),
  [`shaders_scene_passes`](../3DEngine.Examples/Shaders/ShadersScenePasses.cs),
  [`shaders_model`](../3DEngine.Examples/Shaders/ShadersModel.cs),
  [`shaders_lightmap_rendering`](../3DEngine.Examples/Shaders/ShadersLightmapRendering.cs),
  [`shaders_mesh_instancing`](../3DEngine.Examples/Shaders/ShadersMeshInstancing.cs),
  [`shaders_instance_hues`](../3DEngine.Examples/Shaders/ShadersInstanceHues.cs),
  [`shaders_compute_life`](../3DEngine.Examples/Shaders/ShadersComputeLife.cs),
  [`shaders_compute_texture`](../3DEngine.Examples/Shaders/ShadersComputeTexture.cs),
  [`shaders_compute_mesh`](../3DEngine.Examples/Shaders/ShadersComputeMesh.cs), and the shaders
  they load in [`resources/shaders`](../3DEngine.Examples/resources/shaders)
- The game [`games/Sumo`](../games/Sumo/Program.cs), whose marbles, crowd and floor are drawn by
  shaders of its own and whose floor a compute shader paints each frame
- The cheatsheet's [Shaders](../CHEATSHEET.md#shaders) and [Compute](../CHEATSHEET.md#compute)
- Previous: [Global illumination](global-illumination.md)
- Next: [Audio](audio.md)
