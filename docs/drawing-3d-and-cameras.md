# Drawing in 3D and cameras

3D drawing places shapes and models in a world of units, seen through a `Camera3D`, depth tested
so what is nearer hides what is behind it. The camera is a plain value the program keeps and moves,
and the same frame can draw 3D and then 2D over it.

## A camera and a world

`BeginMode3D` draws everything after it through a camera until `EndMode3D`, after which drawing is
in screen pixels again. A `Camera3D` holds where it is, the point it looks at, which way is up and
its vertical field of view in degrees. From the `models_geometric_shapes` example, raylib's:

```csharp
Camera3D camera = new(new Vector3(0.0f, 10.0f, 10.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

SetTargetFPS(60);

while (!WindowShouldClose())
{
    BeginDrawing();

        ClearBackground(Color.RayWhite);

        BeginMode3D(camera);

            DrawCube(new Vector3(-4.0f, 0.0f, 2.0f), 2.0f, 5.0f, 2.0f, Color.Red);
            DrawCubeWires(new Vector3(-4.0f, 0.0f, 2.0f), 2.0f, 5.0f, 2.0f, Color.Gold);
            DrawCubeWires(new Vector3(-4.0f, 0.0f, -2.0f), 3.0f, 6.0f, 2.0f, Color.Maroon);

            DrawSphere(new Vector3(-1.0f, 0.0f, -2.0f), 1.0f, Color.Green);
            DrawSphereWires(new Vector3(1.0f, 0.0f, 2.0f), 2.0f, 16, 16, Color.Lime);

            DrawCylinder(new Vector3(4.0f, 0.0f, -2.0f), 1.0f, 2.0f, 3.0f, 4, Color.SkyBlue);
            DrawCylinderWires(new Vector3(4.0f, 0.0f, -2.0f), 1.0f, 2.0f, 3.0f, 4, Color.DarkBlue);
            DrawCylinderWires(new Vector3(4.5f, -1.0f, 2.0f), 1.0f, 1.0f, 2.0f, 6, Color.Brown);

            DrawCylinder(new Vector3(1.0f, 0.0f, -4.0f), 0.0f, 1.5f, 3.0f, 8, Color.Gold);
            DrawCylinderWires(new Vector3(1.0f, 0.0f, -4.0f), 0.0f, 1.5f, 3.0f, 8, Color.Pink);

            DrawCapsule(new Vector3(-3.0f, 1.5f, -4.0f), new Vector3(-4.0f, -1.0f, -4.0f), 1.2f, 8, 8, Color.Violet);
            DrawCapsuleWires(new Vector3(-3.0f, 1.5f, -4.0f), new Vector3(-4.0f, -1.0f, -4.0f), 1.2f, 8, 8, Color.Purple);

            DrawGrid(10, 1.0f);

        EndMode3D();

        DrawFPS(10, 10);

    EndDrawing();
}
```

The world is right-handed with y up, as raylib's is. A cube is given by its center and its size
along x, y and z, a cylinder by the center of its base, its radius at the top and at the bottom,
its height and its sides, so a radius of zero makes a cone and four sides a square frustum, and
`DrawGrid` draws the ground's lines around the origin, which helps judge where things are.

The 3D shapes are cubes, spheres, cylinders and cones, capsules, planes, lines, points, circles,
rays and triangles, each with a wire form, listed in the cheatsheet's [3D shapes](../CHEATSHEET.md#3d-shapes)
section. Models loaded from files and meshes the program generates are drawn the same way, inside
`BeginMode3D`, and lit by the scene's lights.

## Moving the camera

`UpdateCamera` moves a camera from the keyboard, the mouse and the first gamepad as raylib's does,
in one of its modes:

| Mode | Moves |
|---|---|
| `CameraMode.Free` | The mouse and the arrow keys turn, W, A, S and D fly the way it looks, Space and left Ctrl rise and sink, the middle button pans |
| `CameraMode.Orbital` | Circles its target half a radian a second |
| `CameraMode.FirstPerson` | The mouse and the arrow keys look, and W, A, S and D walk along the ground |
| `CameraMode.ThirdPerson` | As first person, about its target, which the program draws as the player |
| `CameraMode.Custom` | Nothing, the program moves it |

