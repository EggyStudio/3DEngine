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
  `MeshEntityDraws` records through each camera entity, lit by the light entities or, with
  none, by one fixed light (§4).
- **Shaders** are Slang, compiled to SPIR-V by `slangc` and cached (§1).
- **A frame profile** of the schedule's stages, the renderer's steps and each pass on the CPU and
  the GPU, with two stress examples that find how much a frame holds (§6).

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

- **Reflection beyond what a program sets.** `slangc -reflection-json` is read for the
  uniforms, textures and storage buffers a shader declares at the top level, which are cached
  beside the SPIR-V (`ShaderProgram.Uniforms`, `Textures` and `Buffers`). Descriptor layouts and
  vertex inputs are still written by hand beside each pipeline.

Compute shaders are compiled from a function marked `[shader("compute")]`, and their storage
buffers found in the reflection as structured buffers, apart from textures. A dispatch
(`GraphicsDevice.Dispatch`) is submitted at once to the queue the frames use, in a command buffer
of its own with a barrier before it against every earlier write and one after it for every later
read and the CPU's, and a fence the next dispatch or a wait frees its objects by. It runs after
the frames already submitted and before the one being recorded, which is submitted at
`Stage.Last`. Storage buffers are host-visible, so `ReadShaderBuffer` and `UpdateShaderBuffer`
wait for the dispatches in flight and copy through the mapping. `UpdateShaderBuffer` does not wait
for the frames in flight, so one still drawing from a buffer may see the write.

A compute shader writes a texture it declares as `RWTexture2D`, which the reflection tells from a
sampled one by its access, and samples a `Sampler2D`, both set with `SetShaderValueTexture`.
Textures are made with storage usage, and their sRGB views for sampling only, since an sRGB format
cannot be storage. The image a dispatch writes is moved to the general layout in its command buffer
and back to the one textures are sampled in after it, and the shader's image carries no format,
which the device's `shaderStorageImageWriteWithoutFormat` feature, enabled where it is supported,
allows. A texture reaches the GPU in the frame after it is loaded, so a dispatch before then is
skipped with the reason in the log.

A shader that draws reads storage buffers too, declared as `StructuredBuffer`, at the bindings
Slang gives them in its first set. The buffers set on it travel with each draw after the
textures in the draw's snapshot of its own textures, and the immediate and model passes bind them
in the set a shader with resources of its own has, from `ShaderBufferStore`, which the render
world is handed. A buffer a draw was not given is bound to 16 zero bytes. The dispatch's barrier
after it makes what it wrote visible to the frame drawn after it, so a simulation stepped on the
GPU is drawn with no copy through the CPU, as `shaders_compute_life` draws its grid.

## 2. The immediate pass

`Draw` calls from the flat API (see [DESIGN.md](DESIGN.md)) record into the `DrawList` resource, a
growing array of 24-byte vertices (position, texture coordinate and color) and a growing array of
32-bit indices into it, split into batches. A quad, which every sprite, glyph and rectangle is, is
four vertices and six indices. A batch is a run of consecutive shapes with the same topology (lines
or triangles), transform, depth mode, texture, blend mode and scissor, so a scene of shapes drawn
through one camera is two batches. Untextured shapes sample a white pixel, so one shader draws
both. `ImmediateNode` runs after `main_pass` and before ImGui. It writes the frame's vertices and
indices into the dynamic buffer arena in one copy each and issues one indexed draw per batch with
`immediate.slang`, the batch's transform a push constant. The list is cleared in `First`.

A batch also carries a shader id and four `float4` values. Inside `BeginShaderMode`, the batch draws
with the stages of a program the flat API compiled (`ShaderStore`), whose fragment stage, and
vertex stage when it has one, replace the engine's. Every immediate shader imports
`shaders/engine.slang`, which declares the vertex output, the texture binding and the 128-byte push
block (the transform, then the four values), so a program's shader matches the pipeline without
declaring any of it. Uniforms a shader declares at the top level, which Slang puts in a uniform
buffer at binding 0, travel with the batch as the bytes they held when it was recorded, and the
batch binds a set of its own with them beside its texture, from a ring kept per frame in flight.
Textures a shader declares beyond the module's, found by name in Slang's reflection, take the
bindings Slang gave them, binding 0 among them for a shader with no uniforms, so such a shader
has a descriptor layout of its own built from them, and the batch's set holds the textures
`SetShaderValueTexture` set, as they were when it was recorded. A model shader's are the same,
beside the material's maps.
An unloaded shader's stages and pipelines are destroyed after the frames in flight that might use
them.

