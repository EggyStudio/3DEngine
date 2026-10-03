using System.Numerics;
using SDL3;

namespace Engine;

public static partial class Engine3D
{
    private static Input Input => World.Resource<Input>();

    // -- Keyboard

    /// <summary>Whether <paramref name="key"/> went down this frame.</summary>
    public static bool IsKeyPressed(Key key) => Input.KeyPressed(key);

    /// <summary>Whether <paramref name="key"/> is held.</summary>
    public static bool IsKeyDown(Key key) => Input.KeyDown(key);

    /// <summary>Whether <paramref name="key"/> came up this frame.</summary>
    public static bool IsKeyReleased(Key key) => Input.KeyReleased(key);

    /// <summary>Whether <paramref name="key"/> is not held.</summary>
    public static bool IsKeyUp(Key key) => !Input.KeyDown(key);

    // -- Mouse

    /// <summary>Whether <paramref name="button"/> went down this frame.</summary>
    public static bool IsMouseButtonPressed(MouseButton button) => Input.MousePressed(button);

    /// <summary>Whether <paramref name="button"/> is held.</summary>
    public static bool IsMouseButtonDown(MouseButton button) => Input.MouseDown(button);

    /// <summary>Whether <paramref name="button"/> came up this frame.</summary>
    public static bool IsMouseButtonReleased(MouseButton button) => Input.MouseReleased(button);

    /// <summary>Whether <paramref name="button"/> is not held.</summary>
    public static bool IsMouseButtonUp(MouseButton button) => !Input.MouseDown(button);

    /// <summary>The pointer's position in the window, from the top left corner.</summary>
    public static Vector2 GetMousePosition() => new(Input.MouseX, Input.MouseY);

    /// <summary>The pointer's horizontal position in the window.</summary>
    public static int GetMouseX() => Input.MouseX;

    /// <summary>The pointer's vertical position in the window.</summary>
    public static int GetMouseY() => Input.MouseY;

    /// <summary>How far the pointer moved this frame.</summary>
    public static Vector2 GetMouseDelta() => new(Input.MouseDeltaX, Input.MouseDeltaY);

    /// <summary>How far the wheel turned this frame, positive away from the user.</summary>
    public static float GetMouseWheelMove() => Input.WheelY;

    // -- Cursor

    private static bool _cursorHidden;

    /// <summary>Shows the mouse cursor over the window.</summary>
    public static void ShowCursor()
    {
        _cursorHidden = false;
        if (World.TryGetResource<AppWindow>(out _)) SDL.ShowCursor();
    }

    /// <summary>Hides the mouse cursor over the window.</summary>
    public static void HideCursor()
    {
        _cursorHidden = true;
        if (World.TryGetResource<AppWindow>(out _)) SDL.HideCursor();
    }

    /// <summary>Whether the cursor is hidden, by <see cref="HideCursor"/> or <see cref="DisableCursor"/>.</summary>
    public static bool IsCursorHidden() => _cursorHidden;

    /// <summary>Hides the cursor and holds it in the window, so the mouse only reports movement, as a first-person camera needs.</summary>
    /// <remarks><see cref="GetMouseDelta"/> keeps reporting movement while the cursor is held, without it reaching the window's edge.</remarks>
    public static void DisableCursor()
    {
        _cursorHidden = true;
        if (World.TryGetResource<AppWindow>(out var window)) SDL.SetWindowRelativeMouseMode(window.Sdl.Window, true);
    }

    /// <summary>Releases and shows the cursor that <see cref="DisableCursor"/> held.</summary>
    public static void EnableCursor()
    {
        _cursorHidden = false;
        if (World.TryGetResource<AppWindow>(out var window)) SDL.SetWindowRelativeMouseMode(window.Sdl.Window, false);
    }

    // -- Gamepads, by index in the order they connected

    /// <summary>Whether a gamepad is connected at <paramref name="gamepad"/>.</summary>
    public static bool IsGamepadAvailable(int gamepad) => Input.Gamepad(gamepad) is not null;

    /// <summary>The name of the gamepad at <paramref name="gamepad"/>, or an empty string.</summary>
    public static string GetGamepadName(int gamepad) => Input.Gamepad(gamepad)?.Name ?? "";

    /// <summary>Whether <paramref name="button"/> went down this frame.</summary>
    public static bool IsGamepadButtonPressed(int gamepad, GamepadButton button) => Input.Gamepad(gamepad)?.ButtonPressed(button) ?? false;

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
