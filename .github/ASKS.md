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

Review: item 8 of REVIEW.md, 2026-10-11; an opaque pipeline without the discard for materials that
cut nothing out, tried before a depth pass, after item 2's parts f and c.

### 2026-10-11, the voxel game in `3DEngine.Game`: colors written in place miss the bounce

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
dark at their last upload. The light that bounces and the reference keep a mesh's colors from its
last upload and take none that `UpdateMeshBuffer` writes, while the model pass draws the new ones.
The game writes colors in place whenever a block's light changes, so a lamp placed in a dark room
bounces off walls as dark as they were before it, until a block changes in their section.

After the uploads the frame agrees with a reference of 8 bounces within 3% on the floor and 2% on
the far wall for the glowstone, and 5% and 20% for an end rod in its place, the vertex colors of
`3c7fe822` in both; against 2 bounces it reads 22% and 31% over, the bounces past the second in a
white room. The end rod's step is gone at `3105e73e`, the largest drop toward it along rows 370 to
460 of the frame 0.6% of the light where 8.5%.

The engine lacks the field's colors and the ray-traced scene's following a color stream written in
place, as the model pass's do. The game keeps writing colors in place meanwhile, since uploading a
section anew on each change of light would have the field build it again.

The vertex stream of the game's own, at `b275f738` against the commit before it, each built from a
clean worktree on `3ec1fdb3`'s engine, each corner of a section being its position and eight packed
bytes and a relight writing the packed bytes alone. In this entry's room with the game's light
levels on and the glowstone placed, 294 sections and 117,054 triangles on the flat world and 1,344
sections and 781,834 triangles on seed 1's land around the same place, the device's mesh buffers
from `./e3d command memory.buffers`, the frame and its `gpu.window_depth`, `gpu.shadows` and
`gpu.hdr_scene` from `./e3d command profile` after 300 frames, and the relight as the time from
`voxel.shade off off` until no section waits to mesh, which shades every loaded section anew. The
flat world is read twice each, before and after by turns; on the land the desktop took up to a fifth
of the GPU at the start of the pair.

| Measure | Before | After |
|---|---|---|
| Vertex bytes on the device, flat | 13.62 MB in 887 buffers | 9.82 MB in 1,178 buffers |
| Vertex bytes on the device, land | 87.07 MB in 4,100 buffers | 63.24 MB in 5,462 buffers |
| Frame, flat | 5.13 to 5.17 ms | 3.96 to 4.49 ms |
| Depth, shadows and scene, flat | 0.92, 0.84 to 0.93, 1.45 to 1.48 ms | 0.42, 0.30, 1.10 ms |
| Frame, land | 34.1 ms | 18.0 ms |
| Depth, shadows and scene, land | 6.0, 18.2, 7.2 ms | 2.9, 7.8, 4.8 ms |
| Relight of every section | 0.34 to 0.38 s | 0.34 to 0.40 s |

A section uploads 72% of the vertex bytes it did, where 45% was expected of 20 bytes a vertex for
44, because the colors the field reads go with the upload, as they must until item 9, and on the
device they bring a buffer of four bytes a vertex and one of eight of zeros for the second texture
coordinate, 32 bytes a vertex in four buffers. The passes that read positions alone, the window's
depth and the shadows, take half to a third of their time, and the frame on the land half; the
relight is the mesher's work and is unchanged. The engine lacks a way to give a mesh of positions
its colors for the field alone, which would take a section to 20 bytes a vertex on the device.

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
