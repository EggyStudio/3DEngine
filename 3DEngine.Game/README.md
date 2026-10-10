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
./e3d open 3DEngine.Game/bin/Debug/net10.0/3DEngine.Game --hidden
```

## Controls

| Input | Does |
|---|---|
| Mouse | Look |
| W, A, S, D | Walk |
| Space | Jump, or rise while flying. Pressed twice quickly, start or stop flying |
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
reads a block's light levels, `voxel.shade` turns the light levels and the shaded corners on or
off, `voxel.world` begins a new world, and `voxel.blocks` lists the blocks by number and name. The engine's own
`gi.*` and `field.*` commands show and measure the light that bounces.

## How the world is drawn

The scene's distance field, which the light is traced through, takes a draw's material color and
emission as the color and glow of what it holds, and not its vertices' colors. So a section of
16 blocks a side is meshed into one mesh for each surface it shows, a grass top, a dirt side, red
concrete, each drawn with that surface's material, and a surface whose faces are unchanged by an
edit keeps its mesh. Faces between two blocks are left out, and each face that remains is two
triangles of its own, since greedy meshing is not written yet.

A block that gives off light (glowstone, a sea lantern, shroomlight, magma) is drawn as a cube of
its own, every cube of one kind in one instanced draw. The field holds a mesh with new vertices
as a few boxes that give off no light for its first eight frames, so a lamp inside a section's
mesh would go out each time a block beside it changed. Drawn apart, it stays lit.

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
the solid ones, and the shade is the vertex's color, which darkens the face where neither the
sky nor a lamp reaches it and where blocks meet. The scene's distance field does not read vertex
colors, so the light that bounces still takes each surface's own color, and the shade decides how
much of that light a face shows. A change of light gives a mesh new colors and keeps its vertices,
which the field knows it by, so relighting leaves the meshes settled in the field. Each quad is
split along the diagonal its occlusion favors, so a shaded corner darkens one triangle softly.

Every surface also reflects the sky, and the engine does not occlude that reflection, so a cave
sealed in stone shows a blue sheen at noon. The game dims the whole sky while the player stands
where the sky's level is low, eased over about half a second, as Minecraft's caves darken. The
settings window turns each of these off, to see the light that bounces alone.

## What is not here yet

Textures, water and anything seen through, leaves included, which are solid. Greedy meshing.
Light that passes through leaves at a cost, as Minecraft's does. Saving a world.
Features and structures that cross from one column into the next, so a tree is placed only two
blocks or more from its column's edge, and villages and the like have nowhere to go yet. Caves
open to the sky. Mobs, items, an inventory and survival.

## What it costs

Every section within the render distance is drawn each frame, through the scene field's gather
and the engine's culling of each pass, so the render distance sets much of the frame's cost. It is
6 by default and up to 16 in the settings window, whose first lines give the frame's time, draws
and triangles.
