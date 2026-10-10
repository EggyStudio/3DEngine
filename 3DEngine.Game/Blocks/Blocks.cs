using System.Numerics;

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
    Torch,
    WallTorchNorth,
    WallTorchSouth,
    WallTorchWest,
    WallTorchEast,
    SoulTorch,
    SoulWallTorchNorth,
    SoulWallTorchSouth,
    SoulWallTorchWest,
    SoulWallTorchEast,
    Lantern,
    HangingLantern,
    SoulLantern,
    SoulHangingLantern,
    EndRod,
    EndRodDown,
    EndRodNorth,
    EndRodSouth,
    EndRodWest,
    EndRodEast,
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

    /// <summary>A block smaller than its cell, made of the boxes of its <see cref="BlockShape"/>, which light and the player pass and which hides no face beside it.</summary>
    Shaped,
}

/// <summary>
/// What a kind of block is: its key, the name commands take in lower case with underscores, the
/// name shown, the surface of each face, whether it can be broken, the light level from 0 to 15
/// it fills the blocks around it with, as Minecraft's light-giving blocks do, its kind, the boxes
/// of a block smaller than its cell, and the block a player holds to place it where that is
/// another, as a torch is held to place a torch on a wall.
/// </summary>
public sealed record BlockInfo(BlockId Id, string Key, string Name, int Top, int Side, int Bottom, bool Breakable = true, int Light = 0,
    BlockKind Kind = BlockKind.Opaque, BlockShape? Shape = null, BlockId? HeldAs = null)
{
    /// <summary>Whether it gives off light, its top's surface glowing, which a block smaller than its cell takes from its glowing piece.</summary>
    public bool Emits => Surfaces.All[Top].Emits;

    /// <summary>The block a player holds to place it, and picks from it, which is itself but for a block placed in more than one way.</summary>
    public BlockId Item => HeldAs ?? Id;

    /// <summary>
    /// The boxes of it that give off light, each the place and size within its cell of a lamp's cube
    /// centered on the origin a side long, and its surface: the whole cell for a glowing block, the
    /// glowing pieces of a smaller one, and none for the rest.
    /// </summary>
    public (Matrix4x4 Local, int Surface)[] Glows { get; } = Shape is { } shape
        ? [.. shape.Pieces.Where(p => p.Glows).Select(p => (Matrix4x4.CreateScale(p.To - p.From) * Matrix4x4.CreateTranslation((p.From + p.To) / 2), p.Surface))]
        : Surfaces.All[Top].Emits ? [(Matrix4x4.CreateTranslation(new Vector3(0.5f)), Top)] : [];

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

        // A block smaller than its cell shows the surface of the piece that glows on its hotbar slot.
        BlockInfo Shaped(BlockId id, string key, string name, BlockShape shape, int light, BlockId? heldAs = null)
        {
            var look = shape.Pieces.FirstOrDefault(p => p.Glows, shape.Pieces[0]).Surface;
            return new(id, key, name, look, look, look, Light: light, Kind: BlockKind.Shaped, Shape: shape, HeldAs: heldAs);
        }

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
            Shaped(BlockId.Torch, "torch", "Torch", BlockShape.Torch(Surfaces.Flame), 14),
            Shaped(BlockId.WallTorchNorth, "wall_torch_north", "Wall Torch", BlockShape.WallTorch(Facing.North, Surfaces.Flame), 14, BlockId.Torch),
            Shaped(BlockId.WallTorchSouth, "wall_torch_south", "Wall Torch", BlockShape.WallTorch(Facing.South, Surfaces.Flame), 14, BlockId.Torch),
            Shaped(BlockId.WallTorchWest, "wall_torch_west", "Wall Torch", BlockShape.WallTorch(Facing.West, Surfaces.Flame), 14, BlockId.Torch),
            Shaped(BlockId.WallTorchEast, "wall_torch_east", "Wall Torch", BlockShape.WallTorch(Facing.East, Surfaces.Flame), 14, BlockId.Torch),
            Shaped(BlockId.SoulTorch, "soul_torch", "Soul Torch", BlockShape.Torch(Surfaces.SoulFlame), 10),
            Shaped(BlockId.SoulWallTorchNorth, "soul_wall_torch_north", "Soul Wall Torch", BlockShape.WallTorch(Facing.North, Surfaces.SoulFlame), 10, BlockId.SoulTorch),
            Shaped(BlockId.SoulWallTorchSouth, "soul_wall_torch_south", "Soul Wall Torch", BlockShape.WallTorch(Facing.South, Surfaces.SoulFlame), 10, BlockId.SoulTorch),
            Shaped(BlockId.SoulWallTorchWest, "soul_wall_torch_west", "Soul Wall Torch", BlockShape.WallTorch(Facing.West, Surfaces.SoulFlame), 10, BlockId.SoulTorch),
            Shaped(BlockId.SoulWallTorchEast, "soul_wall_torch_east", "Soul Wall Torch", BlockShape.WallTorch(Facing.East, Surfaces.SoulFlame), 10, BlockId.SoulTorch),
            Shaped(BlockId.Lantern, "lantern", "Lantern", BlockShape.Lantern(Surfaces.LanternGlass), 15),
            Shaped(BlockId.HangingLantern, "hanging_lantern", "Lantern", BlockShape.HangingLantern(Surfaces.LanternGlass), 15, BlockId.Lantern),
            Shaped(BlockId.SoulLantern, "soul_lantern", "Soul Lantern", BlockShape.Lantern(Surfaces.SoulLanternGlass), 10),
            Shaped(BlockId.SoulHangingLantern, "soul_hanging_lantern", "Soul Lantern", BlockShape.HangingLantern(Surfaces.SoulLanternGlass), 10, BlockId.SoulLantern),
            Shaped(BlockId.EndRod, "end_rod", "End Rod", BlockShape.EndRod(Facing.Up), 14),
            Shaped(BlockId.EndRodDown, "end_rod_down", "End Rod", BlockShape.EndRod(Facing.Down), 14, BlockId.EndRod),
            Shaped(BlockId.EndRodNorth, "end_rod_north", "End Rod", BlockShape.EndRod(Facing.North), 14, BlockId.EndRod),
            Shaped(BlockId.EndRodSouth, "end_rod_south", "End Rod", BlockShape.EndRod(Facing.South), 14, BlockId.EndRod),
            Shaped(BlockId.EndRodWest, "end_rod_west", "End Rod", BlockShape.EndRod(Facing.West), 14, BlockId.EndRod),
            Shaped(BlockId.EndRodEast, "end_rod_east", "End Rod", BlockShape.EndRod(Facing.East), 14, BlockId.EndRod),
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

    // The block each item places facing each way, or air where it has no such block.
    private static readonly BlockId[,] _variants = Variants();

    private static BlockId[,] Variants()
    {
        var variants = new BlockId[_all.Length, 6];
        foreach (var block in _all)
            if (block.Shape is { } shape) variants[(int)block.Item, (int)shape.Facing] = block.Id;
        return variants;
    }

    /// <summary>Whether a block hides the faces beside it, stops light and darkens the corners around it.</summary>
    public static bool IsOpaque(BlockId id) => _opaque[(int)id];

    /// <summary>Whether a body stands on and walks into a block, which it does all but air and water.</summary>
    public static bool Collides(BlockId id) => _collides[(int)id];

    /// <summary>Whether a block is drawn see-through, in its section's second mesh.</summary>
    public static bool IsSeeThrough(BlockId id) => _seeThrough[(int)id];

    /// <summary>Whether a block smaller than its cell can stand on, hang from or be fixed to a block, which it can on any whole block but water.</summary>
    public static bool IsSturdy(BlockId id) => _collides[(int)id];

    /// <summary>The block an item places facing a way, or air where it is not placed that way.</summary>
    public static BlockId Variant(BlockId item, Facing facing) => _variants[(int)item, (int)facing];

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
