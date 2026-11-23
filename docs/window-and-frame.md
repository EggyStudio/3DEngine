# The window and the frame

A program opens a window, draws a frame at a time until the window is asked to close, and closes
it. Everything else in the engine happens inside those frames, so this page is where every
program starts.

## A window

`InitWindow` opens a window and builds the app behind it, `SetTargetFPS` caps how many frames a
second it draws, and `CloseWindow` frees what the app holds. A program imports the flat API once,
with `using static Engine.E3D;`, and calls each function by name:

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
| `WindowShouldClose` | The window's events are read, and it answers true once the window's close button, the system's own shortcut or an exit key the program set with `SetExitKey` asked to close |
| `BeginDrawing` | Time advances, input is read and the ImGui frame starts, so the game's own systems see this frame |
| `EndDrawing` | What was drawn is rendered and presented, and the program waits for the frame rate it asked for |

Between `BeginDrawing` and `EndDrawing` every `Draw` call records something for this frame and
returns at once. Nothing drawn outlives the frame, so a shape drawn in one frame and not the next
is gone, and a program draws its whole picture every frame.

`ClearBackground` sets the color the frame starts from. Called anywhere in the frame it clears the
whole of it, before anything is drawn.

A window has no exit key until the program sets one, and the key goes with the window as it
closes. Where one is set, the key is handed down one order and the first taker keeps it. The
console or the settings panel open takes it, a field of an ImGui window with the caret or an ImGui
popup open takes it, and then a menu of the game's own, which says it holds the keyboard with
`HoldExitKeyWhile(() => menuOpen)`. The window sees the key only where none of them does, and
`IsExitKeyHeld` says which way it goes. The debug overlay takes no input, so with it alone shown
the key passes on. The examples program sets Escape, so Escape goes back a page in the settings
panel, and with nothing open but the overlay it leaves the example, back to the list where the
example was picked from there.

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
A window dragged larger is drawn again at its new size once it has kept that size for 0.15 s of
frames, and one sized before the first frame draws that frame at the new size, so a game applying
its saved resolution right after `InitWindow` shows its loading screen at it. `SetWindowIcon` gives the window an icon from an image, `SetWindowOpacity` makes it see-through,
and `ToggleBorderlessWindowed` makes the window one with no frame as large as the monitor, which
covers it at the monitor's resolution with no change of display mode, as most games offer their
fullscreen, and gives the window back its size as it toggles off. `ToggleFullscreen` takes the
monitor's own mode, or the one `SetWindowFullscreenMode` chose, and both go as the `window.mode`
setting goes, so `IsWindowFullscreen` and the setting read the same mode.

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

<!-- compiled with:
float size = 2;
Vector3 color = Vector3.One;
bool wires = false;
-->
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
`SetProfileValue` adds a number of the program's own, which the window and `e3d command profile`
show as it was last set, or as the mean of the values set over a window of seconds, so a count that
leaps from frame to frame reads steady:

<!-- compiled with:
int alive = 0;
-->
```csharp
SetProfileValue("particles.alive", alive, 1);
```

A texture of the program's is drawn in an ImGui window by the id `GetImGuiTextureId` gives it,
which `ImGui.Image`, `ImGui.ImageButton` and a draw list's `AddImage` take, and a part of it by its
coordinates, as an inventory draws each item's icon from one atlas, with no rectangles of its own:

<!-- compiled with:
Texture2D icons = default;
int slot = 0;
-->
```csharp
var cell = new Vector2(slot % 8, slot / 8) / 8;
ImGui.Image(GetImGuiTextureId(icons), new Vector2(32, 32), cell, cell + new Vector2(1f / 8));
```

ImGui keeps where its windows were moved, docked and sized in `imgui.ini` beside the program, so a
player finds a layout as they left it. A run that shows no window, hidden, offscreen or headless, as
`./e3d` and a test open one, neither reads nor writes the file, and starts from the program's own
layout each time.

An ImGui window dragged outside the game's window is given a window of its own on the desktop,
with ImGui's viewports turned on, which they are not until the program sets ImGui's flag:

```csharp
ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
```

Each such window is drawn in the same frame as the game's and shown with it. ImGui then measures
its positions from the desktop's corner rather than the window's, so a window placed by position
adds `ImGui.GetMainViewport().Pos`. They are offered on X11, Windows and macOS. Wayland lets no
program place its windows, so there ImGui's windows stay inside the game's.

## Without a window on the screen

Every program built on the engine takes the same flags, so one can run where nothing is shown.

