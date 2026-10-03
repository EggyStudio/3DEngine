# Design

How 3DEngine is meant to be used, and the rules its public surface follows. The model is raylib,
where a program opens a window, loops over frames, draws in each one with plain function calls and closes
the window, and every function it can call fits on one cheatsheet. Dear ImGui is used the same way,
inside the same frame. The ECS and the behaviors underneath are available to a program that grows
into them, and a program that does not use them never sees them.

The engine is C# on .NET 10, with SDL3 for the window, input and audio, Vulkan for drawing, Dear
ImGui for interfaces and Slang for shaders. It runs on Linux, Windows and macOS (through MoltenVK).

## What exists

- **One project.** The engine is `3DEngine/`, compiled into `3DEngine.dll` under the `Engine`
  namespace, with the source generator as a separate analyzer assembly.
- **A sparse-set ECS** (`EcsWorld`) with deferred commands, change bits and queries over up to three
  components, and a resource map (`World`) beside it.
- **A staged schedule** (`Startup`, `First`, `PreUpdate`, `FixedUpdate`, `Update`, `PostUpdate`,
  `Render`, `Last`, `Cleanup`) that runs systems in parallel batches by their declared reads and
  writes.
- **Behaviors**, which are `[Behavior]` structs whose stage methods a Roslyn generator turns into
  systems, with filters, run conditions and toggle keys.
- **An SDL3 window** with keyboard and mouse input, and **a Vulkan device** over Vortice.Vulkan with
  a render graph that draws meshes, the immediate draw list and ImGui, with shaders in Slang.
- **The flat API** for the window, timing, input, the frame, cameras, 2D and 3D shapes and text,
  listed in [CHEATSHEET.md](CHEATSHEET.md), with examples in `3DEngine.Examples`.

Every area of the flat API has a first version. What each lacks is in [TODO.md](TODO.md).

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
| `Engine3D.Window.cs` | the window and frame timing (the monitor is not covered) |
| `Engine3D.Input.cs` | keyboard, mouse and gamepads |
| `Engine3D.Drawing.cs` | the frame, cameras and render targets |
| `Engine3D.Shapes.cs` | 2D shapes |
| `Engine3D.Shapes3D.cs` | 3D shapes and the grid |
| `Engine3D.Text.cs`, `Engine3D.Fonts.cs` | text and fonts |
| `Engine3D.Textures.cs` | images and textures |
| `Engine3D.Models.cs` | meshes, models and materials |
| `Engine3D.Shaders.cs` | Slang shaders and their parameters |
| `Engine3D.Audio.cs` | sounds and music |

Arguments are plain values (`Vector3`, `Color`, `Rectangle`, `Camera3D`), and resources are small
structs holding an id, so nothing in the API needs a class hierarchy to be understood.

## 2. The frame

`InitWindow` builds an `App` with the default plugins. The loop the program writes then drives
the app one frame at a time, through `App.BeginFrame` and `App.EndFrame`:

| call | what runs |
|---|---|
| `WindowShouldClose` | processes the window's events, then reports whether the window or the exit key (Escape) asked to close |
| `BeginDrawing` | `Startup` on the first frame, then `First`, `PreUpdate` and `Update`, which advance time, start the ImGui frame and run the game's systems |
| `EndDrawing` | `PostUpdate`, `Render` and `Last`, which apply deferred commands, render what was recorded and present, then waits for the target frame time |
| `CloseWindow` | `Cleanup`, then disposes the app |

So the frame a raylib-style loop sees and the frame an ECS system sees are the same frame, and a
program can mix the two. `Startup` waits for the first frame, so plugins and behaviors added between
`InitWindow` and the loop take part in it. `App.Run()` drives the same steps for a program that
hands the loop to the engine.

## 3. Immediate drawing

A `Draw` call records something into the frame's draw list and returns. Nothing it records
outlives the frame, so a cube drawn in one frame and not the next is gone. Shapes are batched into
lines and triangles of position and color, in the manner of raylib's rlgl layer, and drawn by one
pass after the meshes the ECS holds and before ImGui.

`BeginMode3D(camera)` sets the view the following calls draw through, with depth testing, and
`EndMode3D` returns to screen space, in pixels from the top left corner, for 2D shapes and text.
Text is glyphs from a font atlas drawn as textured quads in the same list. A `Camera3D` is a plain struct the program keeps and updates
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

`GetApp()` returns the app `InitWindow` built. A program reaches the ECS through it, and anything
registered there runs inside the frames the loop drives:

```csharp
InitWindow(1280, 720, "Spinning");
var ecs = GetApp().World.Resource<EcsWorld>();

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

The flat functions work inside systems too, when the app is the one `InitWindow` built, so an
`[OnRender]` method can call `DrawCube`. A game that outgrows the loop moves its logic into
behaviors one piece at a time, and nothing in the flat API has to be unlearned. The `ecs_behaviors`
example moves balls in a behavior and draws them from the loop.

## 6. Resources the program owns

`Load` returns a resource and `Unload` frees it, and the program decides when. No `Load` function
exists yet, and this section is the rule they follow. There is no
reference counting and no garbage collection of GPU memory in the flat API, since raylib's
experience is that a pair of calls is understood by everyone and leaks are found by the log, which
reports what was still loaded at `CloseWindow`. The asset server under the ECS keeps its own
handles with hot reload for the systems that use it.

## 7. No editor

There is no editor application. A program is code, as it is with raylib, and a scene is built by
the calls that make it or loaded from a file another tool wrote. Tools a game needs inside itself,
such as a debug panel, a level tweaker or an entity list, are ImGui windows the game draws in its
own frame, so they cost one function each and ship only if the game keeps them.

## 8. Dependencies

A dependency is taken when it does a basic job completely and is maintained, and nothing beyond
that is added. The set is:

| package | used for |
|---|---|
| SDL3-CS | the window, input, gamepads and audio |
| Vortice.Vulkan | the Vulkan API |
| Twizzle.ImGui-Bundle.NET | Dear ImGui |
| StbImageSharp | decoding images |
| AssimpNetter | reading models (glTF, FBX, OBJ and the rest) with their materials and textures |
| NVorbis | decoding Ogg Vorbis, in managed code |
| BepuPhysics | rigid bodies |
| Microsoft.CodeAnalysis | the source generator, and compiling behaviors while an app runs |
| `slangc` | compiling Slang to SPIR-V, fetched as a tool and not linked |

Scene description formats, material graph languages, embedded browsers, web servers, spatial
audio middleware and an editor are left out. Each brings more surface than the engine has users
for, and each was tried in an earlier revision of this repository and kept on the `legacy-modules`
branch.

## 9. One project

The engine is one project, `3DEngine/`, with a folder per area (`Core`, `Ecs`, `Behaviors`,
`Components`, `Platform`, `Graphics`, `Rendering`, `Gui`, `Assets`, `Scenes`, `Physics`, and `Api`
for the flat functions). Beside it are the generator, the tests and the examples. A folder is a
namespace's worth of code, and there are no module repositories, so a change that touches the ECS
and the renderer is one commit.

## 10. The cheatsheet

`.github/CHEATSHEET.md` lists every public `Engine3D` function on one line, grouped by area, with a
comment saying what it does, in the form of the raylib cheatsheet. A function that is added, renamed
or removed changes the cheatsheet in the same commit, so the sheet is always the API.

## Order

1. Shaders for models, with parameters by name from Slang's reflection.
2. Mipmaps and image editing (`ImageResize`, `ImageDraw*`).
3. More mesh generators and `DrawModelWires`.

Each lands with its lines in the cheatsheet and an example beside it.
