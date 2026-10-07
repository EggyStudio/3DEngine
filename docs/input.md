# Input

Input is read by asking, each frame, what the keyboard, the mouse, the fingers on a touch screen
and the gamepads are doing. Every answer is about the current frame, so a program reads input
where it updates, before it draws, and needs no events or callbacks.

## Keys

A key is held, went down this frame, or came up this frame, and each has its own question:

| Call | True |
|---|---|
| `IsKeyDown(key)` | While the key is held, as moving needs |
| `IsKeyPressed(key)` | On the one frame the key went down, as jumping or opening a menu needs |
| `IsKeyReleased(key)` | On the one frame it came up, as letting go of a charged shot needs |
| `IsKeyPressedRepeat(key)` | When the system repeats a held key, as moving through a list does |
| `IsKeyUp(key)` | While it is not held |

From the `core_2d_camera` example, which moves its player while the arrows are held and turns its
camera with A and S:

<!-- compiled with:
Rectangle player = default;
Camera2D camera = default;
-->
```csharp
var step = (IsKeyDown(Key.Right) ? 1 : 0) - (IsKeyDown(Key.Left) ? 1 : 0);
player = player with { X = player.X + step * 240 * GetFrameTime() };

camera.Target = new Vector2(player.X + 20, player.Y + 20);
if (IsKeyDown(Key.A)) camera.Rotation--;
if (IsKeyDown(Key.S)) camera.Rotation++;
```

