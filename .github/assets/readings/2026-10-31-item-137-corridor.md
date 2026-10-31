The coarse cascades inside a space narrower than their spacing, read on the engine's tree against
`19658e55` on an RTX 4070 Laptop GPU under tuned's `powersave-bazzite`, in `shaders_bounce_rooms` at
800 by 450 against path-traced references of 1,024 paths a pixel, in item 89's wheat room, and in
the voxel game's rain forest at seed 1 against references of 256 paths a pixel crossing its leaves.

The corridor's walls by its open end, where the sun's patch lies, are past the first cascade's
reach, its field cascade beginning at x 79.2 with the eye at 87.5, so they read the second cascade,
whose spots there at z of 1.2 and -1.2 lay inside the walls, 1.0 to 1.3 out, and were moved out
through the nearer faces to 1.54. Those walls took no light past the first bounce, and the
corridor's probe of the first cascade read nothing on any face where its reference read 0.015 to
0.125. With the lamp lighting the thin room's wall 30 units off through the opening switched off, a
light past the field's edge, the probe's first bounce read 62% over its reference and every bounce
65 to 92% under.

A spot of a cascade above the first, in or beside a wall between a space narrower than its spacing
along two axes and a wider one, goes into the narrow one through the wall's farther face, the second
cascade's probes there standing at z of 0.66 and -0.66, the two by the opening 16 and 3% under their
references and the next one in 30%. The corridor:

| | Walls | Ceiling | Floor | Distance to the reference |
|---|---|---|---|---|
| High, before | -67%, -69% | -52% | -54% | 0.0039 |
| High, after | -67%, -67% | -42% | -48% | 0.0032 |
| Low, before | -72%, -73% | -62% | -49% | 0.0041 |
| Low, after | -54%, -53% | -44% | -46% | 0.0039 |

Every other view read the same distance to its reference at both qualities but the strip's room at
High, 1.0% further, where the same placement with other comments read it as before and the trace
before this item with a line of comment more took the Cornell box's at High 0.6% further, the High
views of those two rooms moving by up to 1% with the trace's text alone. Global illumination took
1.79 to 1.83 ms in the corridor at 1920 by 1080 at High where 2.23 to 2.24, and 2.39 to 2.42 in the
Cornell box where 2.38 to 2.43, read by turns twice each.

Three forms were left out. Moving the first cascade's probes too took the Cornell box at Low 1.4%
further, a space narrow along one axis the strip's room 0.3% at Low, and moving a probe the field's
normal leaves within its clearance to the most open point within its reach, the window's room 5 and
16% further at High and Low, the grazing floor 15% at both, the carried lamp's room 11.6% at Low,
and the wheat room's pixels in the lower half under 0.6 of its reference from 2,475 to 2,895 with
the screen's probes off.

The wheat room's dark round patches, item 131's lead, stand under probes of the first cascade over
the middles of its half-unit squares, their faces within 13 to 21% of the reference, and read 2,475
pixels with the placement as before. The rain forest's leaves' sides at -640, 55, 638 at noon, the
game's shading off, read 0.0102 in linear light where their reference reads 0.0040, and every region
of the view read against it as before.

In the second commit a probe of a cascade below the last that sees none of the probes of the cascade
above around it outside every mesh carries its rays on to the field's edge, bringing the light of
what they meet and nothing where they meet nothing. With the placement it took the corridor's walls
at High to 60 and 62% under, its ceiling to 38% and its floor to 45%, its distance to 0.0029, 25%
nearer than before this item, Low as the placement left it and every other room's distance as it
was, the strip's room at High 1.0% further and the Cornell box's 0.6%, as the placement alone and
the trace before this item with its text changed read them. Global illumination took 1.83 to 1.87 ms
in the corridor at 1920 by 1080 at High where 1.80 to 1.81 with the placement alone, and 2.41 and
2.42 in the Cornell box where 2.41 and 2.46. Alone it changed no room. Bringing the sky where a ray
met nothing lit the closed room of the cascade edge tests through the cascades not yet built in its
first frames at 0.060 in linear light, over twice what its floor settles at, so it brings none.
