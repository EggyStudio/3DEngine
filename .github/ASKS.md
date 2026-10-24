# What the games ask of the engine

A game built on this engine, in `games` or beside it, finds where the engine falls short while it is
made: a cost it cannot afford, a thing it cannot do, a thing that is wrong. The session making the
game writes that here, one entry a finding, and the session that writes REVIEW.md reads this file,
turns an entry into an item of REVIEW.md's Now list by its weight, and writes the item's number back
under the entry. The engine's working session reads REVIEW.md, as it always has, and not this file.
An entry stays until its item is done and the game has read what came of it, and is then removed by
the session that wrote it.

An entry has a heading with the date, the game and the finding in a line, and under it: what was
measured and how, with its numbers, the command (`./e3d command profile`), the scene, the resolution
and the GPU; what the engine lacks or spends; what the game does meanwhile; and, written by the
reviewing session alone, a line beginning `Review:` with the item it became or the answer. Prose
follows STYLE.md, and no entry names a person (NORM.md's rule 4.7).

## Entries

### 2026-10-10, the voxel game in `3DEngine.Game`: its tests build on every run but run on none

`3DEngine.Game.Tests` holds 22 tests of the game's light levels, its meshes' faces and corners, the
player's body, its saves and its generation, each in a small world laid out without a window or a
GPU, which `dotnet test 3DEngine.Game.Tests` runs in about a second on the RTX 4070 Laptop's
machine. The solution builds the project under `-warnaserror` on every system, and `build/test.py`
runs `3DEngine.Tests` alone, so a test of the game that fails reaches no page. The engine lacks a
step that runs the game's tests and counts them on the page beside its own. The game runs them by
hand before each of its commits.

Review: item 1 of REVIEW.md, 2026-10-10; `build/test.py` runs the game's project beside its own with
the batch that next touches it, its count apart on the page.
Review: on 2026-10-17 the game moves to its own repository (REVIEW.md Decision 41), whose build
workflow runs these tests on every push; item 1's clause on `build/test.py` running them here is
dropped.

### 2026-10-11, the voxel game in `3DEngine.Game`: land behind a wall is lit in any order

In a closed room of white concrete 11 by 6 by 11 inside (`voxel.room 10 4 -22 22 11 -10
white_concrete` on a flat world), its west wall red and its east wall green, with sixteen glowstones
on its floor at x 13, 15, 17 and 19 and z -19, -17, -15 and -13, the eye inside at 16.5, 6, -11.5
facing north 20 degrees down, hour 23, High, bloom off, the glow lights left out by `gi.toggle glow
off`; a hidden window of 1280 by 720 and an RTX 4070 Laptop GPU. The model pass, `gpu.hdr_scene`
from `./e3d command profile`, reads 1.42 ms at a render distance of 8 columns and 0.83 ms at 2, as
the engine's session read it in a copy of the game's build with `214feb11`'s shaders, so the land
outside the room, all of it behind the room's walls, costs the pass about 0.6 ms though none of it
is seen.

The game draws its opaque sections in the order it keeps them. Drawn nearest the eye first they cost
no less: 1.40 to 1.43 ms nearest first against 1.35 to 1.38 ms kept with the glow lights off, and
1.85 to 1.92 against 1.79 to 1.83 with them on, three readings after 400 frames of each order, each
order twice, the kept order's readings matching the room's earlier ones, so no other work held the
GPU then. The model pass discards a fragment for its alpha cutoff and no pass writes depth ahead of
it, so its depth test runs after a fragment is lit, and an order that puts the walls first saves
nothing.

The engine lacks an opaque pipeline without the discard for materials that cut nothing out, or a
depth pass ahead of the model pass, either of which would let the walls hide the land before it is
lit. The game draws in its own order meanwhile, its render distance a setting.

The game culls the sections the eye cannot see, on by default and turned off by `voxel.cull off`, a
flood through the sections' open faces from the eye's section as Minecraft's visibility graph has
it, leaving the eye's own section only by the faces the open cells around the eye reach, the lamps'
cubes drawn all the same. Read on `2a14ab13`'s engine from a clean worktree, each kind twice and in
turns, `./e3d command profile` over 300 frames, or over a walk of 14 blocks: this entry's room as
above, a cave carved in a hill of stone at noon on the flat world, from 50, 6, -30 to 60, 10, -20 in
stone from 40, 4, -40 to 70, 40, -10, and a cave of seed 1's hills at -28, 34, -32 walked east at
noon. The passes are the window's depth, the model pass, the sun's shadows and the light that
bounces, in milliseconds.

| Scene, culling | Sections left out | Depth | Model pass | Shadows | Bounce | Frame |
|---|---|---|---|---|---|---|
| This room, off | 0 of 226 | 0.416 | 1.115-1.117 | 0.218-0.220 | 1.413-1.419 | 3.7-4.3 |
| This room, on | 64 of 226 | 0.301 | 0.983-0.990 | 0.167-0.168 | 1.392-1.401 | 3.9-4.1 |
| Carved cave, off | 0 of 243 | 0.451 | 1.188-1.192 | 0.205-0.224 | 1.532-1.533 | 3.9-4.1 |
| Carved cave, on | 241 of 243 | 0.006 | 0.617 | 0.042 | 1.057-1.060 | 2.1 |
| Hills' cave, off | 0 of 1,090 | 2.743-2.752 | 4.640-4.704 | 6.876-6.930 | 1.351-1.363 | 16.2 |
| Hills' cave, on | 905 of 1,085 | 0.257 | 0.656-0.660 | 0.060-0.073 | 1.095-1.102 | 3.5-4.2 |

In this room the flood leaves out 64 sections, 0.30 ms of the window's passes and the shadows, of
the 0.34 to 0.42 the land costs: the flat world's open air outside the walls shares the room's
sections, and a section's faces are joined as wholes, so the flood passes out through them to the
land around. Underground it leaves out nearly all, five sixths of the frame in the cave of the
hills. The light the eye sees does not change: that cave lit by a glowstone reads the same with
culling on and off, its mean 9.93 of 255 both ways and no pixel more than 2 apart, and the carved
cave stays black at noon with the hill above it left out, so the culled land shades nothing the
drawn sections do not. The field's own cost over the walk stays at 0.004 ms either way. The game
draws so meanwhile; the engine's occlusion culling is the review's to weigh against these.

Review: item 8 of REVIEW.md, 2026-10-11; an opaque pipeline without the discard for materials that
cut nothing out, tried before a depth pass, after item 2's parts f and c.

Review: item 8 measured, 2026-10-12; the hidden land costs the pass its geometry and not its
fragments, the device testing depth early under the discard already, 0.34 to 0.42 ms of vertex and
raster work for 289 columns of sections inside the frustum, so the opaque pipeline goes in for the
devices that gain and the land's cost is the game's to remove by culling the sections the eye cannot
see, a flood fill through the sections' open faces from the eye's as Minecraft's visibility graph
does, measured on this room; an occlusion culling in the engine waits on that measure.

### 2026-10-11, the voxel game in `3DEngine.Game`: colors written in place miss global illumination

On `3c7fe822`, a closed room of white concrete 11 by 6 by 11 inside (`voxel.room 10 4 -22 22 11 -10
white_concrete` on a flat world) built at hour 23, the game's light levels and corners then turned
off (`voxel.shade off off`), which the game gives the room's sections as new colors written by
`UpdateMeshBuffer` into their color stream, their vertices kept, and a glowstone placed at 16, 5,
-16; the eye at 16.5, 5, -11.8 facing north 25 degrees down, High; a hidden window of 1280 by 720
and an RTX 4070 Laptop GPU. Each figure is a region's mean linear light in the frame, as
`gi.compare` reads it beside a reference of 1024 paths a pixel.

From 300 frames after the lamp to 2,100 the frame's floor reads 0.099, 0.060 and 0.020 and its far
wall 0.035, 0.021 and 0.007. A block set outside each of the room's four columns and taken away
again makes each section upload its mesh anew, the room's blocks and light as before, and from 300
frames on the floor reads 0.130, 0.081 and 0.027 and the wall 0.072, 0.047 and 0.016, a third
brighter and twice as bright, and stays so. The reference does the same: its far wall reads 0.002
before the uploads and 0.043 after, and whole sections are black in its picture where the room was
dark at their last upload. Global illumination and the reference keep a mesh's colors from its
last upload and take none that `UpdateMeshBuffer` writes, while the model pass draws the new ones.
The game writes colors in place whenever a block's light changes, so a lamp placed in a dark room
bounces off walls as dark as they were before it, until a block changes in their section.

After the uploads the frame agrees with a reference of 8 bounces within 3% on the floor and 2% on
the far wall for the glowstone, and 5% and 20% for an end rod in its place, the vertex colors of
`3c7fe822` in both; against 2 bounces it reads 22% and 31% over, the bounces past the second in a
white room. The end rod's step is gone at `3105e73e`, the largest drop toward it along rows 370 to
460 of the frame 0.6% of the light where 8.5%.

The field's colors and the ray-traced scene's follow a color stream written in place since
`feec07a2`, and the game writes its sections' colors in place with their packed bytes on each change
of light, measured below.

The vertex stream of the game's own, at `b275f738` against the commit before it, each built from a
clean worktree on `3ec1fdb3`'s engine, and `b275f738`'s game again on `86fba7ad`'s engine, each
corner of a section being its position and eight packed bytes and a relight writing the packed bytes
alone. In this entry's room with the game's light levels on and the glowstone placed, 294 sections
and 117,054 triangles on the flat world and 1,344 sections and 781,834 triangles on seed 1's land
around the same place, the device's mesh buffers from `./e3d command memory.buffers`, the frame and
its `gpu.window_depth`, `gpu.shadows` and `gpu.hdr_scene` from `./e3d command profile` after 300
frames, and the relight as the time from `voxel.shade off off` until no section waits to mesh, which
shades every loaded section anew. The flat world is read twice each, before and after by turns; on
the land the desktop took up to a fifth of the GPU at the start of the pair, and another program's
process up to a sixth during `86fba7ad`'s read. The lines of passes give the window's depth, the
shadows and the scene in that order, in milliseconds.

| Measure | Before | `b275f738` | `86fba7ad`'s engine |
|---|---|---|---|
| Vertex bytes on the device, flat | 13.62 MB, 887 buffers | 9.82 MB, 1,178 | 6.16 MB, 590 |
| Vertex bytes on the device, land | 87.07 MB, 4,100 buffers | 63.24 MB, 5,462 | 39.56 MB, 2,732 |
| Frame, flat | 5.13 to 5.17 ms | 3.96 to 4.49 ms | 4.13 to 4.29 ms |
| Passes, flat | 0.92, 0.84 to 0.93, 1.45 to 1.48 | 0.42, 0.30, 1.10 | 0.43, 0.39, 1.16 |
| Frame, land | 34.1 ms | 18.0 ms | 20.5 ms |
| Passes, land | 6.0, 18.2, 7.2 ms | 2.9, 7.8, 4.8 ms | 2.8, 7.3, 4.5 ms |
| Relight of every section | 0.34 to 0.38 s | 0.34 to 0.40 s | 0.33 to 0.36 s |

With `b275f738` on `3ec1fdb3`'s engine a section uploads 72% of the vertex bytes it did, the colors
the field reads going to the device as well with a buffer of eight bytes of zeros for the second
texture coordinate, 32 bytes a vertex in four buffers. On `86fba7ad`'s engine, which keeps a mesh of
positions' colors on the CPU for the field alone, it uploads 45%, its positions and packed bytes, 20
bytes a vertex in two buffers, as expected. The passes that read positions alone, the window's depth
and the shadows, take half to a third of their time with the stream, and the frame on the land half
or near it; the bytes saved since change the frame little, and the relight is the mesher's work and
is unchanged.

On `feec07a2`'s engine, where colors written into a mesh in place reach the field and the ray-traced
scene in the frame after, the game writes a relit section's colors at 3 beside its packed bytes.
Read against the same build writing the packed bytes alone, each from a clean worktree, in this
entry's room with `voxel.shade off off` as the relight before the glowstone, a hidden window of 1280
by 720 at High. The floor is `gi.compare`'s region `white block 2, top` and the far wall `white
block 4, side facing +z`, each the red of its mean linear light, the other two in proportion, beside
a reference of 1024 paths and 8 bounces traced 300 frames after the lamp and again after the four
blocks have each section upload anew.

| Read | Floor, frame and reference | Far wall, the same | Section 1's floor, reference |
|---|---|---|---|
| Packed bytes alone, 300 and 2,100 frames | 0.098, 0.099 | 0.034, 0.002 | 0.002 |
| Packed bytes alone, after the uploads | 0.129, 0.128 | 0.072, 0.073 | 0.108 |
| Colors written as well, 300 and 2,100 frames | 0.129, 0.128 | 0.072, 0.073 | 0.108 |
| Colors written as well, after the uploads | 0.129, 0.128 | 0.072, 0.073 | 0.108 |

With the colors written the frame and the reference read from the relight what an upload gives them,
within 1% of each other, and no section of the reference stays black. The cost, in the same room lit
and settled, of one section relit in place, section 1, 0, -1 shaded again alone with the light
levels flipped through `./e3d eval` and its faces kept, against one uploaded anew, a block set or
cleared at 19, 5, -13 in it, each read by `./e3d command profile` from a reset before the change to
a read two frames after it, about four frames, five of each in turns with five of nothing, in two
runs. The desktop's gnome-shell took 34 to 44% of the GPU through the first run and up to 19%
through the second, so the lines are ranges over both, lowest to highest, and their medians, in
milliseconds, the field's `scene_field` and the indirect light's `global_illumination` on the CPU
and on the GPU.

| Frames of | Field, CPU | Field, GPU | Bounce, CPU | Bounce, GPU |
|---|---|---|---|---|
| Nothing | 0.126-0.243, 0.164 | 0.003-0.004 | 0.373-0.976, 0.475 | 1.539-1.653, 1.579 |
| A relight | 0.135-0.308, 0.164 | 0.004 | 0.363-1.317, 0.466 | 1.557-1.927, 1.577 |
| An upload | 0.156-0.675, 0.190 | 0.017-0.023 | 0.429-0.550, 0.482 | 1.555-1.589, 1.577 |

A relit section reads as a frame of nothing on every line, its repainting under what the profile
tells apart, where an upload's field reads 0.015 ms of the GPU more over the frames, near 0.06 in
all, and the first upload of each run 0.4 to 0.5 ms of the CPU more. Over 1,200 frames after each
kind the lines read alike, the field 226 meshes still and none stamped.

Review: item 9 of REVIEW.md, 2026-10-11; a write to a color stream marks the mesh's colors stale,
the field repainting its cells' colors and the ray scene its corners in the frame after, before item
8.

Review: the engine's vertex stream of the program's own, item 20 of REVIEW.md, done at `3ec1fdb3` on
2026-10-11, is the game's to take as its first user, which the owner asked for at 20:05 and refined
at 22:55: a section's mesh becomes positions alone and four or eight packed bytes a vertex,
`UploadMesh(positions, indices, MeshStream.From(format, packed), colors)`, twelve bytes of position
on the GPU and the packed bytes beside, where a vertex costs 44 today. The packed bytes hold the
face at 3 bits, the corner at 2, the shade at 8 and the tint, a `UInt2` keeping the tint at 8 bits a
channel with room to spare, 20 bytes a vertex, and a `UInt` holding it at 6 bits a channel, 16
bytes, which may band in a biome's gradient, the choice the owner's eye's (Decision 27), `UInt2`
unless the look is the same; the position stays a `Vector3`, so wheat, the lantern and water keep
their shapes. `terrain.slang`'s stage takes `float3 position : POSITION, uint2 packed : CUSTOM0,
ModelInstance instance`, unpacks the normal from a six-entry table and the coordinate from a
four-entry one, the color from the tint and the shade, and calls the streams overload of
`transformModelVertex` with a zero `uv2`; a relight is `UpdateMeshBuffer(mesh, MeshStreamBuffer,
packed, 0)`, which writes the packed bytes alone through a ring, and the positions' equality decides
a new upload against a relight; the colors stay in the upload for the field's tint, which reads the
color stream, so this entry's fault stands as it is until item 9. The packed bytes ride in the
mesh's stream and not in a `ShaderBuffer` read by the vertex's index, since the stream travels with
each section's mesh and every draw reads it with nothing else bound, where a buffer is bound to the
shader; a value is four bytes or a `uint` in names, comments and the guide. Measured before and
after with `./e3d command profile` on this entry's room, the bytes a section uploads, the relight's
time and the frame's, written here in this entry's style, and `MesherTests` rewritten for positions
and packed bytes. When the game's own order allows.

Review: item 21 of REVIEW.md, 2026-10-12; the colors of a mesh of positions alone serve the field
alone and reach no buffer on the device, so a section is 20 bytes a vertex there, before the
pillar's read; the land measured again after it.

### 2026-10-18, the voxel game in `Voxel-Sandbox`: no particle keeps its place on a turning sky

The game's stars are fixed points on a sky that turns with the hour about the axis from north to
south, the sun on one side of it and the moon on the other, and are asked for as a particle effect
parented to the sun's and the moon's places. Read on the 3DEngine 5.2.43 package, built from
`fc5fdc97`. `CreateParticleEmitter` makes an entity with a `ParticleEmitter` and a `Transform`, and
`Particles.Run` (`3DEngine/Rendering/Particles/Particles.cs:52`) takes the translation of its world
matrix alone, so the emitter's rotation and its parent's turn reach no particle. A particle starts
at a random point within `Radius` of that place and is stepped on the GPU in world space, so one
already born keeps its place in the world as the emitter turns, and none starts where the program
names. For the stars the engine lacks two things:

- an emitter whose particles live in its entity's space, moving and turning with its transform and
  its parent's, so a star field parented to the sky turns with it
- particles placed at positions the program gives in that space, living until the program removes
  them, so each star keeps its place from frame to frame and from run to run

Meanwhile the game draws each star with `DrawBillboardPro` in `Sky.Draw`
(`VoxelSandbox/Rendering/Sky.cs`), 500 fixed ways on the sky turned by one matrix a frame, of which
those above the horizon, about half, are drawn. On a flat world in a hidden window of 1280 by 720 on
the RTX 4070 Laptop GPU, facing the moon 25 degrees up, `./e3d command profile` over about a second
read `program.drawing` at 2.08 and 1.82 ms at hour 20 with the stars and 1.76 and 1.77 ms at noon
without them, and `gpu.immediate` at 0.000 ms in both. The billboards cost the frame little, and the
entry asks for the stars to be the engine's particles rather than for time. The game moves them to
particles once a package brings the parented emitter.

Review: item 32 of REVIEW.md, 2026-10-18; an emitter whose particles live in its entity's space,
moving and turning with its transform and its parent's, and particles placed where the program says
and living until it removes them, after item 31 and before item 30's parts; the game moves its stars
to it when a package brings it.

### 2026-10-18, the voxel game in `Voxel-Sandbox`: the sun's contact shadows cannot be turned off

The game draws the sun's shadows pixelated. Each point of a face takes its light at the middle of
the block's pixel it lies in, a sixteenth of a block on a side, so a shadow's edge steps along the
pixels (`VoxelSandbox/resources/shaders/terrain.slang`). Read on the 3DEngine 5.2.43 package, built
from `fc5fdc97`, the sun's light at a point is the shadow map's share of it times the contact
shadow's (`modelpass.slang:583`). The shadow map's share (`cascadeReached`, `lights.slang:73`)
averages nine nearest comparisons a texel apart. At the game's shadow distance of 96 blocks the
cascades end at 7.7, 28.8 and 96 blocks, and `ShadowFit.TryFit` for the game's view of 70 degrees at
16 by 9 gives them texels of 0.0066, 0.024 and 0.079 blocks, 0.11, 0.38 and 1.27 of a block's pixel
of 0.0625. In the two near cascades the nine comparisons span less than a block's pixel, so the
stepped edge is as sharp as the pixel, and the look near the player needs neither a filter switch
nor a texel size. Past 29 blocks the far cascade's texel, a little over a block's pixel, steps the
edge by texels rather than by pixels, a lesser thing where a block's pixel is about a pixel of the
window.

The contact shadow (`sunContact`, `modelpass.slang:293`) is traced toward the sun through the
scene's distance field in the occlusion pass (`ao.slang`) at half the window's size, out to 32
cells of the finest cascade, 8 blocks at the game's cells of 0.25, blurred with the occlusion and
read by the fragment's place in the window, so the game's snapped point does not reach it. It is on
wherever the field is built and the sun casts shadows (`AmbientOcclusionRenderer.ContactShadows`),
and the package turns it off only with the field, which global illumination needs. On a flat world
in a hidden window of 1280 by 720 on the RTX 4070 Laptop GPU, a pillar of stone one block across and
six high (`voxel.fill 0 4 -10 0 9 -10 stone`) under the sun of 8.30 (`voxel.time 8.5`), seen from
above looking down 70 degrees, casts a hard shadow 208 pixels across a row of the frame. Beside it
the contact shadow darkens the grass from 90 to 150 pixels past the hard edge, 0.4 to 0.7 of a
block, a soft fringe along the stepped edge.

The engine lacks a setting that turns the sun's contact shadows off, or leaves them out where the
shadow map holds the point, so that a game keeps the field and global illumination with hard
shadows. Meanwhile the game's shadows carry the fringe, and the game turns the contact shadows off
once a package brings the setting.

Review: item 33 of REVIEW.md, 2026-10-18; a setting that turns the sun's contact shadows off, or
leaves them out where the shadow map holds the point, so a game keeps the field and global
illumination with hard shadows, after item 32; the far cascade's texel is noted under it as the
lesser thing; the game turns the contact shadows off when a package brings the setting.

### 2026-10-18, the voxel game in `Voxel-Sandbox`: an ImGui window cannot draw a program's texture

The game draws every item's icon once into an atlas, 16 pixels an icon, which its hotbar draws as a
texture with `DrawTexturePro` and its inventory window, drawn with ImGui, would draw with
`ImGui.Image` or `ImDrawList.AddImage`. Read on the 3DEngine 5.2.43 package, built from `fc5fdc97`,
and the same at the engine's `29eb2eca`, `ImGuiRenderNode.Run` binds the font atlas's set once
before its loop over the draw commands (`3DEngine/Gui/Vulkan/ImGuiRenderNode.cs:172`) and draws
every command with it, `pcmd.TextureId` read by none, so an image of a program's texture draws a
part of the font atlas in its place. The engine lacks a texture id for a `Texture2D` that the ImGui
pass binds for each command drawn with it, as ImGui's own Vulkan backend binds a set for each.

Meanwhile the inventory window draws each icon's pixels as `AddRectFilled` rectangles, a row's
stretch of one color each, 11,087 for the 97 icons the creative window shows at once. On a flat
world in a hidden window of 1280 by 720 on the RTX 4070 Laptop GPU, `./e3d command profile` over two
seconds, read with the window open and then closed four times, put `program.drawing` 0.53, 0.63,
0.78 and 1.15 ms higher with it open and `cpu.imgui` 0.09 to 0.12 ms higher, and `gpu.imgui` at
0.015 ms. The game at `344ef5b`, whose window drew a square of one color for each item, read
0.01, 0.12, 0.21 and 0.71 ms and 0.01 ms the same way, so the rectangles cost the frame about half a
millisecond while the window is open. The game moves the window onto the atlas texture once a
package brings the texture id.

Review: item 34 of REVIEW.md, 2026-10-18; a texture id for a `Texture2D` that the ImGui pass binds
for each command drawn with it, after item 33; the game moves its inventory window onto its atlas
when a package brings it.

### 2026-10-19, the voxel game in `Voxel-Sandbox`: a draw through a program's own shader is drawn by every pass

The game draws each section of its land as a mesh through its own shader
(`VoxelSandbox/resources/shaders/terrain.slang`), whose vertex stage reads a face's normal, corner
and color from a stream of its own and keeps the mesh's positions where they are. Read on the
3DEngine 5.2.92 package, built from `33fd0804`, the model pass culls a batch's instances by their
boxes a block of 64 at a time in each pass, the camera's, the depth's, each shadow cascade's and each
light's, and gives a batch drawn through a shader of the program's own no blocks
(`3DEngine/Rendering/Models/ModelRenderer.Instances.cs:108`), which every pass then draws whole
(`3DEngine/Rendering/Models/ModelRenderer.Culling.cs:104` and `:115`). On seed 1's overworld at the
spawn at noon looking east, rendered offscreen at 1280 by 720 on the RTX 4070 Laptop GPU with no
other program on it, once every column in reach was meshed, `./e3d command models.draws` read the
draws of each pass at each render distance in columns of 16 blocks:

| columns | camera | cascade 0 | cascade 1 | cascade 2 | depth |
|---|---|---|---|---|---|
| 4 | 199 | 179 | 181 | 187 | 181 |
| 8 | 627 | 563 | 565 | 571 | 565 |
| 12 | 1,326 | 1,198 | 1,200 | 1,206 | 1,200 |
| 16 | 2,251 | 2,021 | 2,022 | 2,028 | 2,022 |

The nearest cascade holds the first 7.7 blocks of the game's shadow distance of 96, and draws as
many sections as the camera. `./e3d command profile`, read over four seconds with the frame paced at
60 a second, put the frame and the passes' GPU time, in milliseconds, at:

| columns | sections | frame | gpu | shadows | depth | main pass | global illumination | scene field |
|---|---|---|---|---|---|---|---|---|
| 4 | 580 | 16.7 | 4.7 | 0.47 | 0.56 | 1.66 | 1.83 | 0.004 |
| 6 | 767 | 16.7 | 9.3 | 3.37 | 1.08 | 2.39 | 2.28 | 0.004 |
| 8 | 1,253 | 16.7 | 12.1 | 4.32 | 1.94 | 3.65 | 2.07 | 0.004 |
| 9 | 1,539 | 17.2 | 16.6 | 6.46 | 2.73 | 5.22 | 2.00 | 0.004 |
| 10 | 1,799 | 21.7 | 21.8 | 9.55 | 3.44 | 6.28 | 2.30 | 0.004 |
| 12 | 2,484 | 29.7 | 29.7 | 14.03 | 4.89 | 8.38 | 2.24 | 0.005 |
| 16 | 4,140 | 46.7 | 46.4 | 23.52 | 7.65 | 12.93 | 2.11 | 0.004 |

The shadows and the depth pass grow with every section within the render distance, past the 96
blocks the shadows cover, where global illumination and the scene field stay level, and they hold
the game to 8 columns at 60 frames a second. On lavapipe the GPU's work read 584 ms a frame at 4
columns and 923 ms at 8, the shadows' 59 and 132 ms of it.

The engine lacks a way for a draw through a program's own shader to be culled by its mesh's box,
which a program whose vertex stage keeps the mesh's positions, as the terrain's does, would ask for.
Meanwhile the game keeps its render distance at 8 columns until a package brings the culling.

Review: item 36 of REVIEW.md, 2026-10-19; a program may say of a shader that its vertex stage keeps
the positions, and a batch drawn with it is then culled by its mesh's box in every pass as the
engine's own are, after the sampling term's commit; the game keeps 8 columns until a package brings
it.

### 2026-10-21, the voxel game in `Voxel-Sandbox`: a shader buffer is read back only by holding the program

The game is to compute its columns' noise fields on the GPU and place the blocks from them on the
CPU, where its bodies, edits, light levels and saves read the blocks, so each batch of columns comes
back through `ReadShaderBuffer`, whose remark says it waits for the dispatches submitted and holds
the program until the GPU has caught up, which a game reading every frame pays for each time
(`3DEngine/Api/E3D.Compute.cs:113`). Read on the 3DEngine 5.2.190 package, built from `b3922179`, on
seed 1's overworld at noon from above the forest at the spawn looking east, 12 columns drawn and
every one meshed, offscreen at 1280 by 720 on the RTX 4070 Laptop GPU with no other program on it,
a fragment sent by `./e3d eval -f` between frames dispatched a compute shader writing 128 KB, a
column's 32,768 cells at four bytes each, and read it back at once, timed by a stopwatch on the main
thread, eight times five frames apart. The dispatch's call took 0.60 to 0.83 ms after the first,
which made its pipeline, and the read 1.08 to 1.71 ms, every read giving the bytes the shader wrote.
`./e3d command profile` over 240 frames there put the frame at 16.9 ms, 4.8 ms of it the wait for
the next, so a read a frame takes up to a third of what the frame has left.

Flying, the frame has nothing left. Three ten-second flights east over land not yet loaded, read by
`./e3d command profile` after `profile.reset` on the 5.2.155 package in the same place and size,
put the frame at 18.3 to 18.9 ms at 8 columns and 21.4 to 28.8 ms at 12, the program's update 5.1
to 5.8 ms and 10.2 to 14.0 ms of it, where it is 2.5 to 2.7 ms standing, the growth the columns
joined to the world and the sections meshed (Voxel-Sandbox's README, What it costs). A read with a
wait in each of those frames adds its wait to them.

The engine lacks a readback asked for in one frame and collected in a later one without a wait,
which a game generating on the GPU and placing blocks on the CPU asks for. Meanwhile the game
generates its columns on the CPU on worker tasks (`VoxelSandbox/World/ChunkStreamer.cs:65`).

Review: item 68 of REVIEW.md, 2026-10-21; a readback asked for and collected later,
`ReadShaderBufferAsync` answering a `ShaderReadback` handle with a fence of the submission,
`IsShaderReadbackReady` answering without waiting and `GetShaderReadback` copying the bytes out once
ready, the staging buffers pooled, with the compute guide's section on when a game reads with a wait
and when without; taken after item 67 and before the modeling work, a package due after item 69.

### 2026-10-21, the voxel game in `Voxel-Sandbox`: a mesh reaches the screen only from arrays on the CPU

The game meshes each section of 16 by 16 by 16 blocks on the main thread under a budget of 4 ms a
frame (`VoxelSandbox/VoxelGame.cs:14`) and gives its faces to the engine by `UploadMesh` and
`UpdateMeshBuffer` (`VoxelSandbox/Rendering/ChunkRenderer.cs:57` and `:67`), the positions, the
indices, the colors and the terrain shader's stream of eight bytes a corner. A mesher in compute
would write those into shader buffers, and a mesh reaches the screen only from arrays on the CPU,
so they would be read back with `ReadShaderBuffer`'s wait, 1.08 to 1.71 ms for 128 KB as the entry
above read it, and uploaded again, the GPU's work crossing to the CPU and back.

Read as the entry above says, the three flights at 12 columns put the frame at 21.4 to 28.8 ms and
the program's update at 10.2 to 14.0 ms, of which the meshing takes up to its 4 ms a frame, the
update's 2.7 ms standing a part, and the columns' join the remainder, and the engine's render graph
at 6.7 to 9.2 ms, where it is 4.5 to 5.0 ms flying at 8 columns and 5.3 to 5.5 standing at 12.

The engine lacks a mesh drawn from buffers a compute shader wrote, with a count of vertices the
shader writes, so a dispatch and a draw share the buffers with no copy and no wait, which a program
meshing in compute asks for. Meanwhile the game meshes on the CPU.


Review: item 69 of REVIEW.md, 2026-10-21; a mesh over shader buffers,
`LoadMeshFromShaderBuffers(positions, stream, count, capacity)`, drawn by an indirect draw whose
vertex count the shader writes into the count buffer's first four bytes, a barrier before the vertex
stage, culled by a box `SetMeshBoundingBox` sets and read by the global illumination as any mesh,
with an example `shaders_compute_mesh`; taken after item 68, a package due after it.

### 2026-10-21, the voxel game in `Voxel-Sandbox`: global illumination leaks through an edge, steps at its edges, and costs the model pass more without the screen's probes

The owner judges the screen's probes pixelated and the cascades without them the better look for
the game, and sees a light block's light leak through the gap where two columns meet at a corner,
penumbrae on the ground with zig-zags, the screen's probes shifting as the eye moves, points where
the light and the blocks' faces fight, and noise in the engine's bounce rooms 3 to 6. Each was read
at the game's `28eb41c` on the 3DEngine 5.2.190 package, built from `b3922179`, with `voxel.gi
high`, the game's default, its four cascades and its screen's probes every 8 pixels, offscreen at
1280 by 720 on the RTX 4070 Laptop GPU, on the flat world opened by `./e3d open
VoxelSandbox/bin/Debug/net10.0/VoxelSandbox --offscreen --transient --flat`, after `voxel.cycle
off`, `voxel.hud off`, `voxel.clouds off`, `voxel.fly on` and `voxel.kill all`, and again after
`gi.toggle screen off`, which leaves the screen's probes out.

**The leak through an edge.** `voxel.room 10 4 -22 22 11 -10 stone`, a wall that steps a block east
halfway, `voxel.fill 16 5 -21 16 10 -16 stone` and `voxel.fill 17 5 -15 17 10 -11 stone`, so its two
halves meet only along the edge at x 17, z -15, a glowstone west of it, `voxel.set 15 5 -14
glowstone`, at hour 23 with the game's light levels off, `voxel.shade off off`, so the engine's light
alone shows, and the eye east of the wall, `voxel.tp 21.2 5 -20.2` and `voxel.look 225 -32`. Light
comes through the edge and fans over the floor and the wall east of it, though no ray between the
two halves passes but through the edge itself. Over 500 by 400 pixels about the edge the brightest
reads 29 sRGB levels with the screen's probes and 43 without, the mean 0.10 and 0.11. With the edge
closed, `voxel.fill 16 5 -15 16 10 -15 stone`, they read 0 and 3, the mean 0 and 0.006. So the
leak is the cascades' and stays without the screen's probes
([capture, eight times as bright](https://raw.githubusercontent.com/EggyStudio/Voxel-Sandbox/main/.github/assets/gi/leak.webp)).

**The zig-zag edges.** The leaked light's edges zig-zag in steps along the floor, wavy with the
screen's probes and in square steps without them. A lamp's shadow shows the same: a glowstone on a
block, `voxel.set 40 4 -30 stone` and `voxel.set 40 5 -30 glowstone`, a pillar two blocks tall
three blocks east, `voxel.fill 43 4 -30 43 5 -30 stone`, at hour 23, seen from straight above,
`voxel.tp 45.5 9 -29.5` and `voxel.look 90 -70`, its shadow's edges on the grass wavy with the
screen's probes and stepped without them
([capture, contrast stretched](https://raw.githubusercontent.com/EggyStudio/Voxel-Sandbox/main/.github/assets/gi/penumbra.webp)).

**The shift as the eye moves.** In a closed room of white concrete, `voxel.room 10 4 -22 22 11 -10
white_concrete`, with `voxel.set 16 5 -16 glowstone`, at hour 23, the eye at `voxel.tp 16.5 5
-11.8` and `voxel.look 0 -20`, `voxel.flicker 160 0 0.5 20 <file>` stepped half a block over 20
frames. Each frame changed from the one before by 0.28 sRGB levels on the mean while it moved with
the screen's probes and 0.26 without, and over the 30 frames after, 0.0014 with them, 0.0085 at
most, and nothing without. Put half a block over at once, the frame after and the frame 60 later
differ by a level at most with the screen's probes and by none without. Over seed 1's spawn at noon,
flying at 96 looking east over the forest, two blocks east over 60 frames changed each frame by 0.51
levels with the screen's probes and 0.49 without, and nothing after either way. So in these scenes
the screen's probes move the light by little that a frame's change shows, and the owner sees the
shift in play.

**Points where the light and the faces fight, and noise.** Neither was seen in the scenes above.
In the lamp room, still for 150 frames, each frame changed from the one before by 0.0002 levels on
the mean with the screen's probes and by none without, no pixel by more than 8. The owner saw the
noise in the engine's own bounce rooms 3 to 6.

**The model pass's cost without the screen's probes.** A closed room of stone, `voxel.room 0 4 -30
22 11 -16 stone`, forty glowstones on its floor two blocks apart in five rows of eight, x 5 to 19
and z -28 to -20, at hour 23, the eye at `voxel.tp 11.5 9 -17.2` and `voxel.look 0 -35` with all
forty in view, `gi.state` carrying the forty as lights. `./e3d command profile` over 240 frames after
`profile.reset`, three times each way, with the frame paced at 60 a second and gnome-shell taking
about a fifth of the GPU, put `gpu.hdr_scene` at 1.79, 1.82 and 1.81 ms with the screen's probes and
12.95, 12.96 and 12.86 ms without them. The frame held 16.7 ms with them and took 20.1 to 20.3 ms
without them, and `gpu.global_illumination` stayed at 6.6 to 6.8 ms either way. So leaving the
screen's probes out, for forty light blocks in view, costs the model pass eleven milliseconds a
frame
([capture](https://raw.githubusercontent.com/EggyStudio/Voxel-Sandbox/main/.github/assets/gi/forty.webp)).

The engine lacks a way to leave the screen's probes out that a program calls, light kept from passing
through an edge where two solid cells meet, edges of indirect light that do not step with the
cascades' cells, and glow lights whose cost in the model pass does not grow with the light blocks in
view. Meanwhile the game keeps the screen's probes, as at High, and changes nothing in its light.


Review: item 71 of REVIEW.md, 2026-10-21; `SetGlobalIlluminationScreenProbes` first, then the glow
lights' cost in the model pass bounded by a form that does not grow with the lights in view, a list
of the glow lights a screen tile sees or a coarse glow pass without the probes, right after it since
the game waits on both; then, after the compute asks and the animated capture, the edge leak, the
stepped edges, the noise and the shift, each reproduced in the engine's own scenes and mended under
the rule that no view's distance to its reference widens, or named where the method stops.

### 2026-10-21, the voxel game in `Voxel-Sandbox`: a lantern's or a torch's light lies in blotches

The owner sees what read as fireflies in the game's captures of a room lit at night, soft round
blotches of light about 40 pixels across standing still on the ceiling and the walls. Each was read
at the game's `6845654` on the 3DEngine 5.2.190 package, with `voxel.gi high`, the game's default,
offscreen at 1280 by 720 on the RTX 4070 Laptop GPU, on seed 1 opened by `./e3d open
VoxelSandbox/bin/Debug/net10.0/VoxelSandbox --offscreen --transient`, after `voxel.cycle off`,
`voxel.still on`, `voxel.hud off`, `voxel.clouds off`, `voxel.fly on` and `voxel.kill all`. The room
is built in the air, `voxel.room 300 100 300 308 105 308 oak_planks`, lit by a lantern hanging from
the middle of its ceiling, `voxel.set 304 104 304 hanging_lantern`, and two torches on its far
walls, `voxel.set 307 102 304 wall_torch_west` and `voxel.set 304 102 307 wall_torch_north`, at hour
21, the eye in a corner, `voxel.tp 301.5 101.4 301.5` and `voxel.look 135 -4`, after 240 frames.
`gi.state` carries 141 small emitters as lights, the 64 nearest the eye lit.

The blotches are read on the flat ceiling either side of the lantern, in strips of 520 by 90 pixels
from 40, 15 and 720, 15, each pixel's luminance less a Gaussian blur of the strip of 20 pixels, as
the largest excess in sRGB levels and as how many spots of 40 pixels or more stand 4 levels or more
over the blur.

| light | spots | largest excess |
|---|---|---|
| high, as the game plays | 8 | 10 |
| the same 60 frames later | 8 | 10 |
| high, `gi.toggle screen off` | 3 | 11 |
| medium | 3 | 9 |
| low | 5 | 18 |
| off | 0 | 2 |
| high, the lantern alone | 4 | 8 |
| high, the two torches alone | 5 | 11 |

A glowstone in the lantern's place with the torches taken away, a whole emissive block, leaves the
ceiling smooth in the capture, though the strips reach the block itself and so read 48. Still for
120 frames, `voxel.flicker 120 0 0 20 <file>`, each frame changed from the one before by 0.02 to
0.06 levels on the mean and 0.009 percent of the pixels by more than 8, so the blotches stand still
rather than flicker. So they are global illumination's light from the small emitters, the flames
and the lantern's glass, with or without the screen's probes, and a whole emissive block's light
lies smooth
([capture](https://raw.githubusercontent.com/EggyStudio/Voxel-Sandbox/main/.github/assets/gi/blotches.webp)).

The engine lacks indirect light from a small emitter that lies as smooth on the surfaces around it
as a whole emissive block's. Meanwhile the game keeps its lanterns and torches as they are and
changes nothing in its light.


Review: item 71 of REVIEW.md, 2026-10-21, its second part's first fault left after the glow lights'
bound; the blotches reproduced in the engine's own scene, a room with a small emitter near its
ceiling, read to their cause in the probes that gather a small emitter's light near a surface, in
the cascades and the screen's probes alike, and mended under the rule that no view's distance to its
reference widens, or named where the method stops; after item 70.

### 2026-10-21, the voxel game in `Voxel-Sandbox`: a masked surface drawn through a program's shader casts a whole quad's shadow

The owner asks for the plants' shadows, the grass's blades and the flowers stepping with the pixels
of the sun's map, which fit the game's look. The game's plants are a mesh of flat walls drawn
through its own shader, `VoxelSandbox/resources/shaders/terrain.slang`, with a `ModelMaterial` of
`AlphaMode` Mask and `BouncesLight` off, its fragment stage cutting each blade out by its tile's
alpha in an atlas the shader declares itself. Read at the game's `fc38219` on the 3DEngine 5.2.190
package, offscreen at 1280 by 720 on the RTX 4070 Laptop GPU, on the flat world, `voxel.world flat
1`, after `voxel.still on`, `voxel.hud off`, `voxel.fly on`, `voxel.kill all` and `voxel.clouds
off`, with Bare Bones on. A row of nine plants, `voxel.set 0 4 -6 short_grass` and on along x to 8
with tall grass, a poppy, a dandelion, a fern, a cornflower, short grass, an oxeye daisy and a blue
orchid, and a row of short grass at z -8, at hour 7.5, seen from `voxel.tp 4.5 7 -2.5` and
`voxel.look 0 -50`.

With `CastsShadows` on, each plant's shadow on the grass is its two walls' whole quads, dark shapes
a block wide with straight edges, and not its blades. It stays so with the material's `Texture` set
to the atlas and the vertex stage handing `transformModelVertex` each wall's corner in its tile's
cell of the atlas, 0 and 1 across and down from the lower left corner round, so the shadow pass had
the texture and the coordinates a masked material's own draw gives it
([capture](https://raw.githubusercontent.com/EggyStudio/Voxel-Sandbox/main/.github/assets/gi/plant-shadows.webp)).
So the shadow pass does not cut a masked surface drawn through a program's own shader out of the
sun's map.

The engine lacks a masked surface's cut out of the shadow maps for a draw through a program's own
shader, by the program's own fragment stage or by the material's texture at the coordinate its
vertex stage gives. Meanwhile the plants cast no shadow, as before.


 Review: item 79 of REVIEW.md, 2026-10-21; the shadow pass cuts a mask material's surface by its
texture's alpha at the cutoff for a draw through a program's own shader, by the program's own
fragment stage for the shadow where it gives one or by the material's texture at the coordinate the
vertex stage gives, for the sun's cascades and the spot and point lights alike; after item 73.
Shipped in 5.2.215 and taken by the game at `d7c4892` on 2026-10-24, the terrain shader cutting each
wall's shadow by its tile's alpha, at 2.3 ms of GPU a frame on the development machine.

### 2026-10-21, the voxel game in `Voxel-Sandbox`: the sky's light on the scene cannot be set apart from the sky drawn

The owner asks for the game's light to look as the owner's modpack's shader, Aurora's Shaders after
Complementary, draws its light with the owner's settings. Its ambient light is the sky's color to
the power of 0.65 times 0.85, whitened, and its AMBIENT_MULT of 50 against a default of 150 leaves a
surface in full shade at noon 0.55 of that and the default 1.45. Read at the game's `fa7fba0` on the
3DEngine 5.2.190 package, offscreen at 1280 by 720 on the RTX 4070 Laptop GPU, with the default
packs on, on the flat world, `voxel.world flat 1`, after `voxel.still on`, `voxel.hud off`,
`voxel.fly on`, `voxel.kill all` and `voxel.clouds off`. A wall of cobblestone, `voxel.fill 0 4 -10 0
11 0 cobblestone`, at hour 9, its east face in the sun from `voxel.tp 6.5 6 -4.5` and `voxel.look 270
0` and its west face in shade from `voxel.tp -5.5 6 -4.5` and `voxel.look 90 0`, each face's mean
over 200 by 200 pixels at the window's middle after 240 frames.

| | sunlit face, sRGB | shaded face, sRGB | shade to sun |
|---|---|---|---|
| the modpack's picture 0, the same cobblestone | 146, 112, 94 | 45, 43, 53 | 0.38 |
| the game, its sun warmed as the shader colors it | 165, 151, 140 | 49, 70, 93 | 0.45 |

The shade-to-sun ratios are of sRGB luminance; in linear light the modpack's is 0.15. So the game's
shade is lighter and far bluer than the shader's, its sky's light the sky's own blue at full
strength. `SetEnvironmentMap`'s intensity lowers the light and the sky drawn together. The sky
looking up at hour 9 reads 81, 137 and 218 at an intensity of 1 and 59, 102 and 170 at 0.5, so the
game cannot lower or whiten its sky's light without darkening the sky the player sees.

The engine lacks a way to set the sky's light on the scene apart from the sky drawn, its strength
and its tint or saturation, as `SetEnvironmentLight(intensity, tint)` would. Meanwhile the game
lights with its sky as it draws it, and takes the shader's share and whiteness when the call comes.

Review: item 81 of REVIEW.md, 2026-10-21; `SetEnvironmentLight(intensity, tint)`, the share and the
tint of the environment map's light on the scene with the drawn sky unchanged, read by the model
pass and the global illumination's sky term alike; after item 80.

### 2026-10-21, the voxel game in `Voxel-Sandbox`: the frame's curve cannot be the one the owner's modpack's shader draws

The owner asks for the game's frame to be graded as the owner's modpack's shader, Aurora's Shaders
after Complementary, grades it with the owner's settings. Its BSL tonemap, read as numbers, takes a
light c times its exposure, then c / (c^w + 1)^(1/w) with w its white curve, then c to the power of
its lower curve in the darks to its upper in the lights, mixed by the square root of c, then gamma
1/2.2, then each channel times its saturation less the channels' mean times the saturation less 1.
The owner's settings are an exposure of 0.90, a white curve of 1.3, a lower curve of 1.05, an upper
curve of 1.00 and a saturation of 1.05, against the shader's defaults of 1.40, 2.0, 1.20, 1.50 and
1.55. In sRGB levels at lights of 0.05, 0.1, 0.25, 0.5, 1, 2 and 4, the curve reads 0.230, 0.317,
0.475, 0.620, 0.763, 0.874 and 0.941. The nearest of the engine's curves, each at its best exposure,
is Reinhard's at 1.05, 0.023 off on the mean of the squares' root, then ACES's fit at 0.54, 0.055
off, and a cut at 1 at 0.64, 0.062 off.

The engine lacks a curve a program sets by the four numbers, or a tonemap pass through a program's
own shader. Meanwhile the game tonemaps by Reinhard's curve at an exposure of 1.7 with
`SetColorGrading(1.12, 1.4, white)`, the nearest of three grades of a plains village set beside
the modpack's picture, since the shader's warmer light and deeper shade saturate its frame more
than its own saturation of 1.05 does.


Review: item 80 of REVIEW.md, 2026-10-21; `SetTonemapCurve(exposure, white, lower, upper)` in the
shader's form beside the other tonemaps, and `SetTonemapShader(shader)` running a program's own
fragment stage over the frame in the tonemap's place; after item 79.
