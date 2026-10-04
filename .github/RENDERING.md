# Rendering

What the renderer draws, how a frame moves through it, and the order it grows in. The target is a
forward renderer that a reader can follow end to end in an afternoon, with enough of a frame graph
underneath that shadows, post processing and render targets slot in as nodes, and nothing that
needs an offline toolchain beyond `slangc`.

## What exists

- **A Vulkan device** (`GraphicsDevice`, over Vortice.Vulkan) targeting Vulkan 1.2 with
  `VK_KHR_swapchain`, three frames in flight, mailbox presentation where the surface offers it, a
  depth image of its own, and optional validation layers. Memory comes from `vkAllocateMemory`
  directly. `IGraphicsDevice` is the interface the rest of the engine draws through, and
  `NullGraphicsDevice` stands in for it in tests.
- **Classic render passes.** Every pass is a `VkRenderPass` with framebuffers. Dynamic rendering and
  synchronization2 are not used.
- **A frame in three steps**, each a list of systems in `Renderer`:
  1. **Extract** copies what the frame needs out of the game's `World` into a separate `RenderWorld`
     (`CameraExtract`, `LightExtract`, the draw lists), so the game can change its world while
     the frame is drawn.
  2. **Prepare** uploads what changed (`GpuMeshesPrepare`, `GpuTexturesPrepare`,
     `LightingUboPrepare`) and fills per-frame buffers through `DynamicBufferAllocator`.
  3. **Graph** runs the render graph's nodes in topological order. `MainPassNode` begins and
     clears the swapchain pass, and the model, immediate and ImGui nodes draw into it.
- **The immediate pass** (§2) draws the shapes and textures the flat API records.
- **The model pass** (§3) draws the meshes `DrawModel` records and every mesh entity, which
  `MeshEntityDraws` records through the first camera entity, lit by the light entities or, with
  none, by one fixed light (§4).
- **Shaders** are Slang, compiled to SPIR-V by `slangc` and cached (§1).

## 1. Slang through slangc

Every shader is a `.slang` file holding all of its entry points, and `SlangLoader` turns it into a
`ShaderProgram` with the SPIR-V of each stage. A function marked `[shader("vertex")]` or
`[shader("fragment")]` is an entry point, and its SPIR-V names it `main`. `SlangCompiler` runs
`slangc` per stage with `-target spirv -matrix-layout-column-major`, which gives `mul(matrix,
vector)` the meaning `matrix * vector` has in GLSL over the same bytes, so the engine uploads
`System.Numerics` matrices unchanged.

Each result is cached in `source/.slang-cache` beside the running program, keyed by a hash of the
source, the entry point, the arguments, and every file it imports or includes, looked for beside
the file that names it and then in the import directory as slangc looks, followed through their
imports, with paths written with forward slashes. A machine
without `slangc` reads the cache, an entry whose sources have changed is never used, and a shader
added beside the others leaves their entries valid. `e3d shaders <folder> <cache>` fills a cache
ahead of time, which is how `build/pack.sh` ships the built-in shaders compiled.

`slangc` is a tool rather than a library. `build/fetch-slang.sh` downloads a pinned release into
`build/tools/slang`, and the compiler is looked for in `ENGINE_SLANGC`, then on the `PATH`, then
there. Linking Slang into the engine would add a native library of tens of megabytes to every
game for the sake of compiling while it runs, and running `slangc` as a process does the same with
nothing shipped.

Slang is chosen over GLSL because one language covers vertex, fragment and compute with modules,
generics and interfaces, and because the same source can later target Metal or Direct3D without a
second set of files. What is not built:

- **Reflection beyond uniforms.** `slangc -reflection-json` is read for the uniforms a shader
  declares at the top level, their names, offsets and sizes, which are cached beside the SPIR-V
  (`ShaderProgram.Uniforms`). Descriptor layouts and vertex inputs are still written by hand
  beside each pipeline.
- **Compute.** Only the vertex and fragment stages are compiled.

## 2. The immediate pass

`Draw` calls from the flat API (see [DESIGN.md](DESIGN.md)) record into the `DrawList` resource, a
growing array of 24-byte vertices (position, texture coordinate and color) split into batches. A
batch is a run of consecutive shapes with the same topology (lines or triangles), transform, depth
mode and texture, so a scene of shapes drawn through one camera is two batches. Untextured shapes
sample a white pixel, so one shader draws both. `ImmediateNode` runs after `main_pass` and before
ImGui. It writes the frame's vertices into the dynamic buffer arena in one copy and issues one draw
per batch with `immediate.slang`, the batch's transform a push constant. The list is cleared in
`First`.

A batch also carries a shader id and four `float4` values. Inside `BeginShaderMode`, the batch draws
with the stages of a program the flat API compiled (`ShaderStore`), whose fragment stage, and
vertex stage when it has one, replace the engine's. Every immediate shader imports
`shaders/engine.slang`, which declares the vertex output, the texture binding and the 128-byte push
block (the transform, then the four values), so a program's shader matches the pipeline without
declaring any of it. An unloaded shader's stages and pipelines are destroyed after the frames in
flight that might use them.

