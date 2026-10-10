using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>
/// An ImGui window of what the frame holds and costs, and of the light, the sky and the world to
/// change while playing, with the engine's own window of the light that bounces beside it.
/// </summary>
public sealed class SettingsWindow
{
    private static readonly string[] Qualities = Enum.GetNames<GlobalIllumination>();
    private static readonly string[] Worlds = Generators.Kinds;

    private int _worldType;
    private int _seed = 1;

    public bool Open { get; set; }

    public bool BounceWindow { get; set; }

    public bool ProfileWindow { get; set; }

    public void Draw(VoxelGame game)
    {
        if (BounceWindow) DrawBounceWindow();
        if (ProfileWindow) DrawProfileWindow();
        if (!Open) return;

        ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(460, 0), ImGuiCond.FirstUseEver);
        var open = Open;
        if (ImGui.Begin("Voxel", ref open))
        {
            Stats(game);
            if (ImGui.CollapsingHeader("Light", ImGuiTreeNodeFlags.DefaultOpen)) Light(game);
            if (ImGui.CollapsingHeader("Sky", ImGuiTreeNodeFlags.DefaultOpen)) Sky(game.Sky);
            if (ImGui.CollapsingHeader("World", ImGuiTreeNodeFlags.DefaultOpen)) World(game);
        }
        ImGui.End();
        Open = open;
    }

    private static void Stats(VoxelGame game)
    {
        var player = game.Player;
        var at = player.Body.Position;
        ImGui.TextUnformatted($"{GetFPS()} fps, {GetFrameTime() * 1000:0.0} ms a frame");
        ImGui.TextUnformatted($"Feet at {at.X:0.0}, {at.Y:0.0}, {at.Z:0.0}, facing {player.Heading:0} degrees{(player.Flying ? ", flying" : "")}, in {game.World.BiomeAt((int)MathF.Floor(at.X), (int)MathF.Floor(at.Z))?.Name ?? "nowhere loaded"}");
        ImGui.TextUnformatted(game.Target is { } hit
            ? $"Looking at {Blocks.Get(game.World.GetBlock(hit.X, hit.Y, hit.Z)).Name} at {hit.X}, {hit.Y}, {hit.Z}, its face's light {LightBeside(game, hit)}"
            : "Looking at nothing within reach");
        var renderer = game.Renderer;
        ImGui.TextUnformatted($"{game.World.ColumnCount} columns, {game.Streamer.Pending} generating, {game.World.Loaded.Count} sections to mesh");
        ImGui.TextUnformatted($"{renderer.Sections} sections drawn as a mesh each, {renderer.Lamps} lamps");
        ImGui.TextUnformatted($"{renderer.Draws} draws of {renderer.Triangles:N0} triangles");
    }

    private static string LightBeside(VoxelGame game, BlockHit hit)
    {
        var (x, y, z) = hit.Beside;
        var (sky, block) = game.World.GetLight(x, y, z);
        return $"sky {sky}, blocks {block}";
    }

    private void Light(VoxelGame game)
    {
        var light = game.Light;
        var quality = (int)light.Quality;
        if (ImGui.Combo("Light that bounces", ref quality, Qualities, Qualities.Length))
        {
            light.Quality = (GlobalIllumination)quality;
            SetGlobalIllumination(light.Quality);
        }

        var field = false;
        field |= ImGui.SliderInt("Field cascades", ref light.Cascades, 1, 8);
        field |= ImGui.SliderFloat("Field cell", ref light.CellSize, 0.125f, 1, "%.3f");
        field |= ImGui.SliderInt("Cascades built a frame", ref light.Budget, 1, 8);
        if (field) light.ApplyField();

        // Each changes every section's colors, which are written again a few sections a frame.
        var levels = game.Renderer.LightLevels;
        if (ImGui.Checkbox("Light levels", ref levels))
        {
            game.Renderer.LightLevels = levels;
            game.World.ReshadeAll();
        }
        ImGui.SameLine();
        var corners = game.Renderer.CornerShade;
        if (ImGui.Checkbox("Corners shaded", ref corners))
        {
            game.Renderer.CornerShade = corners;
            game.World.ReshadeAll();
        }

        var glow = game.Renderer.GlowScale;
        if (ImGui.SliderFloat("Glow", ref glow, 0, 4)) game.Renderer.GlowScale = glow;
        if (ImGui.SliderFloat("Bloom", ref light.Bloom, 0, 2)) SetBloom(light.Bloom);
        if (ImGui.SliderFloat("Ambient occlusion", ref light.Occlusion, 0, 1)) SetAmbientOcclusion(light.Occlusion);
        var exposure = ImGui.Checkbox("Exposure follows the scene", ref light.AutoExposure);
        if (!light.AutoExposure) exposure |= ImGui.SliderFloat("Exposure", ref light.Exposure, 0.1f, 4);
        if (exposure) light.ApplyExposure();

        var bounce = BounceWindow;
        if (ImGui.Checkbox("The bounce's window", ref bounce)) BounceWindow = bounce;
        ImGui.SameLine();
        var profile = ProfileWindow;
        if (ImGui.Checkbox("The frame profile", ref profile)) ProfileWindow = profile;
    }

    private static void Sky(Sky sky)
    {
        var hour = sky.Hour;
        if (ImGui.SliderFloat("Hour", ref hour, 0, 24, "%.2f")) sky.Hour = hour % 24;
        var cycle = sky.Cycle;
        if (ImGui.Checkbox("The day goes on", ref cycle)) sky.Cycle = cycle;
        var minutes = sky.DayMinutes;
        if (ImGui.SliderFloat("Minutes a day", ref minutes, 1, 60)) sky.DayMinutes = minutes;
    }

    private void World(VoxelGame game)
    {
        var fog = game.Fog;
        if (ImGui.Checkbox("Fog toward the render distance", ref fog)) game.Fog = fog;
        var distance = game.RenderDistance;
        if (ImGui.SliderInt("Render distance", ref distance, 2, 16)) game.RenderDistance = distance;
        ImGui.TextUnformatted(game.Save is { } save ? $"Saved in {save.Folder} every 30 seconds" : "Transient, not saved");
        if (game.Save is not null && ImGui.Button("Save now")) game.SaveWorld();
        ImGui.Combo("Kind", ref _worldType, Worlds, Worlds.Length);
        ImGui.InputInt("Seed", ref _seed);
        if (ImGui.Button("Open or begin")) game.NextWorld = (Worlds[_worldType], _seed);
    }
}
