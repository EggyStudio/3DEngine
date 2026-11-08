# Global illumination

Global illumination adds the light each surface reflects onto the others, tinted by its color, so a
red wall tints the floor beside it, a room the sun shines into through a window is lit away from the
patch on its floor, and a lamp carried through a cave lights it round its corners. The engine works
it out each frame by radiance cascades, with nothing baked, so every light and every mesh may move,
and an emissive surface lights its room. This page says how it works, what it costs and where it
falls short, and how a scene's light is read against a path-traced reference. The
`shaders_cornell_box` example draws the room it is built on, and `shaders_bounce_rooms` the rooms
global illumination finds hardest. `scenes_sponza` draws Intel's Sponza, fetched with `./e3d scenes
fetch intel-sponza`, from named views at an hour of the day, its lamps off until L turns them on,
their light passing the glass about each bulb.

## Turning it on

`SetGlobalIllumination` takes a quality, `Low`, `Medium` or `High`, or `Off`, the default, which
draws a scene as raylib does. `shaders_cornell_box` lights a white room with a red wall and a green
one by a lamp and an emissive panel on its ceiling, whose light reaches the room only as indirect
light:

<!-- compiled with:
var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
-->
```csharp
// The scene's distance field the light is traced through, three cascades with the finest's cells
// 0.15 units wide and two built again a frame.
SetSceneField(3, 0.15f, 2);
SetGlobalIllumination(GlobalIllumination.High);
CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);

