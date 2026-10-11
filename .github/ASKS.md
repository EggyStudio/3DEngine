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

### 2026-10-11, the voxel game in `3DEngine.Game`: land behind a wall is lit in any order

In a closed room of white concrete 11 by 6 by 11 inside (`voxel.room 10 4 -22 22 11 -10
white_concrete` on a flat world), its west wall red and its east wall green, with sixteen glowstones
on its floor at x 13, 15, 17 and 19 and z -19, -17, -15 and -13, the eye inside at 16.5, 6, -11.5
facing north 20 degrees down, hour 23, High, bloom off, the glow lights left out by `gi.toggle glow
off`; a hidden window of 1280 by 720 and an RTX 4070 Laptop GPU. The model pass, `gpu.hdr_scene`
from `./e3d command profile`, reads 1.42 ms at a render distance of 8 columns and 0.83 ms at 2, as
the engine's session read it in a copy of the game's build with `214feb11`'s shaders, so the land
outside the room, all of it behind the room's walls, costs the pass about 0.6 ms though none of it
is seen.

The game draws its opaque sections in the order it keeps them. Drawn nearest the eye first they cost
no less: 1.40 to 1.43 ms nearest first against 1.35 to 1.38 ms kept with the glow lights off, and
1.85 to 1.92 against 1.79 to 1.83 with them on, three readings after 400 frames of each order, each
order twice, the kept order's readings matching the room's earlier ones, so no other work held the
GPU then. The model pass discards a fragment for its alpha cutoff and no pass writes depth ahead of
it, so its depth test runs after a fragment is lit, and an order that puts the walls first saves
nothing.

The engine lacks an opaque pipeline without the discard for materials that cut nothing out, or a
depth pass ahead of the model pass, either of which would let the walls hide the land before it is
lit. The game draws in its own order meanwhile, its render distance a setting.

Review: item 8 of REVIEW.md, 2026-10-11; an opaque pipeline without the discard for materials that
cut nothing out, tried before a depth pass, after item 2's parts f and c.

### 2026-10-11, the voxel game in `3DEngine.Game`: colors written in place miss the bounce

On `3c7fe822`, a closed room of white concrete 11 by 6 by 11 inside (`voxel.room 10 4 -22 22 11 -10
white_concrete` on a flat world) built at hour 23, the game's light levels and corners then turned
off (`voxel.shade off off`), which the game gives the room's sections as new colors written by
`UpdateMeshBuffer` into their color stream, their vertices kept, and a glowstone placed at 16, 5,
-16; the eye at 16.5, 5, -11.8 facing north 25 degrees down, High; a hidden window of 1280 by 720
and an RTX 4070 Laptop GPU. Each figure is a region's mean linear light in the frame, as
`gi.compare` reads it beside a reference of 1024 paths a pixel.

From 300 frames after the lamp to 2,100 the frame's floor reads 0.099, 0.060 and 0.020 and its far
wall 0.035, 0.021 and 0.007. A block set outside each of the room's four columns and taken away
again makes each section upload its mesh anew, the room's blocks and light as before, and from 300
frames on the floor reads 0.130, 0.081 and 0.027 and the wall 0.072, 0.047 and 0.016, a third
brighter and twice as bright, and stays so. The reference does the same: its far wall reads 0.002
before the uploads and 0.043 after, and whole sections are black in its picture where the room was
dark at their last upload. The light that bounces and the reference keep a mesh's colors from its
last upload and take none that `UpdateMeshBuffer` writes, while the model pass draws the new ones.
The game writes colors in place whenever a block's light changes, so a lamp placed in a dark room
bounces off walls as dark as they were before it, until a block changes in their section.

After the uploads the frame agrees with a reference of 8 bounces within 3% on the floor and 2% on
the far wall for the glowstone, and 5% and 20% for an end rod in its place, the vertex colors of
`3c7fe822` in both; against 2 bounces it reads 22% and 31% over, the bounces past the second in a
white room. The end rod's step is gone at `3105e73e`, the largest drop toward it along rows 370 to
460 of the frame 0.6% of the light where 8.5%.

The engine lacks the field's colors and the ray-traced scene's following a color stream written in
place, as the model pass's do. The game keeps writing colors in place meanwhile, since uploading a
section anew on each change of light would have the field build it again.

Review: item 9 of REVIEW.md, 2026-10-11; a write to a color stream marks the mesh's colors stale,
the field repainting its cells' colors and the ray scene its corners in the frame after, before item
8.

