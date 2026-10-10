# Materials, light and shadows

A model's look comes from its material, which says what color its surface is and how it takes
light, and from the lights of the world, which may cast shadows. The surface follows glTF's
metallic-roughness model, so a model exported from Blender looks as it did there.

## A material

Each mesh of a model is drawn with a `ModelMaterial` from `model.Materials`. A material is a color
and a texture, which multiply each other, and how metallic and how rough the surface is. The
`models_skybox` example draws four spheres from a mirror to chalk:

```csharp
// Spheres from mirror to chalk, which reflect the sky drawn behind them.
var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 32, 32));
// ...
for (int i = 0; i < 4; i++)
{
    sphere.Materials[0] = new ModelMaterial(new Color(230, 230, 235)) { Metallic = i < 2 ? 1 : 0, Roughness = 0.05f + i * 0.3f };
    DrawModel(sphere, new Vector3(-3 + i * 2, 0.5f, 0), 1, Color.White);
}
```

`Metallic` is 0 for plastic, stone and wood and 1 for bare metal, and `Roughness` is 0 for a mirror
and 1 for chalk, 0.5 unless set. A metal takes the color of what it reflects, tinted by its own,
and a rough surface spreads its highlight wide and dim. The tint a draw call gives multiplies the
material's color, so one model is drawn in several colors without a material each.

A material is a value, so a material set before a draw is the one that draw uses, which is how one
sphere above is drawn four ways in a frame.

## Maps

A material from a file usually carries maps, images that vary the surface across it. A program
sets them on a material of its own the same way:

| Property | Holds |
|---|---|
| `Texture` | The color, in sRGB |
| `NormalMap` and `NormalScale` | Bumps that light falls across, tangent space with up toward the top of the image |
| `MetallicRoughnessMap` | Roughness in green and metallic in blue, as glTF packs them, multiplying the two values |
| `OcclusionMap` and `OcclusionStrength` | Creases, darkening the light from all around |
| `EmissiveMap` | Where the surface gives off its `Emissive` color |

```csharp
var crate = LoadModelFromMesh(GenMeshCube(1, 1, 1));
crate.Materials[0].Texture = LoadTexture("resources/crate.png");
crate.Materials[0].NormalMap = LoadTexture("resources/crate_normal.png");
```

A default texture in a map's place means none.

## Glowing and see-through surfaces

`Emissive` is the color of the light a surface gives off whatever lights it, as a screen, a lamp's
bulb or lava, with `EmissiveIntensity` to brighten it. It shows with no light at all, and lights
nothing else, so a lamp is an emissive model with a light placed inside it.

<!-- compiled with:
Model lamp = default;
-->
```csharp
lamp.Materials[0] = new ModelMaterial(Color.White) { Emissive = new Color(255, 200, 120), EmissiveIntensity = 2 };
```

Light past white shows as white until bloom spreads it into the pixels around it, as a bright
light glows through a lens. `SetBloom(intensity, threshold)` turns it on, 0 turns it off, which it
is by default, and the threshold, 1 unless given, is how bright a pixel has to be to glow, so a
surface lit no brighter than white stays sharp. Text and shapes drawn after `EndMode3D` go on top
untouched. `shaders_bloom` runs it, with B turning it off and on. A glowing orb or a bulb casts a
shadow like any surface unless its material's `CastsShadows` is false, as `games/Summit`'s orbs'
are.

```csharp
SetBloom(0.8f);
```

A material's `AlphaMode` says what the alpha of its color and texture does:

| Mode | Draws |
|---|---|
| `MaterialAlphaMode.Blend` | The surface laid over what is behind it, by its alpha, the default |
| `MaterialAlphaMode.Mask` | Solid where alpha is at least `AlphaCutoff` and nothing where it is below, as leaves and fences need |
| `MaterialAlphaMode.Opaque` | Solid everywhere, alpha ignored |

