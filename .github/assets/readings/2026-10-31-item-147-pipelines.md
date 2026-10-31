The driver's compiled pipelines kept between runs, read on the engine's tree against `df856af8`
built in Release and put into the voxel game at `8a1e308` on 5.2.333, on an RTX 4070 Laptop GPU
with driver 615.78.08 under tuned's `powersave-bazzite`, offscreen at 1280 by 720 over seed 1's
spawn at noon. Each run named a folder of its own made empty as the driver's shader cache
(`__GL_SHADER_DISK_CACHE_PATH`), loaded the land looking west, then turned east to the water 64
blocks off, the first frame with a reflection plane, the wall clock read over the turn and the 30
frames after it.

| | First run | Second run |
|---|---|---|
| Before | 30.2 s | 26.5 s |
| After | 26.8 s | 0.98 s |

The first run after wrote 10.5 MB to `source/.slang-cache/pipelines.bin`, and the second run's
slowest frame read 192 ms, 184 of it the mirrored pass. A run that compiles no pipeline the file
lacks leaves it as it was, and a file whose header names another GPU or driver is read as empty.
