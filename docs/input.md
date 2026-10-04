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

```csharp
var step = (IsKeyDown(Key.Right) ? 1 : 0) - (IsKeyDown(Key.Left) ? 1 : 0);
player = player with { X = player.X + step * 240 * GetFrameTime() };

camera.Target = new Vector2(player.X + 20, player.Y + 20);
if (IsKeyDown(Key.A)) camera.Rotation--;
if (IsKeyDown(Key.S)) camera.Rotation++;
```

Keys are named by their place on a US keyboard (`Key.W`, `Key.Space`, `Key.Alpha1` for the 1 above
the letters, `Key.LShift`), so W, A, S and D are under the same fingers on a French or German
keyboard. The player above moves 240 pixels a second, scaled by `GetFrameTime()`, so it moves at
the same speed at any frame rate. A program typing text reads `GetCharPressed` instead, which the
[Text and fonts](text-and-fonts.md#typed-text) page shows.

## The mouse

`GetMousePosition` gives the pointer's place in the window's pixels, and the mouse buttons are
asked as keys are, with `IsMouseButtonDown`, `IsMouseButtonPressed` and `IsMouseButtonReleased`.
From the `shaders_compute_life` example, where holding the left button draws cells:

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

```csharp
camera.Zoom = Math.Clamp(MathF.Exp(MathF.Log(camera.Zoom) + GetMouseWheelMove() * 0.1f), 0.1f, 3f);
```

`DisableCursor` hides the pointer and holds it to the window, so the mouse turns a camera without
the pointer leaving, and `EnableCursor` gives it back. `HideCursor` hides it without holding it.
In a 2D world seen through a camera, `GetScreenToWorld2D(GetMousePosition(), camera)` gives the
world point under the pointer, and in 3D `GetScreenToWorldRay` gives the ray, which the
[Drawing in 3D and cameras](drawing-3d-and-cameras.md#from-the-world-to-the-screen-and-back) page
shows.

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
whether one is there. Buttons are named by their place on the pad (`South`, `East`, `West`,
`North`) rather than by the letter printed on them, so `South` is A on an Xbox pad and the cross on
a PlayStation one. The `core_input_gamepad` example draws a pad's state:

```csharp
if (!IsGamepadAvailable(0))
{
    DrawText("Connect a gamepad, or drive one with: e3d command input.button 0 South 30", 40, 200, 20, Color.Gray);
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

## Input and ImGui

ImGui reads the same keyboard and mouse. `UpdateCamera` leaves the camera still while the mouse is
over an ImGui window or text is typed into a field, and a program reading input of its own asks
ImGui the same way, through `ImGuiNET`, before taking a click for the game:

```csharp
if (IsMouseButtonPressed(MouseButton.Left) && !ImGui.GetIO().WantCaptureMouse) Shoot();
```

## Input in the ECS

A behavior reads the same input through its context, `ctx.Input`, as the `ecs_behaviors` example
does to spawn more balls:

```csharp
if (ctx.Input.KeyPressed(Key.Space)) Spawn(ctx, 100);
```

## Driving input from outside

`./e3d command input.key W 30` holds W for thirty frames in a running program, and the other
`input.*` commands click, drag, type, touch and press gamepad buttons, which is how a program is
tested without a person at it. The `core_input_gamepad` example is captured that way, with a pad
that is not there. The `./e3d` page of this guide covers it.

## See also

- Examples: [`core_input_gamepad`](../3DEngine.Examples/Core/CoreInputGamepad.cs),
  [`core_input_gestures`](../3DEngine.Examples/Core/CoreInputGestures.cs),
  [`core_2d_camera`](../3DEngine.Examples/Core/Core2DCamera.cs),
  [`text_input_box`](../3DEngine.Examples/Text/TextInputBox.cs)
- The cheatsheet's [Input](../CHEATSHEET.md#input)
- Previous: [Audio](audio.md)
