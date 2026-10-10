using System.Numerics;

namespace Engine.Game;

/// <summary>
/// The game's console commands, which <c>./e3d command</c> runs, so a test of the light can be built,
/// walked to and looked at from the terminal without a mouse.
/// </summary>
/// <remarks>Commands run on the main thread at the start of a frame, so they change the game directly.</remarks>
public static class GameCommands
{
    // The most blocks one fill changes, a cube of 64, so a mistyped corner does not stall the frame.
    private const long FillLimit = 64 * 64 * 64;

    internal static VoxelGame? Game { get; set; }

    [Command("voxel.state", "Where the player stands and looks, the block looked at, the columns and sections loaded, the frame's draws, the hour and the light")]
    internal static string State()
    {
        if (Game is not { } game) return "no world is loaded";
        var player = game.Player;
        var at = player.Body.Position;
        var renderer = game.Renderer;
        var target = game.Target is { } hit
            ? $"looking at {Blocks.Get(game.World.GetBlock(hit.X, hit.Y, hit.Z)).Key} at {hit.X}, {hit.Y}, {hit.Z}"
            : "looking at nothing within reach";
        return $"feet at {at.X:0.00}, {at.Y:0.00}, {at.Z:0.00}, facing {player.Heading:0} and pitched {player.Pitch * 180 / MathF.PI:0}, "
            + $"{(player.Flying ? "flying" : player.Body.OnGround ? "on the ground" : "in the air")}, {target}; "
            + $"{game.World.ColumnCount} columns, {game.Streamer.Pending} generating, {game.World.Loaded.Count} sections to mesh; "
            + $"{renderer.Sections} sections in {renderer.Meshes} meshes, {renderer.Draws} draws, {renderer.Triangles} triangles, {renderer.Lamps} lamps; "
            + $"hour {game.Sky.Hour:0.00}, light {game.Light.Quality}, render distance {game.RenderDistance}, world {game.World.Generator.Name} of seed {game.World.Generator.Seed}, holding {Blocks.Get(game.Hotbar.Current).Key}";
    }

    [Command("voxel.tp", "Puts the player's feet at a place: voxel.tp <x> <y> <z>")]
    internal static string Teleport(float x, float y, float z)
    {
        if (Game is not { } game) return "no world is loaded";
        game.Player.Teleport(new Vector3(x, y, z));
        return $"the player stands at {x}, {y}, {z}";
    }

    [Command("voxel.look", "Turns the player to a heading in degrees clockwise from north (-z) and a pitch in degrees up: voxel.look <heading> <pitch>")]
    internal static string Look(float heading, float pitch)
    {
        if (Game is not { } game) return "no world is loaded";
        game.Player.Heading = heading;
        game.Player.Pitch = Math.Clamp(pitch, -89, 89) * MathF.PI / 180;
        return $"the player faces {heading} and is pitched {pitch}";
    }

    [Command("voxel.fly", "Starts or stops flying: voxel.fly <on>")]
    internal static string Fly(bool on)
    {
        if (Game is not { } game) return "no world is loaded";
        game.Player.Flying = on;
        game.Player.Body.Velocity = Vector3.Zero;
        return on ? "the player flies" : "the player walks";
    }

    [Command("voxel.set", "Places a block by its name or number, air to clear one: voxel.set <x> <y> <z> <block>")]
    internal static string Set(int x, int y, int z, string block)
    {
        if (Game is not { } game) return "no world is loaded";
        if (!Blocks.TryFind(block, out var id)) return $"no block is called {block}, and voxel.blocks lists them";
        return game.World.SetBlock(x, y, z, id) ? $"{Blocks.Get(id).Key} is at {x}, {y}, {z}" : $"nothing changed at {x}, {y}, {z}, which holds that block already or is not loaded";
    }

    [Command("voxel.fill", "Fills a box between two corners with a block, air to clear it: voxel.fill <x1> <y1> <z1> <x2> <y2> <z2> <block>")]
    internal static string Fill(int x1, int y1, int z1, int x2, int y2, int z2, string block) => Box(x1, y1, z1, x2, y2, z2, block, hollow: false);

    [Command("voxel.room", "Builds the walls, floor and ceiling of a box between two corners from a block and clears its inside, a closed room to light: voxel.room <x1> <y1> <z1> <x2> <y2> <z2> <block>")]
    internal static string Room(int x1, int y1, int z1, int x2, int y2, int z2, string block) => Box(x1, y1, z1, x2, y2, z2, block, hollow: true);

