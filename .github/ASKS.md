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

### 2026-10-10, the voxel game in `3DEngine.Game`: the bounce's last cascade draws a line at sunset

Seed 1's hills from 0.5, 100, 0.5, flying, facing east and 12 degrees down, at hour 18.2 with the
sun under the horizon, render distance 8, the field's four cascades of 0.25 built two a frame, the
sun's shadows to 96 blocks, the light levels and corners off, the hud hidden; a hidden window of
1280 by 720 and an RTX 4070 Laptop GPU. Each row of the view's middle, columns 560 to 720, is read
for luminance from `./e3d shot`, and the distance along the ground under its middle comes from the
game's `voxel.depth <x> <y>`. Each figure is the light at High over the light with the bounce Off,
which takes the environment map's light instead.

| Distance along the ground | High | High, cell 0.5 | High, shadows to 200 |
|---|---|---|---|
| 21 to 48 blocks | 0.80 to 0.87 | 0.91 to 1.09 | as High |
| 60 to 96 blocks | 0.56 to 0.80 | 0.54 to 0.79 | as High |
| 102 blocks | 0.94 | 0.69 | as High |
| 122 to 148 blocks | 1.00 | 0.77 to 0.94 | as High |

The step from about 0.6 to 1.0 falls between 96 and 102 blocks, where `field.state` puts the end of
the last cascade, 2 a cell from x -32, so 96 blocks ahead. With the cell doubled it moves past the
148 blocks drawn, and with the shadows taken to 200 blocks it stays where it was, so the line is the
field's and not the shadows'. At hour 10 the bounce differs from the map by under 8% and no step
shows. Past the last cascade a surface takes the map's unoccluded light, which at a low sun is up to
two-thirds brighter than the bounce inside, and nothing blends the one into the other. The game does
nothing about it; a cell of 0.5 moves the line past the render distance at the cost of the finest
cells. Captures: `voxel-border-sunset.webp` and `voxel-border-sunset-cell05.webp`.

Review: item 2 of REVIEW.md, its part c, 2026-10-10; the near blended into the far across a band at
the last cascade's edge.

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

### 2026-10-11, the voxel game in `3DEngine.Game`: a long, thin emitter's light steps on the floor

On `1fa88e8b`, the game's end rod, a glowing box 2/16 by 15/16 by 2/16 of a block drawn as one
scaled lamp cube over a base of its section's mesh 4/16 wide and 1/16 high, stands on the floor of a
closed room of white concrete 11 by 6 by 11 inside (`voxel.room 10 4 -22 22 11 -10 white_concrete`
on a flat world) at 16, 5, -16 (`voxel.set 16 5 -16 end_rod`), the eye at 16.5, 5, -11.8 facing
north and 25 degrees down, hour 23, High, render distance 8; a hidden window of 1280 by 720 and an
RTX 4070 Laptop GPU. The floor's linear light is read along rows of the frame that `gi.compare`
writes beside a reference (its `-frame.pfm`), and the point under each pixel is worked out from the
camera.

Toward the rod the light rises smoothly and then drops from one pixel to the next, by 8.4 to 8.8%
along rows 380 to 450 with the game's light levels and corners on and the rod's glow at 17, as the
game ships it, and by 7.3 to 7.8% with them off and its glow at 28. The drops lie on a closed curve
2.3 to 2.7 blocks from the rod's axis, nearest along the diagonals, 2 to 3 levels of about 75 on the
screen (`voxel-end-rod-step.webp`, its levels stretched). The path-traced reference shows no step
there. The step stands at the same pixels at Low, Medium and High, and with the screen's probes,
their filter and the frame before's light each left out by `gi.toggle`, and with `gi.toggle glow
off` the floor is black, so it is in the carried light. A scan of each capture's rows for a drop of
2 levels within 6 pixels finds none in the same room lit by glowstone, a torch's flame of 2/16 by
3/16 by 2/16, a lantern's glass of 6/16 by 7/16 by 6/16 or their blue soul fire, so it comes with an
emitter long against its width.

The engine lacks a carried light that is smooth across that curve for such a box. The game draws the
end rod as it is meanwhile.

Review: item 2 of REVIEW.md, its part d and e, 2026-10-11; the lamps' switch from the faces' closed
form to their point form at four half diagonals, which a box long against its width crosses too
near, moved or blended, with a test.

### 2026-10-11, the voxel game in `3DEngine.Game`: the path-traced reference ignores vertex colors

On `1fa88e8b`, the same room lit by one glowstone at 16, 5, -16, the same eye, hour and window,
`gi.reference <png> 1024 2` and then `gi.compare <png>`. The reference's floor reads 0.155, 0.090
and 0.029 with the game's light levels and corners on and the same with them off, while the frame's
goes from 0.084, 0.051 and 0.017 to 0.099, 0.060 and 0.020, so the reference does not read the
sections' vertex colors. `BounceReference` traces each copy's flat color times its texture's
average, and the game draws its sections in one white material with each block's color in their
vertices, white concrete's 212, 216 and 217, which the field and the model pass read. With its
shading off the frame reads 34% under the reference on the floor and 52% on the walls for glowstone,
12% and 56% for a torch, and 30% and 55% for an end rod, and a floor of white traced where the frame
lights one of two thirds of white accounts for a third on its own, so the comparison cannot tell the
bounce's error from the color's.

The engine lacks a reference that takes a mesh's vertex colors as the field does, so the game's
light cannot be measured against the path tracer. The game reads its light by `voxel.ring` and the
frame's rows meanwhile, and lights its rooms of white concrete for the comparisons it makes between
its own lamps.

Review: item 2 of REVIEW.md, its apparatus, 2026-10-11; the reference takes a hit's vertex colors as
the field's splat blends them, before part f's measures in your room.

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
