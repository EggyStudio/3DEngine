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
/// A press goes down when the command runs, at the top of <see cref="Stage.First"/>, so the frame
/// that follows sees it as pressed, and it is released at the top of the frame after the last one
/// it was held for.
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
            _releases[i].Release(input);
            _releases.RemoveAt(i);
        }
    }

    /// <summary>Holds a key for <paramref name="frames"/> frames, starting with the next.</summary>
    public void Key(Input input, Key key, ulong frame, int frames)
    {
        input.SetKey(key, true);
        _releases.Add((frame + (ulong)Math.Max(1, frames), i => i.SetKey(key, false)));
    }

    /// <summary>Moves the pointer to (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void Move(Input input, int x, int y)
    {
        input.AddMouseDelta(x - input.MouseX, y - input.MouseY);
        input.SetMousePosition(x, y);
        if (ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddMousePosEvent(x, y);
    }

    /// <summary>Holds a mouse button for <paramref name="frames"/> frames at the pointer's position.</summary>
    public void Button(Input input, MouseButton button, ulong frame, int frames)
    {
        input.SetMouseButton(button, true);
        if (ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddMouseButtonEvent((int)button, true);
        _releases.Add((frame + (ulong)Math.Max(1, frames), i =>
        {
            i.SetMouseButton(button, false);
            if (ImGui.GetCurrentContext() != IntPtr.Zero) ImGui.GetIO().AddMouseButtonEvent((int)button, false);
        }));
    }

    /// <summary>Turns the wheel by <paramref name="amount"/>, positive away from the user.</summary>
    public static void Wheel(Input input, float amount)
    {
        input.AddWheel(0, amount);
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
        ConsoleHost.Hold(frame + (ulong)Math.Max(1, frames) + 1);
        return $"held {key} for {Math.Max(1, frames)} frame(s)";
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
        ConsoleHost.Hold(frame + 2);
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
        ConsoleHost.Hold(frame + (ulong)Math.Max(1, frames) + 1);
        return $"dragged {which} by {dx}, {dy}";
    }

    [Command("input.wheel", "Turns the mouse wheel, positive away from the user: input.wheel <amount>")]
    internal static string Wheel(float amount)
    {
        SyntheticInput.Wheel(Parts().Input, amount);
        return $"wheel {amount}";
    }
}
