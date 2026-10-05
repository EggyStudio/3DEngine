using System.Globalization;
using Engine;
using static Engine.Engine3D;

/// <summary>What a player does, each bound to a key and a gamepad button.</summary>
public enum Action { Forward, Back, Left, Right, Jump, Run, Pause }

/// <summary>
/// The player's settings, kept in manor-settings.txt beside the program as lines of a name and a
/// value, read when the game starts and written whenever the settings screen changes one.
/// </summary>
public sealed class Settings
{
    public const string File = "manor-settings.txt";

    public static readonly (int Width, int Height)[] Resolutions = [(960, 540), (1280, 720), (1600, 900), (1920, 1080)];

    public int Resolution = 1;
    public bool Fullscreen;
    public bool Vsync = true;
    public float Volume = 0.8f;
    public float Music = 0.5f;
    public float LookSpeed = 1;
    public bool InvertY;
    public bool MotionBlur = true;

    public readonly Dictionary<Action, Key> Keys = new()
    {
        [Action.Forward] = Key.W, [Action.Back] = Key.S, [Action.Left] = Key.A, [Action.Right] = Key.D,
        [Action.Jump] = Key.Space, [Action.Run] = Key.LShift, [Action.Pause] = Key.Escape,
    };

    // Moving is the left stick's, so only the buttons are bound.
    public readonly Dictionary<Action, GamepadButton> Buttons = new()
    {
        [Action.Jump] = GamepadButton.South, [Action.Run] = GamepadButton.LeftStick, [Action.Pause] = GamepadButton.Start,
    };

    public static Settings Load()
    {
        var settings = new Settings();
        if (!FileExists(File) || LoadFileText(File) is not { } text) return settings;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2) settings.Read(parts[0], parts[1]);
        }
        return settings;
    }

    // One line, a value it cannot read leaving the setting as it was, so a file from an older
    // version or edited by hand still loads.
    private void Read(string name, string value)
    {
        float Number(float fallback) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        bool Flag(bool fallback) => bool.TryParse(value, out var v) ? v : fallback;
        switch (name)
        {
            case "resolution": Resolution = Math.Clamp((int)Number(Resolution), 0, Resolutions.Length - 1); break;
            case "fullscreen": Fullscreen = Flag(Fullscreen); break;
            case "vsync": Vsync = Flag(Vsync); break;
            case "volume": Volume = Math.Clamp(Number(Volume), 0, 1); break;
            case "music": Music = Math.Clamp(Number(Music), 0, 1); break;
            case "look": LookSpeed = Math.Clamp(Number(LookSpeed), 0.2f, 3); break;
            case "invert": InvertY = Flag(InvertY); break;
            case "blur": MotionBlur = Flag(MotionBlur); break;
            default:
                if (name.StartsWith("key.") && Enum.TryParse<Action>(name[4..], out var action) && Enum.TryParse<Key>(value, out var key))
                    Keys[action] = key;
                else if (name.StartsWith("pad.") && Enum.TryParse<Action>(name[4..], out var padAction) && Enum.TryParse<GamepadButton>(value, out var button))
                    Buttons[padAction] = button;
                break;
        }
    }

    public void Save()
    {
        var lines = new List<string>
        {
            $"resolution = {Resolution}", $"fullscreen = {Fullscreen}", $"vsync = {Vsync}",
            $"volume = {Volume.ToString(CultureInfo.InvariantCulture)}", $"music = {Music.ToString(CultureInfo.InvariantCulture)}",
            $"look = {LookSpeed.ToString(CultureInfo.InvariantCulture)}", $"invert = {InvertY}", $"blur = {MotionBlur}",
        };
        lines.AddRange(Keys.Select(k => $"key.{k.Key} = {k.Value}"));
        lines.AddRange(Buttons.Select(b => $"pad.{b.Key} = {b.Value}"));
        SaveFileText(File, string.Join("\n", lines) + "\n");
    }

    /// <summary>Puts the window, the audio and the frame's effects as the settings say.</summary>
    public void Apply()
    {
        var (width, height) = Resolutions[Resolution];
        if (IsWindowFullscreen() != Fullscreen) ToggleFullscreen();
        // A slider moved is not a resize, which makes the swapchain again.
        if (!Fullscreen && (GetScreenWidth() != width || GetScreenHeight() != height)) SetWindowSize(width, height);
        if (Vsync) SetWindowState(ConfigFlags.VsyncHint);
        else ClearWindowState(ConfigFlags.VsyncHint);
        SetMasterVolume(Volume);
    }

    public bool Down(Action action) =>
        IsKeyDown(Keys[action]) || (Buttons.TryGetValue(action, out var button) && IsGamepadAvailable(0) && IsGamepadButtonDown(0, button));

    public bool Pressed(Action action) =>
        IsKeyPressed(Keys[action]) || (Buttons.TryGetValue(action, out var button) && IsGamepadAvailable(0) && IsGamepadButtonPressed(0, button));
}
