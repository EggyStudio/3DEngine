# Light that bounces

Light that reaches a surface leaves it again, tinted by its color, and lights what it falls on next,
so a red wall tints the floor beside it, a room the sun shines into through a window is lit away
from the patch on its floor, and a lamp carried through a cave lights it round its corners. The
engine works that light out each frame by radiance cascades, global illumination with nothing baked,
so every light and every mesh may move, and a surface that gives off light lights its room. This
page says how it works, what it costs and where it falls short, and how a scene's light is read
against a path-traced reference. The `shaders_cornell_box` example draws the room it is built on,
and `shaders_bounce_rooms` the rooms the bounce finds hardest.

## Turning it on

`SetGlobalIllumination` takes a quality, `Low`, `Medium` or `High`, or `Off`, the default, which
draws a scene as raylib does. `shaders_cornell_box` lights a white room with a red wall and a green
one by a lamp and a glowing panel on its ceiling, whose light reaches the room only by bouncing:

<!-- compiled with:
var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
-->
```csharp
// The scene's distance field the light is traced through: three cascades, the finest's cells
// 0.15 units wide, two cascades built again a frame.
SetSceneField(3, 0.15f, 2);
SetGlobalIllumination(GlobalIllumination.High);
CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);

// A panel that gives off light of its own lights the room as a lamp would.
var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
panel.Materials[0].Emissive = new Color(255, 240, 220);
panel.Materials[0].EmissiveIntensity = 6;
// ...
DrawModelEx(slab, new Vector3(-2.65f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5.6f, 5.6f), new Color(200, 30, 30));
DrawModelEx(panel, new Vector3(0, 4.97f, 0), Vector3.UnitY, 0, new Vector3(1.6f, 0.06f, 1.6f), Color.White);
```

Where `SetSceneField` has not been called, the bounce turns the field on at four cascades of its
default cell, a quarter of a unit. `Config.GlobalIllumination` and `Config.SceneField` set both for
an app made from a `Config`. Nothing else changes in a program. Its lights, materials and meshes are
those it draws already, and a material's color, its texture's average and its mesh's vertex colors
are the color the light leaves a surface with. Vertex colors written in place with
`UpdateMeshBuffer`, as a voxel game lights a section again by its light levels, reach the light that
bounces in the frame after, the field painting again the cells the mesh is nearest without building
it again.

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
frame grows with the cascades and not with the lights or the meshes. A light switched off takes the
light it bounced with it the frame after, and a mesh moved casts its new shadow into the bounce at
once.

## The scene's distance field

