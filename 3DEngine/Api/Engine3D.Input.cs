using System.Numerics;

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
}