This is raylib's rlgl layer in Vulkan terms. It keeps shapes, grids, gizmos and debug lines out of
the ECS and out of the mesh path. Its pipelines (lines or triangles, depth tested or not) do not
cull, so a shape's triangles may wind either way, and blend by alpha unless a batch was recorded
inside `BeginBlendMode`, which makes a pipeline for each of raylib's modes the batch asks for. Alpha
is laid over by alpha in every mode, so a render target keeps the coverage of what was drawn into
it. A batch recorded inside `BeginScissorMode` carries its rectangle, clipped to the target, and
the pass sets the scissor to it and back to the whole target after its last batch. A texel with no
coverage is discarded, so a sprite's empty corners write no depth.

Textures loaded through the flat API go into `TextureStore`, and `GpuTexturesPrepare` uploads them
before the graph runs, keeps one image, view, sampler and descriptor set per texture for every pass
that samples them, and destroys an unloaded or replaced texture's objects four frames later, once
no frame in flight can read them. A texture's filter and wrap are its sampler's, so changing either
replaces the sampler and set and keeps the image.

## 3. Meshes and materials

Every mesh draws through one pass, `ModelNode`, before the immediate shapes: the flat API's
models and the ECS's mesh entities alike. A mesh is uploaded once into host-visible vertex and index buffers (32-byte
vertices of position, normal and texture coordinate, 32-bit indices) through `MeshStore` and
`GpuMeshesPrepare`, and `DrawModel` records a mesh, a world transform, the camera and a material
each frame. Draws of the model pass's own shader that share a mesh and its five maps are one
instanced draw, in the order each such batch first appears. Each draw is an instance of 96 bytes
in a second vertex buffer stepped per instance (`ModelRenderer.Instance`, `ModelInstance` in the
shader), holding the world matrix as three rows of a 3x4 (the rotation for normals and the
translation for world positions) and the material's factors. The camera's view-projection is a
push constant for the batch (`modelPush`), as a light's is in the shadow pass, so draws recorded
through two cameras are batched apart. `model.slang` shades by the frame's lights, or by one fixed light
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

A draw's maps are in its set, the first. Its base color texture, normal map, metallic-roughness
map, emissive map and occlusion map are at bindings 1 to 5. Its factors are in its instance, its
color and emission linear, decoded on the CPU, then its metallic, roughness, normal and occlusion
strengths, and the vertex stage hands them to the fragment stage unblended across the triangle.
The instances are written into a ring kept mapped, with a region per frame slot, the shadow pass's
and each target's one after another, and a ring outgrown is replaced by one twice the size. The
model pass's own draws share one set per combination of maps, so draws differing only in their
factors share a set and a draw call. A set no frame in flight binds is freed. A draw with a shader
of its own is a batch of one, with a set of its own holding its uniforms.

A material is double-sided unless it says otherwise, as a glTF file can, whose draws are batched
apart and drawn by a pipeline that culls back faces. The back of a double-sided face is lit by its
normal turned toward the viewer, as glTF has it. A format that does not say, as OBJ, is
double-sided.

A material's alpha mode says what its alpha means, as glTF's does. Opaque ignores it. Mask cuts
the surface out where the color times the texture is below the cutoff and leaves the rest solid,
in the opaque batches. Blend, the default for a material the program makes, lets what is behind
show through. The mode reaches the fragment stage in the instance's emission `w` (below zero for
opaque, the cutoff for mask, zero for blend), read by `alphaTested`. A tint with alpha on a model
whose file says opaque blends it, as raylib's tint fades a model.

The pipeline blends by alpha and writes depth, so a draw that lets what is behind it show must
come after that. A draw is translucent when it blends and its color has alpha below 255 or its
base color texture has a pixel neither clear nor solid (`TextureStore.IsTranslucent`, found when
the pixels are uploaded). It stays out of the opaque batches and is drawn after all of them, in
the order the program recorded it, batched only with the draws beside it that share its mesh and
set. `MeshEntityDraws` records its translucent entities after the opaque ones, from the farthest
from the camera to the nearest.
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