The probes' rays are traced through a distance field of the scene, how far each point is from the
nearest surface of the meshes that cast shadows, which the GPU keeps from frame to frame. The light
that bounces shares it with the occlusion, the sun's contact shadows and the particles that collide,
as [The scene as a distance field](materials-light-and-shadows.md#the-scene-as-a-distance-field) in
the materials guide says.

`SetSceneField(cascades, cellSize, updateBudget)` sets its shape. The field is cascades of 64 cells
a side, each twice as coarse and as wide as the one before, so four cascades of a quarter reach 16
units across in the first and 128 in the last, and the probes stand in the same cascades. The cell
is the finest detail the bounce sees. A wall thinner than a cell is held about its middle so it
still stops light, and a glowing strip thinner than a cell lends its light to the cells near it. A
cascade is built again where the camera has gone past it or a mesh came or went, as many cascades a
frame as the budget says, the finest first. A mesh drawn in the same place for eight frames is built
in from its triangles; one that moves is held as boxes painted its color, so a red figure bounces
red light, though the light it gives off does not bounce unless it is small enough to be a light of
its own, below.

Each cascade lies mostly ahead of the eye, its middle 0.35 of its width in front, so its side behind
the eye is between 1.6 and 9.6 of its cells back. What stands behind the camera farther than that is
held by the coarser cascades alone. `./e3d command field.show 0` draws the first cascade over the
window as the field holds the scene, and `field.state` says where each cascade lies.

## The world's probes

The world's probes stand eight cells of their cascade apart, two units at a cell of a quarter in the
first, and trace from their cascade's spacing to four times it, the first from the probe itself.
Each quality traces a number of directions a probe in each cascade, more where the far light needs
them, as the table below gives. A ray that meets a surface brings back the light that surface sends
on: its color times the sun's light where the field lets it through, the lamps' each that casts
shadows only where the field lets it through too, the light it gives off, and the light that bounced
to it the frame before, so light bounces again each frame and a room fills with it as it would. A
ray that meets nothing brings back the light from all around, the environment map's, the ambient
lights' or a reflection probe's, so a room is lit by the sky through its windows and dark where no
light gets in. A probe deep inside a wall, which would see the light on the wall's far side, is left
out, and the surfaces near it read the probes around it, so a sealed room stays dark beside a lit
one.

## The screen's probes

Beside the world's probes stands a probe for every few pixels of the window, every 16 at `Low`, 12
at `Medium` and 8 at `High`, on the surface the window shows there. Each traces the near light
through the window's depth first, as far as the first cascade's probes stand apart, and takes the
world's probes' light past that, so the light under a table, in a corner or where a wall meets the
floor is seen at the size the window draws it and not at the probes' spacing of two units. The
probes' light is blended with the frame before's where the surface is the same one, held within the
spread of this frame's light around each point, so it holds still as the camera moves and follows a
light that changed at once. A render texture that draws models through a camera of its own, as each
half of a split screen, has screen probes of its own. With the camera sliding the bounce adds
nothing that can be read to the picture's change, after a walk or a turn the light has settled by
the fifth frame, and a block's side lit only by a red wall's bounce comes within a tenth of its new
light in the frame a lamp is brought in. Two views of `games/Sumo` at 640 by 720, each a render
texture, take 1.14 ms of the GPU at `Low` between them, as `./e3d command profile` gives `targets`,
where they took 0.62 reading the world's probes alone. A reflection probe's faces take the light
that bounced from the world's probes alone.

## Glowing surfaces

A small surface that gives off light, one whose box is no longer than twice the spacing of the first
cascade's probes, 4 units at the field's cell of a quarter, lights what is around it as a lamp
would, with no light made for it. Its light reaches each surface from the faces of its box, and what
stands between throws a soft shadow, so a glowing block lights the floor it lies on as a path-traced
reference does, within 5% from a block out to four, at every quality. Glowing blocks that touch, as
a stack of them, are one light. The 64 such lights nearest the eye light so, each as far as it
lights a surface by a fiftieth, 17 units for a glowing block of the voxel game's, and a larger
surface, as a panel across a ceiling, is left to the probes' rays, which meet it often enough. A
frame with none of them pays nothing for them.

## The qualities and what they cost

On the development machine's laptop GPU at 800 by 450 with the frame rate unlimited, the rooms of
`shaders_bounce_rooms` cost this on the GPU, as `./e3d command profile` names it
`global_illumination`, with `./e3d command gi.state` giving the rest:

| Quality | Probe cascades | Directions each | Screen probes | Memory | GPU time |
|---|---|---|---|---|---|
| `Low` | 2 | 64, 64 | every 16 pixels | 1.65 MB | 0.59 ms |
| `Medium` | 3 | 64, 64, 256 | every 12 pixels | 4.38 MB | 0.74 ms |
| `High` | 4 | 64, 256, 256, 256 | every 8 pixels | 9.16 MB | 1.00 ms |

`Low` suits a slow GPU or a scene whose light comes from near, `Medium` most games, and `High` a
scene whose light comes from far off, a cave lit from its mouth or a valley under a sunset. A
quality traces no more cascades than the field has, and the field adds 5 MB a cascade. Against
path-traced references of the bounce rooms' nine views each view's light differs from its
reference's by between 0.02 of it and 0.29, summed pixel by pixel, every quality alike within a few
hundredths, so the qualities differ in how far and how finely the light is seen more than in how
right it is. Each glowing surface carried as a light costs a little more. In a dark room at 1280 by
720 at `High`, one glowing block costs 0.35 ms, sixteen 1.08 and sixty-four 2.77.

## Reflections and the sky

Where light bounces, a glossy surface, one with a roughness under 0.5, traces its reflection too.
The ray is stepped through the window's depth first, and a surface it meets there reflects the light
the frame before showed on it; a ray that leaves the picture is traced on through the field, and the
surface it meets there reflects its color times the light reaching it, so a mirror shows what stands
behind the camera. At `High`, on a GPU that traces rays itself, a reflection the field misses, past
its cascades or too thin for its cells, is traced against the meshes' own triangles, which
`./e3d command gi.rays off` turns off and `gi.rays on` back on. The traced reflection fades into the
reflection probe's or the environment map's as a surface grows rough, from a roughness of 0.25 to
0.5. The environment map's reflection is weighed by the share of the sky the probes around a surface
see along its mirror direction, so a rough wall in a cave sealed from the sky reflects none of it.
In `shaders_reflections` at `Medium` the scene's pass takes 0.30 ms of the GPU with its floor
polished and 0.23 ms with it rough, as `./e3d command profile` names it `hdr_scene`, and at `High`
the GPU's rays see its 8 copies of 6 meshes in 1.23 MB, built again each frame in some 0.02 ms.

## Where it falls short

- **Behind the eye.** A cascade's side behind the eye is a few of its cells back, so a reflection's
  hit or a probe's ray that leaves the field through that side takes the sky's light unhidden, and a
  cave reaching past it lets a little in, 1.7 levels of 255 of the engine's light in a cave of 30 by
  30 blocks under a hill at `High`.
- **A space wider than the last cascade.** A probe's ray that leaves the last cascade takes the sky.
  At `Low`, whose last cascade's rays reach 16 units at the default cell, a cave wider than that
  takes the sky past them, 27 levels in that cave, so a game of deep caves takes `Medium` or `High`.
- **Moving meshes.** A mesh that moves bounces light as the boxes the field holds it as, and the
  light it gives off does not bounce unless it is small enough to be a light of its own.
- **Thin things.** What is thinner than a cell is held about its middle, so a thin wall stops light,
  but a gap thinner than a cell is closed to it.

## Reading it

`DrawBounceWindow()`, called between `BeginDrawing` and `EndDrawing` as any ImGui window is, shows
what the light that bounces holds while a scene's light is worked on, as `shaders_bounce_rooms` does
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
boxes leave parts of the bounce out. The same is in a running program through `./e3d`, as [Driving a
program with e3d](driving-with-e3d.md) says:

| Command | What it does |
|---|---|
| `gi.state` | The quality, the cascades of probes and their rays, the screen's probes, the reflections and the GPU memory each takes |
| `gi.show <view> [cascade or png]` | Draws over the window the screen's probes as tiles, their light, filtered or the frame before's share of it, a cascade's rays' light, its merge or its probes as cubes, or the frame against a reference |
| `gi.toggle <part> <on\|off>` | Leaves out the frame before's light, the screen's filter, the screen's probes, the merge, the light that bounces again, the bounce following a light out, the glowing surfaces carried as lights, or every cascade but one |
| `gi.probe <x> <y> <z> [samples] [bounces] [cascade]` | The light arriving at the probe nearest a point against a path-traced reference of it |
| `gi.rays <on\|off>` | Whether `High` traces what the field misses through the GPU's own rays |
| `gi.reference <png> <samples> [bounces]` | Path traces the window's picture through the GPU's rays, as the reference the bounce is measured by |
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
  rooms, its light reaching round corners and through doorways only as light that bounces
- The cheatsheet's [Light that bounces](../CHEATSHEET.md#light-that-bounces)
- Previous: [Materials, light and shadows](materials-light-and-shadows.md)
- Next: [Shaders and compute](shaders-and-compute.md)