using System.Numerics;
using System.Text;
using Engine;
using ImGuiNET;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>The screens the interface zone steps through, an app's own state machine.</summary>
public enum Screen
{
    Title,
    Play,
    Pause,
}

/// <summary>
/// The interface: a state machine of screens stepped by keys, text typed onto a sign, an ImGui
/// window with a button, a slider, a box, a list and a colour, a gamepad's button, and a health bar
/// and a gauge drawn in 2D from the slider, each shown while the player is in the zone.
/// </summary>
/// <remarks>
/// While the player is here, Enter moves the screen from the title to play, P pauses and plays
/// again, the characters typed go onto the sign, which Backspace takes back, and the first
/// gamepad's lower face button is counted, which reaches the game and not the ImGui window, the pad
/// moving through ImGui only while the panel is open. The ImGui window's button says where it is
/// drawn, so a run clicks it there. The interface is ok when the screen has been each of its three,
/// text was typed, the button was clicked and the pad's button pressed.
/// </remarks>
sealed class Interface : Zone
{
    private readonly RenderTexture2D _sign = LoadRenderTexture(256, 64);
    private readonly StringBuilder _typed = new();
    private readonly HashSet<Screen> _seen = [];
    private bool _here, _ticked = true;
    private int _clicks, _pressed, _choice;
    private float _slider = 0.6f;
    private Vector3 _colour = new(0.3f, 0.6f, 0.9f);
    private Vector2 _button;

    public Interface() => AddState(Screen.Title);

    public override string Name => "Interface";
    public override Vector3 Center => Zones.At(202.5f, Zones.Outer);
    public override Vector3 Start => Place(0, -8);

    public override void Update(float dt, Player player)
    {
        _here = Zones.Around(player.Feet) == this;
        _seen.Add(GetState<Screen>());
        if (!_here) return;
        var screen = GetState<Screen>();
        if (IsKeyPressed(Key.Enter) && screen == Screen.Title) SetState(Screen.Play);
        if (IsKeyPressed(Key.P)) SetState(screen == Screen.Pause ? Screen.Play : Screen.Pause);
        for (var c = GetCharPressed(); c != 0; c = GetCharPressed())
            if (_typed.Length < 24) _typed.Append(char.ConvertFromUtf32(c));
        if (IsKeyPressed(Key.Backspace) && _typed.Length > 0) _typed.Length--;
        if (IsGamepadButtonPressed(0, GamepadButton.RightFaceDown)) _pressed++;
    }

    public override void DrawOffscreen()
    {
        BeginTextureMode(_sign);
        ClearBackground(new Color(40, 40, 50));
        DrawText(_typed.Length > 0 ? _typed.ToString() : "type here", 10, 8, 20, Color.White);
        DrawText(GetState<Screen>().ToString(), 10, 36, 20, new Color(250, 200, 90));
        EndTextureMode();
    }

    public override void Draw(Camera3D camera)
    {
        DrawCylinder(Place(0, 0), 0.08f, 0.08f, 2.2f, 8, new Color(110, 80, 50));
        DrawBillboard(camera, _sign.Texture, Place(0, 0, 2.6f), 3, Color.White);
    }

    public override void DrawScreen(Camera3D camera)
    {
        if (!_here) return;

        // A health bar and a gauge in 2D, both from the slider.
        var (x, y) = (GetScreenWidth() - 230, GetScreenHeight() - 120);
        DrawRectangle(x, y, 200, 18, new Color(60, 20, 20));
        DrawRectangle(x, y, (int)(200 * _slider), 18, new Color(220, 60, 60));
        DrawRectangleLines(x, y, 200, 18, Color.White);
        DrawRing(new Vector2(x + 170, y - 40), 18, 26, -90, -90 + 360 * _slider, 32, new Color(90, 200, 240));

        ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 110), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(280, 0), ImGuiCond.Always);
        if (ImGui.Begin("Interface", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse))
        {
            if (ImGui.Button("Press me")) _clicks++;
            var (min, max) = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
            _button = (min + max) / 2;
            ImGui.SameLine();
            ImGui.Text($"pressed {_clicks} times");
            ImGui.SliderFloat("Health", ref _slider, 0, 1);
            ImGui.Checkbox("Ticked", ref _ticked);
            ImGui.Combo("Kind", ref _choice, "Square\0Round\0Star\0");
            ImGui.ColorEdit3("Colour", ref _colour);
        }
        ImGui.End();
    }

    public override string Status()
    {
        var screens = string.Join(", ", Enum.GetValues<Screen>().Where(_seen.Contains));
        var ok = _seen.Count == 3 && _typed.Length > 0 && _clicks > 0 && _pressed > 0;
        return $"the screen has been {screens} and is {GetState<Screen>()}, typed \"{_typed}\", the button at {_button.X:0},{_button.Y:0} clicked {_clicks} times, "
               + $"the slider at {_slider:0.00}, the pad's lower face button pressed {_pressed} times{(ok ? ", interface ok" : "")}";
    }
}
