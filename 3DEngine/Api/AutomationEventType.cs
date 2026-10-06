namespace Engine;

/// <summary>What an <see cref="AutomationEvent"/> records, numbered as raylib numbers its event types in a file.</summary>
public enum AutomationEventType
{
    /// <summary>No event.</summary>
    None = 0,
    /// <summary>A key came up. <c>Param0</c> is the <see cref="Key"/>.</summary>
    InputKeyUp,
    /// <summary>A key is held, recorded each frame it is. <c>Param0</c> is the <see cref="Key"/>.</summary>
    InputKeyDown,
    /// <summary>A key went down, which raylib names and neither records nor plays.</summary>
    InputKeyPressed,
    /// <summary>A key came up, which raylib names and neither records nor plays.</summary>
    InputKeyReleased,
    /// <summary>A mouse button came up. <c>Param0</c> is the <see cref="MouseButton"/>.</summary>
    InputMouseButtonUp,
    /// <summary>A mouse button is held, recorded each frame it is. <c>Param0</c> is the <see cref="MouseButton"/>.</summary>
    InputMouseButtonDown,
    /// <summary>The pointer moved. <c>Param0</c> and <c>Param1</c> are where to, in the window's pixels.</summary>
    InputMousePosition,
    /// <summary>The wheel turned. <c>Param0</c> and <c>Param1</c> are its movement across and along.</summary>
    InputMouseWheelMotion,
    /// <summary>A gamepad was connected. <c>Param0</c> is its index.</summary>
    InputGamepadConnect,
    /// <summary>A gamepad was disconnected. <c>Param0</c> is its index.</summary>
    InputGamepadDisconnect,
    /// <summary>A gamepad button came up. <c>Param0</c> is the pad and <c>Param1</c> the <see cref="GamepadButton"/>.</summary>
    InputGamepadButtonUp,
    /// <summary>A gamepad button is held. <c>Param0</c> is the pad and <c>Param1</c> the <see cref="GamepadButton"/>.</summary>
    InputGamepadButtonDown,
    /// <summary>A gamepad axis is away from rest. <c>Param0</c> is the pad, <c>Param1</c> the <see cref="GamepadAxis"/> and <c>Param2</c> its value times 32768.</summary>
    InputGamepadAxisMotion,
    /// <summary>A finger lifted. <c>Param0</c> is its place among the fingers down.</summary>
    InputTouchUp,
    /// <summary>A finger is down. <c>Param0</c> is its place among the fingers down.</summary>
    InputTouchDown,
    /// <summary>A finger moved. <c>Param0</c> is its place, <c>Param1</c> and <c>Param2</c> where to.</summary>
    InputTouchPosition,
    /// <summary>A gesture was made. <c>Param0</c> is the <see cref="Gesture"/>.</summary>
    InputGesture,
    /// <summary>The window was asked to close.</summary>
    WindowClose,
    /// <summary>The window was maximized.</summary>
    WindowMaximize,
    /// <summary>The window was minimized.</summary>
    WindowMinimize,
    /// <summary>The window was resized. <c>Param0</c> and <c>Param1</c> are its new size.</summary>
    WindowResize,
    /// <summary>A screenshot was taken, to <c>screenshot000.png</c> and on.</summary>
    ActionTakeScreenshot,
    /// <summary>The target frame rate was set. <c>Param0</c> is the rate.</summary>
    ActionSetTargetFps,
}
