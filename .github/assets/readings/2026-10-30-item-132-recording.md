The model passes' recording of the voxel game's sections, read on the engine at `1703c54e` built in
Release and put into the game at `fc0937a`, Debug on 5.2.326, at the game's open view, flying at 90
over seed 1's spawn at noon looking east, 1280 by 720 offscreen on the RTX 4070 Laptop GPU under
tuned's `powersave-bazzite`, a profile over 600 frames, two rounds. A scratch copy timed each part
of the recording, which reads the passes 2 to 30% over the plain build. Another session's program
held the GPU through some readings, which the frame shows and the passes' CPU times do not.

The four passes, the shadows', the depth's, the camera's and the mirror's, in milliseconds of the
main thread a frame, and the descriptor sets the program's shader takes:

| Columns | Draws | Plain build | Timed | Sets a frame | The sets, timed | Sets shared, timed |
|---|---|---|---|---|---|---|
| 8 | 1,116 to 1,138 | 4.7 to 5.0 | 5.8 to 6.2 | 897 and 898 | 3.0 to 3.2 | 2.7 |
| 12 | 2,180 to 2,210 | 9.4 to 10.9 | 9.6 to 11.5 | 2,041 and 2,047 | 5.6 to 6.5 | 5.1 |
| 16 | 3,788 to 3,811 | 16.4 to 17.0 | 18.4 to 19.7 | 3,655 and 3,661 | 10.1 to 11.7 | 7.6 |

Most of the 3 to 5 µs a draw is not the draw. A section's draw through the terrain shader takes a
descriptor set made for it in each pass and written whole, its uniforms copied and its five maps,
its atlas and its table bound one by one, 2.8 to 3.2 µs a set, where every section's set holds the
same values, the shader's being copied once a frame for all of them. The depth's and the shadows'
passes make one for each section cut out by its leaves, 427 at 12 columns, whether the pass draws it
or not. One set a frame for the draws with the same shader, values and maps, tried in the scratch
copy, takes the passes from 9.6 to 11.5 ms to 5.1 at 12 columns and from 18.4 to 19.7 to 7.6 at 16,
the frame at 16 from 26.6 and 28.5 ms in the plain build to 17.9, the land below the horizon at 12 a
mean of 0.08 of 255 from the plain build's frame and 0.4% of its pixels over 10, the water and the
mobs.

With the sets shared, the passes' 5.1 ms at 12 columns are 0.96 ms of culling, 0.057 µs a box a view
over 6,084 boxes in the cascades and about 2,000 in each other view, 0.80 ms of the depth's and the
shadows' passes each walking every batch to gather those that cast, 2.04 ms of binds and draws, 0.92
µs a draw, 0.4 to 0.5 µs of it three vertex binds of two arrays made each and an index bind, where
the instances' ring and the zeros are the same for every draw, and the rest the timing's own cost
and the lookups.

Item 108's thousand boxes as entities, standing still, record in the camera's pass in 1.16 to 1.20
ms through the engine's shader, with no set a draw, and in 2.47 to 2.55 ms through `toon.slang`,
1.64 to 1.66 of it sets, 0.81 with the sets shared.

The three ways read against this. An indirect multi-draw over an instances' buffer would replace the
binds and draws, about 2 ms at 12 columns, but needs one set a pipeline and material first and the
sections' meshes in shared buffers, each section holding its own today, which the field and picking
read too. A pass's commands kept while its draws are unchanged would save the whole recording on a
still view, but the passes push the camera's view and cull by it, the cascades follow it, and the
instances' ring and the sets change every frame, so a moving eye records again nearly every frame.
Secondary command buffers on workers would leave the main thread the longest pass, the camera's 3 ms
at 12 columns, the work the same, the sets' ring, the allocator and the renderer's dictionaries made
safe across threads first. The sets shared, the unchanged binds left bound, the vertex binds made
without arrays and the casting batches gathered once a frame come before each of the three, which on
these parts would leave about 2.5 ms for the four passes at 12 columns and 4 at 16, an estimate, the
culling and the walks then half of it, after which a multi-draw is read for what it still saves.
