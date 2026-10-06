# Rendering

What the renderer draws, how a frame moves through it, and the order it grows in. The target is a
forward renderer that a reader can follow end to end in an afternoon, with enough of a frame graph
underneath that shadows, post processing and render targets slot in as nodes, and nothing that
needs an offline toolchain beyond `slangc`.

## What exists

- **A Vulkan device** (`GraphicsDevice`, over Vortice.Vulkan) targeting Vulkan 1.3 with
  `VK_KHR_swapchain`, three frames in flight, mailbox presentation where the surface offers it, a
  depth image of its own, and optional validation layers. Buffers and textures are carved out of
  blocks (`GraphicsDevice.Memory`). `IGraphicsDevice` is the interface the rest of the engine draws
  through, and `NullGraphicsDevice` stands in for it in tests.
- **Dynamic rendering and synchronization2.** A pass is begun on the images it draws into
  (`GraphicsDevice.Rendering`), with no render pass object or framebuffer made ahead, and moves
  them into their attachment layouts and out to the layout they are read in by barriers of its
  own. A pass (`IRenderPass`) is only the formats and samples it draws at, compared by value, which
  is all a pipeline is made for, so the caches of pipelines key on it. Every barrier is
  synchronization2's (`GraphicsDevice.Barriers`).
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
  none, drawn unlit as raylib draws them (§4).
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

- **Vertex inputs from reflection.** `slangc -reflection-json` is read for the uniforms a shader
  declares at the top level and for every descriptor in every set, each with its set, binding and
  kind (a uniform buffer, a combined image sampler, a storage buffer or image), cached beside the
  SPIR-V. Descriptor set layouts are made from it (`ShaderProgram.LayoutOf`): the model pass's
  material and lights sets from `model.slang`, the bloom, composite and FXAA passes' from their
  shaders, and a program's own shader's set 0 joined with what its pass binds
  (`ShaderProgram.Merge`), so a binding added to a shader reaches its pipeline with no layout
  written for it. Vertex inputs are still written beside each pipeline, for the engine's fixed
  vertex formats, and a sampler declared apart from its texture is not bound.

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
beside the material's maps. A `SamplerCube` is marked in the reflection, and the shader cache
keeps the mark, so it is bound the cube view of a texture `LoadTextureCubemap` made, eight bits a
channel in six layers, or a black cube where none is set, and a 2D slot handed a cube binds the
white texture, since neither view can stand for the other.
An unloaded shader's stages and pipelines are destroyed after the frames in flight that might use
them.

This is raylib's rlgl layer in Vulkan terms. It keeps shapes, grids, gizmos and debug lines out of
the ECS and out of the mesh path. A batch carries the faces it culls, the back ones of a shape
drawn inside `BeginMode3D` and none of a 2D one until rlgl's culling is switched, so a 2D shape's
triangles may wind either way, and its pipelines blend by alpha unless a batch was recorded
inside `BeginBlendMode`, which makes a pipeline for each of raylib's modes the batch asks for. Alpha
is laid over by alpha in every mode, so a render target keeps the coverage of what was drawn into
it. A batch recorded inside `BeginScissorMode` carries its rectangle, clipped to the target, and
the pass sets the scissor to it and back to the whole target after its last batch. A texel with no
coverage is discarded, so a sprite's empty corners write no depth.

A line drawn at one sample a pixel takes the pixels OpenGL's would, by the diamond rule of the
line rasterization extension's Bresenham mode, where the device has it, so a grid lands where
raylib's does. With several samples a line stays the driver's, whose samples smooth it. OpenGL
counts rows up the screen and Vulkan down, so the two break a tie between rows the other way round,
a line on the boundary between two rows drawn on the lower in raylib and a pixel whose middle is on
a shape's lower edge filled there. The pass moves every untextured batch a 256th of a pixel down
the screen after its transform, which breaks each tie as raylib's does. A textured batch is left
where it is, since a texture filtered between its texels would take a trace of the next row.

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
through two cameras are batched apart. `model.slang` shades by the frame's lights, or when the
world has none and no environment draws the color unlit, texture times color with the light the
material gives off, as raylib's default shader draws a model.
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

