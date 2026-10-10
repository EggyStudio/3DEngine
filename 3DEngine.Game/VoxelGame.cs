using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>The game's state and its frame: the world, the player, the sky, and what is drawn over them.</summary>
public sealed class VoxelGame : IDisposable
{
    private const float Reach = 5;
    // Holding a mouse button breaks or places a block four times a second, as creative mode does.
    private const float Repeat = 0.25f;
    private const double MeshBudgetMs = 4;

    // The changed columns and the player are saved this often, beside a save on leaving a world.
    private const float SaveSeconds = 30;

    private readonly bool _transient;
    private float _breakWait, _placeWait, _sinceSave;
    private bool _captured, _swallowClick;

    /// <summary>
    /// Opens the world saved under <paramref name="name"/>, or under its kind and seed when no name is
    /// given, or begins it there from <paramref name="kind"/> and <paramref name="seed"/> where none is
    /// saved yet. A <paramref name="transient"/> world is neither read from disk nor written to it, as
    /// a test run's is.
    /// </summary>
    public VoxelGame(string kind, int seed, string? name, bool transient)
    {
        _transient = transient;
        Renderer = new ChunkRenderer();
        Sky = new Sky(10);
        Light.ApplyAll();
        Open(kind, seed, name);
        Capture(true);
    }

    public VoxelWorld World { get; private set; } = null!;

    public ChunkStreamer Streamer { get; private set; } = null!;

    /// <summary>Where the world is saved, or null for a transient one.</summary>
    public WorldSave? Save { get; private set; }

    public ChunkRenderer Renderer { get; }

    public FirstPersonController Player { get; } = new();

    public Sky Sky { get; }

    public LightSettings Light { get; } = new();

    public Hotbar Hotbar { get; } = new();

    public SettingsWindow Settings { get; } = new();

    /// <summary>How many columns around the player are drawn, and two more loaded.</summary>
    /// <remarks>
    /// Eight by default, about 1,800 draws on the hills of the first seed. Every section within it is
    /// drawn each frame, since one left out leaves the scene field, so it sets much of the frame's
    /// cost, most of it the CPU's for each draw.
    /// </remarks>
    public int RenderDistance { get; set; } = 8;

    /// <summary>The block the crosshair rests on, as of this frame.</summary>
    public BlockHit? Target { get; private set; }

    public bool HudHidden { get; set; }

    /// <summary>Whether the world fades into the sky's color toward the render distance, as Minecraft's fog hides where the world ends.</summary>
    public bool Fog { get; set; } = true;

    public bool PickerOpen { get; private set; }

    /// <summary>A recording of the picture's change around a motion, which <c>voxel.flicker</c> starts, read a frame at a time.</summary>
    public FlickerRun? Flicker { get; set; }

    /// <summary>A world asked for by a command or the settings window by its kind and seed, opened at the start of the next frame.</summary>
    public (string Kind, int Seed)? NextWorld { get; set; }

    /// <summary>Saves the world loaded and opens the one saved under a kind and seed, or begins it.</summary>
    public void SwitchWorld(string kind, int seed)
    {
        SaveWorld();
        Open(kind, seed, null);
    }

    private void Open(string kind, int seed, string? name)
    {
        Save = _transient ? null : new WorldSave(Path.Combine(WorldSave.Root, name ?? $"{kind}-{seed}"));
        var info = Save?.ReadInfo();
        var generator = (info is null ? null : Generators.Create(info.Kind, info.Seed)) ?? Generators.Create(kind, seed) ?? new Overworld(seed);
        Renderer.Clear();
        World = new VoxelWorld(generator);
        // The old streamer's workers finish into its own queue, which nothing reads again.
        Streamer = new ChunkStreamer(World, Renderer, Save);
        _sinceSave = 0;

        if (info is null)
        {
            // A new world's player stands on the highest block at the origin.
            Streamer.LoadNow(Vector3.Zero, 2);
            Player.Flying = false;
            Player.Teleport(new Vector3(0.5f, World.Top(0, 0) + 1, 0.5f));
            return;
        }
        var at = new Vector3(info.X, info.Y, info.Z);
        Streamer.LoadNow(at, 2);
        Player.Teleport(at);
        (Player.Heading, Player.Pitch, Player.Flying, Sky.Hour) = (info.Heading, info.Pitch * MathF.PI / 180, info.Flying, info.Hour);
    }

    /// <summary>Keeps every loaded column the player changed and the player's place, and writes them, unless the world is transient.</summary>
    public void SaveWorld()
    {
        if (Save is null) return;
        foreach (var column in World.Columns)
            if (column.Changed) Save.Keep(column);
        Save.Flush();
        var at = Player.Body.Position;
        Save.WriteInfo(new WorldInfo(World.Generator.Name, World.Generator.Seed, at.X, at.Y, at.Z,
            Player.Heading, Player.Pitch * 180 / MathF.PI, Player.Flying, Sky.Hour, [.. Blocks.All.Select(b => b.Key)]));
        _sinceSave = 0;
    }

