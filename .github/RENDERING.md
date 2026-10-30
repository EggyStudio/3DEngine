# Rendering

What the renderer draws, how a frame moves through it, and the order it grows in. The target is a
forward renderer small enough for one reader to read whole, with enough of a frame graph
underneath that shadows, post processing and render targets are added as nodes, and nothing that
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
     clears the swapchain pass, and the model, immediate and ImGui nodes draw into it. Once the
     pass has ended, a node drawing into windows of its own does so in `AfterWindowPass`, as ImGui's
     does for its viewports, each window's swapchain image acquired as it is drawn, waited on and
     signaled by the frame's one submit and presented by its one present beside the main window's
     (`GraphicsDevice.Windows`). The ImGui node draws each command with the texture it names, a
     program's through the set the immediate pass draws it by (`GpuTextures.SetFor`), by the id
     `GetImGuiTextureId` gives (`ImGuiTextures`), and the font atlas where it names none.
- **The immediate pass** (§2) draws the shapes and textures the flat API records.
- **The model pass** (§3) draws the meshes `DrawModel` records and every mesh entity, which
  `MeshEntityDraws` records through each camera entity, lit by the light entities or, with
  none, drawn unlit as raylib draws them (§4).
- **Shaders** are Slang, compiled to SPIR-V by `slangc` and cached (§1).
- **A distance field of the scene** around the window's eye, in cascades built on the GPU from the
  meshes that cast shadows, which ambient occlusion, the sun's contact shadows and particles read
  (§4), and **global illumination** traced through it as Radiance Cascades each frame (§4).
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
second set of files.

`slangc -reflection-json` is read for the uniforms a shader declares at the top level, for every
descriptor in every set, each with its set, binding and kind (a uniform buffer, a combined image
sampler, a sampled image and the sampler declared apart from it, a storage buffer or image), and
for each input of a vertex stage by its semantic, all cached beside the SPIR-V. Descriptor set
layouts are made from it (`ShaderProgram.LayoutOf`), the model pass's material and lights sets
from `model.slang`, the bloom, composite and FXAA passes' from their shaders, and a program's own
shader's set 0 joined with what its pass binds (`ShaderProgram.Merge`), so a binding added to a
shader reaches its pipeline with no layout written for it. The vertex formats are the engine's,
each attribute written once beside its pass with the semantic it is read by
(`VertexStream`), and a vertex stage of a program's own is fed each at the location Slang gave its
semantic, so it may declare its inputs in any order. A stage whose inputs carry no semantic, or a
cached one from before they were kept, is fed at the engine's own locations.

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
allows. A render target's colors are made with storage usage where the device stores their
formats, asked once a format (`StoresTargets`), and as a transfer's destination. Where it does not,
as some devices do not store the window's eight-bit BGRA, the shader writes a stand-in of eight
bits a channel in RGBA order or of the target's floats, which every device stores
(`GraphicsDevice.CreateStandIn`), kept with the target's texture. The dispatch blits the target into
it before the shader and blits it back after, a blit taking each channel into its own where a copy
would move the bytes as they lie, and a test writes one target both ways on a device that stores
it and compares the pictures. A texture reaches the GPU in the frame after it is loaded, so a
dispatch before then is skipped with the reason in the log.

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

Every mesh draws through one pass, `ModelNode`, before the immediate shapes, the flat API's
models and the ECS's mesh entities alike. A mesh is uploaded once into host-visible vertex and index buffers (32-byte
vertices of position, normal and texture coordinate, 32-bit indices) through `MeshStore` and
`GpuMeshesPrepare`, and `DrawModel` records a mesh, a world transform, the camera and a material
each frame. Draws of the model pass's own shader that share a mesh and its five maps are one
instanced draw, in the order each such batch first appears. Each draw is an instance of 96 bytes
in a second vertex buffer stepped per instance (`ModelRenderer.Instance`, `ModelInstance` in the
shader), holding the world matrix as three rows of a 3x4 (the rotation for normals and the
translation for world positions) and the material's factors. The camera's view-projection is a
push constant for the batch (`modelPush`), as a light's is in the shadow pass, so draws recorded
through two cameras are batched apart. A mesh's colors and second texture coordinates are vertex
buffers at bindings 2 and 3 where it has them, and its stream of the program's own (`MeshStream`)
one at binding 5, past the subsurface pass's binding 4, read as `CUSTOM0` by a vertex stage of the
program's own alone, which a mesh without it feeds from sixteen bytes of zeros at a stride of 0 in a
format of the input's class; a write of the stream in place goes into the next of a ring of buffers,
as replaced vertices do. A mesh uploaded from its positions alone gives twelve bytes a vertex at
binding 0, its normal and texture coordinate read as zeros at binding 6 at a stride of 0, and every
pipeline that binds a mesh's vertex, the model pass's own and a shader's of the program's own, the
solid and masked shadow pipelines and the velocity pass's, has a form for it, picked by the mesh as
it draws; the pass that scatters light under a surface leaves such a mesh out. The CPU keeps its
positions with zero normals, which the field, the glow lights, picking and bodies read as any
mesh's. `model.slang` shades by the frame's lights, or when the
world has none and no environment draws the color unlit, texture times color with the material's
emissive light, as raylib's default shader draws a model.
`model.slang` is built on the `modelpass` module, which a model shader of the program's own imports
too. A draw with one is drawn by a pipeline made from it, and its uniforms, copied when the draw
was recorded, reach it in a uniform buffer at binding 0 of the first descriptor set, where Slang
puts uniforms declared at the top level, beside the texture at binding 1.

A mesh entity is a `Mesh` and a `Material`. The mesh is positions three per triangle, or vertices
and the indices of their triangles, with optional normals and texture coordinates, or a mesh the
program uploaded with streams of its own. `MeshEntityDraws` uploads the arrays once, keyed by the
positions array, frees them the first frame no entity holds them, drawn or not, and copies the
material's base color, normal and metallic-roughness texture assets into `TextureStore`. A
triangle without normals is lit by its face's normal, and an indexed vertex by the mean of the
faces it is a corner of. A material that names a program's shader is drawn as `DrawMesh` draws
with one, a draw of its own, the shader's values copied once a frame for every entity drawn with
it. An entity whose `Visible` is false is drawn nowhere, and one whose `ShadowOnly` is true is
drawn into the shadow maps alone. A thousand meshes of their own, a box each in a grid of 32 by 32
under a shadowed sun, drawn through `DrawMesh` each frame cost 0.21 to 0.25 ms of the program's calls
and 0.53 to 0.61 ms of `model_batches`, and as entities standing still 0.15 to 0.19 ms of
`MeshEntityDraws` and 0.35 to 0.47 ms of `model_batches`, through the engine's shader and through
`toon.slang` alike, frames of 3.1 to 4.1 ms either way.

**The order of the opaque draws, read on 2026-10-25.** The voxel game's sections drawn through
`DrawMesh` took twice the GPU's scene pass the same sections took as entities, 19.8 to 21.2 ms
against 9.8 to 10.3 in its cherry grove at 1920 by 1080 at High on 5.2.313, with the same draws,
triangles, pipelines, culling, depth state and coverage of the depth pass. The cause is the draws'
order. Each section's solid faces, its faces cut out and its lava came by turns, each a batch alone
through the terrain shader's solid pipeline, which keeps the GPU's early depth test, or its cut-out
one, so the pass bound a pipeline 315 times a frame, where the entities, gathered by their look and
drawn after the game's plants, bound one 7 times. On a scratch copy of the game with the engine at
`23b4793b` the scene pass read 13.1 to 14.1 ms for the sections through `DrawMesh` and 9.3 to 10.8
as entities; the `DrawMesh` sections' batches bound by their pipeline read 9.2 to 10.6, nearest
first within each pipeline 8.4 to 9.0, and nearest first alone 11.8 to 13.0. The model pass draws a
view's opaque batches by the pipeline each draws with, in the order each pipeline first comes and
nearest first within it, each run between two batches that write no depth apart, so a sky drawn
first with depth writes off keeps its place (`ModelRenderer.Order.cs`). With it the sections
through `DrawMesh` read 8.9 to 12.9 ms where 14.4 to 21.2, and as entities 12.4 to 14.6 where 10.5
to 15.0, another program holding part of the GPU through both, the frames within the spread two
runs of one build show, an RMSE of 0.0011 to 0.0057. The thousand meshes above, in Release, take
0.03 to 0.15 ms more of the scene pass's recording where all are of one pipeline, 1.44 to 1.58 ms
where 1.07 to 1.46, and through `toon.slang`, solid and cut out by turns, 2.45 to 2.60 where 3.19
to 3.30, the frame 6.96 to 7.26 ms where 7.56 to 7.99, through `DrawMesh` and as entities alike.
`./e3d command models.draws` says how many pipelines the views' passes bound.

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

An opaque material's batches are kept apart from the others, and their pipelines set the fragment
stage's specialization constant 0, `solidMaterial`, which folds `alphaTested`'s discard away. A
stage that can discard lets a device turn its early depth test off, as lavapipe and the tiling
GPUs do, so a fragment behind a wall would be lit before the test threw it away. The development
machine's RTX 4070 tests depth early under a discard and defers only the write, so the fold saves
nothing there. In the voxel game's closed room on a flat world, the land outside behind the walls
cost the model pass 0.34 to 0.42 ms at eight columns against two before and after, with the glow
lights' march per fragment on or off. That cost is the vertex and raster work of 289 columns of
sections against 25, all inside the frustum, which a depth pass ahead of the model pass would add
to rather than remove. Culling the sections the eye cannot see removes it, the game's own flood
fill through its sections' open faces first.

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
four joints and weights a vertex (`MeshStore.SetSkin`), and `GpuMeshes` makes a `GpuSkin` for it,
which holds the vertices at rest, the joints and the weights in storage buffers, a vertex buffer the
posed vertices are written into, which the model pass draws, and a ring of buffers for the joints'
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
(`ModelAnimation.KeyframeMorphWeights`), and `SetModelMorphWeight` poses the model again as its bones
were last posed. A clip played on part of the skeleton (`UpdateModelAnimationLayer`) takes each
bone's pose relative to its parent from the second clip from a bone down, and composes it onto
where the first clip puts that bone's parent.

Particles take the skins' path too. `ParticleExtract` copies each `ParticleEmitter`, placed by its
entity's world matrix, with the window's camera (`MeshEntityDraws.WindowCamera`) and the frame's
seconds, held to a tenth of a second. `ParticleRenderer` keeps a storage buffer for each emitter, a
header of eight float4 values and then two a particle (position and age, velocity and life), and the
`particles` node, after `skinning`, records a dispatch of `particle_step.slang` for each, a thread a
particle, with everything the step and the draw need in its 128 bytes of push constants. The frame's
births are its share of the rate, the fraction owed carried to the next frame, and its burst, in a
run of slots after the last frame's, so the oldest are replaced, each started within the emitter's
radius with its velocity turned within the cone and its speed and life varied by a hash of its slot
and a seed of the frame and the emitter. Only the slots that may hold a live particle are stepped,
sorted and drawn, counted back from the newest, which the CPU bounds by the runs of births of the
frames before and the longest life a particle of each can draw, so a dispatch of those slots, a
thread's slot that many back from the newest, meets every particle alive, and an emitter whose
particles have all died records nothing. The step writes the newest slot into the header's fourth
value, which the draw counts an instance back from where no sorted key names its particle. Twenty
empty emitters of the voxel game's sizes, lit and laid over by alpha, read 0.19 ms of
`cpu.particles` and 0.26 of `gpu.particles` taking every slot, and 0.027 and 0.000 taking those
alone, against 0.008 and 0.000 with no emitter. The first thread writes the colors, sizes and brightness
into the header, which the draw reads, so nothing of an emitter is written from the CPU while a
frame in flight reads it. The header's next three values are the rows of the matrix the draw places
a particle by, written into the command buffer before the dispatch, the identity for particles in
the world and the entity's world matrix for those an emitter keeps in its space (`Local`), which
start about its middle there, turn with it and meet nothing. A program's `Points` are a buffer of
their own, made again when the points change and stepped by no dispatch, its whole header written
each frame the same way, and an emitter laid over by alpha sorts each buffer from the eye in the
space its particles are kept in. A particle alive falls by gravity and slows by the emitter's drag,
as an exponential of the step, whose bits ride in the push block's last word. Where an emitter
collides, the node first draws the window's meshes that cast shadows into a depth at half the
window's size, with `DrawDepth`, as ambient occlusion draws its own, and binds it with the view it
was drawn through as the step's second set, one written each frame in flight, and the white texture
with collision off in frames none collides. The step finds a particle's pixel in that depth, the
surface there in the world through the inverse view-projection, and its normal from the texels
beside it, turned toward the camera, and a particle that crossed that surface's plane from the
camera's side in the step bounces off it, keeping the share of its speed into it that rides with the
collision's two bits in the capacity word's spare high bits, or ends there. A particle already
behind a surface, as one passing behind a post, crosses nothing. On `shaders_particles` the depth
costs the graph about 0.03 ms of CPU and the GPU about 0.01 ms. The draw (`particles.slang`) imports
the model pass, binds its material set with the emitter's texture as the base color, white without
one, and the window's lights set as sets 0 and 1 and the particles as set 2, and draws six vertices
an instance, a square facing the camera's eye that `eyeInWorld` finds from the view-projection, or
the way the header's eighth value gives, the emitter's normal or the particle's velocity turned as
the particle is placed, its normal turned toward the eye so it is lit from either side, over
the window's scene decoded to linear light (§5) or after a target's meshes into the target, depth
tested and not written, added or laid over by alpha. The square is a round dot, or the texture
tinted where the emitter has one, whole or the frame of a sheet the share of the life gone picks,
mixed with the next frame by how far the life is through its own where the emitter blends them, the
branch on the emitter's setting so both samples are taken where every pixel takes them. The look's
last value packs whether it is lit and textured, the sheet's columns and rows and whether they blend
into an integer a float holds exactly, since the push block has no room left. A lit particle goes
through `lit` as a rough surface facing the camera and an unlit one through `toDisplay`, so both
follow the view's output flag, and the window's particles undo the window's encoding, since they are
drawn over its decoded light, so an additive cloud adds up in linear light as emitted light does.
Emitters laid over by alpha are drawn after the additive ones, from the farthest from the camera's
eye to the nearest by where each emitter is. Within an emitter laid over by alpha,
`particle_sort.slang` sorts after the step, in the emitter's buffer after its particles, a key a
particle of its negative squared distance from the window's eye, a large one for the dead and
infinity for the padding to a power of two, by Batcher's bitonic sort. Blocks of 512 keys are sorted
in a workgroup's shared memory in one dispatch, and an emitter of more takes a dispatch for each
step across blocks and one for the steps within them after it, so 400 particles cost 0.02 ms of the
GPU where a dispatch a step took 0.47. A render target drawn through a camera of its own sorts them
again from its eye before its pass, and the window again from its own before its scene's particles
are drawn where a target sorted after it (`ParticleRenderer.SortFor`), each emitter keeping the eye
its buffer was last sorted from. An emitter of 300 in `games/Sumo`'s two views added 0.025 to 0.03
ms of the GPU to `targets`, as `./e3d command profile` gives it, 0.38 against 0.35. The step clears
and the sort sets a flag in the header, by which the draw reads each instance's particle through the
sorted keys. `TargetsNode` draws them into each render target after its meshes, with its own lights,
since the step runs before the targets, through the camera of the target's first `BeginMode3D`,
which `Mode3DCamera.Targets` keeps, or the one its meshes were drawn through for a camera entity's
texture. A target drawn only in 2D has no camera for them. A reflection probe's capture draws them
into each face after its meshes, through the face from the probe's middle, lit by the capture's
lights, so a fire in a room shows in its metal.

## 4. Lights and shadows

`LightExtract` copies every `Light` entity into the render world, and `LightingUboPrepare` packs up
to 16 into one uniform buffer per frame, which `ModelRenderer` binds as a second descriptor set from
a ring of one per frame in flight. The set's images, seventeen of them, are bound apart from two
samplers they share, one blending and one reading the nearest texel, since Metal allows a stage
sixteen samplers and a combined image sampler counts as one, so the model pass's fragment stage
reads seven with the material's five maps and a program's shader has nine left. The device reads its
limits of samplers and images a stage when it starts and refuses a pipeline past them with a
sentence of the counts (`GraphicsDevice.Limits`), where the driver would draw nothing, and a test
holds every built-in shader to sixteen. A `Light` is a kind (directional, point, spot or ambient), a
color, an intensity, a range and a spot's inner and outer angles, and each is one 64-byte entry.
`lights.slang`, which `modelpass.slang` imports beside the set it reads (`lightset.slang`), adds
each light by Lambert's cosine, a directional light by its direction, an ambient light everywhere
alike, and a point or spot by the square of the distance, brought smoothly to nothing at its range
and cut by a spot's cone. The light that arrives is reflected by the material's metallic-roughness
model. Each light but an ambient one reflects by GGX's distribution, Smith's height-correlated
shadowing and Schlick's Fresnel, toward a camera the shader finds from the transform alone, since
the push constants have no room for its position, and scatters diffusely what Fresnel leaves, none
of it off a metal. An ambient light scatters its diffuse share and reflects the share Karis's fit of
the environment term gives with no environment to reflect. A light's color times its intensity is
the light a white diffuse surface facing it returns, so the diffuse term is the albedo itself and
the specular term is multiplied by pi to match. Fresnel's base, one less the dot of the view and the
half vector, is held to 0 to 1, since with a point light at the camera's own point, as a game's held
light is, the half vector is the view's and their dot rounds a hair over 1, and lavapipe takes a
power as exp2 of a logarithm, so the fifth power of a base a hair under 0 was no number and a room
lit so read 12,617 of 57,600 pixels black in specks where the RTX 4070 read none
(`PointLightAtEyeTests`).

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
entities and no environment draws its models unlit, so a color near white keeps its shade as
raylib's does. Where the curve runs depends on the view, which a flag in its lighting buffer,
`output`, tells `toDisplay` and `unlit`. The window's view, `output.y`, writes its light into the
HDR frame sRGB-encoded with no curve, carried on past 1 (`linearToSrgbPastWhite`), and the curve
runs once over the frame in the composite (§5), after the frame's blending, from the module
`color.slang` both import. A render target's view, neither flag, has eight bits to hold its light,
so its curve and encoding run at the end of the model pass. A reflection probe's faces, `output.x`,
keep their light linear.

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
draws is in front. The draw goes through `toDisplay` and the curve like a reflection does, and is
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

A `ReflectionPlane`, a world resource `SetReflectionPlane` sets, is a plane whose glossy surfaces
mirror the scene as the camera mirrored in it sees it. `MirrorNode`, after the probes and before the
window's scene, draws the window's batches again through the window's view-projection mirrored in
the plane and turned upside down, which keeps each face's order of corners, with its depth row
replaced by the plane scaled to put the frustum's far corner at depth 1 (Lengyel's oblique near
plane), so what lies under the plane is clipped rather than mirrored up through it, and a batch
wholly under it is culled by that near plane before it is drawn (`ModelRenderer.Draw` with
`depth`). They are lit as a probe's faces are (`ModelRenderer.ProbeCaptureLights`), by the window's
lights and shadow maps with no screen's probes, ambient occlusion or traced reflection, into a
half-float target at half the window's size with a depth of its own, cleared to the window's clear
color in linear light, the particles drawn over them, and the picture is copied into an image with
its mips (`GraphicsDevice.RecordPlanarReflection`) at set 1's binding 38, white where no plane is
drawn. The lighting buffer carries the plane, the image's last mip, how far off the plane a surface
still takes it, and one over the window's size. `mirrored` in `lights.slang` reads it at the
pixel's place turned upside down, at the mip `min(lastMip, 6)` times twice the roughness, all of it
on the plane and none a tenth of a unit off it or with the normal more than 26 degrees from the
plane's, and the model pass lays it over the traced or the probe's reflection by that share, faded
out from a roughness of 0.25 to 0.5 as the trace is. Nothing is drawn while the eye stands under the
plane, nor for a render target or a probe's faces. In `shaders_reflections` the `mirror` node takes
0.046 ms of the GPU at 800 by 450 with global illumination off and 0.27 ms at 1920 by 1080 at
`High`, and 0.11 to 0.16 ms of the CPU. Summit has no water, and with a sea of roughness 0.05 added
at a height of -0.5 for the reading it takes 0.10 ms of the GPU at 960 by 540 and 0.16 ms at 1920
by 1080 and 0.15 to 0.17 ms of the CPU, the frame 1.23 to 1.25 ms without the plane and 1.35 to 1.38
ms with it. In the voxel game's cherry grove at seed 1, 2,241 draws, it takes 1.3 to 1.7 ms of the
GPU at 800 by 450 and 1.5 to 2.1 ms at 1920 by 1080 over the grove, 2.2 to 2.8 ms looking along its
river from four blocks over the water, and 3.7 to 4.1 ms of the CPU, which the window's depth pass
takes for the same draws (4.1 ms), so the frame, bound by the CPU, takes 3 to 5 ms longer. Before
the batches under the plane were culled the CPU took 4.5 ms, the sections under the river being
few beside the land over it.

The first directional light with `CastsShadows` set casts the frame's one shadow, in three cascades.
`ShadowFit` cuts each view's camera out to 150 units into slices ending at 12, 45 and 150 units, or
out to the distance `SetShadowDistance` puts in `ShadowSettings` in the same proportions, and fits a
tile of a depth map two tiles on a side, 2048 texels a tile unless `SetShadowMapSize` puts another
size in `ShadowSettings`, to the sphere around each, so the near slice spends its texels on a few
units and the far one on many. Each is moved in whole texels so the edges of shadows hold still as
the camera moves, and reaches four radii further toward the light for casters above the view. Ten
spot lights with `CastsShadows` set draw into the fourth tile, ranked by what they matter to every
camera the frame draws meshes through, the window's and each render target's. Those whose reach
(their range around them, or the shadow distance for one with none) a camera's frustum holds come
first, then those whose light reaching an eye is greatest, their brightness over one plus the square
of how far their reach is from it, times the share of that camera's picture their reach covers, its
box put through the camera and held within the picture, the whole of it where the box reaches round
past the eye (`LightingUboPrepare.Rank` and `Share`), the most of any camera, then those whose reach
comes nearest one. So a lamp lighting a wall across the view keeps its shadows over a brighter one
lighting a corner of it, at no cost that can be read, `prepare.LightingUboPrepare` in `./e3d command
profile` taking 0.010 ms in Wick's first doorway and 0.017 to 0.020 ms in Manor's hall with the
share and without. The tile is the whole of it for one, a quarter each for up to four, and past four
a quarter each for the first two and a sixteenth each for the rest, in its lower half
(`ShadowFit.SpotTileArea`), each through a perspective projection from the light as wide as its
outer cone and as deep as its range, or the shadow distance for a light with none. A shadowed spot
light carries its slot, counted from one, in its cone's third component, as a point light does, and
its projection and texel width ride in the lighting buffer. `ShadowNode` clears the map once and
draws the window's meshes into each tile in use, before the window's passes, with `shadow.slang`'s
vertex stage and no fragment stage, through a depth-only pass (`GraphicsDevice.CreateShadowMap`). A
render target that draws meshes has cascades fitted to its own camera and a lighting buffer of its
own (`TargetShadows`), and `TargetsNode` draws the map for it before its pass, so a split screen
drawn into two textures shadows each view. Views whose cameras are near enough share one set of
cascades fitted to all of their cameras (`ShadowFit.FitCascades` over several), each joining the
first group whose first cascade fitted to all of it is no more than a quarter wider in its texels
than any member's own (`LightingUboPrepare.SharedTexelGrowth`), so a view reads its shadows from the
shared map as from its own, and a group drawn one after another draws the map once, by its first
view with that view's meshes (`ModelRenderer.ShadowMapsDrawn` counts the draws). With no sun every
view's map is the same and all share it. Sumo's two views, facing each other across the ring, share
theirs, and its render textures take 0.35 to 0.37 ms of the GPU in `targets` where they took 0.45,
and a view of that layout differs from itself drawn alone at 77 of 28,800 pixels, along its shadows'
edges (`SharedShadowTests` holds two near views and two far apart to their own). The window's map is
drawn after every target, where it shares none with the last of them, and the point lights' faces,
the same from every camera, once a frame. Each view draws its own batches and instances, which its
model pass draws after it, reading each instance's world matrix, color and cutoff, and each tile and
face pushes its light's view-projection, so the frame's instances are written once for both passes.
A batch is gathered by the kind of shadow its draws cast as well, none, solid or masked, and a
masked one is drawn with `shadow.slang`'s fragment stage and its maps, which cuts it out below its
cutoff as the model pass does, so its shadow has its holes. A blended draw that is clear anywhere is
drawn the masked way too, its fragment stage keeping a texel where its alpha passes a threshold that
interleaved gradient noise spreads over the texels, so the nine samples the model pass averages give
a shadow as dark as the surface is opaque. The map is bound at binding 1 of the lights' set, beside
the cascades' matrices and texel widths in the lighting buffer, and the white texture takes its
place in a frame with no shadow. The shader takes the nearest cascade whose tile holds the point, a
little inside its edge, moves the point off its surface by a texel and a half of that cascade along
its normal turned to the sun's side (`litSide` in `lights.slang`), and averages nine comparisons
around it. A two-sided face lit from its back, as a plant's thin wall is, has its normal facing away
from the light, and moved along it as given the point went behind the face as the light sees it, so
the face shadowed itself in speckles and stripes. In a field of wheat's walls a block tall under the
voxel game's sun at ten in the morning, 56 degrees up, the walls' light spread 0.149 of its mean
within each wall against 0.053 in a path-traced reference, read as each wall's standard deviation
over its mean at High at 800 by 450 and weighted by its pixels, and turned to the light's side
0.054; a step toward the light for a face the sun grazes, tried beside it, changed nothing more. A
test's wall lit from its back reads 0.4403 as its twin lit from its front does, where it read
0.1909, spread 0.298 of its mean. The voxel game's plants, whose shader reads the shadow through the
same lookup, are the first to gain. A spot or point light's lookup turns the normal to its lamp's
side the same way. Across the outer fifth of a tile the next cascade is read as well and blended in,
so the shadow's softness changes over a band where one cascade gives way to the next, and past the
last cascade's band the shadow fades out rather than ending at a line. A spot light's texels widen
with distance from it, so its offset grows with that distance. A model shader with a vertex stage of
its own casts the shadow of its mesh as it was before that stage moved it.

Past the last cascade a fill cascade reaches on to the camera's far plane (`ShadowFit.TryFitFill`),
which `SetShadowFill` turns off and the cascades' count leaves out. It is fitted as the others are,
to the sphere around the views' slice from the shadow distance to their far planes, and is fitted
none where every far plane comes first or a projection has none. Since the map's four tiles are
taken, it is drawn into a depth image of its own of one tile, after the map and with its pipelines,
and bound at binding 37 of the lights' set, the white texture in its place where a view has no
fill. The lighting buffer carries its matrix and its texel's width in the slot after the counted
cascades', a width of 0 saying there is none, and `lightReaching` reads it as the cascade after the
last, which gives way to it across its outer fifth as to the next of its own. Through `Camera3D`'s
45 degrees at 16 by 9, its far plane of 4,000 units and the shadow distance of 150, its texels are
3.8 units wide where the last cascade's are 0.11, and its depth bias, the same 0.0005 of the depth
range as each cascade's and so about three of its texels, is 11.7 units, so a caster lower than
that leaves no shadow in it and the fill shadows the land with its hills and buildings. The
share of the point the map holds that the contact shadows past the map's reach are taken by counts
the cascades alone, falling to 0 across the last one's edge whether the fill reads on past it or
not, so those contact shadows go on over the fill's coarse texels. A test holds a square's shadow
on a wall at twice the shadow distance with the fill and none without it.

**The sun's width, read on 2026-10-26.** `SetShadowSoftness` gives the sun a width across in
degrees, carried in the lighting buffer as twice the tangent of half of it, the edge's spread per
unit of a caster's height over a point, after the environment's light. Where it is set, each cascade
is read as percentage-closer soft shadows read a map (`penumbra.slang`). Sixteen depths on a
golden-angle disc search for what lies in front of the point over the width a caster at the
cascade's near plane would spread the sun to, held to 24 texels either side; where they find none
the point is lit, where all sixteen find one it lies in the shadow's core, and between, sixteen
bilinear comparisons are taken over the width the found casters' mean depth spreads the sun to, a
texel at least, the disc turned by a hash of the point. Each tap is compared with the surface's own
depth there, its slope across the map solved from its normal and the cascade's rows and held to that
of a surface 80 degrees from the sun, so a wide edge on a slope does not shadow the slope, and the
taps stay a texel and a half inside the tile. A pole 5 units tall under a sun 45 degrees up has its
edge between a quarter and three quarters of the light 0.04 units across at its foot and at its tip
with no width, and 0.08 and 0.24 at 3 degrees (`ShadowSoftnessTests`); read over twice the sun's
spread, as first written, the foot's was 0.14. With no width the cascades are read as before and the
bounce rooms and the voxel game's grove draw as they did. At the sun's 0.53 degrees the bounce
rooms' scene pass took 0.04 to 0.06 ms more at 800 by 450 and 0.25 to 0.41 at 1920 by 1080, and the
grove at 1920 by 1080 takes 7.4 to 7.9 ms at Low where 6.5 to 6.7 and 8.0 to 8.4 at High where 7.3,
the same at 3 degrees, most of it the search at every point the sun reaches; the way out in the
shadow's core took 0.2 to 0.3 ms off it.

