# Models and animation

A `Model` is one or more meshes with their materials, loaded from a file or made from a mesh the
program generates, and drawn inside `BeginMode3D` like the 3D shapes. A model with a skeleton is
posed by the clips of its file, frame by frame or at a time in seconds.

## Loading and drawing a model

`LoadModel` reads a model file through Assimp, which knows glTF, FBX, OBJ, COLLADA and about forty
more, with the colors and textures its materials name, embedded ones too. MagicaVoxel's `.vox`, the
Inter-Quake Model's `.iqm` and the Model 3D's `.m3d` it reads itself, as raylib does, an IQM with
its skeleton, which Assimp's reader leaves out. `DrawModel` draws it at a
position and a scale, tinted, and `DrawModelEx` turns it about an axis by degrees and scales each
axis apart. From the `models_loading` example:

<!-- compiled with:
Camera3D camera = default;
Model floor = default, cube = default, sphere = default;
float angle = 0;
bool showBounds = false;
-->
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
drawn unlit, their color and texture as they are, as raylib draws them, until the program makes
lights of its own, which the [Materials, light and shadows](materials-light-and-shadows.md) page
covers.

## Meshes the program makes

The `GenMesh` functions make the common solids, each as a `ModelMesh` that `LoadModelFromMesh`
turns into a model with a white material. The `models_mesh_generation` example, raylib's, makes
every one and a triangle of its own, shown one at a time:

<!-- compiled with:
ModelMesh GenMeshCustom() => default!;
-->
```csharp
Model[] models =
[
    LoadModelFromMesh(GenMeshPlane(2, 2, 4, 3)),
    LoadModelFromMesh(GenMeshCube(2.0f, 1.0f, 2.0f)),
    LoadModelFromMesh(GenMeshSphere(2, 32, 32)),
    LoadModelFromMesh(GenMeshHemiSphere(2, 16, 16)),
    LoadModelFromMesh(GenMeshCylinder(1, 2, 16)),
    LoadModelFromMesh(GenMeshTorus(0.25f, 4.0f, 16, 32)),
    LoadModelFromMesh(GenMeshKnot(1.0f, 2.0f, 16, 128)),
    LoadModelFromMesh(GenMeshPoly(5, 2.0f)),
    LoadModelFromMesh(GenMeshCustom()),
];
```

A mesh of the program's own triangles is made by `UploadMesh` from an array of `ModelVertex`
values and the indices of each triangle's three corners, as the example's `GenMeshCustom` makes
its triangle:

```csharp
static ModelMesh GenMeshCustom()
{
    ModelVertex[] vertices =
    [
        new(new Vector3(0, 0, 0), Vector3.UnitY, new Vector2(0, 0)),
        new(new Vector3(1, 0, 2), Vector3.UnitY, new Vector2(0.5f, 1.0f)),
        new(new Vector3(2, 0, 0), Vector3.UnitY, new Vector2(1, 0)),
    ];
    return UploadMesh(vertices, [0, 1, 2]);
}
```