Morph targets ride the same dispatch. A mesh's targets, read from glTF through Assimp as how far
each moves each vertex, are a sixth storage buffer of the skin, two float4 a vertex (position and
normal) target after target, and each frame's weights follow the joints' matrices in the ring's
buffer, with a push constant of the joint and target counts. `skin.slang` moves a vertex toward its
targets before the joints move it. A mesh with targets and no skeleton is given a skin of one joint
that never moves, so it takes the same path. A clip's weight channels are sampled with its bones
(`ModelAnimation.FrameMorphWeights`), and `SetModelMorphWeight` poses the model again as its bones
were last posed. A clip played on part of the skeleton (`UpdateModelAnimationLayer`) takes each
bone's pose relative to its parent from the second clip from a bone down, and composes it onto
where the first clip puts that bone's parent.

Particles take the skins' path too. `ParticleExtract` copies each `ParticleEmitter`, placed by its
entity's world matrix, with the window's camera (`MeshEntityDraws.WindowCamera`) and the frame's
seconds, held to a tenth of a second. `ParticleRenderer` keeps a storage buffer for each emitter, a
header of four float4 values and then two a particle (position and age, velocity and life), and the
`particles` node, after `skinning`, records a dispatch of `particle_step.slang` for each, a thread a
particle, with everything the step and the draw need in its 128 bytes of push constants. The frame's
births are its share of the rate, the fraction owed carried to the next frame, and its burst, in a
run of slots after the last frame's, so the oldest are replaced, each started within the emitter's
radius with its velocity turned within the cone and its speed and life varied by a hash of its slot
and a seed of the frame and the emitter. The first thread writes the colors, sizes and brightness
into the header, which the draw reads, so nothing of an emitter is written from the CPU while a
frame in flight reads it. A particle alive falls by gravity and slows by the emitter's drag, as an
exponential of the step, whose bits ride in the push block's last word. Where an emitter collides,
the node first draws the window's meshes that cast shadows into a depth at half the window's size,
with `DrawDepth`, as ambient occlusion draws its own, and binds it with the view it was drawn
through as the step's second set, one written each frame in flight, and the white texture with
collision off in frames none collides. The step finds a particle's pixel in that depth, the
surface there in the world through the inverse view-projection, and its normal from the texels
beside it, turned toward the camera, and a particle that crossed that surface's plane from the
camera's side in the step bounces off it, keeping the share of its speed into it that rides with
the collision's two bits in the capacity word's spare high bits, or ends there. A particle already
behind a surface, as one passing behind a post, crosses nothing. On `shaders_particles` the depth
costs the graph about 0.03 ms of CPU and the GPU about 0.01 ms. The draw
(`particles.slang`) imports the model pass, binds its material set with the emitter's texture as
the base color, white without one, and the window's lights set as sets 0 and 1 and the particles
as set 2, and draws six vertices an instance, a square facing the camera's eye that `eyeInWorld`
finds from the view-projection, after the window's meshes into the window or the HDR frame, depth
tested and not written, added or laid over by alpha. The square is a round dot, or the texture
tinted where the emitter has one, whole or the frame of a sheet the share of the life gone picks,
mixed with the next frame by how far the life is through its own where the emitter blends them,
the branch on the emitter's setting so both samples are taken where every pixel takes them.
The look's last value packs whether it is lit and textured, the sheet's columns and rows and
whether they blend into an integer a float holds exactly, since the push block has no room left.
A lit particle goes through `lit` as a rough surface facing the camera and an unlit one through
`toDisplay`, so both follow the HDR frame's output flag. Emitters laid over by alpha are drawn after
the additive ones, from the farthest from the camera's eye to the nearest by where each emitter is.
Within an emitter laid over by alpha, `particle_sort.slang` sorts after the step, in the emitter's
buffer after its particles, a key a particle of its negative squared distance from the window's
eye, a large one for the dead and infinity for the padding to a power of two, by Batcher's bitonic
sort. Blocks of 512 keys are sorted in a workgroup's shared memory in one dispatch, and an emitter
of more takes a dispatch for each step across blocks and one for the steps within them after it,
so 400 particles cost 0.02 ms of the GPU where a dispatch a step took 0.47. The step clears and
the sort sets a flag in the header, by which the draw reads each instance's particle through the
sorted keys. `TargetsNode` draws them into each render target after
its meshes, with its own lights, since the step runs before the targets, through the camera of the
target's first `BeginMode3D`, which `Mode3DCamera.Targets` keeps, or the one its meshes were drawn
through for a camera entity's texture. A target drawn only in 2D has no camera for them. A
reflection probe's capture draws them into each face after its meshes, through the face from the
probe's middle, lit by the capture's lights, so a fire in a room glows in its metal.

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
clamp per channel turns it white, and a color below the bend is unchanged. A world with no light
entities and no environment draws its models unlit, encoded with no curve, so a color near white
keeps its shade as raylib's does. With bloom off the curve runs at the end
of the model pass. With bloom on, the window's view writes linear light into the HDR frame instead
(a flag in its lighting buffer, `output.x`, which `toDisplay` reads), and the curve runs once over
the frame in the composite (§5), from the module `color.slang` both import.

