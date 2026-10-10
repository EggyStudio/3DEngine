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

### 2026-10-10, the voxel game in `3DEngine.Game`: the field's gather costs 2.3 ms on a still scene

The scene for each entry of the voxel game: seed 1's hills, the player at the spawn (0.5, 89, 0.5)
facing north and level, a hidden window of 1280 by 720, an RTX 4070 Laptop GPU, the light that
bounces at High over four field cascades of 0.25 a cell built two a frame, the sun casting shadows
to 96 units, ambient occlusion at 0 and bloom at 0.5. Each pass's time is its average from
`GetProfileAverage`, read through `./e3d eval` after 600 frames at each setting, in two passes that
agreed, with nothing else drawing on the GPU, and the draws are the game's own count from
`./e3d command voxel.state`.

On that scene, `cpu.scene_field` reads 2.3 ms at 6 columns and 3.9 ms at 8, with no block
edited and every mesh drawn unchanged in its place for hundreds of frames, about 2 microseconds a
draw a frame. `SceneFieldPlan` compares each draw's instance, its mesh, vertex array, transform,
color and emission, with the frame before's each frame, so a still scene pays for every draw as a
changing one does. The game has nothing to do about it but draw less.

With every pass culled to its view at `d1031bc3`, this is most of what still grows with the draws.
On the same scene in a Debug build, `cpu.scene_field` reads 1.9 ms at 6 columns and 1,133 draws,
3.0 to 3.3 ms at 8 and 1,836, and 4.8 ms at 10 and 2,891, beside 2.3 ms of the GPU's shadows at 8,
and a Release build reads 1.7 to 2.0 ms at 8 in a frame of 7 to 8 ms that waits on the CPU. The
game draws 8 columns by default and does not ask for its far shadow cascades drawn less often or
smaller, nor for a cascade's draws issued as one, which would save the GPU time the frame does not
wait on and a quarter of a millisecond of the CPU's.

Review: item 2 of REVIEW.md, its part d, 2026-10-10.

### 2026-10-10, the voxel game in `3DEngine.Game`: the field ignores the vertices' colors

At 8 columns on the scene above, 1,127 sections are drawn as 1,971 meshes, 1.75 a section, because
`SceneFieldRenderer.Gather` gives the field each draw's material color times its texture's average
color and the model pass alone multiplies in the vertices' colors. A section meshed as one mesh
colored at its vertices would bounce one color for every block in it. With the field taking the
vertices' colors into its splat, as the model pass does, a section would be one mesh and one draw,
some 43% fewer draws on this scene before any culling. The game meshes a section into one mesh for
each surface it shows, a grass top, a dirt side, red concrete, drawn with that surface's material.

Review: item 2 of REVIEW.md, its part e, 2026-10-10.

### 2026-10-10, the voxel game in `3DEngine.Game`: a changed mesh gives off no light for 8 frames

Read in `SceneFieldPlan.cs` and RENDERING.md section 4 and not measured, since the game is built
around it. The field keys a mesh by its vertex array, so a mesh uploaded again is a new mesh,
stamped for its first eight frames as up to eight boxes in its color that give off none of its
light, and a block placed beside a lamp inside a section's mesh would put the lamp out for those
frames and the cascades built after them. The engine lacks a stamp that carries a mesh's emission,
or a mesh kept in the field as it was until the one replacing it is still. The game draws each block
that gives off light as a cube of its own, the cubes of one kind in one instanced draw, which an
edit beside it leaves as it was, and a cobblestone placed beside a glowstone in a closed stone room
left the room lit in the captures after it.

Review: item 2 of REVIEW.md, its part f, measured first, 2026-10-10.

### 2026-10-10, the voxel game in `3DEngine.Game`: the sky's reflection lights a sealed cave

On seed 1 at noon, a chamber 9 by 4 by 9 blocks cleared at 21 to 29, 37 to 40 and 21 to 29 inside
solid stone with no opening, the player inside looking across it, a hidden window of 1280 by 720,
an RTX 4070 Laptop GPU, the light that bounces at High. Each reading is the mean sRGB of the view
over 1080 by 500 pixels every fourth pixel, from `./e3d shot`. With the sky's environment map the
chamber reads (6.6, 13.3, 24.4), a blue sheen brighter where the walls are seen at a grazing angle,
and with the map unloaded through `./e3d eval` it reads (0.1, 0.1, 0.1). The game's light levels,
which darken the stone's color to about 1% there, change nothing, so the light is the map's
reflection off stone of roughness 0.9 and not its diffuse light, which the bounce keeps out.
`gi.state` gives reflections below roughness 0.5 a march through the depth and then the field,
and nothing occludes a rougher surface's reflection of the sky. The engine lacks an occlusion of
the environment's reflection, by the field or by what the bounce finds of the sky. The game dims
the whole map by the sky's light level where the player's eyes are, which takes the chamber to
(0.9, 1.1, 2.8) and dims the sky's light on everything while the player is underground.

Review: item 2 of REVIEW.md, its part g, 2026-10-10.