| Flag | Runs |
|---|---|
| `--hidden` | Rendering into a window that is never shown |
| `--offscreen` | Rendering with no window and no display, as on a server or in CI |
| `--headless` | With no window and no GPU, the logic and the ECS only |
| `--frames N` | For N frames, then closing |
| `--frame-time S` | With each frame S seconds long, whatever the machine takes, so a run repeats frame for frame |

`TakeScreenshot("frame.png")` writes the frame being drawn to a file once it is presented, and
`LoadImageFromScreen()` gives the last frame presented as an image, which `core_screen_recording`
writes into an animated GIF every few frames. Frames are kept for it from its first call on, so the
first call gives the window in its clear color. The `e3d` tool in the repository controls a running
program from the terminal, which is how the examples' pictures are taken.

A run is recorded as an animation, a GIF or an animated PNG by the file's extension, a picture
every so many milliseconds of the app's clock until it is stopped:

<!-- compiled with:
var done = false;
-->
```csharp
StartRecording("walk.gif", 250);     // a picture every quarter of a second
// ...
if (done) StopRecording();
```

Each picture is the frame `TakeScreenshot` would write, and plays for as long as it was apart from
the next, so a step of a second plays at a second, and a third argument keeps each picture smaller,
`0.5` at half the window's size. The file is written once the last picture is back from the GPU,
under its own name only when whole, with a line in the log saying where. The GIF holds one palette
of 255 colors cut over every frame, so a capture of a few flat colors comes back exactly, and each
frame after the first only the part that changed; the animated PNG keeps every color, which GitHub's
pages play as they play a GIF. The `record` command does the same from outside for a length:
`./e3d command record walk.gif 5s every 250ms` takes twenty pictures, `record.status` says when the
file is written, and `record.stop` ends one sooner.

## The debug keys

Every program answers the Diagnostics module's five keys, Minecraft's F1, F2 and F3, F4 opening the
console and F5 the settings panel, each a setting a player rebinds from the console, as
`keys.console F6`, and the first three moved by a call too or turned off with `Key.Null`. A key is
taken as it is let go where no other key went down while it was held, so a program's own chords on
them stand, F3 with F4 among them, and the program sees each press as it sees any key, so a program
that takes one for itself moves it. A key of one character is not taken while a field of an ImGui
window has the caret. While the console or the settings panel is open the program sees no input at
all, these keys among it, which still act, as the input guide says.

F1 hides the program's interface, what it draws between `BeginUi()` and `EndUi()`, so the world is
seen alone, as Minecraft hides its HUD. While it is hidden the engine drops every 2D shape and text
drawn inside the scope, and `BeginUi()` answers false, so the program's ImGui windows, which ImGui
draws whatever a frame submits, go inside an `if` on it:

<!-- compiled with:
int health = 100;
-->
```csharp
if (BeginUi())
{
    ImGui.Begin("Inventory");
    ImGui.End();
}
DrawText($"Health {health}", 10, 10, 20, Color.White);    // dropped while hidden
EndUi();
```

A pause screen, a menu or a map drawn outside the scope stays whatever F1 says, as Minecraft's
screens do, and the world and the debug overlay draw on. `IsUiHidden()` says whether it is hidden,
and `SetUiHiddenKey` moves the key.

F2 let go writes a screenshot, the frame being drawn as a PNG in a `screenshots` folder beside the
program, made on first use, named by the time as `2026-10-21_03.45.12.png`, a second in the same
second `_2` and a third `_3`, with a line in the log saying where. Held past half a second it
records in place of the screenshot, fifteen pictures a second of the program's clock as PNGs
numbered from `0001.png` in a folder named by the time beside the screenshots, until it is let go or
the recording holds a hundred and fifty, ten seconds. `SetScreenshotKey(Key.F12)` moves it and
`SetScreenshotKey(Key.Null)` turns it off. `SetScreenshotFolder(folder, written)` points both
elsewhere and is called with each screenshot's path once it is written and each recording's folder
once its last picture is, so a program names and notes its pictures as they land.

