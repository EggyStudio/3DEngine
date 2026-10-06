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

## Effects over the frame

Besides bloom, eight effects change how the scene is shown, each set by one call and each off until
set. Any of them draws the scene through the frame that holds light past white, as bloom does, and
what is drawn after `EndMode3D`, text, shapes and ImGui, goes over the result untouched.

| Call | What it does |
|---|---|
| `SetExposure(1.5f)` | Scales the scene's light before its curve, brighter above 1 and dimmer below |
| `SetAutoExposure(true)` | Makes the exposure follow the scene as an eye adapts, between a quarter and four times unless bounds are given |
| `SetTonemap(Tonemap.Aces)` | The curve that brings light past white under it: the engine's own, which leaves colors under 0.9 as they are, `Reinhard`, `Aces` or `Clamp` |
| `SetColorGrading(1.1f, 0.8f, new Color(255, 240, 220))` | Contrast, saturation and a tint, 1, 1 and white leaving it as it is |
| `SetVignette(0.4f)` | Darkens toward the corners, from half the way out unless a radius is given |
| `SetFxaa(true)` | Smooths the jagged edges multisampling leaves, inside a surface and of thin lines |
| `SetDepthOfField(8, 2, 0.02f)` | Keeps what is 8 units from the camera sharp and blurs what is nearer or farther, to its widest 2 units either side |
| `SetMotionBlur(0.5f)` | Smears the picture along the way the camera moved since the frame before, as a film camera's shutter does |

The `shaders_bloom` example gives each a key:

```csharp
if (IsKeyPressed(Key.T)) SetTonemap(curve = (Tonemap)(((int)curve + 1) % 4));
if (IsKeyPressed(Key.V)) SetVignette((vignette = !vignette) ? 0.6f : 0);
if (IsKeyPressed(Key.G)) SetColorGrading(1, (graded = !graded) ? 0.3f : 1, graded ? new Color(255, 225, 190) : Color.White);
if (IsKeyPressed(Key.F)) SetFxaa(fxaa = !fxaa);
if (IsKeyPressed(Key.E)) SetExposure((bright = !bright) ? 1.8f : 1);
// ...
if (IsKeyPressed(Key.D)) SetDepthOfField(8.3f, 2, (focus = !focus) ? 0.025f : 0);
if (IsKeyPressed(Key.M)) SetMotionBlur((blur = !blur) ? 0.6f : 0);
```

The depth of field and motion blur work from the depth of what is drawn inside `BeginMode3D`, so
the text and interface drawn after stay sharp. Motion blur knows the camera's movement alone, so a
thing moving past a still camera stays sharp.

An exposure that follows the scene brings a dim room up and a sunlit field down, over a second or
so, as an eye does, and `SetExposure` then multiplies what it chooses. The `shaders_auto_exposure`
example rides a camera out of a tunnel lit by a few lamps into a field in the sun and back:

```csharp
SetAutoExposure(true, min: 0.3f, max: 6, speed: 1.5f);
```

The field is dazzling for a moment on the way out, and the tunnel dark on the way back in, until the
exposure catches up.

## Lights

A world with no lights is lit by one fixed light from above, so a model shows as soon as it is
drawn. The first light a program makes replaces it. There are four kinds:

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

A light made with `castsShadows: true`, or turned on later with `SetLightCastsShadows`, darkens
what other models hide from it. The first directional light that casts shadows casts the sun's,
out to 150 units from the camera and sharpest near it. `SetShadowDistance` brings that in for a
small scene, which sharpens it, or out for a wide one. Ten spot lights and twelve point lights that
cast shadows cast theirs too, a point light's all around it, those the camera sees first and then
those whose reach comes nearest it, so a level of a dozen torches shadows each. The ones that
matter most get the sharpest shadows: past four, the first two spot lights and the first four
point lights keep their texels, and the rest share theirs at half the width.

`SetShadowMapSize` sets how many texels wide each tile of the shadow map is, 2048 unless set, which
is the shadow quality a game's settings offer: 4096 sharpens every shadow at four times the memory,
and 1024 softens them for a slower machine.

Shapes drawn with `DrawCube` and the others cast no shadow, and models do. The shadow map example
draws its lamp as a shape for that reason, since a model around the lamp would shadow everything
from it:

```csharp
// Drawn as a shape rather than a model, since a model around the lamp would shadow
// everything from it.
DrawSphere(lampAt, 0.12f, new Color(255, 230, 190));
```

## Light from a sky

`SetEnvironmentMap` lights models from every side by a sky image, so a smooth surface reflects the
sky and a rough one takes its colors. The
[Models and animation](models-and-animation.md#a-sky-around-the-world) page loads one. With a map set the fixed light goes, whether or not there are lights.

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
around it, and a render texture is drawn without it. `games/Manor` turns it on for its rooms.

## Rooms that reflect themselves

Indoors, metal would reflect the sky through the walls. A reflection probe is a box whose surfaces
reflect what is around its middle instead, captured from the meshes the window draws, or a render
texture's when the window draws none, over the frames after it is made, a face a frame. The `models_reflection_probe` example puts one in a room of three colored walls:

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
  [`shaders_bloom`](../3DEngine.Examples/Shaders/ShadersBloom.cs),
  [`shaders_auto_exposure`](../3DEngine.Examples/Shaders/ShadersAutoExposure.cs),
  [`ecs_animated_models`](../3DEngine.Examples/Ecs/EcsAnimatedModels.cs),
  [`models_stress`](../3DEngine.Examples/Benchmarks/ModelsStress.cs)
- The cheatsheet's [Lights](../CHEATSHEET.md#lights) and
  [Models and meshes](../CHEATSHEET.md#models-and-meshes)
- Previous: [Models and animation](models-and-animation.md)
- Next: [Shaders and compute](shaders-and-compute.md)