    public void Update()
    {
        if (NextWorld is { } next)
        {
            NextWorld = null;
            SwitchWorld(next.Kind, next.Seed);
        }
        if (Flicker is { } run && !run.Step(this)) Flicker = null;
        // A long frame, as when the window is dragged, is taken as a short one, so the player does
        // not fall through the time it took.
        var seconds = MathF.Min(GetFrameTime(), 0.05f);
        _sinceSave += seconds;
        if (_sinceSave > SaveSeconds) SaveWorld();
        var io = ImGui.GetIO();
        var keys = !io.WantTextInput;

        if (keys) Toggles();
        if (!_captured && IsMouseButtonPressed(MouseButton.Left) && !io.WantCaptureMouse)
        {
            Capture(true);
            _swallowClick = true;
        }
        if (_swallowClick && !IsMouseButtonDown(MouseButton.Left)) _swallowClick = false;

        Hotbar.Update(keys, _captured);
        Player.Update(World, seconds, look: _captured, move: keys);
        Target = VoxelRay.Cast(World, Player.Eye, Player.Look, Reach);
        if (_captured && !_swallowClick) Interact(seconds);

        Sky.Update(seconds);
        Streamer.Update(Player.Body.Position, RenderDistance);
        Renderer.Update(World, Player.Eye, MeshBudgetMs);
    }

    private void Toggles()
    {
        if (IsKeyPressed(Key.Escape))
        {
            if (PickerOpen) PickerOpen = false;
            Capture(!_captured);
        }
        if (IsKeyPressed(Key.E))
        {
            PickerOpen = !PickerOpen;
            Capture(!PickerOpen && !Settings.Open);
        }
        if (IsKeyPressed(Key.F3))
        {
            Settings.Open = !Settings.Open;
            Capture(!Settings.Open && !PickerOpen);
        }
        if (IsKeyPressed(Key.F1)) HudHidden = !HudHidden;
        if (IsKeyPressed(Key.G)) Light.NextQuality();
    }

    private void Capture(bool captured)
    {
        _captured = captured;
        if (captured) DisableCursor();
        else EnableCursor();
    }

    private void Interact(float seconds)
    {
        _breakWait -= seconds;
        _placeWait -= seconds;
        if (IsMouseButtonPressed(MouseButton.Left)) _breakWait = 0;
        if (IsMouseButtonPressed(MouseButton.Right)) _placeWait = 0;

        if (IsMouseButtonDown(MouseButton.Left) && _breakWait <= 0)
        {
            _breakWait = Repeat;
            Break();
        }
        if (IsMouseButtonDown(MouseButton.Right) && _placeWait <= 0)
        {
            _placeWait = Repeat;
            Place(Hotbar.Current);
        }
        if (IsMouseButtonPressed(MouseButton.Middle) && Target is { } hit)
            Hotbar.Pick(World.GetBlock(hit.X, hit.Y, hit.Z));
    }

    /// <summary>Breaks the block the crosshair rests on, unless it cannot be broken.</summary>
    public bool Break()
    {
        if (Target is not { } hit || !Blocks.Get(World.GetBlock(hit.X, hit.Y, hit.Z)).Breakable) return false;
        return World.SetBlock(hit.X, hit.Y, hit.Z, BlockId.Air);
    }

    /// <summary>Places a block against the face the crosshair rests on, unless it would take up some of the player.</summary>
    public bool Place(BlockId block)
    {
        if (Target is not { Inside: false } hit) return false;
        var (x, y, z) = hit.Beside;
        // A block goes into air or water, the water giving way to it.
        if (Player.Body.Overlaps(x, y, z) || World.GetBlock(x, y, z) is not (BlockId.Air or BlockId.Water)) return false;
        return World.SetBlock(x, y, z, block);
    }

    public void Draw()
    {
        // The haze begins two thirds of the way to the render distance and covers the world at it,
        // and under water it is the water's blue and covers it within 16 blocks.
        var reach = RenderDistance * Section.Size;
        if (Player.EyeInWater) Renderer.Fog(new Color(24, 52, 120), 2, 16);
        else if (Fog) Renderer.Fog(Sky.Horizon, reach * 0.65f, reach * 0.95f);
        else Renderer.Fog(Sky.Horizon, float.MaxValue, float.MaxValue);

        BeginDrawing();
        ClearBackground(Sky.Horizon);
        BeginMode3D(Player.Camera);
        DrawSkybox();
        Renderer.Draw(Player.Eye, RenderDistance);
        if (Target is { } hit && !HudHidden) Hud.DrawOutline(hit);
        EndMode3D();

        // Under water the view is tinted blue, as Minecraft's is.
        if (Player.EyeInWater) DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(20, 50, 140, 110));
        if (!HudHidden)
        {
            Hud.DrawCrosshair();
            Hud.DrawHotbar(Hotbar);
            if (!Settings.Open) Hud.DrawHelp(Light.Quality);
        }
        Settings.Draw(this);
        if (PickerOpen) DrawPicker();
        EndDrawing();
    }

    // Every block as a swatch, which puts it in the chosen slot of the hotbar when clicked.
    private void DrawPicker()
    {
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        var open = true;
        if (ImGui.Begin("Blocks", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            var column = 0;
            foreach (var block in Blocks.All)
            {
                if (block.Id == BlockId.Air) continue;
                var swatch = block.Swatch;
                if (ImGui.ColorButton(block.Name, new Vector4(swatch.R, swatch.G, swatch.B, 255) / 255, ImGuiColorEditFlags.NoTooltip, new Vector2(40, 40)))
                    Hotbar.Slots[Hotbar.Selected] = block.Id;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(block.Name);
                if (++column % 6 != 0) ImGui.SameLine();
            }
            ImGui.NewLine();
            ImGui.TextUnformatted($"Chosen slot: {Hotbar.Selected + 1}, {Blocks.Get(Hotbar.Current).Name}");
        }
        ImGui.End();
        if (!open)
        {
            PickerOpen = false;
            Capture(!Settings.Open);
        }
    }

    public void Dispose()
    {
        SaveWorld();
        Renderer.Dispose();
    }
}