Keys are named by their place on a US keyboard (`Key.W`, `Key.Space`, `Key.One` for the 1 above
the letters, `Key.LeftShift`), so W, A, S and D are under the same fingers on a French or German
keyboard. The player above moves 240 pixels a second, scaled by `GetFrameTime()`, so it moves at
the same speed at any frame rate. A program typing text reads `GetCharPressed` instead, which the
[Text and fonts](text-and-fonts.md#typed-text) page shows.

## The mouse

`GetMousePosition` gives the pointer's place in the window's pixels, and the mouse buttons are
asked as keys are, with `IsMouseButtonDown`, `IsMouseButtonPressed` and `IsMouseButtonReleased`.
From the `shaders_compute_life` example, where holding the left button draws cells:

<!-- compiled with:
const float CellSize = 8;
-->
```csharp
// Holding the left button brings cells to life under the pointer.
if (IsMouseButtonDown(MouseButton.Left))
{
    var mouse = GetMousePosition() / CellSize;
    // ...
}
```

`GetMouseDelta` gives how far the pointer moved this frame and `GetMouseWheelMove` how far the
wheel turned, which `core_2d_camera` zooms by:

<!-- compiled with:
Camera2D camera = default;
-->
```csharp
camera.Zoom = Math.Clamp(MathF.Exp(MathF.Log(camera.Zoom) + GetMouseWheelMove() * 0.1f), 0.1f, 3f);
```

`DisableCursor` hides the pointer and holds it to the window, so the mouse turns a camera without
the pointer leaving, and `EnableCursor` gives it back. `HideCursor` hides it without holding it.
In a 2D world seen through a camera, `GetScreenToWorld2D(GetMousePosition(), camera)` gives the
world point under the pointer, and in 3D `GetScreenToWorldRay` gives the ray, which the
[Drawing in 3D and cameras](drawing-3d-and-cameras.md#from-the-world-to-the-screen-and-back) page
shows.

`SetMouseCursor` sets the pointer's shape, as the bar over a text field or a hand over a link:

<!-- compiled with:
Rectangle playButton = default;
-->
```csharp
SetMouseCursor(CheckCollisionPointRec(GetMousePosition(), playButton) ? MouseCursor.PointingHand : MouseCursor.Default);
```

A game drawn into a render texture and scaled to the window, as a pixel-art game letterboxed to
any size is, sets `SetMouseOffset` and `SetMouseScale` so the pointer reads in the texture's pixels.
The offset moves it to the texture's corner, and the scale shrinks the window's pixels to the
texture's.

## Touch and gestures

`GetTouchPointCount` says how many fingers are on the screen and `GetTouchPosition(index)` where
each is. A finger keeps the id `GetTouchPointId` gives while it stays down. On a machine with no
touch screen the mouse stands in, its left button held as one finger, so a game made for touch is
tried with a mouse.

The gestures read from fingers, or from the mouse, are taps, double taps, holds, drags, swipes in
four directions and pinches in and out. `GetGestureDetected` gives this frame's, which the
`core_input_gestures` example lists:

```csharp
var gesture = GetGestureDetected();
// ...
for (int i = 0; i < GetTouchPointCount(); i++)
    DrawCircleV(GetTouchPosition(i), 24, Color.Maroon.Fade(0.6f));

var current = gesture switch
{
    Gesture.Hold => $"Hold, {GetGestureHoldDuration():0.0} s",
    Gesture.Drag => $"Drag, {GetGestureDragAngle():0} degrees",
    _ => gesture.ToString(),
};
```

`IsGestureDetected(Gesture.SwipeLeft)` asks for one gesture, and `SetGesturesEnabled` limits which
are recognized. A drag's distance, a pinch's spread and their angles have calls of their own.

## Gamepads

A gamepad is asked by its index, from 0 in the order the pads connected. `IsGamepadAvailable` says
whether one is there. Buttons are named by their place on the pad, as raylib names them, the left
face the directional pad and the right face the four buttons (`RightFaceDown`, `RightFaceRight`,
`RightFaceLeft`, `RightFaceUp`), rather than by the letter printed on them, so `RightFaceDown` is A
on an Xbox pad and the cross on a PlayStation one. The `core_input_gamepad` example draws a pad's
state:

```csharp
if (!IsGamepadAvailable(0))
{
    DrawText("Connect a gamepad, or drive one with: e3d command input.button 0 RightFaceDown 30", 40, 200, 20, Color.Gray);
    EndDrawing();
    continue;
}
// ...
private static void Button(Vector2 at, GamepadButton button, Color lit)
{
    DrawCircleV(at, 18, IsGamepadButtonDown(0, button) ? lit : Color.LightGray);
    DrawCircleLines((int)at.X, (int)at.Y, 18, Color.Gray);
}

private static void Stick(Vector2 at, GamepadAxis x, GamepadAxis y)
{
    DrawCircleLines((int)at.X, (int)at.Y, 40, Color.Gray);
    DrawCircleV(at + new Vector2(GetGamepadAxisMovement(0, x), GetGamepadAxisMovement(0, y)) * 30, 12, Color.Maroon);
}
```

`GetGamepadAxisMovement` gives a stick from -1 to 1 along each axis, down being positive, and a
trigger from 0 to 1. A stick rests a little off its middle, so a game ignores small values, as
`if (MathF.Abs(x) < 0.15f) x = 0;` does.

`SetGamepadVibration` rumbles a pad for some seconds. A pad with a gyro, an accelerometer, a
touchpad or a light bar has calls for each, which the cheatsheet lists.

## Bindings a player changes

A game that lets its player choose their keys keeps each action's key and button in a table of
its own and asks it rather than naming keys in its loop. `GetKeyPressed` gives
the next key pressed and `GetGamepadButtonPressed` the button pressed last while it is held, so a
settings screen waiting for one binds whatever comes, and `SaveFileText` and `LoadFileText` keep the table beside the program
between runs. `games/Manor` does this, from its `Settings` class:

<!-- compiled with:
public enum Action { Jump, Interact }
Action? waitingFor = null;
Dictionary<Action, Key> keys = [];
Dictionary<Action, GamepadButton> buttons = [];
-->
```csharp
if (waitingFor is { } action && GetKeyPressed() is var key && key != Key.Null)
{
    keys[action] = key;
    SaveFileText("manor-settings.txt", string.Join("\n", keys.Select(k => $"key.{k.Key} = {k.Value}")));
}
// ...
bool Down(Action action) => IsKeyDown(keys[action]) || IsGamepadButtonDown(0, buttons[action]);
```

`FileExists` before `LoadFileText` reads a file that may not be there yet, as on a first run,
without the warning a missing file gives.

## Dropped files

A file dragged from the desktop onto the window is kept by the engine until the program takes it,
so a drop is not lost to a frame that did not ask. `LoadDroppedFiles` gives the paths in the order
they arrived and `UnloadDroppedFiles` forgets them. From the `core_drop_files` example:

<!-- compiled with:
List<string> files = [];
-->
```csharp
if (IsFileDropped())
{
    files.AddRange(LoadDroppedFiles());
    UnloadDroppedFiles();
}
```

A level editor drawn in ImGui loads a dropped scene file this way, and a model viewer a model.

## Input and ImGui

ImGui reads the same keyboard and mouse. `UpdateCamera` leaves the camera still while the mouse is
over an ImGui window or text is typed into a field, and a program reading input of its own asks
ImGui the same way, through `ImGuiNET`, before taking a click for the game:

<!-- compiled with:
void Shoot() { }
-->
```csharp
if (IsMouseButtonPressed(MouseButton.Left) && !ImGui.GetIO().WantCaptureMouse) Shoot();
```

ImGui reads the first gamepad too, so a menu drawn in ImGui is played with the pad alone. The
d-pad or the left stick moves between the focused window's widgets, starting on its first, the
bottom face button presses the one it is on, and the right face button backs out of a field or a
popup. `ImGui.SetNextWindowFocus()` before a menu's window gives it the pad. Arrow keys, Enter and
Escape do the same from a keyboard.

## Input in the ECS

A behavior reads the same input through its context, `ctx.Input`, as the `ecs_behaviors` example
does to spawn more balls:

<!-- compiled with:
BehaviorContext ctx = null!;
void Spawn(BehaviorContext context, int count) { }
-->
```csharp
if (ctx.Input.KeyPressed(Key.Space)) Spawn(ctx, 100);
```

## Driving input from outside

`./e3d command input.key W 30` holds W for thirty frames in a running program, and the other
`input.*` commands click, drag, type, touch and press gamepad buttons, which is how a program is
tested without a person at it. The `core_input_gamepad` example is captured that way, with a pad
that is not there. The [Driving a program with e3d](driving-with-e3d.md) page covers it.

## Recording and playing input

A program records its own input with raylib's automation events, a demo played back or a bug
written down as the player made it. Each frame's input is recorded as `EndDrawing` begins, a key
held being an event in every frame it is held and another when it comes up, and the pointer, the
wheel, a finger and a gamepad's axes an event when they move:

```csharp
AutomationEventList events = LoadAutomationEventList(null);    // empty, to record into
SetAutomationEventList(events);
SetAutomationEventBaseFrame(0);
StartAutomationEventRecording();
// ... frames played
StopAutomationEventRecording();
ExportAutomationEventList(events, "run.rae");
```

`PlayAutomationEvent` sets what an event records as if it had happened in this frame, so a program
plays a frame's events before it reads its input, each event's `Frame` saying which frame it
belongs to. A key played down stays down until its event of coming up is played, as a held key
does. The `core_automation_events` example records a run of its platformer with S and plays it
back with A. Keys are written by the engine's own codes, the `Key` a program reads, so a file
recorded by raylib's own program plays its frames and types here but not its keys.

## See also

- Examples: [`core_input_gamepad`](../3DEngine.Examples/Core/CoreInputGamepad.cs),
  [`core_input_gestures`](../3DEngine.Examples/Core/CoreInputGestures.cs),
  [`core_2d_camera`](../3DEngine.Examples/Core/Core2DCamera.cs),
  [`core_drop_files`](../3DEngine.Examples/Core/CoreDropFiles.cs),
  [`core_automation_events`](../3DEngine.Examples/Core/CoreAutomationEvents.cs),
  [`text_input_box`](../3DEngine.Examples/Text/TextInputBox.cs)
- The game [`games/Sumo`](../games/Sumo/Program.cs), two players on one keyboard or a gamepad each,
  and [`games/Wordfall`](../games/Wordfall/Program.cs), played by typing, with words from a file
  dropped on its window
- The cheatsheet's [Input](../CHEATSHEET.md#input)
- Previous: [Audio](audio.md)
- Next: [Physics](physics.md)
