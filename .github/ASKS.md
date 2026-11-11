# What the games ask of the engine

A game built on this engine, in `games` or beside it, finds where the engine falls short while it is
made, a cost it cannot afford, a thing it cannot do or a thing that is wrong. The session making the
game writes that here, one entry a finding, and the session that writes REVIEW.md reads this file,
turns an entry into an item of REVIEW.md's Now list by its weight, and writes the item's number back
under the entry. The engine's working session reads REVIEW.md, as it always has, and not this file.
An entry stays until its item is done and the game has read what came of it, and is then removed by
the session that wrote it.

An entry has a heading with the date, the game and the finding in a line, and under it what was
measured and how, with its numbers, the command (`./e3d command profile`), the scene, the resolution
and the GPU; what the engine lacks or spends; what the game does meanwhile; and, written by the
reviewing session alone, a line beginning `Review:` with the item it became or the answer. Prose
follows STYLE.md, and no entry names a person (NORM.md's rule 4.7).

## Entries

### 2026-10-10, the voxel game in `3DEngine.Game`, its tests build on every run but run on none

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

### 2026-10-11, the voxel game in `3DEngine.Game`, land behind a wall is lit in any order

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
no less, 1.40 to 1.43 ms nearest first against 1.35 to 1.38 ms kept with the glow lights off, and
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
turns, `./e3d command profile` over 300 frames, or over a walk of 14 blocks, in three scenes, this
entry's room as above, a cave carved in a hill of stone at noon on the flat world, from 50, 6, -30
to 60, 10, -20 in stone from 40, 4, -40 to 70, 40, -10, and a cave of seed 1's hills at -28, 34, -32
walked east at noon. The passes are the window's depth, the model pass, the sun's shadows and the
light that bounces, in milliseconds.

| Scene, culling | Sections left out | Depth | Model pass | Shadows | Bounce | Frame |
|---|---|---|---|---|---|---|
| This room, off | 0 of 226 | 0.416 | 1.115-1.117 | 0.218-0.220 | 1.413-1.419 | 3.7-4.3 |
| This room, on | 64 of 226 | 0.301 | 0.983-0.990 | 0.167-0.168 | 1.392-1.401 | 3.9-4.1 |
| Carved cave, off | 0 of 243 | 0.451 | 1.188-1.192 | 0.205-0.224 | 1.532-1.533 | 3.9-4.1 |
| Carved cave, on | 241 of 243 | 0.006 | 0.617 | 0.042 | 1.057-1.060 | 2.1 |
| Hills' cave, off | 0 of 1,090 | 2.743-2.752 | 4.640-4.704 | 6.876-6.930 | 1.351-1.363 | 16.2 |
| Hills' cave, on | 905 of 1,085 | 0.257 | 0.656-0.660 | 0.060-0.073 | 1.095-1.102 | 3.5-4.2 |

In this room the flood leaves out 64 sections, 0.30 ms of the window's passes and the shadows, of
the 0.34 to 0.42 the land costs, since the flat world's open air outside the walls shares the room's
sections, and a section's faces are joined as wholes, so the flood passes out through them to the
land around. Underground it leaves out nearly all, five sixths of the frame in the cave of the
hills. The light the eye sees does not change. That cave lit by a glowstone reads the same with
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

### 2026-10-11, the voxel game in `3DEngine.Game`, colors written in place miss global illumination

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
brighter and twice as bright, and stays so. The reference does the same. Its far wall reads 0.002
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

### 2026-10-18, the voxel game in `Voxel-Sandbox`, no particle keeps its place on a turning sky

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

### 2026-10-18, the voxel game in `Voxel-Sandbox`, the sun's contact shadows cannot be turned off

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

### 2026-10-18, the voxel game in `Voxel-Sandbox`, an ImGui window cannot draw a program's texture

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

### 2026-10-19, the voxel game in `Voxel-Sandbox`, a draw through a program's own shader is drawn by every pass

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

### 2026-10-21, the voxel game in `Voxel-Sandbox`, a shader buffer is read back only by holding the program

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

### 2026-10-21, the voxel game in `Voxel-Sandbox`, a mesh reaches the screen only from arrays on the CPU

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

### 2026-10-21, the voxel game in `Voxel-Sandbox`, global illumination leaks through an edge, steps at its edges, and costs the model pass more without the screen's probes

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
screen's probes and in square steps without them. A lamp's shadow shows the same. With a glowstone
on a block, `voxel.set 40 4 -30 stone` and `voxel.set 40 5 -30 glowstone`, a pillar two blocks tall
three blocks east, `voxel.fill 43 4 -30 43 5 -30 stone`, at hour 23, seen from straight above,
`voxel.tp 45.5 9 -29.5` and `voxel.look 90 -70`, its shadow's edges on the grass are wavy with the
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

### 2026-10-21, the voxel game in `Voxel-Sandbox`, a lantern's or a torch's light lies in blotches

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

### 2026-10-21, the voxel game in `Voxel-Sandbox`, a masked surface drawn through a program's shader casts a whole quad's shadow

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

### 2026-10-21, the voxel game in `Voxel-Sandbox`, the sky's light on the scene cannot be set apart from the sky drawn

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

### 2026-10-21, the voxel game in `Voxel-Sandbox`, the frame's curve cannot be the one the owner's modpack's shader draws

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

### 2026-10-24, the voxel game in `Voxel-Sandbox`, a small emitter's light gives black pixels, which the screen's probes keep and spread

The README's pictures are taken again at a field of view of 40 degrees (REVIEW.md item 46), and the
view of seed 1's desert village draws most of its land pure black. It was read at the game's
`edbc3b2` on the 3DEngine 5.2.215 package, with `voxel.gi high`, offscreen at 1280 by 720 on the
RTX 4070 Laptop GPU, driver 615.78.08, the game opened as `build/pictures.sh` opens it, with its
three default packs, `voxel.world overworld 1`, `voxel.still on`, `voxel.hud off`, `voxel.fly on` and
`voxel.fov 40`, the eye's feet at 728.8, 69, -678.2, `voxel.look 315 -18`, hour 17, 240 frames after
the place is meshed. `gi.state` carries 56 small emitters as lights, the village's torches, the 56
nearest the eye lit.

Each capture's pixels darker than 1 percent are counted, with ImageMagick's `-threshold 1%`.

| light | black pixels |
|---|---|
| high, the field of view 70 | 303 |
| high, the field of view 40 | 215,459 |
| the same, 600 frames later | 596,201 |
| `voxel.gi off` and then `high` again | 215,442 |
| then the field of view 70 again | 407,389 |
| `gi.toggle again off`, after the black | 215,445 |
| `gi.toggle history off`, after the black | 320 |
| `gi.toggle glow off` as well | 36 |
| `gi.toggle glow off` from the start, the field of view 40 | 0 |
| `gi.rays off`, `gi.toggle filter off` or `gi.toggle again off` from the start | 215,442, 215,434 and 215,429 |
| `voxel.gi off` | 0 |

With the screen's probes' history off, a few hundred pixels are black each frame, and with the small
emitters' lights left out too, nearly none are, so the black comes from the light of a small emitter,
and the frame before's light in the screen's probes keeps it and spreads it over the land, as a
value that is not a number spreads through every blend it enters. Neither turning global illumination off and on
again nor widening the view clears what the history holds, and a teleport out of the place does.
The engine lacks a guard that keeps a light's result from reaching the history when it is not a
number, and whatever gives it at this view. The game takes this view from another place meanwhile,
where a run shows none black.

Review: item 90 of REVIEW.md, 2026-10-24; the cause of the non-number at that view mended at its
source, and the screen probes' history and the glow-only pass taking a non-finite light as none, so
a fault left anywhere darkens one texel for one frame and never spreads; first after the batch in
hand.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, the scene pass spends a third of its time reading global illumination's cascades per pixel

The game's frame at the owner's settings, render distance 12, global illumination High, the three
default packs and the screen's probes off, is bound by the GPU, and the scene pass is most of it.
Read with `./e3d command profile` over 300 frames at an unlimited rate, the Release build at
`2263618` on the 5.2.215 package, offscreen on the development machine's RTX 4070 Laptop GPU, seed 1's
cherry grove from -736, 79, -960 facing 345 and pitched -12 at hour 16, 1683 draws and 1.18 million
triangles.

| resolution, light | frame | scene pass | global illumination | shadows | window depth |
|---|---|---|---|---|---|
| 1280 by 720, High | 14.9 | 8.3 | 2.6 | 2.3 | 1.5 |
| 1920 by 1080, High | 18.6 | 11.6 | 2.9 | 2.2 | 1.6 |
| 1920 by 1080, off | 11.9 | 6.0 | 0 | 3.0 | 2.3 |

At 1920 by 1080 turning global illumination off takes the scene pass from 11.6 ms to 6.0, so 5.6 ms
of the scene pass is the model pass reading the cascades' probes at each pixel, twice the 2.9 ms the
cascades themselves take to trace. The plains village from 550.5, 72, -356.5 reads the same, its
scene pass 13.3 ms at 1920 by 1080 with the light on. The pass would hold the frame under 16.7 ms
at 1920 by 1080 with that read at a third of its cost. The engine lacks a cheaper way for a pixel to
take the cascades' light, a coarser read, one shared by the pixels of a tile, or one made once a
frame into a screen-sized image the scene pass samples. The game draws as it does meanwhile, and
cuts its own costs, the sun's shadow taken once a pixel in its terrain shader and a shorter shadow
distance, by the reading under its own item 93.

Review: item 96 of REVIEW.md, 2026-10-24; the scene pass's per-pixel read of the cascades' light
made cheaper than the trace, by a coarser read with a bilateral upsample, a gather shared per tile,
or the light written once a frame into a screen-sized image, whichever the reading keeps under the
covered-fault rule; after item 94.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, a point light at the camera's own point shades black specks on lavapipe

A point light standing at the camera's own point, a torch held at the eye, shades the pixels it
lights with black specks on lavapipe, while the RTX 4070 Laptop GPU draws them clean. Read on the
5.2.215 package with the validation layer on and no error from it, the flat world at hour 20 from
the roof of a stone room at 13.5, 12, -19 looking down at -60, with `voxel.pixel 13.5 12.001 -20.5`.
The roof reads 27, 25, 39 by moonlight and 0, 0, 0 with the torch held, where the GPU reads 109, 68,
29, and the frame is lit orange and shot through with black specks everywhere the light reaches.

| on lavapipe, the torch held | the roof's pixel |
|---|---|
| at the eye | 0, 0, 0, specked |
| at the eye, its shadow off | 0, 0, 0, specked |
| at the eye, `ContactShadows.Off` | 0, 0, 0, specked |
| at the eye, global illumination off | 0, 0, 0, specked |
| a third of a block under the eye | 114, 71, 29, clean |

So the specks are neither the light's shadow cube nor the contact shadows nor global illumination,
and moving the light off the camera's point clears them. The engine lacks a guard for a light at
the camera's own point, whatever term divides by its distance there. The game keeps its held light
a third of a block under the eye meanwhile, still at the player's own place.

Review: item 97 of REVIEW.md, 2026-10-24; the cause read on lavapipe with the validation layer, a
guard at the degenerate value as item 90's, a test in the Linux suite lighting a room by a point
light at the camera's point; after item 89.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, the sun's shadow cannot be read at a point of the program's choosing within `litLight`

The game's terrain shader steps the sun's shadow along an eight-by-eight grid on each face, as the
owner's modpack's shader does, by taking `litLight` and then replacing its sun's share with the
sun's light read at the middle of the step (`litStepped` in `resources/shaders/terrain.slang`). So
it reads the sun's shadow three times a pixel within the map, once inside `litLight`, once at the
point to take that share back out, and once at the step's middle. Read with `./e3d command profile`
over 300 frames at an unlimited rate, the Release build at `3236b56` on the 5.2.215 package,
offscreen at 1920 by 1080 on the RTX 4070 Laptop GPU, seed 1's cherry grove from -736, 79, -960
facing 345 at hour 16, two runs of each in turn:

| terrain shader | frame | scene pass |
|---|---|---|
| the shadow stepped | 19.5, 19.7 | 12.5, 12.7 |
| the stepping left out | 17.8, 18.0 | 10.9, 11.2 |

The two extra reads and the sun's light worked out again cost the scene pass about 1.6 ms. The
engine lacks a way to tell `litLight` the point the shadow light's shadow is read at, or to leave
the shadow light out of it, so a program that places the sun's shadow itself reads it once. The game
keeps its stepped shadow meanwhile, since copying `litLight` into its shader would hold a copy of
the engine's lighting apart from the engine's changes to it.

Review: item 99 of REVIEW.md, 2026-10-24; `litLight` takes the shadow light's shadow from the
caller, a parameter with the share read at the program's point or an overload leaving the shadow
light out, after item 98 and before 96.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, the scene's field holds leaves solid, so a canopy's floor takes none of the sky's light

The game's leaves are a masked surface whose texture is clear in places, and the source lets the
sky's light through a canopy, so a rain forest's floor at noon reads light level 13 of 15 there, 0.80
of open ground's light on the screen. The scene's distance field holds a leaf's cell as solid as
stone, so the cascades' rays toward the sky stop at the canopy and the floor under it takes none of
the sky's light. Read with `./e3d command
voxel.pixel` from straight above at noon, the Release build at `aaeaa2a` on the 5.2.215 package,
offscreen at 1280 by 720 on the RTX 4070 Laptop GPU and on lavapipe alike, on the flat world with a
roof of jungle leaves 21 by 21 and two deep five blocks over the grass and a stone wall 11 long and
11 high on open ground, the luminance of the grass in sRGB levels:

| grass | sky level | luminance |
|---|---|---|
| sunlit on open ground | 15 | 172.9 |
| in the wall's shade on open ground | 15 | 42.2 |
| under the roof of leaves | 13 | 20.2 (19.5 on lavapipe) |

Seed 1's rain forest from -934.5, 58.2, -1343.5 facing 200 at noon reads a mean luminance of 4.0 of
255 over the whole capture, its floor near black. The engine lacks a way to let a share of light
through a cell, marked as foliage by the draw that stamps it, the cascades' march taking that share
off a ray as it crosses such a cell rather than stopping there, as the source lets the sky's light
through leaves. Meanwhile the game's terrain shader keeps a face's light no less than the sky's light
from all around at the face's sky level on the source's brightness curve (`skyFloor` in
`resources/shaders/terrain.slang`), which lifts the grass under the roof to 42.4 and leaves the wall's
shade and the sunlit grass as they were, a floor that stays under the engine's light once the field
lets light through leaves.

Review: item 100 of REVIEW.md, 2026-10-24; a cell marked as foliage carries a share of light that
passes it, the cascades' march taking that share off a ray rather than stopping, after item 96 and
before 93.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, the sun's map lights a deep cave's block tops and their edges at 45 blocks on 5.2.281 as on 5.2.215

Read with `./e3d command voxel.pixel` at the game's `758e9df`, a Release build on the 3DEngine
5.2.281 package beside one at `9a3d746` on 5.2.215, offscreen at 1280 by 720 on the RTX 4070 Laptop
GPU, with the game's three default packs, `voxel.world overworld 1`, render distance 12, the screen
probes off, the eye's feet on the floor of a cave at -59.5, -4, -91.5, 73 blocks under the ground,
`voxel.look 0 -30`, 150 frames after each change. The tops of deepslate blocks 45 blocks ahead are
read at -65.5, 1, -136.5 and of stone blocks 90 blocks ahead at -45.5, 2, -180.5, each in sRGB levels,
the same on both packages to the level:

| hour | global illumination | shadow distance | 45 blocks | 90 blocks |
|---|---|---|---|---|
| 12 | high | 96 | 35, 29, 20 | 18, 20, 22 |
| 12 | high | 150 | 35, 29, 20 | 9, 13, 18 |
| 12 | off | 96 | 38, 38, 43 | 25, 36, 53 |
| 12 | high | 1 | 0, 0, 0 | 9, 13, 18 |
| 0 | high | 96 | 0, 0, 0 | 0, 0, 0 |

At the shadow distance of 1 the game's contact shadows past the map hold the tops dark, and at
midnight there is no sun, so the light on them at 96 and 150 is the sun's through the map, the
ground 70 blocks above the tops left out of what the map holds for that view. The one-pixel lines of
light on the tops' edges and down their sides there are the map's too, read for the game's item 119.
Over the frame's band at 480, 0, 420 by 90 the pixels lit above 24 levels number 4,496 to 4,500 with
the shadows at 96 and 3,775 to 3,779 at 1, the lines and the lit tops gone, and moving the eye by a
fraction of a pixel, 0.0005 to 0.0015 of a block, moves none of them, where cracks between faces
would come and go with the eye. The game's faces each lie on the block grid and share their corners,
which a test of its mesher holds. So the engine lacks a map that keeps the casters above a deep view
in its depth, or reads a face's edge rows against the map's texels, as item 83 meant to.

Review: item 101 of REVIEW.md, 2026-10-24; the sun's map keeping the casters above a deep view, read
on the game's own build at the place, with 94's test widened; first.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, the far cascade's coarse cells take the sky's light from walls near the ground and leave a cave's light as it was

Read at the game's `758e9df` on the 3DEngine 5.2.281 package, offscreen at 1280 by 720 on the RTX
4070 Laptop GPU, the far cascade set through `./e3d eval "SetSceneField(4, 0.25f, 2, farCascade: <d>)"`
over the game's own field of four cascades of 0.25. At `float.PositiveInfinity` `field.state` gives
the far cascade 73.75 blocks a cell, at 2048 37.75, at 1024 19, at 512 9.5 and at 256 4.75.

- The game's `build/play.sh`, its flat world at noon, two panes of a pack's glass with a gray frame
  before a wall of red concrete. The frame reads 197, 195, 194 in sRGB levels without the far cascade
  and 197, 192, 144 with it at the far plane, the blue of the light from the sky taken. A flat scene
  of the same wall and glass with no pack reads 186, 98, 98 without it, 182, 89, 57 at the far plane
  and 186, 97, 86 at 512.
- The deep cave of the entry above reads the same at every far distance and without it, its tops
  at 45 blocks 35, 29, 20 and at 90 blocks 18, 20, 22, so the far rays of that cave meet nothing the
  far cascade adds.
- Seed 1's sunlit plains by its village read the same, 137.3, 168.9, 40.1 over the lower half of the
  frame either way.
- With `./e3d command profile` over 300 frames at an unlimited rate, at 1920 by 1080 at seed 1's
  cherry grove from -736, 79, -960 facing 345 at hour 16, global illumination at High, the far
  cascade at the far plane costs the scene pass about 5 ms, 29.4 against 24.2.

So the far cascade's cells, as wide as reaching the far plane makes them, hold the ground about
the eye as a slab many blocks thick and darken the low sky a wall beside it sees, and the engine
lacks a far cascade that keeps the sky over flat ground, or a reach a game can bound by what it
draws. The game leaves the far cascade off meanwhile.

Review: item 102 of REVIEW.md, 2026-10-24; the far cascade read only by a ray that leaves the
counted cascades, its near cells taking no part within the counted reach, the cost read; after item
101.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, a test cannot run a behavior's systems through the engine's frame

Read at the game's `5113176` on the 3DEngine 5.2.281 package, where a stack lying in the world became
an entity with an `ItemDrop` behavior and a `Body`, its systems `[OnPreUpdate] Fall`,
`[OnUpdate] PickUp` and `[OnPostUpdate] Gather`. `App.AddPlugin` and `App.Frame` are internal in the
package, and `Engine3D.UseApp` with them, so a test in the game's own project can build an
`EcsWorld` but cannot add `BehaviorsPlugin` to an `App` and step a frame, and no test reads the
order of the stages or a resource reaching a system as the game's frame gives it. The engine lacks
a public way for a test to step an app's schedule a frame at a time without a window. The game
gives each system a twin that takes the `EcsWorld` and its inputs as arguments, which the tests call
in the stages' order by hand, 288 tests in about 14 s.

Review: item 104 of REVIEW.md, 2026-10-24; a test builds an app with its plugins and steps its
frames without a window through the package's public surface; after item 101.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, a query reaches three components at most

`EcsWorld.Query` takes one to three component types (`3DEngine/Ecs/EcsWorld.API.cs`, `Query<T1, T2,
T3>`). The stacks need two, an `ItemDrop` and a `Body`, and the mobs to come carry a body, a kind,
health, an aim, a path and a pose, read together each tick. The engine lacks a query over four or
more components, or one whose further components are read by `GetRef` at no lookup's cost. The game
will query the two or three read most and fetch the rest by `GetRef` per entity meanwhile.

Review: item 105 of REVIEW.md, 2026-10-24; Query over up to six components, or a tuple of any count,
with the same iteration and ref access; after item 104.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, a behavior cannot hold a reference to data no entity changes

E3D006, a warning the game's strict build fails on, refuses a field of a reference type in a
behavior, since entities run in parallel could change it at once. A stack's item is a class the game
keeps one of for each item and never changes while it plays, so the stack holds the item's id and
looks it up in `Items.All` at each read. The engine lacks a way to say a field's reference is read
only, as an attribute on the field or a type the generator accepts as immutable, so data shared and
never written can sit in the struct. The game keeps ids meanwhile.

Review: item 106 of REVIEW.md, 2026-10-24; the reason read and said, then the analyzer allowing a
reference to an immutable record or the guide saying the id pattern is the way; after item 105.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, an entity cannot be found by a key of the game's, as the cell a fire burns in

Read at the game's tree after `299aa33` on the 3DEngine 5.2.283 package, where each fire became an
entity with a `Fire` behavior of its cell, age and wait. The voxel world names every cell set to fire
each frame, those the fires spread themselves among them, and the game must start a fire's entity
only where its cell has none. The ECS finds an entity by its id alone, so the game gathers every
fire's cell into a set each frame the world names one, a walk over all the fires, 41 of them four
blocks of planks into a burn in an offscreen flat world, and hundreds in a forest. The engine lacks a
lookup of an entity by a key a component holds, kept as the component changes, as a cell, a column
or a chunk, which the mobs will need too for their columns. The game walks the fires meanwhile and
keeps no index of its own.

Review: item 107 of REVIEW.md, 2026-10-24; an index the world keeps for a component type that names
its key, with a query by key and a contains; after item 106.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, the engine's mesh entities cannot carry the game's sections

Read on the 3DEngine 5.2.283 package and the engine's source as of its main after `e5ad8d28`, to turn
each meshed section into an entity the engine draws, as the game's item 95 asks. The game draws its
sections through `DrawMesh` with its own Slang shader, `resources/shaders/terrain.slang`, which reads
the texture atlas, the fog, the animation table and each vertex's packed light and corner shade from
a `MeshStream` of two uints a vertex, under six materials, opaque, cutout, cutout glass, solid
cutout, molten and see-through, at about 1,200 sections drawn on a flat world at render distance 12
and up to 4,400 held in a flight over seed 1's cherry grove.

- The ECS `Material` has no shader (`Components/Material.cs`), and `MeshEntityDraws.Build` sets no
  shader, uniforms or shader textures on the draws it makes, so a section drawn as an entity loses
  its atlas, fog, light and shade.
- The ECS `Mesh` holds positions, normals and uvs, three positions a triangle, so the packed stream
  and the vertex colors the flat `UploadMesh` overloads take have no place, and each section would
  be uploaded at about three times its indexed size.
- A look is kept per mesh and material, and past 4,096 looks or groups every look is forgotten and
  built again each frame (`MeshEntityDraws.cs`, `MaxLooks`). The sections' parts pass that at the
  game's render distance.
- A mesh no entity drew in a frame, as a section replaced when a block is placed or broken, frees
  its mesh and forgets every look, so the next frame builds every entity's again on the main thread.
- No component hides an entity from the camera's pass, or keeps it in the shadow maps alone, so the
  sections the game's flood leaves unseen, item 90's, and the shadow draw of the engine's item 101
  have no form for entities.
- The draws of mesh entities are counted in the engine's internals and `models.draws` alone, so the
  game's play check of 400 draws of sections at the land's edge cannot read them as a profile value.

The engine lacks a mesh entity drawn through a program's shader with its own vertex streams and
indices, kept across a mesh replaced or a look count of some thousands without building every
entity again, with a component that leaves it out of the camera's pass or keeps it in the shadow
pass alone, and its draws counted where a program reads them. The game draws its sections through
`DrawMesh` meanwhile and holds item 95 until a package carries these.

Review: item 108 of REVIEW.md, 2026-10-24; the mesh entity path carrying a program's shader and
streams, looks and meshes kept, Visible and ShadowOnly components and the draws in the profile;
after item 107.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, a behavior's system cannot ask to run after or before another in its stage

Read on the 3DEngine 5.2.283 package and the engine's source, splitting the game's frame into
behaviors for the game's item 96. The schedule orders a stage's systems by `SystemDescriptor.After`
and `Before` and otherwise by the order they were added, and shuffles that order by a seed so a
system that leans on an order nothing asked for meets another (`Core/Schedule/Schedule.Order.cs`,
`Schedule.Shuffle.cs`), but no behavior attribute reaches `After` or `Before`, and `InState` is one to a
method (`AllowMultiple = false`). The game's update holds ifs whose order is its rule, so the block
aimed at is found before the hand works on it, the player moves before either, and the vitals are
taken in survival before the mobs answer. Each of those split into a system of its own would run in an order
no one asked for, and the controller cannot move into the update stage after the states' transition
while it must come before the aim. The engine lacks an attribute naming the systems a behavior's
system runs after or before, as `[After(nameof(Steer))]` on a static system, and more than one state a
system is gated on. The game keeps the ordered ifs inside one update system meanwhile, and gates by
states and run conditions only the systems whose order does not matter.

Review: item 110 of REVIEW.md, 2026-10-24; After and Before attributes on a behavior's system and
InState more than once on a method; after item 109.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, the sun's shadows have one softness at every distance from their caster

Read on the 3DEngine 5.2.283 package for the game's item 43, the look of a warm low sun with long soft
shadows. The package sets the sun's shadows by their reach, their fill and their map's size
(`SetShadowDistance`, `SetShadowFill`, `SetShadowMapSize`) and filters each the same few texels about
its point, so a tree's shadow at its trunk and the same shadow's tip twenty blocks off at a low sun
have one edge, where the sun's disc widens an edge with the distance from the caster to the ground,
as the source's shader packs draw it. The engine lacks a penumbra that widens with that distance, as
percentage-closer soft shadows find it from the blockers' depth, with a setting of the sun's angular
size and a way for a program to set it or turn it off, and the sharpness kept near the caster so the
blocks' edges read. The game keeps the engine's one softness meanwhile.

Review: item 111 of REVIEW.md, 2026-10-24; a penumbra widening with the distance from the caster as
percentage-closer soft shadows give it, SetShadowSoftness with a default keeping the present look;
after item 100.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, a flat surface of water mirrors nothing of the shore or the sky

Read on the 3DEngine 5.2.283 package for the game's item 42, water that mirrors the sky and the shore,
offscreen at 1280 by 720 on the RTX 4070 Laptop GPU with the default packs. The game's water is its
terrain shader's see-through material, blended, at a roughness of 1 since its `10cf3f3`. Set to a
roughness of 0.05 in a live session, as glossy as the engine traces for (`tracedReflection` under
0.5), the water of seed 1's river by its plains village, from 550.5, 72, -356.5 facing 45 and
pitched -17, reads the same at noon and at 17 as the rough water beside it, the shore and the sky
showing in none of it, and a pool of the flat world beside a wall of red concrete at noon reads 143,
128, 183 where the wall's mirror image would lie, a faint purple of the wall's red through the
water's blue rather than the wall, the trace through the frame before's picture and the field's
coarse cells giving no image of either. The box probes (`CreateReflectionProbe`) suit a room and not
open water. The engine lacks a planar reflection at a height a program sets, the scene drawn from
the camera mirrored in that plane at a lower resolution with its shadows and light reused and given
to a material as a texture, usable from the transparent pass, as the source's shader packs mirror
calm water. The game draws its water with the sky's color by the view's angle, the bed through it
darkened with depth and a soft edge meanwhile, and keeps the shore's mirror for that package.

Review: item 112 of REVIEW.md, 2026-10-24; a planar reflection at a height a program sets, drawn
from the mirrored camera at a lower resolution and given to the material as a texture, usable from
the transparent pass; after item 111.

### 2026-10-24, the voxel game in `Voxel-Sandbox`, a behavior's context gives no wheel

Read on the 3DEngine 5.2.283 package for the game's item 35, a zoom whose wheel narrows the view
while its key is held, in the controller's behavior that reads the frame's input from its context.
`BehaviorContext.Input` gives the keys (`KeyDown`, `KeyPressed`) and the mouse's position and
movement (`MousePosition`, `MouseDelta`), and keeps the wheel's `WheelX` and `WheelY` internal
(`Core/Input/Input.cs`), which the flat API's `GetMouseWheelMove` reads. The engine lacks the wheel
on the context's input, as the keys and the mouse's movement are. The behavior reads the wheel
through `GetMouseWheelMove` meanwhile.

Review: item 113 of REVIEW.md, 2026-10-25; the context's input gaining the wheel's move; after item
110.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, an emitter holding no particles costs the frame as much as a full one

Read on the 3DEngine 5.2.283 package for the game's item 32, the rain's splashes, the falling leaves,
the cave's dust and the wakes. An emitter bursts where it stands in the frame after `EmitParticles`,
so a kind bursting at several places a frame needs several emitters, and the game first held twenty
alive, all laid over by alpha, most of them empty most of the time. `./e3d command profile` read
`cpu.particles` 3.5 ms and `gpu.particles` 2.1 ms at the forest's spawn of seed 1 with every
density at none, so not one particle alive, against 0.005 and 0.000 ms with no emitter, since
`Particles.Step` dispatches each emitter's step and its sort and `Draw` draws its whole
`MaxParticles` whether it holds any or not. The engine lacks an emitter that costs nothing while it
holds no particles, or a burst given its own place (`EmitParticles(emitter, count, at)`), so one
emitter serves a kind. The game makes an emitter as a burst needs one and unloads it once its last
particles have died, at most three of a kind, which reads 0.04 ms and 0.03 ms in the rain.

Review: item 114 of REVIEW.md, 2026-10-25; the step, the sort and the draw taking the alive count;
after item 108.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, a particle always faces the camera

Read on the 3DEngine 5.2.283 package for the game's item 32, the rings a raindrop leaves on water.
`particles.slang` turns every particle's square toward the eye, so a ring stands up off the water
rather than lying on it, and `ParticleEmitter` has no way to lay its particles in a plane. The
engine lacks particles facing a direction the emitter gives, as up for a ripple or a decal. The game
draws the rings as a mesh of flat squares it builds each frame meanwhile, each showing the frame of
a sheet its age has reached.

Review: item 115 of REVIEW.md, 2026-10-25; an emitter's facing, the camera, a fixed normal or the
velocity's way; after item 114.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, the profile shows a program's value as its last frame's

Read on the 3DEngine 5.2.283 package for the game's item 32. `profile` averages the frame's times
over about a second, and a value given through `SetProfileValue` is shown as it was last set, as
`FrameProfile.Set` says, so a count of what a frame gave off, a leaf now and then, read 347 in one
call and 0 in the next two, 13 frames apart. The engine lacks a program's value averaged as its own
times are, which `SetProfileValue` could be asked for by a flag. The game eases its particle counts
over about a second before it gives them meanwhile.

Review: item 116 of REVIEW.md, 2026-10-25; a profile value with a window showing its mean; after
item 115.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, a key names one entity, so the mobs of a column cannot be found by it

Read on the 3DEngine 5.2.291 package for the game's item 94, its mobs' column bookkeeping.
`EcsWorld.Index<T, TKey>` keeps one entity to a key, a second given the same key taking the first's
place, which suits a fire's cell. The mobs share their columns, a herd of animals or a village's
people standing in one, and as a column unloads the game must find the mobs standing in it to keep
them for the save, and gathers every column's mobs as it saves. At the flat world's spawn of seed 1
`voxel.mobs` read 214 mobs loaded, each column that unloads walking all of them. The engine lacks an
index whose key names many entities, as a column names its mobs or a section its drops, kept as the
key changes. The game walks the mobs for their column meanwhile, which costs little at these counts.

Review: item 117 of REVIEW.md, 2026-10-25; a grouping index enumerating the entities under a key
without allocating; after item 116.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, a voice that has played out stays in the audio server until it is stopped

Read on the 3DEngine 5.2.291 package for the game's item 33, its steps. `AudioServer.Play` keeps
each voice it starts in its table until `AudioSource.Stop` takes it out (`AudioServer.Stop` is the one
`_voices.Remove`), and `Tick` walks the whole table each frame, working out each spatial voice's pan
and volume and setting them on the backend. A one-shot sound, as a step, a hit or a click, is played
and forgotten by a game, so the table grows by one for each. `voxel.sounds` read 34 steps played and
36 voices in a hidden run of the game in a forest after 1,800 frames, each of them walked every
frame from then on. The engine lacks a voice that is let go once it has played out, unless it loops,
or a word on `Play` that a caller must stop every voice it starts. The game stops each of its steps
half a second after it began meanwhile.

Review: item 118 of REVIEW.md, 2026-10-25; a played-out voice let go when its source ends, Tick
walking the playing ones; after item 117.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, a voice played to SDL's dummy driver never says it has played out

Read on the 3DEngine 5.2.291 package in the same run, hidden, whose log says the device was opened
through `dummy`, which "takes it at the rate it plays and plays none". The game stopped each step whose
`AudioSource.IsPlaying` read false, and the voices still grew with the steps, 6 steps and 7 voices
after 1,800 frames, so `SdlAudioBackend.IsVoicePlaying` kept reading the stream's queued or available bytes as
more than none for a sound of a third of a second. The engine lacks a dummy device that drains its
voices' streams as a real one does, so a hidden or offscreen run reads a voice's end as the shown one
does. The game times its steps itself meanwhile.

Review: item 119 of REVIEW.md, 2026-10-25; the dummy driver ending a one-shot voice as a device
does; after item 118.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, a voice cannot be dulled behind blocks nor given a room's reverb

Read on the 3DEngine 5.2.291 package for the game's item 33, the occlusion and the reverb. A voice's
`AudioVoiceParams` and its `AudioSource` set its loudness, balance, pitch, loop and place, and the
spatial processor its fall with distance and its directivity, and nothing filters it. The game
counts the opaque cells a line from the listener to a mob's step crosses and halves the loudness for
each, 0.5 through a wall of one block and 0.125 through three in its tests, 0.002 for a pig nine blocks
into the rock under seed 1's plains, where the source's sound mods also dull it, by a low-pass whose
cutoff falls with the blocks crossed. It reads the room about the listener by 26 rays to 24 blocks,
0.54 of them meeting a block, at 4.8 blocks on average, on seed 1's plains at -172, 62, 12, 0.88 and 2.2
under the forest's canopy at the spawn, and 0.92 and 7.8 in the deep cave at -59.5, -4, -91.5, for a
reverb whose wet share and decay would follow them. The engine lacks a low-pass on a voice set by its
cutoff, and a reverb on a voice or on the mix set by its wet share, its room's size and its decay. The
game lowers the loudness alone meanwhile and says the room in `voxel.sounds`.

Review: item 120 of REVIEW.md, 2026-10-25; a low-pass by cutoff and a reverb by wet share, size and
decay on the mixer; after item 119.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, a mesh over shader buffers carries one stream, so the scene's field loses a section's colors

Read on the 3DEngine 5.2.291 package for the game's item 29, its sections meshed in compute through
the engine's item 69. The game uploads each section's opaque faces with two streams beside their
positions and indices (`VoxelSandbox/Rendering/ChunkRenderer.cs:67`), the terrain shader's eight
bytes a corner as a `MeshStream` of `UInt2`, and a color a corner, its surface's darkened by its
shade, which the scene's distance field reads as each block's color, so a lamp's light bounces green
off grass and red off red concrete. A mesh over shader buffers holds its positions and one stream
(`MeshStore.Counted`), and the field takes colors from that stream only where its format is
`UNorm8x4`, as item 69's conversion reads it, so a section meshed in compute with its packed stream
would give the field no colors and every block would bounce the material's white. A section's
faces vary from none to some thousands, and a mesh over shader buffers holds whole buffers, so each
section would hold buffers as large as its fullest case, or the game would count its faces on the
CPU first, which is half the meshing.

Read with `./e3d command profile` after `profile.reset`, three ten-second flights east at sprint over
land not yet loaded on seed 1 at render distance 12, offscreen at 1280 by 720 on the RTX 4070 Laptop
GPU, on the game's tree after `1d31dda` with the main thread's meshing given as `update.mesh` and its
taking in of columns as `update.stream`, both eased over about a second. The frame took 22.0, 37.5
and 37.0 ms, `update.mesh` 1.0, 7.8 and 6.9 ms, `update.stream` 0.9, 1.9 and 2.4 ms,
`cpu.scene_field` 6.8, 8.6 and 9.3 ms, and the GPU's passes about 12 ms, so the main thread binds
the frame and the meshing is the largest part of it the game owns.

The engine lacks a mesh over shader buffers with a color stream beside its stream of the program's
own, which the field reads as it reads an uploaded mesh's colors, and one over a range of a shader
buffer, an offset and a capacity, so a program meshing thousands of sections in compute keeps them
in a few large buffers. The game meshes on the CPU meanwhile and holds the meshing part of its item
29 until a package carries these.

Review: item 121 of REVIEW.md, 2026-10-25; a color stream beside the program's own on a mesh over
shader buffers and the mesh over a range of a shared buffer, the field reading both; after item 102.

### 2026-10-25, the voxel game in `Voxel-Sandbox`, a mesh entity casts the sun's shadow and stands in the scene's field whatever its material

Read on the 3DEngine 5.2.313 package's source at `073f34c8` for the game's item 95, its sections as
mesh entities. A section's faces are seven meshes by how they are drawn, and four of them are drawn
through a `ModelMaterial` that leaves out a part of the frame. The see-through glass and ice, the
water and the glass a pack cuts out set `CastsShadows` false, so the sun passes through a window and
the scene's distance field, which holds a see-through mesh as solid, keeps the indirect light coming
through it, and the plants a pack cuts out set `BouncesLight` false, so the field holds no grass
(`VoxelSandbox/Rendering/ChunkRenderer.cs:170`). The ECS `Material` holds neither, and
`MeshEntityDraws` builds each entity's draw with `ModelDraw`'s defaults, a shadow cast and the
field holding it (`3DEngine/Rendering/Models/MeshEntityDraws.Looks.cs:42`), so as entities those
four would darken the floor under every window and pane, shut each room's indirect light behind its
glass and stand the grass in the field as boxes.

The engine lacks a mesh entity that casts no shadow and one the scene's field leaves out, as a
`CastsShadows` and a `BouncesLight` on the ECS `Material` or a component for each, beside
`ShadowOnly`. The game draws its solid faces, the faces a pack cuts out that cast a whole shadow and
its lava as entities meanwhile, and the other four through `DrawMesh` until a package carries these.

Review: item 124 of REVIEW.md, 2026-10-25; CastsShadows and BouncesLight on the mesh entity, as the
Material's or a component each, the coder's call; with item 100 at its first clean point.

### 2026-10-26, the voxel game in `Voxel-Sandbox`, rays crossing a voxel canopy's leaves cost global illumination 2.7 ms at 1080p

Read on the 3DEngine 5.2.319 package for the game's item 133, its leaf blocks' faces a part of their
own whose material's `LightThrough` is 0.5 a unit of depth, and its plants' 0.71, so a canopy's floor
takes the light through the leaves the engine's item 100 lets pass. At seed 1's cherry grove at 1920
by 1080 at High, the three default packs, render distance 12, Release, offscreen on the RTX 4070
Laptop GPU on the power-saving profile, a profile over 300 frames read in turns with the shares and
with none on the same package and build but for them, with no other program on the GPU, the
`gpu.global_illumination` read 6.14 and 6.09 ms with the shares against 3.44 and 3.42 with none, and
the frame 19.58 and 19.51 against 16.70 and 16.64, the scene pass and the shadows within 0.1 ms. A
grove's canopy is thousands of leaf blocks' faces, each a surface a ray crosses where it stopped,
where item 100's own reading of leaves filling the view took 1.5 to 2.5 ms at 800 by 450.

The engine lacks a cheaper crossing of many thin surfaces in a ray, as a canopy of voxel leaves is,
such as a ray marching a share of light through a cell of the field rather than surface by surface,
or a bound on the crossings a ray's budget allows by the quality. The game keeps the shares, since
the canopy's floor takes 0.74 of the open ground's shade from the engine alone where it took 0.55
with none, and pays the 2.7 ms meanwhile.

Review: taken as 3DEngine item 127 on 2026-10-26, the crossing's cost read on a voxel canopy and brought down, after item 112.

### 2026-10-26, the voxel game in `Voxel-Sandbox`, a program's interface has no text field to type into

Read on the 3DEngine 5.2.319 package for the game's item 134, its pause menu's New World screen,
where a player types a world's seed. The flat API gives the characters typed (`GetCharPressed`),
a key's repeat while held (`IsKeyPressedRepeat`) and the place of the input method's window
(`SetTextInputArea`), and no field to type into, so the game draws its own in its interface's look
(`VoxelSandbox/Ui/Gui.cs`, `TextField`), a box with its text and a blinking caret at the text's end,
taking the printable characters typed each frame to a length, Backspace with its repeat, and the
input method's window beside the box. It holds nothing more, no caret moved by the arrows, Home or
End, no selection by the pointer or by Shift, no paste or copy, no Delete, no click placing the
caret and no second field to move to by Tab, and the program keeps its own keys off while the field
is open, since the engine does not say a field holds the keyboard, so typing a seed's letter would
otherwise step a setting of the game's bound to that key.

The engine lacks a text field of its own in the flat API, its text and caret in the program's hands
each frame, as `bool TextField(ref string text, Rectangle box, int most)` answering whether Enter
was pressed, with the caret moved and the text selected, pasted and copied by the keys and the
pointer as a desktop's fields are, a focus that a click or Tab moves between fields, the input
method's window placed beside the caret, and a call saying a field holds the keyboard
(`IsTextInputActive()`) so a program's own keys stand aside, drawn in the engine's look or through
a program's drawing of the box, the text and the caret. The game keeps its own field meanwhile.

Review: taken as 3DEngine item 128 on 2026-10-26, a text field of the interface's own with a caret, a selection and the clipboard, after item 127.

### 2026-10-26, the voxel game in `Voxel-Sandbox`, each section draw costs the main thread 3 to 5 µs a pass, so 2,000 sections drawn four times bound the frame

Read on the 3DEngine 5.2.326 package for the game's item 139, its sections drawn as mesh entities,
one `Mesh` and `Material` each, about 1,100 of them seen at 12 columns. Flying at a height of 90
over seed 1's spawn at noon, looking east over the forest to the sea, at 1280 by 720 offscreen on
the RTX 4070 Laptop GPU, Debug, the power-saving profile, every reading with no other program on
the GPU and the engine's shadow fill off, a profile over ten seconds, three readings each:

| Columns | Frame | The main thread's `stage.Last`, the render graph's | GPU's passes | Draws | Depth | Camera with the mirror | Cascades |
|---|---|---|---|---|---|---|---|
| 8 | 16.67 to 16.78 ms | 8.4 to 8.8 ms | 8.7 to 9.6 ms | 1,173 to 1,215 | 1.2 to 1.5 ms | 2.1 to 2.7 ms | 1.3 to 1.6 ms |
| 12 | 16.71 to 17.08 ms | 12.9 to 13.2 ms | 12.3 to 13.3 ms | 2,226 to 2,322 | 2.3 to 2.4 ms | 4.9 to 5.1 ms | 2.1 ms |
| 16 | 25.33 to 25.99 ms | 20.9 to 21.9 ms | 16.4 to 18.1 ms | 3,837 to 3,901 | 3.8 to 4.0 ms | 8.9 to 9.1 ms | 3.0 to 3.1 ms |

The last three columns are the CPU's `cpu.window_depth`, `cpu.hdr_scene` with `cpu.mirror`, and
`cpu.shadows`. By `models.draws` at 12 columns the depth pass draws about 670 sections, the camera
about 780, the planar mirror about 420 (the camera's count reads 762 with the mirror off and 1,185
with it on) and the three cascades about 400, so each draw costs 3 to 5 µs of the main thread to
record in each pass, 9.4 ms of its 13 at 12 columns. At 16 the main thread bounds the frame at 21 ms
while the GPU takes 16.5. With the shadow fill on, its 2,085 draws added 2 to 3 ms more, and the
game leaves it off. The engine's note under its item 112 named this recording as the mirror's CPU
share.

The engine lacks a cheaper recording of many static mesh entities, such as the draws of one
pipeline and material recorded as one indirect multi-draw over the instances' buffer, or a pass's
commands kept while its set of draws and their meshes are unchanged, so a frame's four passes over
2,000 unmoving sections cost the main thread well under a millisecond each. The game's render
distance stays 12, where the frame stands at the edge of 16.7 ms, and 16 columns pay the recording
meanwhile.

Review: taken as 3DEngine item 132 on 2026-10-26, the passes' recording of unmoving draws brought well under a millisecond, after item 97.

Read again in the game on 6.0.11 on 2026-10-31, Debug, offscreen at 1280 by 720, on tuned's
power-saving profile with the processor's energy preference at power, the sections drawn in groups of
two by two since the game's item 143. Over seed 1's spawn at 16 columns the depth's, the shadows',
the mirror's and the camera's passes recorded about 1,950 draws in 3.0 and 3.4 ms of the main thread
where 5.2.342 took 7.6 and 7.8 in the same hour, 1.5 to 1.8 µs a draw where 3.9 to 4.0
(`./e3d command profile`), and the frame there stays the GPU's, 23.0 and 23.1 ms where 23.8 and
24.0.

### 2026-10-30, the voxel game in `Voxel-Sandbox`, a window resized before its first frame keeps a swapchain of its first size until a present fails

Read on the 3DEngine 5.2.326 package for the game's item 140, which puts the window into the
resolution its options file keeps right after `InitWindow` and before the world loads. A plain
`dotnet run` on the development machine, Debug, SDL's x11 driver, the RTX 4070 Laptop GPU, with an
options file keeping a window of 2560 by 1440: the engine made its swapchain at 1280 by 720 at 0.87
s, the window took 2560 by 1440 within the next second, the Startup stage and the loading screen's
first frame came at 2.35 s, and the swapchain stayed 1280 by 720 until the first present after the
world's load reported it out of date at 10.86 s, when it was made again at 2560 by 1440. For eight
seconds the loading screen was drawn into a swapchain a quarter of its window's area.

The engine lacks a swapchain that follows the window's size from the window's resize event, or from
the window's size read at the first frame, rather than waiting for a present to fail, so a program
that sizes its window before drawing draws its first frames at that size.

Review: taken as 3DEngine item 134 on 2026-10-30, the swapchain made again on a resize before the first frame, after item 133.

### 2026-10-30, the voxel game in `Voxel-Sandbox`, the sun's cascades draw every section in their boxes, where a slice swept toward the sun holds half

Read on the 3DEngine 5.2.326 package for the game's item 141, the sun's shadows at 128 blocks.
Flying at a height of 90 over seed 1's spawn at noon at 1280 by 720, Debug, the RTX 4070 Laptop
GPU, at 12 columns, the three cascades draw 1,276 to 1,311 sections, 3.0 ms of the CPU and 2.6 of the
GPU, where at 64 blocks they drew 389 for 2.0 and 0.8. The voxel engine ochre (github.com/rornic/ochre,
`crates/ochre-world/src/shadow.rs`) keeps for each cascade only the casters within its view's slice
swept toward the sun, with a margin of a few texels and the depth bias, and read its shadow lists at
1.08 million triangles against Bevy's 2.37 million over fifteen suns, 54 percent fewer.

The engine lacks a cull of each cascade's casters to the part of its box that can shade what the
camera sees, the camera's slice of the cascade extruded toward the sun, so a cascade reaching 128
blocks draws the sections that cast into the view and none behind it.

Review: taken as 3DEngine item 143 on 2026-10-30, the casters culled to each cascade's slice swept toward the sun, behind the cascades of Decision 75.

Read again in the game on 6.0.14 on 2026-11-01, Debug, offscreen at 1280 by 720, on tuned's
power-saving profile with the processor's energy preference at power, the sections drawn in groups
of two by two since the game's item 143 and the shadows reaching 128 blocks. Over seed 1's spawn at
12 columns the three cascades drew 542 and 539 groups in 0.90 and 0.85 ms of the CPU and 0.45 of the
GPU, where 6.0.11 drew 551 in 0.94 and 2.61 in the same hour, and at 16 columns they took 0.45 and
0.47 ms of the GPU where 2.60 (`./e3d command profile`), the meshes' buffers in the GPU's own memory.
The cascades' casters cost the GPU under half a millisecond at either distance.

### 2026-10-30, the voxel game in `Voxel-Sandbox`, a mesh over shader buffers takes its positions as three floats, where packed ones take a third

Read for the game's item 143. A section's mesh is the engine's mesh over shader buffers, its
positions three floats, 12 bytes, with a stream of eight packed bytes the game's shader reads and
the four bytes of color the scene's field reads, 24 bytes a vertex, so half of it is position. The
voxel engine ochre (`crates/ochre-terrain/src/pack.rs`) packs its chunks' vertices from 56 bytes to
32 or less, positions as signed 16-bit fractions of the mesh's bounds among them, normals in four
bytes as an octahedral pair, colors as halves and indices as 16-bit where they fit, packed on
worker tasks, which took its flowing water's 99th percentile frame from 4.38 to about 1.6 ms. A
section's positions so packed take 6 bytes, the vertex 18, and its indices 16-bit, since a section
holds fewer than 65,536 vertices.

The engine lacks a mesh's positions as small integers within its bounds and its indices as 16-bit
where they fit, so a program of many small static meshes uploads and reads a quarter fewer bytes of
vertices and half the bytes of indices.

Review: taken as 3DEngine item 144 on 2026-10-30, positions and indices packed to 16 bits for a mesh over shader buffers, behind the cascades of Decision 75.

### 2026-10-30, the voxel game in `Voxel-Sandbox`, a mesh whose stream is written in place keeps a ring of mapped buffers for good

Read for the game's item 143, its sections' opaque parts drawn in groups of four sections, each
group's packed stream and colors written in place with `UpdateMeshBuffer` where only the light of a
section changed, so the scene's field keeps the mesh settled. `GpuMeshes` moves such a mesh's stream
into a ring of `RetireFrames` and one buffers kept mapped and keeps the ring for the mesh's life,
though a section's light changes a few times as the land around it arrives and seldom after. At 8
columns over seed 1's spawn, Debug, the RTX 4070 Laptop GPU, the process maps 499 MB of the driver's
memory with the groups against 244 with a mesh to each section's part, whose rings were a quarter
the size, and 371 MB where a relit group is uploaded again instead.

The engine lacks a ring that gives its memory back once its writes stop, the stream copied into one
buffer of the GPU's own after a few frames without a write, so a mesh written in place now and then
holds one copy of its stream rather than four mapped.

Review: taken as 3DEngine item 145 on 2026-10-30, the ring of mapped buffers given back once a mesh's writes stop, behind the cascades of Decision 75.

### 2026-10-30, the voxel game in `Voxel-Sandbox`, the scene field takes 12 to 42 ms of the CPU in the frame the eye crosses into a new section

Read on the 3DEngine 5.2.333 package for the game's item 145, its field `SetSceneField(4, 0.25f, 2)`
with the far cascade reaching 192 blocks, its sections drawn as mesh entities, Debug, offscreen at
1280 by 720, the RTX 4070 Laptop GPU. Stepping the eye 16 blocks over land already loaded and
meshed, with nothing streaming, `profile.slowest` read the frame at 32 to 82 ms with
`cpu.scene_field` at 12.5 to 41.6 ms at every step, against a still frame of 17.3 ms. Flights over
land not yet loaded read their slowest frames at 64 to 79 ms with the field 7.8 to 30.2 ms, and the
99th part of a flight's frames is 47 to 55 ms at 8 columns and 86 to 110 at 12, three to six
times its median, where the game's own work as columns join averages 0.2 to 3.5 ms a frame.

The engine lacks a field that follows the eye without the CPU's work of a whole cascade in one
frame, the cells a crossing brings in stamped within the frame's budget of cascades or spread over
the frames after, so a player walking or flying sees no hitch at each section's edge.

Review: taken as 3DEngine item 146 on 2026-10-30, the field's move on a crossing spread over frames, a cascades item placed after 131.
Review: done in the engine at `da54d289` and `10295556` (item 146), the field's pool holding the
build's meshes and ShadowOnly draws kept in the field, the step at 12 columns from 40 ms at its 99th
part to 21 to 25 and the slowest from 42 to 28; in the next package.
Review: read by the game on 5.2.342 at `7bd8f1c` on 2026-10-31, the six steps' 99th part at 12
columns 49.3 and 85.1 ms to 37.8 and 43.8, the slowest 62.7 and 95.4 to 49.8 and 58.4; the entry is
done.

Read again in the game on 5.2.342 on 2026-10-31, Debug, offscreen at 1280 by 720, on tuned's
power-saving profile with the processor's energy preference at power, other sessions' tests holding
up to 29 percent of the GPU, two rounds a package in turns. Six steps of 16 blocks over the loaded
land at 12 columns read `cpu.scene_field` at 0.7 to 19.6 ms a step, most under 4, against 11.7 to
42.2 on 5.2.333, the 99th part of the frames over the steps at 37.8 and 43.8 ms against 49.3 and
85.1, and the slowest at 49.8 and 58.4 against 62.7 and 95.4 (`voxel.frames`, `profile.slowest`).
A jump of three sections reads the field at 25.5 to 40.9 ms against 36.5 to 59.2. The deep cave at
-59.5, -4, -91.5 at noon at Low with the far cascade on reads a mean luma of 0.245 against 0.398,
the rock the eye cannot see kept in the field, and the same with the far cascade off.

### 2026-10-31, the voxel game in `Voxel-Sandbox`, the first frame with a reflection plane compiles the mirrored pass's pipelines in 22.5 s

Read on the 3DEngine 5.2.342 package for the game's item 151, Debug, offscreen at 1280 by 720, the
RTX 4070 Laptop GPU with driver 615.78.08, on tuned's power-saving profile with the processor's
energy preference at power. The game sets `SetReflectionPlane` at the water in view, and over seed 1
the first water comes into view 64 blocks east of the spawn. The frame it is first set in took 23.7
to 24.0 s of the wall clock, `profile.slowest` reading `cpu.mirror` at 22.5 to 23.0 s and the frame
held at 250 ms, in every run. Later frames with the plane, the eye leaving the water and coming
back, and Water Reflections turned off and on again read `cpu.mirror` at 0.9 to 4.6 ms. With
`__GL_SHADER_DISK_CACHE_PATH` naming a folder of its own, a first run read 23.7 s and the second 1.1
s with `cpu.mirror` too small to read, so the time is the driver compiling the pipelines the engine
makes for the mirrored view's render pass, keyed apart from the window's. The driver's default cache
on this machine holds 1.1 GB across six programs and served none of the game's runs. A player's
first sight of water, at each start where the driver's cache misses, holds the game for over 20
seconds.

The engine lacks pipelines made before the frame that first needs them, those of a pass a program
turns on later made at start or over the frames after the call that asks for the pass, or a
`VkPipelineCache` of its own kept beside the program between runs, so a pass first drawn in play
costs its first frame no compile.

Review: taken as 3DEngine item 147 on 2026-10-31, the pipeline cache kept between runs and a pass's
pipelines made before the frame that first needs them, after the engine's item 132.

Read again in the game on 6.0.11 on 2026-10-31, as above. With `__GL_SHADER_DISK_CACHE_PATH` naming
an empty folder and no `pipelines.bin` beside the program, the ten frames from the first with the
plane took 1.3 s of the wall clock, the slowest 107.5 ms with `cpu.mirror` 3.2 ms. The second run,
both caches warm, took 1.0 s, the slowest 91.1 ms, and a run with the driver's folder empty again
and the engine's file kept took 1.0 s, the slowest 68.4 ms with `cpu.mirror` 0.3 ms
(`profile.slowest`). A shot every 30
frames after the cold run's first matched its last from 2.8 s on but for 22 pixels of the river
within 11 of 255 until 13 s, so no frame waits for the compile.

Review: read by the game on 6.0.11 at `8efd1d0` on 2026-10-31, the first water's frames 23.7 s to
1.3 with both caches cold and 1.0 warm; the entry is done, as is the recording entry of 2026-10-26,
the four passes' main thread at 16 columns 7.6 and 7.8 ms to 3.0 and 3.4.

Review: read by the game on 6.0.14 at `23ba89e` on 2026-11-01, the GPU's passes at 16 columns 22.8
ms to 13.6 with the shadows 2.6 to 0.45 and the depth pass 3.1 to 0.19, the engine's item 151; the
cascades' entry of 2026-10-30 is answered by it.

### 2026-11-09, the voxel game in `Voxel-Sandbox`, a walk moves the far cascade a cell and the light of what stands outside the field steps in one frame

Read on the 3DEngine 6.1.12 package for the game's item 166, Debug, offscreen at 1280 by 720, the
RTX 4070 Laptop GPU with driver 615.78.08, on tuned's power-saving profile, every frame recorded by
`record <path> 900f every 1f` while the game's `voxel.flicker` moved the eye. The game's settings
are the illumination at High, four cascades with a cell of 0.25 and a budget of two, and the far
cascade reaching the 192 blocks of 12 columns, the field's center half the finest cascade ahead of
the eye. Seed 1's cherry grove at hour 16, from -736, 79, -960 facing 345 and pitched -12, the eye
walking 0.0717 blocks a frame for 180 frames along its heading. The clouds stand at a height of 120
outside the field, since they cast no shadow, and take their indirect light through the model pass.

In that part of the walk each frame differs from the one before on 9.2 to 9.5 percent of the window
past 12 levels, the land moving, and at the walk's frame 123 the share is 11.6, the near clouds'
patch of 260 by 70 pixels going in that one frame from 163, 180, 203 to 148, 163, 186 in sRGB
levels, the frames before and after it moving as their neighbors do. Settled, the same patch reads
158, 178, 205 at the walk's frame 90 and 144, 161, 184 at frame 122, 2.3 blocks on, and with the far
cascade off 164, 184, 210 and 169, 185, 209, with the illumination off 150, 166, 189 and 154, 166,
186. So the clouds' light changes with where the far cascade lies, by 14 to 21 levels between those
two places, and in the walk it holds the light of the cascade's old place and takes the new one in a
single frame, where the frame in motion matches the settled picture of its pose within 1 percent of
its pixels everywhere else. The same walk in the village at dusk and in a torch-lit cave, and a turn
of 0.25 degrees a frame in all three, have no such frame.

Of the engine's items, 239's arrival after a cut is the nearest, a cascade arriving at a new place
as a step. Its first lever starts the arriving cascade from the light of the cascade above, which
the far cascade does not have, and a walk moves the far cascade a cell at a time with no cut. The
engine lacks the far cascade's move eased in the light it gives, the light at a place it held before
the move carried into the frames after it, so a walk does not step the light of the clouds or of
anything else the model pass lights from it. The game draws the clouds as before meanwhile.

The frames are in the game's `.github/dump/2026-11-09`, which git ignores, under
`22-05-item-166-grove-`: the recording, the walk's frames 252 and 253 and their difference, and the
settled shots of the walk's frames 90, 122 and 123, of 90 and 122 with the far cascade off, and of
90, 122 and 123 with the illumination off.

### 2026-11-09, the voxel game in `Voxel-Sandbox`, a settled view's indirect light depends on how the eye came to it, in the screen probes' tiles

Read on the 3DEngine 6.1.12 package for the game's item 166, Debug, offscreen at 1280 by 720, the
RTX 4070 Laptop GPU with driver 615.78.08, on tuned's power-saving profile, at the game's settings
as in the entry above. Seed 1's cherry grove at hour 16 with the clock held, the eye at -739.34, 79,
-972.47 facing 345 and pitched -12. Walked there over 180 frames from 12.9 blocks back and let
settle, `field.wait` and 240 frames, then taken 64 blocks off and back and let settle the same, the
two pictures differ on 0.35 percent of the window past 12 levels and 0.07 past 25, and the same
place teleported to in another run differs from the second by 0.69 and 0.25. A settled view's frames
are the same to the bit from one to the next, so each holds one estimate of the light, and the
difference lies in the screen probes' 8-pixel tiles across the land.

In the recordings of the same read, each frame of a turn made from where the eye had settled matches
the settled picture of its pose within 0.02 to 0.06 percent past 12 at 0.75 and 3 degrees a frame,
where the frames of a walk or a sprint differ from pictures settled after a teleport by 0.55 to 1.0,
the size of the path's own difference. So no lag of the light behind the motion shows past what the
path leaves, and what changes as the eye moves is which held estimate a place shows.

The engine lacks a settled light that is the same for a view whatever the eye did before it, the
error of a probe's light averaging out over the frames a view is held rather than standing in the
pattern its placement and history leave. Of the engine's items, 240's kind, the screen probes' tiles
and their kept light, is the nearest, here in a still view after a walk rather than on a thin shape.
The game does nothing about it meanwhile.

The frames are in the game's `.github/dump/2026-11-09`, which git ignores, under `item-166-path-`:
the place walked to and teleported to, both settled, and their difference eight times over.

Corrected on 2026-11-10 on the 6.2.12 package. The difference was the eye's place and not its path.
The walk adds its step in floats each frame and ends at -739.33984, -972.4695, and the teleport went
to that place rounded to a hundredth, half a millimeter off, while the other run's place, -739.3403,
-972.4664, was worked out in doubles, 3.6 millimeters off. Teleported to the walk's own end, the
place walked to and the place teleported to differ on none of the window past 12 levels, at a mean
of 0.012 of 255, and two runs teleported to one place differ on 0.002 to 0.003 percent, on 6.1.12
and 6.2.12 alike. Half a millimeter moves 0.34 percent of the window past 12, 0.17 of it with the
illumination off, where the sun's shadow edges move by a texel of its map as a cascade's middle
snaps to whole texels, and the rest in the screen probes' light. So the entry asks for nothing, and
the walk's and the sprint's 0.55 to 1.0 percent are read again against poses placed as the game's
floats place them. The pictures are in the game's `.github/dump/2026-11-10` under `item-166-pose`.

### 2026-11-10, the voxel game in `Voxel-Sandbox`, `build/flicker.py`'s carried turn reads a floor ten times Sponza's in a game of pixel textures

Read on the 3DEngine 6.2.12 and 6.1.12 packages for the game's item 166, Debug, offscreen at 1280 by
720, the RTX 4070 Laptop GPU on tuned's power-saving profile, at the game's settings. Seed 1's
cherry grove at hour 16 with the clock held, the eye at -736, 79, -960 pitched -12, turned in place
by the game's `voxel.flicker` at 0.75 degrees a frame to the right over 120 frames and recorded
every frame by `./e3d record`, once with the screen probes on, once with them off (`gi.toggle screen
off`) and once with the illumination off. `build/flicker.py --turn 0.75 --eye 0,0,0 --target <the
look> --fov 70 --levels 4,12` read 116 pairs inside each turn, and a sweep of the field of view from
60 to 78, the pitch from -11 to -13 and the rate from 0.73 to 0.77 found the least change at the
game's own 70, -12 and 0.75.

Over the window the pairs moved 10.86 percent past 4 and 4.06 past 12 with the probes on, 9.86 and
3.81 with them off, and 9.83 and 4.09 with the illumination off, where Sponza's atrium reads 0.9
past 4 with the probes off. The floor is the carry's own. The game's textures are pixel art drawn
with their texels' hard edges and its blocks' edges are aliased, and a turn's shift of a fraction of
a pixel resampled bilinearly changes every such edge by more than 4 levels. Masked to the shaded
ground under the trees, about 8 percent of the first picture, the floor is 24 percent past 4 and 13
past 12, and the probes' 37 and 17 percent stand on it.

The engine lacks three options of the tool, which the read made by hand in a script beside the game.
First, two recordings of one turn compared pair by pair at the same heading, counting the pixels a
pair moves past a level in the first and not in the second, so one part's flicker is read against
the floor both share. The probes on against off moved 1.32 percent of the window past 4 and 15
percent of the masked ground after the blur below, where the runs with the probes off and the
illumination off moved 0.54 and 0.65 against each other. Second, a blur over about a probe's tile, a
Gaussian of radius 3 on both pictures of a pair, which takes the window's floor from 9.8 to 0.95
percent past 4 and from 4.1 to 0.010 past 12 and keeps the probes' blocks, the probes then reading
1.96 and 0.089. Third, a map that follows the turn, each pair's moved pixels carried back into the
first picture's camera as the mask is carried forward, so a place that flickers stays one place on
the map. The map as it stands adds up the screen's pixels over the turn and smears them along it
into curved bands, the same with the probes on and off over 87 degrees.

The pictures are in the game's `.github/dump/2026-11-10` under `item-166-carried-turn` and
`item-166-carried-masked`.

### 2026-11-10, the voxel game in `Voxel-Sandbox`, after a cut the scene field builds what the game meshes under its base triangle budget, and a grove's shaded ground reads open for 35 to 77 frames

Read on the 3DEngine 6.2.17 package for the game's item 166, Debug, offscreen at 1280 by 720, the
RTX 4070 Laptop GPU on tuned's power-saving profile, at the game's settings of four cascades of 0.25
with two built a frame, the far cascade to 192 and the engine's default of 32,768 triangles a frame.
Seed 1 at hour 16 with the clock held and each frame's time fixed at a 60th of a second. The eye was
teleported by `voxel.tp` 150 blocks, from -736, 79, -810 into the cherry grove at -736, 79, -960
pitched -12, and `./e3d record` took every frame for 400 frames, each compared with the view's
settled shot. The cost is `./e3d command profile` over the 84 to 103 frames from the cut.

In the cut's first frame 26 percent of the window lies past 12 levels from the settled view, the
shaded ground under the trees lit as if open. From the frame after, 22 to 24 percent stays past 12
until it falls in one frame, at frames 35, 42, 54, 58 and 77 over five runs. With the illumination
off the cut lies under 0.1 percent past 12 from frame 28, and with the game's culling off, which
meshes every section in reach, the hold lasts 105 frames. The engine lifts every cascade and 16
times the triangles a frame after a cut, 524,288, until the cascades the eye went past are built
(`SceneFieldPlan.Splat`). The sections the game meshes as the new view comes in arrive after that,
as changes built under the base budget, each region read as it was last built until its build ends,
and the 1.31 million triangles the game draws there are 40 frames of that budget. The frame has no
room under its pacing in these runs, `wait` 0.003 ms, so the engine's raise of the budget by the
frame's room stays at the base.

Held at four times the default by `SetSceneField(4, 0.25f, 2, 131072, 192)` before the cut, fewer
triangles than the lift allows, so that what it changes comes after the lift, the share past 12
falls to 5.8 percent in frame 1 and 2.9 in frame 2. The mean lies within 2 levels from frame 7 in
both runs, and the last 0.8 to 2 percent past 12 goes under 0.1 by frames 50 and 55. Held at 32
times, the mean lies within 2 levels from frame 6 and the share past 12 under 0.1 percent from frame
46. Eight cascades a frame in place of two change nothing, 42 frames. Over the frames from the cut
the budget held at four times costs the field's GPU pass 1.41 ms a frame against 1.01, and the
frame's work reads 39.3 ms against 42.2 with every frame recorded.

A game cannot raise the budget for the second after a cut alone. `SetSceneField` with a budget
changed makes the field again, since `SceneField` releases a field whose settings differ from those
it was made with, so a raise lights the view from nothing in the frame it lands and the return to
the base does so again. Raised to four times in the frame after the cut and put back 60 frames on,
the grove lies under 0.1 percent past 12 from frame 42 and goes back to 27 percent at frame 90,
under 0.1 percent again only from frame 132. After a respawn each remake changes 99.9 percent of the
window for a frame.

A death and respawn at the wooded spawn, from 150 blocks off and facing the woods at heading 270,
lies under 0.1 percent past 12 three frames after the new view comes in, plain and with the budget
held at four times. The game's respawn loads and meshes the sections around the spawn before it
moves the player (`Streamer.LoadNow`), so the lift after the cut builds them. Facing a leaf at
heading 90, the window one face, the face reads 6 levels light for 30 frames after the view comes
in, for 1 with the budget held at 32 times and for 56 with eight cascades a frame.

The engine lacks a lift that lasts while what a cut brings in still arrives. It could hold its
lifted triangle budget while meshes still come into the near cascades after a cut, so a game need
not know of the cut, or keep the field when a budget alone changes, so a game could raise it for a
while. The game keeps the default budget meanwhile.

The pictures are in the game's `.github/dump/2026-11-10` under `item-166-levers`,
`item-166-respawn-leaf`, `item-166-respawn-views` and `item-166-cuts`.

### 2026-11-11, the voxel game in `Voxel-Sandbox`, the scene field placed by `SetSceneFieldCenter` each frame lights a closed room wrong, where `SetSceneFieldAhead` at the same point lights it as 6.2.17 did

Read on the 3DEngine 6.2.32 package at the game's pin of it, Debug, through the game's scripted run
`build/play.sh` with the validation layer, offscreen at 1280 by 720 in a flat world, on the RTX 4070
Laptop GPU with driver 615.78.08 on tuned's power-saving profile. Before the pin the game laid the
scene field half its finest cascade ahead of the eye by calling
`SetSceneFieldCenter(camera.Position + GetCameraForward(camera) * (64 * CellSize / 2))` before
`BeginDrawing` each frame, eight blocks at cells of 0.25. The run was taken with that and with
`SetSceneFieldAhead(0.5f)` in its place, called once with the field's other settings, which the
engine works out as the same point.

The run closes a stone room 12 blocks across with one glowstone at 16, 5, -16, stands the player at
its south wall at night facing the lamp and waits 30 frames. It then closes a room of white concrete
with a lantern at 54, 5, -20, stands the player over it looking down at 40 degrees, waits 32 frames
and reads the light at 33 points along a line across the floor from the lantern (`voxel.line`). With
the center set each frame the lantern's pool read from 133.2 to 91.1 levels with a largest step of
34.5, 0.82 of the fall, in 3 runs of 3, the same to the tenth each time, and the run failed there.
With `SetSceneFieldAhead(0.5f)` it read from 249.2 to 90.6 or 90.7 with a largest step of 8.3 or
8.4 in 4 runs of 4, as 6.2.17 reads with the center set each frame. The glowstone room's capture
with the center set each frame shows the floor dark in a ring about the lamp and the walls lit a
bright yellow, 73 percent of the window past 24 levels from the capture with the engine's option,
which looks as 6.2.17's does.

`SceneField` takes both points down the same call, `plan.Update(settings?.Center ?? AheadOf(...))`,
so the difference lies in the call made each frame or in the point it gives, which these runs do not
tell apart. The game calls `SetSceneFieldAhead` from its pin of 6.2.32 on, and a program that sets
the center each frame, as the function's remarks advise for a view about its player, still meets it.

The engine lacks a field that lights a view alike whether its center is given each frame by
`SetSceneFieldCenter` or worked out by `SetSceneFieldAhead`.

The pictures are in the game's `.github/dump/2026-11-11` under `item-167-room`.

Withdrawn by the game on 2026-11-11. The failing build was made in a git worktree reached through
`/home`, a link to `/var/home`, and there MSBuild copied the game's `resources` flat beside the
program, so `ChunkRenderer` found no `resources/shaders/terrain.slang` and the run drew without the
game's terrain shader. The passing build came from the checkout with the folder in place. Built in
such a worktree, the pin alone, the pin with only the center swapped for `SetSceneFieldAhead` and
item 167's tree with the center each frame put back each read the lantern's pool from 133.2 to 91.1
in all eight runs that reached it, and built through `/var/home` the same three trees read it from
249.2 to 90.6 to 90.9 in 3 runs of 3 each, so the fault followed the build's folder and not the
center.

### 2026-11-11, the voxel game in `Voxel-Sandbox`, a reflection whose terrain pipeline the driver makes in a frame of play shows no mirror 32 frames on

Read on the 3DEngine 6.2.32 package with the game's item 167, as the entry above. The run fills a
pool of water beside a wall of red concrete at noon, stands the player looking across it, waits 30
frames, reads a pixel, waits 2 and reads the water where the wall's mirror image lies
(`voxel.pixel`), then again with the game's Water Reflections off. In 2 runs of 3 the water read
red 163, green 181, blue 210 with the reflection on and off alike, the sky's color and no wall, and
the run failed there. The game's log at each failure ended with `made 1 pipelines in 1649 ms,
terrain.slang's vertexMain with terrain.slang's fragmentMain`, 1665 ms in the other run, a
model.slang pipeline made in 437 and 441 ms a second before. In the third run the water read red
171, green 148, blue 167, the wall's red, as on 6.2.17.

Every package keeps the user's caches under the one folder `0.1.0-preview.1`
(`UserCache.Version`), since 6.2.32's informational version reads `0.1.0-preview.1+02c3a743`. The
first two runs found `pipelines/0.1.0-preview.1/VoxelSandbox.bin` as the game's runs on 6.2.17 had
left it, and the third came after a run on 6.2.32 had written it at 01:05. Two later runs from that
file, at 01:43 and 01:55, failed the same way. Each read the file at the open, `The driver's
compiled pipelines read from ~/.cache/3DEngine/pipelines/0.1.0-preview.1/VoxelSandbox.bin, 92146
KB.`, and each made the same pipelines in play at nearly the same frames, the 01:43 run's first.

- Frames 217 and 219, model.slang with model.slang in 420 and 412 ms, and mob.slang with
  model_streams.slang in 390 and 389 ms
- Frames 218 and 220, particles.slang in 418 and 445 ms
- Frames 532 and 531, terrain.slang's vertexMain with `SPIR-V the compiler did not give`, in 1138
  and 1107 ms
- Frames 1635 and 1629, the same, in 1118 and 1094 ms
- Frames 1703 and 1702, terrain.slang's vertexMain with its fragmentMain, in 1143 and 1211 ms
- Frames 1771 and 1773, the same, in 2282 and 2407 ms
- Frames 3114 and 3156, the same at the pool, in 1145 and 1552 ms, with model.slang's at frames
  3140 and 3088 in 1569 and 423 ms

Each of the seven writes in each run said `The driver's compiled pipelines read, 0 bytes to write`,
and the file kept its time of 01:05, so what a run makes in play is not kept for the next. The game's
logs of the two runs are `~/.cache/3dengine-tools/work/i167/logs/play-0143-Engine.log` and
`play-second-Engine.log` beside it. Of six runs on 6.2.32 with the shaders cached that reached the
pool, four failed there. With `XDG_CACHE_HOME` naming an empty folder the open took 84 s on 6.2.32
and 91 s on 6.2.17, and the whole run passed on both, the water reading red 171. From an empty
folder 6.2.32 passed twice more and then failed twice running, at 03:51 and 03:55, each reading the
file at 0 KB and its 149 shader stages from the program's own folder. In the first, frame 5 made 22
pipelines in 48.0 s, frame 1768 two of terrain.slang's in 13.6 and 27.1 s, and frame 3167, at the
pool, terrain.slang's in 1574 ms, and the second made them alike, at the pool at frame 3181 in 1660
ms. Their logs are `play-cold-0351-session.log` and `play-cold-0355-session.log` beside the others.
So an empty cache does not keep the mirror whole either, and the mirror is missing where a pipeline
it needs is made in a frame of play.

The engine lacks a reflection that shows in the frames after the one that waited on its pipeline,
or the pipelines a reflection needs made at the open.

Read again on 6.2.41 on 2026-11-11, each run's caches in a folder of its own, three runs from an
empty folder and each followed by one from the cache it left. The water read red 171, green 148,
blue 167 in all six, and every run passed. From an empty folder the file grew at each write, from
8.8 MB after frame 5 to 21.8 MB, and the pool's frame made three of terrain.slang's pipelines with
its fragmentMain, stages 7e3b6259, compiled in 14.3 to 19.0 s each, all three within the 15.0, 17.3
and 19.1 s since the log's line before. From the cache they left, read at 21265 KB, every pipeline
made said `from the cache` with the stages' hash the empty run gave, and terrain.slang's three at
the pool took 1.17 to 1.37 s each. No run's log says `the driver saying not where from`. Frame 5
made its 22 pipelines in 49.7, 61.3 and 62.3 s of the driver's time from an empty folder, and 54.4,
66.2 and 67.6 s passed from the probes made to it, so they were made one after another. From the
cache it took 4.4, 4.4 and 4.6 s. The logs are in `~/.cache/3dengine-tools/work/voxel-6241/logs`.

### 2026-11-11, the voxel game in `Voxel-Sandbox`, a sea lantern's light on the wall beside it is drawn in stripes of small squares

Read on the 3DEngine 6.2.41 package with the game at `1f1e955`, Debug, offscreen at 1280 by 720, on
the RTX 4070 Laptop GPU with driver 615.78.08 on tuned's power-saving profile, from a copy of a
player's data folder, a saved world of seed 1 with its options, the illumination at Low and the
scene field at 4 cascades with cells of 0.25. The world opens with the eye at 4448.4, 25.6, 599.8
facing 48 degrees east of north in a closed room of white concrete whose inside is x 4447 to 4454,
y 24 to 27 and z 596 to 600, as its save's region files hold it. The field's finest cascade, 0.25 a
cell from 4446, 18, 586, holds the room.

The one glow light in reach is a sea lantern at 4452, 24, 595, set in the north wall at the floor
with only its face toward +z open, on the plane z = 596. That face lies in a pocket three blocks
wide, one deep and two high, closed by red concrete at 4450, 24 to 25, 596, at 4451 to 4453, 24 to
25, 597 and at 4454, 24 to 25, 596, so its light leaves the pocket upward past y = 26. The east
wall's face, the plane x = 4455 facing -x, stands 2 blocks from the lantern's box and 2.5 from its
middle. Fourteen magma blocks lie 8 to 18 blocks off, outside the room past its south wall, reached
through a door and a tunnel. `gi.state` says 28 small emitters are carried as lights.

Rows read across the east wall's face by `voxel.line` at a 32nd of a block show the stripes at y
26.9 to 27.9 and z 596.0 to 598.3, over the red concrete at 4454, 24 to 25, 596. Each of those rows
has 5 to 25 steps of over 3 levels between neighbors, the largest 8 to 11.5, where the rows at y
26.4 to 26.6 below them have none over 3 and the largest 2.7. With the lantern made white concrete
every row reads 0, and with the 14 magma blocks made stone the rows read as before within a level,
so the lantern's light alone draws the stripes.

`field.march` was run from a striped texel at 4455, 27.625, 597.564, a dark one 0.094 beside it and
a smooth one a block below. From a tenth of a cell off the wall, where `glowLightShown` starts,
toward the open face's halves at 4452.75 and 4452.25, 24.5, 596 and toward the box's middle, each
met a surface 0 along in 1 step, at its start in cascade 0's cell, its normal -1, 0, 0. All 17
texels of a row across three stripes, bright and dark, did the same. From a cell and a half off
toward the box's middle, the two striped texels passed over the pocket's rim and met the north wall
at 4453.0, 25.3, 596.0, 3.2 and 3.3 along in 10 steps. The smooth one met the top of the red
concrete at 4454.0, 26.0, 596.9, 1.1 along in 5 steps. So `field.march`, a ray, meets the wall at
once from a tenth of a cell off at the striped texels and the smooth one alike, and does not tell
them apart where the glow lights' marches start at Low.

The engine lacks a glow light whose light falls smoothly over a wall it strikes at a low angle from
beside it.

The pictures are in the game's `.github/dump/2026-11-11` under `item-173-room`, and the readings,
with both copies of the data, in `~/.cache/3dengine-tools/work/i173`.

### 2026-11-11, the voxel game in `Voxel-Sandbox`, one pixel at a trunk's edge against snow takes about ten times the trunk's light, and bloom spreads it into a red blob

Read on the 3DEngine 6.2.41 package with the game at `1f1e955`, Debug, offscreen at 1280 by 720, on
the RTX 4070 Laptop GPU with driver 615.78.08 on tuned's power-saving profile, from a copy of a
player's data folder, a saved world of seed 1 with its options, the illumination at Low, the hour
held at 13.35, the weather clear and the clouds hidden. The player names the fault as colored blobs
under bloom in the distance during play.

From the eye at 37.5, 123.1, 37.5 facing 315 degrees and pitched 12 degrees down, one stands over a
spruce 134 blocks off. With the options' bloom at 0 it is one pixel, at 272, 352, reading 255, 209,
199 in sRGB levels. The pixel to its left is the trunk, the log at -98, 105, 39, reading 55, 46, 42.
`voxel.depth` puts the pixel itself on the snow two blocks behind the trunk, at -100, 104, 39, 138
blocks off. It is the trunk's lower right corner against the snow beyond it. With bloom at the
options' 0.1, the 64 by 64 pixels about it read a mean of 202, 186, 182, and 179, 182, 175 with the
illumination off.

The pixel reads the same in five reads 7 frames apart, and in each of these, after 30 to 60 frames.
- Each part `gi.toggle` names left out in turn: history, filter, screen, merge, again, follow, glow,
  through, specular and normals.
- Each of the 4 cascades alone, and Medium and High.
- The sun's shadows left out by `SetLightCastsShadows`.
- The game's terms in terrain.slang: its sky floor at 0, its light levels off, Unshaded turned.
- Noon, 255, 211, 201.
- The snow block under the pixel made stone, snow or grass.

With every surface drawn white by `gi.toggle albedo off`, it reads 255, 255, 255. At dusk it reads
174, 137, 122 beside the snow's 212, 175, 159. With the illumination off it reads 221, 215, 205, as
the snow beside it does. With the eye moved a quarter block north it reads 245, 241, 234, and with
the view turned 0.05 degrees it reads the same.

So the color is the trunk's under about ten times the trunk's light, on a pixel the trunk covers only
in part. Only the illumination off clears it, and no part `gi.toggle` names. The frame is drawn with
the engine's 4 samples a pixel, `Config.Samples`, and FXAA and temporal antialiasing are off.
ModelVertexOutput reads the normal at the samples the triangle covers but the world point and the
way to the eye at the pixel's middle, which at the trunk's corner lies off the trunk's face.

The engine lacks a lit pixel at a silhouette's edge whose light stays within the light of the
surfaces it covers.

With one sample a pixel (`E3D_SAMPLES=1`) the pixel is gone, nothing about the trunk's foot reading
more red than blue by 30 levels with a channel at 240, and the 64 by 64 about it reads 181, 187, 185
with bloom. At 4 samples with temporal antialiasing on, the pixel reads 247, 221, 211 and the 64 by
64 reads 182, 188, 185. At 4 samples and 2560 by 1440 it stays one pixel, at 544, 703, reading 252,
231, 178, and the 128 by 128 about it reads 182, 188, 185. So the fault is the edge's and not the
place's, and lies in how a pixel a surface covers in part is shaded.

It is common in the distance. From 133 blocks off with the eye stepped a twentieth of a block 20
times, the frames near the origin held 27 such lone bright pixels over the 20 steps, and bloom
added over 20 levels about one of them in 7 steps, up to 142 levels over 1,532 pixels from one
white pixel on snowy steps 140 blocks off. Beside 4432, 120, 592 they held 8, with 2 such steps. A
second session at each place read the same counts. Four spruce logs on snow built block for block
at both places, in the middle of those frames, made none at either, so the counts follow the land
behind them and do not compare the two places.

A spruce trunk's foot from 15 blocks reads orange with blue at 0, which is the dirt under it seen
where the snowy ground steps down, its own color under the frame's grading, and not this fault. The
sheets are in the game's `.github/dump/2026-11-11` under `red-blob` and `item-176-samples`, and the
readings in `~/.cache/3dengine-tools/work/i176`.

### 2026-11-11, the voxel game in `Voxel-Sandbox`, the light a pixel takes cannot be read by its terms

Two reads of the game's this day ended where the game could not say which of the engine's terms
lights a pixel. In the sea lantern's stripes above, `gi.show` drew the screen's probes as tiles,
and their light and its filtered form drew black at Low, where no screen probes are traced. At the
trunk's edge above, every part `gi.toggle` names left the pixel as it was, and only the illumination
off changed it, so the term that makes it was found by none of them. `gi.show` draws the probes' data
over the window, and `voxel.pixel` and `voxel.color` give the frame's color after the tonemap and
the grading. The game could not find out, at one pixel of the frame, the light the model pass gives
it from each of its terms before they are summed, the sun's direct light and its shadow, the sky's
light, the world probes' light, the screen probes' light, the glow lights' near and far light and
the specular, nor the world point, the normal and the samples it was shaded with.

The engine lacks a way to read one pixel's light by its terms in linear light.

### 2026-11-11, the voxel game in `Voxel-Sandbox`, the screen's probes mottle a wall a glow light lights at 4,432 blocks from the origin, where the same wall near the origin is smooth

For the engine's item 258. Read on the 3DEngine 6.2.41 package with the game at `1f1e955`, Debug,
offscreen at 1280 by 720, on the RTX 4070 Laptop GPU with driver 615.78.08 on tuned's power-saving
profile, from a copy of a player's data folder with its options, ambient occlusion at 0, bloom set
to 0 for the reads, the hour held at 13.35, the weather clear, the clouds hidden. One scene is built
twice block for block on a platform in the air, its corner at 16, 120, 16 and at 4432, 120, 592,
the offset a multiple of 64 on both axes, and the 836 blocks of the two read the same from the save.
It is a floor of light gray terracotta 16 by 16, a west and a north wall of white concrete five high,
a roof over the west half, a pillar, a sea lantern set in the west wall at the floor and one standing
on the floor under the roof. Each place is read from the same eye against its scene, 17.5, 3.1, 17.5
blocks from the corner, facing 315 degrees and pitched 12 down, in two sessions, by five rows of
`voxel.line` at a 31st of a block, at Low, Medium and High, with every stage as the game draws it and
with each stage changed in turn. The two sessions at one place read alike to a hundredth of a level
in most rows.

Along the west wall's face at the lantern's height and a half, 1.005, 2.5, from 1 to 16 blocks along
z, the mean step between neighbors is 0.34, 0.35 and 0.37 near the origin and 0.45 to 0.47, 0.52 and
0.52 to 0.53 at 4432, at Low, Medium and High. The difference of the two places' captures shows the
wall mottled in squares at Medium and High. With the screen's probes left out by `gi.toggle screen
off` the two places read 0.34 to 0.36 against 0.36 to 0.37. With the glow lights left out they read
0.37 to 0.39 against 0.39 to 0.44. With the filter, the frame before's light or the sun's shadows left
out the difference stays as it was, 0.11 to 0.19. The faces of spruce logs on snow built beside each
scene differ by up to 10 levels between the places at Medium and High, and by 0 between sessions.

The engine lacks screen probes whose glow light falls on a wall as smoothly far from the origin as
near it.

The sheet is in the game's `.github/dump/2026-11-11` under `item-174-places`, and the rows, the
captures and the table in `~/.cache/3dengine-tools/work/i174`.

### 2026-11-11, the voxel game in `Voxel-Sandbox`, ambient occlusion darkens and mottles a scene at 4,432 blocks from the origin more than the same scene near it

For the engine's item 258, read in the scene of the entry above, with `SetAmbientOcclusion` at 1 in
place of the player's 0. The north wall's face at 2.5 high reads a mean of 175.6, 175.7 and 174.9
near the origin and 168.0, 168.8 and 167.5 at 4432, at Low, Medium and High, 7 to 7.5 levels darker,
where with it at 0 the two read within 0.3. Its mean step is 0.47 to 0.49 against 0.55 to 0.59. Across
the floor at the roof's shadow the mean is 3 levels lower and the mean step 0.68 to 0.72 against 0.80
to 0.84, and along the west wall 0.35 to 0.38 against 0.56 to 0.67. The two sessions at one place read
within 0.5 of a level. The difference of the captures at Medium shows the walls and the roof's
underside mottled at 4432.

The engine lacks an ambient occlusion that closes a surface off as much far from the origin as near
it.

### 2026-11-11, the voxel game in `Voxel-Sandbox`, the sun's stepped shadow under a roof steps differently at 4,432 blocks from the origin

For the engine's item 258, read in the scene of the first entry above. Along the floor under the
roof at 2.5 blocks from the west wall, where the sun reaches under the roof's edge, 27, 26 and 26
steps between neighbors exceed 3 levels near the origin, and 23, 22 and 23 at 4432, at Low, Medium
and High, in both sessions. With the sun's shadows left out by `SetLightCastsShadows` both read 8. The
game takes the shadow at the middle of an eighth of a block of the face, terrain.slang's
`pixelMiddle`, and hands it to the engine's `litLight`. At 4432 a float steps by 0.0005, so the
game's middles move by no more than that against an eighth of a block, and a step could change only
for a point within 0.0005 of an eighth's edge. The mean step reads alike, 1.10 to 1.13 against 1.07
to 1.12.

The engine lacks a sun's shadow that steps the same far from the origin as near it.
