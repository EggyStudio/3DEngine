<p align="center">
  <img src="https://raw.githubusercontent.com/EggyStudio/3DEngine/main/.github/assets/icon.png" alt="3D Engine icon" width="160"/>
</p>

<h1 align="center">3D Engine</h1>

<p align="center">A C# engine on SDL3, Vulkan, Dear ImGui and Slang, used the way raylib is used.</p>

<p align="center">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-512BD4">
  <img alt="Graphics" src="https://img.shields.io/badge/Graphics-Vulkan-AC162C">
  <img alt="Windowing" src="https://img.shields.io/badge/Windowing-SDL3-0B7BB2">
  <img alt="Shaders" src="https://img.shields.io/badge/Shaders-Slang-2E7D32">
  <img alt="Status" src="https://img.shields.io/badge/Status-Early-yellow">
</p>

```csharp
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

A program opens a window, draws each frame with static calls and closes the window, and every
call it can make is on one [cheatsheet](.github/CHEATSHEET.md). Dear ImGui works between
`BeginDrawing` and `EndDrawing` with no setup. Under the flat API is an ECS whose behaviors are
`[Behavior]` structs a source generator turns into systems, and it runs inside the same frames, so a
program uses as much of it as it needs.

## Contents

- [Examples](#examples)
- [The ECS underneath](#the-ecs-underneath)
- [Driving a running app](#driving-a-running-app)
- [Building](#building)
- [Status](#status)
- [Documents](#documents)
- [License](#license)

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

## The ECS underneath

`InitWindow` builds an `App` with the default plugins, and `BeginDrawing` and `EndDrawing` run its
stages. Anything registered on `GetApp()` runs inside those frames. A behavior is a struct whose
fields are per-entity state and whose methods are systems:

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

The loop draws what the world holds with the same flat calls:

```csharp
foreach (var (_, ball) in GetApp().World.Resource<EcsWorld>().Query<Ball>())
    DrawSphere(ball.Position, 0.3f, Color.Red);
```

The ECS keeps components in sparse sets, runs systems in parallel batches by the components they
read and write, defers structural changes through `EcsCommands`, and compiles behaviors from
source files while an app runs. [ARCHITECTURE.md](.github/ARCHITECTURE.md) describes how.

## Driving a running app

`./e3d` drives any program built on the engine from a terminal, which is how changes are checked
without opening a visible window:

```bash
./e3d open models_loading --hidden     # renders, but no window appears
./e3d command entity.count
./e3d command input.key W 40           # input through the engine
./e3d shot after.png                   # the next frame, as a PNG
./e3d stop
```

Programs take `--serve`, `--hidden`, `--headless` and `--frames N`, and a game adds commands with
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
through Assimp, sounds and music, text and ImGui work through the flat API, and the ECS, the
scheduler and behaviors are tested. What is missing:

- **Shaders do not load through the flat API.** There is no `LoadShader`, so every draw uses the
  engine's own shaders.
- **Music is decoded whole** rather than streamed, so a long piece costs its length in memory.
- **Lighting is one fixed light.** Models are shaded by it, shapes are unlit as raylib's are, and
  the ECS's meshes are drawn in their base color.
- **Text is ImGui's font**, drawn above everything else.
- **No gamepads or render targets** in the flat API.
- **Linux is the tested platform**, in CI on every push. Windows and macOS build from the same
  packages and are not covered by CI.

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
