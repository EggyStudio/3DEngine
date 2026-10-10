# 3DEngine.Game

A voxel game in the manner of Minecraft, and the ground floor of a larger one. Its first use is
to test the light that bounces (Radiance Cascades over the scene's distance field) with blocks
that give off light. It holds an endless world of colored blocks in columns of 16 by 16 by 128,
a first-person player with Minecraft's sizes and speeds, building and breaking with an outline
on the block looked at, Minecraft's light levels with smooth lighting and shaded corners, a sky
whose sun crosses it, and an ImGui window of the light's settings.

It sits beside the examples rather than under `games/`, built on the engine's project instead of
its package, so a change to the light that bounces is played on the next build.

```bash
dotnet run --project 3DEngine.Game                      # the hills of seed 1
dotnet run --project 3DEngine.Game -- --flat --seed 7   # a flat world to build a test on
dotnet run --project 3DEngine.Game -- --world house     # a save of its own name
./e3d open 3DEngine.Game/bin/Debug/net10.0/3DEngine.Game --hidden --transient
dotnet test 3DEngine.Game.Tests                         # its light, meshes, body, saves and land
```

A world is saved under `~/.local/share/3DEngine.Game/saves` on Linux, and the matching local
application data folder elsewhere, in a folder named by its kind and seed, `overworld-1` or
`flat-7`, unless `--world` names it. It is saved every 30 seconds, on opening another world and
on quitting, and opened where it was left. `--transient` keeps nothing on disk, as a test run
driven by `./e3d` should, so it leaves the saves as they were.

## Controls

| Input | Does |
|---|---|
| Mouse | Look |
| W, A, S, D | Walk |
| Space | Jump, or rise while flying or swimming. Pressed twice quickly, start or stop flying |
| Left Shift | Sneak, which keeps the player from walking off an edge, or sink while flying |
| Left Control | Sprint |
| Left button | Break the block looked at, four a second while held |
| Right button | Place the chosen block against the face looked at |
| Middle button | Pick the block looked at into the hotbar |
| 1 to 9, wheel | Choose a slot of the hotbar |
| E | The blocks, to put any of them in the chosen slot |
| F3 | The settings window: what the frame holds and costs, the light, the sky and the world |
| G | The next quality of the light that bounces, Off after High |
| F1 | Hide the crosshair, the hotbar and the outline |
| Escape | Free the cursor, or hold it again |

## Commands

Each is a line of `./e3d list` and runs with `./e3d command`, so a test of the light is built and
looked at from the terminal. A closed room lit by one lamp, at night:

```bash
./e3d command voxel.world flat
./e3d command voxel.room 10 4 -22 22 11 -10 stone
./e3d command voxel.set 16 5 -16 glowstone
./e3d command voxel.tp 16.5 5 -11.8
./e3d command voxel.look 0 -20
./e3d command voxel.time 23
./e3d shot room.png
```

`voxel.state` says where the player stands and looks, what is loaded and drawn, the hour and the
light. `voxel.fill`, `voxel.room` and `voxel.set` change blocks, `voxel.tp`, `voxel.look` and
`voxel.fly` move the player, `voxel.break` and `voxel.place` act as the mouse buttons do,
`voxel.time` and `voxel.cycle` set the sky, `voxel.distance` the render distance, `voxel.light`
reads a block's light levels, `voxel.shade` turns the light levels and the shaded corners on or off,
`voxel.world` saves the world and opens or begins another of a kind and seed, `voxel.save` saves at
once, and `voxel.blocks` lists the blocks by number and name. `voxel.gi`, `voxel.field` and
`voxel.shadows` set the light that bounces, the scene's distance field and the sun's shadows, and
`voxel.hud` hides what is drawn over the world for a capture. Three measure the light:
`voxel.flicker` records each frame's change in the picture around a turn or a step into a file, with
the first frame after it, the settled one and their difference beside it, `voxel.ring` reads the
last frame's light around a circle on the ground by angle, and `voxel.depth` says how far the block
under a pixel is. The engine's own `gi.*` and `field.*` commands show and measure the light that
bounces.

## Biomes

Two slow noises give each place a temperature and a humidity, as Minecraft's climate does, and
they choose its biome: plains with a few oaks, forests of oak and birch, birch forests, taiga of
spruce, snowy taiga and snowy plains where it is cold, flat desert of sand over sandstone with
cacti where it is hot and dry, and bare stone mountains wherever the ground rises past 82 blocks,
snow above 92. Grass and oak leaves are gray surfaces tinted by a color looked up from the two
values at the corners of Minecraft's colormaps, so their green changes smoothly across a biome's
edge, yellowing near a desert and cooling toward the snow, while the biome itself changes at once.
Birch and spruce leaves keep a color of their own. A hot, dry climate also flattens the land, so a
desert lies flat and the hills sink as they near it.

## Water, glass and ice

Wide basins sink the land under the sea level of 48, where water stands over a floor of sand near
the shore and gravel deeper, its top frozen to ice where the climate is cold, as about a fourteenth
of the land is on seed 1. Water stands still and does not flow. The player swims in it, sinking
slowly, rising while Space is held and moving at a little over half speed, and sees through a blue
tint while the eye is under it. Glass, ice and water are drawn see-through, a second mesh of each
section drawn after the opaque ones and farthest first, each vertex's alpha its surface's. They
cast no shadow, which keeps them out of the scene's distance field as well, since the field holds
a see-through mesh as solid, so the sun and the light that bounces come through a window of glass
as through an opening: a room lit through a skylight reads the same with glass in it as without.
Glass lets the light levels through, and water takes a level more for each block of it, so the sky
fades with depth and a lake's floor darkens as it deepens.

## Trees and structures

Each column places the trees and the structures that start in it and those that start in the
columns around it and reach into it, keeping the blocks that fall inside itself. Where one starts
depends on the seed and the ground at its start alone, which the generator can tell for a column
not generated, so a tree or a building crosses a column's edge whole although every column is
generated alone on a worker, as Minecraft places a structure's pieces chunk by chunk.

A structure has at most one start in each cell of 64 by 64 blocks, where its chance falls and the
biome at its start suits it. A small village house of planks with log corners on a cobblestone floor
stands in the plains, forests and taiga, a glowstone in its ceiling for its torch; an igloo of snow
with a shroomlight inside on the snowy plains; a stepped sandstone pyramid in the desert, a dark
chamber at its heart with a floor of blue and red; and a cobblestone dungeon deep under any ground,
a magma block at its middle where Minecraft's has its spawner. `voxel.find
<house|igloo|pyramid|dungeon>` gives the nearest one's place.

## How the world is drawn

A section of 16 blocks a side is one mesh in one white material, each block's color in its
faces' vertices. The scene's distance field, which the light is traced through, reads those
colors as the model pass does, so the light that bounces takes each block's color. Faces between
two blocks are left out, and each face that remains is two triangles of its own, since greedy
meshing is not written yet. A section meshed again with the same faces keeps its mesh and takes
only new colors.

A block that gives off light (glowstone, a sea lantern, shroomlight, magma) is drawn as a cube of
its own, every cube of one kind in one instanced draw, because a material's light is one for its
whole draw and a section's one material gives off none.

Sections are drawn by their distance from the player and are not culled to the view, because a
mesh left out of a frame leaves the field, and the light it gave or the shadow it cast in the
bounce goes with it until it settles again.

## Light levels and corners

Each block holds Minecraft's two light levels from 0 to 15. The sky's is 15 under the open sky,
falls straight down without losing any and loses one for each block it goes sideways or up, and
a light-giving block's loses one for each block from it, glowstone, sea lanterns and shroomlight
giving 15 and magma 3. A new column's light is worked out on the worker that generates it, joined
to its neighbors' as it arrives, and taken back and spread again around each block placed or
broken.

Each corner of a face is shaded from the four cells in front of the face that meet at it, as
Minecraft's smooth lighting is. Its light is the average of the open ones, its occlusion counts
the solid ones, and the shade darkens the block's color in the vertex, where neither the sky nor
a lamp reaches it and where blocks meet. A level keeps nearly all of the color down to a few
levels from dark and next to none at 0, because the light that bounces brings a lamp's falloff
itself, where Minecraft's own curve, which is its only light, dims by distance. The field reads
the darkened color too, so an unlit cave sends on nearly none of the light that bounces. A change of
light gives a mesh new colors and keeps its vertices, which the field knows it by, so relighting
leaves the meshes settled in the field. Each quad is
split along the diagonal its occlusion favors, so a shaded corner darkens one triangle softly.

The settings window turns the light levels and the shaded corners off, each on its own, to see
the light that bounces alone.

## What is not here yet

Textures, and leaves seen through, which are solid. Water that flows. Greedy meshing. Light that
passes through leaves at a cost, as Minecraft's does. Villages of several houses and paths, and
structures read from files rather than built in code. Caves open to the sky. Mobs, items, an
inventory and survival.

## What it costs

Every section within the render distance is drawn each frame, through the scene field's gather
and the engine's culling of each pass, so the render distance sets much of the frame's cost. It is
8 by default and up to 16 in the settings window, whose first lines give the frame's time, draws
and triangles.