    private static string Box(int x1, int y1, int z1, int x2, int y2, int z2, string block, bool hollow)
    {
        if (Game is not { } game) return "no world is loaded";
        if (!Blocks.TryFind(block, out var id)) return $"no block is called {block}, and voxel.blocks lists them";
        (x1, x2) = (Math.Min(x1, x2), Math.Max(x1, x2));
        (y1, y2) = (Math.Min(y1, y2), Math.Max(y1, y2));
        (z1, z2) = (Math.Min(z1, z2), Math.Max(z1, z2));
        var volume = (long)(x2 - x1 + 1) * (y2 - y1 + 1) * (z2 - z1 + 1);
        if (volume > FillLimit) return $"the box holds {volume} blocks, more than the {FillLimit} one fill changes";

        var changed = 0;
        for (int y = y1; y <= y2; y++)
            for (int z = z1; z <= z2; z++)
                for (int x = x1; x <= x2; x++)
                {
                    var shell = x == x1 || x == x2 || y == y1 || y == y2 || z == z1 || z == z2;
                    if (game.World.SetBlock(x, y, z, hollow && !shell ? BlockId.Air : id)) changed++;
                }
        return $"{changed} of {volume} blocks changed";
    }

    [Command("voxel.light", "The sky's and the light-giving blocks' light levels from 0 to 15 at a place: voxel.light <x> <y> <z>")]
    internal static string Light(int x, int y, int z)
    {
        if (Game is not { } game) return "no world is loaded";
        var (sky, block) = game.World.GetLight(x, y, z);
        return $"{Blocks.Get(game.World.GetBlock(x, y, z)).Key} at {x}, {y}, {z}, sky {sky}, blocks {block}";
    }

    [Command("voxel.shade", "Whether faces are darkened by light levels and their corners where blocks meet: voxel.shade <levels> <corners>")]
    internal static string Shade(bool levels, bool corners)
    {
        if (Game is not { } game) return "no world is loaded";
        (game.Renderer.LightLevels, game.Renderer.CornerShade) = (levels, corners);
        game.World.ReshadeAll();
        return $"light levels {(levels ? "on" : "off")}, corners shaded {(corners ? "on" : "off")}, every section shaded again over the next frames";
    }

    [Command("voxel.time", "Sets the hour of the day from 0 to 24, the sun up from 6 to 18: voxel.time <hour>")]
    internal static string Time(float hour)
    {
        if (Game is not { } game) return "no world is loaded";
        game.Sky.Hour = (hour % 24 + 24) % 24;
        return $"the hour is {game.Sky.Hour:0.00}";
    }

    [Command("voxel.cycle", "Lets the day go on by itself or holds the hour: voxel.cycle <on>")]
    internal static string Cycle(bool on)
    {
        if (Game is not { } game) return "no world is loaded";
        game.Sky.Cycle = on;
        return on ? "the day goes on" : "the hour is held";
    }

    [Command("voxel.world", "Begins a new world, overworld or flat, from a seed: voxel.world <kind> [seed]")]
    internal static string NewWorld(string kind, int seed = 1)
    {
        if (Game is not { } game) return "no world is loaded";
        IWorldGenerator? generator = kind.ToLowerInvariant() switch
        {
            "overworld" => new Overworld(seed),
            "flat" => new Superflat(seed),
            _ => null,
        };
        if (generator is null) return $"no world is called {kind}, which is overworld or flat";
        game.NextWorld = generator;
        return $"the {generator.Name} world of seed {seed} begins next frame";
    }

    [Command("voxel.distance", "Sets how many columns around the player are drawn, from 2 to 16: voxel.distance <columns>")]
    internal static string Distance(int columns)
    {
        if (Game is not { } game) return "no world is loaded";
        game.RenderDistance = Math.Clamp(columns, 2, 16);
        return $"{game.RenderDistance} columns are drawn around the player";
    }

    [Command("voxel.select", "Puts a block in the hotbar's chosen slot: voxel.select <block>")]
    internal static string Select(string block)
    {
        if (Game is not { } game) return "no world is loaded";
        if (!Blocks.TryFind(block, out var id) || id == BlockId.Air) return $"no block is called {block}, and voxel.blocks lists them";
        game.Hotbar.Slots[game.Hotbar.Selected] = id;
        return $"slot {game.Hotbar.Selected + 1} holds {Blocks.Get(id).Key}";
    }

    [Command("voxel.break", "Breaks the block the crosshair rests on, as a left click does")]
    internal static string Break() =>
        Game is not { } game ? "no world is loaded" : game.Break() ? "the block is broken" : "no block within reach can be broken";

    [Command("voxel.place", "Places the hotbar's chosen block against the face the crosshair rests on, as a right click does")]
    internal static string Place() =>
        Game is not { } game ? "no world is loaded" : game.Place(game.Hotbar.Current) ? $"{Blocks.Get(game.Hotbar.Current).Key} is placed" : "no block can be placed there";

    [Command("voxel.blocks", "Every block's number and name, which voxel.set, voxel.fill and voxel.select take")]
    internal static string List() => string.Join(", ", Blocks.All.Select(b => $"{(int)b.Id} {b.Key}{(b.Emits ? " (gives off light)" : "")}"));
}