A skinned mesh is posed on the GPU. When a model is loaded, each skin hands the mesh store its
four joints and weights a vertex (`MeshStore.SetSkin`), and `GpuMeshes` makes a `GpuSkin` for it:
the vertices at rest, the joints and the weights in storage buffers, a vertex buffer the posed
vertices are written into, which the model pass draws, and a ring of buffers for the joints'
matrices, one more than there are frames a buffer is read in. A pose hands over only the
matrices (`MeshStore.PoseSkin`), and `SkinningNode`, the first node of the graph, records for
each mesh posed that frame a dispatch of `skin.slang` into the frame's command buffer, with a
barrier before it that waits for earlier frames to finish reading the vertex buffer and one after
it that makes the posed vertices visible to the shadow and model passes. A matrix is four rows,
applied as System.Numerics applies a matrix to a row vector, so the CPU's layout is read as it is.
With no renderer the CPU poses the vertices itself, as it did before, and they are the mesh's own.

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
brightness past white, and an eight-bit image is decoded from sRGB. It is prefiltered on the CPU
into a half-float cube map with faces 64 texels wide, mip 0 for a mirror and each mip after for a
roughness of `mip / (mips - 1)`, by GGX importance sampling with the eye along the normal (Karis's
split sum), each sample reading the image blurred to its solid angle. The model pass looks the
mirror direction up at the mip for the surface's roughness, weighted by Karis's fit, and lights the
diffuse share by the image's irradiance, both darkened by occlusion. The irradiance is the image
projected onto the nine spherical harmonics of bands 0 to 2 on the CPU, each band scaled by its
share of a cosine lobe (Ramamoorthi and Hanrahan), so a surface takes the light of the whole half of
the sky it faces. The cube is set 1's binding 2, a black cube when there is none, and the lighting
buffer carries its intensity, its last mip and the nine coefficients, which come last so no other
field moves. With a map set the fixed light is not used, whether or not there are light entities.

The map keeps a second cube for the sky, at a quarter of the image's width a face up to 512
texels, resampled with no prefiltering, at set 1's binding 3. `DrawSkybox` records a model draw
of a cube around the camera with `sky.slang`, which looks the sky cube up along the way from the
eye through each pixel and sets its depth a millionth inside the far plane, so whatever else the frame
draws is in front. The draw goes through the model pass's tonemap like a reflection does, and is
left out of the shadow map (`ModelDraw.CastsShadow`).