What looks like faces fighting in a sunlit scene of the voxel game is the sun's map read where it
is coarse or where a face shadows itself, and global illumination has no part in it. In a flat
world at ten in the morning at 1280 by 720 at `High`, a field of wheat, whose thin walls are
two-sided and masked, carries speckled stripes of dark and light that move as the eye turns, the
walls shadowing themselves, and a pond beside it takes the wheat's shadow in square steps, the grid
of eight texels a block the game chooses for its shadows. Both stand as they were with global
illumination off and go with the sun's shadows off, and no face of the game's blocks, its water or
its leaves fights another. The pattern the screen's probes leave on the grazing floor of
`shaders_bounce_rooms`, rows of scallops at their tiles, reads under a level of sRGB light, seen
only with its contrast stretched.

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

### The scene's distance field

`SetSceneField` builds a signed distance field of the meshes drawn into the window that cast
shadows, in cascades of 64 cells a side around the window's eye, each twice as coarse and as wide
as the one before, for the passes that need to know what the window's depth does not hold
(`SceneFieldRenderer`, `GraphicsDevice.SceneField`). The cascades lie one after another along z in
one 3D image of half floats, which every pass binds once with a uniform buffer saying where each
lies (`scenefield.slang`). A cell holds its distance to the nearest surface in world units, below
zero behind a face of a mesh that is not double-sided, exact within four cells and held at four
beyond, so a trace through it steps at least that far through open space.

A far cascade past the counted ones, which `SetSceneField`'s `farCascade` asks for and its count
leaves out, lies last in the image, 64 cells a side as each is. Its cells are as wide as reaching
that distance ahead of the eye from its lead takes, held to the window's camera's far plane, at
least twice the last counted cascade's and rounded up to a sixteenth of that, so a far plane read
from the camera's matrix gives the same cells each frame (`SceneFieldPlan.FarCellFor`). The uniform
buffer's counts give its place in their fourth component, and nine cascades fit. It is planned,
built and stamped as the others are, under the cascades' and the triangles' budgets, the finest
first. Global illumination gives it a cascade of probes after those the quality traces, as many
directions as the cascade before it, standing in it where each other stands in the field's cascade
of its number (`cascadeAt` in `scenefield.slang`, `SceneFieldPlan.CascadeOfProbes`), so at `Low` with
four counted cascades its probes follow the second's. The far cascade is read only by a ray that
leaves the counted cascades. The cascade before its probes traces as the last does, on to the far
cascade's diagonal, its rays meeting the far cascade's cells only once they have left the counted
ones and bringing the sky where they meet nothing, and the merge takes nothing above it. The far
probes give their light to the pixels past the counted cascades alone, the last counted cascade
fading into them no more across its band (`fadesOn` in `cascades.slang`). The far cascade holds a
double-sided sheet or a wall thinner than a cell as thin as the last counted cascade holds one,
where half its own cell, 37 blocks at the voxel game's far plane, held the flat world's ground of
four blocks as a slab rising past the eye. Merged from the far probes, which stood in that slab, the
cascade before them took their light over its own sky and their share of the sky in place of its
own, and the voxel game's play check's glass frame read 168, 157, 106 with the far cascade at the
far plane where 185, 167, 126 without it, its red wall 139 where 154, in sRGB levels at an exposure
of 1; with both mended they read the same either way. The bounce rooms' grazing floor at `Low` with
a far cascade read 0.108 of its light from its reference, 0.069 since, and their other views alike
within 0.007. Seed 1's deep cave reads the same with the far cascade and without it, its tops 90
blocks ahead 9, 13, 18 at High, which is the game's fog alone, 0, 0, 0 with the fog off, since no
cascade's light reaches them. At seed 1's cherry grove at 1920 by 1080 the far cascade costs the
scene pass 0.2 to 0.3 ms and global illumination 0.25 ms, 8.0 to 8.3 against 7.8 to 8.0 and 2.6
against 2.4, as it did before the mend. A probe
of the first cascade facing a black wall 40 units off, past two counted cascades of 0.15, read 1.14
of a sky of pi where it read 3.14 without the far cascade (`GlobalIlluminationTests`). On lavapipe,
by `./e3d command profile`, the Cornell box's field at its three cascades and a far one to the far
plane took 27.0 MB in place of 21.5 and its probes 8.68 in place of 6.04, global illumination 263 to
282 ms in place of 222 to 231 for a third more rays, and a frame building two cascades 17.4 ms in
place of 17.1. Ore's at its four cascades of half a block and a far one to 384, cells of 8, took
32.5 MB in place of 27.0 and its probes 11.32 in place of 8.68, global illumination 509 to 532 ms in
place of 445 to 460 for a quarter more rays, and the field's builds 61 ms a frame in place of 66, the
same within the noise.

