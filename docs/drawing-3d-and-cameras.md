# Drawing in 3D and cameras

3D drawing places shapes and models in a world of units, seen through a `Camera3D`, depth tested
so what is nearer hides what is behind it. The camera is a plain value the program keeps and moves,
and the same frame can draw 3D and then 2D over it.

## A camera and a world

`BeginMode3D` draws everything after it through a camera until `EndMode3D`, after which drawing is
in screen pixels again. A `Camera3D` holds where it is, the point it looks at, which way is up and
its vertical field of view in degrees. The `shapes_basic_3d` example:

```csharp
var camera = new Camera3D(new Vector3(0, 10, 10), Vector3.Zero, Vector3.UnitY, 45);

SetTargetFPS(60);

while (!WindowShouldClose())
{
    UpdateCamera(ref camera, CameraMode.Orbital);

    BeginDrawing();
    ClearBackground(Color.RayWhite);

    BeginMode3D(camera);
    DrawCube(new Vector3(-4, 0, 2), 2, 5, 2, Color.Red);
    DrawCubeWires(new Vector3(-4, 0, 2), 2, 5, 2, Color.Gold);
    DrawCubeWires(new Vector3(-4, 0, -2), 3, 6, 2, Color.Maroon);

    DrawSphere(new Vector3(-1, 0, -2), 1, Color.Green);
    DrawSphereWires(new Vector3(1, 0, 2), 2, 16, 16, Color.Lime);

    DrawPlane(new Vector3(4, 0, -2), new Vector2(3, 3), Color.SkyBlue);
    DrawLine3D(new Vector3(4, 0, 2), new Vector3(4, 4, 2), Color.Purple);

    DrawGrid(10, 1);
    EndMode3D();

    DrawFPS(10, 10);
    EndDrawing();
}
```

The world is right-handed with y up, as raylib's is. A cube is given by its center and its size
along x, y and z, a plane lies on the ground, and `DrawGrid` draws the ground's lines around the
origin, which helps judge where things are.

The 3D shapes are cubes, spheres, cylinders and cones, capsules, planes, lines, points, circles,
rays and triangles, each with a wire form, listed in the cheatsheet's [3D shapes](../CHEATSHEET.md#3d-shapes)
section. Models loaded from files and meshes the program generates are drawn the same way, inside
`BeginMode3D`, and lit by the scene's lights.

## Moving the camera

`UpdateCamera` moves a camera from the keyboard and mouse in one of four ways:

| Mode | Moves |
|---|---|
| `CameraMode.Free` | W, A, S, D, Q and E fly, the right mouse button turns, Shift goes faster |
| `CameraMode.Orbital` | Circles its target |
| `CameraMode.FirstPerson` | The mouse looks and W, A, S and D walk along the ground |
| `CameraMode.ThirdPerson` | As first person, around its target, which the program draws as the player |

The first and third person modes are meant with the cursor held to the window, which
`DisableCursor` does and `EnableCursor` undoes. From the `core_3d_camera_first_person` example:

```csharp
var camera = new Camera3D(new Vector3(0, 2, 4), new Vector3(0, 2, 0), Vector3.UnitY, 60);
var mode = CameraMode.FirstPerson;
// ...
DisableCursor();
SetTargetFPS(60);

while (!WindowShouldClose())
{
    if (IsKeyPressed(Key.Alpha1)) mode = CameraMode.Free;
    if (IsKeyPressed(Key.Alpha2)) mode = CameraMode.FirstPerson;
    if (IsKeyPressed(Key.Alpha3)) mode = CameraMode.ThirdPerson;
    if (IsKeyPressed(Key.Alpha4)) mode = CameraMode.Orbital;
    if (IsKeyPressed(Key.Tab))
    {
        if (IsCursorHidden()) EnableCursor();
        else DisableCursor();
    }

    UpdateCamera(ref camera, mode);
    // ...
}
```

A game that moves its camera itself sets `camera.Position` and `camera.Target` each frame and
skips `UpdateCamera`. The `core_3d_camera_free` example looks back at the origin when Z is pressed
by setting the target:

```csharp
if (IsKeyPressed(Key.Z)) camera.Target = Vector3.Zero;
```

## Perspective and orthographic

A camera's `Projection` is `CameraProjection.Perspective`, where farther things are smaller and
`FovY` is an angle, or `CameraProjection.Orthographic`, where they keep their size and `FovY` is
how many units high the view is, as a strategy game or an editor's side view has it:

```csharp
var top = new Camera3D(new Vector3(0, 20, 0.01f), Vector3.Zero, Vector3.UnitY, 30, CameraProjection.Orthographic);
```

## From the world to the screen and back

A label over a thing in the world is drawn in 2D after `EndMode3D`, at the point the world position
falls on the screen:

```csharp
var above = GetWorldToScreen(enemy + new Vector3(0, 2, 0), camera);
if (IsPointInFrontOfCamera(enemy, camera)) DrawText("Enemy", (int)above.X - 20, (int)above.Y, 20, Color.Maroon);
```

Going the other way, `GetScreenToWorldRay(GetMousePosition(), camera)` gives the ray under the
mouse, which `GetRayCollisionBox`, `GetRayCollisionSphere` and `GetRayCollisionMesh` test against
the world, as picking a thing with the mouse needs.

