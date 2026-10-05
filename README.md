<h1 align="center">3D Engine</h1>

<p align="center">A C# engine on SDL3, Vulkan, ImGui and Slang. Raylib's design over a Bevy-style ECS and scheduler, scripted with its own Unity-style behaviors.</p>

<p align="center">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-512BD4">
  <img alt="Graphics" src="https://img.shields.io/badge/Graphics-Vulkan-AC162C">
  <img alt="Windowing" src="https://img.shields.io/badge/Windowing-SDL3-0B7BB2">
  <img alt="Shaders" src="https://img.shields.io/badge/Shaders-Slang-2E7D32">
  <img alt="Status" src="https://img.shields.io/badge/Status-Early-yellow">
</p>

```csharp
using Engine;
using static Engine.Engine3D;

InitWindow(800, 450, "[core] basic window");
SetTargetFPS(60);

while (!WindowShouldClose())
{
    BeginDrawing();
    ClearBackground(Color.RayWhite);
    DrawText("Congrats! You created your first window!", 190, 200, 20, Color.LightGray);
    EndDrawing();
}

CloseWindow();
```

That is raylib's first example, and why not raylib itself is a fair question. This engine keeps
its flat API and its loop, in C# with no binding, and adds a Vulkan renderer with shadows and
reflections, an ECS, physics, skeletal animation, scene files and ImGui in the frame. It costs
raylib's reach, the web, phones and small boards, and asks for Vulkan. The comparison, with what
was measured, is [Compared with raylib](https://github.com/EggyStudio/3DEngine/blob/main/docs/compared-with-raylib.md).

```csharp
[Behavior]
public struct Ball
{
    public Vector3 Position;
    public Vector3 Velocity;

    [OnUpdate]
    public void Move(BehaviorContext ctx)
    {
        var dt = (float)ctx.Time.DeltaSeconds;
        Velocity.Y -= 9.81f * dt;
        Position += Velocity * dt;
    }
}
```

A program opens a window, draws each frame with static calls and closes the window, and every
call it can make is on one [cheatsheet](https://github.com/EggyStudio/3DEngine/blob/main/CHEATSHEET.md). Dear ImGui works between
`BeginDrawing` and `EndDrawing` with no setup. Under the flat API is an ECS whose behaviors are
`[Behavior]` structs like `Ball`, whose fields are each entity's state and whose methods a source
generator turns into systems. It runs inside the same frames, so a program uses as much of it as it
needs. [DESIGN.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/DESIGN.md#5-the-ecs-underneath) shows the rest of it, and
[ARCHITECTURE.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/ARCHITECTURE.md) how it is built.

## Contents

- [A program of your own](#a-program-of-your-own)
- [Examples](#examples)
- [Guide](#guide)
- [Driving a running app](#driving-a-running-app)
- [Building](#building)
- [Status](#status)
- [Documents](#documents)
- [License](#license)

## A program of your own

The engine is the `3DEngine` package on nuget.org, and `3DEngine.Templates` beside it makes a
game from it in one command:

```bash
dotnet new install 3DEngine.Templates
dotnet new 3dengine -n Hello && cd Hello
dotnet run
```

The project has a window, a loop and a cube in `Program.cs`, a `resources` folder for what it
loads, and a `source/behaviors` folder for scripts the running game compiles again when they are
saved. `dotnet new 3dengine-ecs` makes one whose state is in behaviors instead, with a script in
that folder setting how fast its cubes turn. Where there is no display,
`dotnet run -- --offscreen --frames 30` draws thirty frames with no window and exits.

Without the templates, a console project takes the package:

```bash
dotnet new console -n Hello && cd Hello
dotnet add package 3DEngine
```

and the program at the top of this page goes into `Program.cs`. A program can use a package built
from a checkout of this repository instead, as
[BUILDING.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/BUILDING.md#a-program-on-a-local-package)
shows.

## Examples

Each example is a short program in `3DEngine.Examples`, run by name:

```bash
build/fetch-slang.sh                                          # once, for the shader compiler
dotnet run --project 3DEngine.Examples -- core_3d_camera_free
```

A picture opens the program that drew it, and the name under it is the one it runs by. Each of
raylib's own examples, and what is written of it here, is a row of
[the examples table](https://github.com/EggyStudio/3DEngine/blob/main/.github/EXAMPLES.md).

| | |
|---|---|
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Core/CoreBasicWindow.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_basic_window.webp" width="400"/></a><br>`core_basic_window` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Core/Core3DCameraFree.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_3d_camera_free.webp" width="400"/></a><br>`core_3d_camera_free` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shapes/ShapesBasic2D.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shapes_basic_2d.webp" width="400"/></a><br>`shapes_basic_2d` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shapes/ShapesBasic3D.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shapes_basic_3d.webp" width="400"/></a><br>`shapes_basic_3d` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Gui/GuiImGuiWindow.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/gui_imgui_window.webp" width="400"/></a><br>`gui_imgui_window` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Ecs/EcsBehaviors.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_behaviors.webp" width="400"/></a><br>`ecs_behaviors` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Textures/TexturesBasic.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_basic.webp" width="400"/></a><br>`textures_basic` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Models/ModelsLoading.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_loading.webp" width="400"/></a><br>`models_loading` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Ecs/EcsMeshEntities.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_mesh_entities.webp" width="400"/></a><br>`ecs_mesh_entities` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Audio/AudioSound.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/audio_sound.webp" width="400"/></a><br>`audio_sound` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Core/CoreInputGamepad.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_input_gamepad.webp" width="400"/></a><br>`core_input_gamepad` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Textures/TexturesRenderTarget.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_render_target.webp" width="400"/></a><br>`textures_render_target` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Text/TextFonts.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_fonts.webp" width="400"/></a><br>`text_fonts` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersPostprocessing.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_postprocessing.webp" width="400"/></a><br>`shaders_postprocessing` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Ecs/EcsStates.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_states.webp" width="400"/></a><br>`ecs_states` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Models/ModelsMeshGeneration.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_mesh_generation.webp" width="400"/></a><br>`models_mesh_generation` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Core/Core3DCameraFirstPerson.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_3d_camera_first_person.webp" width="400"/></a><br>`core_3d_camera_first_person` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Textures/TexturesImageDrawing.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_image_drawing.webp" width="400"/></a><br>`textures_image_drawing` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Textures/TexturesMipmaps.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_mipmaps.webp" width="400"/></a><br>`textures_mipmaps` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Text/TextInputBox.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_input_box.webp" width="400"/></a><br>`text_input_box` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Core/CoreWindowFlags.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_window_flags.webp" width="400"/></a><br>`core_window_flags` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Ecs/EcsPhysics.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_physics.webp" width="400"/></a><br>`ecs_physics` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Models/ModelsTerrain.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_terrain.webp" width="400"/></a><br>`models_terrain` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersModel.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_model.webp" width="400"/></a><br>`shaders_model` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Scenes/ScenesLevel.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/scenes_level.webp" width="400"/></a><br>`scenes_level` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Models/ModelsAnimation.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_animation.webp" width="400"/></a><br>`models_animation` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Physics/PhysicsBoxes.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/physics_boxes.webp" width="400"/></a><br>`physics_boxes` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/games/Pusher/Program.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/pusher.webp" width="400"/></a><br>`games/Pusher` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/games/Hopper/Program.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/hopper.webp" width="400"/></a><br>`games/Hopper` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Benchmarks/TexturesBunnymark.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_bunnymark.webp" width="400"/></a><br>`textures_bunnymark` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Benchmarks/ModelsStress.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_stress.webp" width="400"/></a><br>`models_stress` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Text/TextFontSdf.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_font_sdf.webp" width="400"/></a><br>`text_font_sdf` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Models/ModelsSkybox.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_skybox.webp" width="400"/></a><br>`models_skybox` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersComputeLife.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_compute_life.webp" width="400"/></a><br>`shaders_compute_life` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersShadowmap.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_shadowmap.webp" width="400"/></a><br>`shaders_shadowmap` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Core/CoreInputGestures.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_input_gestures.webp" width="400"/></a><br>`core_input_gestures` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersMeshInstancing.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_mesh_instancing.webp" width="400"/></a><br>`shaders_mesh_instancing` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Ecs/EcsAnimatedModels.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_animated_models.webp" width="400"/></a><br>`ecs_animated_models` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Core/Core2DCamera.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_2d_camera.webp" width="400"/></a><br>`core_2d_camera` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Core/CoreDropFiles.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_drop_files.webp" width="400"/></a><br>`core_drop_files` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Audio/AudioRawStream.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/audio_raw_stream.webp" width="400"/></a><br>`audio_raw_stream` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Models/ModelsReflectionProbe.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_reflection_probe.webp" width="400"/></a><br>`models_reflection_probe` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersBloom.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_bloom.webp" width="400"/></a><br>`shaders_bloom` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/games/Summit/Program.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/summit.webp" width="400"/></a><br>`games/Summit` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Models/ModelsMorphAndLayers.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_morph_and_layers.webp" width="400"/></a><br>`models_morph_and_layers` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersComputeTexture.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_compute_texture.webp" width="400"/></a><br>`shaders_compute_texture` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersAutoExposure.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_auto_exposure.webp" width="400"/></a><br>`shaders_auto_exposure` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/games/Swarm/Program.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/swarm.webp" width="400"/></a><br>`games/Swarm` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/3DEngine.Examples/Shaders/ShadersParticles.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_particles.webp" width="400"/></a><br>`shaders_particles` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/games/Rally/Program.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/rally.webp" width="400"/></a><br>`games/Rally` |
| <a href="https://github.com/EggyStudio/3DEngine/blob/main/games/Manor/Program.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/manor.webp" width="400"/></a><br>`games/Manor` | <a href="https://github.com/EggyStudio/3DEngine/blob/main/games/Tactics/Program.cs"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/tactics.webp" width="400"/></a><br>`games/Tactics` |

`games/Pusher` is a small game outside the solution, built from the package `build/pack.sh` makes,
as a game of your own would be. It has a level from a scene file, an animated player pushing crates
into a goal with physics and contacts, a light with a shadow, a sky, a sound, text, an ImGui panel,
and a menu, play and pause. `games/Hopper` is its 2D counterpart, a platformer with a sprite sheet,
a tile atlas, a following 2D camera, coins, music, a loaded font, a scaled pixel view, a gamepad and
a saved high score. `games/Summit` is a 3D platformer: a level of prefabs solid as their meshes are
drawn, an animated character on the character controller with a camera that follows it and comes
in front of walls, a carousel turned by a hinge's motor, a bridge hung on ropes, a lift, orbs that
glow through bloom and are collected by triggers, a sun outdoors and a lamp in a house a reflection
probe lights, a sky, music and sounds, a menu, a pause, a restart and a gamepad. `games/Swarm` is
written in the ECS instead, its loop only opening and closing frames: waves of creatures placed from
prefabs walk at the player, hundreds at once, each moved by a behavior taking its components, shots
are triggers that harm by their speed, the waves go by states and sub-states, a HUD is drawn in
ImGui, many short sounds overlap, and a script of the game's numbers is compiled again whenever it
is saved while the game runs. `games/Rally` races a car round a dirt road over hills from noise:
a heightmap solid as it is drawn, a car of one box held up by four rays cast to the ground as
springs, gates that are triggers counting the laps, a ghost of the best lap kept in a file, dust
from the tyres, motion blur with speed, and an engine's note pitched by it. `games/Manor` is a
first-person walk through a house of six rooms and the grounds round it, looking for six lanterns:
a grid of prefab cells streamed in as the player nears and let go behind, doors on hinges swung by
motors when their sensors see the player, rooms lit by lamps and reflection probes and a yard by
the sun's cascaded shadows, fire, steam, dust and a fountain's spray as particles, exposure that
opens indoors, and a settings screen for the resolution, vertical sync, volumes and key and button
bindings, kept in a file. All of it, menus included, is played with a gamepad alone.
`games/Tactics` is a strategy board seen from above: soldiers, archers and a knight on grass,
woods, hills and water, picked with the mouse by a ray from the camera to the tile or unit it
meets, or several at once by a box dragged round them and listed in an ImGui panel, walking as far
as their moves allow by the cheapest way, which Dijkstra's search finds. Each side sees only what
its units can, past no woods, and the rest is fog. The computer's side heads for the nearest enemy
it sees by A*, and a match is saved to a file and taken up again. BUILDING.md says how they are built.

A 3D scene with a camera the keyboard and mouse move:

```csharp
var camera = new Camera3D(new Vector3(10, 10, 10), Vector3.Zero, Vector3.UnitY, 45);

while (!WindowShouldClose())
{
    UpdateCamera(ref camera, CameraMode.Free);

    BeginDrawing();
    ClearBackground(Color.RayWhite);

    BeginMode3D(camera);
    DrawCube(Vector3.Zero, 2, 2, 2, Color.Red);
    DrawCubeWires(Vector3.Zero, 2, 2, 2, Color.Maroon);
    DrawGrid(10, 1);
    EndMode3D();

    ImGui.Begin("Stats");
    ImGui.Text($"{GetFPS()} FPS");
    ImGui.End();

    EndDrawing();
}
```

## Guide

The guide in [`docs/`](https://github.com/EggyStudio/3DEngine/blob/main/docs) is for somebody
using the engine, a page an area, each built on an example that runs, read in order or by area.

| | |
|---|---|
| [A first game](https://github.com/EggyStudio/3DEngine/blob/main/docs/first-game.md) | From `dotnet new 3dengine` to a small finished game in twelve steps, each a few lines and a picture |
| [The window and the frame](https://github.com/EggyStudio/3DEngine/blob/main/docs/window-and-frame.md) | Opening a window, the loop, time, the window's state and ImGui in the frame |
| [Drawing in 2D](https://github.com/EggyStudio/3DEngine/blob/main/docs/drawing-2d.md) | Shapes, colors, text, splines, a 2D camera and collision |
| [Drawing in 3D and cameras](https://github.com/EggyStudio/3DEngine/blob/main/docs/drawing-3d-and-cameras.md) | 3D shapes, moving a camera, projections, picking and drawing into a texture |
| [Textures and images](https://github.com/EggyStudio/3DEngine/blob/main/docs/textures-and-images.md) | Loading and drawing textures, filtering, editing images, sprites and many sprites |
| [Text and fonts](https://github.com/EggyStudio/3DEngine/blob/main/docs/text-and-fonts.md) | The default font, fonts from files, other scripts, typed text and distance field fonts |
| [Models and animation](https://github.com/EggyStudio/3DEngine/blob/main/docs/models-and-animation.md) | Loading and generating models, terrain, skeletal animation, layered clips, morph targets, a sky and instancing |
| [Materials, light and shadows](https://github.com/EggyStudio/3DEngine/blob/main/docs/materials-light-and-shadows.md) | Metallic and rough surfaces, maps, glowing and see-through surfaces, bloom and effects over the frame, lights and shadows, reflection probes |
| [Shaders and compute](https://github.com/EggyStudio/3DEngine/blob/main/docs/shaders-and-compute.md) | Slang shaders for 2D drawing and models, post processing, compute shaders, their buffers and the textures they write |
| [Audio](https://github.com/EggyStudio/3DEngine/blob/main/docs/audio.md) | Sounds, streamed music, volume, pitch and pan, and sound placed in a 3D world |
| [Input](https://github.com/EggyStudio/3DEngine/blob/main/docs/input.md) | Keys, the mouse, touch and gestures, gamepads, and input shared with ImGui |
| [Physics](https://github.com/EggyStudio/3DEngine/blob/main/docs/physics.md) | Bodies that fall and collide, rays, contacts, triggers, joints and a character |
| [Behaviors and the ECS](https://github.com/EggyStudio/3DEngine/blob/main/docs/behaviors-and-the-ecs.md) | Behaviors, stages, spawning entities, filters, and the world from the loop |
| [States](https://github.com/EggyStudio/3DEngine/blob/main/docs/states.md) | Screens and modes as a state, behaviors that follow it, and states within states |
| [Scenes](https://github.com/EggyStudio/3DEngine/blob/main/docs/scenes.md) | Saving and loading levels, the file, a program's own components, and scenes inside scenes |
| [Driving a program with e3d](https://github.com/EggyStudio/3DEngine/blob/main/docs/driving-with-e3d.md) | Asking a running program about its world, input, captures, the log and commands of its own |
| [Shipping a game](https://github.com/EggyStudio/3DEngine/blob/main/docs/shipping-a-game.md) | A game of its own on the package, and one native executable a player runs |
| [Compared with raylib](https://github.com/EggyStudio/3DEngine/blob/main/docs/compared-with-raylib.md) | What is the same as raylib, what this engine adds, what it costs, and what was measured |
| [CHEATSHEET.md](https://github.com/EggyStudio/3DEngine/blob/main/CHEATSHEET.md) | Every function of the flat API on one line |

## Driving a running app

`./e3d` drives any program built on the engine from a terminal, which is how changes are checked
without opening a visible window:

```bash
./e3d open models_loading --offscreen  # renders, with no window and no display needed
./e3d command entity.count
./e3d command input.key W 40           # input through the engine
./e3d shot after.png                   # the next frame, as a PNG
./e3d stop
```

Programs take `--serve`, `--hidden`, `--offscreen`, `--headless`, `--frames N` and
`--frame-time S`, and a game adds commands with
`[Command]` on a static method. [The skill](https://github.com/EggyStudio/3DEngine/blob/main/.claude/skills/e3d-cli/SKILL.md) lists the commands.

## Building

You need the .NET 10 SDK and a Vulkan 1.3 driver. SDL3, Assimp and Dear ImGui come with their NuGet
packages, and `build/fetch-slang.sh` downloads the Slang compiler into `build/tools`.

```bash
build/fetch-slang.sh
dotnet build 3DEngine.slnx
dotnet test 3DEngine.Tests
```

[BUILDING.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/BUILDING.md) has the layout, the platforms and how shaders are compiled.

## Status

Early, and used for small games. The flat API carries most of raylib's: the window and input,
2D and 3D shapes, images and textures, models through Assimp with skeletal animation, sounds, music,
audio streams, text in fonts, render targets, Slang shaders for shapes and models, compute shaders,
lights with shadows, an environment map and reflection probes, physics, states and scenes, with
ImGui in the same frame. The ECS, the
scheduler and behaviors run underneath, and a game ships as one native executable through native
AOT. What is missing:

- **Some of raylib is not carried**, as VR stereo and the audio processors, which TODO.md names
  with the reasons.
- **The effects over the frame are bloom, exposure, a choice of curve, color grading, a vignette,
  FXAA, depth of field and motion blur**, with ambient occlusion beside them, past what a program
  draws through a render texture and a shader of its own.
- **CI draws on Linux and Windows through lavapipe and on macOS through MoltenVK**, under the
  validation layer, on every push, and the development is on Linux, where every example and game
  is played and captured.

[TODO.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/TODO.md) lists the rest, in the order it blocks making a game.

## Documents

| | |
|---|---|
| [DESIGN.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/DESIGN.md) | The rules the API follows, and the dependency policy |
| [ARCHITECTURE.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/ARCHITECTURE.md) | The app, the schedule, the ECS and the renderer |
| [RENDERING.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/RENDERING.md) | The renderer and the order it grows in |
| [BUILDING.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/BUILDING.md) | Building, testing and platforms |
| [TODO.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/TODO.md) | Outstanding work |

## License

[Mozilla Public License 2.0](https://github.com/EggyStudio/3DEngine/blob/main/LICENSE). The
libraries the engine depends on, and their licenses, are in
[THIRD-PARTY-NOTICES.md](https://github.com/EggyStudio/3DEngine/blob/main/THIRD-PARTY-NOTICES.md),
which the package carries beside the license.
