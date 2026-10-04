using System.Numerics;
using ImGuiNET;

namespace Engine;

/// <summary>
/// Input the console injects: keys and mouse buttons held for a number of frames, the pointer
/// moved, the wheel turned. It is written into the same <see cref="Input"/> resource the window's
/// events are, and mouse input is also handed to ImGui, so a game and its ImGui windows see it as
/// they would see a person.
/// </summary>
/// <remarks>
/// <para>
/// Synthetic desktop input (xdotool and the like) depends on the window having focus and on the
/// desktop session accepting it, and neither holds in a hidden run or on a locked session. This
/// path depends on neither, and works in a headless run, where there is no window at all.
/// </para>
/// <para>
/// Changes are queued on <see cref="Input"/> and made when the loop next processes events, where a
/// real key arrives, so a raylib-style loop that reads <c>IsKeyPressed</c> before
/// <c>BeginDrawing</c> sees a press in the frame it is made for. A held key is released the same
/// way after the last frame it was held for.
/// </para>
/// </remarks>
public sealed class SyntheticInput
{
    private readonly List<(ulong Frame, Action<Input> Release)> _releases = [];

    /// <summary>Releases what is due by <paramref name="frame"/>.</summary>
    public void Update(Input input, ulong frame)
    {
        for (int i = _releases.Count - 1; i >= 0; i--)
        {
            if (_releases[i].Frame > frame) continue;
            input.Enqueue(_releases[i].Release);
            _releases.RemoveAt(i);
        }
    }

