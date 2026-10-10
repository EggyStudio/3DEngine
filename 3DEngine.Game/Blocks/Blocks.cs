namespace Engine.Game;

/// <summary>A kind of block, stored as two bytes in a section, with air as zero so a new section is empty.</summary>
public enum BlockId : ushort
{
    Air,
    Grass,
    Dirt,
    Stone,
    Cobblestone,
    Bedrock,
    Sand,
    OakLog,
    OakLeaves,
    OakPlanks,
    Snow,
    WhiteConcrete,
    RedConcrete,
    GreenConcrete,
    BlueConcrete,
    Glowstone,
    SeaLantern,
    Shroomlight,
    Magma,
    // New blocks go at the end, so a world saved before keeps its numbers without being renumbered.
    BirchLog,
    BirchLeaves,
    SpruceLog,
    SpruceLeaves,
    Sandstone,
    Cactus,
    Gravel,
    Glass,
    Water,
    Ice,
}

/// <summary>How a block meets light, the player and the faces beside it.</summary>
public enum BlockKind
{
    /// <summary>Nothing: no faces, no collision, light passes.</summary>
    Air,

    /// <summary>A solid block that hides the faces beside it and stops light.</summary>
    Opaque,

    /// <summary>A solid block light passes through, drawn see-through, as glass and ice are.</summary>
    Glass,

    /// <summary>A block the player swims through, drawn see-through, which dims light a level more for each block of it.</summary>
    Water,
}

/// <summary>
/// What a kind of block is: its key, the name commands take in lower case with underscores, the
/// name shown, the surface of each face, whether it can be broken, the light level from 0 to 15
/// it fills the blocks around it with, as Minecraft's light-giving blocks do, and its kind.
/// </summary>
public sealed record BlockInfo(BlockId Id, string Key, string Name, int Top, int Side, int Bottom, bool Breakable = true, int Light = 0, BlockKind Kind = BlockKind.Opaque)
{
    /// <summary>Whether it gives off light, in which case it is drawn as a cube of its own rather than in its chunk's meshes.</summary>
    public bool Emits => Surfaces.All[Top].Emits;

    /// <summary>The color a hotbar slot shows for it, its top's as it looks in the plains.</summary>
    public Color Swatch => Surfaces.All[Top].Plain;
}

/// <summary>Every kind of block, by its id.</summary>
public static class Blocks
{
    private static readonly BlockInfo[] _all = Build();