A cascade's corner lies on a grid of eight of its cells, so it moves only when the eye has gone
that far, and where it moves, or a still mesh came into it or left, it is built again, as many a
frame as the budget allows, the finest first (`SceneFieldPlan`). Until then a cascade keeps the
place and the meshes it was built with, which the uniform buffer says, so a pass never reads one
cascade at another's place. A mesh is still when it has been drawn the same, mesh, vertices, matrix
and sides, for eight frames running. A frame that draws the meshes the frame before did, in the
same order, is read by place, each mesh compared with the one at its place, where working each
frame out whole hashed every mesh's instance several times. In a Release build of the voxel game at
eight columns, 1,836 meshes, the plan took 92 µs a frame in place of 1,600 and the field's node
0.39 ms of the CPU in place of 1.8 to 2.0, by `./e3d command profile`. A still mesh replaced in
place, by a mesh of other vertices drawn through the same matrix where it was, as a game's section
meshed again when a block is placed, stays in the field as it was until its replacement is still,
and the replacement is not stamped meanwhile, so one build takes the one out and puts the other in,
nor after it settles while a cascade around it the budget leaves for a later frame holds the one it
replaced. Stamped there as its boxes, a section of the voxel game's ground filled the air above it
in the two cascades a budget of two left, and a closed stone room lit by one glowstone went dark for
the frame eight after a block of its floor broke and came back the next, 89 and 87% of the pixels
changing by more than 8 levels in the two frames, where only the frame of the break changes, 9% of
them (`SceneFieldTests`, `voxel.flicker`). A mesh that moves keeps its vertices and is stamped as
before, and one kept for a replacement that never settles leaves after sixteen frames. Three walls
around a lamp that lights them by indirect
light alone, replaced with a block beside the lamp, read 116 on the wall before and 69 six frames
after, the walls gone from the field at once and their replacement standing in as a few boxes; kept,
the wall held 114 to 116 until the replacement settled (`GlobalIlluminationTests`). Its triangles go
into one buffer in its mesh's own space the first time a build needs them, so a build hands the GPU
each instance's matrix and where its mesh's triangles start. A build clears a word a cell to the
band, then a workgroup a triangle puts the triangle in the world and takes, for each cell within
the band of its bounds, the distance to it in 1024ths of a cell above a bit set where the triangle
is double-sided, a bit set where the cell is in front of the face and the way the face looks in
eight bits, keeping the least by an atomic minimum (`field_splat.slang`). Of two triangles as near,
as a crate on the ground, the one the cell lies behind wins, the cell being inside some mesh, and a
cell is behind a face only within 60 degrees of straight back from it, so where an edge or a corner
is nearest, as above a pillar's rim, a face the way runs along does not put the cell inside. A cell
whose nearest point is on an edge no other triangle shares by the places of its ends
(`SceneFieldRenderer.OpenEdges`, the corner's w in the pooled triangles) is in front, so a ground
plane puts no wedge below its rim inside. A triangle of next to no area beside its longest side is
passed over, since its face turns whichever way rounding took it. `GenMeshSphere`'s last row meets
its pole at corners the rounding of sin(pi) leaves apart, and taken as faces they turned 97 to 143
of the 1662 cells within half a unit of a sphere inside, beyond the pole, where none is now
(`SceneFieldTests`). The face a cell lies within 25 degrees of straight behind is kept apart as
well, in a word a cell. A second pass turns the words into distances in both the image the meshes
alone make and the one the passes read (`field_resolve.slang`), half a cell less where the nearest
triangle is double-sided. A double-sided mesh has no inside, and a sheet of one between two rows of
cells would leave half a cell in each, which a trace steps over, so held half a cell thick on
either side it crosses zero wherever it lies, as the walls and floors of Manor's rooms, imported
from OBJ files, need. A closed wall thinner than a cell may have no cell's middle inside it either,
and a cell in front of one face and less than a cell before another straight behind it that looks
the other way takes the middle of the two, less half a cell, so the wall is held as the sheet is.
The faces of a box's edge, which meet square, are not taken for a wall. It took a cascade's build
in `shaders_scene_field` from 0.44 ms to 0.46 on the GPU (`field.rebuild 4000`, Release). A cell
outside every surface whose neighbors along two axes lie inside one while the cell across the
diagonal from it lies outside, the pattern the cells about an edge two blocks meet along make and
no single face makes, is held inside at its distance. Between the four cells about such an edge
the distance never fell below zero, so in the voxel game's room of stone whose wall steps a block
over halfway, its halves meeting along one edge, a march from the floor east of the wall saw half
of a glowstone west of it through the edge, and the floor about the edge's foot read 21 sRGB
levels at 1280 by 720 at `High` with the screen's probes left out and 20 with them, where a
path-traced reference reads none and the edge closed by a block 2 and 0; held inside, it reads 2
and 0 (`GlobalIlluminationTests`). No view of the bounce rooms moves from its reference by 0.001 at
800 by 450 or at 160 by 90, and a cascade's build in `shaders_scene_field` takes 0.578 ms where
0.557 on the development machine's GPU (`field.rebuild 4000`). A second
dispatch of the splat paints each cell the color and the emission of the triangle whose word
it kept, the material's color times its texture's average in linear light
(`TextureStore.AverageColor`), times its vertices' colors where its mesh has them, each corner's
sRGB bytes pooled in the bits of its place's fourth word and blended at the cell's nearest point as
the model pass blends them across the face, into an image of each beside the distances, for global
illumination, so a mesh of many colors, as a voxel game's section, reflects each of them; a
mesh still settling is stamped in its color times its vertices' mean. Built every frame in the
voxel game at eight columns, the field's node took 0.46 to 0.48 ms of the GPU where it took 0.41 to
0.45 (`field.rebuild 4000`, Release). An emitter thinner than a cell, as a strip on a wall, may be
no cell's nearest surface, so where an instance's thinnest extent is under a cell a third dispatch
lends the cells within a cell of its triangles its light and the area of its faces near each, those
a closed mesh turns toward the cell, within a square a cell wide about the cell's middle laid on
the face, seen along each axis, and the resolve gives a cell the larger of its nearest surface's
light and the lent light times the share of a cell's face those faces cover along the axis they
cover most of, a whole face at most. The lend's buffer, six words a cell, 6 MB, is made the first
time a build has such an emitter. A mesh that moved more recently, a skinned one, and a still one
whose cascades are not yet built again, but for one that replaced a mesh in place, are stamped each
frame as boxes, the nearest the eye first to 256 boxes, into the bricks of four cells they come
within the band of, each cell the least of
the still image's distance and those of the boxes that come within its brick, which the plan lists
brick by brick (`field_stamp.slang`), and the bricks stamped the frame before are stamped again so
a box that left one is gone. A cell is painted the color of the nearer of the still meshes and the
boxes, a box its mesh's color as the splat reckons it, from a copy of the still meshes' colors the
resolve writes beside the image the passes read, so a cell a box painted takes the still color
again once the box has left. A mesh's emission is not stamped, which would take a copy of
the still light as well, 2 MB a cascade. A skinned mesh is a box for each joint around the vertices
at rest it holds most, posed by the joint's latest matrix, and one that does not bend a box for
each of up to eight parts its triangles are cut into where each cut takes a third of the volume
away (`SceneFieldRenderer.Cut`), or the box around all of it. A mesh past what the room left for a
box each of the meshes beyond it is the box around all of it, so a crowd past 256 boxes is figures
near the eye and boxes beyond. Three dancing robots in `shaders_cornell_box` are 147 boxes in 540
bricks, stamped and painted in 0.06 ms of the RTX 4070 (0.05 unpainted) and placed in 0.45 ms of
the CPU in the Release build, as their meshes' 57 boxes were. A still mesh whose vertices' colors
are written in place (`UpdateMeshBuffer` at 3, `MeshStore.TakeRecolored`) is pooled again with
them, and in each built cascade not built again that frame the paint pass runs over its triangles
alone against the words the cascade's last build kept, a megabyte a cascade, writing the colors
the passes read directly, so a relit section's indirect light takes its new colors in the frame
after without its distances found again; the ray scene writes the colors into the mesh's corners,
which its structure does not read. Where the field kept the colors of a mesh's last upload, a lamp
placed in the voxel game's dark room lit it indirectly from walls as dark as they had been. A mesh
whose material's `BouncesLight` is false is left out of the field as one that casts no shadow is,
and so out of the glow lights and the ray scene the global illumination reference traces, while it
casts its shadows, since the voxel game's walking animals, four to six pieces each, cost the field
16 to 28 microseconds of the CPU and 13 to 15 of the GPU a piece a frame, 17 of them taking the
CPU's share from 0.8 ms to 2.9 and the GPU's from nothing to 1.0. Most of that GPU time was cascades
built again, since a piece that stood for eight frames between walks settled into the field and left
it when it walked on, each time marking the cascades around it. A mesh that moved within the last
1,024 frames, as a place in the order drawn or as one gone whose vertices a mesh new in the frame
has, settles only after standing that long, a still copy of the same mesh after eight, and the
game's animals stand one to eight seconds, up to 960 frames at 120 a second, and 76 figures of five
pieces walking among a land of 400 meshes and standing 45 frames in every 135 had a cascade built
again every frame, 6.2 ms of the GPU's, and have none, 0.16 to 0.19 ms, and standing 240 to 960
frames they had one every fourth frame with 480 frames as the count and one in fifty with 1,024,
those of the figures that stood from the first frame and settled before they walked. A frame in
which a mesh drawn in the same place in the order moved is read by place, as one in which nothing
moved, where it was read whole with every mesh in a dictionary, and the plan's work for those
figures, 2.7 ms of the CPU's in a build that is not optimized, is 1.2 ms, 4.1 microseconds a box,
and 0.68 ms in the Release build, 2.4 a box, the stamp on the GPU 0.6 a box.

Three passes read it. `ao.slang` adds an occlusion read along the normal and four ways leaning from
it at four distances out to the radius, and traces the sun's light toward the sun from a cell and a
half out, darkened by how near a surface the ray passes as a share of how far along it is, its
share in the occlusion image's green channel, which the model pass multiplies the shadowed
directional light by, so the pass runs for the contact shadows alone where the occlusion is off.
`SetContactShadows` leaves the trace out (`ContactShadows.Off`), or has the model pass take the
contact shadow as little as the shadow map holds the point (`ContactShadows.PastShadowMap`, the
lighting buffer's `ambientOcclusion.y` 2), the map's share from `lightReaching`, 1 within its
counted cascades and falling to 0 across the last one's edge, so a shadow the map draws keeps its
hard edge, and the fill past them holds none.
`particle_step.slang` steps a colliding particle's move through the field where the field holds the
place it moves to, and meets the window's depth elsewhere. `field_view.slang` draws one cascade over
the window as the field holds the scene, where `field.show` asks. A render texture drawn through a
camera of its own reads the field as the window does, for its occlusion, the sun's contact shadows
and global illumination, and a probe's faces read only the indirect light through it.

### Global illumination

`SetGlobalIllumination` traces the indirect light between surfaces through the field each frame
as Radiance Cascades (`GlobalIlluminationRenderer`, `GraphicsDevice.GlobalIllumination`), in the
`global_illumination` node after the occlusion and before the probes. Nothing is baked.

A cascade of world probes lies every eight cells of the field's cascade of the same number, eight a
side, so each cascade's probes are twice as far apart as the one before's. Each probe traces an
octahedron of directions, a texel each, across an interval. The first cascade's runs from the probe
to four times the spacing and each after's from its spacing to four times that, so a cascade's
interval begins half way along the one below's (`gi_trace.slang`). `Low` traces 8 by 8 directions in
each of two cascades, `Medium` adds 16 by 16 in a third, and `High` traces 8 by 8 then 16 by 16 in
four, no more cascades than the field has. A ray that meets a surface brings back its painted color
times the sun's light where a trace toward the sun through the field gets through and the point and
spot lights', each that casts shadows only where a trace toward it gets through too
(`hiddenLampLight`), given as the model pass has a light, what a white surface facing it returns,
and its color over pi times the indirect light that reached it the frame before, which the probes
hold as the light reaching a face, blended from the probes around it that it sees, a trace through
the field to each in front of it (`bouncedSeenAt`), with its emission (`shadeProbeHit` in
`gi.slang`). The faces are read where each cascade lay the frame before, its corner then carried in
the lights' buffer, since they were gathered there (`gatheredAt` in `gi_trace.slang`). That indirect
light is taken at a share each probe keeps, its whole but the frame after the probe's own light,
what its rays brought straight from the sun, the lights and emissive surfaces, fell by a fifth
or rose by a quarter, when it is the share its own light kept, channel by channel, at most four
times; the trace sums each workgroup of a probe's rays' own light and all they brought back, and
the gather adds the sums up and sets the share (`gi_ambient.slang`). A probe whose own light is
under a quarter of all its rays bring, lit by indirect light around a corner, is judged by
the own light of the 27 probes of its cascade within one of it summed instead, which 27 of its
workgroup's rays read before the rest trace, and a cascade that moved since the frame before keeps
the whole. So the indirect light from a light that went out goes with it the frame after,
where it lingered over the frames each bounce takes (`gi.toggle follow off`). A ray is traced
from its probe, so a probe a little above a floor does not bring back the light under it for the
cascade below to take, and one that meets a surface before its interval begins brings back that
surface's light for its own probe's faces, marked so the merge of the cascade below reads it as
dark, as that surface lies nearer the probe than the ray below reaches. A ray of the last cascade
that meets nothing brings back the environment map, or the ambient lights' color, and one of any
other cascade lets the light from beyond through. The three closed a room to a lamp over its roof
or under its floor, which lit it before nearly as brightly as the lamp unshadowed, at a cost within
the noise of 0.03 ms in `shaders_cornell_box`.

The cascades are merged from the last down (`gi_merge.slang`). A texel whose ray met nothing adds
the cascade above's texels inside its own, blended between the eight probes of the cascade above
around it by how near each is, a probe inside a mesh passed over, and a probe the field hides from
this one passed over too. The first eight threads of each probe trace those eight lines through the
field into shared memory before the probe's texels read them, so a probe under a ceiling takes
nothing from one above it, which sees the sky. Each probe of every cascade then sums its merged
light into six faces of a cube, irradiance from each axis's two ways (`gi_ambient.slang`), which a
ray's hit reads the frame after for the indirect light that reaches it, and the model pass reads
where no screen probe holds a pixel. Between the eight probes around a point, a probe is weighed by
how near it is and, as DDGI weighs them, how squarely it stands in front of the surface, and one
behind the surface's plane next to nothing, how near taken from a point half the probes' spacing off
the surface along its normal (`Lean` in `gi.slang`), so a wall lying in a plane of probes takes the
light of the row in front of it as well as theirs. A probe nearer a surface than a tenth of its
spacing, or inside a mesh, is moved along the field's normal to stand that far off it, half its
spacing at most, as DDGI moves its probes (`placed` in `gi_trace.slang`), so the probes in a wall's
or a ceiling's plane stand in the room before it; the trace moves each probe before its rays leave,
the merge reads where it moved them, and the gather writes each probe's move into its faces' alpha,
two more than its move along each face's axis in its spacing and 0 for a probe that holds nothing,
which every pass that reads the faces takes its place from. The field holds a solid as the faces
around it, so its cells deeper inside than the faces' band read as the open, and a probe standing
there traces through the rock to the backs of the faces around it. In the voxel game the last
cascade's probes deep in a mountain held the sky they found past the field's side and the grass they
met from below. A ray that stops half a cell or more behind a face came on it from behind, having
stepped from such a cell straight into the cells behind it, and the trace marks its distance below
zero; a probe whose rays met more faces from behind than from in front, or more than a quarter of
them from behind, the share DDGI takes, is passed over as one inside a mesh from the next frame, its
rays traced all the same so it is found again, as DDGI finds its probes inside geometry by the share
of rays that met back faces. The rooms read their references as before to the fourth place, and
global illumination costs 0.02 ms more in `shaders_bounce_rooms`, 1.47 where 1.45. The voxel game
leaves a section's faces against a lamp block out of its mesh and draws the lamp apart as a cube, so
the field reads the stone beside the lamp as the open in front of the lamp's faces, its cells as far
outside as they lie inside, up to 0.64, and a probe whose spot was the corner of a wall, a floor and
a lamp against them was moved 0.30 into the stone, its rays meeting 21 faces from behind of 64 and
42 in front. At night it held the lamp's light 340 to 2450% over the reference across its faces, and
the grass outside the wall took it, 5.8 times the reference's light on the grass by the wall's foot
with the screen's probes off, red over green 1.21 where 0.36, and with them on red over green 0.71
at the wall's foot where 0.57. Walled in by the quarter, it holds nothing, and the grass reads 1.42
times the reference as the grass beside it does, 1.38, red over green 0.37, and 0.56 at the wall's
foot. Of the four cascades' probes the quarter walls in three the majority did not, this one and two
inside the ground under the grass, 19 of 64 from behind and 20 in front, and none in
`shaders_bounce_rooms`, whose views read as before to the last place at 160 by 90
(`GlobalIlluminationTests`).
A ray of the last cascade that meets nothing brings the sky's light marked a
quarter in its alpha, which the merge reads as it reads a ray that met a surface, and each merged
texel carries the share of it that reached the sky in an image of its own, 1 for a ray of the last
cascade that met nothing and the cascade above's blended where a ray met nothing, which the gather
weighs as it weighs the light into a sixth image of faces, each face's share of the sky. The model
pass weighs the environment map's reflection by it where global illumination is on, read along the
mirror direction from the eight probes around the surface as the light is (`skyAt` in `gi.slang`),
since the occlusion alone weighed it before and nothing said the sky was not there. A box sealed on
every side under a blue sky read 37.5 in blue on its rough inside walls and 0.13 after, an open
floor 62.7 and 61.8 (`GlobalIlluminationTests`). It took the voxel game's scene pass at eight
columns from 1.55 to 1.65 ms of the GPU and its global illumination from 1.26 to 1.32 in a Release
build, and the Cornell box's from 0.69 to 0.70 ms at `High` with 0.84 MB more, by
`./e3d command profile` and `gi.state`.

The first interval is traced again on the screen (`gi_screen.slang`). A probe stands on the surface
at the middle of each tile of 16, 12 or 8 pixels by the quality, read from the half-size depth the
occlusion pass draws, which it draws for the probes alone where the occlusion and contact shadows
are off. It sends 16 rays over the hemisphere around the surface's normal, one through each texel
of the hemisphere's square of four by four, each tile's from one of sixteen places in its texels,
every four tiles by four holding each place once (below), twice as far as the
world's probes are apart where a ray ends in the finest cascade and as far as they are apart past
it, stepped through the depth while on the screen and through the field from where they leave it.
Reaching as far as the probes are apart, a wall two units before a pillar beside a lamp took the
light past that from the probes standing at the pillar's dark face, and its indirect light read 0.68
of a path-traced reference's where 1.05 without the pillar, and reaching twice as far 0.79 and 1.01,
a gap of 22 points where 37; the 27 cases of `shaders_bounce_rooms` sum 3.778 where 4.006, the
corridor 3 to 9% under where 17%, the strip's room at 800 by 450 4% over where 10%, and the carried
lamp's room 3 points further under, 6% at 800 by 450, the world's own rays there meeting the red
wall its lamp lights 8 to 16% under the reference too. Reaching twice as far everywhere, a closed
room near the field's side behind the eye took 0.0037 of the sky's light where 0.0007, which the
coarser cascades and the probes beside that side let in, and 0.0015 bounded so; and a ray that went
behind the depth and then off the picture was handed to the field where it went off, past the wall
it had gone behind, which lit a closed room with the lamp outside it 92 levels where 8 at most, so
such a ray is handed to the field from the last point it stood in front of the depth (`traceScreen`
in `gi.slang`). A surface the march meets is taken only where its face, as the depth around the hit
gives it, turns toward the ray, and a face met from behind is passed as the depth is. Taken as met,
a ray of a probe on the carried lamp's room's far wall past the green wall's end, the lamp carried
beyond the wall and the wall moved, met the face the eye sees, lit, where the face toward the probe
is dark, 1375% over the reference along it (`gi.screenprobe`), and its green lay over the far wall
and the floor past the wall's edge, green over red 0.97 and 1.44 there where the reference reads
0.74 and 0.99. Passed, the field reads the ray 57% under, and green over red reads 0.83 and 1.08.
The rooms' views at 800 by 450 move by 0.0015 of their light at most, none further by more than
0.0001, and that room so lit by 0.0002 further, its floor past the edge 14% under where 1%, the
green having covered the shortfall of the light its probes read beyond their rays' reach. At 160 by
90 the grazing floor's view at `High` widens by 0.003, the probes on its ceiling having met their
own ceiling at no distance, lit, 55 to 460% over the reference along those rays, which covered their
sixteen fixed ways' reading under the light over their hemisphere, 6 and 7% under where 9 and 22%
over; the corridor's at `Low` by 0.006, its floor's tiles 85% under where 47%, as `High` reads them,
87%; and the Cornell box's at `Low` by 0.0006, spread over the box, no tile moving by more than
0.0001 of its light. The reflections in shaders_reflections read as before at `High` and `Low`,
their floor and balls within 1%. It costs the illumination pass 0.01 to 0.03 ms in the carried
lamp's room and 0.03 at `Low` to 0.09 at `High` in shaders_reflections, and the reflections 0.01
(`./e3d command profile`, the frames unlimited).
The pass costs 0.05 to 0.07 ms more in the rooms' views at `High`, 0.06 to 0.14 in
the corridor's and 0.08 to 0.15 in the voxel game's room of glowstone, the least of eight readings
each beside another program's use of the GPU. Three and four times as far read the wall past the
pillar up to 1.75 of the reference, and a weight in the probes' blend for how much of the surface's
half of the sphere each shares, by its reach, moved the wall 0.01 to 0.04. A ray that meets nothing
in its reach takes the world's first cascade, or past it the second, blended between the eight
probes around the surface that a trace from a cell in front of it reaches, each probe's light along
the ray blended between the four texels of its octahedron around the ray's way, across the square's
edges as the octahedron folds (`folded` in `gi_screen.slang`). A probe's rays leave from one of
sixteen places in their texels, every four tiles by four holding each place once and every probe
moving on to the next place each frame, and the first of two passes (`gi_screen_filter.slang`)
keeps each probe's light over up to sixteen frames where its surface was, this frame's rays one
frame more in the mean, so a probe that keeps them has sent every ray of a texel's sixteen. The
second blends each probe's kept light with the 5 by 5 around it on a surface alike in normal, each
point read from the depth and weighed by how near it lies to the plane of the probe's surface. The
probe's point is read from the depth, found in the frame before through that frame's camera, which
the view carries, and the four probes then around it blended where each stood on a like surface,
their kept light and how many frames it holds, kept in two images the frame's kept light and
surfaces are copied into after the filter. Each of the four is weighed by where it
stands, the middle of the half-sized depth's texel under its tile's middle, a pixel past that
middle; weighed by the tiles' middles, a still view's blend took a pixel's share of the probes to
its right and below every frame, a sixteenth of a tile at `Low` and an eighth at `High`, which held
the thin room and the Cornell box 0.2 to 0.5 points of their light nearer their path-traced
references and the window's room and the carried lamp 0.3 to 0.6 further (`BounceRoomsTests`). The
filter's weights spread wider took the thin room further from its reference still, and narrower
nearer, so the drift had brought it there rather than the filter's width. A probe whose surface the
frame before did not show, at the edge of the picture or behind what moved, takes this frame's light
alone. The kept light is held first within twice the spread of this frame's light among the like
probes around, each channel's standard deviation by the filter's own weights, so where a light
changed the history is pulled to it, and where it holds still the history lies inside and
keeps its calm. A block's side lit only by a wall's indirect light comes within a tenth of its new
light in the frame a lamp is brought in, where it took seven frames without the hold. With the
camera sliding a hundredth of a unit a frame through a Cornell box, global illumination adds −0.03
levels a frame to the picture's change and 0.03 without the history, at `Low` on the RTX 4070, where
the slide alone changes 0.48, and on lavapipe −0.03 and 0.02 (`GlobalIlluminationTests`), the hold
costing nothing that can be read in Wick's first doorway. The model pass blends the four probes
around a pixel the same way, falls back to the world's probes where none is like it, those it sees
through the field (`bouncedSeen`, as the thin room's corner reads them), and puts the result in
place of the diffuse light from all around, the environment map's, the ambient lights' and the
reflection probes', which reaches a surface only through the rays that meet nothing.

A glossy surface traces its reflection in the model pass (`tracedReflection` in
`lights.slang`), where it has its own normal, its normal map's included, and its roughness, so
only a fragment under a roughness of 0.5 traces and a scene of rough surfaces pays nothing. The
mirror ray is stepped through the window's half-size depth of this frame, 12, 16 or 24 steps by the
quality, spaced more finely near the surface, and the step it meets a surface in is halved five
times (`traceScreen` in `gi.slang`, which the screen probes trace through too). The surface it meets
is looked up in the frame before's picture through the camera of the frame before, its level of
blur by the roughness, and blended toward the field's shading of the same point at the picture's
edge, and where the frame before's depth there held a point away from the hit, something in front
of the surface then, the field's shading is taken in its place. A ray that leaves the picture or meets nothing on it is traced on through the field, and the
surface it meets there is shaded as a probe's ray shades one. Where it meets nothing, the probe's or
the environment's reflection stands, as it does for a surface as it grows rough, the traced
reflection fading from a roughness of 0.25 to 0.5, and the ambient lights' specular fades with it.
While global illumination is on, the window's scene decoded to linear light (§5) is copied after the
model pass at half size into an image with its mips, and the window's half-size depth beside it
(`GraphicsDevice.RecordKeepFrame`), which the next frame's reflections read.

Where the device traces rays (`VK_KHR_ray_query` with its acceleration structures, turned on at
start where the driver has them, `GraphicsDevice.CanQueryRays`), the model pass is built a second
time with `RAY_QUERY` defined (`SlangLoader`'s variants, which `e3d shaders` compiles into a
program's cache too), and its lights' set holds the window's meshes as the GPU's rays see them
(`GraphicsDevice.RayQuery`). Each mesh the field gathered, the skinned left out, gets a
bottom-level structure from its triangles the first frame it is drawn, its corners kept in one
buffer, and at `High` the top-level structure of every copy is built again each frame with each
copy's color and emission, written into one of a ring of buffers a frame in flight. A
reflection whose ray the field misses traces it there, and the triangle it meets, its face from the
corners turned toward the ray, is lit by the lamps, each that casts shadows through a ray toward
it, the sun through a second ray, and the indirect light or the sky (`rayReflection` in
`lights.slang`). A reflection the field meets is lit as a probe's ray is, the lamps that cast
shadows hidden where the field stands between (`shadeHit`, whose second loop over the lamps after
`directLight`'s is the shape lavapipe draws, where the lamps' loop ahead of the sun's in
`directLight` is not). A device that draws on its CPU leaves ray
queries off, since lavapipe of Mesa 25.2 crashed in the model pass's fragment stage at its first ray
query, any-hit alone included, where the structures it built without a word from the validation
layer traced on a GPU. `gi.rays` turns the path off and on in a running program.

Global illumination is measured against a reference the engine traces itself through the same
structures (`BounceReference`, `gi_reference.slang`), each copy's surface record holding its
material's roughness and metallic for it. A hit takes its mesh's vertices' colors as well, blended
as the model pass blends them, carried in the w of each corner the structures' build leaves unread,
so a mesh of many colors, as a voxel game's section of white concrete, is traced as drawn, where
traced in its copy's color alone the game's rooms read a third brighter in the reference than in the
frame. `gi.reference <png> <samples>` traces that many paths a pixel from the window's view, a few a
submission, each waited for. It lights the first face as the model pass lights one, specular and the
share its face reflects included, and every face after as global illumination lights one, with the
sun and the lamps that cast shadows reaching a face where a ray toward them gets through, the
emission at every face, the way on drawn by the cosine, Russian roulette from the third bounce, and
the environment map or the ambient lights where a path meets nothing. It writes a PNG of the light,
the light itself as a PFM, what each pixel's first ray met, the copy and the axis its face turns
toward, and the names of those regions, a flat slab's top a floor and its underside a ceiling, a
standing slab a wall by its color and the way it faces, and the rest blocks by their order.
`gi.compare <png>` reads the window's decoded light back and gives each region's mean light in each
channel against the reference's, the difference and its share of the reference, and writes a picture
of the difference, red where the frame is brighter and blue where it is darker.
`BounceReferenceTests` holds the reference to a closed box whose walls are all equally emissive,
which it shows as their emission over one less the walls' color, 2.01 within 2%, and to a lone slab
under a lamp, which the frame and the reference read alike to a hundredth of a percent. The Cornell
box at 800 by 450 takes 1.0 s on the RTX 4070 for 1,024 paths a pixel. `shaders_bounce_rooms` holds
the cases global illumination finds hardest, a room each forty units apart with a camera fixed on
it. They are the Cornell box, a room of walls thinner than the field's cell with a lamp outside it,
a corridor lit from its open end, the sun through a window, white blocks beside red walls one, three
and six units off, a small bright strip in a dark room, a floor seen at a grazing angle, and a room
whose lamp a key carries and whose wall another moves. `build/bounce-rooms.sh` captures each view,
traces its reference at `High` and compares each quality's frame with it.

What the light holds is drawn to be looked at (`BounceViewRenderer`), as `gi.show` and
`DrawBounceWindow` choose it. `gi_view.slang` draws over the window after its shapes and before
ImGui, in the `bounce_view` node after `scene_field_view`. It draws the screen's probes as tiles
over the picture, their light as their rays brought it, filtered, or the share the frame before's
gave, which the filter writes into the blended light's alpha as one more than it; a cascade's rays
or merge, read through a sampled image in the general layout their compute work leaves them in
(`GraphicsDevice.BindProbeVolume`), the recording then adding a barrier from those writes to the
fragment stage and one from that stage to the next frame's rays; and the decoded frame against a
reference uploaded as half floats. `gi_probes.slang` draws a cascade's probes as cubes, an instance
a probe, in the HDR scene's pass after its meshes, so the scene hides them as it hides a mesh.
Through the immediate pass they would be a batch with depth after the program's own, which would
move the line between the scene and the interface drawn over it. `gi.toggle` leaves out the frame
before's light or the neighbors' in the screen's filter (flags in `ScreenView.grid.w`), the screen's
probes (the lighting buffer's `Screen.y`), the merge, or every cascade but one, the cascades above
it giving nothing and those below passing its light on where their rays meet nothing (the push of
`gi_merge.slang`). `BounceViewTests` draws each view at `Low` and leaves each part out.

**Global illumination, measured before its fixes.** Each room of `shaders_bounce_rooms` at 800 by
450 on the RTX 4070, as `build/bounce-rooms.sh <folder> 1024 "Low Medium High" "1 2 all"` measures
it, each quality's frame over every region against a reference of every bounce, and High's against
references of one bounce and of two (`gi.reference <png> <samples> <bounces>`).

| Room | `Low` | `Medium` | `High` | `High` against one bounce | against two |
|---|---|---|---|---|---|
| The Cornell box | −30% | −30% | −33% | −17% | −27% |
| Thin walls, a lamp outside | −54% | −54% | −58% | −15% | −35% |
| A corridor lit from its end | −22% | −21% | −21% | −3% | −8% |
| The sun through a window | −75% | −75% | −73% | −50% | −63% |
| Red walls beside white blocks, outdoors | −1% | −1% | −1% | −1% | −1% |
| A small bright strip | −87% | −87% | −87% | −67% | −77% |
| A floor at a grazing angle | −67% | −67% | −66% | −27% | −45% |
| A lamp carried, a wall moved | −60% | −60% | −60% | −31% | −45% |

The quality moves the error by a few points, and the error grows with the share of the light past
the first bounce, so rays are not what is short. `gi.probe <x> <y> <z> <samples> <bounces>` places
the loss. It reads the probe of the first cascade nearest a point, its own rays' light, its light
merged with the cascades above and its six faces, and traces the light arriving at its middle from
each of its directions as a reference. A probe in each room's middle at High, with the frame's light
given one bounce (`gi.toggle again off`) against its hits lit directly (`gi.probe x y z 256 0`),
and with every bounce against every bounce, as the hit shading is and with the lamps' and the sun's
light at a hit times pi.

| Room | Its rays' hits, once | times pi | Merged, every bounce | times pi |
|---|---|---|---|---|
| The Cornell box | −49% | −2% | −60% | −21% |
| Thin walls, a lamp outside | −48% | +62% | −76% | −26% |
| A corridor lit from its end | no light within reach | | −100% | −100% |
| The sun through a window | no light within reach | | −90% | −70% |
| Red walls beside white blocks | −67% | +5% | −74% | −19% |
| A small bright strip | no light within reach | | −100% | −100% |
| A floor at a grazing angle | −72% | −11% | −81% | −40% |
| A lamp carried, a wall moved | −75% | −22% | −88% | −61% |

So the loss lay in the trace first. `shadeHit` and `shadeProbeHit` in `gi.slang` lit a hit as
`color / Pi * arrived`, where `directLight` gives the lamps' and the sun's light as the model pass
has a light, the light a white surface facing it returns, so a lit surface sent on a pi-th of its
light and only the indirect light reaching it, which the probes hold as irradiance, was right over
pi. The reflections' hits through the GPU's rays in `lights.slang` lit the same way. With the lamps'
light times pi the probes' hits come within 2 to 22% of the reference, the thin room's apart, where
they were 48 to 75% under, and each room's frame at High against every bounce gains from 4 to 31
points, to −22% in the Cornell box, −27% with thin walls, −17% in the corridor, −57% through the
window, −57% at the grazing floor and −51% with the lamp carried, the strip and the outdoor blocks
unchanged. The merge and the gather lose little beside it. Each face the model pass reads equals the
one gathered from the merge to the third digit, and the merge trails the rays' own hits by 1 to 7
points. The gather took each texel of a probe's octahedron as an equal share of the sphere, which
read a uniform sky's ±z faces at 0.82 of its light at 4 texels a side, `Low`'s and `Medium`'s first
cascade, and 0.93 at 8, and its ±x and ±y faces at 1.04 and 0.98. What remains after pi is the light
past the first bounce, read at a hit from the frame before's probes. In the Cornell box, with pi,
the probe's merged light is 0.973 with one bounce against the reference's 1.010, and 1.111 with
every bounce against 1.431, so it holds a third of the 0.421 past the first bounce. The thin room's
walls, under the field's cell, let the outside lamp's light through, which pi makes 62% too much at
the probe and 82% at `Low`'s frame against one bounce. The corridor's and the window's probes hold
almost nothing of the sunlit floor's light, a limit of their own to be read, and the strip, 0.06
thick under cells of 0.15, is in no cascade of the field, so its room stays 87% under whatever the
trace does.

The four artifacts the Cornell box shows, each picture the frame at High above the reference with
every bounce, both linear light drawn through the same curve, measured from the frame's light that
`gi.compare` writes beside its difference (`-frame.pfm`):

- **Bands across the floor.** Each row of the floor left of the tall block lies −12.6% to +15.5%
  about a smooth fit at High, where the reference's rows lie within ±2.3% and `Low`'s and
  `Medium`'s within ±4.7%. The screen's probes draw them. With them off the rows lie within ±6.5%,
  with their filter off within ±34% and with their history off within ±28%. The filter weighs a
  neighbor by how near its distance from the eye is, within 2%, and on a floor seen at a slant the
  probes in rows 8 pixels apart lie farther apart than that, so it blends nothing along the slope
  and each row keeps its own noise.
  ![The Cornell box's floor, the frame at High above the reference](assets/bounce/cornell-floor-bands.webp)
- **The floor at the red wall's foot.** The floor beside the wall reads 0.94 of the floor 40 pixels
  on in the frame at High and 0.89 in the reference, so the corner darkens by 6% where it should by
  11%, and it lacks the red the reference spreads onto it.
  ![The floor at the red wall's foot](assets/bounce/cornell-red-wall-foot.webp)
- **The halo under the emissive panel.** In linear light the ceiling falls away from the panel
  faster in the frame than in the reference, to 0.28 of its light at 60 pixels from the middle
  where the reference keeps 0.33, and to 0.15 at 120 against 0.21. The halo reads broad because the
  rest of the ceiling is 31% short of its light.
  ![The ceiling around the emissive panel](assets/bounce/cornell-ceiling.webp)
- **The small block's side facing the green wall.** It is 82% under the reference at High, 76% at
  `Low`, with its green at 0.041 against 0.242, yet its green over its red is 2.6 where the
  reference's is 3.2, so the tint is there and the light a fifth of it, the trace's loss above.
  ![The small block's side facing the green wall](assets/bounce/cornell-small-block.webp)

A fifth shows at the grazing floor's ceiling, a step where the screen's probes end, at the edge of
the field's first cascade, past which the model pass reads the world's probes, which `gi.toggle
screen off` takes away with the probes (above, the frame; below, the frame with them off).
![The grazing room's ceiling with and without the screen's probes](assets/bounce/grazing-screen-step.webp)

On the Cornell box's view global illumination costs 0.36 ms of the GPU at `Low`, 0.44 at `Medium`
and 0.53 at `High`, as `./e3d command profile` names it `global_illumination`, with the frame rate
unlimited (`./e3d eval "SetTargetFPS(0)"`). The fixes are taken in the order of the gain measured,
the lamps' light at a hit times pi, here and in the reflections, with the thin room's leak it
raises, then the light past the first bounce, the screen's filter along a slanted surface, the
gather's shares of the sphere, the step where the screen's probes end, and the sun's light into the
corridor and the window's room.

**The fixes, each measured.** The first takes the lamps' and the sun's light at a hit times pi
beside the indirect light, in `shadeHit`, `shadeProbeHit` and the reflections' hits, so a lit
surface sends on the whole of its light. Every room's frame over every region against every bounce,
by `build/bounce-rooms.sh`, each cell the error before the fix → after it, as in the tables after.

| Room | `Low` | `Medium` | `High` | `High` against one bounce |
|---|---|---|---|---|
| The Cornell box | −30% → −16% | −30% → −15% | −33% → −22% | −17% → −4% |
| Thin walls, a lamp outside | −54% → −12% | −54% → −13% | −58% → −27% | −15% → +51% |
| A corridor lit from its end | −22% → −18% | −21% → −17% | −21% → −17% | −3% → +3% |
| The sun through a window | −75% → −64% | −75% → −64% | −73% → −57% | −50% → −23% |
| Red walls beside white blocks | −1% | −1% | −1% | −1% |
| A small bright strip | −87% | −87% | −87% | −67% |
| A floor at a grazing angle | −67% → −62% | −67% → −61% | −66% → −57% | −27% → −9% |
| A lamp carried, a wall moved | −60% → −52% | −60% → −52% | −60% → −51% | −31% → −15% |

Global illumination costs what it did, 0.36, 0.44 and 0.53 ms by quality on the Cornell box's view.
The thin room's frame passing the one-bounce reference by 51% is its walls' leak, now a share of the
whole light, and a closed room with a lamp under its floor holds 14.6 levels where it held under 8,
both the probes' visibility's to mend.

The second weighs each direction a probe gathers by its texel's share of the sphere, one over the
cube of its octahedron point's distance from the middle, beside its cosine, with the weights scaled
to sum to pi (`octahedronShare` in `gi.slang`, `gi_ambient.slang`), and the screen's probes' rays
the same over their hemisphere, where an equal share read a uniform sky at 1.04 of its light. Every
face of a probe under a uniform sky reads pi to 2% at `Low` and `High` (`GlobalIlluminationTests`).
Before and after, over every region against every bounce and, in the last column, `Low` against one
bounce.

| Room | `Low` | `Medium` | `High` | `Low` against one bounce |
|---|---|---|---|---|
| The Cornell box | −16% → −21% | −15% → −20% | −22% | +4% → −2% |
| Thin walls, a lamp outside | −12% → −8% | −13% → −9% | −27% → −23% | +82% → +89% |
| A corridor lit from its end | −18% → −15% | −17% → −15% | −17% → −15% | +1% → +4% |
| The sun through a window | −64% → −63% | −64% → −62% | −57% → −56% | −35% → −33% |
| A floor at a grazing angle | −62% | −61% | −57% | −18% → −19% |
| A lamp carried, a wall moved | −52% → −50% | −52% → −50% | −51% → −49% | −18% → −14% |

The Cornell box against one bounce comes within 3% at every quality, −2, −1 and −3%, where the
faces along x and y read 4% over and hid part of what the light past the first bounce lacks, which
shows as 5 points more under every bounce at `Low` and `Medium`. The outdoor blocks and the strip
are unchanged, the closed room with a lamp under its floor holds 17.3 levels where it held 14.6, and
global illumination costs what it did, 0.36, 0.44 and 0.53 ms.

The third finds the light past the first bounce short in a cascade's own faces. A ray of a cascade
past the first that met a surface before its interval began was blocked dark, so those cascades'
faces lacked every surface nearer their probes than the interval's start. With one bounce, a
probe of the second cascade in the Cornell box read 63% under a reference of the light arriving
at it (`gi.probe 0 2.5 0 512 0 1`). Most of that box lies past the first cascade's reach from the
view's camera, so its surfaces, and the hits that read them for the indirect light reaching a
surface, took those faces. Such a ray now brings back the surface's light for its own probe's faces,
alpha a half, and the probe reads 5% under. The merge into the cascade below still reads it as dark,
since the surface lies nearer that probe than the ray below reaches. Taken as light, it read the
ceiling beside the lamp for one two units off, up to 26 times too bright along those ways, and
blocked dark in the merge the first cascade stays where it was, 2% under with one bounce. Before and
after, over every region against every bounce.

| Room | `Low` | `Medium` | `High` | `High` against one bounce |
|---|---|---|---|---|
| The Cornell box | −21% → +2% | −20% → +2% | −22% → −10% | −3% → +11% |
| A corridor lit from its end | −15% → −9% | −15% → −9% | −15% → −8% | +5% → +13% |
| A floor at a grazing angle | −62% → −53% | −61% → −50% | −57% → −46% | −8% → +15% |

The thin room, the window's room, the carried lamp, the outdoor blocks and the strip read within a
point of what they did, and the probe in the Cornell box's middle reads 15% under with every bounce
where it read 20%. Shading those rays costs 0.006 to 0.008 ms, for 0.36, 0.45 and 0.54 ms by
quality. The Cornell box's reference frame is drawn again, its back wall and blocks brighter.

The fourth weighs the screen's probes by the plane of the surface rather than by their distance
from the eye. The filter reads each neighbor's point from the depth under its tile's middle and
weighs it by how far that point lies from the plane of the probe's surface, and the model pass
carries the pixel's own plane to each of the four probes' tile middles by how its point moves from
pixel to pixel (`ddx` and `ddy` of its position) and weighs each by how near its distance from the
eye is to that plane's there. Weighed by distance alone, rows of probes 8 pixels apart on a floor
seen at a slant lay further apart than the 2% allowed, so the filter blended no row with the next,
and beside an emissive wall at the floor's far end the model pass found no probe near enough, took
the world's probes, and the wall's light ended in a line, falling by half within four rows where it
falls by 18% at most (`GlobalIlluminationTests`). The rows of the Cornell box's floor lie this far
about a smooth fit.

| Quality | Before | After | The reference |
|---|---|---|---|
| `Low` | −4.1% to +4.8% | −1.1% to +2.3% | ±2.3% |
| `Medium` | −3.0% to +4.3% | −1.3% to +1.6% | ±2.3% |
| `High` | −12.9% to +15.3% | −4.0% to +5.9% | ±2.3% |

What is left at `High` is the world's probes, whose blend between probes 1.2 units apart shows on
the floor by itself at ±8% with the screen's probes off, the history changing it little, −3.3% to
+5.2% with it off. The
error over every region moves a point at most in any room, and the cost is within the noise,
global illumination 0.37, 0.45 and 0.54 ms by quality and the scene's pass 0.16 ms.

The fifth takes on the leaks. Each probe keeps how far its rays went along eight by eight directions
before a surface stopped them, the trace writing each ray's distance and the gather laying them out
as the probes' reach, and `bouncedAt` weighs a probe by whether a surface, taken three tenths of the
probes' spacing off along its normal, lies within that reach, its weight falling to a twentieth over
an eighth of the spacing past it, a cell of the probes' cascade (`probeSees` in `gi.slang`), as DDGI
weighs its probes by the distances they traced. Cut to nothing over a quarter of the spacing, the
weight left a pixel with one or two of its probes seen to their light alone, which drew a notch
beside the Cornell box's tall block and a smear by its green wall. A closed room with a lamp under
its floor took 15.3 levels of the lamp's light, lent by the probes beneath the floor to the walls by
it through the light past the first bounce, which `gi.toggle again off` took to nothing. It takes
1.6, and its test's bound is 8 again, the lamp over its roof giving 6.1. The thin room's leak was
the field's. Its walls, 0.1 thick in cells of 0.15, lay between two cells' middles, each 0.025
outside the wall, so a march toward the lamp outside stepped over them and the lamp lit the floor
inside, which the room's probe read on its face toward the floor at 255% over a reference of hits
lit directly. The field holds a wall thinner than a cell, where it held one thinner than half a
cell, as a sheet half a cell thick about its middle, and that probe's hits read 6% over where they
read 63%. Before and after, over every region against every bounce.

| Room | `Low` | `Medium` | `High` | `High` against one bounce |
|---|---|---|---|---|
| The Cornell box | +1% → +7% | +2% → +8% | −10% → −6% | +11% → +17% |
| Thin walls, a lamp outside | −8% → −32% | −9% → −32% | −23% → −43% | +58% → +18% |
| A corridor lit from its end | −11% → −19% | −10% → −18% | −9% → −16% | +12% → +3% |

The thin room reads under every bounce as the other rooms do, its leak having covered the
shortfall, and the Cornell box passing its reference by 7 and 8% at `Low` and `Medium` and the
corridor falling 8 points are the light the probes weighed out had lent them; the window's room,
the grazing floor, the carried lamp, the outdoor blocks and the strip move a point at most. The
Cornell box's small block reads its side facing the green wall 26% under at `Low` and 44% at `High`,
where it read 76 and 82% under before the first fix. The floor at the red wall's foot still darkens
by 3 to 6% where the reference darkens by 11%. The merge's bilinear fix, which the corner was to
have, is not tried. It needs each parent's light from its interval's start kept apart from the early
hit the third fix keeps for the parent's faces, and a march from this probe's interval end to each
of eight parents' interval starts for every direction of every merge. The reach costs some 0.05 ms
at each quality, global illumination 0.42, 0.51 and 0.59 ms, the scene's pass 0.012 ms more at
0.173, and 0.75 MB at `High`, where the world's probes take 5.32 MB.

The sixth was to read the sun's visibility at a hit from the shadow cascades rather than trace it
through the field's cells, for the sunlit floors the window's and the corridor's rooms hold little
of. Measured first, the hits were right, and the probes beside the window room's patch of sun read
their rays' light 0% and 20% from a reference of hits lit directly. The loss was the merge's. The
probe in the room's middle read its face toward the floor 78% short with one bounce, the patch lying
past its rays' reach, and every probe of the cascade above that could see it met the floor before
its own interval began, which the merge reads as dark (the third fix). Each cascade's rays reach
four times its probes' spacing, where they reached twice, as far again as the next cascade's
interval begins, so a surface its probes meet early this probe's ray meets itself; the middle probe
reads 15% short where it read 50%. Over every region against every bounce.

| Room | `Low` | `Medium` | `High` | `High` against one bounce |
|---|---|---|---|---|
| The Cornell box | +7% → +17% | +8% → +17% | −6% → +5% | +17% → +30% |
| Thin walls, a lamp outside | −32% → −10% | −32% → −9% | −43% → −14% | +18% → +76% |
| A corridor lit from its end | −19% → −18% | −18% → −17% | −16% → −16% | +3% → +4% |
| The sun through a window | −62% → −50% | −62% → −50% | −56% → −50% | −20% → −10% |
| A floor at a grazing angle | −53% → −35% | −50% → −33% | −45% → −27% | +17% → +56% |
| A lamp carried, a wall moved | −49% → −27% | −49% → −27% | −48% → −32% | −10% → +18% |

The rooms' errors summed fall from 310 points to 245 at `Low` and from 302 to 232 at `High`. The
Cornell box passes its reference by 17% at `Low` and `Medium`, where the probe in its middle, read
at `High`, holds within 5% of hits lit directly, so the coarser qualities' overshoot is left to be
read with their tiers. A room's light fades over more frames once what lights it goes out, an
emissive panel's 22.5 levels left 18 frames on and none 48 on (`GlobalIlluminationTests`). The
closed room with a lamp under its floor holds 1.6 levels as it did, and the sun's visibility from
the shadow cascades is not tried, the hits reading the sun right. The longer rays cost 0.003 ms at
`Low`, 0.05 at `Medium` and 0.07 at `High`, global illumination 0.42, 0.55 and 0.66 ms. The Cornell
box's, Wick's and Manor's library's reference frames are drawn again.

The seventh was for the halo under the Cornell box's panel, its ceiling 31% short away from the
panel in B. The fixes before it took it away, and the ceiling reads 8% over at `Low` and 3% at
`High`, falling from the panel to 0.36 of the middle's light 60 pixels off at `High` where the
reference keeps 0.33. Of the two things the seventh was to measure, the first interval's length is
the sixth fix's, and `High` traces 64 directions in its first cascade already. `Low` and `Medium`
traced 16, and their Cornell box passed its reference by 17%, since a ray of 16 over the bright
patch beside the lamp stood for a sixteenth of the sphere. They trace 64 in their first cascade, as
`High` does. Over every region against every bounce, `Low` and `Medium`.

| Room | `Low` | `Medium` |
|---|---|---|
| The Cornell box | +17% → +6% | +17% → +8% |
| Thin walls, a lamp outside | −10% → −15% | −9% → −14% |
| A corridor lit from its end | −18% → −18% | −17% → −17% |
| The sun through a window | −50% → −53% | −50% → −53% |
| A floor at a grazing angle | −35% → −35% | −33% → −34% |
| A lamp carried, a wall moved | −27% → −32% | −27% → −32% |

The rooms that fall a few points were read brighter by the same coarse rays, a lit patch standing for
a sixteenth of the sphere, and the summed error holds, 245 → 247 at `Low`. Global illumination costs
0.003 ms more at `Low` and 0.007 at `Medium`, 0.42 and 0.56 ms. As `GlobalIlluminationTests` reads
the look on the RTX 4070 at `Low`, with the camera sliding global illumination adds 0.95 levels a
frame to the picture's change, where it added 1.23 after the sixth, and 3.48 without the history,
and an emissive panel's room
falls from 211 levels to 24.3 18 frames after the panel goes dark, 5.3 at 24 and 0.9 at 30.

The eighth takes away the step where the screen's probes ended, at the edge of the field's first
cascade, past which a surface took the world's probes alone and a slanted ceiling drew the edge in
stairs. A screen probe past the first cascade takes the light from beyond its rays from the second
cascade's merge, so the screen's probes reach as far as the second cascade does, a probe of the
second whose ray met a surface early lending that surface's light here, as the way along it from
that probe. On a floor fifty units long seen from one end at `Low`, 161 of the 182 probes on it hold
light where 106 did, the rest past both cascades (`GlobalIlluminationTests`). On the grazing room's
ceiling at `High` the largest jumps from one row of pixels to the next, the thousandth part of them
that jump most, fall from 93.8% of the light to 41.8%. The error over every region moves little, in
these rooms and by a point at most in the others.

| Room | `Low` | `Medium` | `High` |
|---|---|---|---|
| The Cornell box | +6% → +6% | +8% → +5% | +5% → +6% |
| A corridor lit from its end | −18% → −21% | −17% → −21% | −16% → −21% |
| A floor at a grazing angle | −35% → −37% | −34% → −36% | −27% → −28% |

The probes past the first cascade trace their rays where they held nothing, which costs 0.028 ms at
`Low`, 0.013 at `Medium` and 0.017 at `High`, global illumination 0.45, 0.57 and 0.66 ms. In the
look, global illumination adds 1.00 levels a frame to the sliding picture's change where it added
0.95, and 3.68 without the history, and the panel's room fades as it did, to 24.3
levels 18 frames on and 0.9 at 30.

Each cascade's light fades into the next across its outer band, eight of its cells, a probes'
spacing (`CascadeBand`), the screen's probes' into the world's across the second cascade's, and the
last cascade's into the light from beyond the probes, a sky over a ground (below), which takes the
place of global illumination past it (`bouncedHold`); a surface's depth in
a cascade is that of its point leaned off it as the probes are read for it, so a floor on a
cascade's lower side, its probes half a spacing over it, lies half a spacing inside. The passes that
read the field to its edge, the occlusion from it, the sun's contact shadows and the glow lights'
marches, fade across the outer band of the field's coarsest built cascade the same way
(`fieldHold`). Every band is read from the coarsest cascade built, and on a side the eye stands near
it ends six cells short of the eye or is none, the field's info carrying the eye (`bandHold`). A
cascade lies 0.35 of its width ahead of the eye, so the eye stands 1.6 to 9.6 cells from its side
behind it, and across that side the last cascade's band gave a closed room's walls the sky's light
whole wherever it reached them, by the heading as the cascades turned with the eye, 0.11 to 0.14 of
the open floor's light at six headings of twelve, where with the band kept clear of the eye they
read under 0.0004 at every one; read from the last cascade while it was not built, a closed room's
floor under an ambient light took 9.4 times the light it settles at in the frames before. The last
cascade's hold is read at the surface itself where that holds more than its leaned point, since a
surface facing the eye leans four cells toward the side behind it, which may stand 1.6 cells behind
the eye, and a pillar's face a block before the eye leaned past that side, held nothing and took the
sky's light whole, in the voxel game's cave at three headings of a turn and in `CascadeEdgeTests`
0.157 of the open floor's light where 0.0006. A reflection's hit lies where the eye does not look,
so a glossy floor can mirror a wall past the last cascade's side behind the eye, which takes the
sky's light unhidden. A probe's ray that leaves the field through that side takes the sky as well,
so a cave reaching past it lets a little in. One of 30 by 30 blocks under a hill reads 1.7 levels of
255 at `High` with the game's own shading off and 0.01 with the sky left out of the probes' rays;
and at `Low`, whose last cascade's rays reached 16 units, a cave wider than that took the sky past
them at every heading, that one 27 levels, until the last cascade's rays went on to the field's edge
(the grazing floor's room, below). Read from one cascade alone, the light stepped at each one's end,
on a canyon's floor under a sky with no sun, seen from twenty units up and twelve degrees down as
the voxel game's hills were, from one row of pixels to the next by 14% at the screen's probes' end
at `High`, 10% at the third cascade's and 15% at the last's, by 18% twice at `Medium` and by 25% at
`Low`, whose screen probes end with its last cascade, and on the game's hills at sunset from 0.6 to
1.0 of the sky's light where the last ended; faded, the canyon's floor changes from row to row by
3.7%, 3.8% and 1.9% at most at 320 by 180 (`CascadeEdgeTests`) and by 2.9%, 3.0% and 1.0% at 640 by
360.

The light from beyond the probes is a sky over a ground, the distant ambient (`distantAround` in
`lights.slang`), faded in across the last cascade's band, an eighth of its width. The sky's light
from above, the environment's irradiance straight up or the ambient lights' light, lights the share
of the sphere above a surface's horizon, (1 + n.y) / 2 of it, and below the horizon the ground sends
back its color times the sky's light on it and each directional light's on a floor
(`distantSun`), the ground's color taken as the surface's own. The shadowed light's share is read
through the shadow map where the ground the face looks at lies, the point moved along its normal by
a cell of the field's coarsest built cascade and read as a floor (`groundReached`), since at the
point itself a face turned from the sun lies behind its own mesh as the sun sees it and would take
none of the sunlit ground before it. So a wall with the sun behind it takes the ground lit where its
own shadow ends short of that point and shadowed where it reaches past, as a hill's shaded side in
its own long shadow at a low sun does, at nine more taps of the map for the pixels past the probes
that face some ground. A floor takes the sky's light from above alone, which the environment's
irradiance along its normal gives it too, and a wall half the sky and the ground's light, where the
irradiance along its normal gave it the map's light below its horizon whole and the ambient lights
all of theirs. Along a wall facing
the sun beside a floor under a sky, seen across the field's edge at 320 by 180, the largest change
from one pixel to the next fell from 2.32% to 0.88% at `High`, where the last cascade ends, and the
wall past the field read 2.8% darker than inside it where 6.3%; the floor's 0.27% is the same
(`CascadeEdgeTests`). With global illumination off a surface takes the environment's irradiance
along its normal and the ambient lights' light whole.
In `games/Ore` seen from its highest column, its field set to two cascades of a quarter block so
the field's edge lies on its land sixteen blocks out, the grass tops and the far hills past the edge
take the sky over a ground where they took the environment's light whole, up to 59 sRGB levels
apart. At Ore's own four cascades of half a block the field reaches past its 48 columns, and no
pixel changes by more than 9 levels.
![Ore past its field's edge, without the distant ambient and with it](assets/cascades/ore-field-edge.webp)

The ninth gives the field the light of an emitter thinner than a cell. A strip 0.06 thick on a wall,
under cells of 0.15, painted no cell its own, the wall nearer every cell's middle, so its light was
nowhere in the field and added nothing to global illumination. A probe beside it read none of the
4.1 its face toward the strip should have. Each emissive instance carries its thinnest extent, its
mesh's bounds along its own axes scaled into the world, and where that is under a cell a third
splat lends the cells within half a cell of its triangles its light times its thickness over the
cell, the share of a cell's face it covers, the most any of its triangles gives, in a buffer of
256ths a channel (`field_splat.slang`). The resolve gives a cell the larger of that and its nearest
surface's light, so an emissive panel under a ceiling, which paints its own cells, is not counted
twice. That share gave a strip its 0.4 and an emissive sheet as thin a seventh of its light where
its face covers a cell's face whole, and half a cell left out the cells inside a wall a sheet lies
flat on, exactly half a cell behind it, which the field blends with those in front where it is read
at the sheet, so a sheet 0.02 thick on a wall emitted 1.27 of its 2 at its face. The share is read
from the area of the emitter's faces near each cell, those a closed mesh turns toward the cell,
within a square a cell wide about the cell's middle laid on each face, seen along each axis and
taken along the axis they cover most of, a whole face at most, and lent to the cells within a cell,
so the sheet emits its 2 and the strip keeps its 0.4 (`SceneFieldTests`), where summed over
the axes a strip's front and top faces would lend a cell beside it twice what it shows from either. The probe beside the strip reads 12% under a reference of hits lit directly, the one in the
room's middle 78%, the band the strip's light lies in seen there across some seven degrees, and the
strip's room reads 45, 44 and 41% under every bounce by quality, where it read 87%. A closed room lit
by such a strip alone reads 131 levels on its floor where it read none (`GlobalIlluminationTests`).
The Cornell box's panel, 0.06 thick too, lends its edge's cells its light, the box 1 to 2 points
brighter, +7, +6 and +8%, and every other room moves a point at most. The splat runs in a loop of
its own, after a branch every thread takes alike, where in the loop of the distances and the paint
it cost a cascade's build 0.027 ms with no emitter at all; a build with no such emitter clears and
reads none of the buffer, and a cascade's build in `shaders_scene_field` takes 0.459 ms where it
took 0.455 (`field.rebuild 4000`). Read from the faces' area, the lend costs the two builds a frame
of `shaders_bounce_rooms` 0.350 ms where its first share cost 0.343 (`field.rebuild 4000`, twice
each). A panel 0.06 thick under a ceiling, as the Cornell box's, emits its whole 2 at its face where
it emitted 1.8, and the ceiling a tenth past its edge emits a fifth of it where it emitted none,
from the cells whose squares take in part of its face, so the Cornell box reads +9, +7 and +10% by
quality where it read +7, +6 and +8%, and the strip's room −46, −44 and −42%, the rest as they were
(the references `build/bounce-rooms.sh` made, compared with `gi.compare`). Global illumination costs
0.44, 0.57 and 0.66 ms. The look is read from here over five slides of the camera, along x either
way, up, ahead and askew, and global illumination adds 0.87 levels a frame to the picture's change,
0.51 to 1.28 by slide, where it adds 3.10 without the history, 1.39 to 3.92, and the panel's room
fades as it did.

The tenth holds the fade. Each frame a probe's rays take the indirect light reaching what they meet
from the probes of the frame before, so once a light goes out its indirect light goes on, less
each frame by what the walls send on. An emissive panel's room in a render texture fell under a
level of light 30 frames after the panel went dark at `Low` and 28 at `High`, and a lamp carried
across a room split by a wall left light behind it for 14 and 13 frames, a quarter of a level a
channel over where the picture settles. A probe now sums its own light, what its rays bring
straight from the sun, the lights and emissive surfaces, with all they bring, a workgroup of 64
rays at a time in the trace, added up in the gather, and where its own light fell by a fifth or
more against the frame before's, the next frame's rays take the indirect light at the share its
own light kept, channel by channel. A probe whose own light is under a quarter of all its rays
bring is judged by every probe's own light summed instead, since its own light is too little of
its light to say what went out. The two do different work. Judged by its own light alone, the
light the lamp leaves behind goes in 8 and 7 frames, and the panel's room in 29 and 26, since most
of the room's probes are lit more by the panel's indirect light than by the panel, under the
quarter; judged by the sum alone, the room goes in 2 frames at both qualities and the lamp's trail
stays at 14 and 13, since a lamp carried keeps the sum. Together the room goes under a level 2
frames after the panel and the lamp's trail in 8 and 7 (`GlobalIlluminationTests`, the room read
each frame and the lamp's picture against where it settles 90 frames on), and a corridor around a
corner from a lit room, whose probes see none of its lamp, falls to nothing within a frame of the
lamp going out, where taken whole it kept 156 of its 241 levels 24 frames on. A share for a fall
alone made the light the lamp has not yet brought to its new side worse, 10.3 levels short the
frame after where 8.4 were taken whole, the indirect light left on the old side gone before the new
side's came, and a light that flickers would have its indirect light cut at each fall and kept at
each rise, so a rise by a quarter or more is taken at its share as well, four times the indirect
light at most, and the
light not yet come is 8.1 levels short the frame after and goes in 12 and 11 frames where it went
in 15 and 13. Where nothing changes the shares are whole and every room reads as it did, the hold on
or off (`gi.toggle follow off`); the sums of a cascade that moved since the frame before are not
set against each other, and the five slides' crawl holds to the hundredth, 0.86 levels a frame
(0.51 to 1.28) against 3.10 unheld (1.39 to 3.91), which `GlobalIlluminationTests` now reads
over the five. Blocks circling a lit room with the camera still change the picture 1.08 levels a
frame at `Low` with the hold and 1.06 without, and 0.97 at `High` either way, so the field's stamps
of moving meshes do not set it off. Global illumination costs 0.444, 0.585 and 0.676 ms where it
cost 0.438, 0.576 and 0.661, timed one after the other on the Cornell view of
`shaders_bounce_rooms`; summed by one thread a probe in the gather, the 256 rays of the finer
cascades cost `High` 0.022 ms.

The eleventh reads the three rooms still far under after the tenth, which look too dim from afar,
at their probes, with `gi.probe` at `High` against references of no bounce, one bounce and
every bounce, and finds two causes, the first in most of the rooms. The reference counts the
bounces after the first
surface a path meets, so a probe's rays with `gi.toggle again off` are set against its reference
of no bounce. In the window's room a probe a unit past the sunlit patch read the light straight
from the sun 70% short, and the room's ceiling and side walls read 0.003 of the reference's 0.086
and 0.07, 91 to 97% short at one bounce already. The room is six units across and three high, a
whole number of the first cascade's spacing of 1.2, so its walls and ceiling lie in planes of its
probes, which stand inside them and hold nothing, and a surface weighed its probes at its own
point, the trilinear weight all on those in its plane and none on the row in front. Weighed from a
point half the spacing off the surface along its normal, the row in front takes the weight that
the probes inside the wall drop. Each room's error over every region against its reference with
every bounce (`build/bounce-rooms.sh`'s references, `gi.compare`), by quality.

| Room | `Low` | `Medium` | `High` |
|---|---|---|---|
| Cornell box | +9% → +10% | +7% → +8% | +10% → +11% |
| Thin walls | −15% → +6% | −14% → +6% | −14% → +6% |
| Corridor | −21% → −21% | −21% → −21% | −21% → −21% |
| Window | −53% → +8% | −53% → +10% | −50% → +17% |
| Red walls | −1% → −1% | −1% → −1% | −1% → −1% |
| Strip | −46% → −15% | −44% → −24% | −42% → −15% |
| Grazing floor | −37% → −8% | −36% → −5% | −28% → +13% |
| Carried lamp | −32% → −5% | −32% → −4% | −32% → −4% |

The errors summed fall from 214 points to 74 at `Low` and from 198 to 88 at `High`. A lean of 0.3
of the spacing reads within two points of it in every room, and 0.75 brings the Cornell box to +6
and +8% and takes the thin room to +12% and the grazing floor at `High` to +17%, 76 and 99 summed,
so it is half. A closed room whose ceiling and walls lie in planes of probes, lit by an emissive
panel on its floor, reads 191 levels on its ceiling at `Low` and 195 at `High` where it read 9.8
and 13.3 (`GlobalIlluminationTests`). The window room's middle probe brings 22% over the
reference where it brought 46% under, its light straight from the sun still 20% short and the
light past the first bounce over, as the Cornell box reads over too, and its walls and ceiling
read 32 to 48% over at `High` where they read 91 to 97% under. A surface in a plane of probes now
marches to the row in front, where it marched to none, so global illumination costs 0.473, 0.621
and 0.720 ms where it cost 0.446, 0.588 and 0.678, timed one after the other twice. More light
passes the first bounce, and it takes longer to settle. Taken whole, the panel's room falls under a
level 45 frames after the panel where it fell in 30, and with the hold in 2 still; the lamp carried
across the split room leaves 3.2 levels behind it where it left 8.5, the room settling brighter, of
which the hold keeps 0.17 at `Low` and 0.22 at `High` 4 frames on where taken whole it keeps nine
tenths, the last of it under a quarter of a level by frames 10 and 9 where taken whole by 18 and 16;
and the light the lamp has not yet brought to its new side goes in 18 frames where it went in 12.
The carried lamp's test reads the share left 4 frames on, since its last part, which lavapipe takes
to frames 11 and 12, lies near a quarter of a level. The corridor, two units
wide, keeps its −21%. Its walls read 0.000 of the reference's 0.004 and every probe of its first
cascade brings nothing, its rays meeting only walls no light reaches straight, since the second
cascade's probes, 2.4 apart, stand at its walls' planes a unit and a fifth either side of its
middle, inside the walls, so no probe carries the sunlit end's light along it into the first
cascade's reach. That is the method's limit. A corridor narrower than the second cascade's spacing
holds none of its probes where their rows fall in its walls, and moving such a probe into the
open, as DDGI does, takes it to the nearer free side, here outside the corridor, 0.1 past the
walls' outer faces against 0.2 to their inner.

The lean, read after it was committed, also takes the five slides' crawl from 0.86 levels a frame
to 0.36 (0.26 to 0.47), and unheld from 3.10 to 1.63 (1.40 to 1.80), as `GlobalIlluminationTests`
reads them.

The tenth's probes judged by every probe's own light summed tied each passage lit by indirect light
alone to every lamp in the level. In a corridor around a corner from its lit room, with a second
room apart whose lamp holds some half of every probe's own light, that lamp going out dimmed the
corridor by 3.3 and 3.7% 3 frames on at the two qualities, and a lamp that flickered there would
flicker it. Such a probe is judged by the own light of the 27 probes of its cascade within one of
it summed, this frame's against the frame before's, and the corridor keeps its light to the tenth
of a level (`GlobalIlluminationTests`). The panel's room still goes under a level 2 frames after
the panel, and a corridor around a corner of walls of 0.72 falls to nothing within a frame of its
own room's lamp going out where taken whole it fades in 22 frames. The rise counts for these
probes too. The light not yet come to the carried lamp's new side goes in 11 and 10 frames where
it went in 18, 1.9 and 1.1 levels short 6 frames on where 5.2 and 4.6, and the light left
on the old side lingers, 0.47 levels at `High` 12 frames on where 0.12, as the probes near the new
place take their indirect light raised by what their own light rose, the new light's first bounce in
it; judged by falls alone they read as the level's sum read them, 18 and 17 frames for the light not
yet come. Global illumination costs 0.468, 0.621 and 0.722 ms against 0.474, 0.621 and 0.719, within
the noise. The slower fade taken whole was read for the loop's gain before it was judged a fault. In
a closed box whose walls are all of one color, lit by an emissive panel, the hold off, the first
cascade's probes' light falls each frame after the panel goes dark by 0.515 at walls of 0.503 once
the field settles, the white panel some 4% of the surface at 1, and by 0.26 to 0.27 at walls of
0.25, so the light past the first bounce keeps its walls' share and no more. The rooms the tests and
`shaders_bounce_rooms` light are white, the tests' of 1, whose light a closed room never loses, so
they fade over more frames now that the lean lets their walls take part.

The probes of a wall's or a ceiling's plane, which the lean passes over since they stand inside it,
are moved into the room. A room's signed error over every region lets one region's excess hide
another's shortfall, as the strip's room's ceiling read 39% over while its back wall read 56% and
its floor 35% under, so each room is read here by the mean of each pixel's difference from the
reference as well, as a share of the reference's light, and the move is judged by it. Each room,
its signed error and its pixels' mean difference, before → after (`build/bounce-rooms.sh`'s
references, `gi.compare`).

| Room | `Low` | `Medium` | `High` |
|---|---|---|---|
| Cornell box | +10%, 18 → +9%, 16 | +8%, 15 → +7%, 14 | +11%, 15 → +9%, 14 |
| Thin walls | +6%, 6 → +9%, 8 | +6%, 7 → +9%, 9 | +6%, 7 → +9%, 9 |
| Corridor | −21%, 21 → −21%, 21 | −21%, 21 → −21%, 21 | −21%, 21 → −21%, 21 |
| Window | +8%, 32 → −8%, 24 | +10%, 33 → −6%, 24 | +17%, 39 → +3%, 28 |
| Red walls | −1%, 1 → −1%, 1 | −1%, 1 → −1%, 1 | −1%, 1 → −1%, 1 |
| Strip | −15%, 46 → −26%, 42 | −24%, 46 → −30%, 43 | −15%, 44 → −23%, 42 |
| Grazing floor | −8%, 13 → −8%, 10 | −5%, 13 → −4%, 8 | +13%, 19 → +10%, 13 |
| Carried lamp | −5%, 12 → −4%, 9 | −4%, 12 → −4%, 9 | −4%, 12 → −3%, 9 |

The pixels' differences sum from 149, 148 and 158 to 131, 129 and 137; the strip's room reads 11
points further under, its ceiling come down to +9%, and the thin room 3 over. Moved to a quarter of
the spacing they sum to 135 and 145 at `Low` and `High`, and moving only probes inside a mesh or
touching one reads the same as moving every probe nearer than a tenth; with the move, a lean of a
quarter sums to 134, 129 and 128 and of three quarters to 136 and 148 at `Low` and `High`, and the
lean stays half, its signed errors the smaller. A closed room whose ceiling lies in a plane of
probes has those probes holding light a tenth of their spacing below it and a probe in the open at
its spot (`GlobalIlluminationTests`). Global illumination costs 0.481 to 0.484, 0.633 and 0.738 ms
where it cost 0.469 to 0.472, 0.620 and 0.722, timed one after the other twice.

The world's probes take the indirect light at their rays' hits from the probes around each hit
that a march through the field reaches (`bouncedSeenAt`), where the screen's probes and the model
pass weigh the probes by how far each one's rays reached (`bouncedAt`), which the trace binds and
did not read. Read through the reach, the trace's hits cost global illumination 0.455, 0.595 and
0.686 ms where the marches cost 0.482, 0.631 and 0.736, and the rooms read within a point or two but
the thin room, +15% and its pixels 14 where +9% and 8, and the corridor, −11% where −21%, which the
twentieth of a probe's weight the reach keeps for one it does not see lends it from the probes in
its walls; and a closed room with a lamp that casts shadows under its floor takes 42.9 levels of the
lamp's light in a channel, where the marches hold it to 2.5 under its bound of 8
(`GlobalIlluminationTests`). The marches stay.

D closes the measurement of global illumination. `BounceRoomsTests` draws `shaders_bounce_rooms`'
eight views at 160 by 90 at each quality and holds each view's difference from its reference, its
pixels' differences in linear light summed over the reference's light, both averaged to 80 by 45,
within 15% over what it read on the RTX 4070, the references path-traced with 4096 paths a pixel and
checked in under `References/bounce`, 352 KB, the test skipped where the GPU traces no rays.
The views read, at `Low`, `Medium` and `High`, the Cornell box 0.170, 0.153 and 0.166 of its light,
the thin room 0.091, 0.084 and 0.087, the corridor 0.260, 0.259 and 0.256, the window's 0.299, 0.271
and 0.286, the red walls 0.019, 0.024 and 0.019, the strip's 0.511, 0.492 and 0.479, the grazing
floor 0.103, 0.108 and 0.124, and the carried lamp's 0.123, 0.118 and 0.122. In
`shaders_cornell_box` at 800 by 450 global illumination costs 0.37, 0.49 and 0.69 ms of the GPU,
`High` with the field at four cascades, and its world and screen probes take 1.45, 3.87 and 8.06 MB,
with each probe's own light, share and place and its rays' sums now counted by `gi.state`
(docs/materials-light-and-shadows.md). The trace's binding of the probes' reach, which it reads
nowhere since its hits march, is gone.

The guide (docs/materials-light-and-shadows.md) has each quality's GPU time and memory in
`shaders_cornell_box`, and what the reflections cost in `shaders_reflections`. Two things are left.
The screen's probes blend every probe around what their rays meet, since a trace to each cost 0.10
to 0.15 ms there and leaked 3 levels of a lamp's light without it, and a moving mesh takes part in
global illumination as the boxes of its joints or its parts the field holds it as, in its color but
with none of its emission unless it is small enough to be a glow light, which any mesh drawn is,
still or moving. A render target that draws meshes through a camera has screen probes of its own, on
the depth at half its size of the meshes it draws that cast shadows that its occlusion pass draws
(`AmbientOcclusionRenderer.DrawTarget`, `ModelRenderer.DrawDepth` for its id), probes traced, blended and held as the window's on that depth with a history of their own, let go
the frame after one the target is not drawn in (`GlobalIlluminationRenderer.DrawTarget`, before the
target's pass), and its buffer says to read them first (`TargetIllumination`). The `targets` node
runs after `global_illumination` for that, before the window's `shadows`, so a target reads this
frame's world probes. Two views of `games/Sumo` at 640 by 720 take 1.14 ms of the GPU at `Low` in
`targets` where they took 0.62 with the world's probes alone, and a target's room reads within a
tenth of a level of the window's (`GlobalIlluminationTests`), where it read some 60 levels apart. A
probe capture reads the world's probes alone, its buffer given the window's probes and its cascades
with the screen's probes and reflections off, and a target's glossy surfaces reflect the probes and
the environment alone. Where the window draws no mesh the field is placed around the first target's
camera and holds the targets' meshes (`SceneField.Gather`).

The light went on settling for a dozen frames and more after the camera moved, as the voxel game
found in a closed white room at night lit by one emissive block on its floor. Still, the picture
changes nothing, and drawn at 320 by 180 at `High` through the game's field, the frames after a walk
of four blocks over 20 frames changed 0.72, 0.50, 0.36, 0.26 and 0.19 levels, and after a quarter
turn in one frame 3.48, 2.07, 1.55, 1.09 and 0.84 (`GlobalIlluminationTests`). Two causes, each of
which the other's mend leaves standing. A screen probe's ray that met nothing took each world
probe's light from the one texel of its octahedron the ray fell in, and the normal a probe rebuilds
from the depth wavers a degree or two off its surface's, so a ray near a texel's border flipped to
the next as the camera moved; on a wall square to the world's axes half the 16 rays lie on such
borders, the hemisphere's 4 by 4 falling on the edges of the octahedron's 8 by 8. The 5 by 5 filter
made the flips the blotches the walls showed, the frame before's light averaged them while the
camera moved, and once it stopped the light drifted from that average to the one pattern that stood,
a fifth of the way each frame. The hold within twice the spread of the light around never caught it,
since that spread is the flips' own. Read between the four texels around each ray, the light changes
smoothly with the ray's way. And the world's probes' rays read the indirect light of the frame
before at their cascades' corners of this frame, where the faces had been gathered at the frame
before's, so once a cascade moved with the eye, by a probe's spacing as its middle passes each
spacing, each probe took the faces of a probe a step or more beside it, and the light past the
first bounce, fed from those faces each frame, took ten frames and more to find itself; the cascades
lie a third of their width ahead of the eye, so a turn moves every one. Read at the corners they
were gathered at, the walk's frames after change 0.24, 0.17, 0.13, 0.09 and 0.07, and the turn's
0.18, 0.13, 0.10, 0.08 and 0.06; with the texels alone the turn's fifth reads 0.69, and with the
corners alone the walk's reads 0.19. A test holds each fifth frame under 0.12. The slide through the
Cornell box loses its crawl with the flips, as above, 0.33 levels a frame to −0.03 and unheld 1.61
to 0.03. The four texels cost global illumination some 0.04 ms in `shaders_cornell_box` at 800 by
450, 0.405, 0.534 and 0.735 ms at `Low`, `Medium` and `High` where it took 0.367, 0.494 and 0.696,
measured as the guide measures it, by `./e3d command profile`'s `global_illumination`. Against the
path-traced rooms the Cornell box reads 0.203, 0.180 and 0.183 of its light where it read 0.170,
0.153 and 0.166, the strip's 0.466, 0.473 and 0.448 where 0.511, 0.492 and 0.479, and the carried
lamp's 0.119, 0.103 and 0.109 where 0.123, 0.118 and 0.122, the others within a hundredth, the eight
views summing 4.59 where they summed 4.62 (`BounceRoomsTests`, each bound moved to 15% over its
view's reading). The corners change no room's reading. What drift is left lies on the floor before
the emissive block in patches a tile wide, where some of a probe's 16 rays meet the small bright
block and the rest miss it, as the world's probes show lobes around a small emitter.

Small emissive surfaces are lights inside global illumination (`GlowLights`, `glow.slang`).
The voxel game found a glowstone on its floor drawing eight lobes on the floor around it at `High`,
its pool ending two blocks out, and seen from forty blocks no pool at all. Measured on the engine
first, the cause was the probes' horizon and not the field's hold of the block or the first
cascade's directions. The world's probes stand at odd block coordinates, so the lowest layer above a
floor lies level with the block's top, whose upward faces the floor reads through the lean, and the
layer under them lies inside the floor; with the screen's probes off the floor beside the block read
nothing at either cell, and the block lifted a block off the floor lit it with no lobes. So the
floor took the block's light from the screen probes' sixteen rays alone, twelve of them 18 degrees
above the horizon at fixed azimuths, whose gaps of 26.6 and 36.9 degrees met the block's 28 at two
blocks at some angles and not others, and their reach of two blocks was the pool's edge. Sixty-four
rays took the pool at a block and a half from 0.119 to 0.048, the sixteen having taken the block for
a whole texel each where they met it, a set turned 22.5 degrees left the lobes, and the first
cascade at 256 directions changed nothing.

An emitter whose box is no longer than twice the first cascade's spacing is carried as a light, its
box in the world, its emission and its mesh's surface over its box's, the 64 nearest the eye, each
reaching as far as it lights a surface by a fiftieth; emitters square to the axes that touch and
emit the same light are one light of the box around them, the faces they press together left out of
its surface. A mesh of many emitters, as Ore draws its ore, is cut into its parts, its triangles
joined where they share a corner (`GlowLights.PartsOf`), and carried where each part is, a light
each, naming the faces of its box its triangles lie on, two bits an axis packed with its share
(`glowShare` and `glowFaces` in `glow.slang`), so a block showing its top and one side emits from
those alone, where spread over six faces it lit the ground before its side by a third; a part flat
as one face is a hundredth of its size thick about it. The field and the ray scene mark such a mesh
whole, so its parts past the 64 nearest light nothing a probe carries to the ground around them,
where taken whole the mesh lit it through the probes' rays; Ore holds 69, `gi.state` counting them.
Its light at a point is that of the faces of its box the point lies before, in closed form within
four of its half diagonals, Lambert's sum over each face's edges clipped to the point's horizon by
Heitz's fit of the arc, and past them each face as points, cut along its longer side into pieces
about as long as its shorter, four at most, or a face about square into quarters, within 1.6% of the
closed form there; as one point a face read 7% over it, a block's, and 11%, a rod's 2 by 15 by 2
sixteenths, which stepped the floor's light around the voxel game's end rod by 7 to 9% where the
forms met, and cut so the rod's floor reads a path-traced reference within 2% to 1.6 blocks, past
which its reach fades it, and bends nowhere (`GlowLightsTests`). How much of it a point sees is
marched through the field, at each step the way's clearance from the nearest surface against the
cone the light spans there, so a shadow's edge is as soft as the light is wide, as a distance field
gives a penumbra, a surface counted only where it lies nearer than the point's own plane and the
light's own box, and inside a surface the share read by stepping out of it along the point's normal
and, where the field's slope there leans across the way, across it toward the nearest face, the
nearer way out kept, once after the march at the step that lay deepest inside, and at each step
bounded by the way to the nearest face, which the way out is no shorter than, the march ending by
its own condition where the bound leaves nothing. Each way out ends where it left the surface, the
step that overshot taken back by the distance read past it, so the depth under an edge goes to
nothing as the way grazes it and meets the share outside. Ended at the step's end, a way that grazed
an edge was at least half a cell under it, and in the voxel game's room of a lamp of four glowstones
beside a pillar of two columns, the wall behind the pillar rose by 22 to 24% of its light in 20
pixels in every band of rows, the step's place moving by up to 17 pixels at the pillar's top and
foot, where a path-traced reference of 1,024 paths rises smoothly. The frame now rises by 5 to 7% in
any 20 pixels there, the dark side 0.95 of the reference where 0.78, the 27 cases of
`shaders_bounce_rooms` within 0.0003 of their shares, the glowstone room's walls a point nearer the
reference, and the GPU time of global illumination and of the model pass the same
(`GlowPillarTests`). The cost is a penumbra that follows the probes' points more behind a wall one
cell thick, whose depth under its top is read from the field's distance there, a turn of five pixels
moving the floor's light behind it by 5.4% where 2.2% (`GlowPenumbraTests`). Read at each step by a
loop inside the march's own, or the march left by a break inside a wall, it lost the RTX 4070's
device in a closed room with a lamp inside seen from outside, an MMU fault through the constant
cache (Xid 31) for which the validation layer's GPU-assisted checks found no read out of range, and
a test holds the room (`GlowLightsTests`). The screen's probes march to the halves of each face of
each light in reach that gives the probe's surface an eighth of the lights' light or more, halved
across the axis that rises most from the surface, or the longer where neither rises more, and keep a
byte of each such light, and march to the middle of each fainter one and keep the fainter ones'
light summed. Under a row of three emissive blocks touching under a ceiling, one light, with a block
floating under its middle, the face halved along its short side was marched at two points on its
middle line, both behind the block from the floor under the whole row, and a march passing inside
the block's side read as deep as the block is tall, so the floor three blocks under the block read
0.74 of a path-traced reference and beside it 1.09, where 0.98 and 1.07 with the longer side halved
and the way out taken across (`GlowLightsTests`). Each half counts by its own light as a point, its
area by the two cosines over the square of its distance. Weighed by the box's half size along the
face's normal in place of the face's area, alike for a cube, four emissive blocks two by two weighed
the side a pillar beside them hid at a quarter of its due against the face it left open, so the wall
past the pillar read 0.07 to 0.19 more of the light seen past it than a path-traced reference, and a
red wall in the voxel game 1.2 to 1.3 of its reference; weighed by area, within 0.03
(`GlowLightsTests`), and the red wall 0.90 to 0.98. Each marches from a point of its tile that moves
from frame to frame, the middles of the tile's four by four cells in the order of Bayer's matrix, so
a round of sixteen frames marches from each texel of the half-sized depth under the tile once, while
the probe's tiers and its rays stay at the tile's middle. A pass over the half-sized depth
(`gi_screen_glow.slang`) takes each texel's light from the four probes around it, where they marched
from, weighed by how near each is, how alike its normal is, how near its distance from the eye is to
the distance the texel's plane lies at along its way, and how near that plane the depth lies a third
and two thirds of the way to it, so a probe on the floor past a wall does not lend the floor before
the wall what it sees; where none of the four is so, the four of the four by four around that count
most. The texel's normal is taken along each axis toward the side whose next two texels go on in a
line, so a texel at a wall's foot takes the floor's. The pass blends the light with what the texel
held the frame before where its surface was, from the texels of the frame before as far from the eye
whose normal lies within 60 degrees of the texel's, kept beside each texel's state. A texel holds
its light as a share of its own surface's rough light, and taken by their distance alone, the texels
of a wall at a corner lent the floor along it the wall's share, a line of light along the ninth
room's corners a texel apart, the corner 37% over the floor and the wall beside it at 800 by 450
where 4% under (`BounceRoomsTests`). Within 25 degrees, a surface's own texels at 160 by 90, two
pixels wide and their normals taken from a wavering depth near its edges, lost their blend too, and
the ninth room's view read 0.0506 where 0.0502. The blend is a running mean over the first round and
a thirty-second
of each frame's after it, an eighth while the view moves, a moving view's blend counting as one
frame's so the mean starts anew where it stops. The blend is dropped where no texel of the frame
before held the surface, where a light that reaches it has a place whose light the frame before did
not have there, the lights kept in their places from frame to frame (`GlowPlaces`), or where, after
a round, the frame's light lies further from it than half again the furthest the frames' light had
lain, that furthest let go by a sixty-fourth a frame. A blend younger than a round is dropped as
well where the field's cascade under the texel was built again in the frame, within the box around
the meshes that came or went in it, which the view's bytes carry, since before the field holds a
wall the probes see a lamp past it. Held whole in the blend, those first frames left a penumbra
straying from where it settles by 6.3% two rounds after an app began and under 1% only after 110
frames, and with the young blend dropped by 0.5% (`GlowPenumbraTests`). What a texel keeps is, of
the light near it that gives it the most, within twice its box's half diagonal, its share of that
light's rough unhidden light, half its box's surface over the square of the distance from the box
(`glowLightRough`), which the model pass scales by the same worked out at the pixel with the pixel's
own normal, so a lamp's light falls away at each pixel as it does across the lamp's thickness, and
the other lights' light as it is. The model pass reads the two by two texels around a pixel, each
weighed by how near it is, how alike its normal is and how near its distance from the eye is to the
pixel's plane's there. A pixel whose like texels no probe holds, as a sliver of floor between two
lamps, marches to the middles of the four brightest, one on a light's own surface takes no other's,
and a view with no screen probes marches from each pixel to the faces' halves of the four brightest,
as the probes do. A probe's ray's hit marches to the middles of the two brightest, so the walls a
lamp lights reflect its light on. A light gives nothing to a point within half a cell of its box
whose normal leads out of it, the light's own surface, since a hit lies anywhere within a fraction
of a cell of a face and the field's normal tilts near its edges, so part of the face rose above the
hit's plane and lit it. On lavapipe the block's faces lit themselves and added 0.018 to the floor
two blocks off, 0.044. The field marks the cells such an emitter paints, so a ray that meets it
leaves its light out (`surfaceGlowUncarried`), and a reflection keeps it. A probe's reference
(`gi.probe`) leaves it out where a path first meets the emitter, as the probe's rays do; counted,
the rays of twelve probes around four emissive blocks read 44 to 70% under it at one bounce, and
within 12% of it left out, all but the dimmest, which reads 37% under. The shaders hold one march
and one closed form for each place that reads the glow lights, a reflection's hit shaded once
whether met on the screen or in the field, and their loops are kept from unrolling, since unrolled
and inlined at each of the model pass's calls they made its compile on lavapipe 0.9 s for every app
where it took 0.23 before them, and the Windows job's tests ran out their time; 0.32 so, the
Rendering tests on lavapipe here 280 s where 261 before the glow lights. A frame with no glow lights
draws with a build of the model pass that leaves them out (`NO_GLOW_LIGHTS`, read as `Glowing` in
`lights.slang`), and a frame without global illumination with one that leaves it out and the traced
reflection with it (`NO_BOUNCE`, read as `Bouncing`), picked each frame by the count the frame's
lights hold and whether global illumination is on (`ModelBuild`). The fragment stage is 253,212
bytes of SPIR-V whole, 124,660 without the glow lights and 55,228 without global illumination, each
in a program's cache from `e3d shaders`, which compiles each set of the variants a shader names and
`NO_BOUNCE` alone, since that build traces no rays. lavapipe compiles a pipeline's variants on a
thread of its own as it first draws with them, and the device's last wait waits for them, so with
Mesa's cache off an app that draws a lit cube without global illumination took 1.3 to 1.4 seconds
after the first in one process, where it took 2.1 to 2.5 with the build without the glow lights and
3.9 to 4.1 whole, its last wait 0.8 to 0.9 seconds where 1.6 to 1.8 and 2.9 to 3.1. A program's own
model shader that imports `modelpass` is built whole.

Read in linear light from straight above, the game's glowstone on a floor lights the floor 0.257,
0.100, 0.045, 0.023, 0.013 and 0.0055 at one to four blocks in a path-traced reference, where the
engine read 0.126, 0.101, 0.068, 0.034, 0.012 and nothing, its eighth harmonic 0.17 and 0.35 of the
light two and two and a half blocks out at `High`; carried, every ring from 0.8 to 4.2 blocks reads
0.96 to 1.05 of the reference at every quality and both cells, the eighth harmonic 0.005, and the
same from five to eighty blocks up (`GlowLightsTests`). Hit or miss, a shadow had no edge and ran in
the field's cells, and a stack of four blocks threw a ring; counted, the floor and the block's own
faces darkened the falloff in rings, 0.83 to 1.02 of the reference with dips of a tenth. Past a wall
half as high as the block, a block and a quarter off, the floor reads the reference's light to
within a tenth of it, where one march to the block's middle read it dark to five blocks, and four
blocks stacked two by two light the floor around them within 2% of the reference. Against the
path-traced rooms the Cornell box's panel and the strip are such emitters. The Cornell box reads
0.141, 0.129 and 0.117 of its light where it read 0.203, 0.180 and 0.183, the strip's 0.256, 0.248
and 0.270 where 0.466, 0.473 and 0.448, the others as they were, and a ninth room, an emissive block
on a white floor in a dark room with a low wall beside it and four more stacked, 0.052, 0.048 and
0.045 where it read 0.200, 0.193 and 0.171 (`BounceRoomsTests`). Taken from texels of half the
window's size, which at the rooms' 160 by 90 are two pixels wide beside a strip six hundredths of a
block thick, the strip's read 0.275, 0.267 and 0.290 and the ninth 0.060, 0.055 and 0.050, where at
1280 by 720 they read 0.163 and 0.020 against 0.162 and 0.018 with each pixel's own. On an RTX 4070
the Cornell box's panel costs global illumination 0.21 to 0.26 ms and the model pass 0.04 to 0.05 at
800 by 450, and a dark room of emissive blocks at 1280 by 720 at `High` costs them 0.33 and 0.02 ms
for one block, 0.97 and 0.11 for sixteen and 2.63 and 0.14 for sixty-four, where the probes' marks
read at each pixel cost 0.28 and 0.04, 0.85 and 0.22, and 2.35 and 0.30 in the same session, and
before that 0.26 and 0.03, 0.70 and 0.14, and 1.78 and 0.30, where marching to each face of every
light, and from each pixel at an edge, cost sixty-four 5.3 and 2.3, and a room of sixteen eleven
blocks square, its lamps two and a half apart, 0.68 and 0.25, where the lights of a sixteenth of the
light read at each pixel cost them 0.89 and 0.47; in the voxel game's own room, its lamps two apart
and the eye a block over the floor, the model pass's share was 0.30 ms, 0.40 with the glow lights'
code as `0f5cdbcf` compiled it, the same with the land around it drawn to two columns as to eight,
and the room's global illumination and model pass take 1.89 to 1.96 and 0.47 to 0.56 ms whole, where
with the marks read at each pixel they took 1.72 to 1.74 and 0.74 to 0.90 (`./e3d command profile`'s
`global_illumination` and `hdr_scene`, against `gi.toggle glow off`).

Across a lamp's penumbra behind a wall half its height, a turn of one to eight pixels at 640 by 360
moves the floor's light along it by 0.36 to 0.89% of it on average, where a pixel that marches
itself moves it by 0.69 to 0.91%, the probes' points blended at the probes by 1.6 to 2.7%, and the
probes marching from their tiles' middles by 3.1 to 7.9%; at 320 by 180 by 3%, where at the tiles'
middles by 15 to 18%, and the line holds within 0.8% of its light over a round of the points
(`GlowPenumbraTests`). A lamp placed shows in its first frame, all its light there and its penumbra
one point of a round, 7.7% from where it settles, and within 1% a round on; the floor behind a wall
taken away takes the lamp's light in the frame the wall goes. After a turn of two to six pixels a
frame stops, the 99th part of the picture's pixels changes by 0.8% of its mean light in the frame
after and by 0.3% fifteen frames on, and a frame of a slow turn differs from the one before by 0.1%
of the light more than where each pixel marches. In the first frames of an app, before the field
holds what stands between, every point sees the lamps, and a texel under a shadow lets that go over
some hundred frames, which the tests draw past. A frame with no glow light skips the pass, which
cost a frame of an app of a cube 0.09 s on lavapipe at 320 by 180.

**The glow lights with the screen's probes left out, read on 2026-10-21.** A program that leaves the
screen's probes out (`SetGlobalIlluminationScreenProbes`) left each pixel to march to every glow
light in view itself, a cost that grows with the lights; the Cornell box's model pass took 1.01 ms at
800 by 450 where 0.30 with the probes, and 2.24 at 1280 by 720, the strip's room 1.13 where 0.29,
`games/Ore` at night, sixty-four lit, 5.65 where 0.61, and the voxel game 12.9 with forty glowstones
in view where 1.8. While a glow light is lit, the screen's probes now stand for the glow lights
alone, placed and marching to them once a tile as they do, with no ray traced, filtered or held, and
the glow pass makes the depth's texels from them, which a pixel reads at a cost the lights do not
change (`GlobalIlluminationRenderer.GlowOnly`). The model pass is back to 0.29 and 0.63 ms in the
Cornell box, 0.29 in the strip's room and 0.53 in Ore, for 0.12 ms more of global illumination in
the Cornell box than with nothing of the screen's, and Ore's whole global illumination 1.73 ms where
3.61 with the probes. Every view with a glow light reads as far from its reference as the march
did at both sizes, the Cornell box 0.083 at `Low` and 0.089 at `High`, the strip's room 0.116 and
the emissive block's 0.057 and 0.056, and the stepped penumbrae the march left on Ore's ground are
gone, the probes' tiles showing nowhere on its ore. A pixel still marches to a glow light's face
halves itself where the screen's probes are traced and left unread (`gi.toggle screen off`) and
where no texel around it holds a point like its own, and there a shadow's edge steps and tears where
the probes' texels read it smooth. Under a glowstone on a stone with a pillar of two stones three
blocks east, seen from above at 1280 by 720 at `High`, the penumbra's contours break at the
shadow's edges under the toggle, run smooth with the probes left out and waver a little with them
read. The voxel game's capture of stepped penumbrae on 2026-10-21 took the toggle's path.
![Ore's ground beside a block at night, with the screen's probes, marched at each pixel, and with probes standing for the glow lights alone](assets/cascades/glow-only-ore.webp)

**The screen's probes by quality, read on 2026-10-25.** `SetGlobalIlluminationScreenProbes` left
the flat API, and the quality carries the probes in its place, so a game gives its player no switch
of their own. `Low` leaves them out, every surface reading the cascades' light alone and the glow
lights read from probes standing for them alone (`GlobalIlluminationRenderer.GlowOnly`), as the
call's off read them, and `Medium` and `High` stand them as before. `gi.toggle screen off` and the
global illumination window's box leave them out whole at any quality for reading, those standing
for the glow lights with them. In the voxel game's room of forty glowstones at hour 23 at 1280 by
720 with `High`'s cascades, the scene pass took 2.24 ms with the probes, 1.2 ms with the probes
standing for the glow lights alone, and 13.3 ms with each pixel marching to the lights, which is
what the toggle's frame costs. The bounce rooms at `Low` read 0.099, 0.117, 0.270, 0.227, 0.019,
0.202, 0.051, 0.062 and 0.058 of their light from their references, where with the probes 0.115,
0.078, 0.226, 0.308, 0.020, 0.207, 0.069, 0.113 and 0.058, the thin walls and the corridor further
with nothing under the probes' spacing, and the Cornell box, the window, the grazing floor and the
carried lamp nearer.

**The screen's probes' spacing by the window's height, read on 2026-10-25.** Every 8 pixels at
`High` whatever the window, 3840 by 2160 placed nine times the probes of 1280 by 720, and in the
voxel game's room of forty glowstones global illumination took 8.3, 13.7, 17.8 and 35.9 ms at 1280
by 720, 1920 by 1080, 2560 by 1440 and 3840 by 2160 offscreen on the development machine's GPU,
about 3 of it the world's probes, and the Cornell box 3.3, 3.8, 4.5 and 7.2. Past 1080 pixels high
the tile grows with the height (`GlobalIlluminationRenderer.TileAt`), every 11 pixels at 2560 by
1440 and 16 at 3840 by 2160 at `High`, and the room reads 8.3, 13.1, 12.4 and 13.3 ms, the Cornell
box 2.4, 3.7, 3.1 and 3.7. A render texture's probes take the window's tile. The nine rooms, each
traced at 1920 by 1080 with 256 paths a pixel and read in 96 by 54 blocks, read as far from their
references at 3840 by 2160 as at 1920 by 1080, 0.040, 0.087, 0.162, 0.221, 0.007, 0.086, 0.049,
0.039 and 0.018 where 0.040, 0.087, 0.163, 0.223, 0.007, 0.085, 0.051, 0.040 and 0.019, and every 8
pixels at 3840 by 2160 0.040, 0.087, 0.160, 0.221, 0.007, 0.084, 0.047, 0.039 and 0.018. Another
session's tests shared the GPU through the readings, which may have raised the times alike.

**The thin walls at Low, read on 2026-10-25.** With no screen probe standing, every pixel read the
world's probes by their reach (`probeSees`), judged from 0.3 of the probes' spacing off its surface,
and by the foot of a wall a tenth of a unit thick, under the field's cell of 0.15, that point lay
within the wall's cells, as far from a probe beyond the wall as the probe's rays reached, so the
probe read as seen and the bright lamp outside the bounce rooms' thin room lit the floor and the
walls inside along each foot in triangles, the floor there 0.303 of linear light with the lamp
outside and 0.196 without. Marching each pixel to its probes through the field (`bouncedSeen`)
took the triangles away and cost the scene pass 0.46 ms more at 1920 by 1080, and marching where
another surface lies nearer the judged point than the pixel's own took 2.5 ms more in the voxel
game's room of forty glowstones, its blocks' corners everywhere. A pixel marches where that other
surface is thinner than a cell, the field a cell through it outside again (`byThinWall`), 0.5% of
the room's pixels and 0.75% of seed 1's spawn, the scene pass 0.25 to 0.57 ms more there, and the
thin room's floor reads 0.207 with the lamp outside and without it alike. The rooms at Low read
0.103 and 0.107 where 0.099 and 0.117 for the Cornell box and the thin walls, the rest as before. A
speck where the thin room's right and back walls meet its floor stays, as it does with every pixel
marching, Low's remainder.

**The world's probes' march at High, read on 2026-10-25.** A pixel the screen's probes stand around
on no surface like its own read the world's probes by marching the field toward each of eight
(`bouncedSeen`), for the thin room's corner, which the reach let read the lamp outside. In the
voxel game's cherry grove at High at 1920 by 1080 that was 9.1% of the pixels, its leaves, grass and
far blocks, and the march took 3 to 5.7 ms of the scene pass, nearly all the world's probes' cost
there, the pass 7.7 to 7.9 ms with no read of them at all. Such a pixel marches by a wall thinner
than a cell alone (`byThinWall`), and the grove's scene pass read 10.53, 10.57; 11.13, 10.84; and
10.00, 9.66 ms where 13.07, 13.08; 12.11, 12.11; and 15.41, 15.35 in three rounds side by side,
other programs holding part of the GPU, and an earlier round 10.25 and 10.33 where 12.29 and 12.61.
The frames are alike to the eye, the bounce rooms read as before at every quality, and the thin
room's corner holds, where with no march at all it read 2.17 times its wall. A pass gathering the
world's probes at half the window's size, read first, saved nothing, the march and not the gather
being the cost.

**Surfaces that let light through, read on 2026-10-25.** The voxel game's rain forest floor at noon
read near black under its canopy, the field holding the leaves solid, so no sky light reached the
floor through the probes. A material's `LightThrough` sets the share of light that passes a unit of
a surface's depth, which the field's colors carry in their alpha (`lightThroughPaint` in
`scenefield.slang`). A ray of the cascades or of the screen's probes that meets such a cell marches
through the surface half a cell a step (`lightThroughDepth` in `lightthrough.slang`), keeps the
share for each unit of depth and takes the rest of the surface's light, crossing up to four
surfaces. A surface crossed before a cascade's interval begins is the cascade below's, so the ray
keeps its share for its probe's own faces alone, in the distances' second channel, which adds 0.12
MB to Low's probes, 0.37 to Medium's and 0.56 to High's; taken off its light, the floor under a slab
read the sky's light halved twice. Under a slab a unit thick letting half through a unit a floor
takes 0.36 of the sky's light the solid slab holds back at Low and 0.40 at High two units under it,
where the slab's depth along each way gives 0.36, the rays at a slant crossing more of it, and under
one letting all of it through 0.99, as the reading below has it. A frame where no surface drawn lets
light through traces with `gi_trace.slang` built with `NO_LIGHT_THROUGH`. With the ray's light
written as the crossing writes it, summed over what the ray met and set after, the trace cost global
illumination 7% more at Low in the bounce rooms even with the crossing compiled out, 0.594 ms where
0.553, and with it written at the hit as before 0.554 and 0.555 where 0.552 and 0.551. The screen's
probes shade what a ray meets in two places, the surfaces it crosses and the one it stops at in one
and what lies past its reach in the other, where they shaded in three, which takes 0.16 to 0.60 ms
off High in the bounce rooms, the Cornell box 1.59 and 1.60 ms where 1.91 and 1.92 at 800 by 450 and
2.09 and 2.06 where 2.62 and 2.66 at 1920 by 1080, the thin walls' room 1.10 where 1.26 and 1.29 and
1.74 where 1.93 and 1.92, the frames within 2 of 255 of what they were, and the Cornell box at
Medium 1.50 where 1.82, the least of twelve readings each with other programs on the GPU.

**What a canopy lets through past the screen's probes' reach, read on 2026-10-25.** Under a slab of
leaves thirty units across five units up, letting half through a unit, the floor at High took as
little of the sky's light as under a solid one, 0.0089 where 0.0077 in linear light, while at Low it
took 0.035 where 0.015. Two things held the light back. A ray of the cascades that crossed the
leaves had its reach set where it first met them, so the merge's read toward where the probe above
met a surface and the screen's probes' test of which world probes see a point both took the leaves
as a wall; with the reach set where the ray stopped, past the leaves, the slab two units up read
0.36 at Low and 0.40 at High where 0.23 and 0.29. And the merge passed over each probe of the
cascade above that the field hid from a probe, which a canopy did for every probe of the cascade
above standing over it, so a cascade under the canopy took nothing from above it and the screen's
probes, reading the cascades' light past their reach of four units, read black; seen past the leaves
(`seesPastLeaves` in `lightthrough.slang`), a floor under a canopy five units up takes 0.31 of the
sky's light the solid one holds back where the canopy's depth along each way gives 0.38, and 0.004
without. The floor under the picture's canopy of leaf blocks letting 0.6 through reads 0.041 at High
where 0.007 solid. The bounce rooms read as before, 0.553 to 0.556 ms at Low and 1.60 to 1.66 at
High in the Cornell box at 800 by 450 against 0.553 to 0.555 and 1.61, the merge's added read only
where the field stops its march. In the voxel game's cherry grove, its leaves unmarked, global
illumination took 1.21 to 1.51 ms at Low at 800 by 450 where 1.22 to 1.44 before this item and 1.46
to 1.48 at 1920 by 1080 where 1.47 to 1.49, and at High 2.85 to 3.21 where 3.28 to 3.33 and 3.35 to
3.38 where 4.24 to 4.60, the screen's probes shading in two places, the scene pass alike. Where
leaves fill much of the view, the canopy over the eye at 800 by 450, crossing them takes global
illumination from 0.60 ms to 1.10 at Low and from 1.53 to 2.49 at High.

**A canopy's many faces crossed for less, read on 2026-10-26.** A voxel game's cut-out leaves keep a
face against each neighbor, so a ray through a canopy crosses a face a block, up to four, and each
was shaded where the ray crossed it, with a march to the probes it sees, the sun's shadow and the
glow lights each time. In the voxel game's cherry grove at seed 1 at 1920 by 1080, crossing its
leaves took global illumination 2.55 to 2.70 ms at High and 1.48 to 1.65 at Low against the rays
stopping at them as at solid ones (`gi.toggle through off`), at High about 1.6 ms in the cascades'
rays, 1.1 in the screen's probes' and 0.2 in the merge, the crossing left in one stage at a time. A
build crossing one leaf at most took 1.7 ms of it at High, shading the surface the ray stopped at
past the leaf 0.8 of that, the march past it the rest, and the march through the leaf's depth too
little to read. The first surface letting light through that a ray crosses stands for every one it
crosses, shaded once after the march for the share of the ray's light they take together, and the
surface the ray stops at is shaded once for the share that reaches it, in `gi_trace.slang` and
`gi_screen.slang` alike, so a ray is shaded twice at most and never in the march. Crossing the
grove's leaves takes 1.95 to 2.06 ms at High and 1.06 to 1.08 at Low, at High 1.4 in the cascades'
rays, 0.46 in the screen's probes' and 0.13 in the merge, and the canopy over the eye above 0.39 ms
at Low and 0.70 at High where 0.48 and 0.95. What is left is the march past the leaves, which a ray
that meets nothing pays as well; a bound of one crossing took 0.2 ms more off at High and at Low and
one of two 0.05 to 0.1, and cutting the march through a canopy once the ray kept under a
thirty-second of its light took nothing, so neither is kept. The grove's picture at High differs by
0.2 of 255 on the whole and by 0.1 to 0.6 on its floor under the trees, the floor under a canopy of
leaf blocks two deep
(`A_Floor_Under_A_Canopy_Of_Leaf_Blocks_Takes_The_Sky_Light_Their_Faces_Let_Through`) takes the same
share of the sky's light to four places, 0.30 at High two and a half units under it, 0.17 at Low and
0.23 at High five and a half under it, and the bounce rooms, where nothing lets light through, take
0.75 ms at Low and 2.08 to 2.16 at High at 1920 by 1080 where 0.81 and 2.19 to 2.31, their frames
differing between the two no more than between two runs of either. The path-traced reference holds
every surface solid, so no reference reads the light through the leaves, and the references the
tests hold read as before.

**The screen's probes on thin two-sided walls, read on 2026-10-26.** In a room built as the voxel
game's wheat field, thin two-sided walls a block tall four to a block, half a unit apart, under the
game's sun at ten in the morning, the walls facing the eye and the sun read 44% over a path-traced
reference at High at 800 by 450 and those seen from their backs 18%, the second taking no screen
probe, since the model pass looked them up along the normal as given. Looked up along the normal
turned to the eye as the shading's is, the back of a two-sided face takes the screen's probes
standing on it, and the two kinds read 44% and 45% over close and 14% and 8% from further off. The
excess is the field's. It holds a two-sided face below zero within half a cell either side, so
between walls half a unit apart, two cells of a quarter, every cell reads a surface, and a screen
probe's rays, starting half a cell off their wall, are met within 0.11 of their start and take the
wall's own light, the probe under one pixel 255% over the reference along its ways
(`gi.screenprobe`). Three mends were read and none is kept. Leaving such a probe out for the world's
probes read the walls 15% over and drew the probes still traced among them as bright patches a tile
wide; having its rays read the world's probes along their ways read them within 1% of the reference
but let a closed room's thin walls take the light outside them; having its march pass a surface the
ray is leaving kept the rooms closed and read the walls 30% over. A narrower band for two-sided
faces, or a finer cascade where thin walls stand, is item 131's. The voxel game keeps its plants out
of the field, so its wheat does not show it.

**What the light looks like, read on 2026-10-18.** The examples that draw global illumination,
`shaders_cornell_box`, `shaders_bounce_rooms` in the room its capture shows, the lamp carried, and
in the thin walls' room and the emissive block's, `shaders_reflections`, and `games/Ore` where its
capture stands, by night and by day, were captured at the frames their captures take on the RTX 4070
and on lavapipe (Mesa 26.2.2), and each frame at `High` was measured against a path-traced reference
of 512 paths a pixel (`gi.reference`, `gi.compare`), whole and with each part of global illumination
left out in turn (`gi.toggle`). Lavapipe draws each fault below as the GPU does, so each comes from
the method and none from the device. Each picture is linear light drawn through the reference's
curve, clamped and encoded as sRGB, the frame first and the reference last, from the frame's light
that `gi.compare` writes beside its difference.

- **a. The ore's light on the ground beside it in Ore.** By night the ground before the ore block at
  the right lies in uneven patches with dark holes, where the reference's light falls off smoothly
  from the block's foot. Over that ground the frame's blocks of 16 pixels read 0.03 to 1.74 of the
  reference's light from the tenth percentile to the ninetieth, their mean 0.037 against 0.046, and
  by day the same patches lie on the stone as a pink stain. The world probes give the ground none of
  it, 0 with the screen probes left out, and the cliffs beside each ore lose the orange the
  reference puts on them. The first cascade's probe nearest the ground there stands at 22, 10, 30, a
  unit above it, and the block, a unit tall, sends its light to it level or from below, so the face
  a floor facing up reads holds 0.000 where the reference's probe holds 0.028 (`gi.probe`). The
  model pass reads the world probes only where no screen probe holds a pixel, so the screen probes
  alone light that ground, 16 rays from the middle of each tile of 8 pixels finding the block or
  missing it, and their 5 by 5 filter blends the tiles' steps into the patches. Mended by carrying
  such a mesh's parts as glow lights, each from the faces of its box it shows (above), the ground
  reads 0.0446 of the reference's 0.0455, its blocks of 16 pixels 0.95 to 0.99 of the reference's
  light, the cliffs beside the ore 0.111 of 0.112 where they read 0.093, and the whole frame 1%
  under where 3% (the pictures, the frame before, the frame now and the reference).
  ![The ground beside Ore's ore by night, the frame before the mend, the frame after it, and the reference](assets/cascades/ore-ground-cloud.webp)
- **b. Corners without their darkening.** In the Cornell box the back wall at the red wall's corner
  reads 0.76 of its light 50 pixels in, where the reference's reads 0.60, so the room looks flat at
  its edges. With the screen probes left out it reads 0.58, so the screen probes lift it, each
  tile's probe standing at its middle, up to 8 pixels off the corner, where more of the room lies
  open to it than to the corner. The world probes alone darken the corner as the reference does and
  draw faceted shapes along the ceiling's edge. Reading the light beyond the screen probes' rays
  where they end (c) leaves the corner at 0.77, its probe on the back wall reading the second
  cascade, whose probe nearest its rays' ends stands over the tall block and met a surface before
  its interval began in 182 of its 233 ways, and its rays meeting the red wall reading the light
  past the first bounce there twice the reference's. Four rays along each of the first cascade's
  texels (c) leave it at 0.77 too. Its probe's rays went 1.2 units, half as far as in the first
  cascade, as they had since a closed room near the field's side behind the eye took 0.0037 of the
  sky's light where 0.0007, and read the light beyond from the probe over the tall block, which
  holds that block's lamp-lit top and the ceiling under the lamp as met before its interval began;
  going 2.4, the corner's screen probe reads 5% over the reference across its hemisphere where 86%,
  and the corner 0.68 of the wall 50 pixels in where 0.77, the reference 0.60, while the closed room
  keeps 0.0004 of the sky's light at `High` and 0.0003 at `Low`, as it did (`gi.screenprobe`,
  `gi.compare`, CascadeEdgeTests, the pictures, the frame before, the frame now and the reference,
  at half the light). Global illumination costs 1.43 to 1.44 ms where 1.39 to 1.40 in the Cornell
  box at 800 by 450 on the development machine's GPU, and alike in the carried lamp's room and on
  lavapipe (`./e3d command profile`, the frames unlimited). What remains lies in the corner's own
  surfaces, its screen probe's rays meeting the red wall within 0.2 of the back wall reading 120 to
  139% over the reference across their ways, their light carrying 0.10 to 0.35 of green where the
  reference's holds 0.02, the field's color there blending the white wall into the red within its
  cells of 0.3, and the light past the first bounce there twice the reference's. The field paints
  each cell the color of its one nearest triangle, but a cell on the corner's diagonal lies as far
  from the red wall as from the white one and holds whichever painted it, and a hit's color blended
  the eight cells around it, those among them; nor does a cell's distance to the hit's plane tell
  those cells apart, since it is the same to either wall. Mended by keeping, beside each cell's
  color, the way its face looks, the eight bits the field's build already keeps in each cell's uint,
  a byte a cell, about 0.5 MB a cascade with the still meshes' copy, and blending a hit's color from
  the cells whose face looks within 60 degrees of the hit's normal alone, or from all eight where
  none does. The red wall's hits within a cell of the white wall send on 0.007 to 0.008 of green to
  0.43 to 0.45 of red of the lamp's light where 0.050 to 0.105 to 0.44 to 0.47, the wall's own color
  0.012 to 0.58, and read 12 to 31% over the reference across their ways where 24 to 139% over, the
  nearest of them 0.31 of luminance where 0.56 against the reference's 0.25, the corner's screen
  probe 5% under the reference across its hemisphere where 5% over, and the corner 0.66 of the wall
  50 pixels in where 0.68 (`gi.screenprobe`, `gi.compare`, the pictures, the frame before, the frame
  now and the reference, at half the light). The Cornell box's frame reads 1.003 where 1.004 and the
  small block's front 1.40, the carried lamp's room as it did, the bounce rooms test's views at 160
  by 90 within 0.001 of their light, and the world's probes near the box's walls 0 to 4 points of
  their faces' light from where they were, their hits beside the walls taking those walls' own
  colors (`gi.probe`); global illumination costs 1.47 to 1.49 ms where 1.45 to 1.48 at `High` and
  1.15 where 1.16 at `Low`, the reflections' scene pass alike (`./e3d command profile`, the frames
  unlimited). What remains at the corner's hits is the light past the first bounce, their red 1.36
  to 1.40 where the reference across their ways holds 1.07 to 1.11. Of it, the hits send on 0.45 of
  red of the light straight from the lamp and the panel, where the reference holds 0.33 of the
  lamp's and, by the frame's own less the reference's of no bounce, about 0.12 of the panel's, and
  0.91 to 0.95 of the light past the first bounce, where the reference's, less the panel's, is 0.63
  to 0.65, so that term carries the whole excess (`gi.screenprobe` at no bounce, one bounce and
  every bounce). A hit takes it from the world's probes around it, the second cascade's at -1.2,
  3.6, -1.2 nearest, whose face along the red wall's normal reads 6.16 of red where its reference
  reads 5.81, and the first cascade's nearest the corner, at -1.8, 4.2, -0.6, 6.00 where 5.56, each
  face gathered mostly from rays that met a surface before their interval began and none of it from
  the screen's rays (`gi.probe`). Read at the hit, they give it about 5.0 of red where the light
  reaching the hit in the reference is about 3.5, the light falling by two fifths from where the
  probes stand to the corner 1.3 to 1.9 units on, which a blend of probes 2.4 apart does not draw.
  With the screen's probes left out, the red wall reads 1.13 of the reference 30 pixels from the
  corner and 1.25 at 10, the model pass reading the same probes there (`gi.compare`), and the
  world's probes' own rays that meet the back wall under the ceiling in the corner read 1.5 to 2
  times the reference's light there, the same term at their hits.
  ![The Cornell box's back corner, the frame before a hit's color was taken from the cells facing its way, the frame after, and the reference, at half the light](assets/cascades/cornell-corner.webp)

  Scaling the light past the first bounce at a hit by the ratio of the hit's openness to the
  openness where the probes it reads stand was measured on 2026-10-19 and is not kept. Each openness
  was read from the field's distances along five cones about the hit's normal, one along it and four
  leaning 45 degrees toward the sides, each at four steps doubling out to the probes' spacing, the
  nearest surface at each step against the cone's width there, 0.7 of the step, read in the finest
  cascade with the width held within its four exact cells, and the cones weighed by their cosine to
  the normal. Read in the cascade whose exact cells cover the width instead, a thin wall reads half
  a cell thicker and an open wall's leaning cones close by a sixth. On the screen's probes' hits
  alone, the ratio held at one at most, the corner probe's red-wall hits read 0 to 38% over the
  reference along their ways where 26 to 54%, and 6 to 32% with the cones twice as long, and the
  small block's front 1.32 of the reference's light where 1.40. The corner over 50 pixels in reads
  0.645 where 0.660, the reference 0.601, because the corner is not over in the frame, the back
  wall's first 12 pixels from it reading 1.00 to 1.02 of the reference and the wall 50 pixels in
  0.93, so the figure is the wall further in reading under, and the factor lowers both. The open
  middle moves, the Cornell box's back wall to 0.969 of the reference where 0.977 and the carried
  lamp's room's floor to 0.972 where 0.988, the upper red wall falls to 0.898 where 0.903 and the
  wall's foot (d) to 0.197 where 0.200.

  Two faults show in the hits' figures. A hit whose cones a near wall closes reads that wall as
  shade, though that wall lights it, as one on the tall block's side facing the red wall reads 0.06
  open against 0.77 where its probes stand and loses nine tenths of its light where it read 64%
  under the reference already, since the field's distances say how near a surface is and not how
  much light it sends. And the probes' openness read at the mean of their places reads as low as
  0.07 where that mean falls beside the tall block, and read at each probe, weighed as its light is,
  it leaves the frame's figures alike. Held at 1.5 at most, the ratio raises the open middle by 1.2%
  and the far wall by 4%. On the world's probes' hits as well, it brings the probe of the second
  cascade near the corner, the first cascade's nearest the corner and the pocket's to 2, 10 and 9%
  over their references merged where 9, 16 and 16%, but lowers the Cornell box's back wall to 0.947,
  the carried lamp's floor to 0.904 and the wall's foot to 0.177, so the frame's open middle reads
  near the reference because the world's probes' excess offsets what the screen's probes' sixteen
  ways miss. The ratio costs 0.45 to 0.50 ms of global illumination's 1.15 to 1.50 at `High` and
  `Low` in the Cornell box on the development machine's GPU, two readings of five cones at every hit
  (`gi.screenprobe` with each hit's openness logged, `gi.compare` at 800 by 450, `gi.probe`,
  `./e3d command profile`, the frames unlimited).
- **c. The small block's front in the Cornell box.** Its face toward the open side reads 0.207 where
  the reference reads 0.125, 65% over, and unevenly, its blocks of 16 pixels 1.46 to 1.83 of the
  reference's, lighter at the lower left and darker at the upper right where the reference's face is
  even. With the screen probes left out it reads 0.120, 4% under, so they put the excess on it.
  Their rays toward the open side meet nothing within their reach and take the world's first cascade
  along their way, and with the merge left out the face reads 17% over and without the light past
  the first bounce 35% under. Those rays took the world's probes around the point they left the face
  from, which saw along each ray's way what the ray passed by, and read 97% over the reference
  across the ways they stand for; read around where they end, 3% over (`gi.screenprobe`, and
  `gi_screen.slang`'s `beyond`). Two faults of the screen's march came with it, a ray still behind
  the depth at its end counted as meeting nothing and one that went off the picture handed to the
  field past a floor it had crossed, each reading the dark under the floor once the light beyond was
  read where the ray ended. Global illumination costs 1.29 ms where 1.10 in the Cornell box at 800
  by 450 on the development machine's GPU, and 0.96 where 0.97 in the carried lamp's room
  (`./e3d command profile`, the frames unlimited). The face stays 1.65 times the reference's light,
  since a ray that read none of the floor's light under it now meets the floor in front of the
  block, where the light past the first bounce reads twice the reference's. The world's
  first-cascade probe in the pocket between the floor and the block, which lights the floor there,
  reads 85% over its reference in its upward face even of one bounce, where the probes beside it
  read 4% (`gi.probe 0.6 0.6 1.8 256 1`). Most of that comes along one way of its sixty-four, whose
  ray meets the emissive panel's underside 0.79 from the lamp and brings 13.8 where the reference,
  its paths spread over the way's texel, reads 5.6, a part of the face of 2.10 against 0.85, since a
  world probe traces each texel along its middle every frame and so takes a spot under a lamp for
  the whole texel (`gi.probe`'s list of ways). Mended by sending four rays along each of the first
  cascade's texels, one from the middle of each quarter of it, and taking their light together, the
  probe's upward face reads 18% over the reference where 93%, its faces 7 to 24% over where 13 to
  93%, and the block's front 1.39 times the reference's light where 1.65, its blocks of 16 pixels
  1.34 to 1.48 (`gi.probe`, `gi.compare`, the pictures, the frame before, the frame now and the
  reference). With the screen's probes left out the front reads 0.76 where 0.96, the first cascade's
  errors there no longer offsetting each other. Global illumination costs 1.36 to 1.46 ms where 1.27
  in the Cornell box at 800 by 450 on the development machine's GPU and 1.09 to 1.15 where 0.96 in
  the carried lamp's room, the scene pass alike, and on lavapipe the Cornell box's frame takes 152.5
  ms where 150.5, its pass 100 where 87, while Ore's frame by night stays 249 ms
  (`./e3d command profile`, the frames unlimited). In the bounce rooms test the sun through a window
  reads 0.329 of its light from its reference where 0.293 at `High`, the four rays catching more of
  the sun's patch on the floor, which lifts the room's light by 3 to 9%, the floor nearer the
  reference, 15% under where 18%, and the walls facing along z, already over, further over, 22%
  where 13%, and the grazing floor reads 0.098 where 0.101. The first cascade at sixteen by sixteen
  texels, a ray each, read the face 27% over and the front 1.40, at 1.38 ms where 1.29. Keeping
  instead, for each texel, what its ray brought straight from the lights at each of sixteen places
  in the texel, one a frame, and their mean, read 15% and 1.41 at no ray's cost, but a change at a
  place the texel had not yet been seen at since it last took a new light went unseen, the emissive
  box's first-cascade probe reading 0.063 of its walls' light where 1.0 the frame they began to glow
  and then 64% over as its share rose, so those are left out, the way to the rest of the bias once
  the second cascade's term (b) is mended. Where the screen's probes stand in the second cascade,
  its probes 2.4 apart, no reading around the end of a ray 1.2 units long holds what the ray sees,
  which their rays going 2.4 mends (b).
  ![The small block's front, the frame before four rays a texel, the frame after them, and the reference](assets/cascades/cornell-small-block-front.webp)
- **d. A dark patch at the moved wall's foot.** In the carried lamp's room the floor at the green
  wall's right foot falls by 9% over 50 pixels, from 0.144 to 0.131, where the reference's rises by
  13%, from 0.156 to 0.176. It stays with the screen probes, the merge, the frame before and the
  glow each left out, so the world probes' blend on the floor draws it.
- **e. Patches across the far wall in that room.** The wall right of the moved one lies in blocks of
  16 pixels from 0.84 to 1.14 of their mean ratio to the reference, and from 0.85 to 1.12 with the
  screen probes left out, so the world probes' blend across the wall draws them too. Its light was
  0.84 of the reference's over the wall's middle, and with the light beyond the screen probes' rays
  read where they end (c) it is 1.00, the room's frame 0.989 of the reference's light where 0.944
  (`gi.compare`). The patches stay, the wall's blocks from 0.85 to 1.12 of their mean where 0.94 to
  1.12 in the same box before, since the rays now carry the world probes' blend from where they end
  where they carried one set of probes' light across the whole wall. Two rays of the wall's screen
  probe that pass the thin green wall's end within two of the march's steps behind the depth there
  were taken for meeting the face of it the eye sees, dark, where the reference meets the lit floor
  and wall beyond, and the probe read 51% under the reference along its ways (`gi.screenprobe`).
  Taken as met only where the depth does not leap between the ends of the step the ray crossed it
  in, they go on past the wall and the probe reads 2% over, and the wall reads 1.06 of the reference
  where 0.99, its blocks from 0.77 to 1.19 of their mean where 0.82 to 1.13, since its sixteen ways'
  reading over the wall's light is no longer covered by those rays' reading under it (above); the
  room's frame reads 0.989 where 0.978 and the floor at the moved wall's foot 0.201 where 0.187, the
  reference 0.213. The Cornell box reads as it did, the small block's front 1.39 and the corner
  0.68, and the bounce rooms test's views at 160 by 90 move by 0.008 of their light at most, the
  thin walls' by 0.002 and the strip's by 0.002 nearer, at no cost in the illumination pass or the
  reflections' worth measuring, 0.01 ms (`gi.compare`, `./e3d command profile`; the pictures, the
  frame before, the frame now and the reference).
  ![The carried lamp's room, the frame before the screen's march judged an edge by the depth's leap, the frame after it, and the reference](assets/cascades/room-wall-foot-and-far-wall.webp)
- **f. The lamp outside through the thin walls' corner.** Where the two thin walls meet, a tick a
  few pixels long reads 3.25 times the wall beside it, where the reference reads 1.10, and with
  global illumination off the room is dark and the tick gone. With the screen probes left out a
  bright line runs the corner's length and along the walls' feet, 3.35 times the wall at the tick,
  so the world probes there take the lamp's light from outside through the walls, and the screen
  probes hide it but where a tile does not hold the corner's pixels. The tick lies where the right
  wall meets the front wall at the lamp's height, a pixel no screen probe on a surface like its own
  stands near, which read the world's probes as `bounced` does, and the point their reach judges it
  from lies in the front wall's cells, so a probe beyond that wall reads as seen. Mended by reading
  such a pixel's world probes through the field (`bouncedSeen` in `lights.slang`), the tick reads
  1.02 of the wall beside it, faded across a cascade's outer band as `bounced` fades, at a cost to
  the model pass of 0.07 ms in that room at 800 by 450, 0.31 where 0.24, from 185 pixels' groups,
  and 0.09 in the Cornell box, 0.27 where 0.18. Where the world's probes alone light the corner,
  past the screen's probes' reach, their band still reads 1.41 where the reference reads 0.96, read
  from those probes, whose own rays take none of the lamp's light (the reach's slack, below; the
  pictures, the frame before, the frame now, the frame now with the screen's probes left out, and
  the reference).
  ![The thin walls' corner, the frame before the mend, the frame after it, the frame after it without the screen probes, and the reference](assets/cascades/thin-walls-corner-leak.webp)

**A value that is not a number at the screen's probes, read on 2026-10-24.** The voxel game's
desert village of seed 1, seen at a field of view of 40 degrees with its 56 torches carried as
glow lights, drew 215,459 of its pixels black at `High`, 596,201 six hundred frames on and 320 with
the screen's probes' history left out. The cause is one. A surface's normal at a screen probe and
at a glow texel is the cross of its steps along the two axes of the window's depth, each step from
the texel's point to its neighbor's, and far from the world's origin, at a far texel, the
neighbor's point read back from the depth is the same point as the texel's, so a step is exactly
zero, the cross has no length and the normal made from it is a value that is not a number. It came
in three places, the glow pass's normal at each of its texels and the screen pass's normal at each
probe and its glow lights' normal, the last two taking the shorter of the steps on either side and
so the zero one. Every light worked out from such a normal is a value that is not a number, which
the glow pass's blend of the probes around a texel takes into its neighbors' light and its frame
before's carries into the next frame, so frame by frame it spreads into every texel around until
the land is black; in a copy of the game's build with the glow pass marked where its normal was
one, 171 of the black pixels were those texels, and with the three normals mended none is black.
Where the cross has no length or none a float holds, the normal faces the eye (`surfaceNormal` in
`gi.slang`), and the view draws no pixel black, after 240 frames, after 840 and at a field of view
of 70, where 303. An engine scene built to it, sand, houses and 56 torches under a low sun seen at
40 degrees, draws 26,258 pixels black at 640 by 360 drawn 10,000 units from the origin where 6,570
at it, the houses' shadows, and 6,917 with the normal facing the eye (`GlobalIlluminationTests`).
The window's blend of the probes, the frame before's light at each and the glow pass's frame
before take a light a float does not hold as none and write one as none (`lightHeld` in
`gi_screen_filter.slang`), so a fault anywhere that gives one darkens a probe for a frame where it
had spread to every probe around and every frame after. A probe a test feeds one for three frames
leaves 6,132 pixels black after it as before it, where without the guard 6,751.

**The screen's probes ray by ray, read on 2026-10-19.** `gi.screenprobe <x> <y>` reads the window's
screen probe under a pixel as the screen's pass wrote it for the frame, into a buffer written for
that probe alone, each of its sixteen rays where it was met, on the screen, in the field or past its
reach in the world's cascade, with its light and the light of what it met without the indirect
light, against the light a path-traced reference sends along the same way from where the rays start
and over the ways of the hemisphere the ray stands for, its cell. A probe's rays turn with its
normal alone, so it settles on its sixteen ways' light, and the reference along them and over their
cells tells a fault of the rays' reads from one of their ways. Inside a closed box of emissive walls
each ray reads within 13% of the reference and the probe 5% under it (`BounceReferenceTests`). In
the Cornell box 300 frames on, the probe on the small block's front reads 43% over the reference
along its ways, 52% over it across their cells and 64% across the hemisphere. Its eleven rays past
their reach read 97% over their cells, one toward the box's upper left 430%, and the four meeting
the floor in front of the block 72 to 100% over theirs, the light past the first bounce at the three
nearest it 0.80 to 0.86 of red where the reference's is about 0.31 to 0.49. One more meets the floor
0.17 under its top and reads none of its light, since the field's march picks up where the screen's
lost the ray, inside the slab. The probe on the back wall 0.13 from the red wall stands in the
field's second cascade, its cells 0.3 and its rays reaching 1.2, and reads 112% over its cells. Its
rays past their reach read 116% over theirs and those meeting the red wall 88%, the three within 0.3
of the back wall taking 0.10 of green where the reference's direct light holds 0.006, the field's
color there blending the white wall into the red. A ray past its reach took the world's probes
around the point it left the surface from, not where it ends, so a probe 1.2 or 2.4 off saw along
the ray's way what the ray itself passed by, which (c) mends. A probe's sixteen ways also read
what the hemisphere holds unevenly, the reference along them 22% over its whole on the block's
front, 26% under on the back wall, 21% under on the carried lamp's room's floor before the moved
wall and 46% over on its far wall, and the window's filter blends tiles of the same ways.

**The screen probes' sixteen ways, read on 2026-10-19.** A screen probe's ways are set by its normal
alone, so every probe on a floor sends the same sixteen and the window's filter blends like with
like. Over fourteen probes in the Cornell box and the carried lamp's room at 300 frames, the
reference along a probe's ways lies from 39% under to 149% over the reference across its hemisphere,
the Cornell box's floor furthest, and the probe's own light from 33% under to 46% over, its rays'
reads erring as often the other way, since the light from beyond a ray is a texel's and not its
way's (`gi.screenprobe`). Four mends were measured and none kept, by the small block's front, the
back corner against the wall 50 pixels in and the whole frame in the Cornell box, 1.39, 0.68 and
1.005 of the reference before, and by the far wall's patches, its level and the whole frame in the
carried lamp's room, 0.82 to 1.13, 0.99 and 0.978 before (`gi.compare` at 800 by 450). The ways
turned each frame among sixteen places in their texels, each tile a step of its own, read 1.21, 0.67
and 1.016, and 0.90 to 1.10, 0.89 and 0.968, but the 99th part of a still view's pixels changed by
1.7 to 4.4% of its mean light from one frame to the next and some by 10 to 31%, where with the ways
still they change by 0.04% at most. Turned for each tile and held, sixteen places laid so that every
four tiles by four hold each once, they read 1.20, 0.64 and 1.016, and 0.89 to 1.07, 0.90 and 0.968,
a still view as calm as before, but at the 160 by 90 of the bounce rooms test the views moved from
their references, the thin walls' by 0.020 to 0.029 of their light, the strip's by 0.011 to 0.039,
the grazing floor's by up to 0.023 and the window's by up to 0.021, five of the test's bounds passed
by 0.001 to 0.024, and the wick's reference frame differed in 3.2% of its pixels where 2% are
allowed; four places laid two by two read 1.22 and 0.67, and 0.91 to 1.09 and 0.90, and moved the
strip's view by 0.110 and the thin walls' by up to 0.039. Thirty-six ways where sixteen read 1.22,
0.66 and 1.012, and 0.93 to 1.05, 0.92 and 0.973, as calm, at a cost to global illumination of 2.43
ms where 1.43 in the Cornell box and 1.70 where 1.09 in the carried lamp's room on the development
machine's GPU (`./e3d command profile`, the frames unlimited). Under every mend the far wall's level
falls to 0.89 to 0.92 of the reference, the sixteen ways' reading over it having covered its rays'
reading under it, among them rays that pass behind the thin green wall near its end within the
depth's thickness, which the screen's march counts as meeting the face the eye sees, dark, where the
reference meets the lit floor and wall beyond, a fault of the march's thickness, mended since (e).
Measured again on the same day with the screen's hits behind thin walls (e) and the corner's color
(b) mended, against references of 2048 paths a pixel at 800 by 450 and the bounce rooms test's at
160 by 90, each view's distance to its reference taken as the test takes it, its pixels' differences
summed over the reference's light, over blocks of 4 pixels at 800 by 450, both candidates widen four
views at both sizes. The thin walls' widens by 0.022 to 0.032 at 160 by 90 and 0.024 to 0.025 at 800
by 450, the strip's by 0.009 to 0.070 and 0.011 to 0.017, the grazing floor's by 0.012 to 0.030 at
`High` and `Medium` and 0.012 to 0.019 at `High`, and the window's by up to 0.021 and 0.009 to
0.020. At 800 by 450 they narrow the Cornell box's by 0.006 to 0.014, the corridor's by 0.006 to
0.011 and the carried lamp's room's by 0.015 to 0.018. At `High` the far wall reads 0.93 and 0.94 of
the reference where 1.06, its blocks of 16 pixels 0.87 to 1.07 and 0.94 to 1.04 of their mean where
0.77 to 1.19, the wall's foot (d) 0.208 and 0.210 where 0.200 against 0.215, and the small block's
front 1.20 and 1.22 where 1.40, and at `Low` the wall's foot reads 0.179 with the ways turned for
each tile where 0.192. Where a view widens, walls that read over the reference with the fixed ways
read further over, the thin room's walls 8 to 9% over where 5%, the strip's side walls 22% where
13%, the grazing floor's walls 19 to 25% where 3 to 10% while its floor reads 3 to 4% over where
10%, and the window's ceiling 23 to 26% where 0%, so the sixteen ways' reading under on those walls
covers the excess of the world's probes' own hits (b), and neither candidate is kept while that
excess stands. The thirty-six ways cost 2.47 to 2.48 ms of global illumination where 1.45 at `High`
and 2.09 to 2.11 where 1.11 to 1.17 at `Low` in the Cornell box (`gi.compare`, BounceRoomsTests with
its bounds at zero, `./e3d command profile`, the frames unlimited).

**The screen probes' ways placed by tile, read on 2026-10-20.** With (j) and (h) mended, against the
same references, the ways turned for each tile and held, as above, and the thirty-six ways widen the
same three views at both sizes and agree within a point or two on their regions. At 160 by 90 at
`Low`, `Medium` and `High`, the ways placed by tile widen the thin walls' view by 0.013, 0.019 and
0.022, the window's by 0.020, 0.006 and 0.018 and the grazing floor's by 0.003, 0.011 and 0.028, and
narrow the strip's by 0.018, 0.005 and 0.026 and the carried lamp's room's by up to 0.014; at 800 by
450 at `High` and `Low` they widen the thin walls' by 0.025 and 0.023, the window's by 0.006 and
0.012 and the grazing floor's by 0.015 and 0.009, and narrow the Cornell box's by 0.016 and 0.014,
the strip's by 0.020 and 0.013, the carried lamp's room's by 0.029 and 0.024 and the corridor's by
0.011 and 0.007. Split over 28 screen probes in the window's room and 24 on the voxel game's birch
trunk at noon (`gi.screenprobe`), the light the rays bring reads 26 and 33% over the reference along
the same ways from the texels' middles and 28 and 35% from the places, and the reference along the
ways reads the hemisphere's 15 and 41% under from the middles and 2 and 5% under from the places, so
the probes read the hemisphere 12 and 16% over where 0 and 25% under. The sixteen fixed ways'
reading under had covered the rays' reading over, which is two terms of the method. In the window's
room the world's probes read the reference 10 to 28% over across their ways merged with the cascades
above where 9% under to 24% over by their own rays, a way past the first cascade's interval taking
the light a coarser cascade's probe sees along it from where it stands, the sunlit floor or the
window, 0.27 to 0.42 where the reference along it reads 0.015 to 0.26, the merge's parallax; in the
thin room they read 9 to 12% over by their own rays, no way taking the cascade above, the gain past
the first bounce, since they read the same with the lamp outside switched off (the reach's slack,
below). By the angle from the normal, the rays near the surface's plane read their
texels' reference within 1% in the window's room, so neither the screen's march nor the field reads
a grazing way over. On the trunk the patches go, its blocks of 16 pixels reading 1.02 to 1.31 of the
reference, mean 1.17, where 0.58 to 0.97, mean 0.76, and the thirty-six 1.13 to 1.76, mean 1.41. The
ways are kept placed by tile under the covered-fault principle, the two terms named, the grazing
floor's bound in the bounce rooms test taken to 0.09 and the thin wall's end's and the small block's
probes pinned anew (BounceRoomsTests). A still view changes by 0.04% at most in its 99th part from
one frame to the next, where 0.03%, and global illumination costs nothing more, the same sixteen
rays.

**The merge's parallax, read on 2026-10-20.** A probe whose ray met nothing within its interval
takes the light along that way of the eight probes of the cascade above around it, each of which
sees from where it stands what lies along the way, so a probe above standing elsewhere lent the
light of a surface the ray itself would not meet. Each probe above is read toward the point of the
ray nearest where its own ray along the way met a surface, by the reach the gather writes for each
of its eight by eight ways, and along the way itself where its ray met nothing (`gi_merge.slang`).
At the Cornell box's pocket the first cascade's probe's upward face takes from the cascade above
1.61 times the reference's light where 2.71 (BounceRoomsTests) and its face toward −x 1.29 where
2.63, at no cost worth measuring, global illumination within 0.03 ms of what it was at both
qualities in the Cornell box and within 0.04 in the window's room. The bounce rooms' views narrow at
both sizes, the window's by 0.013 to 0.019 at 160 by 90 and 0.015 to 0.025 at 800 by 450, the
grazing floor's by 0.008 to 0.015 and 0.008 to 0.013, the strip's by 0.005 to 0.009 and 0.007 to
0.016 and the carried lamp's room's by up to 0.005, but the thin walls', which widen by 0.004 to
0.005 and 0.003 to 0.005, their walls thinner than a cell reading a point further over at `Low`.
Marching instead from the ray's point twice the spacing out to each probe above's as far out, a
texel's own sight of each in place of the probe's, the bilinear fix, took the pocket's upward face
to 2.16 at 0.24 to 0.29 ms more at `High`. The small block's front (c) reads 1.20 and 1.10 of the
reference at `High` and `Low` where 1.18 and 1.13.

**The reach's slack beside thin walls, read on 2026-10-20.** Nine of the thin room's world probes,
beside its right wall, at the wall's foot and corners, in its middle and by its far wall, take none
of the light of the lamp outside that wall. One beside the wall reads 0.267 by its own rays where a
path-traced reference reads 0.242, and the same with that lamp switched off (`entity.set 4
Light.Intensity 0`), as the reference does, their 10% over being the gain past the first bounce
(`gi.probe`). The frame takes it on the wall's inner face, 1.153 of the reference where 1.072 with
the lamp off, the reference reading the wall alike either way, and in blocks of 20 pixels 50 to 62%
of the reference's light along the wall's foot, 20 to 30% along the ceiling, 15 to 22% at the front
wall's corner and 1 to 2% in its middle (`gi.compare` at 800 by 450). The screen's probes' hits
within 0.4 of the wall take it past the first bounce, the rays of a probe by the corner reading
0.338 where 0.206 with the lamp off against the reference's 0.183, their own light alike
(`gi.screenprobe`), since they read the world's probes through `bouncedAt`, which weighs each by its
reach with a slack, and over half the spacing a probe 0.55 beyond the wall, its rays reaching the
wall's far face, read as seeing them. Over an eighth of the spacing, a cell of the probes' cascade,
the wall reads 1.098, its foot 1.050 times its light with the lamp off where 1.279
(BounceRoomsTests), and the thin walls' view narrows by 0.012 to 0.017 at both sizes. Of the wall's
2.6 points left, the twentieth of a weight `bouncedAt` keeps for a probe it does not see carries
0.8. With the screen's probes left out, the corner by the front wall reads 3.53 times the wall
beside it where 1.01 with the lamp off, and 2.85 over an eighth, the twentieth cut to nothing taking
it to 2.81, so the model pass's read of the probes there lends it the lamp's light by a further way.
The corridor's widens by 0.011 to 0.017, its walls 78 to 79% under the reference where 67 to 70%,
since the sunlit probes beyond its walls 0.3 thick lent them light that covered part of their own
reading under, a term of its own. The Cornell box's back wall reads 1.043 where 1.033 and the
grazing floor's wall facing −x 1.122 where 1.094, the dark probes behind those walls having covered
part of the gain past the first bounce. It costs nothing, global illumination 1.94 ms at `High` in
the Cornell box where 1.94 to 1.99 on the development machine's GPU, and the reflections move by
0.01% of their light in `shaders_reflections` and 0.09% in `models_reflection_probe` (`./e3d command
profile`, the frames unlimited). Marching from the screen's probes' hits to the probes they see
(`bouncedSeenAt`) read the wall 1.085, widened the corridor's view by 0.028 to 0.040 and cost 0.25
to 0.42 ms more.

**The corridor's far light, read on 2026-10-20.** The corridor, sixteen units long, 2 wide in walls
0.3 thick and open at its −x end where the sun comes in, holds no world probe of the second cascade
or above. The second cascade's spots there, 2.4 apart, fall at z ±1.2 inside its walls, and each is
moved out through the face nearer it to z ±1.54, outside, where the sun lights it, and the third
cascade's stand outside too. The merge, which marches to the probes above, sees none of them through
the walls, so the first cascade's probes along the corridor take nothing past their interval and
read nothing at any bounce, where a path-traced reference reads 0.011 to 0.027, and a screen probe
on its wall 13 units in reads nothing where the reference reads 0.008 along its ways (`gi.probe`,
`gi.screenprobe`). Its walls read 78 to 79% under the reference, its ceiling 69% and its floor 66%
(`gi.compare` at 800 by 450). A screen probe's ray past its reach whose world probes hold nothing
along its way goes on through the field, as far as four times its reach and within the cascade it
starts in and the next coarser one, and takes the light of what it meets, and the walls read 67 to
69% under, the ceiling 55% and the floor 55%, the corridor's view narrowing by 0.017 to 0.023 at
both sizes and every other view holding to the third digit. It costs 0.21 to 0.25 ms in the
corridor, where its rays march, nothing in the strip's room or the emissive block's, 0.01 in the
Cornell box and 0.04 in the window's room at `High`, and in Ore 0.00 to 0.13 ms by day and by night
alike at `High` and `Low`, where marched on to the field's edge it cost 0.13 to 0.19 for walls 66 to
68% under and a ceiling 52% (`./e3d command profile`, the frames unlimited). Marched into the
coarser cascades, it passed walls 1 thick in cells of 2, and a closed room at the field's side
behind the eye took the sky's light, its pillar's face 0.029 of an open floor's light where it holds
under 0.002 (CascadeEdgeTests). A probe whose spot lies in a wall moved through its farther face
where no other probe covers that side brought the second cascade's probes at two of the four spots
along the corridor inside, which read nothing, their ways along it reaching the third cascade
outside, and widened the view by 0.002 to 0.006. A world probe's ray traced on where its probe sees
no probe above narrowed it by 0.004 to 0.005 at 0.02 to 0.06 ms in every room, and beside the
screen's march it lit the probes a little and so stopped the march. What stays under is the light
past the first bounce at probes that hold nothing.

**The light past the first bounce at the world probes' own hits, read on 2026-10-19.** Seventeen
faces of eleven world probes were read against the reference by bounce and by where their ways were
met, the faces the walls take their light from beside the walls the sampling term's candidates lit
further over and near the Cornell box's walls (`gi.probe`, each face by where its ways were met). A
probe's first bounce, the light its hits send on of what reaches them straight from the lights, is
its light with `gi.toggle again off` against the reference of no bounce past the probe's hits, which
lights each hit as the lamps light it, and the light past the first bounce is the rest of each. The
Cornell box's panel, carried as glow lights, lights a hit as a light of the engine's own and in the
reference's second segment, so its light was read alone, the lamp switched off
(`entity.set 1 Light.Intensity 0`), and the lamp's taken as the whole less the panel's. In every
room the light past the first bounce reads over the reference. The lamp's light at the Cornell box's
two probes near the back corner reads 2 to 8% under at the first bounce on the faces the red wall,
the back wall and the ceiling take, but 17% under on the second cascade's downward face, and 20 to
39% over past it; the thin room's wall reads 0 to 2% over at first and 14 to 15% past it, the
window's ceiling 8 and 31% and 26 to 30%, and the grazing floor's walls, read in the second cascade,
whose probes stand 0.24 from them, 21% under and 21 to 23% over. So the excess grows with the bounce
count, a gain at each bounce through the probes' own hits, which the reference's light past the
first bounce, split into its second bounce and the rest, puts at 1.08 to 1.09 a bounce in the thin
room, 1.12 to 1.13 on the grazing floor's walls, 1.14 to 1.16 under the window's ceiling and 1.12 to
1.27 in the Cornell box, where the openness ratio on the world probes' hits (b) brought the three
probes from 9, 16 and 16% over to 2, 10 and 9%. The excess past the first bounce comes along the
ways that met a surface, within their interval or before it began, 74 to 100% of the faces' weight,
the second cascade's probe in the Cornell box 27 to 43% over through those met before their interval
and the first cascade's 24 to 40% through those within, while the ways that take the cascade above
read under in the Cornell box and 8 to 27% over on the grazing floor's walls. The panel's light at
those two probes reads from 18% under to 6% over at the first bounce and 5 to 12% over past it. Two
places carry an excess at the first bounce instead, and through the cascade above. The pocket's
probe reads 3 to 19% over at first and 16 to 22% past it, its ways that take the cascade above 78 to
202% over at first and 67 to 131% past it, and the strip's room's two probes 204 to 209% over at
first and 23 to 31% past it, their ways in the cascade above 483 to 2521% over at first where their
own hits read 14 to 57%. There the excess stands at the first bounce, in the probes' own gathering
through the merge (`gi.probe` with 256 paths a way at no bounce, one and every bounce,
`gi.toggle again off`).

**The gain's cause and three mends of it, read on 2026-10-19.** A hit's read of the probes around it
was recomputed from their faces with `gi.toggle again off`, the blend `bouncedSeenSum` takes without
its march, against the reference's irradiance from one bounce traced from the hit's own point and
from each probe's place, weighed as the read weighs them, the Cornell box drawn without its panel so
its lamp alone lights it. Of the three causes a gain could have, the constant that turns the faces
into a hit's light holds, a white floor under a sky of 1 reading 1.00 of the reference and the probe
over it 1 to 2% over on its faces. Nor is it the wall's own light read back, the probes' faces
reading the reference at their own places within 3% under and 7% over on the red wall and the white
ones alike, and the red wall's excess the smaller. It is the gradient, the probes standing where 11
to 64% more light arrives for the hit's normal than at the hit, 11% at the thin room's wall, whose
hits read the first cascade's probes 0.7 to 1.2 off, and 15 to 61% at the Cornell box's back wall
and 2 to 24% at its red wall, whose hits read the second cascade's, its planes 1.1 behind the back
wall and 1.3 before it, and 61 to 64% at the grazing floor's walls. 0.3 off the wall, where the same
probes are read, the excess falls, the thin wall's from 13% to none and the back wall's corner's
from 56 to 30%. Three mends were measured and none kept, by the rule that no view's distance to its
reference widens at 800 by 450 or at 160 by 90 (`gi.compare`, BounceRoomsTests with its bounds at
zero). Probes whose spot lies within six tenths of the spacing behind the surface nearest them, with
the eye across its plane, moved through it along its normal to stand before it, the blends weighing
each probe by a tent about where it stands, cross as meant, those behind the Cornell box's walls and
ceiling standing 0.2 to 0.3 before them, and a still view keeps calm, but the grazing floor's view
widens by 0.017 to 0.034 and three others by 0.003 to 0.008, the gain at the faces barely falls, the
Cornell box's from 1.14 to 1.27 a bounce to 1.09 to 1.19, and the corner's hits read worse, 60 and
70% over where 22 and 56%, since a concave corner's light rises steeply within 0.3 of its walls,
where the probes come to stand. Reading the probes with no lean (`Lean`) takes the gain to 1.03 to
1.08 a bounce in the thin room, under the window's ceiling and in the strip's room, 1.00 on the
grazing floor's walls and 1.05 to 1.17 in the Cornell box, the pocket's alone unchanged at 1.12 to
1.13, the window's view narrowing by 0.012 to 0.066, the strip's by 0.033 to 0.045 and the wall's
foot (d) reading 0.219 where 0.200 against 0.215, but the grazing floor's view widens at `Low` by
0.074 and 0.082. Its far wall lies in the outer band of `Low`'s last cascade, and `probesHold`,
which blends the probes toward the ambient light past that cascade, takes its point by the same
lean, so with none the wall took a closed room's absent ambient light, 46% under where none with the
screen's probes left out. With `probesHold` keeping a lean of half the spacing, the view still
widens at `Low` by 0.048 and 0.055 and at `Medium` by 0.032, its room reading 15% under where 9%,
its floor 17% where 10%, while at `High` it reads 2% under where 8% over. So at `Low` the gain
covered that room's own shortfall, and taking it out leaves the shortfall standing (`gi.probe` and
`gi.screenprobe` with 256 paths a way, `gi.compare`, BounceRoomsTests).

**`Low`'s first bounce in the grazing floor's room, read and mended on 2026-10-19.** The grazing
floor's room reads 9% under its reference at `Low` and 8% over at `High`, where the Cornell box, the
thin room and the carried lamp's room read alike at both, within a point. It lies within reach of
`Low`'s last cascade, whose rays from 2.4 to 9.6 met a surface in all 64 ways of each probe read
there, the floor and the ceiling three units apart meeting even the long ones, and with
`probesHold`, its blend toward the ambient light, held at 1, the room reads 8% under where 9%. With
`gi.toggle again off`, against a reference of one bounce, the room reads 5% under at `Low` and 1%
over at `High`, the light past the first bounce giving the rest of the difference, near the
reference at `Low`'s world probes and 12 to 37% over at `High`'s (the gain, above). Read at one
bounce under four pixels of the room, the screen's probes read the light beyond their reach from 44%
under the reference to 9% over along their ways at `Low` where `High`'s read 23% under to 83% over,
while their hits in the field and on the screen read within 3% at both. The tile is not it, `High`
at 16 pixels and `Low` at 8 moving the frame at one bounce by a point at most, nor a third cascade,
which at 8 texels a side leaves `Low` as it reads. `Low`'s second cascade, of 8 texels a side with
one ray through each, is, since with 16 a side `Low` reads as `High` does, the light beyond reach
33% under to 80% over and the frame at one bounce 1% under. One ray through a texel a sixty-fourth
of the sphere wide takes a bright spot or misses it for the whole texel, which the first cascade's
four rays a texel mended (c). Mended by sending four rays a texel in any cascade of 8 a side, so
every probe sends 256 (`RaysAt`), the room reads as its reference at one bounce at `Low`. `High` is
unmoved to the last digit of every view at both sizes. At `Low` the grazing floor's view narrows by
0.018 at 160 by 90 and 0.037 at 800 by 450 and the Cornell box's by 0.003 to 0.004, the thin room's
and the carried lamp's moving by 0.002 at most, and the views it widens widen to `High`'s own
figures for their rooms, the window's to 0.356 at `Low` and 0.340 at `Medium` where `High`'s is
0.324 at 160 by 90 and to 0.303 where `High`'s is 0.311 at 800 by 450, its walls 21 and 16% over at
`Low` where 6 and 8%, `High`'s 22 and 19%, the strip's to 0.266 and 0.255 where `High`'s is 0.250,
and the grazing floor's at `Medium` to 0.105 where `High`'s is 0.099, so the one ray a texel covered
at `Low` the walls the gain lifts at `High`, and the bounce rooms test holds those views at the new
figures. Global illumination costs 1.18 to 1.20 ms where 1.13 to 1.14 at `Low` and 1.34 to 1.40
where 1.30 to 1.31 at `Medium` in the Cornell box on the development machine's GPU (`gi.screenprobe`
with 256 paths a way at no bounce past the hits, `gi.compare` at 800 by 450, BounceRoomsTests,
`./e3d command profile`, the frames unlimited).

**The reads at no lean measured again, on 2026-10-19.** With the second cascade of `Low` and
`Medium` sending four rays a texel, reading the probes with no lean, `probesHold` keeping a lean of
half the spacing as its own, narrows the window's view by 0.053 to 0.055 at every quality at 160 by
90 and by 0.066 at 800 by 450, and the strip's by 0.041 to 0.050, at `Low` and `Medium` as at
`High`, takes the wall's foot (d) to 0.214 at `Low` and 0.219 at `High` where 0.195 and 0.200,
against 0.215, and the gain at the faces to 1.00 to 1.17 a bounce where 1.08 to 1.27, the pocket's
unchanged. A still view keeps calm, its 99th part changing by 0.03% at most from frame to frame, and
when the field stamps the carried lamp's room's moved wall, about 380 frames after it moves, the
room steps to its settled light in one frame and keeps it, as it does without the change. But the
grazing floor's view widens at `Low` by 0.044 at 160 by 90 and 0.069 at 800 by 450, its room 12%
under where 4%, and the carried lamp's room's by 0.002 at 800 by 450, while the grazing floor's
narrows at `Medium` by 0.030 and at `High` by 0.044 to 0.045, its room 2% under where 8% over. At
the room's walls the probes stand alike at both qualities, the plane nearest the far wall 0.24 off
it and the next 2.4, the reference at their places 61 to 64% brighter than at the wall, and with no
lean a hit there reads the near probe. At `High` that probe holds the reference at its place within
1% and the hit reads 6 to 14% under; at `Low` it holds it 7 to 15% under, its face toward the room
35% under at the first bounce where `High`'s is 21%, since that face looks 11.8 across the room and
its near-level rays end at the last cascade's reach of 9.6 with nothing past them at `Low`, where
`High`'s third cascade lends that light, and the hit reads 23 to 27% under. So the reads keep their
lean, and the widening names the light beyond `Low`'s last cascade at a probe's own face
(`gi.compare`, BounceRoomsTests, `gi.probe` with 256 paths a way at no bounce, one and every
bounce).

**The last cascade's rays to the field's edge, on 2026-10-19.** The last cascade's interval ends at
the field's edge, the diagonal of its coarsest cascade, where it ended at four times its spacing, so
a ray across a room wider than that goes on until it meets a surface or leaves the field, and takes
the sky only where it leaves. In a closed room sixteen units long at `Low` the last cascade's probes
sent the rays of 16 of 1,536 texels into mid-air, which took the sky, and send none
(CascadeEdgeTests). In the grazing floor's room at `Low` the probe at the far wall reads its face
toward the room 22% under the reference at the first bounce where 35%, `High`'s 21%, and the three
probes there read the light past the first bounce 11 to 36% over where from 11% under to 30% over,
as `Medium`'s and `High`'s read it, 7 to 37% over. The room's frame reads 7% over where 4% under,
`High`'s 8%, and its view 0.104 of its light from its reference at 160 by 90 where 0.074, `High`'s
0.099 and `Medium`'s 0.105, and 0.086 at 800 by 450 where 0.053, `High`'s 0.088, so `Low` reads the
room as `Medium` and `High` do, the gain uncovered there as in the window's and the strip's rooms
(above), and the bounce rooms test holds the view at the new figure. Every other view at `Low`, and
every view at `Medium` and `High`, reads as it did to the last digit. Global illumination costs 1.24
to 1.26 ms where 1.16 to 1.17 in the Cornell box, 0.98 to 1.03 where 0.90 in the carried lamp's room
and 2.63 where 2.48 to 2.52 in Ore's landscape at `Low`, 1.36 where 1.33 to 1.34, 1.00 where 0.97
and 2.96 to 2.99 where 2.82 to 2.84 at `Medium`, and 1.47 to 1.48 where 1.44, 1.12 where 1.09 and
3.05 to 3.10 where 3.04 to 3.11 at `High`, the rays that leave a room going on to the field's edge
(`gi.probe`, `gi.compare`, BounceRoomsTests, `./e3d command profile`, the frames unlimited).

**The probes read with no lean, on 2026-10-19.** A hit and a pixel read the probes around them at
the surface itself, where they read them half a spacing off it along the normal (`Lean`), which kept
a surface in a plane of probes from reading the probes inside it before such probes were moved into
the room, and read the probes before a wall standing where 61 to 64% more light arrives than at the
wall, which every bounce took on (the gain, above). The blend toward the light past the probes
(`probesHold`) keeps its lean. With the last cascade's rays reaching the field's edge, the grazing
floor's view reads 0.075 at `Low` and `Medium` and 0.055 at `High` at 160 by 90 where 0.104, 0.105
and 0.099, and 0.046 and 0.043 at `Low` and `High` at 800 by 450 where 0.086 and 0.088; the window's
0.301, 0.287 and 0.270 where 0.356, 0.340 and 0.324, and at 800 by 450 0.236 and 0.245 where 0.303
and 0.311; the strip's 0.209 to 0.216 where 0.250 to 0.266, and 0.104 and 0.105 where 0.150 and
0.149; and the thin room's and the corridor's by up to 0.007 nearer. The gain at the faces is 1.00
to 1.17 a bounce where 1.08 to 1.27, the pocket's unchanged at 1.12 to 1.13, and the wall's foot (d)
reads 0.214 at `Low` and 0.219 at `High` where 0.195 and 0.200, against 0.215. The carried lamp's
room reads 0.002 to 0.003 nearer its reference at 160 by 90 and 0.002 further at 800 by 450 at both
qualities, 0.071 where 0.069 at `High`, its red wall's pixels 0.095 from the reference's in red
where 0.111 and its wall facing -z 0.039 where 0.067 coming nearer, and its floor 9% under where 7%
and its far wall further over, its level 1.075 of the reference's where 1.061, which is the sampling
term's, the sixteen ways' reading over the wall that the lower read had offset. The measure repeats
exactly, the head's frames at 800 by 450 read twice in one run alike to the last bit, so a
difference of 0.002 is the change's. A still view keeps calm, its 99th part changing by 0.04% at
most from frame to frame, and when the field stamps the carried lamp's room's moved wall the room
comes to its settled light over two frames, 18.8% and then 3.8% of its mean light at the 99th part,
where in one, 17.9% and then 0.3% (`gi.compare`, BounceRoomsTests, `gi.probe`).

**The strip's room, read on 2026-10-19.** The two world probes beside the strip read their faces 204
to 209% over the reference at the first bounce, nearly all of it one texel's own rays and none of it
the cascade above's. A probe's ray passes through the strip, 0.06 thick, which the field holds about
its middle, and meets the wall 0.02 behind it, which the strip lights from no distance, where a
path-traced reference's path stops at the strip and leaves its emission out, the strip reaching
surfaces as a light of its own (`GlowLights`). The Cornell box's pocket carries an excess at the
first bounce of another kind, the merge's parallax, the probe above that carries most of one way's
weight standing 1.28 off and meeting the ceiling 1.6 from the lamp, where the pocket's own way meets
it 2.7 from the lamp and a quarter as bright. A hit given none of a light whose box its ray passed
through within a cell before it, a slab test cast back along the ray (`glowLightPassed`), takes the
strip's probes to 5 and 9% over at the first bounce and leaves the strip's room 14% under its
reference, where it read level. The light the strip gives a hit reads 28% under at the floor, 30% at
the ceiling and 38 to 39% directly above and below the strip, against the strip's light in closed
form, Lambert's over its six faces. The march that tells how much of a glow light a hit sees
(`glowLightSeen`) runs from the hit to the box's middle against a cone as wide as the box's mean
half side, 0.35 for the strip, and from above or below the strip it runs along the wall the strip is
mounted on within a cell, the wall inside the cone taken for something between. A cone as wide as
the box across the way toward the surface each step passes, its support along the field's normal
there, 0.03 toward the strip's wall and 0.5 for a cube toward a face, takes the floor to the closed
form, the ceiling to 10% under and the side walls to 10 and 21%, a few hits in the ceiling's back
corners staying 35% under. Measured on `33fd0804` against references traced anew at 2048 paths a
pixel, it moves the strip's view alone, 0.221, 0.216 and 0.212 at `Low`, `Medium` and `High` at 160
by 90 where 0.216, 0.213 and 0.209, and 0.108 at `Low` and `High` at 800 by 450 where 0.104 and
0.105, the room 1% over where level. With both changes the room reads 12 to 13% under, its walls and
floor 16 to 17%, and the term both uncover is the method's (below), so both are taken under the
covered-fault principle, the cone first. Its slope is read by forward differences, three reads of
the field, which take the ceiling to 8% under and the side walls to 8 and 20% where central
differences' six took them to 10, 10 and 21%, the rays from the back wall's screen probe that meet
the ceiling right over the strip to 4% under where 35% (`BounceRoomsTests`), and the strip's view
within 0.001 of the six's. Global illumination costs 1.64 to 1.68 ms where 1.49 to 1.50 at `High`
and 1.51 to 1.52 where 1.28 at `Low` in the Cornell box, and 1.29 to 1.30 where 1.24 and 1.17 to
1.18 where 1.11 to 1.15 in the strip's room, as with six reads. On `fb1d1f95`, with the cone, the
slab test takes the two probes' faces toward the strip to 7 and 10% over at the first bounce where
211 and 206%, and past it to 17 and 23% under where 14 and 7% over, and the strip's view to 0.234,
0.245 and 0.244 at 160 by 90 where 0.222, 0.216 and 0.212, and to 0.130 and 0.136 at `Low` and
`High` at 800 by 450 where 0.109. Every other view reads as it did but the emissive blocks' room at
`High` at 800 by 450, 0.0213 where 0.0209 over blocks of 4 pixels, one block's lit side 0.4% darker
where a screen probe's rays cross a neighbor's box.

That term is the light the strip puts on the wall it is mounted on, which lies within a fifth of a
unit of the strip. The wall takes 7.8 of the strip's light half a hundredth above it, 0.8 a fifth
above and 0.11 half a unit above, and the floor's probes' four rays a texel, through the same
middles of its quarters at every probe of a row, meet the wall half a unit above the strip. Along
the floor probe's two texels toward it the strip's light in closed form averages 0.56 and 0.61,
0.33 of it within half a cell of the strip and 0.16 more within a fifth, where the four rays bring
0.08 and 0.11, so the floor's probes nearest the back hold their upward face 40 to 44% under and
the ceiling's their downward face 24 to 32%. With the strip half a unit off its wall the room reads
within 2% of its reference with or without either change, its side walls 7 to 8% under, and the
floor's probe 21% over where 40% under (`gi.screenprobe`, `gi.probe`, `gi.compare`,
BounceRoomsTests, `./e3d command profile`).

Four things seen are not the cascades'. The emissive blocks of the ninth room seem to stand in a
wide soft light, yet the floor below the small one reads within 4% of the reference's light from its
foot to 140 pixels down, 0.394 against 0.400 at the foot, and 7 to 11% under it beside the block's
edge; the frame's tonemap draws the block's light of 2.6 orange where the reference's curve clamps
it white, which leaves the floor beside it brighter than the block's color reads. No spokes show
around either block seen from above. The bands down the rooms' walls in the 8-bit pictures are the
steps of a smooth gradient, each column of 8 pixels of the thin walls' back wall lying 0.19% from
its neighbors' mean as the reference's does, and the far wall of the carried lamp's room 1.12%
against 1.08%. The dark ring along a sphere's terminator (REVIEW.md, Decision 27) belongs to
subsurface scattering, which `shaders_subsurface` draws without global illumination, and its capture
still shows a dark crescent inside the wax sphere's lit side. And `shaders_reflections`'s reference
draws its mirror floor and its metal spheres as rough, so it judges the wall alone, 10% under; the
noise of a wide penumbra shows in none of these frames apart from a's patches.
![The wax sphere of shaders_subsurface, its terminator](assets/cascades/subsurface-terminator.webp)

**A glow light's near field at a probe's hit, read on 2026-10-24.** A light thinner than a cell,
such as the strip of `shaders_bounce_rooms` or a torch on a wall, lights the wall it stands against
within a few hundredths of a block of its box, a patch one probe's ray meets and the next probe's
misses. A hit within a cell of a light's box takes the light as its mean over a square a cell wide
about the hit on its plane, at the middles of the square's four quarters (`glowLightsAt`). The
strip's view reads 0.207, 0.217 and 0.201 at `Low`, `Medium` and `High` at 160 by 90 where 0.207,
0.233 and 0.213, and 0.093 over blocks of 4 pixels at 800 by 450 at `Low` and `High` where 0.101
and 0.109, every other view within 0.001 at both sizes, the Cornell box 0.00006 further at `Low`
over blocks of 4 pixels, and global illumination's GPU time within 0.05 ms on the development
machine's GPU (`gi.compare` against references of 2048 paths a pixel, BounceRoomsTests with its
bounds at zero, `./e3d command profile` with the frames unlimited). Taken at a cell's distance from
the box instead, the hit moved straight away from it, the strip's view read 0.259, 0.263 and 0.279
at 160 by 90, the strip's light on its own wall lost, which the reference has.

**Where the method stops, read to 2026-10-24.** Six terms of the radiance cascades as the engine
draws them stood unmended, the last mended on 2026-10-30, each named by its cause in the readings
above. Every form read against one was out by the rule that no view's distance to its reference
widens at 800 by 450 or at 160 by 90 (`gi.compare` against references of 2048 paths a pixel,
BounceRoomsTests with its bounds at zero), or cost more than it gave, or left the term as it was.

- **The gain past the first bounce.** A hit reads the probes around it where 11 to 64% more light
  arrives for its normal than at the hit itself, the light rising away from the surface the hit lies
  on, so each bounce through the probes' own hits gains 1.08 in the thin room and up to 1.27 in the
  Cornell box, the world probes 8 to 16% over their references (`gi.probe`). Moving the probes
  behind a surface through it widened the grazing floor's view by 0.017 to 0.034, reading them with
  no lean widened it at `Low` by 0.074 and 0.082, its room's own shortfall uncovered, and scaling
  the light past the first bounce by the hit's openness against the probes' moved the open middle
  and cost 0.45 to 0.50 ms.
- **The probes' spacing against near occlusion.** A hit beside a block reads probes standing 0.6
  above it, where the block hides little, so the floor before the Cornell box's small block takes
  3.4 to 3.6 times the reference's light past the first bounce and the block's front reads 1.20 of
  its reference at `High`. An occlusion at the screen's probes' hits, from short marches or the
  field's distances, brought the front to 1.02 to 1.15 but widened the corridor's, the strip's and
  the carried lamp's views, a near wall that lights a hit counting as shade, at 0.14 to 0.64 ms, and
  weighing the blend toward the probes a hit sees changed nothing at the front, the floor seeing the
  same probes, at 0.26 ms.
- **The coarse cascades' absence inside a space narrower than their spacing.** The corridor, 2 wide
  in walls 0.3 thick, holds no probe of the second cascade or above, their spots in its walls moved
  out through the faces nearer them, so its first cascade's probes hold nothing at any bounce and
  its walls read 67 to 69% under, the march of its screen probes' rays (the corridor's far light,
  above) mending the first bounce of the far light alone. Moving such a spot through its farther
  face brought probes inside whose rays meet the walls before their interval begins and widened the
  corridor's view by 0.002 to 0.006, and tracing the first cascade's rays on where they see no probe
  above lit its probes to 86% under where 100%, the view moving by 0.005 at most, at 0.01 to 0.06
  ms.
- **The faces' read of light that runs along a surface.** A surface reads the probes' faces along
  its normal alone, so light running along a corridor, which its probes hold in the faces toward its
  ends, reaches its walls and floor only where a surface faces it, the mid-corridor probe's face
  toward the open end 0.020 against the reference's 0.125 and its other faces 87 to 93% under. No
  form is read against it alone.
- **A further way to the thin walls' corner.** With the screen's probes left out, the thin room's
  corner by its front wall reads 2.85 times the wall beside it with the lamp outside and 1.01 to
  1.03 with it off, where with the reach's slack at half the spacing it read 3.53, and cutting the
  twentieth of a weight `bouncedAt` keeps for an unseen probe takes it to 2.81, so the model pass's
  read of the probes there lends it the lamp's light by a way not yet named. The screen's probes
  cover it, the corner reading within its test's bound with them.
- **A bright patch narrower than a probe's texel read by a few fixed rays.** Of a screen probe's
  sixteen rays, fixed for its tile, one crosses each of its texels, and where a texel holds a
  bright patch far narrower than it, one probe's ray meets the patch where the probes beside it miss
  it, so the probe reads far over its hemisphere's light and its neighbors under, and the window's
  filter spreads the difference over its 5 by 5 probes into round spots 20 to 40 pixels across that
  stand still from frame to frame. The screen's probes make the spots, and with them left out
  (`gi.toggle screen off`) the surfaces read smooth. Three forms of it are read. A torch or a lantern
  carried as a glow light lights the wall it stands against within a few hundredths of a block of
  its box. In a plank room seven blocks across and four high with a lantern under its ceiling and two torches on its walls, field cell 0.25, at `High` and 960 by 540, a
  ceiling probe reads 0.643 over its hemisphere where a path-traced reference reads 0.079, its ray
  through a torch's patch bringing 1.42 where the reference along it brings 0.659, its neighbors
  read 43 to 46% under (`gi.screenprobe`), and the room shows 8 spots by the voxel game's measure,
  the luminance less a Gaussian blur of 20 pixels counting spots of 40 pixels or more at 4 levels
  or more over it. Three forms were read against it in that room and in the bounce rooms
  (`gi.compare` against references of 2048 paths a pixel, BounceRoomsTests with its bounds at
  zero). The tiles' places in their texels turned each frame, sixteen frames holding each place
  once, took the room's spots to 5 at the same distance from its reference, but over the mean of 16
  frames at 160 by 90 widened the thin walls' view by 0.006 to 0.016, the corridor's at `High` by
  0.004 and the strip's at `Low` by 0.003, and a still view's pixels changed from one frame to the
  next by 4.2 to 5.2% of its mean light at their 99th part in the Cornell box and by 2.6 to 3.3% in
  the carried lamp's room, where with the places held they change by 0.03% at most, as the sixteen
  ways' reading (above) found. A hit taking a glow light at a cell's distance from its box at
  nearest took the spots to 6 and 7 and widened the strip's view by 0.030 to 0.066 at 160 by 90 and
  by 0.068 to 0.071 at 800 by 450. A hit within a cell of a light's box taking the light's mean over
  a cell's square about it (a glow light's near field at a probe's hit, above) took them to 7, the
  round spots on the wall and the ceiling standing as before. Each held global illumination's GPU
  time within 0.1 ms. A point light a third of a block under a ceiling lights the ceiling over it
  as brightly. In `games/Blockout`'s hall, whose lamp is a point light 0.3 under the ceiling beside
  an emissive panel joined to it, three screen probes on the far wall 48 pixels apart read 0.40,
  0.40 and 0.46 at `High` where the path-traced reference over their hemispheres reads 0.57 to
  0.61, the texel of each toward the lamp reading 0.94 to 1.06 in the reference over its whole and
  0.07, 0.23 and 0.39 along its one way, by whether that way meets the bright ceiling about the lamp
  (`gi.screenprobe` at 1280 by 720). The sun on a floor under a window is the third, narrower than
  a texel of a probe on the room's far wall. The noise seen in the bounce rooms 3 to 6 is the
  term's still spots. At `High` at 800 by 450, by the voxel game's measure over the frame's middle,
  the sun's room holds 9 spots with the screen's probes and 6 without and the strip's room 16 and
  7, its walls smooth without them as the references are, and the corridor's and the red walls'
  views hold their scenes' own edges, 7 and 5, either way. Two frames 60 apart hold the same spots,
  and a still view's pixels change from one frame to the next by 0.03% at most (above). As the eye
  moves the spots shift, forming again where the moved probes' tiles meet the bright patches. In the
  voxel game's plank room with a hanging lantern and two torches on its walls at night, at `High`
  at 1280 by 720, the eye stepping half a block over 40 frames changes each frame by 0.27 to 0.30
  sRGB levels on the mean with the screen's probes and by 0.31 to 0.35 with them left unread, one
  frame with them by 0.73, and either way the light holds still the frame the eye stops
  (`voxel.flicker`). Before the step and after it, the ceiling's spots stand at other places on it
  while the lantern and the torches move by their parallax alone. Mended on 2026-10-30, each
  probe's rays moving on to the next of the sixteen places each frame and the probe keeping sixteen
  frames of its light where its surface was before the window's filter blends it, fewer where the
  kept light lies further from this frame's blend of its like neighbors than three times the
  blend's own error, so a roof of leaves made to let light through is followed within thirty
  frames. At `High` at 800 by 450 the bounce rooms' spots by the same measure went from 11, 6, 9,
  10, 4, 17, 24, 6 and 4 to 5, 4 to 5, 8, 5, 4, 4 to 5, 16 to 17, 5 and 4, and the plank room's at
  960 by 540 from 5 to 2, the lantern and its glow. A still view's pixels change from frame to frame
  by 0.07 to 2.6% of its mean light at their 99th part where by 0.17% at most, the strip's room the
  most, and the plank room by 0.021 sRGB levels on the mean where 0.002. As the eye steps or turns
  the probes add what they added before or less, the step 0.014 levels a frame over the probes left
  unread where 0.016, so the probes stay on the window's tiles, their kept light following the
  surface through the frame before's camera, with no grid of their own in the world. The rooms'
  views at 160 by 90 moved by 0.020 of their light at most, 8 of 18 nearer their references and 6
  further by 0.012 at most, the thin walls at `Medium` the most (`BounceRoomsTests`), and global
  illumination took the plank room at 1920 by 1080 2.70 to 2.75 ms where 2.61 to 2.69.

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

The window's scene is drawn through the HDR frame every frame the window shows one, a mesh, a
particle or a shape drawn with depth inside `BeginMode3D` (`BloomRenderer`,
`Rendering/PostProcess`). A frame of 2D alone is drawn straight into the window, where it looks the
same. Two nodes run between `probes` and `main_pass`. `hdr_scene` draws the window's models and its
draw list up to its last batch with depth, the 3D shapes inside `BeginMode3D`, into a half-float
target the size of the window at the window's samples, cleared to the clear color. The frame holds
its light sRGB-encoded, as the eight-bit window does, and carried on past 1. The window's view of
the model pass writes it so (§4), and the 2D colors and the clear color go in as they are. The
scene's blending and multisampling are done on encoded light, then, as raylib's are and as the
window's were, a shape keeps its color, and a shader of the program's own returns its color encoded
wherever it draws. Such a shader drawn into the frame has the color it writes held to what an
eight-bit frame keeps of it before blending, its alpha between 0 and 1 and no channel below 0, light
past 1 kept (`ShaderProgram.HeldToEightBits`, a GLSL.std.450 FClamp put into its SPIR-V before each
store of the color), so a raylib shader whose gamma correction raises its alpha past 1 blends as
raylib's frame blends it. A render texture's own formats are left as the program wrote them.

Where a pass reads the scene's light, bloom, an exposure that follows the scene, the depth of field
or motion blur, the traced reflections of global illumination, or the window's particles,
`hdr_scene` then decodes it texel for texel into a half-float image of one sample (`decode.slang`),
writing the scene's resolved depth into that image's depth, and draws the window's particles over
it, so their emission adds up in linear light. Each of those passes reads this image. The scene is
decoded before any filter reads it, since a filter across an edge before decoding would dim what is
bright beside what is dark. `bloom` halves the decoded light five times, down to a thirty-second of
the window, with Jimenez's thirteen-tap filter, keeping on the first step only the light past the
threshold, eased in over a tenth of it. It then adds each level back onto the one above through a
tent, so the first level holds the light spread over every size (`bloom.slang`).

`main_pass` draws the composite first, over the whole window, the scene decoded or the decoded image
read as it is, with the first level added at the intensity over the number of levels, tonemapped and
encoded (`composite.slang`). The engine's curve gives way to a clamp in a frame where no light past
white comes about, with no light, sky, probe, global illumination, particle, bloom or exposure, so a
world drawn as raylib draws one keeps a color near white as it is (`BloomRenderer.Bends`). The
models node draws nothing more, and the immediate node draws the batches after the split over it, so
a game's interface is never bloomed or tonemapped and keeps raylib's colors, and ImGui after it as
before. The targets are made the first frame the window shows a scene and again when the window's
size changes, and those they replace are destroyed four frames later, as are all of them the first
frame it shows none.

On the laptop's RTX 4070 with the frame rate unlimited, `./e3d command profile` gives the GPU's time
for each node, the median of seven readings of a second each, before the window's scene took this
path whatever the effects and after.

| Frame | Before | After |
|---|---|---|
| `shaders_bloom` with bloom off, 800 by 450 | `models` 0.037 ms, `main_pass` 0.010 | `hdr_scene` 0.069, `main_pass` 0.013 |
| `shaders_bloom` with bloom on | `hdr_scene` 0.064, `bloom` 0.034, `main_pass` 0.019 | 0.071, 0.027, 0.016 |
| `models_loading`, no light | `models` 0.033, `main_pass` 0.049 | `hdr_scene` 0.102, `main_pass` 0.010 |
| `shaders_cornell_box`, global illumination on | `hdr_scene` 0.095 | 0.111 |
| `games/Wick`, 960 by 540, global illumination on | `hdr_scene` 0.234 | 0.258 |

A frame with every effect off pays some 0.02 to 0.035 ms for the target's clear and resolve and the
composite, and one that decodes its scene some 0.01 to 0.02 ms more. `textures_bunnymark` draws no
scene, and `build/raylib-bench/run.sh`'s ramp held 465,168 and 437,806 sprites at sixty frames a
second after against 478,849 before, its runs' spread, and `models_stress`, whose entities go
through the frame now, 646,168 and 584,628 against 615,398. The frame's target takes 44 bytes a
pixel of the GPU's memory at four samples, its multisampled half-float color and the color and the
depth resolved, 16 MB at 800 by 450 and 91 MB at 1920 by 1080, the bloom chain's levels some 3 bytes
a pixel more and the decoded image 12 where a pass reads it. It draws into the window's own
multisampled depth (`GraphicsDevice.CreateRenderTargetOnWindowDepth`), which the window's pass after
the composite clears and draws nothing into with depth, so a frame holds one multisampled depth and
not two, and a target made before the window's depth was made again, as a swapchain made again at
the same size makes it, is made again too (`WindowDepthGeneration`). With one sample the target has
a depth of its own, the window's being none to sample. `nvidia-smi` gave `shaders_bloom` with its
bloom off at 1920 by 1080 278 MiB with no such frame, 405 MiB with the frame's own multisampled
depth and 374 MiB with the window's lent, the 31 MiB the 16 bytes a pixel of that depth.

The composite carries the effects of `FrameEffects` too, `SetExposure`, `SetTonemap` (the engine's
curve, Narkowicz's fit of ACES, a cut at 1, or one of Bevy's eight), `SetColorGrading` (saturation
and a tint in linear light after the curve, contrast about the middle once encoded) and
`SetVignette`, all in the composite's push constants. With `SetFxaa` the composite draws into an
eight-bit target the window's size instead, and `main_pass` draws that through FXAA (`fxaa.slang`,
the console form of Lottes's, over the encoded colors) before the interface. `FrameEffectsTests`
reads each from a frame.

Bevy's eight curves are worked out as `bevy_core_pipeline` 0.19.1's `tonemapping_shared.wgsl` works
them out, its matrices written as dot products, so a picture tonemapped here is the one Bevy draws.
AgX, Tony McMapface and Blender's filmic look the light up in Bevy's own tables, KTX2 cubes of 32,
48 and 64 texels a side in half-float RGBA or RGB9E5, which `build/bevy-luts.py` carries from Bevy's
crate with their Zstandard swapped for zlib, so `TonemapTables` reads them with .NET's `ZLibStream`.
The first frame a curve with a table is chosen uploads its table as a 3D texture
(`GraphicsDevice.CreateVolumeTexture`), sampled linearly and held at its edges as Bevy samples it,
at the composite's binding 3, where a texel stands in for the other curves, and the composite's sets
are kept by the image they read and the table. `TonemapTests` draws SHARED.md's ramp, 1024 by 8
pixels from a 4096th to 256 in eight colors, through each and holds it to a model of each curve and
table on the CPU and to BevyCSharp's picture of it, copied from BevyCSharp's commit 64ec311 under
`References/tonemapping`, within two levels of 255 for a curve and four for a table, every one
within one level of both on the RTX 4070.

`SetAutoExposure` makes the exposure follow the scene, in two passes of `exposure.slang` after the
bloom chain, which keep the log2 of luminance since an eye adapts by ratios and a mean of logs is not
pulled up by one lamp. The first measures the decoded light into a target of 64 by 64, each texel the
mean of sixteen taps over its part of the frame. The second, one texel, weights those toward the
middle of the picture, four times as much there as at the edges, holds the mean between the
luminances the exposure's bounds bring to a mid gray of 0.18, and moves the value of the frame before
toward it by one less e to the minus the speed times the seconds since, or all the way on the first
frame. Two such texels take turns, each frame writing one from the other, and the composite divides
0.18 by two to the value it reads and multiplies the exposure by that, so nothing is read back.

`SetDepthOfField` and `SetMotionBlur` are passes of their own after the bloom chain, into half-float
targets the window's size that the composite then reads in place of the scene, both reading the
decoded light and the HDR target's resolved depth unfiltered and working back to the world through the inverse of the window's
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

`SetAmbientOcclusion` turns on the `ambient_occlusion` node, after `window_depth` and before every
pass that lights the window's meshes (`AmbientOcclusionRenderer`). `window_depth` draws the depth
of the window's batches that cast a shadow into a depth target half the window's size, wherever the
occlusion, the sun's contact shadows or the screen's probes read it, through the shadow pass's
pipelines, whose depth-only pass is the same at any size, pushing each batch's own camera; the
batches are gathered and their instances written in `model_batches` before it, once a frame for
every pass that draws them, so the profile gives the gather, the depth and the occlusion each
apart. On the voxel game's hills at eight columns in a Release build the gather took 1.3 to 1.5 ms
of the CPU, the depth 1.16 ms of the GPU, and the occlusion with the sun's contact shadows 0.08 ms
of it, by `./e3d command profile`. `ao.slang` then puts each texel back in the world through the
inverse view-projection, takes its normal from the nearer neighbor along each axis, and sums
Alchemy's term over twelve taps on a spiral turned by interleaved gradient noise, within the radius
held to three tenths of the picture, each fading out toward the radius. Two passes blur it across
and down, nine taps each, weighed by how near each tap's distance from the eye is to the pixel's.
The lights' set binds the result at binding 9 for the window's view, and the window's lighting
buffer says to read it, so `lit` multiplies its material occlusion, which scales the ambient,
environment and probe light alone, by the occlusion at half the fragment's position. In Manor's
rooms it takes 0.08 ms of the GPU and 0.16 ms of the CPU. A render target that draws meshes through
a camera of its own has the same worked out at half its size in the `targets` node before its pass
(`AmbientOcclusionRenderer.DrawTarget`), the sun's contact shadows with it, bound at binding 9 for
its view, whose buffer says to read it as the window's does, and let go the frame after one the
target is not drawn in. The two views of `games/Sumo` at 640 by 720 take 0.775 ms of the GPU in
`targets` with it on where they take 0.678, the medians of three runs of seven readings of `./e3d
command profile` with the frame rate unlimited, and 1.15 where they take 1.05 with global
illumination at `Low`, whose screen probes stand on the same depth. A probe's faces are drawn
without it.

A material's `SubsurfaceRadius` scatters its diffuse light under its surface over the window's
frame, as Jimenez's separable screen-space scattering does (`SubsurfaceRenderer`). The material's
profile, the share of the radius each color travels and the radius, keys its batches, so a batch
holds one, and once the scene is decoded the window's batches that scatter, opaque or masked and
drawn with the model pass's own shader, are drawn again by `subsurface.slang`
(`ModelRenderer.DrawSubsurface`) into three half-float images of the window's size at one sample,
their diffuse light, which `litLight` in `modelpass.slang` works out apart from the specular, their
profile, which a vertex buffer of one element a batch gives the call, stepped per instance with a
stride of 0, and the light that comes through them from a light behind, an ear's or a leaf's. The
light reaching the far side, facing away along the normal, falls with the thickness it crosses,
each of red, green and blue by e to the minus the thickness over its share of the radius, times the
surface's color, nothing past three of the widest share. The thickness toward the light is marched
through the scene's distance field from a little under the surface until the march leaves the mesh,
where the field's finest cascade holds the point, and past that cascade the sun's shadow map gives
it, the depth from the face the map holds nearest the sun to the point; the field is bound to the
model pass where it is built though light does not bounce, the probes' images left empty there. A fragment is kept only where the scene's depth, its first sample's, lies
within two pixels' change of its own, so a marked surface hidden by another leaves its pixels
unmarked. `subsurface_blur.slang` then spreads that light, with the light that came through,
across into an image of its own and down onto the decoded frame, seventeen taps each way out to the radius, each color by a Gaussian a third
of its share of the radius wide, the radius turned into pixels at the pixel's distance from the eye,
a tap left out where it is unmarked or lies farther from the eye than the radius from the pixel,
and the pass down adds the spread light less the pixel's own diffuse light, which the frame held
where it held none of what came through, so an unmarked pixel is never touched. The window's particles are drawn over the frame after, in a pass that keeps its color and
depth. In `games/Manor` at 1280 by 720, every one of its 139 materials scattering over 5 cm takes
`hdr_scene` 0.95 to 1.02 ms of the GPU where it takes 0.42 to 0.46, and one of them 0.65 to 0.70,
the medians of seven readings of `./e3d command profile` with the frame rate unlimited, the
materials marked through `./e3d eval`. With the light that comes through, its 172 materials now
scattering over 5 cm take `hdr_scene` 1.294 ms where they took 1.185 without it and none 0.47, the
medians of seven readings again, the engine packed with each (`build/pack.sh`). A thin slab lit
from behind reads on its front (136, 102, 83) by a lamp measured through the field and
(180, 162, 150) by the sun measured through its shadow map, where a slab 1.2 thick and an unmarked
one read the ambient light's 39 (`SubsurfaceTests`).

`SetSubsurfaceQuality` sets three tiers. At `Low` the marked images and the spread across are
half the window's size and the spread down onto the frame reads them there, nine taps a way; at
`Medium` the window's size and nine taps; at `High`, the default, seventeen. The scatter pass reads
the scene's depth where its vertex falls in clip space, so a fragment of the half-size images reads
its own place. In `games/Manor` at 1280 by 720, every one of its 172 materials scattering over
5 cm, `hdr_scene` takes 0.722, 1.138 and 1.243 ms where it takes 0.470 with none scattering, the
medians of seven readings, the engine packed; `shaders_subsurface`, which draws wax, skin and
marble spheres and a leaf and a slab of wax lit from behind, takes 0.09 to 0.15 ms more than with
none at each, its readings moving by 0.05 ms between runs, its frame too light to hold the GPU's
clocks. `SubsurfaceTests` holds the lit sphere's softened terminator at each tier.

Drawing that example found four things in the light through, each mended. A march begun at the
drawn surface left at once where the field's surface lay a little inside it, and a sphere's
shadowed side took the sun's light in bands, so the march first finds where it enters the mesh the
field holds, within a cell, and measures to where it leaves, the exit placed between its last step
inside and the first outside. The field's cells of 0.15 against skin's reach, three of its radius
of 0.06, drew scallops along a sphere's terminator, so the field measures only where its cells are
half the reach or less, the sun's shadow map past that. The map read texel by texel speckled the
shadowed side and stepped the band along the terminator, so its depth is the mean of nine samples
a texel apart, each blended between the four texels about it. And the light cut to nothing at
three of the widest share, a twentieth of it, ended in a ragged edge where the measured thickness
wavered across that line, so it fades as e has it. The scene's distance field had held the
example's spheres as spiky blobs too, the fault `8524102e` mended.

A material says how thick its parts are at most (`SubsurfaceThickness`, glTF's
`KHR_materials_volume` thickness through Assimp's `$mat.volume.thicknessFactor`), carried with the
batch's profile as a second attribute of the scatter pass's fifth binding, and the scatter pass
takes the lesser of it and the thickness measured, or it alone where neither the field nor the
sun's map can measure. The field holds a sheet drawn as one a cell thick, half a cell either side,
so a leaf of one sheet let the light through as a slab of 0.15 would; and toward a lamp that casts
no shadow where the field is coarse nothing measures it at all. `shaders_subsurface` draws its leaf
as one sheet two millimeters thick by its material, and `SubsurfaceTests` holds a sheet with a lamp
behind it and no field dark without a thickness, (39, 39, 39), and lit through with five
centimeters of it, (138, 96, 62). It holds the cap too, the same sheet in a field of 0.15 cells,
which measures it a cell thick and lets none of the lamp through, (39, 39, 39), lit through with two
millimeters given, (146, 146, 145).

Where the field is coarse, a spot or point light that casts shadows measures the thickness from its
own map as the sun does, the mean of nine depths a texel apart about the point in its square of
the map or its face of the point map (`spotTile` and `pointFace` in `lights.slang`, which its
shadow lookup shares). From the light, a point's clip coordinates through the face's
view-projection are the light's plus its distance times the way's, so the depth read gives the
distance at which the way met the face nearest the light in one division, whatever the near and
far planes the face was drawn with, and the thickness is the rest of the way to the point
(`lampThickness` in `subsurface.slang`). `SubsurfaceTests`' slabs lit from behind read the thin one
(152, 113, 76) through a point light's map and a spot light's alike, (141, 65, 41) through the field
and (184, 145, 104) by the sun, and the thick one the ambient alone.

A sphere lit from its side showed a dark line along its terminator with a bright one six degrees
past it, skin's 117 against the shadow's 39 and as bright as the lit side six degrees before, and
the far side of wax through the field lit again, 70 at 120 degrees and 88 at 148. A few degrees
past the terminator the point lies at the sphere's edge as the light sees it. The map's mean of
nine depths took in the empty map beyond the edge, and the field's march along the surface found no
mesh within a cell and took it for a sheet, so either read the sphere no thicker than nothing and
the light came through whole. The map's mean takes only the depths in front of the point, and a
light that casts shadows measures through its map wherever it has one, the field kept for a lamp
with none. A field that stood as a sheet where its march found nothing within a cell, and gave way
to the map past it, left the pixels either side of that decision measured two ways and dashed a
line of light across the example's marble sphere. A march still in the mesh at the reach returned
the reach, so the far side kept a twentieth of the light through however thick, which fades out
between half the reach and the reach, smoothly in the thickness. And the light through fell as e to
the minus the thickness over the travel, which from the true chord across the sliver past the
terminator still rose to a hump ten degrees out above the lit side's light before it; it falls as
the Gaussian the spread uses for each color, a third of its travel wide, as Jimenez derives the
light through a part from its profile, so wax's light falls 84, 69, 60, 54, 50, 47, 43 from 88
degrees to 100, half its light at the terminator by some 93 degrees and a tenth by 100, and skin's
edge runs at the terminator 0.04 of a degree from its neighbors' mean around it where its light
through's ran 0.41 off in the map's texels (`SubsurfaceTests`). A Burley profile of a third of
wax's reach over the same sphere falls to half by 98 degrees and a tenth by 118, its tail longer
than one Gaussian's, which a sum of Gaussians in the spread would follow.

The spread and the light through stay out of render textures and of a reflection probe's faces. A
render texture is drawn in eight bits with the curve and the encoding at the end of the model pass,
so it holds no linear frame to add the light under a surface into, and bringing one in would give
each texture the half-float frame and the composite the window has. A wax sphere lit from its side
reads in a render texture as an unmarked one does, (44, 44, 44) at its terminator and (39, 39, 39)
four degrees past it, where the window reads (69, 61, 53) and (54, 43, 39), the same frames drawn
into both at 640 by 480. A probe's faces are 64 pixels of half floats, where wax's reach of 0.3
seen from three units off spans some three pixels, before the faces are filtered for each
roughness, so only a mirror's reflection of a scattering surface close to its probe would show it.

Render targets drawn with `BeginTextureMode` stay eight bits, with the curve and the encoding at the
end of the model pass. A shader of the program's own returns its color encoded in either, so it
reads the same in the window and in a render texture, with every effect on or off, but for light
past the curve's knee, which the window bends after blending and a render texture before.

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

The same runs after the three changes below.

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
8. **A plain draw was drawn whole by every view.** Item 6 culled the groups of mesh entities, and
   a `DrawMesh` or `DrawModel` was still drawn into the camera's pass, the occlusion's depth and
   each cascade wherever it lay. The voxel game built on the engine, which lives in its own
   repository, [Voxel-Sandbox](https://github.com/EggyStudio/Voxel-Sandbox), draws its world as some
   1,800 meshes a frame and cannot leave out the ones it does not see, since the scene's field
   gathers the frame's draws and a mesh left out of a frame leaves it. On seed 1's hills at 1280 by
   720, standing at the spawn facing north at hour 10 with eight columns drawn around the player,
   the frame took 25.5 ms, the GPU 14.2 ms for the shadows, 4.9 for the scene and 4.8 for the
   occlusion's depth, over 9,181 calls, 1,836 in each of the camera's pass, the depth and the three
   cascades, averaged over 600 frames after `profile.reset` by `./e3d command profile` on an RTX
   4070 Laptop GPU, the Debug build.
   **Changed.** `GpuMeshes` keeps the box around each mesh's positions, unknown for a skin the GPU
   poses, and a batch of plain draws is in blocks of 64 as a group is, each around its draws' boxes
   placed by their world matrices (Arvo), so a section of blocks keeps a box as tight as its own. A
   batch with a shader of the program's own is not, since its vertex stage may move what it draws
   past the box, unless the program says the stage keeps the positions (`SetShaderKeepsPositions`).
   Each pass makes its planes once for the matrix it draws through and passes over a
   batch it sees none of before binding its buffers, and a light's pass adds its near and far
   planes, which item 6 left out. The shadow shader writes the position as the light's matrix gives
   it and no pipeline clamps depth, so a block past either is clipped whole whether drawn or not,
   and the cascade's box reaching four of its radii toward the light holds every caster that can
   shadow it. The same scene afterward took 11.9 ms a frame, the GPU 2.3 ms for the shadows, 1.4 for
   the scene and 1.2 for the depth, over 1,825 calls, 424 the camera's, 423 the depth's, and 17, 90
   and 871 the cascades', the CPU 1.29 ms for the shadows in place of 1.78 and 0.70 for the scene in
   place of 1.53, and no pixel of the frames moved by more than one level in 255. `models.draws`
   gives the last frame's calls by pass. At six columns the frame went from 14.3 to 8.4 ms and the
   shadows from 6.8 to 1.8.
   The game's sections came to be drawn with its terrain shader, whose vertex stage unpacks each
   vertex's normal from its stream and keeps its position, and were drawn whole in every pass again.
   Said to keep its positions, on the default world at the spawn at noon at 1280 by 720, the frames
   unlimited, the engine's Debug build, the calls at eight columns went from 2,899 to 886, 631 to
   225 the camera's, 569 to 223 the depth's and 563, 565 and 571 to 25, 58 and 355 the cascades',
   the frame from 11.7 and 13.4 ms to 7.6 and 8.6, the GPU's shadows from 3.3 and 3.2 ms to 1.3 and
   1.2 and the scene from 3.6 and 3.7 to 2.1 and 2.2, and at sixteen columns from 10,352 calls to
   2,001, the frame from 42.3 and 42.5 ms to 26.4 and 29.7, the shadows from 18.8 to 2.8 and 2.9 ms
   and the scene from 11.1 to 5.6, two readings each, and no pixel moved by more than one level in
   255.

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
- **Descriptor pools counted from their layouts** (`GraphicsDevice.MakeDescriptorPool`), done,
  since MoltenVK refuses a set past its pool's counts where Mesa's drivers allocate it and the
  layer only warns. A pass's pool holds the sets of each stage it dispatches, by type from their
  bindings, and the shared sets come from pools by the shape of their layouts, so a binding added
  to a shader reaches its pool with no count written for it. The layer's warning on a set past its
  pool is counted with its errors, so a test fails on one wherever the layer runs. A frame that
  fails waits for the device before it ends, since the renderers keep what they let go for a count
  of frames and a frame not submitted would otherwise shorten it.

### Debugging

- **Object names** through `VK_EXT_debug_utils` (`GraphicsDevice.Name`), done for the shadow
  maps, the environment map and sky, reflection probes and their faces, the model instance ring,
  textures and render textures by their id, so a capture shows them by name. Each render graph
  node's commands are a labeled region, by the node's name, wherever the instance has the
  extension, with validation or under RenderDoc. Mesh buffers and pipelines have no names.

## Order of work

Normals and lights, Assimp's models with their materials, dynamic rendering with synchronization2,
shadow cascades with point and spot shadows, bloom and FXAA, the scene's distance field, light that
bounces as Radiance Cascades over it with glossy reflections traced through the depth and the field
and the GPU's own rays for what the field misses, and tonemapping as a full-screen pass over every
frame that shows a scene (§5) are built, in that order, and nothing of the order is left.
