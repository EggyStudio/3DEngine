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

The engine is the `3DEngine` package on nuget.org:

```bash
dotnet new console -n Hello && cd Hello
dotnet add package 3DEngine
```

The program at the top of this page goes into `Program.cs`, and `dotnet run` opens its window.
Where there is no display, `dotnet run -- --offscreen --frames 30` draws thirty frames with no
window and exits. A program can use a package built from a checkout of this repository instead,
as [BUILDING.md](https://github.com/EggyStudio/3DEngine/blob/main/.github/BUILDING.md#a-program-on-a-local-package) shows.

## Examples

Each example is a short program in `3DEngine.Examples`, run by name:

```bash
build/fetch-slang.sh                                          # once, for the shader compiler
dotnet run --project 3DEngine.Examples -- core_3d_camera_free
```

A picture of an example raylib also has opens raylib's C original of it, running in the browser on
raylib's site, and the name under it is the program here. `build/raylib-examples.sh` finds which
those are.

| | |
|---|---|
| <a href="https://www.raylib.com/examples/core/loader.html?name=core_basic_window"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_basic_window.png" width="400"/></a><br>`core_basic_window` | <a href="https://www.raylib.com/examples/core/loader.html?name=core_3d_camera_free"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_3d_camera_free.png" width="400"/></a><br>`core_3d_camera_free` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shapes_basic_2d.png" width="400"/><br>`shapes_basic_2d` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shapes_basic_3d.png" width="400"/><br>`shapes_basic_3d` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/gui_imgui_window.png" width="400"/><br>`gui_imgui_window` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_behaviors.png" width="400"/><br>`ecs_behaviors` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_basic.png" width="400"/><br>`textures_basic` | <a href="https://www.raylib.com/examples/models/loader.html?name=models_loading"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_loading.png" width="400"/></a><br>`models_loading` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_mesh_entities.png" width="400"/><br>`ecs_mesh_entities` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/audio_sound.png" width="400"/><br>`audio_sound` |
| <a href="https://www.raylib.com/examples/core/loader.html?name=core_input_gamepad"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_input_gamepad.png" width="400"/></a><br>`core_input_gamepad` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_render_target.png" width="400"/><br>`textures_render_target` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_fonts.png" width="400"/><br>`text_fonts` | <a href="https://www.raylib.com/examples/shaders/loader.html?name=shaders_postprocessing"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_postprocessing.png" width="400"/></a><br>`shaders_postprocessing` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_states.png" width="400"/><br>`ecs_states` | <a href="https://www.raylib.com/examples/models/loader.html?name=models_mesh_generation"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_mesh_generation.png" width="400"/></a><br>`models_mesh_generation` |
| <a href="https://www.raylib.com/examples/core/loader.html?name=core_3d_camera_first_person"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_3d_camera_first_person.png" width="400"/></a><br>`core_3d_camera_first_person` | <a href="https://www.raylib.com/examples/textures/loader.html?name=textures_image_drawing"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_image_drawing.png" width="400"/></a><br>`textures_image_drawing` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_mipmaps.png" width="400"/><br>`textures_mipmaps` | <a href="https://www.raylib.com/examples/text/loader.html?name=text_input_box"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_input_box.png" width="400"/></a><br>`text_input_box` |
| <a href="https://www.raylib.com/examples/core/loader.html?name=core_window_flags"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_window_flags.png" width="400"/></a><br>`core_window_flags` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_physics.png" width="400"/><br>`ecs_physics` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_terrain.png" width="400"/><br>`models_terrain` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_model.png" width="400"/><br>`shaders_model` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/scenes_level.png" width="400"/><br>`scenes_level` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_animation.png" width="400"/><br>`models_animation` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/physics_boxes.png" width="400"/><br>`physics_boxes` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/pusher.png" width="400"/><br>`games/Pusher` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/hopper.png" width="400"/><br>`games/Hopper` | <a href="https://www.raylib.com/examples/textures/loader.html?name=textures_bunnymark"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_bunnymark.png" width="400"/></a><br>`textures_bunnymark` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_stress.png" width="400"/><br>`models_stress` | <a href="https://www.raylib.com/examples/text/loader.html?name=text_font_sdf"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_font_sdf.png" width="400"/></a><br>`text_font_sdf` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_skybox.png" width="400"/><br>`models_skybox` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_compute_life.png" width="400"/><br>`shaders_compute_life` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_shadowmap.png" width="400"/><br>`shaders_shadowmap` | <a href="https://www.raylib.com/examples/core/loader.html?name=core_input_gestures"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_input_gestures.png" width="400"/></a><br>`core_input_gestures` |
| <a href="https://www.raylib.com/examples/shaders/loader.html?name=shaders_mesh_instancing"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_mesh_instancing.png" width="400"/></a><br>`shaders_mesh_instancing` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_animated_models.png" width="400"/><br>`ecs_animated_models` |
| <a href="https://www.raylib.com/examples/core/loader.html?name=core_2d_camera"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_2d_camera.png" width="400"/></a><br>`core_2d_camera` | <a href="https://www.raylib.com/examples/core/loader.html?name=core_drop_files"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_drop_files.png" width="400"/></a><br>`core_drop_files` |
| <a href="https://www.raylib.com/examples/audio/loader.html?name=audio_raw_stream"><img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/audio_raw_stream.png" width="400"/></a><br>`audio_raw_stream` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_reflection_probe.png" width="400"/><br>`models_reflection_probe` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_bloom.png" width="400"/><br>`shaders_bloom` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/summit.png" width="400"/><br>`games/Summit` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_morph_and_layers.png" width="400"/><br>`models_morph_and_layers` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_compute_texture.png" width="400"/><br>`shaders_compute_texture` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_auto_exposure.png" width="400"/><br>`shaders_auto_exposure` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/swarm.png" width="400"/><br>`games/Swarm` |

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
is saved while the game runs. BUILDING.md says how they are built.

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

Programs take `--serve`, `--hidden`, `--offscreen`, `--headless` and `--frames N`, and a game adds commands with
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
- **The effects over the frame are bloom, exposure, a choice of curve, color grading, a vignette and
  FXAA**, with no depth of field or motion blur, past what a program draws through a render
  texture and a shader of its own.
- **Linux is the tested platform**, in CI on every push. Windows builds and runs the tests that
  need no GPU in CI, and macOS builds from the same packages and is not covered.

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

[Mozilla Public License 2.0](https://github.com/EggyStudio/3DEngine/blob/main/LICENSE).