    private static BlockInfo[] Build()
    {
        BlockInfo Same(BlockId id, string key, string name, int surface, bool breakable = true, int light = 0, BlockKind kind = BlockKind.Opaque) =>
            new(id, key, name, surface, surface, surface, breakable, light, kind);

        BlockInfo[] all =
        [
            Same(BlockId.Air, "air", "Air", Surfaces.Stone, kind: BlockKind.Air),
            new(BlockId.Grass, "grass", "Grass Block", Surfaces.Grass, Surfaces.Dirt, Surfaces.Dirt),
            Same(BlockId.Dirt, "dirt", "Dirt", Surfaces.Dirt),
            Same(BlockId.Stone, "stone", "Stone", Surfaces.Stone),
            Same(BlockId.Cobblestone, "cobblestone", "Cobblestone", Surfaces.Cobblestone),
            Same(BlockId.Bedrock, "bedrock", "Bedrock", Surfaces.Bedrock, breakable: false),
            Same(BlockId.Sand, "sand", "Sand", Surfaces.Sand),
            new(BlockId.OakLog, "oak_log", "Oak Log", Surfaces.LogTop, Surfaces.Bark, Surfaces.LogTop),
            Same(BlockId.OakLeaves, "oak_leaves", "Oak Leaves", Surfaces.Leaves),
            Same(BlockId.OakPlanks, "oak_planks", "Oak Planks", Surfaces.Planks),
            Same(BlockId.Snow, "snow", "Snow Block", Surfaces.Snow),
            Same(BlockId.WhiteConcrete, "white_concrete", "White Concrete", Surfaces.WhiteConcrete),
            Same(BlockId.RedConcrete, "red_concrete", "Red Concrete", Surfaces.RedConcrete),
            Same(BlockId.GreenConcrete, "green_concrete", "Green Concrete", Surfaces.GreenConcrete),
            Same(BlockId.BlueConcrete, "blue_concrete", "Blue Concrete", Surfaces.BlueConcrete),
            Same(BlockId.Glowstone, "glowstone", "Glowstone", Surfaces.Glowstone, light: 15),
            Same(BlockId.SeaLantern, "sea_lantern", "Sea Lantern", Surfaces.SeaLantern, light: 15),
            Same(BlockId.Shroomlight, "shroomlight", "Shroomlight", Surfaces.Shroomlight, light: 15),
            Same(BlockId.Magma, "magma", "Magma Block", Surfaces.Magma, light: 3),
            new(BlockId.BirchLog, "birch_log", "Birch Log", Surfaces.BirchTop, Surfaces.BirchBark, Surfaces.BirchTop),
            Same(BlockId.BirchLeaves, "birch_leaves", "Birch Leaves", Surfaces.BirchLeaves),
            new(BlockId.SpruceLog, "spruce_log", "Spruce Log", Surfaces.SpruceTop, Surfaces.SpruceBark, Surfaces.SpruceTop),
            Same(BlockId.SpruceLeaves, "spruce_leaves", "Spruce Leaves", Surfaces.SpruceLeaves),
            Same(BlockId.Sandstone, "sandstone", "Sandstone", Surfaces.Sandstone),
            new(BlockId.Cactus, "cactus", "Cactus", Surfaces.CactusTop, Surfaces.CactusSide, Surfaces.CactusTop),
            Same(BlockId.Gravel, "gravel", "Gravel", Surfaces.Gravel),
            Same(BlockId.Glass, "glass", "Glass", Surfaces.Glass, kind: BlockKind.Glass),
            Same(BlockId.Water, "water", "Water", Surfaces.Water, kind: BlockKind.Water),
            Same(BlockId.Ice, "ice", "Ice", Surfaces.Ice, kind: BlockKind.Glass),
        ];
        for (int i = 0; i < all.Length; i++)
            if ((int)all[i].Id != i) throw new InvalidOperationException($"Block {all[i].Key} is listed at {i} but numbered {(int)all[i].Id}.");
        return all;
    }

    /// <summary>Every kind of block, its index its id.</summary>
    public static IReadOnlyList<BlockInfo> All => _all;

    /// <summary>The kind of block an id names.</summary>
    public static BlockInfo Get(BlockId id) => _all[(int)id];

    // Each kind's answers by id, looked up rather than asked of the record, since the mesher and the
    // light ask them for every block they pass.
    private static readonly bool[] _opaque = [.. _all.Select(b => b.Kind == BlockKind.Opaque)];
    private static readonly bool[] _collides = [.. _all.Select(b => b.Kind is BlockKind.Opaque or BlockKind.Glass)];
    private static readonly bool[] _seeThrough = [.. _all.Select(b => b.Kind is BlockKind.Glass or BlockKind.Water)];

    /// <summary>Whether a block hides the faces beside it, stops light and darkens the corners around it.</summary>
    public static bool IsOpaque(BlockId id) => _opaque[(int)id];

    /// <summary>Whether a body stands on and walks into a block, which it does all but air and water.</summary>
    public static bool Collides(BlockId id) => _collides[(int)id];

    /// <summary>Whether a block is drawn see-through, in its section's second mesh.</summary>
    public static bool IsSeeThrough(BlockId id) => _seeThrough[(int)id];

    /// <summary>Whether the crosshair rests on a block, which it does on all but air and water, as Minecraft's does.</summary>
    public static bool IsTarget(BlockId id) => id != BlockId.Air && id != BlockId.Water;

    /// <summary>Finds a block by its key, its name or its number, as a command gives it.</summary>
    public static bool TryFind(string text, out BlockId id)
    {
        if (int.TryParse(text, out var number) && (uint)number < (uint)_all.Length)
        {
            id = (BlockId)number;
            return true;
        }
        foreach (var block in _all)
            if (block.Key.Equals(text, StringComparison.OrdinalIgnoreCase) || block.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
            {
                id = block.Id;
                return true;
            }
        id = BlockId.Air;
        return false;
    }
}