This is raylib's rlgl layer in Vulkan terms. It keeps shapes, grids, gizmos and debug lines out of
the ECS and out of the mesh path. Its four pipelines (lines or triangles, depth tested or not)
blend by alpha and do not cull, so a shape's triangles may wind either way. A texel with no
coverage is discarded, so a sprite's empty corners write no depth.

Textures loaded through the flat API go into `TextureStore`, and `GpuTexturesPrepare` uploads them
before the graph runs, keeps one image, view, sampler and descriptor set per texture for every pass
that samples them, and destroys an unloaded or replaced texture's objects four frames later, once
no frame in flight can read them.

## 3. Meshes and materials

Every mesh draws through one pass, `ModelNode`, before the immediate shapes: the flat API's
models and the ECS's mesh entities alike. A mesh is uploaded once into host-visible vertex and index buffers (32-byte
vertices of position, normal and texture coordinate, 32-bit indices) through `MeshStore` and
`GpuMeshesPrepare`, and `DrawModel` records a mesh, a world transform, the camera and a material
each frame. The push constants are the full transform, the world matrix as three rows of a 3x4
(the rotation for normals and the translation for world positions), and the color, 128 bytes,
which every device supports. `model.slang` shades by the frame's lights, or by one fixed light
from above over an ambient floor when the world has none, which is how the flat API's models look.
`model.slang` is built on the `modelpass` module, which a model shader of the program's own imports
too. A draw with one is drawn by a pipeline made from it, and its uniforms, copied when the draw
was recorded, reach it in a uniform buffer at binding 0 of the first descriptor set, where Slang
puts uniforms declared at the top level, beside the texture at binding 1.

A mesh entity is a `Mesh` (positions three per triangle, with optional normals and texture
coordinates) and a `Material`. `MeshEntityDraws` uploads its arrays once, keyed by the positions
array, frees them the first frame no entity draws them, and copies the material's base color,
normal and metallic-roughness texture assets into `TextureStore`. A triangle without normals is
lit by its face's normal.

