using System.Numerics;
using SDL3;

namespace Engine;

public static partial class Engine3D
{
    private static Input Input => Res<Input>();

    // -- Keyboard

    /// <summary>Whether <paramref name="key"/> went down this frame.</summary>
    public static bool IsKeyPressed(Key key) => Input.KeyPressed(key);

    /// <summary>Whether <paramref name="key"/> is held.</summary>
    public static bool IsKeyDown(Key key) => Input.KeyDown(key);

    /// <summary>Whether <paramref name="key"/> came up this frame.</summary>
    public static bool IsKeyReleased(Key key) => Input.KeyReleased(key);

    /// <summary>Whether the system repeated a held <paramref name="key"/> this frame, as it does in a text field. The first press is not a repeat.</summary>
    public static bool IsKeyPressedRepeat(Key key) => Input.KeyPressedRepeat(key);

    /// <summary>Whether <paramref name="key"/> is not held.</summary>
    public static bool IsKeyUp(Key key) => !Input.KeyDown(key);

    /// <summary>
    /// The next key pressed this frame, in the order pressed, or <see cref="Key.Unknown"/> when
    /// none is left. Each call takes one, so a loop calls it until it answers Unknown.
    /// </summary>
    public static Key GetKeyPressed() => Input.TakeKey();

    /// <summary>
    /// The next character typed this frame as a Unicode code point, or 0 when none is left. Each
    /// call takes one, so a text box reads them in a loop. Layout, shift and dead keys are already
    /// applied, as the platform's text input gives them.
    /// </summary>
    public static int GetCharPressed() => Input.TakeChar();

    // -- Mouse

    /// <summary>Whether <paramref name="button"/> went down this frame.</summary>
    public static bool IsMouseButtonPressed(MouseButton button) => Input.MousePressed(button);

    /// <summary>Whether <paramref name="button"/> is held.</summary>
    public static bool IsMouseButtonDown(MouseButton button) => Input.MouseDown(button);

    /// <summary>Whether <paramref name="button"/> came up this frame.</summary>
    public static bool IsMouseButtonReleased(MouseButton button) => Input.MouseReleased(button);

    /// <summary>Whether <paramref name="button"/> is not held.</summary>
    public static bool IsMouseButtonUp(MouseButton button) => !Input.MouseDown(button);

    // -- Touch, as raylib has it: with no finger down, a held left mouse button stands in for one,
    // so a program written for touch runs with a mouse.

    /// <summary>How many fingers are on the screen, or 1 while the left mouse button is held with none.</summary>
    public static int GetTouchPointCount() =>
        Input.Touches.Count > 0 ? Input.Touches.Count : Input.MouseDown(MouseButton.Left) ? 1 : 0;

    /// <summary>Where finger <paramref name="index"/> is, in window pixels, or the pointer for 0 with no finger down.</summary>
    public static Vector2 GetTouchPosition(int index) =>
        (uint)index < (uint)Input.Touches.Count ? Input.Touches[index].Position
        : index == 0 && Input.Touches.Count == 0 ? GetMousePosition()
        : Vector2.Zero;

    /// <summary>The id finger <paramref name="index"/> keeps while it stays down, or -1 when there is no such finger.</summary>
    public static int GetTouchPointId(int index) => (uint)index < (uint)Input.Touches.Count ? (int)Input.Touches[index].Id : -1;

    /// <summary>The first finger's horizontal position, or the pointer's with no finger down.</summary>
    public static int GetTouchX() => (int)GetTouchPosition(0).X;

    /// <summary>The first finger's vertical position, or the pointer's with no finger down.</summary>
    public static int GetTouchY() => (int)GetTouchPosition(0).Y;

    // -- Gestures, as raylib's rgestures recognizes them from touch or the left mouse button.

    private static Gestures GesturesNow => Res<Gestures>();

    /// <summary>Which gestures are recognized, all of them to begin with.</summary>
    public static void SetGesturesEnabled(Gesture flags) => GesturesNow.Enabled = flags;

    /// <summary>Whether <paramref name="gesture"/> is this frame's gesture.</summary>
    public static bool IsGestureDetected(Gesture gesture) => GesturesNow.Current != Gesture.None && (GesturesNow.Current & gesture) != 0;

    /// <summary>This frame's gesture, <see cref="Gesture.None"/> when there is none.</summary>
    public static Gesture GetGestureDetected() => GesturesNow.Current;

    /// <summary>How long the current hold has lasted, in seconds.</summary>
    public static float GetGestureHoldDuration() => GesturesNow.HoldSeconds;

    /// <summary>How far the current drag has gone, in fractions of the window.</summary>
    public static Vector2 GetGestureDragVector() => GesturesNow.DragVector;