A mesh may have a color at each vertex and a second texture coordinate at each, as raylib's `colors`
and `texcoords2`, given to `UploadMesh` after its indices, either of them null. The colors multiply
the material's color and texture, and a file's are read with it, as `models_loading_vox` colors each
voxel, and the second coordinates are read by a shader of the program's own, as a lightmap's are.
The two are buffers of their own beside the vertices. A mesh with either carries both, the one it
was not given white or zero, which adds twelve bytes to each of its vertices' thirty-two, and a mesh
with neither is drawn from its vertices alone. A stream of the program's own, a `MeshStream` given
to `UploadMesh` after them, is a buffer of its own too, of four to sixteen bytes a vertex by its
format, which a vertex stage of the program's own reads as `CUSTOM0` ([Shaders and
compute](shaders-and-compute.md#a-vertex-stream-of-the-programs-own)). A mesh whose stream packs its
normal and texture coordinate is uploaded from its positions alone, twelve bytes a vertex, and drawn
with such a shader.

`UpdateMeshVertices` moves a mesh's vertices later, keeping the triangles, as water or cloth
needs, and `UpdateMeshBuffer` writes into one of its arrays by raylib's index for it, positions,
texture coordinates, normals, colors or second texture coordinates, as `shaders_lightmap_rendering`
gives a plane its second coordinates after `GenMeshPlane` has made it, and at `MeshStreamBuffer`
the stream of the program's own, in place, uploading nothing else of the mesh again. `ExportMesh` writes a mesh as a Wavefront OBJ file, its shape without its material, which
`LoadModel` reads back and any modeling program opens.

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

// A maze whose white pixels are walls.
var plan = GenImageColor(9, 9, Color.Black);
ImageDrawRectangleLines(ref plan, 0, 0, 9, 9, Color.White);
ImageDrawLine(ref plan, 2, 2, 6, 2, Color.White);
// ...
var maze = LoadModelFromMesh(GenMeshCubicmap(plan, new Vector3(1, 1.2f, 1)));
```

The size given to `GenMeshHeightmap` is the terrain's width, its greatest height and its depth in
world units. A heightmap is usually a grayscale PNG loaded with `LoadImage`, as one drawn in a paint
program.

A cubicmap's block stands centered on its pixel's place, as raylib's does, and each face takes a
quarter of the texture, laid out as raylib's `cubicmap_atlas.png` is, the walls' sides from the top
half and the floor from the bottom right. An open cell has a roof as well, which faces down, so it
is the ceiling from inside the maze and hides nothing from above.

## Animation

A model with a skeleton, as a character exported to glTF or FBX, has `Bones` and the pose they
rest in. `LoadModelAnimations` reads the clips of the same file, each sampled at 60 frames a
second, and `UpdateModelAnimation` poses the model at a frame of one. The `models_animation`
example plays a bending arm back and forth, and draws its bones over it:

<!-- compiled with:
int frame = 0;
bool playing = true;
-->
```csharp
// A skinned arm and its clip from the same glTF. build/make-arm-gltf.py writes it.
var arm = LoadModel("resources/arm.gltf");
var animations = LoadModelAnimations("resources/arm.gltf");
var bend = animations[0];
// ...
// Back and forth, up the clip and then down it again.
if (playing) frame++;
var length = bend.KeyframeCount - 1;
var shown = Math.Abs(((frame % (2 * length)) + 2 * length) % (2 * length) - length);
UpdateModelAnimation(arm, bend, length - shown);
// ...
DrawModel(arm, Vector3.Zero, 1, new Color(230, 160, 60));
// Each bone as a point, joined to its parent.
var pose = bend.KeyframePoses[length - shown];
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

<!-- compiled with:
Model hero = default;
ModelAnimation walk = default!, run = default!;
float time = 0, speed = 0, topSpeed = 1;
-->
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

<!-- compiled with:
Model hero = default;
ModelAnimation run = default!, jump = default!;
float time = 0;
bool layered = true;
-->
```csharp
// The arm rises and falls over the run as the layer's weight eases in and out.
var wave = layered ? 0.5f + 0.5f * MathF.Sin(time * 2) : 0;
UpdateModelAnimationLayer(hero, run, time, jump, 0, "ArmL", wave);
```

The morph targets the layered clip moves follow it too, by the same weight, so a face's
expression played on the head bone changes the face while the body keeps the first clip's.

In the ECS an `AnimatedModel` component plays a clip on an entity by itself, which the
`ecs_animated_models` example shows with five arms of one file, each at its own speed. Its model's
meshes are the entity's in the world as the clip last posed them, so `Collider.ConvexHull` or
`Collider.Mesh` beside a `RigidBody` shapes a body from them once the file has loaded. The body is
made once and keeps that shape as the clip plays on, as a body made from a posed model through the
flat API does. Where the shape must follow the pose, as for a door that swings or an arm a ray must
hit where it is now, removing the entity's `PhysicsBody` has the body made again from the pose of
that frame.

## A state machine of clips

A character idles, walks once its speed passes a value and jumps when a key is pressed, landing back
in its idle once the jump's clip ends. An `AnimationStateMachine` says that as data, states each
naming a clip, whether it loops and its speed, and transitions each from a state, or from any, to
another when a condition holds, with the time the clip left fades into the one entered over. An
`AnimationMachine` beside an entity's `AnimatedModel` runs it, on the parameters the game sets:

<!-- compiled with:
EcsWorld ecs = null!;
int robot = 0;
float speed = 0;
-->
```csharp
var machine = new AnimationStateMachine
{
    States =
    [
        new() { Name = "Idle", Clip = "Idle" },
        new() { Name = "Walk", Clip = "Walking" },
        new() { Name = "Jump", Clip = "Jump", Loop = false },
    ],
    Transitions =
    [
        new() { From = "Idle", To = "Walk", When = AnimationCondition.Above, Parameter = "speed", Value = 0.1f },
        new() { From = "Walk", To = "Idle", When = AnimationCondition.Below, Parameter = "speed", Value = 0.1f },
        new() { To = "Jump", When = AnimationCondition.Triggered, Parameter = "jump", Fade = 0.1f },
        new() { From = "Jump", To = "Idle", When = AnimationCondition.Finished },
    ],
};
machine.Save("robot.machine.json");

ecs.Add(robot, new AnimatedModel("resources/robot.glb"));
ecs.Add(robot, new AnimationMachine("robot.machine.json"));
// ...
ref var animation = ref ecs.GetRef<AnimationMachine>(robot);
animation.Set("speed", speed);
if (IsKeyPressed(Key.Space)) animation.Trigger("jump");
```

A condition is a parameter `Above` or `Below` a value, `True` or `False` for a flag, `Triggered`
for a trigger, which the transition taking it uses up, or `Finished`, the state's clip played to
its end, or come round once where it loops. Each frame the transitions from the state the machine
is in are tried in the order they are listed, the first that holds taken, so a list puts the more
particular first. Entering a state plays its clip from its start through the `AnimatedModel`, its
blend time set to the transition's fade, so the model blends from the clip before as it does when a
program changes its clip, and a clip that plays once holds its last frame. A step with no transition
due reads the parameters its transitions name and nothing else, some 80 nanoseconds for three.

The machine's file is JSON as a scene file is, `Load` reads it and `Save` writes it, and every
entity naming one path shares one reading of it. `Save` gives the file an id, and
`AnimationMachine.File`, a `DataRef<AnimationStateMachine>` from `DataAssets.Load` or
`DataAssets.Find`, names the file by that id in place of its path, so a level keeps its machines
through a rename (see [Data in files of its own](scenes.md#data-in-files-of-its-own)). A scene file
holds an entity's `AnimationMachine` by its path or its file's id and the state it is in, so a level
saved mid-run starts the machine there. The
parameters are set through the entity's component by reference, `ref` in a behavior method or
`GetRef`, and read at the next frame. A machine whose names do not hold together, a transition to a
state it lacks, is said once in the log and runs nothing.

## Morph targets

A mesh can carry morph targets, shapes its vertices are moved toward by a weight from 0 to 1, as
a face smiles or blinks or a ball squashes. A glTF file's targets load with the model, and its
clips that move their weights play as any clip does, as the same example's strip does:

<!-- compiled with:
float time = 0;
-->
```csharp
var strip = LoadModel("resources/morph.gltf");
var pulse = LoadModelAnimations("resources/morph.gltf")[0];
// ...
UpdateModelAnimationAt(strip, pulse, time);
```

`SetModelMorphWeight(strip, "Raise", 0.5f)` sets a target's weight by its name. The GPU moves the
vertices toward their targets before the skeleton poses them, so a skinned face smiles as its
head turns. `build/make-morph-gltf.py` writes the strip.

## A sky around the world

`SetEnvironmentMap` takes an equirectangular image, a panorama twice as wide as it is high, and
lights models by it from every side, so a shiny surface reflects the sky and a dull one takes its
colors. `DrawSkybox` draws the same image as the sky behind everything. From `models_skybox`, which
paints its sky so it needs no file:

<!-- compiled with:
Camera3D camera = default;
-->
```csharp
// An equirectangular sky made here, so the example needs no file, the top half a sky
// with a sun and the bottom half the ground. A photo or a .hdr file loads the same way.
var sky = GenImageColor(1024, 512, Color.Blank);
ImageDrawImage(ref sky, GenImageGradientLinear(1024, 256, 0, new Color(40, 90, 170), new Color(190, 215, 235)),
    0, 0, Color.White);
ImageDrawImage(ref sky, GenImageGradientLinear(1024, 256, 0, new Color(95, 105, 80), new Color(45, 50, 40)),
    0, 256, Color.White);
ImageDrawCircle(ref sky, 300, 150, 14, new Color(255, 250, 225));
SetEnvironmentMap(sky);
// ...
BeginMode3D(camera);
DrawSkybox();
```

`SetEnvironmentMap("sky.hdr")` loads a Radiance file, which keeps light brighter than white, as the
sun is. `UnloadEnvironmentMap` goes back to the light entities alone, or to drawing unlit with none.

A sky image's lower half is its ground, darker than its sky, and a program that fades its land
into distance fog shows that dark half past the last thing it draws. `SetSkyHorizon` draws the sky
below the horizon in a color of its own, the fog's, blended into the image over the degrees given
above it, so the fog meets a sky of its own color. The image's lower half still lights the scene
from below, and a clear color draws the image as it is again:

```csharp
SetSkyHorizon(new Color(200, 210, 225), blendDegrees: 6);
```

## A day sky

`SetSkyTime` starts a day sky at an hour counted from its first midnight. It paints the environment
map from the hour, a blue gradient by day, an orange horizon at dawn and dusk and a dark violet by
night, and turns a directional light that casts shadows, the sun's by day, a warm white at noon and
an orange low, and the moon's by night. `DrawSkybox` draws it, and a program draws its own things
over it as before:

<!-- compiled with:
Camera3D camera = default;
-->
```csharp
SetSkyTime(9);
SetSkyDay(new SkyDay { Minutes = 10 });
// ...
BeginMode3D(camera);
DrawSkybox();
```

The hour moves on by itself, a whole day in `SkyDay.Minutes` of the program's time, twenty unless
set, and a day of 0 minutes stands at the hour `SetSkyTime` sets, 30 being six in the morning of the
second day. The sun rises in the east, along +X, at 6, passes `SkyDay.Tilt` degrees south of the
zenith, toward +Z, at noon, and sets in the west at 18. A program with a sun of its own names it in
`SkyDay.Light`, and the sky turns and colors that light in place of making one of its own, so the
world holds one sun.

The sky sets the horizon `SetSkyHorizon` sets to the hour's color, which `GetSkyHorizon` reads for
a program's own fog, and paints the map again once the hour has moved three minutes of its day, at
most four times a second. `SetEnvironmentMap` or `UnloadEnvironmentMap` ends it, with the light it
made and the horizon it set.

## Many copies of one mesh

Each `DrawModel` is a draw of its own, and the renderer gathers draws of the same mesh and material
into one. Where a program draws thousands of copies itself, `DrawMeshInstanced` takes all their
transforms at once, as the `shaders_instance_hues` example does with ten thousand turning cubes:

<!-- compiled with:
const int Count = 1000;
Matrix4x4[] transforms = new Matrix4x4[Count];
Vector3[] axes = new Vector3[Count], places = new Vector3[Count];
float[] speeds = new float[Count];
float time = 0;
Camera3D camera = default;
Model cube = default;
ModelMaterial material = LoadMaterialDefault();
-->
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
  [`models_loading_vox`](../3DEngine.Examples/Models/ModelsLoadingVox.cs),
  [`models_loading_iqm`](../3DEngine.Examples/Models/ModelsLoadingIqm.cs),
  [`models_loading_m3d`](../3DEngine.Examples/Models/ModelsLoadingM3d.cs),
  [`models_terrain`](../3DEngine.Examples/Models/ModelsTerrain.cs),
  [`models_animation`](../3DEngine.Examples/Models/ModelsAnimation.cs),
  [`models_morph_and_layers`](../3DEngine.Examples/Models/ModelsMorphAndLayers.cs),
  [`models_skybox`](../3DEngine.Examples/Models/ModelsSkybox.cs),
  [`ecs_animated_models`](../3DEngine.Examples/Ecs/EcsAnimatedModels.cs),
  [`shaders_instance_hues`](../3DEngine.Examples/Shaders/ShadersInstanceHues.cs),
  [`models_stress`](../3DEngine.Examples/Benchmarks/ModelsStress.cs)
- The games [`games/Jelly`](../games/Jelly/Program.cs), a blob that squashes and stretches by its
  model's morph targets, the model written by a script beside it, and
  [`games/Tumble`](../games/Tumble/Robot.cs), a robot whose walk and run are blended by its speed,
  its upper body aimed on a layer of its own, which falls as a ragdoll posing the model from its
  bodies and stands up by a blend from the pose it lies in
- The cheatsheet's [Models and meshes](../CHEATSHEET.md#models-and-meshes)
- Previous: [Text and fonts](text-and-fonts.md)
- Next: [Modeling](modeling.md)