`DoubleSided` is false unless set, as raylib draws only a face's front, so a leaf or a flag that
shows from behind sets it, and a glTF file says for each of its materials.

## Light under the surface

Skin, wax, marble, milk and a leaf let light in, and it leaves them a little way from where it
entered, so the line between their lit and shadowed sides is soft and takes the color that travels
farthest under them, red in skin. `SubsurfaceRadius` is how far that light travels, in world units,
0 for none by default, and `SubsurfaceColor` how far each of red, green and blue goes, as a share
of the radius, white for all alike:

<!-- compiled with:
Model head = default;
-->
```csharp
head.Materials[0] = head.Materials[0] with { SubsurfaceRadius = 0.01f, SubsurfaceColor = new Color(255, 90, 60) };  // skin, a centimeter
```

The light such a surface scatters diffusely is spread over the window's frame, across and then
down, as far as each color travels at the surface's distance from the eye, and the light it
reflects stays sharp. A pixel of any other surface is left as it was, and the light does not cross
from a near surface to one behind it. A render texture and a reflection probe's faces draw the
surface without it, and so does a material with a shader of its own or one laid over by alpha. In
`games/Manor` at 1280 by 720, every one of its 139 materials scattering over 5 cm takes the HDR
frame's pass about 1.0 ms of the GPU where it takes 0.45, and one of them 0.65 to 0.70, as `./e3d
command profile` gives `hdr_scene`. A mesh entity's `Material` has the same `SubsurfaceRadius` and
`SubsurfaceColor`, the color's shares from 0 to 1.

## Effects over the frame

Besides bloom, eight effects change how the scene is shown, each set by one call and each off until
set. The scene is drawn into a frame that holds light past white whether or not any is on, and one
pass brings that frame into the window, bending the light past white under it with the curve, so a
scene, and a shader of a program's own drawn in it, look the same whichever effects are on, but for
what each effect changes. What is drawn after `EndMode3D`, text, shapes and ImGui, goes over the
result untouched.

That frame and its pass cost some 0.02 to 0.035 ms of a laptop's RTX 4070 with every effect off,
as `./e3d command profile` gives the GPU's time for `hdr_scene` and `main_pass` with the frame rate
unlimited: 0.082 ms where the models drawn straight into the window took 0.047 in `shaders_bloom`
with its bloom off at 800 by 450, and 0.089 where they took 0.066 in `games/Pusher` at 960 by 540.
Bloom's chain adds about 0.03 ms (`bloom`). Bloom, an exposure that follows the scene, the depth of
field, motion blur, light that bounces and particles read the scene's light, and the frame is
decoded for them once, about 0.02 ms more. The frame takes 44 bytes a pixel of the GPU's memory at
four samples, drawing into the window's own multisampled depth, 16 MB at 800 by 450 and 91 MB at
1920 by 1080, where `nvidia-smi` gave `shaders_bloom` 96 MiB more with it than with no such frame,
its bloom chain's levels in that. A frame of 2D alone, as `textures_bunnymark` draws, draws none of
it.

| Call | What it does |
|---|---|
| `SetExposure(1.5f)` | Scales the scene's light before its curve, brighter above 1 and dimmer below |
| `SetAutoExposure(true)` | Makes the exposure follow the scene as an eye adapts, between a quarter and four times unless bounds are given |
| `SetTonemap(Tonemap.AgX)` | The curve that brings light past white under it: the engine's own, which leaves colors under 0.9 as they are, Narkowicz's fit of ACES, `Aces`, a cut at 1, `Clamp`, or one of the eight Bevy offers, below |
| `SetColorGrading(1.1f, 0.8f, new Color(255, 240, 220))` | Contrast, saturation and a tint, 1, 1 and white leaving it as it is |
| `SetVignette(0.4f)` | Darkens toward the corners, from half the way out unless a radius is given |
| `SetFxaa(true)` | Smooths the jagged edges multisampling leaves, inside a surface and of thin lines |
| `SetDepthOfField(8, 2, 0.02f)` | Keeps what is 8 units from the camera sharp and blurs what is nearer or farther, to its widest 2 units either side |
| `SetMotionBlur(0.5f)` | Smears the picture along the way the camera moved since the frame before, as a film camera's shutter does |

The eight curves Bevy offers are choices too, named as Bevy names them and drawn as Bevy draws them,
so a picture tonemapped here is the one a Bevy game draws:

| Curve | Draws |
|---|---|
| `Tonemap.None` | No curve, each channel cut at 1, as `Clamp` does |
| `Tonemap.Reinhard` | Each channel over one plus itself, its hues shifting as they brighten |
| `Tonemap.ReinhardLuminance` | The color over one plus its luminance, which keeps a bright color's hue better |
| `Tonemap.AcesFitted` | Stephen Hill's fit of ACES, film-like and high in contrast, a bright red turning orange |
| `Tonemap.AgX` | Troy Sobotka's AgX, neutral and a little desaturated |
| `Tonemap.SomewhatBoring` | Tomasz Stachowiak's plain transform, to judge the others against |
| `Tonemap.TonyMcMapface` | Tomasz Stachowiak's, Bevy's default, neutral and keeping saturation in the highlights |
| `Tonemap.BlenderFilmic` | Blender's filmic view transform, to match a render made there |

AgX, Tony McMapface and Blender's filmic look the light up in Bevy's own tables, 3D textures read
the first time their curve is chosen. Reading one takes 0.4 ms of the CPU for AgX's, 1.6 for Tony
McMapface's and 2.4 for Blender's, the best of five reads warm, and they hold 256 KB, 432 KB and
2 MB of the GPU's memory. Looking the light up costs the pass that brings the frame into the window
about 0.002 ms more on a laptop's RTX 4070 at 800 by 450, 0.015 ms against 0.013 to 0.014 for the
engine's curve, as `./e3d command profile` gives `main_pass` in `shaders_bloom`.

The `shaders_bloom` example gives each a key:

<!-- compiled with:
Tonemap curve = default;
bool vignette = false, graded = false, fxaa = false, bright = false, focus = false, blur = false;
-->
```csharp
if (IsKeyPressed(Key.T)) SetTonemap(curve = (Tonemap)(((int)curve + 1) % Enum.GetValues<Tonemap>().Length));
if (IsKeyPressed(Key.V)) SetVignette((vignette = !vignette) ? 0.6f : 0);
if (IsKeyPressed(Key.G)) SetColorGrading(1, (graded = !graded) ? 0.3f : 1, graded ? new Color(255, 225, 190) : Color.White);
if (IsKeyPressed(Key.F)) SetFxaa(fxaa = !fxaa);
if (IsKeyPressed(Key.E)) SetExposure((bright = !bright) ? 1.8f : 1);
// ...
if (IsKeyPressed(Key.D)) SetDepthOfField(8.3f, 2, (focus = !focus) ? 0.025f : 0);
if (IsKeyPressed(Key.M)) SetMotionBlur((blur = !blur) ? 0.6f : 0);
```

The depth of field and motion blur work from the depth of what is drawn inside `BeginMode3D`, so the
text and interface drawn after stay sharp. `SetMotionBlur(amount)` knows the camera's movement
alone, so a thing moving past a still camera stays sharp, and `SetMotionBlur(amount, objects: true)`
blurs each mesh entity along its own movement too, read from where its transform put it the frame
before. A model drawn with `DrawModel` has no frame before to read and blurs by the camera's
movement, and so do a skinned mesh's limbs, which move its vertices rather than its transform.

An exposure that follows the scene brings a dim room up and a sunlit field down, over a second or
so, as an eye does, and `SetExposure` then multiplies what it chooses. The `shaders_auto_exposure`
example rides a camera out of a tunnel lit by a few lamps into a field in the sun and back:

```csharp
SetAutoExposure(true, min: 0.3f, max: 6, speed: 1.5f);
```

The field is dazzling for a moment on the way out, and the tunnel dark on the way back in, until the
exposure catches up.

## Lights

A world with no lights draws its models unlit, their color and texture as they are, as raylib
draws them, with the light a material gives off. The first light a program makes turns lighting
on, and a scene that should look lit from the start makes one, often a sun and a little light
from all around:

```csharp
CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 0.65f);
SetAmbientLight(Color.White, 0.35f);
```

There are four kinds:

| Call | Lights |
|---|---|
| `CreateDirectionalLight(direction, color, intensity)` | From far away along a direction, as the sun |
| `CreatePointLight(position, color, intensity, range)` | Every way from a point, fading with distance, as a lamp |
| `CreateSpotLight(position, direction, color, intensity, inner, outer, range)` | A cone from a point, as a torch |
| `SetAmbientLight(color, intensity)` | Everything alike from all around, as the sky's light in shade |

Each returns a handle the program moves, turns or recolors later with `SetLightPosition`,
`SetLightDirection` and `SetLightColor`, and removes with `UnloadLight`. A point or spot light's
range is where its light comes smoothly to nothing, and 0 lets it reach every distance. Up to 16
lights light a frame.

The `shaders_shadowmap` example lights its scene with a dim sun and a lamp that circles between
pillars:

<!-- compiled with:
float t = 0;
-->
```csharp
// A dim sun for the shape of things, and a lamp circling between pillars, both casting
// shadows. Space turns the lamp's shadows off and on.
CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), new Color(120, 140, 170), 0.6f, castsShadows: true);
var lamp = CreatePointLight(new Vector3(0, 1.5f, 0), new Color(255, 200, 140), 12, castsShadows: true);
var shadows = true;
// ...
var lampAt = new Vector3(MathF.Cos(t * 0.6f) * 2.2f, 1.5f, MathF.Sin(t * 0.6f) * 2.2f);
SetLightPosition(lamp, lampAt);
if (IsKeyPressed(Key.Space)) SetLightCastsShadows(lamp, shadows = !shadows);
```

A light's color times its intensity is how bright a white surface facing it looks, so a sun of
intensity 1 lights white as white, and a lamp is brighter near it and dimmer away.

## Shadows

A light made with `castsShadows: true`, or turned on later with `SetLightCastsShadows`, darkens what
other models hide from it. The first directional light that casts shadows casts the sun's, out to
150 units from the camera and sharpest near it. `SetShadowDistance` brings that in for a small
scene, which sharpens it, or out for a wide one. Ten spot lights and twelve point lights that cast
shadows cast theirs too, a point light's all around it, those a camera sees first, the window's or a
render texture's, and then those whose light reaches a camera brightest, weighed by how much of its
picture each lights, so a level of a dozen torches shadows each and a lamp lighting a wall across
the view keeps its shadows over a brighter one lighting a corner. The ones that matter most get the
sharpest shadows: past four, the first two spot lights and the first four point lights keep their
texels, and the rest share theirs at half the width.

A render texture that draws models through a camera of its own, as each half of a split screen,
has the sun's cascades fitted to its camera, and views whose cameras are near enough share one set
fitted to all of them, drawn once, where the shared first cascade's texels are no more than a
quarter wider than each view's own, so each reads its shadows as it would alone. The two views of
`games/Sumo`, facing each other across the ring, share theirs, and its render textures take 0.35 to
0.37 ms of the GPU where they took 0.45, as `./e3d command profile` gives `targets`, a shared view
differing from itself drawn alone at a quarter of a percent of its pixels, along its shadows' edges.

`SetShadowMapSize` sets how many texels wide each tile of the shadow map is, 2048 unless set, which
is the shadow quality a game's settings offer: 4096 sharpens every shadow at four times the memory,
and 1024 softens them for a slower machine.

Shapes drawn with `DrawCube` and the others cast no shadow, and models do. The shadow map example
draws its lamp as a shape for that reason, since a model around the lamp would shadow everything
from it:

<!-- compiled with:
Vector3 lampAt = default;
-->
```csharp
// Drawn as a shape rather than a model, since a model around the lamp would shadow
// everything from it.
DrawSphere(lampAt, 0.12f, new Color(255, 230, 190));
```

## Light from a sky

`SetEnvironmentMap` lights models from every side by a sky image, so a smooth surface reflects the
sky and a rough one takes its colors. The
[Models and animation](models-and-animation.md#a-sky-around-the-world) page loads one. With a map
set models are lit by it, whether or not there are lights.

## Darker corners

Light from all around, the ambient light, a sky's and a probe's, reaches a room's corner as well
as its open wall, and the floor under a crate as well as the floor around it, so nothing seems to
sit on anything. `SetAmbientOcclusion` darkens that light where the surfaces near a point close it
off, and leaves the light of lights alone:

```csharp
SetAmbientOcclusion(1);           // or SetAmbientOcclusion(1, radius: 3) for a street of houses
```

The radius is how far, in world units, the surfaces that close a point off are looked for, about
the size of what stands close together, and the intensity how dark it goes, 1 suiting most
scenes. It is worked out for the window from a depth of its models drawn at half size each frame,
about a tenth of a millisecond on a desktop GPU, so a model that casts no shadow darkens nothing
around it. A render texture that draws models through a camera, as each half of a split screen,
works out its own the same way before it is drawn, and the two halves of `games/Sumo` at 640 by 720
take about 0.1 ms more of the GPU with it on, as `./e3d command profile` gives `targets`.
`games/Manor` turns it on for its rooms.

## The scene as a distance field

What the window's depth does not hold, a pass that reads it knows nothing of. A wall behind the
camera closes off no light, and a particle that flies past the edge of the picture meets nothing.
`SetSceneField` builds a distance field of the scene around the camera, how far each point is from
the nearest surface of the meshes that cast shadows, which the GPU keeps from frame to frame:

```csharp
SetSceneField(4);                 // four cascades, the finest's cells 0.25 units wide
```

Three things read it while it is on. Ambient occlusion, where `SetAmbientOcclusion` turns it on,
also reads the field along each surface's normal and four ways leaning from it, so a wall out of
the picture still darkens the floor beside it. The sun, the first directional light that casts
shadows, casts soft contact shadows traced toward it through the field across 32 cells of the
finest cascade, sharp where a caster meets what it falls on and softer farther off. And a particle
emitter that collides meets the field wherever it holds the particle, behind things and off the
screen, and the window's depth elsewhere.

The field is cascades of 64 cells a side, each twice as coarse and as wide as the one before, so
four cascades of 0.25 reach 16 units across in the first and 128 in the last. A double-sided mesh,
as a model from an OBJ file is, has no inside and is held half a cell thick on either side, and a
closed wall thinner than a cell, which may have no cell's middle inside it, is held the same about
its middle, so a wall or a floor thinner than a cell still stops what is traced through the field.
A mesh that gives off light and is thinner than a cell, as a glowing strip on a wall, lends its light
to the cells near it, a share as large as the part of a cell it covers, so it lights its room
through the light that bounces. An open mesh,
as a ground plane, is inside only below its faces and not past its rim. A mesh drawn in the same place for eight frames is built into the
cascades around it from its triangles, and one that moves is stamped each frame as boxes rather than
its triangles, painted its color, so a red figure bounces red light, though the light it gives off
does not bounce: a skinned model a box for each joint around the vertices it holds, as
its pose puts them, so the shadows a character gives the light that bounces are its limbs' as
boxes, and a model that does not bend a box for each of up to eight parts its triangles are cut
into, so a table or a car is not one block. Three of raylib's dancing robots in
`shaders_cornell_box` are 147 boxes in 540 bricks of four cells, which the GPU stamps and paints in
0.06 ms and the CPU places in 0.45, in the Release build, where it stamped them unpainted in 0.05
ms. A cascade is built again where the camera has gone past it or a mesh came or went, as many a
frame as the third argument says, one by default, the finest first. In
`shaders_scene_field` on a
laptop's RTX 4070, a frame that stamps its moving crate takes 0.014 ms on the GPU and building the
finest cascade 0.25 ms, and the occlusion pass takes 0.073 ms with the field where it took 0.036
without, as `./e3d command profile` shows them, `field.rebuild 400` building a cascade every frame
for the second. Holding the thin walls and the open rims took a cascade's build there from 0.44 ms
to 0.46 in the Release build, with `field.rebuild 4000`. `Config.SceneField` sets the same for an app made from a `Config`, and
`./e3d command field.show 0` draws the first cascade over the window as the field holds the scene.

## Light that bounces

Light that reaches a surface leaves it again, tinted by its color, and lights what it falls on next,
so a red wall tints the floor beside it and a room the sun shines into through a window is lit
inside, away from the patch on its floor. `SetGlobalIllumination` works that light out each frame:

```csharp
SetGlobalIllumination(GlobalIllumination.Medium);
```

It is traced through the scene's distance field, which it turns on at four cascades where
`SetSceneField` has not, as cascades of light probes, each cascade's probes twice as far apart as the
one before's and tracing the light from twice as far (Radiance Cascades), and a probe for every few
pixels of the window that traces the near light through the window's depth first. Nothing is baked,
so every light and every mesh may move. The light a surface sends on is its material's color, its
texture's average, times the sun's light where the field lets it through, the point and spot
lights', each that casts shadows only where the field lets it through too, and what bounced to it
the frame before, so light bounces again each frame, with the
light it gives off, so an emissive mesh lights its room. The light from all around, the environment
map's, the ambient lights' and a reflection probe's, reaches a surface only through what a ray that
meets nothing brings back, so a room is lit by the sky through its windows and dark where no light
gets in.

`shaders_cornell_box` lights a Cornell box, a white room with a red and a green wall, by a lamp and
a glowing panel, and G steps through the qualities. `shaders_bounce_rooms` gathers the rooms the
bounce finds hardest, one to each of the keys 1 to 8, from a room of thin walls with a lamp outside
to a small bright strip in the dark. On a laptop's RTX 4070 at 800 by 450, with the
frame rate unlimited (`./e3d eval "SetTargetFPS(0)"`) so the GPU holds its clocks, they cost this
on the GPU, as `./e3d command profile` names it `global_illumination`, and `./e3d command gi.state`
gives the rest, `High` measured with the example's field at four cascades
(`./e3d eval "SetSceneField(4, 0.15f, 2)"`), where it traces three:

| Quality | Probe cascades | Directions each | Screen probes | Memory | GPU time |
|---|---|---|---|---|---|
| `Low` | 2 | 64, 64 | every 16 pixels | 0.73 MB | 0.27 ms |
| `Medium` | 3 | 64, 64, 256 | every 12 pixels | 2.80 MB | 0.31 ms |
| `High` | 4 | 64, 256, 256, 256 | every 8 pixels | 6.81 MB | 0.44 ms |

A quality traces no more cascades than the field has, and the field adds 5 MB a cascade, and 5 MB
more that a cascade is built in.

Where light bounces, a glossy surface, one with a roughness under 0.5, traces its reflection too.
The ray is stepped through the window's depth first, and a surface it meets there reflects the
light the frame before showed on it, so the floor of `shaders_reflections` reflects its pillars and
its chrome ball, and the ball the floor, each with the other's reflection in it. A ray that leaves
the picture, or passes behind what the window shows, is traced on through the field, and the surface
it meets there reflects its color times the light reaching it, so a mirror shows what stands behind
the camera. A ray that meets nothing leaves the reflection to the probe or the environment map, as
does a surface as it grows rough, the traced reflection fading out from a roughness of 0.25 to 0.5.
In `shaders_reflections` at `Medium`, the scene's pass takes 0.30 ms of the GPU with its floor
polished and 0.23 ms with it rough, as `./e3d command profile` names it `hdr_scene`, with the frame
rate unlimited as above.

At `High`, where the GPU traces rays itself (`VK_KHR_ray_query`), a reflection the field misses,
past its cascades or too thin for its cells, is traced through the GPU's own rays against the
meshes' triangles, and the surface it meets reflects its color lit by the sun, through a second ray
toward it, the lamps, each that casts shadows through a ray toward it too, and the light that
bounced where the probes reach or the sky where they do not. A device that draws on its CPU, as
lavapipe does, leaves this off and traces through the field alone. `./e3d command gi.rays off` turns
it off in a running program and `gi.rays on` back on, and `gi.state` says how many copies of how
many meshes the GPU's rays see and the memory they take. In `shaders_reflections` at `High` they see
8 copies of 6 meshes in 1.23 MB, and building them again each frame adds some 0.02 ms to
`global_illumination`, where the scene's pass, whose rays here seldom leave the field, reads the
same within its noise of 0.03 ms. A lamp that casts shadows lights what a reflection meets only
where the field, or at `High` the GPU's ray, lets it through, as it lights the light that bounces,
which in Wick's first doorway, its lamp and a wick casting shadows, costs the scene's pass some 0.02
ms of the GPU at `Medium` and `High`, the noise between two runs, and in `shaders_reflections`,
whose lights cast none, nothing that can be read. A mesh that moves bounces light as the boxes the
field holds it as and none of the light it gives off, and the light near the camera is blended with
the frame before's so it holds still as the camera moves, the frame before's held within the spread
of this frame's light around each point, so where a light changed the bounce follows it at once. A
block's side lit only by a red wall's bounce comes within a tenth of its new light in the frame a
lamp is brought in, where it took seven frames, and with the camera sliding the bounce adds 1.23
levels a frame to the picture's change where it adds 4.76 without the frame before's light, as
`GlobalIlluminationTests` reads them on an RTX 4070.
In Wick's first doorway the hold costs nothing that can be read, the bounce taking 0.229 ms at `Low`
and 0.359 at `High` with it and without, as `./e3d command profile` gives `global_illumination` with
the frame rate unlimited. A render texture that draws models through a camera, as each half of a
split screen, has screen probes of its own, traced, blended and held as the window's are in the same
frame, so it shows the light that bounced as the window would: two views of `games/Sumo` at 640 by
720 take 1.14 ms of the GPU at `Low` between them, as `./e3d command profile` gives `targets`, where
they took 0.62 reading the world's probes alone, some 0.26 ms a view. A reflection probe's faces
take the light that bounced from the world's probes alone, and where the window draws no model, as a
game that draws its scene into a texture at a low size and shows the texture, the field follows the
first texture's camera and holds its models. `Config.GlobalIllumination` sets the same for an app
made from a `Config`.

`DrawBounceWindow()`, called between `BeginDrawing` and `EndDrawing` as any ImGui window is, shows
what the light that bounces holds while a scene's light is worked on. It gives what `gi.state`
gives, and draws the view chosen from its list over the window: the screen's probes as tiles, each a
dot of its light, their light as their rays brought it and filtered, and how much of each the frame
before's light gave, red for none and green for the most; a cascade's rays' light and its merge,
each layer of its probes a square of their directions; the cascade's probes as small cubes in the
scene, each face the light the probe gathers from that side; and the frame against a reference, red
where it is brighter and blue where it is darker. Its boxes leave a part out, the frame before's
light, the screen's filter, the screen's probes, the merge of the cascades, the light that bounces
again or every cascade but the one chosen, so what each part gives can be seen, and on a GPU that traces rays its buttons path
trace the view as a reference and give each region's error against it. `shaders_bounce_rooms`
shows it on Tab, and `./e3d command gi.show` and `gi.toggle` set the same in a running program, as
[Driving a program with e3d](driving-with-e3d.md) says.

## Rooms that reflect themselves

Indoors, metal would reflect the sky through the walls. A reflection probe is a box whose surfaces
reflect what is around its middle instead, captured from the meshes and particles the window
draws, or a render texture's when the window draws none, over the frames after it is made, a face a frame. The `models_reflection_probe` example puts one in a room of three colored walls:

```csharp
var probe = CreateReflectionProbe(new Vector3(0, 3, 0), new Vector3(10, 6, 10));
```

A reflection is looked up where it leaves the box, so the room's walls hold still as the camera
moves. Put the box's middle in the open, away from the room's objects, since what stands there fills
the capture. A probe captures again by itself when a light reaching its box is switched on or off,
moved, or brightened or dimmed by a quarter or more, so a lamp put out leaves no glow in the room's
reflections and one flickering about its light costs nothing. `UpdateReflectionProbe` captures it
again after the room's meshes change, and `SetReflectionProbeRefresh(probe, 0.5f)` has it captured
again half a second after each capture, so a door opening in the room or a thing moving through it
is seen, each capture drawing the room a face a frame, `IsReflectionProbeReady` says whether its
capture is made, `UnloadReflectionProbe` removes it, and four probes, the nearest the camera,
reflect at once. In the ECS a probe is a `ReflectionProbe` component placed by its entity's
`Transform`, and one in a prefab beside its room's models, as `games/Manor` streams its rooms in,
captures again once those models have spawned, so it holds the room and not the sky it saw in the
frame it appeared.

## Lights in the ECS

A light made by these calls is a `Light` entity of the ECS, so lights a program makes as entities
light the same models. The `ecs_animated_models` example makes its sun as one:

<!-- compiled with:
BehaviorContext ctx = null!;
-->
```csharp
var sun = ctx.Ecs.Spawn();
ctx.Ecs.Add(sun, Light.Directional(new Vector3(1, 0.97f, 0.92f), 2.5f) with { CastsShadows = true });
```

A mesh entity's `Material` component holds the same values as a `ModelMaterial`, named for the
factors glTF gives them, as `RoughnessFactor` and `MetallicFactor`. The
[Behaviors and the ECS](behaviors-and-the-ecs.md) page draws a world of entities.

## See also

- Examples: [`models_skybox`](../3DEngine.Examples/Models/ModelsSkybox.cs),
  [`models_reflection_probe`](../3DEngine.Examples/Models/ModelsReflectionProbe.cs),
  [`shaders_shadowmap`](../3DEngine.Examples/Shaders/ShadersShadowmap.cs),
  [`shaders_scene_field`](../3DEngine.Examples/Shaders/ShadersSceneField.cs),
  [`shaders_cornell_box`](../3DEngine.Examples/Shaders/ShadersCornellBox.cs),
  [`shaders_reflections`](../3DEngine.Examples/Shaders/ShadersReflections.cs),
  [`shaders_bounce_rooms`](../3DEngine.Examples/Shaders/ShadersBounceRooms.cs),
  [`shaders_bloom`](../3DEngine.Examples/Shaders/ShadersBloom.cs),
  [`shaders_auto_exposure`](../3DEngine.Examples/Shaders/ShadersAutoExposure.cs),
  [`ecs_animated_models`](../3DEngine.Examples/Ecs/EcsAnimatedModels.cs),
  [`models_stress`](../3DEngine.Examples/Benchmarks/ModelsStress.cs)
- [`games/Wick`](../games/Wick/Program.cs), a game lit by a lamp the player carries through dark
  rooms, its light reaching round corners and through doorways only as light that bounces
- The cheatsheet's [Lights](../CHEATSHEET.md#lights) and
  [Models and meshes](../CHEATSHEET.md#models-and-meshes)
- Previous: [Models and animation](models-and-animation.md)
- Next: [Shaders and compute](shaders-and-compute.md)