    /// <summary>The angle of the last drag or swipe, in degrees counterclockwise from right.</summary>
    public static float GetGestureDragAngle() => GesturesNow.DragAngle;

    /// <summary>The vector between a pinch's two fingers, in fractions of the window.</summary>
    public static Vector2 GetGesturePinchVector() => GesturesNow.PinchVector;

    /// <summary>The angle between a pinch's two fingers, in degrees.</summary>
    public static float GetGesturePinchAngle() => GesturesNow.PinchAngle;

    /// <summary>The pointer's position in the window, from the top left corner.</summary>
    public static Vector2 GetMousePosition() => new(Input.MouseX, Input.MouseY);

    /// <summary>The pointer's horizontal position in the window.</summary>
    public static int GetMouseX() => Input.MouseX;

    /// <summary>Moves the pointer to a place in the window, which <see cref="GetMousePosition"/> reports from then on.</summary>
    public static void SetMousePosition(int x, int y)
    {
        Input.SetMousePosition(x, y);
        if (TryRes<AppWindow>(out var window)) SDL.WarpMouseInWindow(window.Sdl.Window, x, y);
    }

    /// <summary>The pointer's vertical position in the window.</summary>
    public static int GetMouseY() => Input.MouseY;

    /// <summary>How far the pointer moved this frame.</summary>
    public static Vector2 GetMouseDelta() => new(Input.MouseDeltaX, Input.MouseDeltaY);

    /// <summary>How far the wheel turned this frame, positive away from the user.</summary>
    public static float GetMouseWheelMove() => Input.WheelY;

    /// <summary>How far the wheel turned this frame on both axes, x for a wheel or trackpad that scrolls sideways.</summary>
    public static Vector2 GetMouseWheelMoveV() => new(Input.WheelX, Input.WheelY);

    // The system cursors made so far, by shape, each made once and kept, since SDL frees one only
    // when asked and setting a cursor each frame is common.
    private static readonly Dictionary<MouseCursor, nint> Cursors = [];

    /// <summary>Sets the pointer's shape over the window, as a text field's or a link's.</summary>
    public static void SetMouseCursor(MouseCursor cursor)
    {
        if (!TryRes<AppWindow>(out _)) return;
        if (!Cursors.TryGetValue(cursor, out var made))
        {
            made = SDL.CreateSystemCursor(cursor switch
            {
                MouseCursor.IBeam => SDL.SystemCursor.Text,
                MouseCursor.Crosshair => SDL.SystemCursor.Crosshair,
                MouseCursor.PointingHand => SDL.SystemCursor.Pointer,
                MouseCursor.ResizeEW => SDL.SystemCursor.EWResize,
                MouseCursor.ResizeNS => SDL.SystemCursor.NSResize,
                MouseCursor.ResizeNWSE => SDL.SystemCursor.NWSEResize,
                MouseCursor.ResizeNESW => SDL.SystemCursor.NESWResize,
                MouseCursor.ResizeAll => SDL.SystemCursor.Move,
                MouseCursor.NotAllowed => SDL.SystemCursor.NotAllowed,
                _ => SDL.SystemCursor.Default,
            });
            if (made == 0) return;
            Cursors[cursor] = made;
        }
        SDL.SetCursor(made);
    }

    // -- Cursor

    private static bool _cursorHidden;

    /// <summary>Shows the mouse cursor over the window.</summary>
    public static void ShowCursor()
    {
        _cursorHidden = false;
        if (TryRes<AppWindow>(out _)) SDL.ShowCursor();
    }

    /// <summary>Hides the mouse cursor over the window.</summary>
    public static void HideCursor()
    {
        _cursorHidden = true;
        if (TryRes<AppWindow>(out _)) SDL.HideCursor();
    }

    /// <summary>Whether the cursor is hidden, by <see cref="HideCursor"/> or <see cref="DisableCursor"/>.</summary>
    public static bool IsCursorHidden() => _cursorHidden;

    /// <summary>Hides the cursor and holds it in the window, so the mouse only reports movement, as a first-person camera needs.</summary>
    /// <remarks><see cref="GetMouseDelta"/> keeps reporting movement while the cursor is held, without it reaching the window's edge.</remarks>
    public static void DisableCursor()
    {
        _cursorHidden = true;
        if (TryRes<AppWindow>(out var window)) SDL.SetWindowRelativeMouseMode(window.Sdl.Window, true);
    }

    /// <summary>Releases and shows the cursor that <see cref="DisableCursor"/> held.</summary>
    public static void EnableCursor()
    {
        _cursorHidden = false;
        if (TryRes<AppWindow>(out var window)) SDL.SetWindowRelativeMouseMode(window.Sdl.Window, false);
    }

    // -- Gamepads, by index in the order they connected