A `ReflectionProbe` entity, which `CreateReflectionProbe` makes, is a box whose surfaces reflect
what is around its middle rather than the environment map. `ProbeNode`, after the window's shadow
and before its passes, captures the first probe out of date, one a frame: the window's batches
are drawn through six views of a right angle from the probe's middle into render targets of 64
texels (`ModelRenderer.Draw` with a view-projection pushed in place of each batch's), lit by the
window's lighting buffer at a quarter exposure (`environment.w`, which `toDisplay` scales by, so
light up to four times the tonemap's knee survives the eight bits), cleared to the window's clear
color, and read back at the end of the frame (`GraphicsDevice.RequestReadback`,
a stall a capture can take). A worker thread maps each direction to the face looking most nearly
along it, through that face's own view-projection, decodes the color, undoes the tonemap and the
exposure, and
prefilters the result as an environment map of faces 32 texels wide with its irradiance
(`EnvironmentMap.FromCapture`). A probe is captured twice, the second time with the first bound,
so the metal in its room reflects the room in the capture rather than the sky. Four probes with a
capture, those whose boxes come nearest the camera, are bound at set 1's bindings 5 to 8, and the
lighting buffer carries each one's middle, intensity, half size, last mip and nine coefficients
after the environment's. A surface in a box takes its reflection and diffuse light from the
smallest box holding it, the reflection looked up where the reflected ray leaves the box (box
projection), and a surface in none keeps the environment map.

The first directional light with `CastsShadows` set casts the frame's one shadow, in three cascades.
`ShadowFit` cuts each view's camera out to 150 units into slices ending at 12, 45 and 150
units, or out to the distance `SetShadowDistance` puts in `ShadowSettings` in the same proportions,
and fits a tile of a depth map two tiles on a side, 2048 texels a tile unless `SetShadowMapSize`
puts another size in `ShadowSettings`, to the sphere around each, so the near slice spends its
texels on a few units and the far one on many. Each is moved in whole texels so the edges of shadows
hold still as the camera moves, and reaches four radii further toward the light for casters above
the view. Four spot lights with `CastsShadows` set, those whose reach (their range around them, or
their position for one with none) comes nearest the window's camera, or the first target's when
the window draws no mesh, draw into the fourth tile, the whole of
it for one and a quarter each for more, through a perspective projection from the light as wide as
its outer cone and as deep as its range, or the shadow distance for a light with none. A shadowed
spot light carries its slot, counted from one, in its cone's third component, as a point light does,
and its projection and texel width ride in the lighting buffer. `ShadowNode` clears the map once and
draws the window's meshes into each tile in use, before the window's passes, with `shadow.slang`'s
vertex stage and no fragment stage, through a depth-only render pass (`GraphicsDevice.CreateShadowMap`).
A render target that draws meshes has cascades fitted to its own camera and a lighting buffer of its
own (`TargetShadows`), and `TargetsNode` draws the map for it before its pass, so a split screen
drawn into two textures shadows each view. The window's map is drawn after every target, and the
point lights' faces, the same from every camera, once a frame. Each view
draws its own batches and instances, which its model pass draws after it, reading each
instance's world matrix, color and cutoff, and each tile and face pushes its light's
view-projection, so the frame's instances are written once for both passes. A batch is gathered by
the kind of shadow its draws cast as well, none, solid or masked, and a masked one is drawn with
`shadow.slang`'s fragment stage and its maps, which cuts it out below its cutoff as the model pass
does, so its shadow has its holes. A blended draw that is clear anywhere is drawn the masked way
too, its fragment stage keeping a texel where its alpha passes a threshold that interleaved gradient
noise spreads over the texels, so the nine samples the model pass averages give a shadow as dark as
the surface is opaque. The map is bound at binding 1 of the lights' set, beside the cascades'
matrices and texel widths in the lighting buffer, and the white texture takes its place in a frame
with no shadow. The shader takes the nearest cascade whose tile holds the point, a little inside its
edge, moves the point off its surface by a texel and a half of that cascade along its normal, and
averages nine comparisons around it. Across the outer fifth of a tile the next cascade is read as
well and blended in, so the shadow's softness changes over a band where one cascade gives way to the
next, and past the last cascade's band the shadow fades out rather than ending at a line. A spot
light's texels widen with distance from it, so its offset grows with that distance. A model shader
with a vertex stage of its own casts the shadow of its mesh as it was before that stage moved it.

Four point lights with `CastsShadows` set, chosen the same way, shadow everything around them, each in six
faces of a quarter of a tile, 512 texels by default, a little wider than a right angle so the nine comparisons near a face's edge
stay on it. The faces are layers of a second depth image, drawn one layer at a time with the
shadow pipelines and sampled as an array at the lights' set's binding 4, and their views and
projections ride in the lighting buffer after the lights, in the order +X, -X, +Y, -Y, +Z, -Z. A
shadowed point light carries its slot, counted from one, in its cone's third component, and the
shader picks the face by the axis the point lies furthest along, which is the face whose view
holds it, so no cube map's conventions have to agree with the matrices. The image is moved to the
layout it is sampled in when it is made, so its layers can be bound before any is drawn, and until
a point light casts a shadow a stand-in of two texels is bound in its place.

## 5. Render targets and post processing

The window and every render target are drawn at `Config.Samples` samples a pixel, 4 by default,
rounded down to what the device can multisample color and depth at. Their passes share one
builder (`GraphicsDevice.CreateColorDepthPass`), so they stay compatible. With more than one
sample a pass draws into a multisampled color image and depth image and resolves the color into
the frame image or the target's sampled image at the end of the subpass, and a target's depth too. A pipeline rasterizes at
the samples of the pass it is made for, and the shadow map's depth-only pass stays at one.

`BeginTextureMode(target)` redirects the calls that follow into an offscreen image, which a later
draw can sample. A target is a color image in the swapchain's format and a depth image in its
depth format (`GraphicsDevice.CreateRenderTarget`), so its render pass is compatible with the
window's and the same pipelines draw into both. Draw list batches and model draws carry the
target they were recorded for, and `TargetsNode` draws each target used in the frame, before the
window's pass, clearing it first and leaving its color image ready to sample. The color image is
registered in `GpuTextures` under the target's texture id, so `DrawTexture` samples it like a
loaded texture.

A target's depth is kept to sample as well, under a texture id of its own (`RenderTexture2D.Depth`).
With one sample the depth image drawn into is stored and sampled. With more, the subpass resolves
the multisampled depth into a single-sampled image by each pixel's first sample, which only
`vkCreateRenderPass2` describes, so a target's pass is made by `CreateTargetPass` rather than the
shared builder, with the same attachments, subpass and dependencies besides. Vulkan leaves resolve
attachments out of compatibility for a pass of one subpass, so the window's pipelines still draw
into it. The multisampled depth is stored even so, because NVIDIA's driver resolves nothing from a
depth that is not.

Post processing is a chain of full-screen Slang passes over the main color target
before it is copied to the swapchain: tonemapping first, then bloom and anti-aliasing (FXAA).

## 6. What a frame costs

The frame profile (`FrameProfile`, in `Diagnostics/`) averages over about a second of frames how
long each stage of the schedule and each system in it took, the renderer's extract, begin, prepare,
graph and end steps, each prepare system, and each graph node on the CPU and, through timestamp
queries, on the GPU. A program on the flat API runs its own code outside the stages, which shows as
`program.update` (between frames) and `program.drawing` (between `BeginDrawing` and
`EndDrawing`), and the wait for the target frame rate as `wait`. `e3d command profile` returns all
of it, and `SetProfileValue` adds a program's own numbers to it.

Two examples grow what they draw until a frame takes longer than a sixtieth of a second, then
narrow in on the largest count that held to within about 3 percent (`StressRamp`):

- `textures_bunnymark`, raylib's bunnymark, 32 by 32 sprites of one texture bouncing around the
  window, each a `DrawTexture` call.
- `models_stress`, cube entities in four materials turning on a grid, under a sun with a shadow and
  two point lights, beside eight skinned arms, each its own model on its own frame.
  `E3D_STRESS_ARMS=0` leaves the arms out.

A run, repeatable from the terminal:

```sh
dotnet build -c Release 3DEngine.Examples
./e3d open 3DEngine.Examples/bin/Release/net10.0/3DEngine.Examples models_stress --offscreen
./e3d command profile        # until "limit" is above 0
./e3d stop
```

Taken on 2026-10-04 on an Intel Core i9-14900HX with an NVIDIA GeForce RTX 4070 Laptop GPU (driver
615.71.09, Linux 7.2), Release build, offscreen at 800 by 450 with 4 samples a pixel, at the count
each search ended on. Times are milliseconds a frame.

| | bunnymark | stress, 8 arms | stress, no arms |
|---|---|---|---|
| Count that holds 60 a second | 121,613 sprites | 1,687 entities | 8,004 entities |
| Frame | 17.5 | 20.4 | 14.7 |
| Program's update | 5.4 | 0.1 | 0.1 |
| Program's drawing calls | 9.4 | 0.0 | 0.0 |
| Schedule's stages | 2.7 | 20.3 | 14.5 |
| `MeshEntityDraws` | | 0.4 | 1.8 |
| Prepare | 1.4 (immediate upload) | 16.3 (`GpuMeshesPrepare`) | 0.1 |
| Graph, CPU | 0.1 | 2.5 | 11.7 |
| Model pass, CPU / GPU | | 2.1 / 1.4 | 10.3 / 1.8 |
| Shadow pass, CPU / GPU | | 0.3 / 0.2 | 1.3 / 0.6 |
| Immediate pass, GPU | 4.0 | 0.0 | 0.0 |

The count with arms moved between about 1,700 and 4,700 from run to run, because the arms' cost
varied more than the entities' did.

The same runs after the three changes below:

| | bunnymark | stress, 8 arms | stress, no arms |
|---|---|---|---|
| Count that holds 60 a second | 186,473 sprites | 32,416 entities | 27,614 entities |
| Frame | 16.7 | 17.3 | 16.6 |
| Program's update | 3.0 | 0.6 | 0.5 |
| Program's drawing calls | 10.4 | 0.0 | 0.0 |
| Schedule's stages | 3.3 | 16.6 | 16.0 |
| `MeshEntityDraws` | | 7.5 | 6.3 |
| Prepare | 2.0 (immediate upload) | 0.0 | 0.0 |
| Graph, CPU | 0.1 | 7.8 | 8.4 |
| Model pass, CPU / GPU | | 4.7 / 1.4 | 4.8 / 1.2 |
| Shadow pass, CPU / GPU | | 3.1 / 1.6 | 3.6 / 1.2 |
| Immediate pass, GPU | 5.1 | 0.0 | 0.0 |

A search ends within about 3 percent of where a frame leaves the budget, and runs differ by more
than that, so the arms' count above the one without them is noise rather than a gain.

The largest costs as they were measured, in order, each with what changed:

1. **An animated mesh's vertices go into a buffer created for them each frame.** `UpdateMeshVertices`
   queues the skinned vertices, and `GpuMeshes` creates, allocates and maps a vertex buffer for them
   and retires the previous one, which is destroyed four frames later. Eight small arms cost 16.3
   ms of the frame, about 2 ms each, which leaves the entities almost nothing.
   **Changed.** A mesh whose vertices are replaced moves into a ring of five vertex buffers kept
   mapped, more than there are frames in flight, and each update is written into the next. The
   same run afterward held 9,338 entities with the eight arms in place of 1,687, and
   `GpuMeshesPrepare` fell from 16.3 ms to under 0.05 ms. At that count the model pass records
   for 8.8 ms, the shadow pass for 1.5 ms and `MeshEntityDraws` takes 2.1 ms, which is cost 2.
2. **Each mesh entity is a draw of its own on the CPU.** The model pass binds the material's set at
   its offset in the factor ring, binds the vertex and index buffers, pushes the transform and
   draws, once per entity and once more in the shadow pass, so 8,004
   cubes of one mesh cost 10.3 ms of recording and 1.3 ms of shadows, while the GPU draws them in
   1.8 ms. `MeshEntityDraws` adds 1.8 ms walking the entities and building their draws.
   **Changed.** Draws sharing a mesh and its maps are one instanced draw in the model and the shadow
   pass, their transforms and factors read per instance, a draw's sRGB color decoded through a table
   and its set found by its texture ids. The same run afterward held 27,614 entities without the
   arms in place of 8,004, and 32,416 with them, the model pass recording 4.8 ms and the shadow pass
   3.6 ms for 27,614 draws, one call each, since the cubes and the ground share a mesh and no maps.
   What is left is gathering and writing each instance. The largest cost at that count is
   `MeshEntityDraws` at 6.3 ms, about 230 nanoseconds an entity spent reading its components,
   encoding its linear albedo as sRGB bytes with three powers for the draw to decode again, and
   adding its draw to the list under a lock of its own, parts that cost about the same when timed
   one by one. **Changed after.** `MeshEntityDraws` keeps each albedo's encoding, adds the frame's
   draws under one lock, and checks an entity's mesh against the one before it. The same run held
   34,217 entities in place of 27,614, at about 190 nanoseconds an entity. What it spends is reading
   the mesh, material and global transform of each entity and copying a `ModelDraw` of about 200
   bytes twice, into its own list and into the draw list. **Changed after.** Each entity's draw is
   kept from frame to frame and built again only when its material compares unequal to the one it
   was built from, its mesh changed, or a texture was still loading. Timed alone over 34,000
   entities pinned to one core, the system took 7.4 ms in place of 11.2 ms. A comparison of the
   material stands in for change marks, which an `Add` replacing the material would not set.
3. **Each sprite costs about 77 nanoseconds in `DrawTexture`** (9.4 ms for 121,613), then 1.4 ms to
   upload and 4.0 ms on the GPU. The example's own movement loop takes 5.4 ms, much of it in
   `GetScreenWidth` and `GetScreenHeight`, which it calls for each sprite as raylib's does and
   which look up a resource each call.
   **Changed.** The flat API keeps each resource it reads until the world's resources change
   (`World.ResourceVersion`), the draw list keeps its open batch's count in fields where it
   compared and copied a whole batch on every shape, and an unturned sprite skips its sine and
   cosine. The same run afterward held 186,473 sprites in place of 121,613, with the movement
   loop at 3.0 ms and `DrawTexture` at about 55 nanoseconds a sprite (10.4 ms). What is left of
   it is the draw list's lock, which a system on a worker thread needs, and six vertices of 24
   bytes for each quad, since the immediate pass drew without an index buffer. The GPU takes 5.1
   ms for them and the upload 2.0 ms.
   **Changed after.** The immediate pass draws by index, so a quad is four vertices and six indices,
   120 bytes in place of 144. Measured one run after the other on a machine busy with other work,
   the search ended at 133,774 sprites without the indices and 127,018 with them, which is within
   what such a run varies by. `DrawTexture` took about 84 nanoseconds a sprite in both, so writing
   the two vertices was not its cost, and the upload took 11.7 nanoseconds a sprite in place of 14.5.
   **Changed after.** Timed alone, recording a quad took 57 nanoseconds, of which the lock, taken
   and released, was about 30. The draw list now takes it only while a schedule runs a batch of
   systems on several threads, the one time two threads can record at once, and a quad writes its
   four vertices through one span and its six indices straight into the open batch, without the
   loop over offsets the other shapes go through. Timed alone, a quad took 18 nanoseconds and a
   `DrawTexture` 24 in place of 44. Run one after the other on 2026-10-04, the search ended at
   154,043 sprites before, 170,258 with the lock alone left out and 243,226 with both, where the
   drawing calls took 11.7 ms, about 48 nanoseconds a sprite with the example's loop, the upload
   3.0 ms and the GPU 6.3 ms.
4. **The shadow pass wrote every instance again for each cascade.** Measured again later in the
   day on the same machine, after point lights and a fourth tile had been added, the run without
   arms held 22,811 entities, with the shadow pass recording for 6.4 ms, the model pass for 4.9 ms
   and `MeshEntityDraws` taking 4.6 ms. Each of the three cascades wrote all 160 bytes of each
   draw's instance, its transform through that cascade's light, so a frame wrote three times what
   the model pass did.
   **Changed.** The shadow pass has a vertex stage of its own in `shadow.slang`, whose instance
   holds the world matrix, and the material's color and cutoff for a masked surface, in 80 bytes,
   and which reads the light's view-projection as a push constant. The frame's shadow instances
   are written once and drawn through each cascade, spot tile and point face. Both passes read the
   draw list by reference, where they copied each draw of about 200 bytes for each call. The same
   run afterward held 45,923 entities in place of 22,811, with the shadow pass recording for 2.4
   ms, the model pass for 4.4 ms, and `MeshEntityDraws` at 8.4 ms, about 180 nanoseconds an entity,
   the largest cost again.
   **Changed after.** The shadow pass draws the window's own batches and instances, which the first
   of the two passes in a frame gathers and writes, and its vertex stage reads the world matrix,
   color and cutoff from the model pass's 160-byte instance. Timed alone over 46,000 entities,
   `MeshEntityDraws` spent 3 ms of its 8 building each parentless entity's matrix from its
   `Transform` by multiplying three matrices, which `ToMatrix` writes out directly, and copying each
   draw into a list of its own before the draw list, where it writes the opaque ones in place under
   the list's lock. It took 4.5 ms in place of 8.0 ms. The same run afterward held 66,859 entities
   in place of 45,923, with `MeshEntityDraws` at 8.0 ms and the shadow pass, which gathers and
   writes the window's instances, recording for 7.1 ms, about 120 and 105 nanoseconds an entity.
5. **Each mesh entity was a draw of about 200 bytes, sorted into batches again by the pass.**
   `MeshEntityDraws` copied each entity's kept draw with its world matrix into the draw list, and
   the first pass of the frame looked up each draw's material set, joined it to its batch and
   wrote its instance from it.
   **Changed.** An opaque entity's instance is written whole by `MeshEntityDraws` into an
   `InstanceGroup`, one for each mesh, set of maps, sides and alpha mode, which the draw list
   carries beside its draws and the pass copies as it is and draws as one batch. An entity keeps
   12 bytes naming a look, one for each mesh and material in use, which holds the draw, the
   instance's factors and the group, so the material each entity is compared with is in the
   cache. Measured one run after the other on a machine busy with other work, the run without arms
   held 145,873 entities in place of 72,937, with the shadow pass recording for 1.9 ms in place of
   7.4 ms and `MeshEntityDraws` at 12.0 ms, about 82 nanoseconds an entity, most of what a frame
   costs. Timed alone over 100,000 entities, reading the entity's `Transform` or `GlobalTransform`
   and making its matrix took about 30 nanoseconds of that and writing its instance about 18.
   **Changed after.** Past 4,096 entities `MeshEntityDraws` records them in chunks of that many on
   threads of their own, each into buffers of its own, padded so that no two chunks count into
   one cache line, which at first left each chunk ten times slower than alone. An entity that
   needs a mesh uploaded or a look built is recorded after the chunks. Timed alone over 100,000
   entities it took 3.2 ms in place of 7.7. The run without arms afterward held 266,673 entities,
   with `MeshEntityDraws` at 5.2 ms, about 20 nanoseconds an entity, the program's own loop turning
   each entity at 5.6 ms, and the shadow pass recording for 5.8 ms, most of it copying 43 MB of
   instances into the ring. The GPU took 8.0 ms for the shadow and 4.5 ms for the model pass.
   **Changed after.** Past 16,384 instances the groups' segments are copied into the ring on
   several threads, each into its own range of the mapped buffer. The run without arms, measured
   again first at 266,673 entities as before, afterward held 307,699, with the shadow pass recording
   for 2.3 ms in place of 5.3 ms. The GPU then took 9.8 ms for the shadow and 5.3 ms for the model
   pass, 15.1 ms of the 16.7 a frame has, so drawing fewer instances, by culling what each cascade
   and the camera do not see, is what raises the count next.
6. **Every view drew every instance.** The camera, each cascade, each spot tile and each point
   face drew all of a group's instances, though the example's camera backs away to keep its grid
   in view and so leaves the grid past the 150 units the sun's cascades reach.
   **Changed.** `MeshEntityDraws` gives each group the sphere around its mesh, and the threads that
   copy a group's segments into the ring find the box around each block of 64 instances, each
   instance's sphere moved by its world matrix and grown by its largest scale. A view draws the
   runs of blocks that its four side planes do not leave out, a call a run, so a block wholly
   beside a view costs it nothing. Near and far are left out of the test, so neither a depth
   convention nor a shadow box's reach toward the light can leave out a block the view draws.
   The run without arms afterward held 321,375 entities in place of 307,699, the GPU taking 0.5 ms
   for the shadows in place of 9.8 ms and 6.2 ms for the model pass. The frame is now the CPU's,
   the program's loop turning every entity the largest part of it.
7. **Each instance carried its camera.** The model pass's instance held the world matrix through
   the camera, 64 of its 160 bytes, which `MeshEntityDraws` multiplied out for each entity, and
   which the shadow pass, pushing the light's matrix, did not read.
   **Changed.** The batch pushes the camera's view-projection, the vertex stage multiplies the
   world position by it, and works the eye out from it, so an instance is 96 bytes and the same
   through every view. Run one after the other on 2026-10-04, the run with arms held 410,266
   entities in place of 379,495, and the GPU took 5.1 ms for the model pass in place of 6.4.
   **Changed after.** `MeshEntityDraws` gathers the entities once a frame, where it gathered
   them again for each camera, and each camera's groups hold the same instances under a template
   with the camera's view-projection, so a camera drawing into a render texture costs its
   translucent entities' sort and not a pass over every entity.

## What the engine needs

### The device

- **Dynamic rendering** (Vulkan 1.3 core), so a pass is a call rather than a render pass object with
  framebuffers to keep in step with the swapchain. Every desktop driver in use exposes it, and it
  removes most of the code in `GraphicsDevice.Swapchain` and `GraphicsDevice.Offscreen`.
- **Synchronization2**, so barriers name their stages and accesses in one structure.
- **Buffers and textures carved out of blocks** (`GraphicsDevice.Memory`), done, since drivers
  limit the number of allocations, often to 4,096. Each memory type has blocks of 64 MiB, buffers
  and images in blocks of their own so Vulkan's granularity between them never applies, a block
  the CPU sees mapped once for good, and a request past half a block given one of its own. The
  engine carves them itself rather than taking the Vulkan Memory Allocator, which DESIGN.md §8
  does not list.
- **A swapchain rebuilt on resize** without a device wait every frame.

### Debugging

- **Object names** through `VK_EXT_debug_utils` (`GraphicsDevice.Name`), done for the shadow
  maps, the environment map and sky, reflection probes and their faces, the model instance ring,
  textures and render textures by their id, so a capture shows them by name. Each render graph
  node's commands are a labeled region, by the node's name, wherever the instance has the
  extension, with validation or under RenderDoc. Mesh buffers and pipelines have no names.

## Order of work

1. Normals and one directional light.
2. Assimp models with textures, and the material struct.
3. Dynamic rendering and synchronization2.
4. Tonemapping, as a full-screen pass over a render target, in place of the curve at the end of the
   model pass.
5. Shadow cascades, then point and spot shadows.
6. Bloom and FXAA.
