# Design

How 3DEngine is meant to be used, and the rules its public surface follows. The model is raylib,
where a program opens a window, loops over frames, draws in each one with plain function calls and closes
the window, and every function it can call fits on one cheatsheet. Dear ImGui is used the same way,
inside the same frame. The ECS and the behaviors underneath are available to a program that grows
into them, and a program that does not use them never sees them.

The engine is C# on .NET 10, with SDL3 for the window, input and audio, Vulkan for drawing, Dear
ImGui for interfaces and Slang for shaders. It runs on Linux, Windows and macOS (through MoltenVK).

## What exists

- **One assembly.** Every module compiles into `3DEngine.dll` under the `Engine` namespace, with the
  source generator as a separate analyzer assembly.
- **A sparse-set ECS** (`EcsWorld`) with deferred commands, change bits and queries over up to three
  components, and a resource map (`World`) beside it.
- **A staged schedule** (`Startup`, `First`, `PreUpdate`, `Update`, `PostUpdate`, `Render`, `Last`,
  `Cleanup`) that runs systems in parallel batches by their declared reads and writes.
- **Behaviors**, which are `[Behavior]` structs whose stage methods a Roslyn generator turns into
  systems, with filters, run conditions and toggle keys.
- **An SDL3 window** with keyboard and mouse input, and **a Vulkan device** over Vortice.Vulkan with
  a render graph that draws meshes and ImGui.

What is missing is the surface this document describes. There is no flat API, a program has to
assemble an `App` and its plugins before anything is drawn, and nothing can be drawn without
spawning an entity first.

## 1. One flat API

Every operation a program needs is a static method on one class, `Engine3D`, split into files by
area. A program imports it once:

```csharp
using static Engine.Engine3D;

InitWindow(1280, 720, "Hello");
SetTargetFPS(60);

while (!WindowShouldClose())
{
    BeginDrawing();
    ClearBackground(Color.RayWhite);
    DrawText("A window", 20, 20, 20, Color.DarkGray);
    EndDrawing();
}

CloseWindow();
```

Names follow raylib where raylib has the operation, so somebody who has written a raylib program
can guess the call, and the cheatsheet reads the same way:

| prefix | meaning |
|---|---|
| `Init` and `Close` | start and stop a subsystem that exists once, such as the window or the audio device |
| `Begin` and `End` | open and close a scope that later calls draw into, such as the frame, a camera or a render target |
| `Load` and `Unload` | create and free a resource the program owns, such as a texture, a model or a shader |
| `Draw` | record something for the current frame |
| `Is`, `Get` and `Set` | ask about or change a piece of state |

The areas mirror raylib's modules, and each is one file under `3DEngine/Api/`:

| file | covers |
|---|---|
| `Engine3D.Window.cs` | the window, the monitor, frame timing |
| `Engine3D.Input.cs` | keyboard, mouse and gamepad |
| `Engine3D.Drawing.cs` | the frame, cameras and render targets |
| `Engine3D.Shapes.cs` | 2D shapes |
| `Engine3D.Shapes3D.cs` | 3D shapes and the grid |
| `Engine3D.Textures.cs` | images and textures |
| `Engine3D.Text.cs` | fonts and text |
| `Engine3D.Models.cs` | meshes, models and materials |
| `Engine3D.Shaders.cs` | Slang shaders and their parameters |
| `Engine3D.Audio.cs` | sounds and music |

Arguments are plain values (`Vector3`, `Color`, `Rectangle`, `Camera3D`), and resources are small
structs holding an id, so nothing in the API needs a class hierarchy to be understood.

## 2. The frame

`InitWindow` builds an `App` with the default plugins and runs its `Startup` stage. The loop the
program writes then drives the app one frame at a time:

| call | what runs |
|---|---|
| `WindowShouldClose` | reports whether the window was asked to close |
| `BeginDrawing` | `First`, `PreUpdate` and `Update`, which poll SDL, advance time and input and run the game's systems, then a new ImGui frame |
| `EndDrawing` | `PostUpdate`, `Render` and `Last`, which apply deferred commands, render what was recorded and present, then waits for the target frame time |
| `CloseWindow` | `Cleanup`, then disposes the app |

So the frame a raylib-style loop sees and the frame an ECS system sees are the same frame, and a
program can mix the two. `App.Run()` drives the same steps for a program that hands the loop to the
engine.

## 3. Immediate drawing