    /// <summary>Whether a gamepad is connected at <paramref name="gamepad"/>.</summary>
    public static bool IsGamepadAvailable(int gamepad) => Input.Gamepad(gamepad) is not null;

    /// <summary>The name of the gamepad at <paramref name="gamepad"/>, or an empty string.</summary>
    public static string GetGamepadName(int gamepad) => Input.Gamepad(gamepad)?.Name ?? "";

    /// <summary>Whether <paramref name="button"/> went down this frame.</summary>
    public static bool IsGamepadButtonPressed(int gamepad, GamepadButton button) => Input.Gamepad(gamepad)?.ButtonPressed(button) ?? false;

    /// <summary>A button that went down this frame on any gamepad, as a "press any button" screen asks, or null for none.</summary>
    public static GamepadButton? GetGamepadButtonPressed()
    {
        foreach (var pad in Input.Gamepads)
            foreach (var button in Enum.GetValues<GamepadButton>())
                if (pad.ButtonPressed(button)) return button;
        return null;
    }

    /// <summary>Whether <paramref name="button"/> is held.</summary>
    public static bool IsGamepadButtonDown(int gamepad, GamepadButton button) => Input.Gamepad(gamepad)?.ButtonDown(button) ?? false;

    /// <summary>Whether <paramref name="button"/> came up this frame.</summary>
    public static bool IsGamepadButtonReleased(int gamepad, GamepadButton button) => Input.Gamepad(gamepad)?.ButtonReleased(button) ?? false;

    /// <summary>Whether <paramref name="button"/> is not held, which is true of a pad that is not connected.</summary>
    public static bool IsGamepadButtonUp(int gamepad, GamepadButton button) => !IsGamepadButtonDown(gamepad, button);

    /// <summary>An axis, from -1 to 1 for sticks and 0 to 1 for triggers.</summary>
    public static float GetGamepadAxisMovement(int gamepad, GamepadAxis axis) => Input.Gamepad(gamepad)?.Axis(axis) ?? 0f;

    /// <summary>How many axes a gamepad has, which is six for every pad SDL maps.</summary>
    public static int GetGamepadAxisCount(int gamepad) => IsGamepadAvailable(gamepad) ? 6 : 0;

    /// <summary>Whether a gamepad reports its motion, through a gyro and an accelerometer, as a DualSense and a Switch Pro Controller do.</summary>
    public static bool IsGamepadMotionAvailable(int gamepad) => Input.Gamepad(gamepad)?.HasMotion ?? false;

    /// <summary>
    /// How fast a gamepad turns about its own axes, in radians a second: x across it, y up out of
    /// it and z toward the player, as SDL reports it, or zero for a pad without a gyro.
    /// </summary>
    public static Vector3 GetGamepadGyro(int gamepad) => Input.Gamepad(gamepad)?.Gyro ?? Vector3.Zero;

    /// <summary>A gamepad's acceleration along its own axes, in meters a second squared with gravity in it, or zero for a pad without one.</summary>
    public static Vector3 GetGamepadAccelerometer(int gamepad) => Input.Gamepad(gamepad)?.Accelerometer ?? Vector3.Zero;

    /// <summary>How many fingers are on a gamepad's touchpad.</summary>
    public static int GetGamepadTouchCount(int gamepad) => Input.Gamepad(gamepad)?.Touches.Count ?? 0;

    /// <summary>Where a finger is on a gamepad's touchpad, from 0 to 1 across and down it, by its index among the fingers on it, or zero for none.</summary>
    public static Vector2 GetGamepadTouchPosition(int gamepad, int index) =>
        Input.Gamepad(gamepad)?.Touches.ElementAtOrDefault(index) ?? Vector2.Zero;

    /// <summary>Sets the color of a gamepad's light, as a DualSense's bar, which a pad without one leaves as it is.</summary>
    public static void SetGamepadLight(int gamepad, Color color)
    {
        if (Input.Gamepad(gamepad) is { Handle: not 0 } pad) SDL.SetGamepadLED(pad.Handle, color.R, color.G, color.B);
    }

    /// <summary>Rumbles a gamepad, each motor from 0 to 1, for <paramref name="seconds"/>.</summary>
    public static void SetGamepadVibration(int gamepad, float leftMotor, float rightMotor, float seconds)
    {
        if (Input.Gamepad(gamepad) is not { Handle: not 0 } pad) return;
        SDL.RumbleGamepad(pad.Handle,
            (ushort)(Math.Clamp(leftMotor, 0f, 1f) * ushort.MaxValue),
            (ushort)(Math.Clamp(rightMotor, 0f, 1f) * ushort.MaxValue),
            (uint)Math.Max(0, seconds * 1000));
    }
}