F3 shows the engine's debug overlay over the frame, as Minecraft's debug screen, two columns of
lines in the engine's font, each on a dark half-clear backing, with no window, header or border,
drawn after everything else the frame draws in 2D and outside the interface's scope, so F1 leaves
it. The head says the frame rate, the last frame's time, the average and the one percent low, the
rate the slowest hundredth of the frames came at, which a game that stutters shows where its average
does not, and the engine's version and build. The left column says the camera's place, its facing in
Minecraft's words, north being minus Z, and its field of view, and the entities, meshes, textures
and assets the program holds. The right column says the runtime, the OS, the CPU and its logical
cores, the memory in use of the machine's and the managed heap, the GPU, its driver, its Vulkan
version and its memory in use, the display's size, refresh, scale and vsync, and the renderer's
passes and draw calls, the triangles of the frame's model draws, the lights and those casting
shadows, the shadow maps, global illumination's state and the GPU's time a frame. In a window too
narrow for the two columns side by side, a line of the right one that would meet the left's goes
down under it, the lines after it following, and a line wider than the window breaks after a comma.
The overlay's lines are written again four times a second, so a number stays still long enough to
read. The key is taken as it is let go, where no other key went down while it was held, so a
program's own chords on F3 stand and never open it, and `SetDebugOverlayKey` moves it.

Under the head a strip of the last 240 frames' times stands as the RivaTuner overlay draws its
graph, a bar a frame, green to 60 frames a second, yellow to 30 and red past it, with faint rows at
the two, and beside it the GPU's time a frame. Every frame's time is kept whether the overlay shows
or not, so it opens on the last four seconds. Shift held as F3 is let go shows the profiler under
the right column, the render graph's passes the slowest first by their GPU and CPU times over the
last second, and the overlay with it, as Minecraft's Shift+F3 does, and F3 alone hiding the overlay
hides the profiler too.

A program adds sections of its own to the left column, under the engine's lines in the order added,
each a heading and its lines, asked four times a second while the overlay shows, as a world
generator's seed and the chunk the player stands in:

<!-- compiled with:
int seed = 42;
var chunk = (X: 3, Z: -1);
-->
```csharp
AddDebugOverlaySection("World", () => [$"Seed {seed}", $"Chunk {chunk.X} {chunk.Z}"]);
// ...
RemoveDebugOverlaySection("World");
```

A section added again under its name takes the place of the first, and one whose lines throw is a
line saying it failed and why where its lines would be.

F4 opens the console, a window across the top of the program's window in the debug overlay's look,
light gray on black at half, whatever the program's ImGui theme, holding every line written, the
lines entered and their answers among them, and a field under them across the window's whole
width where a command or a setting is typed, completed by Tab from the names that begin with what
is typed, which a box joined to the field lists, walked back through by Up and Down, and run by
Enter between the frame's input and its update, as `./e3d command` runs it. A command that answers
frames later writes its answer under the line when it comes, and one that throws writes the stack
of what it threw to the log, as a line `./e3d command` sends does. F5 opens the settings panel, in
the same look on the opaque black of the console's box so it reads over a bright scene, a page for
each first word of the settings' names, steered by the arrows, Enter and Escape, or a pad.

The settings are every value of the engine's a console line or an environment variable sets, each
with its range or its choices, its default, whether it only reads and the variable that overrides
it, a page of them for each first word of their names.

| Page | Settings |
|---|---|
| `ao` | `method` (`Horizons` or `Circular`), `samples`, `filter`, `slope`, `backface`, `boost`, `screen`, `size` (`Half` or `Full`) |
| `gi` | `rays`, `passes`, the parts a reading leaves out, `history`, `filter`, `screen`, `merge`, `again`, `follow`, `glow`, `through`, `albedo`, `specular`, `normals`, `spread`, `points`, with `meshes`, `cascade`, and the view `show` with `show_cascade` |
| `field` | `show`, the cascade of the scene's distance field drawn over the window |
| `window` | `mode` (`Windowed`, `Borderless`, `BorderlessFullscreen` or `Fullscreen`), `scaling` (`Linear`, `Nearest` or `Integer`), `resizable`, `width` and `height` as a window, `monitor`, `vsync`, and `samples`, which only reads |
| `imgui` | `viewports` |
| `time` | `frame`, the seconds a frame moves time by, `E3D_FRAME_TIME` |
| `live` | `reload`, `E3D_LIVE` |
| `log` | `frames`, `ENGINE_LOG_FRAMES` |
| `scenes` | `folder`, `E3D_SCENES` |
| `sky` | `hours`, the hours the day sky has run from its first midnight |
| `run` | how the run was started, each only reading, `frames`, `serve`, `headless`, `offscreen`, `hidden`, `seed`, `shuffle`, `sessions`, `validation` and `slangc` |
| `keys` | the five keys |

