namespace Engine;

/// <summary>A gamepad button, by position, named as raylib names it, in SDL's order and with SDL's values.</summary>
/// <remarks>
/// Buttons are named by where they sit rather than by a printed letter, because the letter on the
/// bottom face button is A on one pad and a cross on another. The left face is the directional pad
/// and the right face the four buttons, so <see cref="RightFaceDown"/> is A on an Xbox pad and the
/// cross on a PlayStation one, as raylib's <c>GAMEPAD_BUTTON_RIGHT_FACE_DOWN</c> is.
/// </remarks>
public enum GamepadButton
{
    /// <summary>The bottom face button (A, cross).</summary>
    RightFaceDown,
    /// <summary>The right face button (B, circle).</summary>
    RightFaceRight,
    /// <summary>The left face button (X, square).</summary>
    RightFaceLeft,
    /// <summary>The top face button (Y, triangle).</summary>
    RightFaceUp,
    /// <summary>The back or select button, left of the middle.</summary>
    MiddleLeft,
    /// <summary>The guide or home button in the middle.</summary>
    Middle,
    /// <summary>The start button, right of the middle.</summary>
    MiddleRight,
    /// <summary>Pressing the left stick.</summary>
    LeftThumb,
    /// <summary>Pressing the right stick.</summary>
    RightThumb,
    /// <summary>The left shoulder button.</summary>
    LeftTrigger1,
    /// <summary>The right shoulder button.</summary>
    RightTrigger1,
    /// <summary>The directional pad's up.</summary>
    LeftFaceUp,
    /// <summary>The directional pad's down.</summary>
    LeftFaceDown,
    /// <summary>The directional pad's left.</summary>
    LeftFaceLeft,
    /// <summary>The directional pad's right.</summary>
    LeftFaceRight,
}
