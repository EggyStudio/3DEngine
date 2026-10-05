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

/// <summary>One connected gamepad's buttons and axes this frame.</summary>
public sealed class GamepadState
{
    private readonly HashSet<GamepadButton> _down = [];
    private readonly HashSet<GamepadButton> _pressed = [];
    private readonly HashSet<GamepadButton> _released = [];
    private readonly float[] _axes = new float[6];
    private readonly SortedDictionary<int, System.Numerics.Vector2> _touches = [];

    internal GamepadState(uint id, string name, nint handle)
    {
        Id = id;
        Name = name;
        Handle = handle;
    }

    /// <summary>The platform's id for the pad, which stays the same while it is connected.</summary>
    public uint Id { get; }

    /// <summary>The pad's name as the platform reports it.</summary>
    public string Name { get; }

    /// <summary>The platform's handle, for rumble. Zero for a pad the console made.</summary>
    internal nint Handle { get; }

    /// <summary>Whether <paramref name="button"/> is held.</summary>
    public bool ButtonDown(GamepadButton button) => _down.Contains(button);

    /// <summary>Whether <paramref name="button"/> went down this frame.</summary>
    public bool ButtonPressed(GamepadButton button) => _pressed.Contains(button);

    /// <summary>Whether <paramref name="button"/> came up this frame.</summary>
    public bool ButtonReleased(GamepadButton button) => _released.Contains(button);

    /// <summary>An axis, from -1 to 1 for sticks and 0 to 1 for triggers.</summary>
    public float Axis(GamepadAxis axis) => (uint)axis < (uint)_axes.Length ? _axes[(int)axis] : 0f;

    /// <summary>Whether the pad reports its motion, which the gyro and accelerometer read.</summary>
    public bool HasMotion { get; internal set; }

    /// <summary>How fast the pad turns about each of its axes, in radians a second, as SDL reports it.</summary>
    public System.Numerics.Vector3 Gyro { get; internal set; }

    /// <summary>The pad's acceleration along each of its axes, in meters a second squared, gravity included.</summary>
    public System.Numerics.Vector3 Accelerometer { get; internal set; }

    /// <summary>The fingers on the pad's touchpad, by finger, each from 0 to 1 across and down it.</summary>
    public IReadOnlyCollection<System.Numerics.Vector2> Touches => _touches.Values;

    internal void SetTouch(int finger, System.Numerics.Vector2? position)
    {
        if (position is { } at) _touches[finger] = at;
        else _touches.Remove(finger);
    }

    /// <summary>The button pressed last on this pad while it is held, null once it comes up.</summary>
    internal GamepadButton? LastPressed { get; private set; }

    /// <summary>When the button last pressed on this pad went down, in the order of presses on every pad, kept once it is up.</summary>
    internal long LastPressedOrder { get; private set; }

    // Presses on every pad counted in one order, so the last of them is found across pads as
    // raylib keeps one last button for all of them.
    private static long _pressOrder;

    internal void SetButton(GamepadButton button, bool isDown)
    {
        if (isDown)
        {
            if (_down.Add(button))
            {
                _pressed.Add(button);
                (LastPressed, LastPressedOrder) = (button, ++_pressOrder);
            }
        }
        else if (_down.Remove(button))
        {
            _released.Add(button);
            if (LastPressed == button) LastPressed = null;
        }
    }

    internal void SetAxis(GamepadAxis axis, float value)
    {
        if ((uint)axis < (uint)_axes.Length) _axes[(int)axis] = value;
    }

    internal void BeginFrame()
    {
        _pressed.Clear();
        _released.Clear();
    }
}
