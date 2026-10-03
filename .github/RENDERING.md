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
- **A frame in four steps**, each a list of systems in `Renderer`:
  1. **Extract** copies what the frame needs out of the game's `World` into a separate `RenderWorld`
     (`CameraExtract`, `MeshMaterialExtract`, `LightExtract`), so the game can change its world
     while the frame is drawn.
  2. **Prepare** uploads what changed (`MeshPrepare`, `TexturePrepare`, `LightingUboPrepare`) and
     fills per-frame buffers through `DynamicBufferAllocator`.
  3. **Queue** sorts draw items into phases (`Opaque3dPhase`, `Transparent3dPhase`).
  4. **Graph** runs the render graph's nodes in topological order. `MainPassNode` clears the
     swapchain image and drains the phases, and `ImGuiRenderNode` draws Dear ImGui into the same
     pass.
- **Meshes** carry positions only, and the fragment shader writes a constant white, so materials
  and lighting are extracted and uploaded but not yet visible.
- **Shaders** are GLSL compiled to SPIR-V at runtime through shaderc.

## 1. Slang through slangc

Every shader is a `.slang` file holding all of its entry points. `SlangCompiler` runs `slangc` per
stage with `-target spirv`, and the result is cached under the asset root in `.slang-cache`, keyed
by a hash of the source, everything it imports and the defines it was compiled with. A machine
without `slangc` reads the cache, so a shipped game needs no compiler, and an entry whose sources
have changed is never used.

`slangc` is a tool rather than a library. `build/fetch-slang.sh` downloads a pinned release into
`build/tools/slang`, and the compiler is looked for in `ENGINE_SLANGC`, then on the `PATH`, then
there. Linking Slang into the engine would add a native library of tens of megabytes to every
game for the sake of compiling while it runs, and running `slangc` as a process does the same with
nothing shipped.

Slang is chosen over GLSL because one language covers vertex, fragment and compute with modules,
generics and interfaces, and because the same source can later target Metal or Direct3D without a
second set of files. Reflection comes from `slangc -reflection-json` and replaces hand-written
descriptor layouts where a shader declares its own parameters.

## 2. The immediate pass

`Draw` calls from the flat API (see [DESIGN.md](DESIGN.md)) record into a `DrawList` resource: a
growing array of position and color vertices, split into a line batch and a triangle batch per
depth mode and per view. `ImmediateNode` runs after `MainPassNode` and before ImGui. It writes the
frame's vertices into the dynamic buffer arena in one copy and issues one draw per batch with
`immediate.slang`. The list is cleared at the start of every frame.

This is raylib's rlgl layer in Vulkan terms. It keeps shapes, grids, gizmos and debug lines out of
the ECS and out of the mesh path, and it is the first thing a program sees on screen, so it is
built before materials.

## 3. Meshes and materials

A mesh carries position, normal, tangent, two texture coordinates and a color, interleaved, with
32-bit indices. Models come from Assimp, which supplies the mesh data, the material factors and the
texture paths in one pass, and textures are decoded by StbImageSharp with mipmaps generated on the
GPU.

The material is one struct for every mesh: base color, metallic, roughness, emissive, normal scale
and occlusion strength, each with an optional texture. That is the glTF metallic-roughness model,
which Assimp maps every format it reads onto. A program that needs something else writes a Slang
shader and passes it to `LoadMaterial`, and the engine binds the standard set of parameters for it
by name.

## 4. Lights and shadows

`Light` is one struct with a kind (directional, point, spot), a color, an intensity and a range, and
`LightingUboPrepare` packs every visible light into one uniform buffer per frame. The first shadow
is one cascaded shadow map for the main directional light, rendered as a depth-only node before the
main pass. Point and spot shadows follow as an atlas.

## 5. Render targets and post processing

`BeginTextureMode(target)` redirects the calls that follow into an offscreen image, which a later
draw can sample. Post processing is a chain of full-screen Slang passes over the main color target
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

- **Validation layers on in Debug builds**, with every message routed into the engine log.
- **Object names** through `VK_EXT_debug_utils`, so RenderDoc shows `Opaque3dPhase` instead of a
  handle.

## Order of work

1. Slang in place of GLSL, with the cache, and the existing mesh and ImGui shaders ported.
2. The immediate pass and the `DrawList`, so shapes and grids appear.
3. Base color reaching the screen, then normals and one directional light.
4. Assimp models with textures, and the material struct.
5. Dynamic rendering and synchronization2, then VMA.
6. Render targets, then tonemapping.
7. The directional shadow map, then point and spot shadows.
8. Bloom and FXAA.