A setting's name alone reads it, `gi.merge`, its name and a value sets it, `gi.merge off`, and
`reset gi.merge` puts its default back, in the console, from `./e3d command` or in the panel,
where Left and Right step it. A value an environment variable gives holds for the run and a set is
kept for the file alone. The `run` settings and `window.samples` say how the run was started, by
its command line or its variables, and only read, since no start reads a value set while the
program runs, so a set of one is refused and the tool starting the program gives them with its
flag or its variable. `run.seed` reads the seed the window gave the random generator, the one
`--seed` or `E3D_SEED` names or the clock's seconds where neither does, so a run is repeated with
`--seed`. A set the app cannot apply, as `gi.merge` with global illumination off, `window.vsync`
with no window or `imgui.viewports` where SDL's driver places no window on the desktop, is refused
with the reason, and the value stays as it was.

The `window` page acts on the window at once. `window.mode borderless` makes a window with no frame
of `window.width` by `window.height` in the middle of the screen it is on, with the desktop round
it, and a size as large as the screen or larger covers the screen, so a player on a large screen
sets a smaller size and has a window of exactly that size, one pixel to one pixel. Setting the width
or the height while it is borderless resizes it at once and keeps it in the middle. `window.mode
borderlessfullscreen` always covers the screen with no frame and no change of the screen's own mode,
and draws at `window.width` by `window.height`, copied onto the screen at the end of each frame, so
a player on a large screen draws fewer pixels and still fills it. `window.scaling` says how,
`linear` filtered and soft at any size, `nearest` sharp, even at a whole ratio and uneven otherwise,
and `integer` at the largest whole multiple that fits, each pixel a square of the same size. Each
keeps the frame's shape, as large as fits in the middle with black round it. The program is given
the size drawn at, so `GetScreenWidth`, the cameras and the pointer are in the frame's pixels, and
the interface is drawn into the frame with the rest and scaled with it. At the screen's own size the
frames are drawn at it with no copy. `window.mode fullscreen` takes the screen's mode, and
`window.mode windowed` gives the window back its frame, its size and its place as a window. A mode
the system refuses answers with its reason in the console and keeps the mode before. `window.width`
and `window.height` are that size, kept while the window covers its screen and taken as it leaves
it, so a player sets the size of the window while it is fullscreen. On Wayland the system places
each window itself, so a borderless window stands where it puts it. `window.resizable` says whether
its edges can be dragged, and `window.monitor` moves it, centered, to another screen by its number
from 0, a fullscreen window staying fullscreen there, and only reads where there is one screen.
While the window is hidden, as `--hidden` asks, the whole page only reads and the settings file
keeps the page's lines as they stand, and in a run with no window the window's own settings are
refused with the reason.

A program whose window is shown keeps the settings a player chooses for the machine, the five keys,
the `window` page but `samples`, and `imgui.viewports`, in one file, `settings.txt` in a folder of
the program's name under the system's folder of applications' data,
`~/.config/<program>/settings.txt` on Linux and `%APPDATA%\<program>\settings.txt` on Windows, the
program named as its assembly is. It is read as
the program starts and written as one of them changes and as the program ends. The `window` page's
lines are given to the window before it is shown, its size first and its mode last, so the first
frame is shown in the mode and at the size the player left. It keeps what a player set, through the
console, the panel or the file, and never a value the program set itself, as the vsync or the size
a window is opened with, and a setting a part of the program adds later takes its line
then and is written as it is set. A line is a setting's name, a space and its value as a console
line holds it, as `window.vsync on`, one for each whose value differs from its default, with the
lines of names this run does not know kept as they stood. A run with no window or a window never
shown, as a tool or a test drives, reads and writes no file.

## The app underneath

`InitWindow` builds an `App`, and `BeginDrawing` and `EndDrawing` run its stages. `GetApp()` hands
it over, for the ECS, plugins and resources, which run inside the same frames as the loop.

## See also

- Examples: [`core_basic_window`](../3DEngine.Examples/Core/CoreBasicWindow.cs),
  [`core_window_toggles`](../3DEngine.Examples/Core/CoreWindowToggles.cs),
  [`core_screen_recording`](../3DEngine.Examples/Core/CoreScreenRecording.cs),
  [`gui_imgui_window`](../3DEngine.Examples/Gui/GuiImGuiWindow.cs)
- The game [`games/Wordfall`](../games/Wordfall/Program.cs), which copies its result to the clipboard
  and saves a screenshot
- The cheatsheet's [Window and timing](../CHEATSHEET.md#window-and-timing) and
  [Frame and cameras](../CHEATSHEET.md#frame-and-cameras)
- Previous: [The loop and the ECS](the-loop-and-the-ecs.md)
- Next: [Drawing in 2D](drawing-2d.md)
