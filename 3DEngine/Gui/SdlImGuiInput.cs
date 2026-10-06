using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using SDL3;

namespace Engine;

/// <summary>Translates SDL input events into ImGui IO events (keys, mouse, text, wheel).</summary>
internal static class SdlImGuiInput
{
    /// <summary>
    /// ImGui's index for a mouse button. ImGui puts the right button before the middle one, where
    /// <see cref="MouseButton"/> and SDL put the middle one first.
    /// </summary>
    public static int ImGuiButton(MouseButton button) => button switch
    {
        MouseButton.Middle => (int)ImGuiMouseButton.Middle,
        MouseButton.Right => (int)ImGuiMouseButton.Right,
        _ => (int)button,
    };

    /// <summary>
    /// Gives ImGui a pointer position in the main window, moved to the desktop's where viewports
    /// are on, for input that comes from no SDL event, as <c>./e3d</c>'s and a replayed recording's.
    /// </summary>
    public static void AddMousePos(float x, float y)
    {
        var origin = SdlImGuiViewports.Enabled ? ImGui.GetMainViewport().Pos : Vector2.Zero;
        ImGui.GetIO().AddMousePosEvent(x + origin.X, y + origin.Y);
    }

    /// <summary>Processes an SDL event and updates the ImGui input state accordingly.</summary>
    /// <param name="e">The SDL event to process.</param>
    public static void ProcessEvent(SDL.Event e)
    {
        var io = ImGui.GetIO();
        SdlImGuiViewports.Observe(e);
        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.MouseMotion:
                // With viewports on, ImGui measures from the desktop, so a position in the window
                // the event came from, the main one or a viewport's, is moved by where that window is.
                var at = new Vector2(e.Motion.X, e.Motion.Y);
                if (SdlImGuiViewports.Enabled) at += SdlImGuiViewports.PositionOf(e.Motion.WindowID);
                io.AddMousePosEvent(at.X, at.Y);
                break;
            case SDL.EventType.MouseButtonDown:
            case SDL.EventType.MouseButtonUp:
                bool down = (SDL.EventType)e.Type == SDL.EventType.MouseButtonDown;
                int button = e.Button.Button;
                // SDL counts from 1 in MouseButton's order.
                if (button >= 1 && button <= 5)
                    io.AddMouseButtonEvent(ImGuiButton((MouseButton)(button - 1)), down);
                break;
            case SDL.EventType.MouseWheel:
                io.AddMouseWheelEvent(e.Wheel.X, e.Wheel.Y);
                break;
            case SDL.EventType.TextInput:
                if (e.Text.Text != IntPtr.Zero)
                {
                    string? s = Marshal.PtrToStringUTF8(e.Text.Text);
                    if (!string.IsNullOrEmpty(s))
                        io.AddInputCharactersUTF8(s);
                }
                break;
            case SDL.EventType.KeyDown:
            case SDL.EventType.KeyUp:
                bool isDown = (SDL.EventType)e.Type == SDL.EventType.KeyDown;
                ImGuiKey imGuiKey = SdlKeyToImGuiKey(e.Key.Scancode);
                if (imGuiKey != ImGuiKey.None)
                    io.AddKeyEvent(imGuiKey, isDown);

                var mods = SDL.GetModState();
                io.AddKeyEvent(ImGuiKey.ModShift, mods.HasFlag(SDL.Keymod.Shift));
                io.AddKeyEvent(ImGuiKey.ModCtrl, mods.HasFlag(SDL.Keymod.Ctrl));
                io.AddKeyEvent(ImGuiKey.ModAlt, mods.HasFlag(SDL.Keymod.Alt));
                break;
        }
    }

    /// <summary>Maps SDL scancodes to ImGui keys.</summary>
    internal static ImGuiKey SdlKeyToImGuiKey(SDL.Scancode sc)
    {
        return sc switch
        {
            SDL.Scancode.Tab => ImGuiKey.Tab,
            SDL.Scancode.Left => ImGuiKey.LeftArrow,
            SDL.Scancode.Right => ImGuiKey.RightArrow,
            SDL.Scancode.Up => ImGuiKey.UpArrow,
            SDL.Scancode.Down => ImGuiKey.DownArrow,
            SDL.Scancode.Home => ImGuiKey.Home,
            SDL.Scancode.End => ImGuiKey.End,
            SDL.Scancode.Insert => ImGuiKey.Insert,
            SDL.Scancode.Delete => ImGuiKey.Delete,
            SDL.Scancode.Backspace => ImGuiKey.Backspace,
            SDL.Scancode.Space => ImGuiKey.Space,
            SDL.Scancode.Return => ImGuiKey.Enter,
            SDL.Scancode.Escape => ImGuiKey.Escape,
            SDL.Scancode.A => ImGuiKey.A,
            SDL.Scancode.C => ImGuiKey.C,
            SDL.Scancode.V => ImGuiKey.V,
            SDL.Scancode.X => ImGuiKey.X,
            SDL.Scancode.Y => ImGuiKey.Y,
            SDL.Scancode.Z => ImGuiKey.Z,
            _ => ImGuiKey.None
        };
    }

    // Each button as ImGui names it, the face buttons by where they sit, so the bottom one
    // activates and the right one goes back, as on every pad ImGui's navigation is made for.
    private static readonly (GamepadButton Button, ImGuiKey Key)[] PadButtons =
    [
        (GamepadButton.RightFaceDown, ImGuiKey.GamepadFaceDown), (GamepadButton.RightFaceRight, ImGuiKey.GamepadFaceRight),
        (GamepadButton.RightFaceLeft, ImGuiKey.GamepadFaceLeft), (GamepadButton.RightFaceUp, ImGuiKey.GamepadFaceUp),
        (GamepadButton.MiddleLeft, ImGuiKey.GamepadBack), (GamepadButton.MiddleRight, ImGuiKey.GamepadStart),
        (GamepadButton.LeftTrigger1, ImGuiKey.GamepadL1), (GamepadButton.RightTrigger1, ImGuiKey.GamepadR1),
        (GamepadButton.LeftThumb, ImGuiKey.GamepadL3), (GamepadButton.RightThumb, ImGuiKey.GamepadR3),
        (GamepadButton.LeftFaceUp, ImGuiKey.GamepadDpadUp), (GamepadButton.LeftFaceDown, ImGuiKey.GamepadDpadDown),
        (GamepadButton.LeftFaceLeft, ImGuiKey.GamepadDpadLeft), (GamepadButton.LeftFaceRight, ImGuiKey.GamepadDpadRight),
    ];

    /// <summary>
    /// Gives ImGui the first connected gamepad's buttons, sticks and triggers, so its navigation
    /// moves through windows and widgets with the pad alone, or tells it there is no pad.
    /// </summary>
    /// <remarks>
    /// A stick counts from a quarter of the way over, past where a pad at rest drifts, to the full
    /// way, as ImGui's own SDL backend reads one.
    /// </remarks>
    public static void FeedGamepad(GamepadState? pad)
    {
        var io = ImGui.GetIO();
        if (pad is null)
        {
            io.BackendFlags &= ~ImGuiBackendFlags.HasGamepad;
            return;
        }
        io.BackendFlags |= ImGuiBackendFlags.HasGamepad;
        // ImGui hides its cursor until a key or button moves it, and a first press of the button that
        // picks only shows it, so a menu opened with the pad in hand took two presses to choose its
        // first item. A pad's press shows the cursor before ImGui reads it, so that press acts.
        foreach (var (button, _) in PadButtons)
            if (pad.ButtonPressed(button))
            {
                ImGuiNative.igSetNavCursorVisible(1);
                break;
            }
        foreach (var (button, key) in PadButtons) io.AddKeyEvent(key, pad.ButtonDown(button));
        Analog(io, ImGuiKey.GamepadL2, pad.Axis(GamepadAxis.LeftTrigger), 0.1f);
        Analog(io, ImGuiKey.GamepadR2, pad.Axis(GamepadAxis.RightTrigger), 0.1f);
        Analog(io, ImGuiKey.GamepadLStickLeft, -pad.Axis(GamepadAxis.LeftX), 0.25f);
        Analog(io, ImGuiKey.GamepadLStickRight, pad.Axis(GamepadAxis.LeftX), 0.25f);
        Analog(io, ImGuiKey.GamepadLStickUp, -pad.Axis(GamepadAxis.LeftY), 0.25f);
        Analog(io, ImGuiKey.GamepadLStickDown, pad.Axis(GamepadAxis.LeftY), 0.25f);
        Analog(io, ImGuiKey.GamepadRStickLeft, -pad.Axis(GamepadAxis.RightX), 0.25f);
        Analog(io, ImGuiKey.GamepadRStickRight, pad.Axis(GamepadAxis.RightX), 0.25f);
        Analog(io, ImGuiKey.GamepadRStickUp, -pad.Axis(GamepadAxis.RightY), 0.25f);
        Analog(io, ImGuiKey.GamepadRStickDown, pad.Axis(GamepadAxis.RightY), 0.25f);
    }

    private static void Analog(ImGuiIOPtr io, ImGuiKey key, float value, float deadZone)
    {
        var amount = Math.Clamp((value - deadZone) / (1 - deadZone), 0, 1);
        io.AddKeyAnalogEvent(key, amount > 0.1f, amount);
    }
}