The screen probes at four resolutions, read in the voxel game's room of forty glowstones with global
illumination at High, offscreen, with another session's tests sharing the GPU so every time runs high
alike. The scene pass is the probes' own cost; the two global illumination columns are the whole of it
with the probes every 8 pixels at any screen, and with the spacing grown by the window's height (item
122).

| Resolution | scene pass with probes | global illumination, every 8 pixels | global illumination, spacing by height |
|---|---|---|---|
| 1280 by 720 | 2.6 ms | 8.3 ms | 8.3 ms |
| 1920 by 1080 | 4.9 ms | 13.7 ms | 13.1 ms |
| 2560 by 1440 | 7.6 ms | 17.8 ms | 12.4 ms |
| 3840 by 2160 | 16.2 ms | 35.9 ms | 13.3 ms |

The probes' spacing grows with the window's height, 8 pixels at 1080p, 11 at 1440p and 16 at 4K, so a
probe covers the same patch of the world at any screen. Global illumination at 4K drops from 35.9 to
13.3 ms, about what 1080p costs, and the bounce rooms at 4K read within 0.002 of their 1080p distances
to the path-traced references in every view, where a fixed 8 pixels at 4K had drifted. Low's glow-only
pass costs 4.5 ms of global illumination at 4K and the per-pixel march would cost 108 ms, so the
quality ladder of item 103 matters there as expected.