## Drawing into a texture

`BeginTextureMode` sends what is drawn after it into a render texture instead of the window, until
`EndTextureMode`, and the texture is then drawn like any other. A security camera's screen, a
minimap, a split screen and a picture-in-picture are made this way. The
`textures_render_target` example draws one 3D scene into a texture and shows it three times:

```csharp
var target = LoadRenderTexture(320, 240);
// ...
BeginTextureMode(target);
ClearBackground(Color.DarkBlue);
BeginMode3D(camera);
DrawModelEx(cube, Vector3.Zero, Vector3.UnitY, angle, Vector3.One, Color.Orange);
DrawGrid(10, 1);
EndMode3D();
DrawRectangleLines(0, 0, 320, 240, Color.Gold);
EndTextureMode();

DrawTexture(target.Texture, 20, 60, Color.White);
DrawTextureEx(target.Texture, new Vector2(360, 60), 0, 0.5f, Color.White);
```

Inside texture mode `ClearBackground` clears the texture, and 2D drawing is in its pixels. A
camera entity in the ECS draws the scene's mesh entities into a texture the same way when its
`Target` is set.

## Particles

Smoke, sparks, dust and fire are particles: small squares facing the camera, given off by an
emitter, moving and falling, and changing size and color over their lives. `CreateParticleEmitter`
places one, from a `ParticleEmitter` that says how, and its particles are simulated on the GPU and
drawn after the frame's models with no drawing call. The `shaders_particles` example's campfire is
three emitters, and its flames glow through bloom, since an unlit particle gives off its color times
its intensity:

```csharp
var fire = CreateParticleEmitter(new Vector3(0, 0.15f, 0), ParticleEmitter.Default with
{
    MaxParticles = 600,
    Rate = 220,
    Life = 0.9f,
    LifeVariation = 0.4f,
    Velocity = new Vector3(0, 1.6f, 0),
    Spread = 18,
    Radius = 0.35f,
    Gravity = new Vector3(0, 0.8f, 0),
    StartSize = 0.45f,
    EndSize = 0.1f,
    StartColor = new Color(255, 190, 80),
    EndColor = new Color(200, 40, 10, 0),
    Intensity = 3,
});
```

Its smoke is `Lit`, so the fire's lamp and the moon light it as a rough surface facing the camera,
and laid over by alpha (`ParticleBlend.Alpha`) where the flames add their light. Each puff is a
`Texture`, an image made in the example of white fading out from the middle and broken up by
noise, tinted by the puff's color in place of the round dot a particle is otherwise, and `Drag`
slows the puffs as they rise, 1 leaving about a third of a speed after a second, so they leave the
fire fast and then hang and spread:

```csharp
Velocity = new Vector3(0.6f, 2.2f, 0),
Gravity = new Vector3(0.15f, 0.4f, 0),
Drag = 0.8f,
Texture = smoke,
```

A texture can also be a sheet of frames, `TextureColumns` across and `TextureRows` down, which
each particle plays through over its life, left to right and then down, as a flame or an explosion
drawn frame by frame is. A texture is the program's, loaded with `LoadTexture`, so a scene file
does not hold it. Its sparks
do not stream and are thrown out a hundred and twenty at a time:

```csharp
if (IsKeyPressed(Key.Space)) EmitParticles(sparks, 120);
```

`SetParticleEmitterPosition` moves where its next particles start, as dust behind a running
wheel, `SetParticleEmitterActive` starts and stops its stream, `GetParticleEmitter` and
`SetParticleEmitter` read and change its settings with `with`, and `UnloadParticleEmitter`
removes it and its particles:

```csharp
SetParticleEmitterPosition(dust, wheel);
SetParticleEmitter(dust, GetParticleEmitter(dust) with { Rate = speed * 4 });
```

An emitter keeps room for `MaxParticles`, and once that many are alive the oldest are replaced.
Emitters laid over by alpha are drawn from the farthest from the camera to the nearest, by where
each emitter is, so where two overlap the nearer is in front, and the particles within one are
sorted far to near on the GPU each frame, so a puff of smoke in front covers one behind. A scene drawn into a render texture has its particles too, drawn through the camera of
its first `BeginMode3D`, so a texture drawn only in 2D shows none. In the ECS an
emitter is a `ParticleEmitter` component placed by its entity's `Transform`, which a scene file
saves.

## See also

- Examples: [`shapes_basic_3d`](../3DEngine.Examples/Shapes/ShapesBasic3D.cs),
  [`core_3d_camera_free`](../3DEngine.Examples/Core/Core3DCameraFree.cs),
  [`core_3d_camera_first_person`](../3DEngine.Examples/Core/Core3DCameraFirstPerson.cs),
  [`textures_render_target`](../3DEngine.Examples/Textures/TexturesRenderTarget.cs),
  [`shaders_particles`](../3DEngine.Examples/Shaders/ShadersParticles.cs)
- The cheatsheet's [Frame and cameras](../CHEATSHEET.md#frame-and-cameras),
  [3D shapes](../CHEATSHEET.md#3d-shapes) and [Particles](../CHEATSHEET.md#particles)
- Previous: [Drawing in 2D](drawing-2d.md)
- Next: [Textures and images](textures-and-images.md)
