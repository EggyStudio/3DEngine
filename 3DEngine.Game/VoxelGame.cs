using System.Numerics;
using ImGuiNET;
using static Engine.E3D;

namespace Engine.Game;

/// <summary>The game's state and its frame: the world, its mobs, the player, the sky, and what is drawn over them.</summary>
public sealed class VoxelGame : IDisposable
{
    private const float Reach = 5;
    // Holding a mouse button breaks or places a block four times a second, as creative mode does.
    private const float Repeat = 0.25f;
    private const double MeshBudgetMs = 4;

    // A hit takes this much of a mob's health, a hand's in Minecraft.
    private const int HandDamage = 1;

    // The changed columns and the player are saved this often, beside a save on leaving a world.
    private const float SaveSeconds = 30;

    private readonly bool _transient;
    private readonly Random _random = new();
    private float _breakWait, _placeWait, _sinceSave;
    private bool _captured, _swallowClick;
    private GameMode _mode;

    /// <summary>
    /// Opens the world saved under <paramref name="name"/>, or under its kind and seed when no name is
    /// given, or begins it there from <paramref name="kind"/> and <paramref name="seed"/> where none is
    /// saved yet. A <paramref name="transient"/> world is neither read from disk nor written to it, as
    /// a test run's is.
    /// </summary>
    public VoxelGame(string kind, int seed, string? name, bool transient)
    {
        _transient = transient;
        Hotbar = new Hotbar(Inventory);
        Renderer = new ChunkRenderer();
        Sky = new Sky(10);
        Light.ApplyAll();
        Open(kind, seed, name);
        Capture(true);
    }

    public VoxelWorld World { get; private set; } = null!;

    /// <summary>The animals and villagers of the loaded columns, and those the unloaded ones keep.</summary>
    public Mobs Mobs { get; private set; } = null!;

    public MobRenderer MobRenderer { get; } = new();

    /// <summary>The stacks of items lying in the world.</summary>
    public ItemDrops Drops { get; } = new();

    public DropRenderer DropRenderer { get; } = new();

    public Inventory Inventory { get; } = new();

    public ChunkStreamer Streamer { get; private set; } = null!;

    /// <summary>Where the world is saved, or null for a transient one.</summary>
    public WorldSave? Save { get; private set; }

    public ChunkRenderer Renderer { get; }

    public FirstPersonController Player { get; } = new();

    public Sky Sky { get; }

    public Clouds Clouds { get; } = new();

    public LightSettings Light { get; } = new();

    public Hotbar Hotbar { get; }

    public InventoryWindow InventoryWindow { get; } = new();

