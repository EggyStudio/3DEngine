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

    private float _breakWait, _placeWait;
    private bool _captured, _swallowClick;

    public VoxelGame(IWorldGenerator generator)
    {
        Renderer = new ChunkRenderer();
        Sky = new Sky(10);
        Light.ApplyAll();
        World = new VoxelWorld(generator);
        Streamer = new ChunkStreamer(World, Renderer);
        Spawn();
        Capture(true);
    }

    public VoxelWorld World { get; private set; }

    public ChunkStreamer Streamer { get; private set; }

    public ChunkRenderer Renderer { get; }

    public FirstPersonController Player { get; } = new();

    public Sky Sky { get; }

    public LightSettings Light { get; } = new();

    public Hotbar Hotbar { get; } = new();

    public SettingsWindow Settings { get; } = new();

    /// <summary>How many columns around the player are drawn, and one more loaded.</summary>
    /// <remarks>
    /// Six by default, about 1,100 draws on the hills of the first seed, since each is drawn again
    /// into each of the sun's four shadow cascades. Eight draws some 1,800 and takes the frame from
    /// about 16 ms to 27 at 1280 by 720 on a laptop's RTX 4070, its shadows from 8 ms to 15 of the
    /// GPU's time.
    /// </remarks>
    public int RenderDistance { get; set; } = 6;

    /// <summary>The block the crosshair rests on, as of this frame.</summary>
    public BlockHit? Target { get; private set; }

    public bool HudHidden { get; set; }

    public bool PickerOpen { get; private set; }

    /// <summary>A world asked for by a command or the settings window, begun at the start of the next frame.</summary>
    public IWorldGenerator? NextWorld { get; set; }

    /// <summary>Begins a new world from a generator, in place of the one loaded.</summary>
    public void NewWorld(IWorldGenerator generator)
    {
        Renderer.Clear();
        World = new VoxelWorld(generator);
        // The old streamer's workers finish into its own queue, which nothing reads again.
        Streamer = new ChunkStreamer(World, Renderer);
        Spawn();
    }

    // The columns around the origin generated here, and the player stood on the highest block there.
    private void Spawn()
    {
        Streamer.LoadNow(Vector3.Zero, 2);
        Player.Flying = false;
        Player.Teleport(new Vector3(0.5f, World.Top(0, 0) + 1, 0.5f));
    }

    public void Update()
    {
        if (NextWorld is { } next)
        {
            NextWorld = null;
            NewWorld(next);
        }
        // A long frame, as when the window is dragged, is taken as a short one, so the player does
        // not fall through the time it took.
        var seconds = MathF.Min(GetFrameTime(), 0.05f);
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
        if (Player.Body.Overlaps(x, y, z) || World.GetBlock(x, y, z) != BlockId.Air) return false;
        return World.SetBlock(x, y, z, block);
    }

    public void Draw()
    {
        BeginDrawing();
        ClearBackground(Sky.Horizon);
        BeginMode3D(Player.Camera);
        DrawSkybox();
        Renderer.Draw(Player.Eye, RenderDistance);
        if (Target is { } hit && !HudHidden) Hud.DrawOutline(hit);
        EndMode3D();

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

    public void Dispose() => Renderer.Dispose();
}