An `EnvironmentMap`, a world resource set by `SetEnvironmentMap` from an equirectangular image,
lights a frame from all around. A Radiance `.hdr` file is read as linear floats, so a sun keeps its
brightness past white, and an eight-bit image is decoded from sRGB through a table of half floats,
an image wider than 4096 halved until it fits, which is all the CPU does. The `environment` node,
ahead of every pass, uploads the image of a map it has not seen with its mips and filters it on the
GPU (`GraphicsDevice.RecordEnvironmentFilter`) with the stages a reflection probe's capture goes
through, below. `env_gather.slang` resamples the image into a source cube twice the target's width,
each texel averaging four samples read from the image's mip whose rows are as far apart as the
samples, and into the sky cube. The image's own mips would weigh a row by its length, which near a
pole holds one direction many times over, where a cube's texels differ in solid angle by a factor
of about five at most, so the source's mips weigh each direction by what it covers and a light at
the zenith is spread as one on the horizon is. The target is a half-float cube with faces 64 texels
wide, mip 0 for a mirror and each mip after for a roughness of `mip / (mips - 1)`, by GGX
importance sampling with the eye along the normal (Karis's split sum), each sample reading the
source at the level its solid angle covers. The model pass looks the mirror direction up at the mip
for the surface's roughness, weighted by Karis's fit, and lights the diffuse share by the image's
irradiance, both darkened by occlusion. The irradiance is the source projected onto the nine
spherical harmonics of bands 0 to 2, each band scaled by its share of a cosine lobe (Ramamoorthi
and Hanrahan), so a surface takes the light of the whole half of the sky it faces. The cube is set
1's binding 2, a black cube when there is none, the irradiance a storage buffer at binding 14,
zeros when there is none, and the lighting buffer carries the map's intensity and its last mip.
With a map set models are lit by it, whether or not there are light entities.

The filter writes a second cube for the sky, at a quarter of the image's width a face up to 512
texels, resampled with no prefiltering, at set 1's binding 3. `DrawSkybox` records a model draw
of a cube around the camera with `sky.slang`, which looks the sky cube up along the way from the
eye through each pixel and sets its depth a millionth inside the far plane, so whatever else the frame
draws is in front. The draw goes through the model pass's tonemap like a reflection does, and is
left out of the shadow map (`ModelDraw.CastsShadow`).

A `ReflectionProbe` entity, which `CreateReflectionProbe` makes, is a box whose surfaces reflect
what is around its middle rather than the environment map. `ProbeNode`, after the window's shadow
and before its passes, captures the first probe out of date, one face a frame. The window's batches,
or the first render target's when the window draws no meshes, are drawn through six views of a right
angle from the probe's middle into half-float render targets of 64 texels (`ModelRenderer.Draw` with
a view-projection pushed in place of each batch's), lit by the window's lighting buffer with its
output flag set, so the light stays linear and as bright as it was drawn, cleared to the window's
clear color in linear light. The frame that draws the sixth face filters them on the GPU after it
(`GraphicsDevice.RecordProbeFilter`), so a capture costs that frame's work and nothing is read back.
`probe_gather.slang` fills a cube of faces 64 texels wide, each texel reading the face that looks
most nearly along it through that face's own view-projection, and `probe_mips.slang` makes its mips,
each texel the average of four. `probe_prefilter.slang` writes the probe's cube of faces 32 texels
wide (`FilteredCube`), mip 0 for a mirror read from the gathered cube's level of the same width, and
each mip after by GGX over 64 samples, each read from the gathered level its solid angle covers, as
the environment map is filtered. `probe_irradiance.slang` projects the gathered cube's level 16
texels wide onto the nine harmonics in one group of 64 threads, into a storage buffer of the probe's
own. A source cube of each width is made once and shared by every filter. A probe is captured twice,
the second time with the first bound, so the metal in its room reflects the room in the capture
rather than the sky. A probe whose map is of an earlier placement or of lights since changed gives a
capture no light, its intensity 0 in the capture's lighting buffer, so its first pass sees only the
lights and its second bounces that. `ReflectionProbes.Sync` keeps the lights that reached each box
when it was last asked for, and asks again when one is added or removed, grows or dims by a quarter,
turns color, or moves a quarter of a unit or turns past eleven degrees, so a lamp switched off is
seen and a flickering one is not. A probe whose `Refresh` is above 0 is captured again that many
seconds after each capture, one pass with the last bound, while what it is wanted as stays, so it
stays ready, and a capture a placement or a light needs goes before a refresh, the refresh of the
probe captured longest ago first. Four probes with a capture, those whose boxes come nearest the
camera, are bound at set 1's bindings 5 to 8 and their irradiance buffers at 10 to 13, and the
lighting buffer carries each one's middle, intensity, half size and last mip after the
environment's. A surface in a box takes its reflection and diffuse light from the smallest box
holding it, the reflection looked up where the reflected ray leaves the box (box projection), and a
surface in none keeps the environment map.

The first directional light with `CastsShadows` set casts the frame's one shadow, in three cascades.
`ShadowFit` cuts each view's camera out to 150 units into slices ending at 12, 45 and 150 units, or
out to the distance `SetShadowDistance` puts in `ShadowSettings` in the same proportions, and fits a
tile of a depth map two tiles on a side, 2048 texels a tile unless `SetShadowMapSize` puts another
size in `ShadowSettings`, to the sphere around each, so the near slice spends its texels on a few
units and the far one on many. Each is moved in whole texels so the edges of shadows hold still as
the camera moves, and reaches four radii further toward the light for casters above the view. Ten
spot lights with `CastsShadows` set draw into the fourth tile, ranked by what they matter to the
window's camera, or the first target's when the window draws no mesh: those whose reach (their range
around them, or the shadow distance for one with none) the camera's frustum holds come first, then
those whose light reaching the eye is greatest, their brightness over one plus the square of how far
their reach is from it (`LightingUboPrepare.Rank`), then those whose reach comes nearest it. The
tile is the whole of it for one, a quarter each for up to four, and past four a quarter each for the
first two and a sixteenth each for the rest, in its lower half (`ShadowFit.SpotTileArea`), each
through a perspective projection from the light as wide as its outer cone and as deep as its range,
or the shadow distance for a light with none. A shadowed spot light carries its slot, counted from
one, in its cone's third component, as a point light does, and its projection and texel width ride
in the lighting buffer. `ShadowNode` clears the map once and draws the window's meshes into each
tile in use, before the window's passes, with `shadow.slang`'s vertex stage and no fragment stage,
through a depth-only pass (`GraphicsDevice.CreateShadowMap`). A render target that draws meshes has
cascades fitted to its own camera and a lighting buffer of its own (`TargetShadows`), and
`TargetsNode` draws the map for it before its pass, so a split screen drawn into two textures
shadows each view. The window's map is drawn after every target, and the point lights' faces, the
same from every camera, once a frame. Each view draws its own batches and instances, which its model
pass draws after it, reading each instance's world matrix, color and cutoff, and each tile and face
pushes its light's view-projection, so the frame's instances are written once for both passes. A
batch is gathered by the kind of shadow its draws cast as well, none, solid or masked, and a masked
one is drawn with `shadow.slang`'s fragment stage and its maps, which cuts it out below its cutoff
as the model pass does, so its shadow has its holes. A blended draw that is clear anywhere is drawn
the masked way too, its fragment stage keeping a texel where its alpha passes a threshold that
interleaved gradient noise spreads over the texels, so the nine samples the model pass averages give
a shadow as dark as the surface is opaque. The map is bound at binding 1 of the lights' set, beside
the cascades' matrices and texel widths in the lighting buffer, and the white texture takes its
place in a frame with no shadow. The shader takes the nearest cascade whose tile holds the point, a
little inside its edge, moves the point off its surface by a texel and a half of that cascade along
its normal, and averages nine comparisons around it. Across the outer fifth of a tile the next
cascade is read as well and blended in, so the shadow's softness changes over a band where one
cascade gives way to the next, and past the last cascade's band the shadow fades out rather than
ending at a line. A spot light's texels widen with distance from it, so its offset grows with that
distance. A model shader with a vertex stage of its own casts the shadow of its mesh as it was
before that stage moved it.

Twelve point lights with `CastsShadows` set, chosen and ranked the same way, shadow everything
around them, each in six faces, a little wider than a right angle so the nine comparisons near a
face's edge stay on it. The first four have faces of a quarter of a tile, 512 texels by default, a
layer each, and the other eight half that, four faces to a layer after theirs
(`ShadowFit.PointFaceArea`), 36 layers in all, with each comparison kept inside its own square. The
faces are layers of a second depth image, each layer cleared once and drawn with the faces it holds, with the
shadow pipelines and sampled as an array at the lights' set's binding 4, and their views and
projections ride in the lighting buffer after the lights, in the order +X, -X, +Y, -Y, +Z, -Z. A
shadowed point light carries its slot, counted from one, in its cone's third component, and the
shader picks the face by the axis the point lies furthest along, which is the face whose view
holds it, so no cube map's conventions have to agree with the matrices. The image is moved to the
layout it is sampled in when it is made, so its layers can be bound before any is drawn, and until
a point light casts a shadow a stand-in of two texels is bound in its place.

## 5. Render targets and post processing

The window and every render target are drawn at `Config.Samples` samples a pixel, 4 by default,
rounded down to what the device can multisample color and depth at. With more than one sample a
pass draws into a multisampled color image and depth image and resolves the color into the frame
image or the target's sampled image when it ends, and a target's depth too. A pipeline rasterizes
at the samples of the pass it is made for, and the shadow map's depth-only pass stays at one.

`BeginTextureMode(target)` redirects the calls that follow into an offscreen image, which a later
draw can sample. A target is a color image in the swapchain's format and a depth image in its
depth format at the window's samples (`GraphicsDevice.CreateRenderTarget`), so its pass is the
window's and the same pipelines draw into both. Draw list batches and model draws carry the
target they were recorded for, and `TargetsNode` draws each target used in the frame, before the
window's pass, clearing it first and leaving its color image ready to sample. The color image is
registered in `GpuTextures` under the target's texture id, so `DrawTexture` samples it like a
loaded texture.

A target loaded with formats draws into up to four color images at once, as a G-buffer is drawn
into, each with its multisampled image and resolve, in eight-bit RGBA, half floats or floats. The
pass describes them by value (`VulkanRenderPass.More`), so two targets of the same formats share
their pipelines, and a pipeline names each format and blends each alike, leaving an image past the
outputs its fragment stage writes (read from its SPIR-V) as it is, so the model pass's and the
immediate pass's own shaders draw into such a target and fill only its first. The images past the
first are textures of their own under ids of their own, retired with the target as its depth is.

A target's depth is kept to sample as well, under a texture id of its own (`RenderTexture2D.Depth`).
With one sample the depth image drawn into is stored and sampled. With more, the pass resolves the
multisampled depth into a single-sampled image by each pixel's first sample, the one depth resolve
every device has. The multisampled depth is stored even so, because NVIDIA's driver resolves
nothing from a depth that is not.

`SetBloom(intensity, threshold)` turns on the HDR frame, which is off and costs nothing by default
(`BloomRenderer`, `Rendering/PostProcess`). Two nodes run between `probes` and `main_pass`.
`hdr_scene` draws the window's models and its draw list up to its last batch with depth, the 3D
shapes inside `BeginMode3D`, into a half-float target the size of the window at the window's
samples, cleared to the clear color decoded to linear. The draw list's shapes there go through
`immediate_linear.slang`, which decodes their sRGB colors, so a shape below the tonemap's knee
comes out as it went in. `bloom` halves the target's resolved color five times, down to a
thirty-second of the window, with Jimenez's thirteen-tap filter, keeping on the first step only the
light past the threshold, eased in over a tenth of it. It then adds each level back onto the one
above through a tent, so the first level holds the light spread over every size (`bloom.slang`).
`main_pass` draws the composite first, the scene with the first level added at the intensity over
the number of levels, tonemapped and encoded (`composite.slang`). The models node draws nothing
more, and the immediate node draws the batches after the split over it, so a game's interface is
never bloomed or tonemapped and keeps raylib's colors, and ImGui after it as before. The targets
are made the first frame bloom is on and again when the window's size changes, and those they
replace are destroyed four frames later, as are all of them the first frame bloom is off. At 800
by 450 on the RTX 4070 the chain takes 0.26 ms and the composite 0.17 ms.

The same frame carries the effects of `FrameEffects`, which turn it on when any is away from its
default: `SetExposure`, `SetTonemap` (the engine's curve, Reinhard's, Narkowicz's fit of ACES, or a
cut at 1), `SetColorGrading` (saturation and a tint in linear light after the curve, contrast about
the middle once encoded) and `SetVignette`, all in the composite's push constants. With `SetFxaa`
the composite draws into an eight-bit target the window's size instead, and `main_pass` draws that
through FXAA (`fxaa.slang`, the console form of Lottes's, over the encoded colors) before the
interface. `FrameEffectsTests` reads each from a frame.

`SetAutoExposure` makes the exposure follow the scene, in two passes of `exposure.slang` after the
bloom chain, which keep the log2 of luminance since an eye adapts by ratios and a mean of logs is not
pulled up by one lamp. The first measures the HDR frame into a target of 64 by 64, each texel the
mean of sixteen taps over its part of the frame. The second, one texel, weights those toward the
middle of the picture, four times as much there as at the edges, holds the mean between the
luminances the exposure's bounds bring to a mid gray of 0.18, and moves the value of the frame before
toward it by one less e to the minus the speed times the seconds since, or all the way on the first
frame. Two such texels take turns, each frame writing one from the other, and the composite divides
0.18 by two to the value it reads and multiplies the exposure by that, so nothing is read back.

`SetDepthOfField` and `SetMotionBlur` are passes of their own after the bloom chain, into half-float
targets the window's size that the composite then reads in place of the scene, both reading the HDR
target's resolved depth unfiltered and working back to the world through the inverse of the window's
camera (`WindowView`, which `CameraExtract` sets from `MeshEntityDraws.WindowCamera`). The depth of
field (`dof.slang`) gives each pixel a blur from its distance to the eye against the focus, and
gathers 32 taps on a golden-angle spiral out to the widest blur, a tap counting once its own blur
reaches past it and a tap behind the pixel counting no wider than the pixel's own, so a blurred
thing in front spreads over what is sharp behind it and not the other way. It reads color unfiltered
too, and a pixel's distance is the nearest of the 3 by 3 texels round it, since multisampling leaves
a thing's edge pixels its color and the depth of what is behind, which spread as faint copies of the
edge otherwise. Motion blur (`motion_blur.slang`) puts each pixel's point through the camera of the
frame before (the inverse view-projection times last frame's, one matrix in the push constants), and
averages twelve taps along the way it moved, scaled by the amount and held to a tenth of the
picture, taking the fastest of eight movements around it so a near thing's edge smears over the
background beside it. With `objects`, the mesh entities whose world matrix differs from the frame
before's, which `MeshEntityDraws` finds in ranges of 4096 on threads of their own, are drawn by
`velocity.slang` into a half-float image of the HDR frame's size, each mesh's as one run of
instances of its world now and then, a fragment dropped behind the scene's depth, and the blur reads
that movement in place of the camera's where it is written. A model drawn with `DrawModel` and a
skinned mesh's limbs blur by the camera alone, and a frame after others drawn without the HDR frame
blurs nothing. In `models_stress`, where every entity turns each frame, the frame held about 425,000
entities at sixty frames a second with it on and about 700,000 with the camera's alone.

`SetAmbientOcclusion` turns on the `ambient_occlusion` node, after `shadows` and before every
pass that lights the window's meshes, whether or not the frame goes through the HDR target
(`AmbientOcclusionRenderer`). It draws the depth of the window's batches that cast a shadow into a
depth target half the window's size, through the shadow pass's pipelines, whose depth-only pass is
the same at any size, pushing each batch's own camera. `ao.slang` then puts each texel back in the
world through the inverse view-projection, takes its normal from the nearer neighbor along each
axis, and sums Alchemy's term over twelve taps on a spiral turned by interleaved gradient noise,
within the radius held to three tenths of the picture, each fading out toward the radius. Two
passes blur it across and down, nine taps each, weighed by how near each tap's distance from the
eye is to the pixel's. The lights' set binds the result at binding 9 for the window's view, and the
window's lighting buffer says to read it, so `lit` multiplies its material occlusion, which scales
the ambient, environment and probe light alone, by the occlusion at half the fragment's position.
In Manor's rooms it takes 0.08 ms of the GPU and 0.16 ms of the CPU.

Render targets drawn with `BeginTextureMode` stay eight bits and tonemapped as they were. A shader of the program's own drawn inside `BeginMode3D` writes into the
HDR frame as it is, so its sRGB colors are read as linear there.

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

- `textures_bunnymark --stress`, raylib's bunnymark as the benchmark, 32 by 32 sprites of one
  texture bouncing around the window, each a `DrawTexture` call.
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
   and the camera do not see, raises the count next.
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

### Beside raylib

Measured on 2026-10-04 on the same machine, in a hidden 800 by 450 window with no frame rate cap,
each with `StressRamp`'s search, by `build/raylib-bench/run.sh`. raylib at commit `30fa673`, built
in C with `-O2` and its SDL3 backend, drawing through OpenGL 3.3, against this engine's Release
build at `9a9d681c` through Vulkan.

| | raylib | 3DEngine |
|---|---|---|
| Sprites, `textures_bunnymark` | 141,882 in each of three runs | 212,822 to 243,226 over three |
| Cubes turning each frame | 6,403 in each of two runs | 294,024 to 314,537 over two |

raylib draws each cube with `DrawModelEx`, unlit, one call each. `models_stress` lights its
entities by a sun with a shadow and two point lights beside eight skinned arms, and batches them
into instanced draws itself. raylib's counts repeat exactly, and this engine's move by a tenth from
run to run, with the runtime's compiler and collector in the frame.

## What the engine needs

### The device

- **Dynamic rendering** (Vulkan 1.3 core), done, so a pass is a call rather than a render pass
  object with framebuffers to keep in step with the swapchain. A device without it, or without
  synchronization2, fails to start with a message saying so.
- **Synchronization2**, done, so barriers name their stages and accesses in one structure.
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

Normals and lights, Assimp's models with their materials, dynamic rendering with synchronization2,
shadow cascades with point and spot shadows, and bloom and FXAA are built, in that order. What is
left of the order is tonemapping as a full-screen pass in every frame, in place of the curve at the
end of the model pass, which runs there while every effect over the frame is off and over the HDR
frame while any is on (§5).
