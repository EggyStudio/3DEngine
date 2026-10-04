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
colors each pixel. The `grayscale.slang` of the `shaders_postprocessing` example:

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

## Values by name

A shader may declare its own uniforms and textures at the top level, which the program finds by
name with `GetShaderLocation`. It returns -1 for a name the shader lacks.

```slang
import engine;

uniform float strength;
Sampler2D detail;
```

```csharp
var strength = GetShaderLocation(shader, "strength");
var detail = GetShaderLocation(shader, "detail");
SetShaderValue(shader, strength, 0.5f);
SetShaderValueTexture(shader, detail, noise);
```

`SetShaderValueMatrix` sets a `float4x4` the same way. A name is looked up once, before the loop,
and the location kept.

## A shader for a model

A model takes a shader through its material, `model.Materials[0].Shader = shader`. Such a shader
imports `modelpass` instead, which gives it `ModelVertexOutput` (position, normal, world position
and texture coordinate), `baseColor(input)` for the material's color and texture, and
`lit(color, input)`, which lights a color by the frame's lights as the engine's own shader does.
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

```csharp
// A model shader of the program's own, its uniforms found by name.
var toon = LoadShader("resources/shaders/toon.slang");
var bands = GetShaderLocation(toon, "bands");
var rimColor = GetShaderLocation(toon, "rimColor");
var viewer = GetShaderLocation(toon, "viewer");

var knot = LoadModelFromMesh(GenMeshKnot(1.1f, 0.25f, 160, 24));
knot.Materials[0].Shader = toon;
// ...
SetShaderValue(toon, bands, (float)levels);
SetShaderValue(toon, rimColor, new Vector4(1f, 0.85f, 0.4f, 0.9f));
SetShaderValue(toon, viewer, camera.Position);
```

`lit` returns a color ready for the screen. A shader that works on the light itself, as this one
does, encodes its result with `toDisplay`.

## Moving vertices

A model shader may have a vertex stage of its own, which takes the mesh's position, normal and
texture coordinate with a `ModelInstance`, hands them to `transformModelVertex` and changes what
it gives back. With `DrawMeshInstanced`, `SV_InstanceID` tells the copies apart, counted from 0.
The `instancing.slang` of the `shaders_mesh_instancing` example gives each of ten thousand cubes
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
was before the stage moved it.

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

```csharp
SetShaderValueBuffer(draw, cellsAt, grids[step % 2]);
// ...
BeginShaderMode(draw);
DrawRectangle(0, 0, Width * CellSize, Height * CellSize, Color.White);
EndShaderMode();
```

A model shader drawn with `DrawMeshInstanced` reads a buffer the same way, each copy picking its
values by `SV_InstanceID`, so particles moved by a compute shader are drawn as meshes. A compute
shader also writes a texture it declares as `RWTexture2D<float4>`, set with
`SetShaderValueTexture`, once the texture has reached the GPU in the frame after it is loaded.

## See also

- Examples: [`shaders_postprocessing`](../3DEngine.Examples/Shaders/ShadersPostprocessing.cs),
  [`shaders_model`](../3DEngine.Examples/Shaders/ShadersModel.cs),
  [`shaders_mesh_instancing`](../3DEngine.Examples/Shaders/ShadersMeshInstancing.cs),
  [`shaders_compute_life`](../3DEngine.Examples/Shaders/ShadersComputeLife.cs), and the shaders
  they load in [`resources/shaders`](../3DEngine.Examples/resources/shaders)
- The cheatsheet's [Shaders](../CHEATSHEET.md#shaders) and [Compute](../CHEATSHEET.md#compute)
- Previous: [Materials, light and shadows](materials-light-and-shadows.md)
