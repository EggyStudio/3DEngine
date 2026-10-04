# Models and animation

A `Model` is one or more meshes with their materials, loaded from a file or made from a mesh the
program generates, and drawn inside `BeginMode3D` like the 3D shapes. A model with a skeleton is
posed by the clips of its file, frame by frame or at a time in seconds.

## Loading and drawing a model

`LoadModel` reads a model file through Assimp, which knows glTF, FBX, OBJ, COLLADA and about forty
more, with the colors and textures its materials name, embedded ones too. `DrawModel` draws it at a
position and a scale, tinted, and `DrawModelEx` turns it about an axis by degrees and scales each
axis apart. From the `models_loading` example:

```csharp
// An OBJ with its material and texture beside it, loaded through Assimp.
var torus = LoadModel("resources/torus.obj");
var bounds = GetModelBoundingBox(torus);
// ...
BeginMode3D(camera);
DrawModel(floor, new Vector3(0, -1, 0), 1, Color.LightGray);
DrawModelEx(torus, Vector3.Zero, Vector3.UnitY, angle, Vector3.One, Color.White);
DrawModel(cube, new Vector3(-3, 0, 0), 1, Color.White);
DrawModel(sphere, new Vector3(3, 0, 0), 1, Color.SkyBlue);
if (showBounds) DrawBoundingBox(bounds, Color.Lime);
DrawGrid(10, 1);
EndMode3D();
// ...
UnloadModel(torus);
```

A model file is read from beside the program, with the files it names found beside it.
`UnloadModel` frees the meshes and the textures the model loaded itself, and a texture the program
gave it is the program's to unload. `GetModelBoundingBox` gives the box around a model, which
`DrawBoundingBox` draws and the collision functions test against.

`DrawModelWires` draws a model's triangle edges, which shows how finely a mesh is made. Models are
lit by one fixed light from above until the program makes lights of its own, which the
[Materials, light and shadows](materials-light-and-shadows.md) page covers.

## Meshes the program makes

The `GenMesh` functions make the common solids, each as a `ModelMesh` that `LoadModelFromMesh`
turns into a model with a white material. The `models_mesh_generation` example makes every one,
sharing one checkered texture:

```csharp
// Every generator, each as a model sharing one checkered texture.
var checker = LoadTextureFromImage(GenImageChecked(64, 64, 8, 8, Color.White, Color.Gray));
(string Name, Model Model)[] models =
[
    ("plane", LoadModelFromMesh(GenMeshPlane(1.6f, 1.6f, 4, 4))),
    ("cube", LoadModelFromMesh(GenMeshCube(1.2f, 1.2f, 1.2f))),
    ("sphere", LoadModelFromMesh(GenMeshSphere(0.7f, 16, 32))),
    ("hemisphere", LoadModelFromMesh(GenMeshHemiSphere(0.8f, 8, 32))),
    ("cylinder", LoadModelFromMesh(GenMeshCylinder(0.6f, 1.4f, 32))),
    ("torus", LoadModelFromMesh(GenMeshTorus(0.55f, 0.2f, 48, 16))),
    ("knot", LoadModelFromMesh(GenMeshKnot(0.8f, 0.12f, 128, 12))),
    ("poly", LoadModelFromMesh(GenMeshPoly(6, 0.8f))),
    ("cone", LoadModelFromMesh(GenMeshCone(0.7f, 1.4f, 32))),
];
foreach (var (_, model) in models) model.Materials[0].Texture = checker;
```

A mesh of the program's own triangles is made by `UploadMesh` from an array of `ModelVertex`
values and the indices of each triangle's three corners, and `UpdateMeshVertices` moves its
vertices later, keeping the triangles, as water or cloth needs. `ExportMesh` writes a mesh as a
Wavefront OBJ file, its shape without its material, which `LoadModel` reads back and any modeling
program opens.

## Terrain and mazes from images

Two generators read an image. `GenMeshHeightmap` raises ground by each pixel's brightness, and
`GenMeshCubicmap` stands a block where each pixel is white, which makes the walls of a maze or a
level drawn as a picture. From the `models_terrain` example, which paints both:

```csharp
// Hills from a few overlapping waves, painted into an image as brightness.
var heights = GenImageColor(64, 64, Color.Black);
for (int z = 0; z < 64; z++)
for (int x = 0; x < 64; x++)
{
    var h = 0.5f + 0.25f * MathF.Sin(x * 0.15f) * MathF.Cos(z * 0.12f) + 0.2f * MathF.Sin((x + z) * 0.08f);
    var b = (byte)Math.Clamp(h * 255, 0, 255);
    ImageDrawPixel(ref heights, x, z, new Color(b, b, b));
}
var terrain = LoadModelFromMesh(GenMeshHeightmap(heights, new Vector3(12, 3, 12)));
terrain.Materials[0].Texture = LoadTextureFromImage(GenImageGradientLinear(64, 64, 0, Color.DarkGreen, Color.Lime));

// A maze: white pixels are walls.
var plan = GenImageColor(9, 9, Color.Black);
ImageDrawRectangleLines(ref plan, new Rectangle(0, 0, 9, 9), 1, Color.White);
ImageDrawLine(ref plan, 2, 2, 6, 2, Color.White);
// ...
var maze = LoadModelFromMesh(GenMeshCubicmap(plan, new Vector3(1, 1.2f, 1)));
```

The size given to `GenMeshHeightmap` is the terrain's width, its greatest height and its depth in
world units. A heightmap is usually a grayscale PNG loaded with `LoadImage`, as one drawn in a paint
program.

## Animation

A model with a skeleton, as a character exported to glTF or FBX, has `Bones` and the pose they
rest in. `LoadModelAnimations` reads the clips of the same file, each sampled at 60 frames a
second, and `UpdateModelAnimation` poses the model at a frame of one. The `models_animation`
example plays a bending arm back and forth, and draws its bones over it:

```csharp
// A skinned arm and its clip from the same glTF. build/make-arm-gltf.py writes it.
var arm = LoadModel("resources/arm.gltf");
var animations = LoadModelAnimations("resources/arm.gltf");
var bend = animations[0];
// ...
// Back and forth, up the clip and then down it again.
if (playing) frame++;
var length = bend.FrameCount - 1;
var shown = Math.Abs(((frame % (2 * length)) + 2 * length) % (2 * length) - length);
UpdateModelAnimation(arm, bend, length - shown);
// ...
DrawModel(arm, Vector3.Zero, 1, new Color(230, 160, 60));
// Each bone as a point, joined to its parent.
var pose = bend.FramePoses[length - shown];
for (int b = 0; b < bend.BoneCount; b++)
{
    DrawSphere(pose[b].Position, 0.06f, Color.Red);
    if (bend.Bones[b].Parent >= 0) DrawLine3D(pose[b].Position, pose[bend.Bones[b].Parent].Position, Color.Red);
}
// ...
UnloadModelAnimations(animations);
```

A clip has a `Name`, as the file gives it, so a program finds "Walk" or "Jump" among them by name.
The posing happens on the GPU, so a crowd of animated models costs the CPU little.

A game usually plays a clip by time rather than by frame, so it runs at the same speed at any frame
rate, and turns one clip into another rather than cutting:

```csharp
time += GetFrameTime();
UpdateModelAnimationAt(hero, walk, time);                              // between frames, looping
UpdateModelAnimationBlend(hero, walk, time, run, time, speed / topSpeed); // a walk turning into a run
```

`UpdateModelAnimationAt` poses a model between the two frames either side of a time, counted round
the clip's length so it loops. `UpdateModelAnimationBlend` poses it between two clips, each at its
own time, a weight of the way from the first to the second. `IsModelAnimationValid` says whether a
clip moves the bones a model has, which a clip from another file may not.

`UpdateModelAnimationLayer` plays one clip on a bone and every bone below it over another, so a
character waves while it runs, the arm moving from wherever the run carries the shoulder. The
`models_morph_and_layers` example plays Summit's hero running with the jump's raised arm on its
left arm alone, the layer's weight easing in and out:

```csharp
// The arm rises and falls over the run as the layer's weight eases in and out.
var wave = layered ? 0.5f + 0.5f * MathF.Sin(time * 2) : 0;
UpdateModelAnimationLayer(hero, run, time, jump, 0, "ArmL", wave);
```

## Morph targets

A mesh can carry morph targets, shapes its vertices are moved toward by a weight from 0 to 1, as
a face smiles or blinks or a ball squashes. A glTF file's targets load with the model, and its
clips that move their weights play as any clip does, as the same example's strip does:

```csharp
var strip = LoadModel("resources/morph.gltf");
var pulse = LoadModelAnimations("resources/morph.gltf")[0];
// ...
UpdateModelAnimationAt(strip, pulse, time);
```

`SetModelMorphWeight(strip, "Raise", 0.5f)` sets a target's weight by its name. The GPU moves the
vertices toward their targets before the skeleton poses them, so a skinned face smiles as its
head turns. `build/make-morph-gltf.py` writes the strip.

In the ECS an `AnimatedModel` component plays a clip on an entity by itself, which the
`ecs_animated_models` example shows with five arms of one file, each at its own speed.

## A sky around the world

`SetEnvironmentMap` takes an equirectangular image, a panorama twice as wide as it is high, and
lights models by it from every side, so a shiny surface reflects the sky and a dull one takes its
colors. `DrawSkybox` draws the same image as the sky behind everything. From `models_skybox`, which
paints its sky so it needs no file:

```csharp
// An equirectangular sky made here, so the example needs no file: the top half a sky
// with a sun, the bottom half the ground. A photo or a .hdr file loads the same way.
var sky = GenImageColor(1024, 512, Color.Blank);
ImageDraw(ref sky, GenImageGradientLinear(1024, 256, 0, new Color(40, 90, 170), new Color(190, 215, 235)),
    new Rectangle(0, 0, 1024, 256), new Rectangle(0, 0, 1024, 256), Color.White);
ImageDraw(ref sky, GenImageGradientLinear(1024, 256, 0, new Color(95, 105, 80), new Color(45, 50, 40)),
    new Rectangle(0, 0, 1024, 256), new Rectangle(0, 256, 1024, 256), Color.White);
ImageDrawCircle(ref sky, 300, 150, 14, new Color(255, 250, 225));
SetEnvironmentMap(sky);
// ...
BeginMode3D(camera);
DrawSkybox();
```

`SetEnvironmentMap("sky.hdr")` loads a Radiance file, which keeps light brighter than white, as the
sun is. `UnloadEnvironmentMap` goes back to the fixed light.

## Many copies of one mesh

Each `DrawModel` is a draw of its own, and the renderer gathers draws of the same mesh and material
into one. Where a program draws thousands of copies itself, `DrawMeshInstanced` takes all their
transforms at once, as the `shaders_mesh_instancing` example does with ten thousand turning cubes:

```csharp
for (int i = 0; i < Count; i++)
    transforms[i] = Matrix4x4.CreateFromAxisAngle(axes[i], float.DegreesToRadians(speeds[i] * time)) * Matrix4x4.CreateTranslation(places[i]);
// ...
BeginMode3D(camera);
DrawMeshInstanced(cube.Meshes[0], material, transforms);
EndMode3D();
```

A shader of the program's own tells the copies apart by `SV_InstanceID`, counted from 0, which the
[Shaders and compute](shaders-and-compute.md) page covers. The `models_stress` example measures how
many lit, turning entities a frame holds at 60 frames a second, beside skinned arms.

## See also

- Examples: [`models_loading`](../3DEngine.Examples/Models/ModelsLoading.cs),
  [`models_mesh_generation`](../3DEngine.Examples/Models/ModelsMeshGeneration.cs),
  [`models_terrain`](../3DEngine.Examples/Models/ModelsTerrain.cs),
  [`models_animation`](../3DEngine.Examples/Models/ModelsAnimation.cs),
  [`models_morph_and_layers`](../3DEngine.Examples/Models/ModelsMorphAndLayers.cs),
  [`models_skybox`](../3DEngine.Examples/Models/ModelsSkybox.cs),
  [`ecs_animated_models`](../3DEngine.Examples/Ecs/EcsAnimatedModels.cs),
  [`shaders_mesh_instancing`](../3DEngine.Examples/Shaders/ShadersMeshInstancing.cs),
  [`models_stress`](../3DEngine.Examples/Benchmarks/ModelsStress.cs)
- The cheatsheet's [Models and meshes](../CHEATSHEET.md#models-and-meshes)
- Previous: [Text and fonts](text-and-fonts.md)
- Next: [Materials, light and shadows](materials-light-and-shadows.md)
