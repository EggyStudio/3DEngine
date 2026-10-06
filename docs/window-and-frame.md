# The window and the frame

A program opens a window, draws a frame at a time until the window is asked to close, and closes
it. Everything else in the engine happens inside those frames, so this page is where every
program starts.

## A window

`InitWindow` opens a window and builds the app behind it, `SetTargetFPS` caps how many frames a
second it draws, and `CloseWindow` frees what the app holds. A program imports the flat API once,
with `using static Engine.Engine3D;`, and calls each function by name:

```csharp
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

`dotnet run --project 3DEngine.Examples core_basic_window` runs it.

## The loop

Each pass through the loop is one frame, and three calls mark its parts.

| Call | What happens |
|---|---|
| `WindowShouldClose` | The window's events are read, and it answers true once the window's close button or the exit key (Escape, changed with `SetExitKey`) asked to close |
| `BeginDrawing` | Time advances, input is read and the ImGui frame starts, so the game's own systems see this frame |
| `EndDrawing` | What was drawn is rendered and presented, and the program waits for the frame rate it asked for |

Between `BeginDrawing` and `EndDrawing` every `Draw` call records something for this frame and
returns at once. Nothing drawn outlives the frame, so a shape drawn in one frame and not the next
is gone, and a program draws its whole picture every frame.

`ClearBackground` sets the color the frame starts from. Called anywhere in the frame it clears the
whole of it, before anything is drawn.

## Time

A frame's length changes with the machine and with what the frame draws, so movement is scaled by
how long the last frame took.

```csharp
var x = 0f;
while (!WindowShouldClose())
{
    x += 120 * GetFrameTime();   // 120 pixels a second, at any frame rate
    BeginDrawing();
    ClearBackground(Color.RayWhite);
    DrawCircle((int)x % GetScreenWidth(), 225, 20, Color.Maroon);
    DrawFPS(10, 10);
    EndDrawing();
}
```

| Function | Gives |
|---|---|
| `GetFrameTime()` | Seconds the last frame took |
| `GetTime()` | Seconds since the window opened, read from the clock as it is called |
| `GetFPS()` | Frames a second, smoothed |
| `SetTargetFPS(fps)` | Caps the frame rate, 0 for none |

`SetConfigFlags(ConfigFlags.VsyncHint)` before `InitWindow` waits for the display instead, and
`SetWindowState(ConfigFlags.VsyncHint)` or `ClearWindowState` changes it while the window is open,
from the next frame, as a settings screen does.

## The window's state

The window can be resized, made fullscreen, moved and asked about. `SetConfigFlags` asks the next
window for what it is opened with, and the rest work on the open one. The `core_window_toggles`
example reads and changes most of it:

```csharp
// Asked of the window before it opens, as raylib's flags are.
SetConfigFlags(ConfigFlags.WindowResizable | ConfigFlags.VsyncHint | ConfigFlags.Msaa4xHint);
InitWindow(800, 450, "[core] window toggles");
SetWindowMinSize(320, 240);
var resizes = 0;
SetTargetFPS(60);

