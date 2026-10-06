namespace Engine;

/// <summary>A gamepad button, by position, in SDL's order and with SDL's values.</summary>
/// <remarks>
/// Buttons are named by where they sit rather than by a printed letter, because the letter on the
/// bottom face button is A on one pad and a cross on another.
/// </remarks>
public enum GamepadButton
{
    /// <summary>The bottom face button (A, cross).</summary>
    South,
    /// <summary>The right face button (B, circle).</summary>
    East,
    /// <summary>The left face button (X, square).</summary>
    West,
    /// <summary>The top face button (Y, triangle).</summary>
    North,
    /// <summary>The back or select button.</summary>
    Back,
    /// <summary>The guide or home button.</summary>
    Guide,
    /// <summary>The start button.</summary>
    Start,
    /// <summary>Pressing the left stick.</summary>
    LeftStick,
    /// <summary>Pressing the right stick.</summary>
    RightStick,
    /// <summary>The left shoulder button.</summary>
    LeftShoulder,
    /// <summary>The right shoulder button.</summary>
    RightShoulder,
    /// <summary>D-pad up.</summary>
    DpadUp,
    /// <summary>D-pad down.</summary>
    DpadDown,
    /// <summary>D-pad left.</summary>
    DpadLeft,
    /// <summary>D-pad right.</summary>
    DpadRight,
}