    /// <summary>Holds a key for <paramref name="frames"/> frames, starting with the next.</summary>
    public void Key(Input input, Key key, ulong frame, int frames)
    {
        var imGuiKey = SdlImGuiInput.SdlKeyToImGuiKey((SDL3.SDL.Scancode)(int)key);
        input.Enqueue(i =>
        {
            i.SetKey(key, true);
            if (imGuiKey != ImGuiKey.None && ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddKeyEvent(imGuiKey, true);
        });
        _releases.Add((frame + (ulong)Math.Max(1, frames), i =>
        {
            i.SetKey(key, false);
            if (imGuiKey != ImGuiKey.None && ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddKeyEvent(imGuiKey, false);
        }));
    }

    /// <summary>The id of the pad the console makes when no real one is connected.</summary>
    public const uint ConsolePadId = 0xC0FFEE;

    /// <summary>The gamepad at <paramref name="index"/>, or a console pad made for index 0 when none is connected.</summary>
    public static GamepadState? Pad(Input input, int index) =>
        input.Gamepad(index) ?? (index == 0 && input.Gamepads.Count == 0 ? input.ConnectGamepad(ConsolePadId, "Console gamepad", 0) : null);

    /// <summary>Holds a gamepad button for <paramref name="frames"/> frames.</summary>
    public void PadButton(Input input, GamepadState pad, GamepadButton button, ulong frame, int frames)
    {
        input.Enqueue(_ => pad.SetButton(button, true));
        _releases.Add((frame + (ulong)Math.Max(1, frames), _ => pad.SetButton(button, false)));
    }

    /// <summary>Moves the pointer to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void Move(Input input, int x, int y)
    {
        input.Enqueue(i =>
        {
            i.AddMouseDelta(x - i.MouseX, y - i.MouseY);
            i.SetMousePosition(x, y);
        });
        if (ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddMousePosEvent(x, y);
    }

    /// <summary>Holds a mouse button for <paramref name="frames"/> frames at the pointer's position.</summary>
    public void Button(Input input, MouseButton button, ulong frame, int frames)
    {
        input.Enqueue(i => i.SetMouseButton(button, true));
        if (ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddMouseButtonEvent((int)button, true);
        _releases.Add((frame + (ulong)Math.Max(1, frames), i =>
        {
            i.SetMouseButton(button, false);
            if (ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddMouseButtonEvent((int)button, false);
        }));
    }

    /// <summary>Holds a finger at (<paramref name="x"/>, <paramref name="y"/>) for <paramref name="frames"/> frames.</summary>
    public void Touch(Input input, long id, float x, float y, ulong frame, int frames)
    {
        input.Enqueue(i => i.SetTouch(id, x, y, down: true));
        _releases.Add((frame + (ulong)Math.Max(1, frames), i => i.SetTouch(id, 0, 0, down: false)));
    }

    /// <summary>Types <paramref name="text"/>, as text input reaches the game and ImGui's focused field.</summary>
    public static void Type(Input input, string text) => input.Enqueue(i =>
    {
        i.AddText(text);
        if (ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddInputCharactersUTF8(text);
    });

    /// <summary>Turns the wheel by <paramref name="amount"/>, positive away from the user.</summary>
    public static void Wheel(Input input, float amount)
    {
        input.Enqueue(i => i.AddWheel(0, amount));
        if (ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddMouseWheelEvent(0, amount);
    }
}

/// <summary>The console commands that drive input.</summary>
internal static class InputCommands
{
    private static (Input Input, SyntheticInput Synthetic, ulong Frame) Parts()
    {
        var world = ConsoleHost.World!;
        return (world.Resource<Input>(), world.GetOrInsertResource(() => new SyntheticInput()), ConsoleHost.Time.FrameCount);
    }

    [Command("input.key", "Holds a key for some frames and answers when it is released: input.key <name> <frames>")]
    internal static string Key(string name, int frames)
    {
        if (!Enum.TryParse<Key>(name, ignoreCase: true, out var key) || key == Engine.Key.Unknown)
        {
            ConsoleHost.Fail("BAD_ARGUMENT", $"'{name}' is not a key. Keys are named as in the Key enum: W, Space, Escape, F2, Up, LShift.");
            return $"not a key: {name}";
        }

        var (input, synthetic, frame) = Parts();
        synthetic.Key(input, key, frame, frames);
        ConsoleHost.Hold(frame + (ulong)Math.Max(1, frames) + 2);
        return $"held {key} for {Math.Max(1, frames)} frame(s)";
    }

    [Command("input.touch", "Holds a finger at a position in the window for some frames: input.touch <id> <x> <y> <frames>")]
    internal static string Touch(int id, int x, int y, int frames)
    {
        var (input, synthetic, frame) = Parts();
        synthetic.Touch(input, id, x, y, frame, frames);
        ConsoleHost.Hold(frame + (ulong)Math.Max(1, frames) + 2);
        return $"touched {x}, {y} with finger {id} for {Math.Max(1, frames)} frame(s)";
    }

    [Command("input.move", "Moves the pointer to a position in the window: input.move <x> <y>")]
    internal static string Move(int x, int y)
    {
        SyntheticInput.Move(Parts().Input, x, y);
        return $"pointer at {x}, {y}";
    }

    [Command("input.click", "Moves the pointer and clicks the left button, answering a frame after release: input.click <x> <y>")]
    internal static string Click(int x, int y)
    {
        var (input, synthetic, frame) = Parts();
        SyntheticInput.Move(input, x, y);
        synthetic.Button(input, MouseButton.Left, frame, 1);
        ConsoleHost.Hold(frame + 3);
        return $"clicked {x}, {y}";
    }

    [Command("input.drag", "Holds a mouse button for some frames while moving the pointer: input.drag <button> <dx> <dy> <frames>")]
    internal static string Drag(string button, int dx, int dy, int frames)
    {
        if (!Enum.TryParse<MouseButton>(button, ignoreCase: true, out var which))
        {
            ConsoleHost.Fail("BAD_ARGUMENT", $"'{button}' is not a mouse button. They are Left, Middle and Right.");
            return $"not a button: {button}";
        }

        var (input, synthetic, frame) = Parts();
        synthetic.Button(input, which, frame, frames);
        SyntheticInput.Move(input, input.MouseX + dx, input.MouseY + dy);
        ConsoleHost.Hold(frame + (ulong)Math.Max(1, frames) + 2);
        return $"dragged {which} by {dx}, {dy}";
    }

    [Command("input.button", "Holds a gamepad button for some frames, on a console pad when none is connected: input.button <pad> <button> <frames>")]
    internal static string PadButton(int pad, string button, int frames)
    {
        if (!Enum.TryParse<GamepadButton>(button, ignoreCase: true, out var which))
        {
            ConsoleHost.Fail("BAD_ARGUMENT", $"'{button}' is not a gamepad button. They are South, East, West, North, Start, Back, LeftShoulder, DpadUp and the rest of GamepadButton.");
            return $"not a button: {button}";
        }

        var (input, synthetic, frame) = Parts();
        if (SyntheticInput.Pad(input, pad) is not { } state)
        {
            ConsoleHost.Fail("BAD_ARGUMENT", $"No gamepad is connected at {pad}.");
            return $"no gamepad at {pad}";
        }

        synthetic.PadButton(input, state, which, frame, frames);
        ConsoleHost.Hold(frame + (ulong)Math.Max(1, frames) + 2);
        return $"held {which} on {state.Name} for {Math.Max(1, frames)} frame(s)";
    }

    [Command("input.axis", "Sets a gamepad axis until it is set again, on a console pad when none is connected: input.axis <pad> <axis> <value>")]
    internal static string PadAxis(int pad, string axis, float value)
    {
        if (!Enum.TryParse<GamepadAxis>(axis, ignoreCase: true, out var which))
        {
            ConsoleHost.Fail("BAD_ARGUMENT", $"'{axis}' is not a gamepad axis. They are LeftX, LeftY, RightX, RightY, LeftTrigger and RightTrigger.");
            return $"not an axis: {axis}";
        }

        if (SyntheticInput.Pad(Parts().Input, pad) is not { } state)
        {
            ConsoleHost.Fail("BAD_ARGUMENT", $"No gamepad is connected at {pad}.");
            return $"no gamepad at {pad}";
        }

        state.SetAxis(which, Math.Clamp(value, -1f, 1f));
        return $"{which} on {state.Name} at {value}";
    }

    [Command("input.text", "Types text into the game's text input and ImGui's focused field: input.text <text>")]
    internal static string Text(string text)
    {
        var (input, _, frame) = Parts();
        SyntheticInput.Type(input, text);
        ConsoleHost.Hold(frame + 2);
        return $"typed {text.Length} character(s)";
    }

    [Command("input.state", "What the engine's input holds: keys and buttons down, the pointer, the fingers, the gamepads")]
    internal static string State()
    {
        var input = Parts().Input;
        var keys = Enum.GetValues<Key>().Where(k => k != Engine.Key.Unknown && input.KeyDown(k)).Distinct().Select(k => k.ToString());
        var buttons = Enum.GetValues<MouseButton>().Where(input.MouseDown).Select(b => b.ToString());
        var pads = input.Gamepads.Select((p, i) => $"{i}: {p.Name}");
        var touches = input.Touches.Select(t => $"{t.Id}: {t.Position.X:0}, {t.Position.Y:0}");
        return $"keys: {string.Join(", ", keys)}\nmouse: {input.MouseX}, {input.MouseY} {string.Join(", ", buttons)}\ntouches: {string.Join("; ", touches)}\ngamepads: {string.Join("; ", pads)}";
    }

    [Command("input.wheel", "Turns the mouse wheel, positive away from the user: input.wheel <amount>")]
    internal static string Wheel(float amount)
    {
        SyntheticInput.Wheel(Parts().Input, amount);
        return $"wheel {amount}";
    }
}