while (!WindowShouldClose())
{
    if (IsKeyPressed(Key.F)) ToggleFullscreen();
    if (IsKeyPressed(Key.R)) SetWindowSize(GetScreenWidth() == 800 ? 640 : 800, GetScreenHeight() == 450 ? 360 : 450);
    if (IsKeyPressed(Key.M)) MaximizeWindow();
    if (IsKeyPressed(Key.N)) RestoreWindow();
    if (IsKeyPressed(Key.C)) SetClipboardText($"{GetScreenWidth()}x{GetScreenHeight()}");
    // Flags turned on and off while the window is open, as raylib's example does.
    foreach (var (key, flag) in new[] { (Key.T, ConfigFlags.WindowTopmost), (Key.U, ConfigFlags.WindowUndecorated) })
        if (IsKeyPressed(key))
        {
            if (IsWindowState(flag)) ClearWindowState(flag);
            else SetWindowState(flag);
        }
    if (IsWindowResized()) resizes++;
    // ...
}
```

`SetWindowState` and `ClearWindowState` turn the same flags on and off for the open window, and
`IsWindowState` asks whether it has them, except MSAA, which is chosen as it opens. Vsync changes
as well, making the swapchain again with the next frame. `SetWindowSize` resizes the window, or in
an offscreen run the images it draws into, which is how a settings screen's resolution is applied.
`SetWindowIcon` gives the window an icon from an image, `SetWindowOpacity` makes it see-through,
and `ToggleBorderlessWindowed` covers the monitor with no border and no change of display mode,
which most games offer as their fullscreen.

`GetScreenWidth` and `GetScreenHeight` give the window's size, which a program reads each frame
rather than keeping, since a resizable window changes it. On a monitor that doubles its pixels, with
`ConfigFlags.WindowHighdpi` asked, the content is drawn at `GetRenderWidth` by `GetRenderHeight`,
twice as many, which `GetWindowScaleDPI` says, while shapes, text and the mouse keep the window's
units. Without it, on Wayland and macOS, the window is drawn at its size and the desktop scales the
picture up, as raylib's is. `core_highdpi_demo` sets the two grids side by side.
`ConfigFlags.WindowTransparent` lets the desktop show through where the frame is cleared to
`Color.Blank`, where the desktop composites windows, and `ConfigFlags.WindowUnfocused` leaves the
keyboard focus where it is as the window is shown. A minimized window draws nothing, and the
program's loop goes on as it was, with `ConfigFlags.WindowAlwaysRun` or without it, where raylib's
waits without it, so a game paused by the player's minimizing it pauses itself by
`IsWindowMinimized`. When the window comes back its frames are drawn at the size it comes back at.

A tool that changes only when it is used, as a level editor drawn in ImGui, calls
`EnableEventWaiting`, after which `WindowShouldClose` waits for input or a window event before the
frame goes on, up to a tenth of a second, and the CPU rests in between. The monitors are counted and
measured by `GetMonitorCount`, `GetMonitorWidth`, `GetMonitorRefreshRate` and the rest the
[cheatsheet](../CHEATSHEET.md#window-and-timing) lists.

## ImGui in the same frame

Dear ImGui works anywhere between `BeginDrawing` and `EndDrawing`, with no setup, and is drawn over
everything else. Tools a game needs while it is made (a level editor, a debug panel, a profile)
are windows the program draws in its own frame. From the `gui_imgui_window` example:

```csharp
ImGui.SetNextWindowSize(new Vector2(280, 0), ImGuiCond.FirstUseEver);
ImGui.Begin("Cube");
ImGui.SliderFloat("Size", ref size, 0.5f, 5f);
ImGui.ColorEdit3("Color", ref color);
ImGui.Checkbox("Wires", ref wires);
ImGui.Text($"{GetFPS()} FPS");
ImGui.End();
```

`DrawProfileWindow()` draws where each frame's time goes in a window of its own.

## Without a window on the screen

Every program built on the engine takes the same flags, so one can run where nothing is shown:

| Flag | Runs |
|---|---|
| `--hidden` | Rendering into a window that is never shown |
| `--offscreen` | Rendering with no window and no display, as on a server or in CI |
| `--headless` | With no window and no GPU, the logic and the ECS only |
| `--frames N` | For N frames, then closing |
| `--frame-time S` | With each frame S seconds long, whatever the machine takes, so a run repeats frame for frame |

`TakeScreenshot("frame.png")` writes the frame being drawn to a file once it is presented. The
`e3d` tool in the repository drives a running program from the terminal, which is how the
examples' pictures are taken.

## The app underneath

`InitWindow` builds an `App`, and `BeginDrawing` and `EndDrawing` run its stages. `GetApp()` hands
it over, for the ECS, plugins and resources, which run inside the same frames as the loop.

## See also

- Examples: [`core_basic_window`](../3DEngine.Examples/Core/CoreBasicWindow.cs),
  [`core_window_toggles`](../3DEngine.Examples/Core/CoreWindowToggles.cs),
  [`gui_imgui_window`](../3DEngine.Examples/Gui/GuiImGuiWindow.cs)
- The cheatsheet's [Window and timing](../CHEATSHEET.md#window-and-timing) and
  [Frame and cameras](../CHEATSHEET.md#frame-and-cameras)
- Next: [Drawing in 2D](drawing-2d.md)
