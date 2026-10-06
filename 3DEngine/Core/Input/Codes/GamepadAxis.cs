namespace Engine;

/// <summary>A gamepad axis, in SDL's order and with SDL's values.</summary>
public enum GamepadAxis
{
    /// <summary>The left stick, -1 left to 1 right.</summary>
    LeftX,
    /// <summary>The left stick, -1 up to 1 down.</summary>
    LeftY,
    /// <summary>The right stick, -1 left to 1 right.</summary>
    RightX,
    /// <summary>The right stick, -1 up to 1 down.</summary>
    RightY,
    /// <summary>The left trigger, 0 released to 1 pressed.</summary>
    LeftTrigger,
    /// <summary>The right trigger, 0 released to 1 pressed.</summary>
    RightTrigger,
}
