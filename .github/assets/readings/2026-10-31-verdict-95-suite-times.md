The suite's time on Linux between `2ad96a80` and `b16f26b5`, read from the two pushes' runs and from
the classes of the scene field and global illumination run at each commit on lavapipe with the
validation layer, on this machine's Intel i9-14900HX under tuned's `powersave-bazzite`.

The run of `2ad96a80` ran the suite whole in 34 m 4 s, and shuffled in 32 m 24 s. That of
`b16f26b5` was at its last tests when its whole run met the limit of 40 minutes, 2,090 ended and 20
of four classes left, `AppLeakTests`' at app 66 of 100 and three of audio. A test that ends in under
a millisecond is printed with `[< 1 ms]`, which build/test.py's line for an ended test did not take,
so it read 1,582 of them, and the 508 of `Api`, `Rendering` and the rest that ended so fast had no
result. Every part holding one ran again from its start, `Rendering`'s 529 for 34 m 51 s where the
whole run had ended each, so the page took 80 m 5 s and the shuffled run reached the job's 100
minutes. Read with the line mended, the three parts holding the 20 run again, and a part run again
runs those tests alone, a class whose every test is left by its name and the rest by their methods'
names, so it goes on from where the whole run was and is not begun again.

The tests that changed between the two commits are item 131's in `SceneFieldTests.Sheets.cs`, a
closed room of double-sided sheets at Low and at High, new, and a sheet met at its plane in place of
one crossing zero, 3.1 s either. The nine classes ran in 42.0 minutes at `2ad96a80` and 45.3 at
`b16f26b5`, 1.1 of it the closed room, and those that grew most:

| Test | `2ad96a80` | `b16f26b5` |
|---|---|---|
| The closed room of sheets stays dark, High | | 39.3 s |
| The closed room of sheets stays dark, Low | | 25.9 s |
| An emissive block shut in a room keeps the device | 97.6 s | 106.5 s |
| A wall sees four joined lamps past a pillar | 64.7 s | 69.4 s |
| Global illumination holds still as the camera slides | 54.8 s | 59.4 s |
| Far from the world's origin no more land is black | 83.3 s | 87.5 s |
| A lamp's light rises past a pillar's edge | 46.3 s | 49.8 s |
| An emissive block's pool reads alike from forty blocks | 51.0 s | 54.2 s |
| The thin wall's foot takes no light from the lamp outside | 52.1 s | 55.2 s |

The rest is spread over every test that draws global illumination, its classes 6 to 8% slower, the
reference frames 2% and the subsurface spheres, which draw none, 0.6%, the probes' rays reading the
plane a cell keeps, as global illumination took 5.64 ms where 5.27 in the voxel game's cherry grove
on the GPU (item 131's note). On the runner the whole run grew by six minutes or more, more than the
same share would give. Its results at `2ad96a80` were not kept, since they are uploaded on a failure
or a cancel alone, so the runner's own times test by test are not read.

The shut-in block, the slowest of them, read at `b16f26b5` with one thing changed at a time, a
frame's time after its first:

| The shut-in block | A frame |
|---|---|
| As the test draws it, 640 by 360 at High, four cascades, 120 frames and 240 | 287 ms |
| 320 by 180 | 277 ms |
| 160 by 90 | 268 ms |
| Three cascades | 240 ms |
| Two cascades | 185 ms |
| Low | 189 ms |
| Global illumination off | 40 ms |
| 30 frames and 60 | 482 ms |

A sixteenth of the pixels takes 7% off, so a smaller window reads no faster on lavapipe, and most of
the slow tests draw at 320 by 180 or 160 by 90, the closed room of sheets 0.25 to 0.37 s a frame at
160 by 90. The time is the frame's work over the field and its probes, about 50 ms a cascade, and
most in the first frames as the field fills, 482 ms a frame over the first ninety and 223 over the
rest. A smaller room reads faster where it lets the field hold fewer cascades, the shut-in block's
lying within the reach of three, and fewer frames or Low read faster where a test's meaning bears
them. The shut-in block checks on lavapipe only that its frame was read back, its fault being the
GPU's, so it would bear them first, 106 s of the 45 minutes here. No test is changed.