    /// <summary>How the player plays, which in survival stops flight and makes blocks take time to break and drop items.</summary>
    public GameMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            Player.MayFly = value == GameMode.Creative;
            if (value == GameMode.Survival) Player.Flying = false;
            (BreakingAt, BreakProgress) = (null, 0);
        }
    }

    /// <summary>The block being broken in survival, and how far from 0 to 1, as of this frame.</summary>
    public (int X, int Y, int Z)? BreakingAt { get; private set; }

    public float BreakProgress { get; private set; }

    public SettingsWindow Settings { get; } = new();

    /// <summary>How many columns around the player are drawn, and two more loaded.</summary>
    /// <remarks>
    /// Eight by default, about 1,800 draws on the hills of the first seed. Every section within it is
    /// drawn each frame, since one left out leaves the scene field, so it sets much of the frame's
    /// cost, most of it the CPU's for each draw.
    /// </remarks>
    public int RenderDistance { get; set; } = 8;

    /// <summary>The block the crosshair rests on, as of this frame, or null where a mob stands before it.</summary>
    public BlockHit? Target { get; private set; }

    /// <summary>The mob the crosshair rests on within <see cref="Mobs.Reach"/>, as of this frame, nearer than any block.</summary>
    public Mob? TargetMob { get; private set; }

    public bool HudHidden { get; set; }

    /// <summary>Whether the world fades into the sky's color toward the render distance, as Minecraft's fog hides where the world ends.</summary>
    public bool Fog { get; set; } = true;

    /// <summary>Whether the window E opens is open, the inventory in survival and every item in creative.</summary>
    public bool InventoryOpen { get; private set; }

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
        var (kept, placed) = Save?.ReadMobs() ?? ([], []);
        Mobs = new Mobs(kept, placed);
        Drops.Clear();
        // The mode and the inventory are the world's own, a new world's in creative with its hotbar of blocks.
        Mode = Enum.TryParse<GameMode>(info?.Mode, ignoreCase: true, out var mode) ? mode : GameMode.Creative;
        Inventory.Clear();
        if (info?.Inventory is { } slots) Inventory.Load(slots);
        else if (Mode == GameMode.Creative) Hotbar.FillCreative();
        Hotbar.Selected = Math.Clamp(info?.Selected ?? 0, 0, Hotbar.Size - 1);
        // The old streamer's workers finish into its own queue, which nothing reads again.
        Streamer = new ChunkStreamer(World, Renderer, Mobs, Save);
        Clouds.Follow(generator.Seed);
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
        Save.WriteMobs(Mobs.Columns());
        var at = Player.Body.Position;
        Save.WriteInfo(new WorldInfo(World.Generator.Name, World.Generator.Seed, at.X, at.Y, at.Z,
            Player.Heading, Player.Pitch * 180 / MathF.PI, Player.Flying, Sky.Hour, [.. Blocks.All.Select(b => b.Key)],
            Mode.ToString().ToLowerInvariant(), Inventory.Save(), Hotbar.Selected));
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
        Mobs.Update(World, Player.Body, Player.Eye, Sky.Hour, seconds);
        Aim();
        if (_captured && !_swallowClick) Interact(seconds);
        else (BreakingAt, BreakProgress) = (null, 0);
        if (_captured && keys && IsKeyPressed(Key.Q)) Throw(IsKeyDown(Key.LeftControl));
        LeaveDrops();
        Drops.Update(World, Player.Body, Inventory, seconds);

        Sky.Update(seconds);
        World.Fluids.Update(seconds);
        Clouds.Update(seconds);
        Streamer.Update(Player.Body.Position, RenderDistance);
        Renderer.Update(World, Player.Eye, MeshBudgetMs);
    }

    // What the mobs that died and, in survival, the blocks that fell with the block they held to leave
    // on the ground.
    private void LeaveDrops()
    {
        foreach (var mob in Mobs.Died)
            foreach (var (item, count) in Loot.Of(mob, _random))
                Drops.Spawn(item, count, mob.Body.Position + new Vector3(0, 0.25f, 0), new Vector3(0, 3, 0));
        Mobs.Died.Clear();
        if (Mode == GameMode.Survival)
            foreach (var (x, y, z, block) in World.Fallen)
                foreach (var (item, count) in Loot.Of(block, _random))
                    Drops.Pop(item, count, x, y, z);
        World.Fallen.Clear();
    }

    /// <summary>Throws one of the chosen slot's items ahead of the player, or its whole stack, as Q and Control with Q do.</summary>
    public bool Throw(bool all)
    {
        if (Hotbar.Current is not { } stack) return false;
        var count = Inventory.Take(Hotbar.Selected, all ? stack.Count : 1);
        var look = Vector3.Normalize(Player.Look);
        Drops.Spawn(stack.Item, count, Player.Eye - new Vector3(0, 0.3f, 0) + look * 0.3f, look * 6 + new Vector3(0, 1, 0), ItemDrops.ThrownDelay);
        return true;
    }

    // The block and the mob the crosshair rests on, a mob before a block hiding it.
    private void Aim()
    {
        Target = VoxelRay.Cast(World, Player.Eye, Player.Look, Reach);
        TargetMob = null;
        if (Mobs.Cast(Player.Eye, Player.Look, Mobs.Reach) is { } met && (Target is not { } hit || met.Distance < hit.Distance))
        {
            TargetMob = met.Mob;
            Target = null;
        }
    }

    private void Toggles()
    {
        if (IsKeyPressed(Key.Escape))
        {
            if (InventoryOpen) CloseInventory();
            Capture(!_captured);
        }
        if (IsKeyPressed(Key.E))
        {
            if (InventoryOpen) CloseInventory();
            else InventoryOpen = true;
            Capture(!InventoryOpen && !Settings.Open);
        }
        if (IsKeyPressed(Key.F3))
        {
            Settings.Open = !Settings.Open;
            Capture(!Settings.Open && !InventoryOpen);
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

    // Closes the window E opens, the stack held in it going back into the inventory and what does
    // not fit thrown.
    private void CloseInventory()
    {
        InventoryOpen = false;
        if (InventoryWindow.Close(Inventory) is { } left)
            Drops.Spawn(left.Item, left.Count, Player.Eye - new Vector3(0, 0.3f, 0), Vector3.Normalize(Player.Look) * 3, ItemDrops.ThrownDelay);
    }

    private void Interact(float seconds)
    {
        _breakWait -= seconds;
        _placeWait -= seconds;
        if (IsMouseButtonPressed(MouseButton.Left)) _breakWait = 0;
        if (IsMouseButtonPressed(MouseButton.Right)) _placeWait = 0;

        if (Mode == GameMode.Creative && IsMouseButtonDown(MouseButton.Left) && _breakWait <= 0)
        {
            _breakWait = Repeat;
            if (!Attack()) Break();
        }
        if (Mode == GameMode.Survival) Dig(seconds);
        if (IsMouseButtonDown(MouseButton.Right) && _placeWait <= 0)
        {
            _placeWait = Repeat;
            Use();
        }
        if (IsMouseButtonPressed(MouseButton.Middle) && Target is { } hit)
            Hotbar.Pick(Items.Of(World.GetBlock(hit.X, hit.Y, hit.Z)), Mode == GameMode.Creative);
    }

    // In survival a click hits a mob, and holding the button on a block breaks it over the time its
    // hardness takes, from the start again where the crosshair leaves it, and waits a quarter
    // second before the next, as Minecraft's does.
    private void Dig(float seconds)
    {
        if (!IsMouseButtonDown(MouseButton.Left))
        {
            (BreakingAt, BreakProgress) = (null, 0);
            return;
        }
        if (IsMouseButtonPressed(MouseButton.Left) && Attack() || TargetMob is not null || _breakWait > 0) return;
        if (Target is not { } hit || !Blocks.Get(World.GetBlock(hit.X, hit.Y, hit.Z)).Breakable)
        {
            (BreakingAt, BreakProgress) = (null, 0);
            return;
        }
        if (BreakingAt != (hit.X, hit.Y, hit.Z)) (BreakingAt, BreakProgress) = ((hit.X, hit.Y, hit.Z), 0);
        var time = Loot.BreakSeconds(World.GetBlock(hit.X, hit.Y, hit.Z));
        BreakProgress += time <= 0 ? 1 : seconds / time;
        if (BreakProgress < 1) return;
        Break();
        (BreakingAt, BreakProgress, _breakWait) = (null, 0, Repeat);
    }

    /// <summary>
    /// Opens or shuts the door the crosshair rests on, or places the chosen slot's block against the
    /// face it rests on, using one up in survival, as a right click does.
    /// </summary>
    /// <returns>The block placed or the door's half turned, or air where nothing happened.</returns>
    public BlockId Use()
    {
        var toggles = Target is { } door && Blocks.Get(World.GetBlock(door.X, door.Y, door.Z)).Toggled is not null;
        var placed = Place(Hotbar.CurrentBlock);
        if (Mode == GameMode.Survival && !toggles && placed != BlockId.Air) Inventory.Take(Hotbar.Selected);
        return placed;
    }

    /// <summary>Hits the mob the crosshair rests on, unless it is still red from the last hit.</summary>
    public bool Attack() => TargetMob is { } mob && mob.Hit(Player.Body.Position, HandDamage);

    /// <summary>Breaks the block the crosshair rests on at once, unless it cannot be broken, dropping what it leaves in survival.</summary>
    public bool Break()
    {
        if (Target is not { } hit) return false;
        var block = World.GetBlock(hit.X, hit.Y, hit.Z);
        if (!Blocks.Get(block).Breakable || !World.SetBlock(hit.X, hit.Y, hit.Z, BlockId.Air)) return false;
        if (Mode == GameMode.Survival)
            foreach (var (item, count) in Loot.Of(block, _random)) Drops.Pop(item, count, hit.X, hit.Y, hit.Z);
        return true;
    }

    /// <summary>
    /// Places a block against the face the crosshair rests on, unless it would take up some of the
    /// player or of a mob: a torch or a lantern the way of it that has a block to hold to, stairs climbing the
    /// way the player looks, a door two blocks high, and a slab on the open half of a slab of its
    /// kind joined to it as the whole block. A door met is opened or shut instead.
    /// </summary>
    /// <returns>The block placed, which for a torch on a wall is the wall torch, or air where none was.</returns>
    public BlockId Place(BlockId block)
    {
        if (Target is not { Inside: false } hit) return BlockId.Air;
        var met = Player.Eye + Vector3.Normalize(Player.Look) * hit.Distance;
        var height = met.Y - MathF.Floor(met.Y);
        var look = MathF.Abs(Player.Look.X) > MathF.Abs(Player.Look.Z)
            ? Player.Look.X > 0 ? Facing.East : Facing.West
            : Player.Look.Z > 0 ? Facing.South : Facing.North;

        // A door opens or shuts, whatever is held.
        var under = World.GetBlock(hit.X, hit.Y, hit.Z);
        if (World.Toggle(hit.X, hit.Y, hit.Z)) return World.GetBlock(hit.X, hit.Y, hit.Z);

        // A slab met on its open half, its top for the lower one and its underside for the upper.
        if (Blocks.Get(under) is { Item: var item, Doubled: { } whole, Shape: { } slab } && item == block && hit.NormalY == (slab.Upper ? -1 : 1))
            return World.SetBlock(hit.X, hit.Y, hit.Z, whole) ? whole : BlockId.Air;

        var (x, y, z) = hit.Beside;
        var there = World.GetBlock(x, y, z);
        // A door stands two blocks high, shut and facing the way the player looks, on a whole block.
        if (Blocks.Get(block).Toggled is not null)
        {
            BlockId lower = Blocks.Variant(block, look), upper = Blocks.Variant(block, look, true);
            if (there != BlockId.Air || World.GetBlock(x, y + 1, z) != BlockId.Air || !Blocks.IsSturdy(World.GetBlock(x, y - 1, z))
                || Player.Body.Overlaps(x, y, z, Blocks.Get(lower).Boxes) || Player.Body.Overlaps(x, y + 1, z, Blocks.Get(upper).Boxes)
                || Mobs.Overlaps(x, y, z, Blocks.Get(lower).Boxes) || Mobs.Overlaps(x, y + 1, z, Blocks.Get(upper).Boxes)) return BlockId.Air;
            World.SetBlock(x, y + 1, z, upper);
            return World.SetBlock(x, y, z, lower) ? lower : BlockId.Air;
        }
        var placed = World.Fit(block, x, y, z, hit.NormalX, hit.NormalY, hit.NormalZ, look, height);
        if (placed == BlockId.Air) return BlockId.Air;
        // A slab placed into the other half of a slab of its kind beside the face met fills the block.
        if (Blocks.Get(there) is { Item: var beside, Doubled: { } joined, Shape: { } half } && beside == block && Blocks.Get(placed).Shape?.Upper != half.Upper)
            placed = joined;
        // A whole block goes into air or water, the water giving way to it, and a smaller one into air.
        else if (there != BlockId.Air && !Blocks.IsWater(there) || Blocks.Get(placed).Shape is not null && there != BlockId.Air) return BlockId.Air;
        if (Player.Body.Overlaps(x, y, z, Blocks.Get(placed).Boxes) || Mobs.Overlaps(x, y, z, Blocks.Get(placed).Boxes)) return BlockId.Air;
        return World.SetBlock(x, y, z, placed) ? placed : BlockId.Air;
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
        MobRenderer.Draw(Mobs.All, Player.Eye);
        DropRenderer.Draw(Drops.All, Player.Eye, GetTime());
        Clouds.Draw(Player.Eye, RenderDistance, Sky.Horizon);
        if (Target is { } hit && !HudHidden)
        {
            Hud.DrawOutline(hit, World.ShapeAt(hit.X, hit.Y, hit.Z));
            if (BreakingAt == (hit.X, hit.Y, hit.Z)) Hud.DrawBreaking(hit, World.ShapeAt(hit.X, hit.Y, hit.Z), BreakProgress);
        }
        EndMode3D();

        // Under water the view is tinted blue, as Minecraft's is.
        if (Player.EyeInWater) DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), new Color(20, 50, 140, 110));
        if (!HudHidden)
        {
            Hud.DrawCrosshair();
            Hud.DrawHotbar(Hotbar);
            if (!Settings.Open) Hud.DrawHelp(Light.Quality, Mode);
        }
        Settings.Draw(this);
        if (InventoryOpen && !(Mode == GameMode.Survival ? InventoryWindow.DrawSurvival(Inventory, Hotbar.Selected) : InventoryWindow.DrawCreative(Hotbar)))
        {
            CloseInventory();
            Capture(!Settings.Open);
        }
        EndDrawing();
    }

    public void Dispose()
    {
        SaveWorld();
        Renderer.Dispose();
        MobRenderer.Dispose();
        DropRenderer.Dispose();
        Clouds.Dispose();
    }
}
