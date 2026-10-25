Why the voxel game's scene pass took twice as long with its sections drawn through `DrawMesh` as
with the same sections as mesh entities, read on scratch copies of the game at `b7608c7`, one as it
is and one with its chunk renderer of `ddede23`, both on the engine at `23b4793b`, in seed 1's
cherry grove at 1920 by 1080 at High, offscreen, the frames unlimited. The two paths make the same
draws through the same pipelines, culled alike, with the same depth state and the same coverage of
the depth pass; what differs is the order. A section's solid faces, its faces cut out and its lava
came by turns, each a batch alone through the terrain shader's solid pipeline or its cut-out one,
so the pass bound a pipeline 315 times a frame, against 7 for the entities.

| Sections drawn | GPU scene pass, ms |
|---|---|
| through `DrawMesh`, in the game's order | 13.1 to 14.1 |
| as entities, in their order | 9.3 to 10.8 |
| through `DrawMesh`, nearest first | 11.8 to 13.0 |
| through `DrawMesh`, by pipeline | 9.2 to 10.6 |
| through `DrawMesh`, by pipeline and nearest first within it | 8.4 to 9.0 |

The model pass draws a view's opaque batches by their pipeline and nearest first within it, each
run between two batches that write no depth apart. With another program holding part of the GPU,
the sections through `DrawMesh` read 8.9 to 12.9 ms with it where 14.4 to 21.2 without, and as
entities 12.4 to 14.6 where 10.5 to 15.0, the frames within the spread two runs of one build show.
In the thousand meshes of item 108, in Release, the order costs 0.03 to 0.15 ms of the pass's
recording where all are of one pipeline, and takes 0.6 to 0.8 ms off it through `toon.slang` with
the solid and the cut out by turns, for `DrawMesh` and entities alike.