A `Draw` call records something into the frame's draw list and returns. Nothing it records
outlives the frame, so a cube drawn in one frame and not the next is gone. Shapes are batched into
lines and triangles of position and color, in the manner of raylib's rlgl layer, and drawn by one
pass after the meshes the ECS holds and before ImGui.

`BeginMode3D(camera)` sets the view the following calls draw through, and `EndMode3D` returns to
screen space for 2D shapes and text. A `Camera3D` is a plain struct the program keeps and updates
(`UpdateCamera(ref camera, CameraMode.Free)`), so a camera is a value rather than an entity until a
program decides it should be one.

## 4. ImGui inside the frame

`BeginDrawing` starts an ImGui frame and `EndDrawing` renders it, so `ImGui.*` calls work anywhere
in between with no setup.

```csharp
BeginDrawing();
ClearBackground(Color.Black);
ImGui.Begin("Stats");
ImGui.Text($"{GetFPS()} fps");
ImGui.End();
EndDrawing();
```

ImGui is drawn last, over everything else. `Engine3D` never wraps ImGui's own API, because ImGui
is already immediate and flat, and a second name for each widget would be a second cheatsheet.

## 5. The ECS underneath

`Engine3D.App` is the app `InitWindow` built. A program reaches the ECS through it, and anything
registered there runs inside the frames the loop drives:

```csharp
InitWindow(1280, 720, "Spinning");
App.AddPlugin(new PhysicsPlugin());

[Behavior]
public partial struct Spin
{
    public float Speed;

    [OnUpdate]
    public void Tick(BehaviorContext ctx)
    {
        ref var transform = ref ctx.Ecs.GetRef<Transform>(ctx.EntityId);
        transform.Rotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitY, Speed * (float)ctx.Time.DeltaSeconds);
    }
}
```

The flat functions work inside systems too, so an `[OnRender]` method can call `DrawCube`. A game
that outgrows the loop moves its logic into behaviors one piece at a time, and nothing in the flat
API has to be unlearned.

## 6. Resources the program owns

`Load` returns a resource and `Unload` frees it, and the program decides when. There is no
reference counting and no garbage collection of GPU memory in the flat API, since raylib's
experience is that a pair of calls is understood by everyone and leaks are found by the log, which
reports what was still loaded at `CloseWindow`. The asset server under the ECS keeps its own
handles with hot reload for the systems that use it.

## 7. Dependencies

A dependency is taken when it does a basic job completely and is maintained, and nothing beyond
that is added. The set is:

| package | used for |
|---|---|
| SDL3-CS | the window, input, gamepads and audio |
| Vortice.Vulkan | the Vulkan API |
| Twizzle.ImGui-Bundle.NET | Dear ImGui |
| StbImageSharp | decoding images |
| AssimpNetter | reading models (glTF, FBX, OBJ and the rest) with their materials and textures |
| BepuPhysics | rigid bodies |
| Microsoft.CodeAnalysis | the source generator, and compiling behaviors while an app runs |
| `slangc` | compiling Slang to SPIR-V, fetched as a tool and not linked |

Scene description formats, material graph languages, embedded browsers, web servers and spatial
audio middleware are left out. Each brings more surface than the engine has users for, and each was
tried in an earlier revision of this repository and kept on the `legacy-modules` branch.

## 8. One project

The engine is one project, `3DEngine/`, with a folder per area (`Core`, `Ecs`, `Behaviors`,
`Platform`, `Graphics`, `Rendering`, `Gui`, `Assets`, `Scenes`, `Physics`, `Api`). Beside it are the
generator, the tests and the examples. A folder is a namespace's worth of code, and there are no
module repositories, so a change that touches the ECS and the renderer is one commit.

## 9. The cheatsheet

`.github/CHEATSHEET.md` lists every public `Engine3D` function on one line, grouped by area, with a
comment saying what it does, in the form of the raylib cheatsheet. A function that is added, renamed
or removed changes the cheatsheet in the same commit, so the sheet is always the API.

## Order

1. The flat project, with the dropped modules removed and everything else building and tested.
2. Slang through `slangc` in place of GLSL, with a cache so a shipped game needs no compiler.
3. `App.Startup`, `App.Frame` and `App.Shutdown`, then `Engine3D.Window`, `Input` and `Drawing`, with
   ImGui inside the frame.
4. The draw list and its pass, then `Shapes3D`, cameras and the first examples.
5. `Textures`, `Models` through Assimp, `Shaders`, then `Text` and `Shapes`.
6. `Audio`, gamepads and render targets.
7. The cheatsheet as each area lands, and the examples beside it.
