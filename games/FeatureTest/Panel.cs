using System.Numerics;
using Engine;
using ImGuiNET;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The admin panel, an ImGui window F1 opens: the frame's costs as the profile measures them, the
/// engine's switches, and each zone's status, its toggles and buttons that go to it and its spots.
/// </summary>
/// <remarks>
/// The keyboard moves through its widgets, and the first gamepad too while it is open, which leaves
/// the pad to the game alone while it is shut. While it holds the keyboard the player is not walked
/// by it. The costs are averages over the last frames, read by their names in the profile, which
/// <c>./e3d command profile</c> lists whole, as the engine's profile window does when the panel
/// shows it.
/// </remarks>
sealed class Panel
{
    // The costs the panel shows, by the names the profile measures them under.
    private static readonly (string Label, string Name)[] Measured =
    [
        ("frame", "frame"), ("update", "program.update"), ("drawing", "program.drawing"), ("render graph", "render.graph"),
        ("GPU scene", "gpu.hdr_scene"), ("GPU shadows", "gpu.shadows"), ("GPU main pass", "gpu.main_pass"), ("GPU ImGui", "gpu.imgui"),
    ];

    private bool _profile;
    private readonly string? _crashed;

    public Panel(LightHandle sun, string? crashed)
    {
        _crashed = crashed;
        SetAmbientOcclusion(1, 1);
        Engine =
        [
            new Toggle("shadows", "The sun casts shadows", true, on => SetLightCastsShadows(sun, on)),
            new Toggle("occlusion", "Ambient occlusion", true, on => SetAmbientOcclusion(on ? 1 : 0, 1)),
            new Toggle("fxaa", "FXAA", IsFxaaEnabled(), SetFxaa),
            new Toggle("cap", "The frame rate held to 60", true, on => SetTargetFPS(on ? 60 : 0)),
        ];
    }

    /// <summary>Whether the panel is shown.</summary>
    public bool Open;

    /// <summary>How many frames the panel was drawn in.</summary>
    public int Drawn { get; private set; }

    /// <summary>The engine's switches, which the console names under "engine".</summary>
    public IReadOnlyList<Toggle> Engine { get; }

    /// <summary>Whether the panel holds the keyboard, so the player is not walked by it.</summary>
    public static bool Typing => ImGui.GetIO().WantCaptureKeyboard;

    /// <summary>The frame's costs, each averaged by the profile.</summary>
    public static string Costs() =>
        string.Join(", ", Measured.Select(m => $"{m.Label} {GetProfileAverage(m.Name):0.000} ms")) + $", {GetFPS()} frames a second";

    public void Update()
    {
        if (IsKeyPressed(Key.F1)) Open = !Open;
        SetImGuiGamepadNavigation(Open);
    }

    /// <summary>Draws the panel, inside the frame, asking for a teleport where a button does.</summary>
    public void Draw(Player player)
    {
        if (_profile) DrawProfileWindow();
        if (!Open) return;
        ImGui.SetNextWindowPos(new Vector2(16, 120), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(420, MathF.Min(520, GetScreenHeight() - 140)), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("Feature test", ref Open))
        {
            Drawn++;
            if (ImGui.CollapsingHeader("Frame", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var (label, name) in Measured) ImGui.Text($"{label,-14} {GetProfileAverage(name),8:0.000} ms");
                ImGui.Text($"{GetFPS()} frames a second");
                ImGui.Checkbox("The engine's profile window", ref _profile);
                if (_crashed is not null) ImGui.TextWrapped($"The last crash is written in {_crashed}.");
            }
            if (ImGui.CollapsingHeader("Engine", ImGuiTreeNodeFlags.DefaultOpen)) Switches(Engine);
            if (ImGui.CollapsingHeader("Player"))
            {
                ImGui.TextWrapped(FeatureCommands.Where);
                var flying = player.Flying;
                if (ImGui.Checkbox("Fly", ref flying)) FeatureCommands.Fly = flying;
            }
            foreach (var zone in Zones.All)
            {
                if (!ImGui.CollapsingHeader(zone.Name)) continue;
                ImGui.PushID(zone.Name);
                ImGui.TextWrapped(zone.Status());
                Switches(zone.Toggles);
                if (ImGui.Button("Go to its start")) FeatureCommands.Go = (zone, null);
                foreach (var spot in zone.Spots.Keys)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(spot)) FeatureCommands.Go = (zone, spot);
                }
                ImGui.PopID();
            }
        }
        ImGui.End();
    }

    private static void Switches(IReadOnlyList<Toggle> toggles)
    {
        foreach (var toggle in toggles)
        {
            var on = toggle.On;
            if (ImGui.Checkbox(toggle.Label, ref on)) toggle.Set(on);
        }
    }
}
