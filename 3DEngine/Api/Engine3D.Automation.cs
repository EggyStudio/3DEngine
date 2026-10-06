using System.Globalization;
using System.Text;
using ImGuiNET;

namespace Engine;

public static partial class Engine3D
{
    // raylib's MAX_AUTOMATION_EVENTS.
    private const int AutomationEventCapacity = 16384;

    // The list recording adds to, whether it does, the frame counted for each event, and what the
    // pointer, the wheel and the fingers were last frame, which an event is recorded against when
    // they change, as raylib compares its current state with its previous.
    private static AutomationEventList? _automationList;
    private static bool _automationRecording;
    private static int _automationFrame;
    private static (int X, int Y) _automationMouse;
    private static (int X, int Y) _automationWheel;
    private static TouchPoint[] _automationTouches = [];
    private static int _automationScreenshots;

    /// <summary>
    /// A list of automation events loaded from a file <see cref="ExportAutomationEventList"/>
    /// wrote, or an empty one to record into when <paramref name="fileName"/> is null.
    /// </summary>
    /// <remarks>
    /// The file is found beside the program or in the working directory, and one that cannot be
    /// read gives an empty list, with a warning, as raylib's does. Each line <c>e</c> is an event,
    /// its frame, type and four parameters, and <c>c</c> gives their count.
    /// </remarks>
    public static AutomationEventList LoadAutomationEventList(string? fileName)
    {
        var list = new AutomationEventList(AutomationEventCapacity);
        if (fileName is null) return list;

        // Found beside the program or in the working directory, as any file the program reads.
        if (LoadFileText(fileName) is not { } text) return list;

        var stated = -1;
        Span<int> values = stackalloc int[6];
        foreach (var line in text.Split('\n'))
        {
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 2 && words[0] == "c" && int.TryParse(words[1], CultureInfo.InvariantCulture, out var count)) stated = count;
            else if (words.Length >= 7 && words[0] == "e")
            {
                var read = true;
                for (int i = 0; i < 6 && read; i++) read = int.TryParse(words[i + 1], CultureInfo.InvariantCulture, out values[i]);
                if (!read) continue;
                if (list.Count == list.Capacity)
                {
                    ApiLogger.Warn($"LoadAutomationEventList: {fileName} holds more than the {list.Capacity} events a list can.");
                    break;
                }
                list.Add(new AutomationEvent(values[0], (AutomationEventType)values[1], values[2], values[3], values[4], values[5]));
            }
        }
        if (stated >= 0 && stated != list.Count)
            ApiLogger.Warn($"LoadAutomationEventList: {fileName} says it holds {stated} events and {list.Count} were read.");
        return list;
    }

    /// <summary>Lets a list go, and stops recording into it if it is the list set.</summary>
    public static void UnloadAutomationEventList(AutomationEventList list)
    {
        if (!ReferenceEquals(list, _automationList)) return;
        _automationList = null;
        _automationRecording = false;
    }

    /// <summary>Writes a list's events to a text file in raylib's format, which <see cref="LoadAutomationEventList"/> reads back.</summary>
    /// <remarks>A relative name is beside the program, as <see cref="SaveFileText"/> writes one.</remarks>
    /// <returns>Whether the file was written.</returns>
    public static bool ExportAutomationEventList(AutomationEventList list, string fileName)
    {
        var text = new StringBuilder();
        text.Append("#\n");
        text.Append("# Automation events, in raylib's text format\n");
        text.Append("#\n");
        text.Append("#    c <events_count>\n");
        text.Append("#    e <frame> <event_type> <param0> <param1> <param2> <param3> // <event_type_name>\n");
        text.Append("#\n\n");
        text.Append(CultureInfo.InvariantCulture, $"c {list.Count}\n");
        for (int i = 0; i < list.Count; i++)
        {
            var e = list.Events[i];
            text.Append(CultureInfo.InvariantCulture, $"e {e.Frame} {(int)e.Type} {e.Param0} {e.Param1} {e.Param2} {e.Param3} // Event: {AutomationTypeName(e.Type)}\n");
        }
        return SaveFileText(fileName, text.ToString());
    }

    // raylib's names for the types, which its files carry after each event.
    private static string AutomationTypeName(AutomationEventType type) => type switch
    {
        AutomationEventType.InputKeyUp => "INPUT_KEY_UP",
        AutomationEventType.InputKeyDown => "INPUT_KEY_DOWN",
        AutomationEventType.InputKeyPressed => "INPUT_KEY_PRESSED",
        AutomationEventType.InputKeyReleased => "INPUT_KEY_RELEASED",
        AutomationEventType.InputMouseButtonUp => "INPUT_MOUSE_BUTTON_UP",
        AutomationEventType.InputMouseButtonDown => "INPUT_MOUSE_BUTTON_DOWN",
        AutomationEventType.InputMousePosition => "INPUT_MOUSE_POSITION",
        AutomationEventType.InputMouseWheelMotion => "INPUT_MOUSE_WHEEL_MOTION",
        AutomationEventType.InputGamepadConnect => "INPUT_GAMEPAD_CONNECT",
        AutomationEventType.InputGamepadDisconnect => "INPUT_GAMEPAD_DISCONNECT",
        AutomationEventType.InputGamepadButtonUp => "INPUT_GAMEPAD_BUTTON_UP",
        AutomationEventType.InputGamepadButtonDown => "INPUT_GAMEPAD_BUTTON_DOWN",
        AutomationEventType.InputGamepadAxisMotion => "INPUT_GAMEPAD_AXIS_MOTION",
        AutomationEventType.InputTouchUp => "INPUT_TOUCH_UP",
        AutomationEventType.InputTouchDown => "INPUT_TOUCH_DOWN",
        AutomationEventType.InputTouchPosition => "INPUT_TOUCH_POSITION",
        AutomationEventType.InputGesture => "INPUT_GESTURE",
        AutomationEventType.WindowClose => "WINDOW_CLOSE",
        AutomationEventType.WindowMaximize => "WINDOW_MAXIMIZE",
        AutomationEventType.WindowMinimize => "WINDOW_MINIMIZE",
        AutomationEventType.WindowResize => "WINDOW_RESIZE",
        AutomationEventType.ActionTakeScreenshot => "ACTION_TAKE_SCREENSHOT",
        AutomationEventType.ActionSetTargetFps => "ACTION_SETTARGETFPS",
        _ => "EVENT_NONE",
    };

    /// <summary>Sets the list <see cref="StartAutomationEventRecording"/> records into.</summary>
    public static void SetAutomationEventList(AutomationEventList list) => _automationList = list;

    /// <summary>Sets the frame count the next frame's events are recorded at, from which it goes on counting.</summary>
    public static void SetAutomationEventBaseFrame(int frame) => _automationFrame = frame;

    /// <summary>
    /// Records the input of each frame from this one on into the list set, at the end of the
    /// frame, as <see cref="EndDrawing"/> begins.
    /// </summary>
    /// <remarks>
    /// A key or button held is an event in every frame it is held and another when it comes up,
    /// and the pointer, the wheel and a finger an event when they move, as raylib records them.
    /// Recording stops when the list is full.
    /// </remarks>
    public static void StartAutomationEventRecording() => _automationRecording = true;

    /// <summary>Stops recording automation events.</summary>
    public static void StopAutomationEventRecording() => _automationRecording = false;

    /// <summary>
    /// Plays an event, setting the key, button, pointer, wheel, finger, gamepad or gesture it
    /// records as if it had happened now, or doing what a window or action event records. Nothing
    /// is played while recording, as raylib plays nothing then.
    /// </summary>
    /// <remarks>
    /// The input is read in this frame by what is called after, so a program plays the events of
    /// a frame before it reads its input, as raylib's example does. ImGui is given the keys, the
    /// buttons, the pointer and the wheel as well, as it is given a person's.
    /// </remarks>
    public static void PlayAutomationEvent(AutomationEvent automationEvent)
    {
        if (_automationRecording || !TryRes<Input>(out var input)) return;
        var imGui = ImGui.GetCurrentContext() != IntPtr.Zero;
        var (p0, p1, p2) = (automationEvent.Param0, automationEvent.Param1, automationEvent.Param2);
        switch (automationEvent.Type)
        {
            case AutomationEventType.InputKeyUp or AutomationEventType.InputKeyDown:
                var down = automationEvent.Type == AutomationEventType.InputKeyDown;
                input.SetKey((Key)p0, down);
                var imGuiKey = SdlImGuiInput.SdlKeyToImGuiKey((SDL3.SDL.Scancode)p0);
                if (imGui && imGuiKey != ImGuiKey.None) ImGui.GetIO().AddKeyEvent(imGuiKey, down);
                break;
            case AutomationEventType.InputMouseButtonUp or AutomationEventType.InputMouseButtonDown:
                var pressed = automationEvent.Type == AutomationEventType.InputMouseButtonDown;
                input.SetMouseButton((MouseButton)p0, pressed);
                if (imGui) ImGui.GetIO().AddMouseButtonEvent(SdlImGuiInput.ImGuiButton((MouseButton)p0), pressed);
                break;
            case AutomationEventType.InputMousePosition:
                input.AddMouseDelta(p0 - input.MouseX, p1 - input.MouseY);
                input.SetMousePosition(p0, p1);
                if (imGui) ImGui.GetIO().AddMousePosEvent(p0, p1);
                break;
            case AutomationEventType.InputMouseWheelMotion:
                input.SetWheel(p0, p1);
                if (imGui) ImGui.GetIO().AddMouseWheelEvent(p0, p1);
                break;
            case AutomationEventType.InputGamepadConnect:
                // A pad played into where none is connected is made, as the console makes one.
                if (input.Gamepad(p0) is null && p0 == input.Gamepads.Count)
                    input.ConnectGamepad(SyntheticInput.ConsolePadId + (uint)p0, "Automation gamepad", 0);
                break;
            case AutomationEventType.InputGamepadDisconnect:
                // Only a pad played in is taken away, since a real one is the window's to close.
                if (input.Gamepad(p0) is { Handle: 0 } played) input.DisconnectGamepad(played.Id);
                break;
            case AutomationEventType.InputGamepadButtonUp or AutomationEventType.InputGamepadButtonDown:
                SyntheticInput.Pad(input, p0)?.SetButton((GamepadButton)p1, automationEvent.Type == AutomationEventType.InputGamepadButtonDown);
                break;
            case AutomationEventType.InputGamepadAxisMotion:
                SyntheticInput.Pad(input, p0)?.SetAxis((GamepadAxis)p1, p2 / 32768f);
                break;
            case AutomationEventType.InputTouchUp:
                if (p0 < input.Touches.Count) input.SetTouch(input.Touches[p0].Id, 0, 0, down: false);
                break;
            case AutomationEventType.InputTouchDown:
                if (p0 >= input.Touches.Count) input.SetTouch(AutomationTouchId(input), 0, 0, down: true);
                break;
            case AutomationEventType.InputTouchPosition:
                if (p0 < input.Touches.Count) input.SetTouch(input.Touches[p0].Id, p1, p2, down: true);
                break;
            case AutomationEventType.InputGesture:
                if (TryRes<Gestures>(out var gestures)) gestures.Current = (Gesture)p0;
                break;
            case AutomationEventType.WindowClose:
                _shouldClose = true;
                break;
            case AutomationEventType.WindowMaximize:
                MaximizeWindow();
                break;
            case AutomationEventType.WindowMinimize:
                MinimizeWindow();
                break;
            case AutomationEventType.WindowResize:
                SetWindowSize(p0, p1);
                break;
            case AutomationEventType.ActionTakeScreenshot:
                TakeScreenshot($"screenshot{_automationScreenshots++:000}.png");
                break;
            case AutomationEventType.ActionSetTargetFps:
                SetTargetFPS(p0);
                break;
        }
    }

    // A finger id no finger down has, for one played down.
    private static long AutomationTouchId(Input input)
    {
        long id = 1;
        while (input.Touches.Any(t => t.Id == id)) id++;
        return id;
    }

    // Records the frame's input into the list set, while recording, and counts the frame. Called as
    // EndDrawing begins, before the frame's presses and releases are cleared, where raylib records.
    private static void RecordAutomationFrame()
    {
        if (!TryRes<Input>(out var input)) return;
        if (_automationRecording && _automationList is { } list) RecordAutomationEvents(list, input, _automationFrame);
        _automationMouse = (input.MouseX, input.MouseY);
        _automationWheel = ((int)input.WheelX, (int)input.WheelY);
        _automationTouches = [.. input.Touches];
        _automationFrame++;
    }

    private static void RecordAutomationEvents(AutomationEventList list, Input input, int frame)
    {
        void Add(AutomationEventType type, int p0 = 0, int p1 = 0, int p2 = 0) =>
            list.Add(new AutomationEvent(frame, type, p0, p1, p2));

        // Keys and buttons in the order of their codes, each coming up before it is held, as
        // raylib walks its arrays of them.
        foreach (var key in input.KeysDown.Concat(input.KeysReleased).Distinct().Order())
        {
            var held = input.KeyDown(key);
            if (!held) Add(AutomationEventType.InputKeyUp, (int)key);
            else Add(AutomationEventType.InputKeyDown, (int)key);
        }
        foreach (var button in input.MouseButtonsDown.Concat(input.MouseButtonsReleased).Distinct().Order())
        {
            if (!input.MouseDown(button)) Add(AutomationEventType.InputMouseButtonUp, (int)button);
            else Add(AutomationEventType.InputMouseButtonDown, (int)button);
        }

        if ((input.MouseX, input.MouseY) != _automationMouse) Add(AutomationEventType.InputMousePosition, input.MouseX, input.MouseY);
        var wheel = ((int)input.WheelX, (int)input.WheelY);
        if (wheel != _automationWheel) Add(AutomationEventType.InputMouseWheelMotion, wheel.Item1, wheel.Item2);

        // A finger by its place among those down, as raylib numbers its touch points.
        var touches = input.Touches;
        for (int i = 0; i < Math.Max(touches.Count, _automationTouches.Length); i++)
        {
            if (i >= touches.Count)
            {
                Add(AutomationEventType.InputTouchUp, i);
                continue;
            }
            Add(AutomationEventType.InputTouchDown, i);
            var (x, y) = ((int)touches[i].Position.X, (int)touches[i].Position.Y);
            var was = i < _automationTouches.Length ? ((int)_automationTouches[i].Position.X, (int)_automationTouches[i].Position.Y) : (0, 0);
            if ((x, y) != was) Add(AutomationEventType.InputTouchPosition, i, x, y);
        }

        for (int pad = 0; pad < input.Gamepads.Count; pad++)
        {
            var state = input.Gamepads[pad];
            foreach (var button in state.ButtonsDown.Concat(state.ButtonsReleased).Distinct().Order())
            {
                if (!state.ButtonDown(button)) Add(AutomationEventType.InputGamepadButtonUp, pad, (int)button);
                else Add(AutomationEventType.InputGamepadButtonDown, pad, (int)button);
            }
            // Every axis rests at 0 here, a trigger as well, so one away from 0 is recorded.
            foreach (var axis in Enum.GetValues<GamepadAxis>())
                if (state.Axis(axis) != 0) Add(AutomationEventType.InputGamepadAxisMotion, pad, (int)axis, (int)(state.Axis(axis) * 32768f));
        }

        if (TryRes<Gestures>(out var gestures) && gestures.Current != Gesture.None) Add(AutomationEventType.InputGesture, (int)gestures.Current);
    }

    // What recording and playback keep, forgotten as the window closes.
    private static void ForgetAutomation()
    {
        _automationList = null;
        _automationRecording = false;
        _automationFrame = 0;
        _automationMouse = default;
        _automationWheel = default;
        _automationTouches = [];
    }
}