### 2026-10-11, the voxel game in `3DEngine.Game`: a wide penumbra is too dark and takes a spot

On `e477bc1e`, built from a clean worktree: a closed room of white concrete 11 by 6 by 11 inside
(`voxel.room 10 4 -22 22 11 -10 white_concrete` on a flat world) at hour 23, three glowstones in a
row under its ceiling at x 15 to 17, y 10, z -16, and a block of white concrete floating at 16, 8,
-16 two blocks under the middle one, so the floor 3 blocks under the block lies wholly in penumbra,
the lamps' row three times as wide as the block and no umbra reaching it; each section uploaded anew
after the lamps (the entry on colors written in place); the eye at 16.5, 8.6, -11.5 facing north 35
degrees down, High, the game's light levels on, as played; a hidden window of 1280 by 720 and an RTX
4070 Laptop GPU. The frame is read in linear light from `gi.compare` against `gi.reference <png>
4096 8`, after 600 frames still, then shots at frames 1 to 40 and 600 after a step of one block
west, in levels of 255 over the floor's crop of 560 by 280 pixels.

| Floor, linear light | Frame | Reference | Frame over reference |
|---|---|---|---|
| Under the block | 0.0574 | 0.0758 | 0.76 |
| At the penumbra's edge | 0.0593 | 0.0756 | 0.78 |
| Beside it | 0.0726 | 0.0688 | 1.06 |
| In front of it | 0.0816 | 0.0903 | 0.90 |

In the reference the floor under the block reads a tenth brighter than beside it, being nearer the
lamps, with no shadow to see; the frame draws a shadow with an edge, a fifth darker than beside it.
After the step a spot of light stands at the shadow's edge, at pixel 875, 420, 5.5 levels over the
ring 12 to 20 pixels around it from the first frame after the step to the 600th, and none stood
there before the step. With `gi.toggle history off` the spot reads 8.2 to 8.6 levels, the shadow's
edge breaks into steps of the screen's probes' 8 pixels, and the frames change by 0.1 to 0.2 levels
from one to the next; with the history on they change by 0.01 or less, so the picture is still after
a step and its error stands. With `gi.toggle screen off` there is no spot, the frames do not change
at all, and the shadow's edges show twice. With `gi.toggle glow off` the room is dark, the lamps
lighting nothing but through the glow lights. Capture `voxel-penumbra-spot.webp`, one stretch of
levels 52 to 93 for all four: the frame still, its reference, the frame 600 frames after the step
with the spot ringed, and the frame after the step with the history off.

A straight edge, on `64fd2f14`: in the same room a glowstone floating at 13, 6, -16 and a column of
white concrete from floor to ceiling at 15, 5 to 10, -18, whose shadow's edge crosses the north wall
upright about 100 pixels wide, the eye at 19.5, 8.1, -13 facing it head on. In each row from 200 to
520 the edge is where the light falls halfway from its lit side to its shadowed side, between
pixels, and a straight line is fitted to the rows.

| Edge | Off the line, rms | Its range | Autocorrelation 8 rows apart |
|---|---|---|---|
| Still | 1.83 px | 6.3 px | 0.90 |
| 1 frame after a step east | 1.48 px | 7.2 px | 0.81 |
| 30 frames after it | 1.50 px | 7.1 px | 0.82 |
| Still, history off | 2.03 px | 7.8 px | 0.89 |
| 1 frame after the step, history off | 1.31 px | 6.7 px | 0.77 |

The edge bows a pixel or two over its height and has no teeth, its deviation alike 8 rows apart. A
torch two blocks before a wall a block high, which throws a line about 16 pixels wide across the
wall behind, draws the end of the torch's reach as stairs of whole blocks with the game's light
levels on, which are the game's own, spreading a block at a time as Minecraft's do, and smooth with
them off. Neither shows the owner's saw, which stands in the owner's own room, below.

The blotches, in the wide penumbra above at the same still frames, as the light less its blur over
10 pixels in the floor's crop of 480 by 230: 0.65% of the light with the history on and 0.85% with
it off, the reference's own noise 6.5% and single pixels. The blotches' correlation falls to half
within 6 to 7 pixels across and 5 to 6 down, near the screen probes' tile of 8 and far over the
texel of 2, and holds at 0.42 and 0.40 at 8 and 16 pixels with the history on, 0.35 and 0.37 off, so
they repeat on the tiles' grid. With the history off the shadow's edges break into a chain of beads
in that picture, the left edge's trace repeating every 19 pixels with teeth 26 pixels from crest to
trough, and with it on the same edge runs as a smooth line. Captures `voxel-penumbra-edge.webp`, the
upright edge at one stretch of linear light 0 to 0.06, still, 30 frames after the step and still
with the history off, and `voxel-penumbra-blotches.webp`, the light less its blur at one stretch of
2% either side, with the history on and off.

