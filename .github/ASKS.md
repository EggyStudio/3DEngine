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

### 2026-10-10, the voxel game in `3DEngine.Game`: the glow lights can lose the GPU device

On `98a1edca`, the game built from that tree, a hidden window of 1280 by 720 and an RTX 4070 Laptop
GPU, High, render distance 8: `./e3d open 3DEngine.Game/bin/Debug/net10.0/3DEngine.Game --hidden
--transient --flat`, then `voxel.room 10 4 -22 22 11 -10 white_concrete`, a closed room 11 by 6 by
11 inside, `frames.wait 120` and `voxel.set 13 5 -19 glowstone`, the eye left at the spawn 20 blocks
off outside the room at hour 10. Within a few frames of the lamp, `RenderPlugin.Render` throws
`ErrorDeviceLost` at the frame's submission and every frame after it does too, in 3 of 5 runs with
the glow lights on and in none of 5 with `gi.toggle glow off` first. The kernel's log gives NVRM Xid
31 for each, a fault of the memory unit reading through the constant cache, a uniform read out of
range. A glowstone in the open or sealed in a box of 3 blocks, and the same room with the eye inside
it before the lamp is placed, lost nothing in the runs tried. The game does nothing about it, and
its readings of the glow lights are taken with the eye inside the room first.

Review: Verdict 47 of REVIEW.md, 2026-10-10, before everything else; a uniform read out of its
range, the case built as the engine's own under the validation layer's GPU-assisted checks.

### 2026-10-10, the voxel game in `3DEngine.Game`: the glow lights cost 0.67 ms of the model pass

On `98a1edca`, the room above with the red and green walls of the earlier measures and sixteen
glowstones on its floor in a grid of 4 by 4, the eye inside it 10 degrees down across them, hour 23,
High, render distance 8, 226 draws, bloom off; a hidden window of 1280 by 720 and an RTX 4070 Laptop
GPU. Each figure is `./e3d command profile` read three times after 600 frames with `gi.toggle glow`
off and on, in two passes that agreed, and the averages from `GetProfileAverage` beside them.

| Glow lights | `gpu.hdr_scene` | `gpu.global_illumination` | Frame |
|---|---|---|---|
| Off | 1.38 to 1.43 ms | 1.30 to 1.33 ms | 4.5 ms |
| On | 2.05 to 2.14 ms | 2.15 to 2.22 ms | 6.1 to 6.2 ms |

The bounce gains 0.85 ms, near the 0.8 expected of sixteen blocks, and the model pass 0.67 ms where
0.2 was expected; before the commit's cost work it gained 1.56 ms of the model pass and 0.81 ms of
the bounce. The same commit holds parts d and e of item 2 as measured before it: the lamp's light at
2 and 2.5 blocks 13.6 and 7.3 at a cell of 0.25 and 13.3 and 7.2 at 0.125 at each quality, its eight
lobes 0.01 of it or less, and its pool the same from 5 to 80 blocks up, 52 to 57 at a block and half
that by 1.5, so those two entries are taken off.

Review: item 2 of REVIEW.md, its part d and e, 2026-10-10; the model pass's share read for where it
goes and brought down, your room the measure, after Verdicts 47 and 30.