Q and E roll every mode that turns, and the wheel and the keypad's plus and minus move the free,
third-person and orbital cameras nearer their target and farther. The first and third person modes
are meant with the cursor held to the window, which `DisableCursor` does and `EnableCursor` undoes.
From the `core_3d_camera_first_person` example, raylib's:

```csharp
var camera = new Camera3D(new Vector3(0, 2, 4), new Vector3(0, 2, 0), Vector3.UnitY, 60);
var cameraMode = CameraMode.FirstPerson;
// ...
DisableCursor();
SetTargetFPS(60);

while (!WindowShouldClose())
{
    if (IsKeyPressed(Key.One)) cameraMode = CameraMode.Free;
    if (IsKeyPressed(Key.Two)) cameraMode = CameraMode.FirstPerson;
    if (IsKeyPressed(Key.Three)) cameraMode = CameraMode.ThirdPerson;
    if (IsKeyPressed(Key.Four)) cameraMode = CameraMode.Orbital;

    UpdateCamera(ref camera, cameraMode);
    // ...
}
```

The movements `UpdateCamera` is made of are functions of their own, as raylib's rcamera has them,
for a camera a program moves by amounts it works out: `CameraMoveForward`, `CameraMoveRight` and
`CameraMoveUp` move it with its target, `CameraMoveToTarget` nearer or farther, and `CameraYaw`,
`CameraPitch` and `CameraRoll` turn it by radians, about itself or about its target.
`UpdateCameraPro` does several at once from amounts in degrees.

A game that moves its camera itself sets `camera.Position` and `camera.Target` each frame and
skips `UpdateCamera`. The `core_3d_camera_free` example looks back at the origin when Z is pressed
by setting the target:

<!-- compiled with:
Camera3D camera = default;
-->
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

<!-- compiled with:
Camera3D camera = default;
Vector3 enemy = default;
-->
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

<!-- compiled with:
Camera3D camera = default;
Model cube = default;
float angle = 0;
-->
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
texture that nothing clears in a frame keeps what it held, as raylib's does, so a trail or a
painting drawn into it a stroke a frame builds up, as `shapes_double_pendulum`'s trail does, and a
new one starts transparent black. A camera entity in the ECS draws the scene's mesh entities into a
texture the same way when its `Target` is set.

A render texture is drawn at the window's samples and resolved, so its edges are smoothed as the
window's are, where raylib's has one sample and hard edges. `LoadRenderTextureEx(width, height,
format, samples: 1)` makes one with hard edges, as pixel art drawn small and scaled up needs, or an
image of ids a shader reads, which resolving would mix where two meet.

A render texture loaded with up to four `PixelFormat`s draws into an image of each at once, as a
deferred renderer's G-buffer is drawn into, a shader writing each from its output of the same
index, `SV_Target0` into the first, and `Textures` holds them in the order of the formats. One of
16 or 32 bits a channel keeps half floats or floats, so a position or a normal outside 0 to 1 is
kept. The `shaders_deferred_rendering` example draws the scene's positions, normals and colors into
three and lights them in a pass over the screen, with `rlDisableColorBlend` so a color's alpha holds
its specular strength:

<!-- compiled with:
Shader deferredShader = default;
-->
```csharp
var gBuffer = LoadRenderTexture(800, 450,
    PixelFormat.UncompressedR16G16B16, PixelFormat.UncompressedR16G16B16, PixelFormat.UncompressedR8G8B8A8);
SetShaderValueTexture(deferredShader, GetShaderLocation(deferredShader, "gPosition"), gBuffer.Textures[0]);
```

A headset's two pictures are drawn as raylib's VR simulator draws them. `LoadVrStereoConfig` works
each eye's projection and offset out from a `VrDeviceInfo` of the headset's measures, and between
`BeginVrStereoMode` and `EndVrStereoMode` the shapes, lines and text of a `BeginMode3D` are drawn
once for each eye, the left in the left half of the target and the right in the right. The
`core_vr_simulator` example draws into a texture of the headset's size and bends it through the
lenses with raylib's distortion shader, written in Slang, whose parameters the config holds:

<!-- compiled with:
RenderTexture2D target = default;
VrStereoConfig config = default;
Camera3D camera = default;
Vector3 cubePosition = default;
-->
```csharp
BeginTextureMode(target);
    ClearBackground(Color.RayWhite);
    BeginVrStereoMode(config);
        BeginMode3D(camera);
            DrawCube(cubePosition, 2.0f, 2.0f, 2.0f, Color.Red);
            DrawGrid(40, 1.0f);
        EndMode3D();
    EndVrStereoMode();
EndTextureMode();
```