The push constants hold the transform and the world rows only, 112 bytes. A draw's material is in
its set, the first. Its base color texture, normal map, metallic-roughness map, emissive map and
occlusion map are at bindings 1 to 5, and its factors at binding 6, as plain floats. Its color and
emission are linear, decoded on the CPU, then its metallic, roughness, normal and occlusion
strengths. Binding 6 is a dynamic uniform buffer. The model pass's own draws write their factors
into a ring with a region per frame slot, in 256-byte steps, and share one set per combination of
maps, bound at the offset of each draw's factors, so draws differing only in their factors share a
set. A set no frame in flight binds is freed, and a ring outgrown is replaced by one twice the size.
A draw with a shader of its own writes its factors into the frame's buffer beside its uniforms and
is bound at offset 0.
A normal map's tangent frame is worked out per pixel from the derivatives of the position and the
texture coordinates (Christian Schüler's cotangent frame), so a mesh needs no tangents, and up in
the map is toward the top of the image, as glTF has it.

What follows is where meshes go from there.

A mesh carries position, normal, tangent, two texture coordinates and a color, interleaved, with
32-bit indices. Models come from Assimp, which supplies the mesh data, the material factors and the
texture paths in one pass, and textures are decoded by StbImageSharp with mipmaps generated on the
GPU.

The material holds glTF's metallic-roughness model, which Assimp maps every format it reads
onto, and all of it is drawn. Emission is added after the lights, so it shows with none.
Occlusion darkens the light from all around (ambient lights), and leaves a lamp's light to the
shadow map. A program that needs something else writes a Slang shader that imports `modelpass`.

## 4. Lights and shadows

`LightExtract` copies every `Light` entity into the render world, and `LightingUboPrepare` packs up
to 16 into one uniform buffer per frame, which `ModelRenderer` binds as a second descriptor set
from a ring of one per frame in flight. A `Light` is a kind (directional, point, spot or ambient),
a color, an intensity, a range and a spot's inner and outer angles, and each is one 64-byte entry.
`modelpass.slang` adds each light by Lambert's cosine: a directional light by its direction, an
ambient light everywhere alike, and a point or spot by the square of the distance, brought smoothly
to nothing at its range and cut by a spot's cone. The light that arrives is reflected by the
material's metallic-roughness model. Each light but an ambient one reflects by GGX's
distribution, Smith's height-correlated shadowing and Schlick's Fresnel, toward a camera the
shader finds from the transform alone, since the push constants have no room for its position,
and scatters diffusely what Fresnel leaves, none of it off a metal. An ambient light scatters its
diffuse share and reflects the share Karis's fit of the environment term gives with no
environment to reflect. A light's color times its intensity is the light a white diffuse surface
facing it returns, so the diffuse term is the albedo itself and the specular term is multiplied
by pi to match.

The pass lights in linear space. Frames and render targets stay UNORM and hold sRGB-encoded bytes,
so the immediate pass, text and every 2D color stay byte for byte as raylib draws them. A texture
image can also be viewed as sRGB, and the model pass samples a base color through that view, so
the sampler decodes it before filtering. A draw's color bytes are decoded in the shader, and normal
and metallic-roughness maps are read as linear, as glTF has them. A light's color and intensity
are linear. The pass encodes what it returns, and `toDisplay` does the same for a model shader of
the program's own that works out a color itself.

The sum goes through a tonemap that leaves the brightest channel alone up to 0.9, bends it smoothly
toward 1 past that, and scales the other two channels with it. A sum past one keeps its hue where a
clamp per channel turns it white, and a color below the bend is unchanged. The fixed light of a
world with no light entities goes through the same curve, with no highlight, so a model looks the
same lit by its first light entity as by the fixed light. The curve runs at the end of the model
pass, because the engine has no main color target to run it over. Once one exists it moves into the
post processing chain, and the model pass writes linear light.

An `EnvironmentMap`, a world resource set by `SetEnvironmentMap` from an equirectangular image,
lights a frame from all around. A Radiance `.hdr` file is read as linear floats, so a sun keeps its
brightness past white, and an eight-bit image is decoded from sRGB. It is prefiltered on the CPU into a half-float cube map with faces
64 texels wide, mip 0 for a mirror and each mip after for a roughness of `mip / (mips - 1)`, by
GGX importance sampling with the eye along the normal (Karis's split sum), each sample reading the
image blurred to its solid angle. The model pass looks the mirror direction up at the mip for the
surface's roughness, weighted by Karis's fit, and takes the roughest mip around the normal for the
diffuse share, both darkened by occlusion. The cube is set 1's binding 2, a black cube when there
is none, and the lighting buffer carries its intensity and last mip. With a map set the fixed
light is not used, whether or not there are light entities.

The first directional light with `CastsShadows` set casts the frame's one shadow. `ShadowFit` fits
a 2048 texel depth map to the sphere around the window camera's view out to 40 units, moved in
whole texels so the edges of shadows hold still as the camera moves, and reaching four radii
further toward the light for casters above the view. `ShadowNode` draws the window's meshes into it
before any other pass, with `model.slang`'s vertex stage and no fragment stage, through a
depth-only render pass (`GraphicsDevice.CreateShadowMap`). The map is bound at binding 1 of the
lights' set, beside the light-space matrix in the lighting buffer, and the white texture takes its
place in a frame with no shadow. The shader moves a point off its surface by a texel and a half
along its normal and averages nine comparisons around it. A model shader with a vertex stage of its
own casts the shadow of its mesh as it was before that stage moved it.

What follows is cascades, so near shadows keep their detail over a long view, then point and spot
shadows as an atlas.

## 4. Render targets and post processing

`BeginTextureMode(target)` redirects the calls that follow into an offscreen image, which a later
draw can sample. A target is a color image in the swapchain's format and a depth image in its
depth format (`GraphicsDevice.CreateRenderTarget`), so its render pass is compatible with the
window's and the same pipelines draw into both. Draw list batches and model draws carry the
target they were recorded for, and `TargetsNode` draws each target used in the frame, before the
window's pass, clearing it first and leaving its color image ready to sample. The color image is
registered in `GpuTextures` under the target's texture id, so `DrawTexture` samples it like a
loaded texture.

Post processing is a chain of full-screen Slang passes over the main color target
before it is copied to the swapchain: tonemapping first, then bloom and anti-aliasing (FXAA).

## What the engine needs

### The device

- **Dynamic rendering** (Vulkan 1.3 core), so a pass is a call rather than a render pass object with
  framebuffers to keep in step with the swapchain. Every desktop driver in use exposes it, and it
  removes most of the code in `GraphicsDevice.Swapchain` and `GraphicsDevice.Offscreen`.
- **Synchronization2**, so barriers name their stages and accesses in one structure.
- **The Vulkan Memory Allocator** in place of one allocation per buffer and image, since drivers
  limit the number of allocations and suballocation is a solved problem.
- **A swapchain rebuilt on resize** without a device wait every frame.

### Per frame

- **Timestamp queries** per node, shown in an ImGui panel, so the cost of a pass is visible without
  an external profiler.
- **A screenshot** (`TakeScreenshot(path)`) read back from the swapchain image, which the tests and
  the examples use to check that a frame looks right.

### Debugging

- **Validation layers on in Debug builds** when installed, with every message routed into the
  engine log and every error kept in `GraphicsDevice.ValidationErrors`. CI installs the layer
  beside lavapipe, so an error fails the render test that drew the frame, or the example whose
  log holds it.
- **Object names** through `VK_EXT_debug_utils`, so RenderDoc shows `model pass` instead of a
  handle.

## Order of work

1. Normals and one directional light.
2. Assimp models with textures, and the material struct.
3. Dynamic rendering and synchronization2, then VMA.
4. Tonemapping, as a full-screen pass over a render target, in place of the curve at the end of the
   model pass.
5. Shadow cascades, then point and spot shadows.
6. Bloom and FXAA.