// An emissive panel lights the room as a lamp would.
var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
panel.Materials[0].Emissive = new Color(255, 240, 220);
panel.Materials[0].EmissiveIntensity = 6;
// ...
DrawModelEx(slab, new Vector3(-2.65f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5.6f, 5.6f), new Color(200, 30, 30));
DrawModelEx(panel, new Vector3(0, 4.97f, 0), Vector3.UnitY, 0, new Vector3(1.6f, 0.06f, 1.6f), Color.White);
```

Where `SetSceneField` has not been called, global illumination turns the field on at four cascades
of its default cell, a quarter of a unit. `Config.GlobalIllumination` and `Config.SceneField` set
both for an app made from a `Config`. Nothing else changes in a program. Its lights, materials and
meshes are those it draws already, and a material's color, its texture's average and its mesh's
vertex colors are the color the light leaves a surface with. Vertex colors written in place with
`UpdateMeshBuffer`, as [Voxel-Sandbox](https://github.com/EggyStudio/Voxel-Sandbox), a voxel game
built on the engine, lights a section again by its light levels, reach global illumination in the
frame after, the field painting again the cells the mesh is nearest without building it again.

`SetBounceGain` takes the light past the first bounce times a gain, 1 by default, at which a
surface sends on its color times the light reaching it, as a path-traced reference has it. A gain
above 1 lifts the shade of the rooms the light reaches only by bouncing, the sun's first bounce off
a sunlit court left as it is, and in a closed room of light surfaces whose colors times the gain
come to 1 the light grows over the frames without end, so a gain past 1 suits darker surfaces or a
scene open to the sky.

## Radiance cascades

Each frame, probes trace rays from points around the camera and gather the light the rays meet,
which the model pass then reads at every pixel as the light arriving from all around. A probe must
see the near light finely, a lamp a block away, and the far light coarsely, a lit hillside a hundred
units off, and rays enough to see a small bright thing far away would cost more than a frame has.
Radiance cascades split the work by distance. The first cascade's probes stand close together and
trace few directions a short way; each cascade after stands its probes twice as far apart and traces
from where the one before stopped to twice as far, so the far light, which changes slowly from point
to point, is gathered by few probes, and the near light, which changes fast, by many. A cascade's
light is merged into the one before's, so a probe of the first sees the whole distance at the cost
of its own short rays.

The engine chose them because nothing has to be baked or placed by hand. The probes stand on grids
that follow the camera, a scene that changes is traced again in the next frame, and the cost of a
frame grows with the cascades and not with the lights or the meshes. A light switched off takes its
indirect light with it the frame after, and a mesh moved casts its new shadow into the indirect
light at once.

## The scene's distance field

The probes' rays are traced through a distance field of the scene, how far each point is from the
nearest surface of the meshes that cast shadows, which the GPU keeps from frame to frame. Global
illumination shares it with the occlusion, the sun's contact shadows and the particles that collide,
as [The scene as a distance field](materials-light-and-shadows.md#the-scene-as-a-distance-field) in
the materials guide says.

`SetSceneField(cascades, cellSize, updateBudget, triangleBudget, farCascade)` sets its shape. The
field is cascades of 64 cells a side, each twice as coarse and as wide as the one before, so four
cascades of a quarter reach 16 units across in the first and 128 in the last, and the probes stand
in the same cascades. The cell is the finest detail global illumination sees. A wall thinner than a
cell is held about its middle so it still stops light, and an emissive strip thinner than a cell
lends its light to the cells near it. The cascades lie around the camera, so a turn builds none of
them, or around the point `SetSceneFieldCenter(point)` gives. A bounded level gives its middle once,
so a cascade as wide as the level holds all of it from every view. One a little wider gives a point
a fixed way from the camera, as `scenes_sponza` gives the point half the way to what each view looks
at, so its third cascade holds the part of the building the view shows and a turn moves nothing. A
game seen from above gives its player's place each frame, as `games/Wick` does, so the finest
cascades hold the rooms about the player rather than the air under the camera. A free camera in an
open world gives nothing, and a building wider than its third cascade has its far rooms held by the
fourth's coarser probes. A cascade that point has gone past the grid of keeps the cells it holds,
moved on the GPU, and builds the slab of cells the move brings in alone, as a clipmap does, so a
walk splats a fraction of what building each cascade again did. One the point went half its width
past or more is built again whole, as many cascades a frame as the budget says, the finest first,
and where a mesh came or went only the cells within its band are built again, from the meshes near
them, so a block broken in a world of large section meshes costs a few frames of the budget rather
than the cascades around it. A mesh drawn in the same place for eight frames is built in from its
triangles, and one that moves is held as boxes painted its color and giving its light, so a red
figure reflects red light and a sliding block of lava lights the wall it passes as one held still
does.

A frame's builds splat 32,768 triangles at most unless `triangleBudget` says otherwise, a mesh
counting its triangles by the share of its bounds within the cells built, an eighth of them at
least, the meshes within a cascade nearest the eye first, a mesh at least. A build of more goes on
over the frames after, its cascade holding what it was last built with until the build ends and the
meshes not yet built in left out meanwhile, or held as boxes where they moved before they settled,
so hundreds of sections of a world streamed in at once, on a teleport or a first load, are built
over several frames rather than stalling one. On an RTX 4070 Laptop GPU, 500 sections of 1,024
triangles arriving at once took the field 41.8 ms in one frame with no budget, and 2.5 to 6.5 ms at
most a frame over 27 frames at the default. `games/Ore` splats 18,824 triangles a build, within the
default, so its builds go as before. A scene of fewer triangles builds as many cascades a frame as
`updateBudget` says. Where the frame waits for its target frame rate, as a program at 60 frames a
second does when its work takes less, the triangle budget is raised so the builds take half the
wait, up to sixteen times the budget given, and it falls back to it where the frame waits none, so
`scenes_sponza`'s field is built again after a jump between its views in 5 to 15 frames where in 20
to 25 at the budget given. `./e3d command field.state` says the budget in force. The frame profile
names the field's cost as `scene_field`, which a game reads with `./e3d command profile` to set the
budget for its own device.

A mesh that came and has not moved, as a scene's on its first load or a world's section streamed in,
stands in for nothing until it is built, since a box much larger than what it holds fills the space
around it, as the box of a building's outer walls filled the whole of Intel's Sponza and darkened
every room for the 330 frames its 3.75 million triangles took to build at the default budget, the
atrium's sunlit floor 29, 39 and 54 of 255 where 160, 143 and 123 once built. Left out, it reads
its own from the first frames, and the sky's light reaches the floor in shade through what is not
yet built meanwhile. `scenes_sponza` keeps the default budgets, which build its triangles over
frames, where a budget of four million had each cascade a walk along its atrium carried on splat up
to 4 million triangles in one frame, 0.14 to 0.17 s of the GPU on an RTX 4070.

`farCascade` adds a far cascade past the others, which `cascades` leaves out of its count, 64 cells
a side as each is, its cells as wide as reaching that many units ahead of the camera takes. It is
held to the camera's far plane, so `float.PositiveInfinity` reaches that plane, and the world's
probes stand in it as in the others. A ray that leaves the counted cascades meets the hills and
buildings it holds before the sky, so a valley past the near field is not lit as open ground, and
the occlusion, the contact shadows and the glow lights' marches read on through it. Its cells are
coarse, so it holds land and buildings and loses what is thinner than they are, and it is built
under the budgets as the others are. Its probes trace a quarter of them a frame, so the far land's
light is up to three frames behind, and all of them on a frame it moves. The screen's probes stand
in the counted cascades alone, so a surface past them takes the far cascade's probes as every
surface does at `Low`. It takes 5.5 MB of the GPU's memory beside the 27 of four cascades, and its
probes 2.6 MB at High, as `field.state` and `gi.state` say.

Each cascade lies mostly ahead of the eye, its middle 0.35 of its width in front, so its side behind
the eye is between 1.6 and 9.6 of its cells back. What stands behind the camera farther than that is
held by the coarser cascades alone. `./e3d command field.show 0` draws the first cascade over the
window as the field holds the scene, and `field.state` says where each cascade lies.

Past the probes' reach a surface takes a sky over a ground in place of their light, the sky's light
from above on the part of the sphere above its horizon and the ground's under it, the ground taken
as the surface's own color lit by the sky and by the sun where the shadow map lets the sun reach the
ground before it. It fades in across the last cascade's outer eighth, so a floor or a wall seen
across the field's edge changes from one pixel to the next by under 1%.

## The world's probes

The world's probes stand eight cells of their cascade apart, two units at a cell of a quarter in the
first, and trace from their cascade's spacing to four times it, the first from the probe itself.
Each quality traces a number of directions a probe in each cascade, more where the far light needs
them, as the table below gives. A ray that meets a surface brings back the light that surface sends
on. That is its color times the sun's light where the field lets it through and the lamps', each
that casts shadows only where the field lets it through too, with its emission and the indirect
light that reached it the frame before, so each frame adds a bounce and a room fills with light as
it would. A
ray that meets nothing brings back the light from all around, the environment map's, the ambient
lights' or a reflection probe's, so a room is lit by the sky through its windows and dark where no
light gets in. A probe deep inside a wall, which would see the light on the wall's far side, is left
out, and the surfaces near it read the probes around it, so a sealed room stays dark beside a lit
one.

## The screen's probes

At `Medium` and `High`, beside the world's probes stands a probe for every few pixels of the window,
every 12 at `Medium` and 8 at `High` up to 1080 pixels high, on the surface the window shows there. Each traces the near
light through the window's depth first, as far as the first cascade's probes stand apart, and takes
the world's probes' light past that, so the light under a table, in a corner or where a wall meets
the floor is seen at the size the window draws it and not at the probes' spacing of two units. A
probe's sixteen rays leave from other places each frame, and the probe keeps up to sixteen frames
of its light where the surface is the same one, held within the spread of this frame's light around
each point, so a bright patch narrower than a ray's share of the sky, a torch's glow on a wall or
the sun through a small window, lights a share of every probe near it rather than one probe frame
after frame, which the blend between the probes drew as a round spot. The light holds still as the
camera moves and follows a light that changed at once. A render texture that draws models through a camera of its own, as each
half of a split screen, has screen probes of its own. With the camera sliding global illumination
adds nothing that can be read to the picture's change, after a walk or a turn the light has settled
by the fifth frame, and a block's side lit only by a red wall's indirect light comes within a tenth
of its new light in the frame a lamp is brought in. Two views of `games/Sumo` at 640 by 720, each a
render texture, took 1.14 ms of the GPU between them with screen probes every 16 pixels, as
`./e3d command profile` gives `targets`, where they took 0.62 reading the world's probes alone. A reflection probe's faces take
the indirect light from the world's probes alone.

The screen's probes come with global illumination at `Medium` and `High`, and `Low` leaves them
out, every surface reading the cascades' light alone, smoother and coarser, with nothing of the
light under a table that is smaller than the probes' spacing, so a slow GPU has the cheaper path
with no switch of its own. A surface by a wall thinner than the field's cell reads the probes it
sees through the field there, so a lamp outside a room of thin walls lights none of the floor along
their foot inside. In the Cornell box of `shaders_bounce_rooms` at 800 by 450 on an RTX 4070
Laptop GPU, the screen's probes take 0.92 ms of the GPU at `High` where no emissive surface is
carried as a light. At `Low` the small emissive surfaces carried as lights are marched to once a
tile by probes standing for them alone, which trace nothing else, so a pixel reads their light at
the same cost however many there are. In a voxel game's closed room of forty glowstones at night,
all in view, at 1280 by 720, the scene pass took 2.2 ms with the screen's probes and 1.2 with those
standing for the glow lights alone, where each pixel marching to every glow light itself took 13.3.
For debugging, `gi.toggle screen off` and the box of the screen's probes in the global
illumination window leave them out whole at any quality, those standing for the glow lights at
`Low` with them, neither made nor traced, for the window and a render texture, so what they cost and
give can be read, and each pixel then marches to the glow lights itself.

Past 1080 pixels high the probes stand further apart as the window is higher, every 11 pixels at
`High` at 2560 by 1440 and every 16 at 3840 by 2160, so each stands for about the same share of the
picture on any screen and their cost holds near 1080p's. In the voxel game's room of forty
glowstones, offscreen on the same GPU, global illumination and the scene pass took this with the
screen's probes at `High`, with `Low`'s probes for the glow lights alone, and with each pixel
marching to the glow lights, as the debugging toggle has it.

| Window | `High`, global illumination | `High`, scene pass | `Low`, global illumination | `Low`, scene pass | Marching, scene pass |
|---|---|---|---|---|---|
| 1280 by 720 | 8.3 ms | 2.6 ms | 2.5 ms | 1.3 ms | 13.8 ms |
| 1920 by 1080 | 13.1 ms | 4.9 ms | 2.9 ms | 2.7 ms | 29.2 ms |
| 2560 by 1440 | 12.4 ms | 7.6 ms | 3.4 ms | 4.8 ms | 49.5 ms |
| 3840 by 2160 | 13.3 ms | 16.2 ms | 4.5 ms | 10.5 ms | 107.7 ms |

Every 8 pixels whatever the window, global illumination took 17.8 ms there at 2560 by 1440 and 35.9
at 3840 by 2160, and the Cornell box's 4.5 and 7.2 ms where 3.1 and 3.7. The nine rooms of
`shaders_bounce_rooms` read as far from their path-traced references at 3840 by 2160 as at 1920 by
1080, within 0.002 of their light.

## Emissive surfaces

A small emissive surface, one whose box is no longer than twice the spacing of the first cascade's
probes, 4 units at the field's cell of a quarter, lights what is around it as a lamp would, with no
light made for it. Its light reaches each surface from the faces of its box, and what stands between
throws a soft shadow, so an emissive block lights the floor it lies on as a path-traced reference
does, within 5% from a block out to four, at every quality. Emissive blocks that touch, as a stack
of them, are one light. A mesh of many such surfaces, as a voxel game draws its blocks of a kind in
one mesh, is cut into them, its triangles joined where they share a corner, each lighting from the
faces of its box it shows, as a block sunk in the ground shows its top alone. The 64 such lights
nearest the eye light so, each as far as it lights a surface by a 300th of its own light, 21 units
for a block however bright, and a mesh of more parts than that lights the ground around none of the
rest, which `gi.state` counts. Past the four brightest at a point, the rest are gathered by their
way from it, each group shadowed as its brightest is, so a room lit by many small lamps takes the
light of every one. A larger surface, as a panel across a ceiling, is left to the probes' rays,
which meet it often enough. A frame with none of them pays nothing for them.

## Leaves and other surfaces that let light through

The scene's distance field holds every surface solid, so the probes' rays stop at a canopy and the
floor under it takes none of the sky's light, where a canopy's leaves, a hedge or a net let some of
it through. A material's `LightThrough` sets the share of global illumination's light that passes a
unit of the surface's depth, from 0, solid, the default, to 1, which lets all of it through:

<!-- compiled with:
var leaves = LoadModelFromMesh(GenMeshCube(1, 1, 1));
-->
```csharp
leaves.Materials[0].LightThrough = 0.5f;
```

A ray that meets such a surface crosses the cells it fills and keeps that share of its light for
each unit it runs through, the rest taking the surface's color as a solid's would, the cascades'
rays and the screen's probes' alike, and the first such surface a ray crosses stands for every one
it crosses, as a canopy's leaves take much the same light, so a ray is shaded twice at most. Each
surface is crossed once, by the cascade in whose interval the ray enters it, its ray following it
past the interval's end to where it leaves it, and a screen probe's ray crosses whole what it
enters within its reach and takes the light past it from where it leaves it. A ray at a slant
crosses more of the surface, so under a slab a unit thick letting half through a unit a floor takes
about a third of the sky's light a solid slab holds back, 0.36 of it at `Low` and 0.38 at `High`
two units under it where the slab's depth along each way gives 0.36, 0.42 and 0.39 five units under
it where 0.41, and under one letting all of it through 0.99. A mesh entity's `Material` takes
`LightThrough` as well. The sun's shadow, through the shadow map, falls as before. A frame in which
no surface drawn lets light through traces the probes' rays with a build of their shader that
leaves the crossing out, so it pays nothing for it. Where leaves fill much of the view, as a canopy
thirty units across over the eye at 800 by 450, crossing them takes global illumination from 0.88
ms to 1.29 at `Low` and from 1.91 to 2.96 at `High`, and in a
voxel world's grove at 1920 by 1080, its canopies thousands of leaf blocks' faces, from 1.4 to 2.5
at `Low` and from 3.3 to 5.3 at `High`, most of it the rays' march past the leaves, which `./e3d
command gi.toggle through off` reads by stopping the rays at them.

## The qualities and what they cost

On the development machine's laptop GPU at 800 by 450 with the frame rate unlimited, the Cornell box
of `shaders_bounce_rooms` costs this on the GPU, read on 2026-11-03, as `./e3d command profile`
names it `global_illumination`, with `./e3d command gi.state` giving the rest. At `Low` the
probes standing for the room's four small emitters alone are in the time.

| Quality | Probe cascades | Directions each | Screen probes | Memory | GPU time |
|---|---|---|---|---|---|
| `Low` | 3 | 64, 64, 64 | none, those standing for the glow lights alone | 8.28 MB, 3.65 with no glow light | 0.91 ms |
| `Medium` | 3 | 64, 64, 256 | every 12 pixels | 10.46 MB | 1.90 ms |
| `High` | 3 | 64, 64, 256 | every 8 pixels | 10.83 MB | 1.98 ms |

A cascade of 64 directions, the first two at every quality and the third at `Low`, sends
four rays along each, one from the middle of each quarter of the direction's texel, and takes their
light together, so a lamp's bright spot under a ceiling stands for a quarter of the texel it falls in
rather than the whole of it, and every probe sends 256 rays. That costs 0.10 to 0.19 ms of global
illumination at `High` and 0.03 to 0.09 ms at `Low` and `Medium` at 800 by 450 on the development
machine's GPU.

Each quality keeps to a budget on the development machine's RTX 4070, read in Sponza's atrium and
the bounce rooms with `./e3d command profile`, without the glow lights global illumination takes
under 2 ms at `Low`, 3 at `Medium` and 4 at `High` at 1920 by 1080, Sponza's atrium 1.56, 2.85 and
3.83, and at 3840 by 2160 at most half again as much, the atrium 1.57, 2.94 and 4.04, since the
screen's probes and the glow lights' texels stand near 1080p's count past it. The glow lights add
0.3 to 0.4 ms at any size, whatever their count, Sponza's 23 of them 0.29 to 0.42.

`Low` suits a slow GPU or a scene whose light comes from near, `Medium` most games, and `High` a
scene whose light comes from far off, a cave lit from its mouth or a valley under a sunset. A
quality traces no more cascades than the field has, and the field adds 5.5 MB a cascade. Against
path-traced references of the nine views of `shaders_bounce_rooms` each view's light differs from
its reference's by between 0.02 of it and 0.29, summed pixel by pixel, every quality alike within a
few hundredths, so the qualities differ in how far and how finely the light is seen more than in how
right it is. Each emissive surface carried as a light costs a little more. In a dark room at 1280 by
720 at `High`, one emissive block costs 0.35 ms, sixteen 1.08 and sixty-four 2.77.

## Reflections and the sky

Where global illumination is on, a glossy surface, one with a roughness under 0.5, traces its
reflection too. The ray is stepped through the window's depth first, and a surface it meets there
reflects the light the frame before showed on it; a ray that leaves the picture is traced on through
the field, and the surface it meets there reflects its color times the light reaching it, so a
mirror shows what stands behind the camera. At `High`, on a GPU that traces rays itself, a
reflection the field misses, past its cascades or too thin for its cells, is traced against the
meshes' own triangles, which `./e3d command gi.rays off` turns off and `gi.rays on` back on. The
traced reflection fades as a surface grows rough, from a roughness of 0.25 to 0.5, into the light
the probes around the surface hold along its mirror way, which spreads wider as the roughness grows,
so stone and wood reflect the room around them, and a reflection probe's box keeps its capture. A
surface lying in a plane the program sets mirrors the
scene drawn from the camera mirrored in it instead, as [A mirror
plane](materials-light-and-shadows.md#a-mirror-plane) says. Under a roughness of 0.5 the
environment map's reflection takes what the trace and the probes leave, weighed by the share of the
sky the probes around a surface see along its mirror direction, so a glossy floor in a cave sealed
from the sky reflects none of it. In `shaders_reflections` at `Medium`
the scene's pass takes 0.30 ms of the GPU with its floor polished and 0.23 ms with it rough, as
`./e3d command profile` names it `hdr_scene`, and at `High` the GPU's rays see its 8 copies of 6
meshes in 1.23 MB, built again each frame in some 0.02 ms.

## Where it falls short

- **Behind the eye.** A cascade's side behind the eye is a few of its cells back, so a reflection's
  hit or a probe's ray that leaves the field through that side takes the sky's light unhidden, and a
  cave reaching past it lets a little in, 1.7 levels of 255 of the engine's light in a cave of 30 by
  30 blocks under a hill at `High`.
- **A space wider than the field.** A probe's ray of the last cascade goes on until it meets a
  surface or leaves the field, and one that leaves takes the sky, so a cave lets the sky in only
  where it reaches past the field, 128 units across at the default cell, or past its side behind
  the eye (above).
- **Moving meshes.** A mesh that moves reflects and gives light as the boxes the field holds it
  as.
- **Thin things.** A closed mesh thinner than a cell is held about its middle, so a thin wall stops
  light, but a gap thinner than a cell is closed to it. A double-sided face is held by its plane in
  the cells it is nearest, so two of them two cells apart, as a field of crops, leave the gap
  between them open.

The terms of the method that stand unmended, each with its cause, the room and the figure that
show it and the forms read against it, are under Where the method stops in
[RENDERING.md](../.github/RENDERING.md).

## Reading it

`DrawBounceWindow()`, called between `BeginDrawing` and `EndDrawing` as any ImGui window is, shows
what global illumination holds while a scene's light is worked on, as `shaders_bounce_rooms` does
on Tab:

<!-- compiled with:
var showBounce = true;
-->
```csharp
BeginDrawing();
// ...
if (showBounce) DrawBounceWindow();
EndDrawing();
```

It gives what `gi.state` gives and draws the view chosen from its list over the window, and its
boxes leave parts of global illumination out. The same is in a running program through `./e3d`, as
[The command line](command-line.md) says.

| Command | What it does |
|---|---|
| `gi.state` | The quality, the cascades of probes and their rays, the screen's probes, the reflections and the GPU memory each takes, how many small emitters are carried as lights, and at `High` where the device traces rays how many probes of each cascade carry their rays past their interval with the rays moving |
| `gi.show <view> [cascade or png]` | Draws over the window the screen's probes as tiles, their light, filtered or the share of it the frames before gave, a cascade's rays' light, its merge or its probes as cubes, or the frame against a reference |
| `gi.passes <on\|off>` | Times global illumination's passes apart in the profile, the ray scene, each cascade's trace, the merges, the gather, the screen's probes, their filter and the glow lights, as `gpu.gi_` lines, the rest staying `gpu.global_illumination` |
| `gi.toggle <part> <on\|off>` | Leaves out the frame before's light, the screen's filter, the screen's probes, neither made nor traced, the merge, the light past the first bounce, the indirect light following a light out, the emissive surfaces carried as lights, the surfaces' reflection of the indirect light, or every cascade but one, or with `meshes on` traces the probes' rays through the GPU's own rays at `High` |
| `gi.probe <x> <y> <z> [samples] [bounces] [cascade]` | The light arriving at the probe nearest a point against a path-traced reference of it, each face by where its ways were met, and the ways that carry most of the face furthest from it, each with how far its rays went against what and how far the reference's path through the texel's middle first met, and whether the probe was carried past its interval with its rays moving |
| `field.march <x> <y> <z> [dx] [dy] [dz] [sun]` | Marches the scene's distance field from a point along a way, or toward the sun, as the passes march it, and says where it met a surface, in which cascade and cell, and each step, with `sun` marching on from that surface toward the sun as a probe's hit does |
| `gi.screenprobe <x> <y> [samples] [bounces]` | Each of the sixteen rays of the screen's probe under a pixel, where it was met and its light, against a path-traced reference along the same way |
| `gi.rays <on\|off>` | Whether `High` traces what the field misses through the GPU's own rays |
| `gi.reference <png> <samples> [bounces] [specular\|gloss]` | Path traces the window's picture through the GPU's rays, as the reference global illumination is measured by, `-1` bounces for as many as a path takes, `specular` for the first face's reflection of the indirect light and `gloss` for every face's |
| `gi.compare <png>` | The window's light against that reference, region by region, with a picture of the difference |

A scene is read against its reference by drawing it, tracing `gi.reference` once with a thousand
samples a pixel, and reading `gi.compare` for how far each region's light lies from it, or
`gi.show difference` for where, red where the frame is brighter and blue where it is darker. The
reference needs a GPU that traces rays.

## See also

- Examples: [`shaders_cornell_box`](../3DEngine.Examples/Shaders/ShadersCornellBox.cs),
  [`shaders_bounce_rooms`](../3DEngine.Examples/Shaders/ShadersBounceRooms.cs),
  [`shaders_reflections`](../3DEngine.Examples/Shaders/ShadersReflections.cs),
  [`shaders_scene_field`](../3DEngine.Examples/Shaders/ShadersSceneField.cs)
- [`games/Wick`](../games/Wick/Program.cs), a game lit by a lamp the player carries through dark
  rooms, its light reaching round corners and through doorways only as indirect light
- [`games/Ore`](../games/Ore/Program.cs), a voxel landscape whose mesh's vertex colors global
  illumination reads, walked in the first person, its emissive ore the only light at night
- The cheatsheet's [Global illumination](../CHEATSHEET.md#global-illumination)
- Previous: [Materials, light and shadows](materials-light-and-shadows.md)
- Next: [Shaders and compute](shaders-and-compute.md)