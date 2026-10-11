# 3DEngine.Game

A voxel game in the manner of Minecraft, and the ground floor of a larger one. Its first use is
to test the light that bounces (Radiance Cascades over the scene's distance field) with blocks
that give off light. It holds an endless world of colored blocks in columns of 16 by 16 by 128,
a first-person player with Minecraft's sizes and speeds, building and breaking with an outline
on the block looked at, Minecraft's light levels with smooth lighting and shaded corners, a sky
whose sun crosses it under drifting clouds, and an ImGui window of the light's settings.

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
| Right button | Place the chosen block against the face looked at, or open or shut a door |
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
`voxel.time` and `voxel.cycle` set the sky, `voxel.clouds` shows or hides the clouds,
`voxel.distance` the render distance, `voxel.light` reads a block's light levels, `voxel.shade`
turns the light levels and the shaded corners on or off, `voxel.world` saves the world and opens or
begins another of a kind and seed, `voxel.save` saves at once, and `voxel.blocks` lists the blocks
by number and name. `voxel.gi`, `voxel.field` and `voxel.shadows` set the light that bounces, the
scene's distance field and the sun's shadows, and `voxel.hud` hides what is drawn over the world for
a capture. Three measure the light: `voxel.flicker` records each frame's change in the picture
around a turn or a step into a file, with the first frame after it, the settled one and their
difference beside it, `voxel.ring` reads the last frame's light around a circle on the ground by
angle, and `voxel.depth` says how far the block under a pixel is. The engine's own `gi.*` and
`field.*` commands show and measure the light that bounces.

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
of the land is on seed 1. The player swims in it, sinking slowly, rising while Space is held and
moving at a little over half speed, and sees through a blue tint while the eye is under it. Glass,
ice and water are drawn see-through, a second mesh of each section drawn after the opaque ones and
farthest first, each vertex's alpha its surface's. They cast no shadow, which keeps them out of the
scene's distance field as well, since the field holds a see-through mesh as solid, so the sun and
the light that bounces come through a window of glass as through an opening, and a room lit through
a skylight reads the same with glass in it as without. Glass lets the light levels through, and
water takes a level more for each block of it, so the sky fades with depth and a lake's floor
darkens as it deepens.

Water flows as Minecraft's does, a block every quarter second. It falls into the air under it, and
where it stands on ground or on a source it spreads sideways, a level further from its source for
each block, up to seven, each level standing lower in its cell, and it dries back when its source is
taken away. Two sources with a gap between them over ground fill it with a third. A lake the world
generates stands still until a block beside it changes, and water half flowed when a world is left
stands as it was until then.

## Torches, lanterns and end rods

A torch, a lantern, an end rod, and the soul torch and soul lantern of blue fire are smaller than a
block, each a few boxes as Minecraft's models are. A torch placed on a wall's side leans out from
it, a lantern placed under a block hangs from it, and an end rod points away from the face it is
placed on, and each falls when the block it holds to is broken. Light passes through them, the
player through a torch, and the crosshair meets the box around their pieces. The piece that gives
off light, a torch's flame, a lantern's glass or the rod, is a lamp's cube scaled to it, so the
light that bounces carries it as a small light by its box, and the other pieces are faces of the
section's mesh.

A small light's glow is its light over its surface, so a torch's flame, a 48th of a block's surface,
glows far brighter than glowstone. Each is set so a closed room's floor takes about three quarters
of glowstone's light from a torch or an end rod, nine tenths from a lantern and two fifths from the
soul fire. A flame a quarter of a block from a wall lights a bright round pool on it, as a light
that near a wall does.

## Slabs, stairs, fences and doors

Slabs of stone, cobblestone, oak, spruce and sandstone fill the lower half of a block, or the upper
half where placed against a ceiling or on the upper half of a side, and a slab placed on the open
half of one of its kind joins it into the whole block. Stairs of cobblestone, oak, spruce, sandstone
and stone brick climb the way the player looks, turned over in the upper half. A fence joins the
fences and the whole blocks on its four sides with two rails each. An oak door stands two blocks
high on a whole block, shut across the side away from the way the player looked placing it, and a
right click opens or shuts both its halves, whatever is held. Breaking either half breaks the door.

The player climbs half a block and a little more without jumping, onto a slab or a stair, as
Minecraft's does. A slab, stairs, a door, a lantern, an end rod and a fence stop the player at their
own boxes, a fence's standing half a block above its post so it cannot be jumped, and a torch and
wheat are walked through. Light passes through them all, so a roof of slabs lets the sky's level
down.

## Caves and ravines

Tunnels wind under the ground where two noises are both near zero, and wide caverns open deep down
where a third is high. They stay three blocks under the ground, so they are dark until dug into, but
in patches of high ground away from the sea a tunnel that rises near the top breaks out, a mouth the
sun shines into. A ravine cuts the ground open along a swaying line about 48 blocks long, up to
seven wide at its middle and narrowing to its ends, its floor up to 30 blocks down with a few magma
blocks glowing on it, so the sun's light falls down its walls and the magma's lights its depths at
night. No ravine is cut beside the sea, so no water stands beside its air. `voxel.find ravine` gives
the nearest.

## Trees and structures

Each column places the trees and the structures that start in it and those that start in the
columns around it and reach into it, keeping the blocks that fall inside itself. Where one starts
depends on the seed and the ground at its start alone, which the generator can tell for a column
not generated, so a tree or a building crosses a column's edge whole although every column is
generated alone on a worker, as Minecraft places a structure's pieces chunk by chunk.

A structure has at most one start in each cell of 64 by 64 blocks, where its chance falls and the
land at its start suits it. An igloo of snow with a shroomlight inside stands on the snowy plains, a
stepped sandstone pyramid in the desert, a dark chamber at its heart with a floor of blue and red,
and a cobblestone dungeon deep under any ground, a magma block at its middle where Minecraft's has
its spawner. `voxel.find <village|igloo|pyramid|dungeon|ravine>` gives the nearest one's place.

A village stands where the plains, the taiga, the snowy plains or the desert lie nearly level across
48 blocks, built of that biome's wood or sandstone. A well at its middle has a lantern hanging under
its roof, four roads of dirt path lead out from it, and beside each road stand two plots, each a
small house, a long house, a farm of wheat with a row of water through it, a forge with a hearth of
magma or a pile of hay, its front toward the road, with lamp posts along the roads, a lantern on a
fence, or a torch in the desert. A house has a shut oak door with a torch over it, lanterns hanging
inside and a roof of stairs climbing to a ridge, flat in the desert. Each road follows the ground
block by block and crosses water on planks, each building stands at its own ground's height on a
footing of stone, and no tree grows within the village.

## How the world is drawn

A section of 16 blocks a side is one mesh in one white material, each block's color in its
faces' vertices. The scene's distance field, which the light is traced through, reads those
colors as the model pass does, so the light that bounces takes each block's color. Faces between
two blocks are left out, and each face that remains is two triangles of its own, since greedy
meshing is not written yet. A section meshed again with the same faces keeps its mesh and takes
only new colors.

A block that gives off light (glowstone, a sea lantern, shroomlight, magma) is drawn as a cube of
its own, and the glowing piece of a torch, a lantern or an end rod as that cube scaled to it, every
cube of one surface in one instanced draw, because a material's light is one for its whole draw and
a section's one material gives off none.

Sections are drawn by their distance from the player and are not culled to the view, because a
mesh left out of a frame leaves the field, and the light it gave or the shadow it cast in the
bounce goes with it until it settles again.

## Light levels and corners

Each block holds Minecraft's two light levels from 0 to 15. The sky's is 15 under the open sky,
falls straight down without losing any and loses one for each block it goes sideways or up, and a
light-giving block's loses one for each block from it, glowstone, sea lanterns, shroomlight and
lanterns giving 15, torches and end rods 14, the soul torch and soul lantern 10 and magma 3. A new
column's light is worked out on the worker that generates it, joined to its neighbors' as it
arrives, and taken back and spread again around each block placed or broken.

Each corner of a face is shaded from the four cells in front of the face that meet at it, as
Minecraft's smooth lighting is. Its light is the average of the open ones, its occlusion counts the
solid ones, and the shade darkens the block's color in the vertex, where neither the sky nor a lamp
reaches it and where blocks meet. A level keeps nearly all of the color down to a few levels from
dark and next to none at 0, because the light that bounces brings a lamp's falloff itself, where
Minecraft's own curve, which is its only light, dims by distance. The field reads the darkened color
too, so an unlit cave sends on nearly none of the light that bounces. A change of light gives a mesh
new colors and keeps its vertices, which the field knows it by, so relighting leaves the meshes
settled in the field. The field and the ray-traced scene keep the colors of a mesh's last upload,
though, so the light that bounces off a section a lamp has lit since is as dark as before it until a
block in the section changes. Each quad is split along the diagonal its occlusion favors, so a
shaded corner darkens one triangle softly.

The settings window turns the light levels and the shaded corners off, each on its own, to see
the light that bounces alone.

## Clouds

A layer of blocky clouds drifts east at 120 blocks up, as Minecraft's does, in cells of 12 by 12
blocks and 4 high, which cells hold a cloud following a slow noise of the world's seed. Each cloud
is a box whose faces against a cloud beside it are left out, its top brightest and its bottom
darkest, see-through, lit by the sun and the moon, and fading into the sky at twice the render
distance. The clouds cast no shadow, which keeps them out of the scene's distance field, so they
shade neither the sun's light nor the light that bounces. The cells around the player are one mesh,
made again only when the player moves a cell's width against them, and their drift is the mesh's
place.

## Fog

The sections are drawn through a shader of the game's own, `resources/shaders/terrain.slang`,
which takes the model pass's light, the sun, the shadows, the light that bounces and the sky, over
each vertex's color as the engine's shader for colored meshes does, and fades the result into the
sky's color at the horizon from two thirds of the render distance to the render distance, as
Minecraft's fog hides where the world ends. Under water the haze is the water's blue and covers
the view within 16 blocks. The sky below the horizon keeps the horizon's color a while, so the
haze at the world's edge meets a sky of its own color. The settings window turns the fog off. The
lamps' cubes are drawn with the engine's own shader and take no haze.

## What is not here yet

Textures, and leaves seen through, which are solid. The corners stairs make where two meet, and
slabs and stairs that keep light out, as Minecraft's do. Greedy meshing. Light that passes through
leaves at a cost, as Minecraft's does. Villagers. Structures read from files rather than built in
code. Mobs, items, an inventory and survival.

## What it costs

Every section within the render distance is drawn each frame, through the scene field's gather
and the engine's culling of each pass, so the render distance sets much of the frame's cost. It is
8 by default and up to 16 in the settings window, whose first lines give the frame's time, draws
and triangles.
