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

### 2026-10-10, the voxel game in `3DEngine.Game`: a lamp's light on a floor shows 8 lobes at High

A flat world at hour 0, one glowstone on the grass at 0, 4, 0, the eye 11.6 blocks above the floor
straight over it and pitched 89 degrees down, the light levels and corners off, the hud hidden; a
hidden window of 1280 by 720 and an RTX 4070 Laptop GPU. The game's `voxel.ring 0.5 4 0.5 <radius>
72` reads the luminance at 72 points around a circle on the floor's top through `GetWorldToScreen`.
The moonlit floor's own 48.2 to 48.8, read at 6 blocks, is taken off, so each figure is the lamp's
light, and the lobes are the harmonics of the 72 readings, each as a share of the lamp's mean light
on the circle.

| Quality | 2 blocks: light, swing, lobes | 2.5 blocks: light, swing, lobes |
|---|---|---|
| High | 29.0, 0.75 of it, 8 lobes at 0.15 | 9.2, 2.26 of it, 8 lobes at 0.39 |
| Medium | 30.3, 0.44 of it, 8 lobes at 0.03 | 12.8, 1.19 of it, 8 lobes at 0.11 |
| Low | 28.5, 0.46 of it, 4 lobes at 0.11 | 14.7, 0.95 of it, 4 lobes at 0.16 |

The single lobe, 0.14 to 0.67, is the largest harmonic at each, which comes of the eye standing a
degree off the vertical, and is left out. Within 1.5 blocks the lobes are under 0.07, and by 3
blocks the lamp's light is down to the floor's own at each quality. The cause was not looked for,
and the game does nothing about it. Captures: `voxel-spokes-high.webp`, `voxel-spokes-medium.webp`
and `voxel-spokes-low.webp`.

Read again on the engine's tree after `3a83cec6`, the same scene with bloom off through `./e3d
eval`, since bloom's halo has a size on the screen and not in the world and stood in the figures
above for about a fifth of the light at 2 blocks. The field's four cascades at a cell of 0.25 and of
0.125, each at the three qualities, the lamp's light on the circle and its lobes as shares of it:

| Cell | Quality | 2 blocks: light, 8 lobes, 4 lobes | 2.5 blocks: light, 8 lobes, 4 lobes |
|---|---|---|---|
| 0.25 | High | 22.5, 0.16, 0.02 | 7.2, 0.43, 0.09 |
| 0.25 | Medium | 23.8, 0.04, 0.04 | 10.5, 0.12, 0.01 |
| 0.25 | Low | 21.8, 0.01, 0.12 | 11.6, 0.02, 0.17 |
| 0.125 | each | 0.0 | 0.0 |

At a cell of 0.125 the lobes are gone because the pool is: the lamp gives the floor 5.8 at 1 block,
where a cell of 0.25 gives it 33.3, 1.1 at 1.25 blocks and nothing from 1.5 out, at High. The finer
cell shrinks the pool from about 2.3 blocks to about 1.3 and its light near the lamp to a sixth. The
captures `voxel-lobes-cell025.webp` and `voxel-lobes-cell0125.webp` are the frame with the lamp less
the frame without it, six times over, around the lamp at twice its size: an eight-lobed flower with
a stepped edge at 0.25, and a pool hardly wider than the cube at 0.125.

Review: item 2 of REVIEW.md, its part d, 2026-10-10; the probes' octahedron, the first cascade's 64
directions the first thing measured.

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

### 2026-10-10, the voxel game in `3DEngine.Game`: a lamp's pool is gone from 40 blocks away

A flat world at hour 0, one glowstone on the grass at 0, 4, 0, the eye straight over it 5, 10, 20,
40 and 80 blocks above the floor and looking down at it, so the cascades lead toward the lamp as
they do walking away from one while facing it; the light levels and corners off, bloom off, the hud
hidden, the field's four cascades of 0.25 built two a frame, High, a hidden window of 1280 by 720
and an RTX 4070 Laptop GPU, on the engine's tree after `3a83cec6`. The lamp's light is the floor's
around it at 72 points a circle through the game's `voxel.ring`, the frame with the lamp less the
frame without it, and the cascades that hold the lamp come from `field.state`, their probes 8 cells
apart.

| Eye above the floor | Lamp's light at 1, 1.5, 2, 2.5, 3 blocks | Cascades holding it, spacing |
|---|---|---|
| 5 blocks | 33.0, 33.7, 25.3, 4.0, 0.0 | 0 to 3, 2 to 16 blocks |
| 10 blocks | 32.3, 31.6, 24.1, 6.7, 0.0 | 0 to 3, 2 to 16 blocks |
| 20 blocks | 36.7, 30.0, 18.1, 6.8, 1.2 | 1 to 3, 4 to 16 blocks |
| 40 blocks | 0.0 at each | 2 and 3, 8 and 16 blocks |
| 80 blocks | 6.4, then 0.0 | 3, 16 blocks |

Near, the pool keeps its light to about 2 blocks and ends between 2 and 2.5, 25 to 4, an edge rather
than a falloff, its outline a ring of lobes in steps. From 20 blocks, out of the first cascade, it
is smaller and round, and from 40, where only the cascades whose probes stand 8 and 16 blocks apart
hold the lamp, it is gone from the bounce, the pool being narrower than their spacing. The game's
light levels light the floor around the lamp as Minecraft's do whatever the distance, so with them
on the far pool shows. Captures: `voxel-pool-5.webp`, `voxel-pool-10.webp`, `voxel-pool-20.webp` and
`voxel-pool-40.webp`, each the frame with the lamp less the frame without it, six times over, around
the lamp at twice its size.

Review: item 2 of REVIEW.md, its part e, 2026-10-10; at 40 blocks the field's cells of one and two
blocks hold the cube's glow nowhere and the probes 8 apart average the rest, read there with the
remedies in order.
