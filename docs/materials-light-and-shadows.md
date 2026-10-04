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

A material's `AlphaMode` says what the alpha of its color and texture does:

| Mode | Draws |
|---|---|
| `MaterialAlphaMode.Blend` | The surface laid over what is behind it, by its alpha, the default |
| `MaterialAlphaMode.Mask` | Solid where alpha is at least `AlphaCutoff` and nothing where it is below, as leaves and fences need |
| `MaterialAlphaMode.Opaque` | Solid everywhere, alpha ignored |

`DoubleSided` is true unless set, so a leaf or a flag shows from behind, and a glTF file sets both.

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
small scene, which sharpens it, or out for a wide one. The first four spot lights and the first four
point lights that cast shadows cast theirs too, a point light's all around it.

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
  [`shaders_shadowmap`](../3DEngine.Examples/Shaders/ShadersShadowmap.cs),
  [`ecs_animated_models`](../3DEngine.Examples/Ecs/EcsAnimatedModels.cs),
  [`models_stress`](../3DEngine.Examples/Benchmarks/ModelsStress.cs)
- The cheatsheet's [Lights](../CHEATSHEET.md#lights) and
  [Models and meshes](../CHEATSHEET.md#models-and-meshes)
- Previous: [Models and animation](models-and-animation.md)
- Next: [Shaders and compute](shaders-and-compute.md)
