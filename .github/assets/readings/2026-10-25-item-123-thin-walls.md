The thin walls' leak at Low and what its mend costs, with global illumination at Low, where no
screen probe stands. The bounce rooms' thin room's floor along its walls' foot is read in linear
light at 480 by 270 by its test, with the bright lamp outside and without it; the voxel game's two
scenes are read at 1920 by 1080 offscreen on a scratch copy of the game, the share of pixels
marching counted from a frame that paints them (item 123).

| Form | thin room's floor, lamp outside / without | voxel room, scene pass | voxel spawn, scene pass |
|---|---|---|---|
| each pixel by the probes' reach | 0.303 / 0.196 | 2.6 to 2.8 ms | 6.0 to 7.4 ms |
| march where another surface is near | no triangles to the eye | 5.1 to 5.2 ms | 8.2 to 8.3 ms |
| march by a wall thinner than a cell | 0.207 / 0.207 | 3.1 to 3.2 ms | 6.1 to 6.3 ms |

The leak came from a pixel's read of the probes across a wall thinner than the field's cell, its
reach judged from a point inside the wall's cells, and not from the probes' rays or the field. In
the voxel game the thin-wall form marches 0.5% of the room's pixels and 0.75% of the spawn's, slivers
at a few blocks' corners, where marching wherever another surface stands near cost 2.5 ms. A speck
where the thin room's right and back walls meet its floor stays.