A wall of red light, on `64fd2f14`: in the same room a wall of magma blocks 6 by 6 against the north
wall at x 13 to 18, y 5 to 10, and a column of white concrete from floor to ceiling at 16, 5 to 10,
-17 four blocks before it, whose shadow runs south across the floor, one edge sharp and one wide;
the eye at 16.5, 10.4, -14.5 looking straight down. Each edge is traced in rows 260 to 700 as the
upright edge was.

| Edge | Off the line, rms | Its range | Autocorrelation 8 rows apart |
|---|---|---|---|
| Sharp edge, still | 0.66 px | 4.1 px | 0.72 |
| Sharp edge, 1 and 30 frames after a step east | 0.91 and 0.87 px | 5.5 and 5.3 px | 0.77 |
| Wide edge, still | 0.73 px | 3.3 px | 0.55 |
| Wide edge, 1 and 30 frames after the step | 0.77 and 0.74 px | 3.6 and 3.7 px | 0.54 and 0.51 |

Neither edge has teeth, no lag up to 40 rows standing out in its deviation. With `gi.toggle glow
off` the room is black, its brightest pixel 0.0000057 where 0.022 with the glow lights on, so each
magma block is carried as a light of its own and the wall lights the room through the glow lights
alone, not through the cascades. Capture `voxel-penumbra-redwall.webp`, the floor in the log of its
light from 0.00001 to 0.03, still and 30 frames after the step.

The owner's saw, in the owner's world: seed 1's overworld as the owner's save stood at 13:53, an
underground room with a floor of stone at y 54 and a ceiling at 59, a wall of green concrete at z
-40 and one of red concrete at z -33 from x 4 to 11, four glowstones at x 4, y 55 and 56, z -37 and
-36, and a pillar of white concrete from floor to ceiling at x 6, z -37 and -36, two blocks east of
them. The pillar's shadow falls on the red wall as an upright seam near x 8, dark on its east side
and lit red on its west. Seen from 9.5, 56.6, -35.4 facing heading 185 and 3 degrees down, High, the
game's light levels on, on `64fd2f14` built from a clean worktree, the seam is traced in each row
from 70 to 650 where the light first rises past halfway from its dark side to its lit side, in
linear light from `gi.compare`, and a tooth is a run of three rows or more standing over 4 pixels
off the seam's median place.

| Case | Rows 250 to 470 off the seam, rms | Teeth, rows 70 to 180 | Teeth, rows 520 to 650 |
|---|---|---|---|
| Still | 1.05 px | up to 34 px | up to 23 px |
| 1 and 30 frames after a step east | 2.40 and 1.52 px | up to 31 and 32 px | up to 17 and 18 px |
| `gi.toggle history off` | 0.28 px | up to 42 px | up to 31 px |
| `gi.toggle screen off` | 0.80 px | up to 22 px | up to 36 px |
| The game's light levels off | 0.97 px | up to 42 px | up to 23 px |

The seam runs straight through its middle and breaks into teeth at its two ends, near the floor and
the ceiling, standing out to either side by turns. They stand with the screen's probes, their
history and the game's light levels each left out, and with `gi.toggle glow off` the room is black,
so they are in the glow lights' march toward the glowstones, past the pillar's ends. Captures
`voxel-penumbra-owner-seam.webp`, the view with its red stretched from level 1 to 114, and
`voxel-penumbra-owner-seam-parts.webp`, the seam in the log of its light from 0.0001 to 0.05 in six
cases: still, 30 frames after the step, the history off, the screen's probes off, the glow lights
off and the light levels off.

The engine lacks a soft shadow from a lamp wider than what blocks it that keeps the light the
penumbra gets, a shadow edge that stays straight to its ends, and screen probes that give no spot
after a move. The game does nothing about it.

Review: item 2 of REVIEW.md, its part h, 2026-10-11, the owner's finding of 11:15, after part g; the
numbers name two things, a soft shadow too dark for a lamp wider than what blocks it and a sample at
the shadow's edge the probes' blend holds wrong after a move, each mended against this reference.
