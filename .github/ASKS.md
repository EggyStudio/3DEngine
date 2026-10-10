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

### 2026-10-10, the voxel game in `3DEngine.Game`: the bounce settles 13 to 18 frames after a move

Measured with the game's `./e3d command voxel.flicker <frames> <turn> <step> <over> <path>`, which
reads each frame through `LoadImageFromScreen` and writes its mean change from the frame before in
sRGB levels and the share of pixels past 8 levels, the motion in equal parts over its frames. The
readback waits for the GPU, so the frames come slower while it records. A hidden window of 1280 by
720, an RTX 4070 Laptop GPU, a Debug build, the light that bounces at High, the game's light levels
and shaded corners off (`voxel.shade false false`) and its hud hidden. The scene: a flat world at
hour 23, a closed room of white concrete 11 by 6 by 11 inside (`voxel.room 10 4 -22 22 11 -10
white_concrete`), its west wall red and its east wall green, one glowstone on the floor at 16, 5,
-16, the player at 16.5, 5, -11.8 facing north and 10 degrees down. Still, every frame's change is
0.000. "Still after" counts the frames after the motion until five in a row change under 0.02.

| Motion | Change during it | First frame after | Still after | Summed after |
|---|---|---|---|---|
| Turn 90 degrees in one frame | 37.0 | 1.02 | 18 frames | 5.41 |
| Turn 90 degrees over 30 frames | 5.5 a frame | 0.32 | 13 frames | 1.58 |
| Step one block in one frame | 11.7 | 0.52 | 14 frames | 2.30 |
| Walk 4 blocks over 20 frames | 10.4 a frame | 0.57 | 15 frames | 2.63 |

Under one pixel in ten thousand changes past 8 levels after the motion, so it is a faint drift over
the whole room rather than a speckle. The difference between the first frame after the walk and the
settled one, sixteen times over, is a grid of patches on the walls and the floor at the spacing of
the first cascade's probes, 2 blocks, which the walk of 4 blocks moves twice, up to about 12 levels.
Each part of the bounce's window left out in turn with `gi.toggle <part> off`:

| Part left out | Turn over 30 frames | Walk over 20 frames |
|---|---|---|
| None | 13 frames, 1.58 | 15 frames, 2.63 |
| Frame before's light (`history`) | 5 frames, 0.75 | 0 frames, 0.02 |
| The screen's probes (`screen`) | 6 frames, 0.94 | 0 frames, 0.02 |
| Their filter (`filter`) | 13 frames, 2.05 | 14 frames, 2.78 |
| The merge (`merge`) | 14 frames, 2.16 | 15 frames, 2.89 |
| Light bouncing again (`again`) | 13 frames, 1.75 | 16 frames, 4.06 |
| Bounce following a light (`follow`) | 13 frames, 1.58 | 15 frames, 2.62 |

On seed 1's hills at hour 10 the same turn and walk are still within a frame, summing 0.05 and 0.09,
the sun's light outweighing the bounce's. The screen's probes carry the frame before's light and
take 13 to 18 frames to reach the light after a motion, and with their history or the screen's
probes left out the drift is gone and the picture lacks what they give, which was not measured. The
game does nothing about it. Captures in `.github/assets/asks`: `voxel-flicker-stopped.webp`, the
first frame after the walk, and `voxel-flicker-difference.webp`, its difference from the settled
one.

Review: item 2 of REVIEW.md, its part b, 2026-10-10; the drift is the screen probes' history
converging, read there with the remedies to measure.

### 2026-10-10, the voxel game in `3DEngine.Game`: the bounce's last cascade draws a line at sunset

Seed 1's hills from 0.5, 100, 0.5, flying, facing east and 12 degrees down, at hour 18.2 with the
sun under the horizon, render distance 8, the field's four cascades of 0.25 built two a frame, the
sun's shadows to 96 blocks, the light levels and corners off, the hud hidden; the same window and
GPU. Each row of the view's middle, columns 560 to 720, is read for luminance from `./e3d shot`, and
the distance along the ground under its middle comes from the game's `voxel.depth <x> <y>`. Each
figure is the light at High over the light with the bounce Off, which takes the environment map's
light instead.

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
straight over it and pitched 89 degrees down, the light levels and corners off, the hud hidden; the
same window and GPU. The game's `voxel.ring 0.5 4 0.5 <radius> 72` reads the luminance at 72 points
around a circle on the floor's top through `GetWorldToScreen`. The moonlit floor's own 48.2 to 48.8,
read at 6 blocks, is taken off, so each figure is the lamp's light, and the lobes are the harmonics
of the 72 readings, each as a share of the lamp's mean light on the circle.

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

Review: item 2 of REVIEW.md, its part d, 2026-10-10; the probes' octahedron, the first cascade's 64
directions the first thing measured.
