Where the scene pass's time goes at High in the voxel game's cherry grove, seed 1 at 1920 by 1080,
offscreen on a scratch copy of the game at the engine's HEAD (item 96). A pixel that no screen probe
on a surface like its own stands near, 9.1% of the grove's, its leaves, grass and far blocks, read
the world's probes by marching the field toward each of eight, as the thin room's corner needs; a
world of blocks has no wall thinner than a cell, so the march now runs only by such a wall.

| Form | scene pass, rounds of two readings |
|---|---|
| marching at every such pixel | 13.07, 13.08; 12.11, 12.11; 15.41, 15.35 ms |
| marching by a thin wall alone | 10.53, 10.57; 11.13, 10.84; 10.00, 9.66 ms |
| no march at all | 8.6 to 9.3 ms, the thin room's corner then 2.17 times its wall |
| no read of the world's probes | 7.7 to 7.9 ms |

Other programs held part of the GPU through every round, so the rounds run apart by as much as 3
ms; within each the gated form read 1.0 to 5.4 ms less. A half-size pass gathering the world's
probes once a two by two of pixels, read first, gave nothing: 11.9 to 12.1 ms at High and 8.75 to
9.16 at Low with it and without, since the cost was the march and not the gather it stood in for.
