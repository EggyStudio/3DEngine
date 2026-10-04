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
call it can make is on one [cheatsheet](.github/CHEATSHEET.md). Dear ImGui works between
`BeginDrawing` and `EndDrawing` with no setup. Under the flat API is an ECS whose behaviors are
`[Behavior]` structs like `Ball`, whose fields are each entity's state and whose methods a source
generator turns into systems. It runs inside the same frames, so a program uses as much of it as it
needs. [DESIGN.md](.github/DESIGN.md#5-the-ecs-underneath) shows the rest of it, and
[ARCHITECTURE.md](.github/ARCHITECTURE.md) how it is built.

## Contents

- [A program of your own](#a-program-of-your-own)
- [Examples](#examples)
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
as [BUILDING.md](.github/BUILDING.md#a-program-on-a-local-package) shows.

## Examples

Each example is a short program in `3DEngine.Examples`, run by name:

```bash
build/fetch-slang.sh                                          # once, for the shader compiler
dotnet run --project 3DEngine.Examples -- core_3d_camera_free
```

| | |
|---|---|
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_basic_window.png" width="400"/><br>`core_basic_window` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_3d_camera_free.png" width="400"/><br>`core_3d_camera_free` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shapes_basic_2d.png" width="400"/><br>`shapes_basic_2d` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shapes_basic_3d.png" width="400"/><br>`shapes_basic_3d` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/gui_imgui_window.png" width="400"/><br>`gui_imgui_window` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_behaviors.png" width="400"/><br>`ecs_behaviors` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_basic.png" width="400"/><br>`textures_basic` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_loading.png" width="400"/><br>`models_loading` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_mesh_entities.png" width="400"/><br>`ecs_mesh_entities` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/audio_sound.png" width="400"/><br>`audio_sound` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_input_gamepad.png" width="400"/><br>`core_input_gamepad` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_render_target.png" width="400"/><br>`textures_render_target` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_fonts.png" width="400"/><br>`text_fonts` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_postprocessing.png" width="400"/><br>`shaders_postprocessing` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_states.png" width="400"/><br>`ecs_states` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_mesh_generation.png" width="400"/><br>`models_mesh_generation` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_3d_camera_first_person.png" width="400"/><br>`core_3d_camera_first_person` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_image_drawing.png" width="400"/><br>`textures_image_drawing` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_mipmaps.png" width="400"/><br>`textures_mipmaps` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_input_box.png" width="400"/><br>`text_input_box` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_window_flags.png" width="400"/><br>`core_window_flags` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/ecs_physics.png" width="400"/><br>`ecs_physics` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_terrain.png" width="400"/><br>`models_terrain` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_model.png" width="400"/><br>`shaders_model` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/scenes_level.png" width="400"/><br>`scenes_level` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_animation.png" width="400"/><br>`models_animation` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/physics_boxes.png" width="400"/><br>`physics_boxes` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/pusher.png" width="400"/><br>`games/Pusher` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/hopper.png" width="400"/><br>`games/Hopper` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/textures_bunnymark.png" width="400"/><br>`textures_bunnymark` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_stress.png" width="400"/><br>`models_stress` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/text_font_sdf.png" width="400"/><br>`text_font_sdf` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/models_skybox.png" width="400"/><br>`models_skybox` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_compute_life.png" width="400"/><br>`shaders_compute_life` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_shadowmap.png" width="400"/><br>`shaders_shadowmap` | <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/core_input_gestures.png" width="400"/><br>`core_input_gestures` |
| <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/examples/shaders_mesh_instancing.png" width="400"/><br>`shaders_mesh_instancing` | |

`games/Pusher` is a small game outside the solution, built from the package `build/pack.sh` makes,
as a game of your own would be. It has a level from a scene file, an animated player pushing crates
into a goal with physics and contacts, a light with a shadow, a sky, a sound, text, an ImGui panel,
and a menu, play and pause. `games/Hopper` is its 2D counterpart, a platformer with a sprite sheet,
a tile atlas, a following 2D camera, coins, music, a loaded font, a scaled pixel view, a gamepad and
a saved high score. BUILDING.md says how they are built.

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
`[Command]` on a static method. [The skill](.claude/skills/e3d-cli/SKILL.md) lists the commands.

## Building

You need the .NET 10 SDK and a Vulkan driver. SDL3, Assimp and Dear ImGui come with their NuGet
packages, and `build/fetch-slang.sh` downloads the Slang compiler into `build/tools`.

```bash
build/fetch-slang.sh
dotnet build 3DEngine.slnx
dotnet test 3DEngine.Tests
```

[BUILDING.md](.github/BUILDING.md) has the layout, the platforms and how shaders are compiled.

## Status

Early. The window, input, the frame, cameras, 2D and 3D shapes, images and textures, models
through Assimp, sounds and music, text in fonts, render targets, Slang shaders, gamepads and ImGui work through the flat API, and the ECS, the
scheduler and behaviors are tested. What is missing:

- **Custom shaders reach shapes, textures and text**, not models, which keep the engine's model
  shader.
- **Music is decoded whole** rather than streamed, so a long piece costs its length in memory.
- **The flat API has no lights of its own.** Its models are shaded by one fixed light, shapes are
  unlit as raylib's are, and light entities in the ECS light models and meshes alike by their
  metallic-roughness materials, with shadows from one directional light.
- **Linux is the tested platform**, in CI on every push. Windows builds and runs the tests that
  need no GPU in CI, and macOS builds from the same packages and is not covered.

[TODO.md](.github/TODO.md) lists the rest, in the order it blocks making a game.

## Documents

| | |
|---|---|
| [CHEATSHEET.md](.github/CHEATSHEET.md) | Every function of the flat API |
| [DESIGN.md](.github/DESIGN.md) | The rules the API follows, and the dependency policy |
| [ARCHITECTURE.md](.github/ARCHITECTURE.md) | The app, the schedule, the ECS and the renderer |
| [RENDERING.md](.github/RENDERING.md) | The renderer and the order it grows in |
| [BUILDING.md](.github/BUILDING.md) | Building, testing and platforms |
| [TODO.md](.github/TODO.md) | Outstanding work |

## License

[Mozilla Public License 2.0](LICENSE).