Models drawn inside it are drawn for each eye as well, and the ECS's mesh entities and particles
once, through the camera.

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

<!-- compiled with:
Texture2D smoke = default;
-->
```csharp
CreateParticleEmitter(new Vector3(0, 1.2f, 0), ParticleEmitter.Default with
{
    Velocity = new Vector3(0.6f, 2.2f, 0),
    Gravity = new Vector3(0.15f, 0.4f, 0),
    Drag = 0.8f,
    Lit = true,
    Blend = ParticleBlend.Alpha,
    Texture = smoke,
    // ...
});
```

A texture can also be a sheet of frames, `TextureColumns` across and `TextureRows` down, which
each particle plays through over its life, left to right and then down, as a flame or an explosion
drawn frame by frame is. With `BlendFrames` set each frame blends into the next over its share of
the life, so smoke rolling through a few frames turns smoothly. A texture is the program's, loaded
with `LoadTexture`, so a scene file does not hold it.

`Collision` has the particles bounce off the scene, keeping `Bounce` of their speed into a
surface, or end their lives where they meet it. They meet the depth of the window's meshes that
cast shadows, drawn at half its size through its camera before they are stepped, so what the window
does not show, off screen or behind something, and shapes drawn without a model, they pass through.
The example's sparks bounce off the ground and the stones, and do not stream but are thrown out a
hundred and twenty at a time:

<!-- compiled with:
ParticleEmitterHandle sparks = default;
-->
```csharp
if (IsKeyPressed(Key.Space)) EmitParticles(sparks, 120);
```

`SetParticleEmitterPosition` moves where its next particles start, as dust behind a running
wheel, `SetParticleEmitterActive` starts and stops its stream, `GetParticleEmitter` and
`SetParticleEmitter` read and change its settings with `with`, and `UnloadParticleEmitter`
removes it and its particles:

<!-- compiled with:
ParticleEmitterHandle dust = default;
Vector3 wheel = default;
float speed = 0;
-->
```csharp
SetParticleEmitterPosition(dust, wheel);
SetParticleEmitter(dust, GetParticleEmitter(dust) with { Rate = speed * 4 });
```

An emitter keeps room for `MaxParticles`, and once that many are alive the oldest are replaced.
Emitters laid over by alpha are drawn from the farthest from the camera to the nearest, by where
each emitter is, so where two overlap the nearer is in front, and the particles within one are
sorted far to near on the GPU each frame, so a puff of smoke in front covers one behind. A scene
drawn into a render texture has its particles too, drawn through the camera of its first
`BeginMode3D` and sorted from it, so each half of a split screen lays its smoke over in its own
order, an emitter of 300 sorted again for each of `games/Sumo`'s two views costing their render
textures some 0.03 ms of the GPU between them, and a texture drawn only in 2D shows none. In the ECS
an emitter is a `ParticleEmitter` component placed by its entity's `Transform`, which a scene file
saves.

## Vertices one at a time, and the matrix stack

raylib draws its shapes through rlgl, a layer where a program gives vertices one at a time, and
the calls its examples make are carried under rlgl's names. Between `rlBegin` and `rlEnd` each
`rlVertex3f` (or `rlVertex2f`) takes the color, texture coordinate and texture set before it, and
two, three or four make a line, a triangle or a quad. `rlPushMatrix` keeps the transform,
`rlTranslatef`, `rlRotatef` and `rlScalef` change it, and `rlPopMatrix` returns to it, and
everything drawn in between is moved, rlgl's vertices, shapes, text and models alike. The
`models_rlgl_solar_system` example turns the earth about the sun and the moon about the earth:

<!-- compiled with:
float earthOrbitRotation = 0, earthOrbitRadius = 8, earthRotation = 0, earthRadius = 1;
float moonOrbitRotation = 0, moonOrbitRadius = 1.5f, moonRadius = 0.25f;
void DrawSphereBasic(Color color) { }
-->
```csharp
rlPushMatrix();
    rlRotatef(earthOrbitRotation, 0.0f, 1.0f, 0.0f);    // Earth's orbit around the sun
    rlTranslatef(earthOrbitRadius, 0.0f, 0.0f);

    rlPushMatrix();
        rlRotatef(earthRotation, 0.25f, 1.0f, 0.0f);    // Earth turning
        rlScalef(earthRadius, earthRadius, earthRadius);
        DrawSphereBasic(Color.Blue);
    rlPopMatrix();

    rlRotatef(moonOrbitRotation, 0.0f, 1.0f, 0.0f);     // The moon's orbit around the earth
    rlTranslatef(moonOrbitRadius, 0.0f, 0.0f);
    rlScalef(moonRadius, moonRadius, moonRadius);
    DrawSphereBasic(Color.LightGray);
rlPopMatrix();
```

The transform given last applies first, as in rlgl, so the earth is scaled, turned and then
carried out along its orbit. A transform set with nothing pushed lasts until the next camera mode
begins or ends, and the next frame starts with none.

Culling is rlgl's to switch as well. `rlEnableBackfaceCulling` leaves out the faces turned away,
shapes and text among them, which are wound counterclockwise as raylib's are, so a triangle given
clockwise is not drawn. `rlSetCullFace(RlCullFace.Front)` leaves out the front faces instead, as
`shaders_cel_shading` does to draw an outline from the back of a model pushed out along its
normals, and `rlDisableBackfaceCulling` draws both. Until a program calls one of them, shapes
drawn inside `BeginMode3D` leave out their back faces, as rlgl's do from the start, so a cube
drawn around the camera is hollow seen from within, while 2D shapes and text draw both faces and
a model the faces its material says. Between `rlEnablePointMode` and `rlDisablePointMode` a model is drawn as a point at each
corner of its triangles.

rlgl's matrix modes are carried for a projection of a program's own. In
`rlMatrixMode(RlMatrixMode.Projection)`, `rlPushMatrix` and `rlPopMatrix` keep and restore the
projection `rlSetMatrixProjection` sets, and back in `RlMatrixMode.Modelview`, `rlLoadIdentity`
and `rlMultMatrixf` set the view.
`textures_portal_window` draws a second scene through an off-center frustum built from the eye
and the corners of an arch, so it lines up with the arch from wherever the camera stands, with
`rlEnableDepthTest` and `rlDisableDepthTest` around it as `BeginMode3D` and `EndMode3D` turn
the test on and off. `rlDisableDepthMask` tests what follows against the depth without writing
its own, models among it, so `models_skybox_rendering` draws its sky cube around the camera and the
grid after it shows in front. Models are drawn through the camera of `BeginMode3D` whatever rlgl's projection is, as
[compared with raylib](compared-with-raylib.md) says.

## See also

- Examples: [`models_geometric_shapes`](../3DEngine.Examples/Models/ModelsGeometricShapes.cs),
  [`core_3d_camera_free`](../3DEngine.Examples/Core/Core3DCameraFree.cs),
  [`core_3d_camera_first_person`](../3DEngine.Examples/Core/Core3DCameraFirstPerson.cs),
  [`textures_render_target`](../3DEngine.Examples/Textures/TexturesRenderTarget.cs),
  [`shaders_particles`](../3DEngine.Examples/Shaders/ShadersParticles.cs),
  [`models_rlgl_solar_system`](../3DEngine.Examples/Models/ModelsRlglSolarSystem.cs),
  [`shapes_rlgl_triangle`](../3DEngine.Examples/Shapes/ShapesRlglTriangle.cs),
  [`models_point_rendering`](../3DEngine.Examples/Models/ModelsPointRendering.cs),
  [`shaders_cel_shading`](../3DEngine.Examples/Shaders/ShadersCelShading.cs),
  [`textures_portal_window`](../3DEngine.Examples/Textures/TexturesPortalWindow.cs),
  [`core_vr_simulator`](../3DEngine.Examples/Core/CoreVrSimulator.cs)
- The game [`games/Sumo`](../games/Sumo/Program.cs), its window split between two cameras, each
  drawing into a render texture of its own
- The cheatsheet's [Frame and cameras](../CHEATSHEET.md#frame-and-cameras),
  [3D shapes](../CHEATSHEET.md#3d-shapes), [rlgl](../CHEATSHEET.md#rlgl) and [Particles](../CHEATSHEET.md#particles)
- Previous: [Drawing in 2D](drawing-2d.md)
- Next: [Textures and images](textures-and-images.md)
