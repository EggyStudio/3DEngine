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
}

/// <summary>
/// What a kind of block is: its key, the name commands take in lower case with underscores, the
/// name shown, the surface of each face, and whether it can be broken.
/// </summary>
public sealed record BlockInfo(BlockId Id, string Key, string Name, int Top, int Side, int Bottom, bool Breakable = true)
{
    /// <summary>Whether it gives off light, in which case it is drawn as a cube of its own rather than in its chunk's meshes.</summary>
    public bool Emits => Surfaces.All[Top].Emits;

    /// <summary>The color a hotbar slot shows for it, its top's.</summary>
    public Color Swatch => Surfaces.All[Top].Color;
}

/// <summary>Every kind of block, by its id.</summary>
public static class Blocks
{
    private static readonly BlockInfo[] _all = Build();

    private static BlockInfo[] Build()
    {
        BlockInfo Same(BlockId id, string key, string name, int surface, bool breakable = true) =>
            new(id, key, name, surface, surface, surface, breakable);

        BlockInfo[] all =
        [
            Same(BlockId.Air, "air", "Air", Surfaces.Stone),
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
            Same(BlockId.Glowstone, "glowstone", "Glowstone", Surfaces.Glowstone),
            Same(BlockId.SeaLantern, "sea_lantern", "Sea Lantern", Surfaces.SeaLantern),
            Same(BlockId.Shroomlight, "shroomlight", "Shroomlight", Surfaces.Shroomlight),
            Same(BlockId.Magma, "magma", "Magma Block", Surfaces.Magma),
        ];
        for (int i = 0; i < all.Length; i++)
            if ((int)all[i].Id != i) throw new InvalidOperationException($"Block {all[i].Key} is listed at {i} but numbered {(int)all[i].Id}.");
        return all;
    }

    /// <summary>Every kind of block, its index its id.</summary>
    public static IReadOnlyList<BlockInfo> All => _all;

    /// <summary>The kind of block an id names.</summary>
    public static BlockInfo Get(BlockId id) => _all[(int)id];

    /// <summary>Whether a block is solid to walk on and hides the faces beside it, which every block but air is for now.</summary>
    public static bool IsSolid(BlockId id) => id != BlockId.Air;

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
