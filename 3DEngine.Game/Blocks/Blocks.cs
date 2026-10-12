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
    DirtPath,
    Farmland,
    SprucePlanks,
    HayBale,
    OakFence,
    Wheat,
    StoneBricks,
    StoneSlab,
    StoneSlabTop,
    CobblestoneSlab,
    CobblestoneSlabTop,
    OakSlab,
    OakSlabTop,
    SpruceSlab,
    SpruceSlabTop,
    SandstoneSlab,
    SandstoneSlabTop,
    CobblestoneStairs,
    CobblestoneStairsEast,
    CobblestoneStairsSouth,
    CobblestoneStairsWest,
    CobblestoneStairsTop,
    CobblestoneStairsEastTop,
    CobblestoneStairsSouthTop,
    CobblestoneStairsWestTop,
    OakStairs,
    OakStairsEast,
    OakStairsSouth,
    OakStairsWest,
    OakStairsTop,
    OakStairsEastTop,
    OakStairsSouthTop,
    OakStairsWestTop,
    SpruceStairs,
    SpruceStairsEast,
    SpruceStairsSouth,
    SpruceStairsWest,
    SpruceStairsTop,
    SpruceStairsEastTop,
    SpruceStairsSouthTop,
    SpruceStairsWestTop,
    SandstoneStairs,
    SandstoneStairsEast,
    SandstoneStairsSouth,
    SandstoneStairsWest,
    SandstoneStairsTop,
    SandstoneStairsEastTop,
    SandstoneStairsSouthTop,
    SandstoneStairsWestTop,
    StoneBrickStairs,
    StoneBrickStairsEast,
    StoneBrickStairsSouth,
    StoneBrickStairsWest,
    StoneBrickStairsTop,
    StoneBrickStairsEastTop,
    StoneBrickStairsSouthTop,
    StoneBrickStairsWestTop,
    OakDoor,
    OakDoorEast,
    OakDoorSouth,
    OakDoorWest,
    OakDoorTop,
    OakDoorEastTop,
    OakDoorSouthTop,
    OakDoorWestTop,
    OakDoorOpen,
    OakDoorEastOpen,
    OakDoorSouthOpen,
    OakDoorWestOpen,
    OakDoorTopOpen,
    OakDoorEastTopOpen,
    OakDoorSouthTopOpen,
    OakDoorWestTopOpen,
    Water1,
    Water2,
    Water3,
    Water4,
    Water5,
    Water6,
    Water7,
    FallingWater,
    WhiteWool,
    LightGrayWool,
    GrayWool,
    BlackWool,
    BrownWool,
    PinkWool,
    BirchPlanks,
    CraftingTable,
    CoalOre,
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
/// of a block smaller than its cell, the block a player holds to place it where that is another,
/// as a torch is held to place a torch on a wall, the whole block two of a slab make, and the block
/// a door becomes when it is opened or shut.
/// </summary>
public sealed record BlockInfo(BlockId Id, string Key, string Name, int Top, int Side, int Bottom, bool Breakable = true, int Light = 0,
    BlockKind Kind = BlockKind.Opaque, BlockShape? Shape = null, BlockId? HeldAs = null, BlockId? Doubled = null, BlockId? Toggled = null)
{
    /// <summary>The boxes that stop the player: the whole cell for a solid block, a solid shape's own, and none for air, water and the rest.</summary>
    public IReadOnlyList<Box> Boxes { get; } = Kind is BlockKind.Opaque or BlockKind.Glass ? [Box.Cell] : Shape?.Collision ?? [];

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

        // A half of a door, which opening or shutting turns into its other way.
        BlockInfo Door(BlockId id, string key, BlockShape shape, BlockId toggled, BlockId? heldAs = null) =>
            new(id, key, "Oak Door", Surfaces.DoorWood, Surfaces.DoorWood, Surfaces.DoorWood, Kind: BlockKind.Shaped, Shape: shape, HeldAs: heldAs, Toggled: toggled);

        // A block smaller than its cell shows the surface of the piece that glows on its hotbar slot.
        BlockInfo Shaped(BlockId id, string key, string name, BlockShape shape, int light, BlockId? heldAs = null, BlockId? doubled = null)
        {
            var look = shape.Pieces.FirstOrDefault(p => p.Glows, shape.Pieces[0]).Surface;
            return new(id, key, name, look, look, look, Light: light, Kind: BlockKind.Shaped, Shape: shape, HeldAs: heldAs, Doubled: doubled);
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
            new(BlockId.DirtPath, "dirt_path", "Dirt Path", Surfaces.DirtPath, Surfaces.Dirt, Surfaces.Dirt),
            new(BlockId.Farmland, "farmland", "Farmland", Surfaces.Farmland, Surfaces.Dirt, Surfaces.Dirt),
            Same(BlockId.SprucePlanks, "spruce_planks", "Spruce Planks", Surfaces.SprucePlanks),
            new(BlockId.HayBale, "hay_block", "Hay Bale", Surfaces.HayTop, Surfaces.Hay, Surfaces.HayTop),
            Shaped(BlockId.OakFence, "oak_fence", "Oak Fence", BlockShape.Fence(Surfaces.Planks, 0), 0),
            Shaped(BlockId.Wheat, "wheat", "Wheat", BlockShape.Wheat(Surfaces.Wheat), 0),
            Same(BlockId.StoneBricks, "stone_bricks", "Stone Bricks", Surfaces.StoneBricks),
            Shaped(BlockId.StoneSlab, "stone_slab", "Stone Slab", BlockShape.Slab(Surfaces.Stone, false), 0, doubled: BlockId.Stone),
            Shaped(BlockId.StoneSlabTop, "stone_slab_top", "Stone Slab", BlockShape.Slab(Surfaces.Stone, true), 0, BlockId.StoneSlab, BlockId.Stone),
            Shaped(BlockId.CobblestoneSlab, "cobblestone_slab", "Cobblestone Slab", BlockShape.Slab(Surfaces.Cobblestone, false), 0, doubled: BlockId.Cobblestone),
            Shaped(BlockId.CobblestoneSlabTop, "cobblestone_slab_top", "Cobblestone Slab", BlockShape.Slab(Surfaces.Cobblestone, true), 0, BlockId.CobblestoneSlab, BlockId.Cobblestone),
            Shaped(BlockId.OakSlab, "oak_slab", "Oak Slab", BlockShape.Slab(Surfaces.Planks, false), 0, doubled: BlockId.OakPlanks),
            Shaped(BlockId.OakSlabTop, "oak_slab_top", "Oak Slab", BlockShape.Slab(Surfaces.Planks, true), 0, BlockId.OakSlab, BlockId.OakPlanks),
            Shaped(BlockId.SpruceSlab, "spruce_slab", "Spruce Slab", BlockShape.Slab(Surfaces.SprucePlanks, false), 0, doubled: BlockId.SprucePlanks),
            Shaped(BlockId.SpruceSlabTop, "spruce_slab_top", "Spruce Slab", BlockShape.Slab(Surfaces.SprucePlanks, true), 0, BlockId.SpruceSlab, BlockId.SprucePlanks),
            Shaped(BlockId.SandstoneSlab, "sandstone_slab", "Sandstone Slab", BlockShape.Slab(Surfaces.Sandstone, false), 0, doubled: BlockId.Sandstone),
            Shaped(BlockId.SandstoneSlabTop, "sandstone_slab_top", "Sandstone Slab", BlockShape.Slab(Surfaces.Sandstone, true), 0, BlockId.SandstoneSlab, BlockId.Sandstone),
            Shaped(BlockId.CobblestoneStairs, "cobblestone_stairs", "Cobblestone Stairs", BlockShape.Stairs(Surfaces.Cobblestone, Facing.North, false), 0),
            Shaped(BlockId.CobblestoneStairsEast, "cobblestone_stairs_east", "Cobblestone Stairs", BlockShape.Stairs(Surfaces.Cobblestone, Facing.East, false), 0, BlockId.CobblestoneStairs),
            Shaped(BlockId.CobblestoneStairsSouth, "cobblestone_stairs_south", "Cobblestone Stairs", BlockShape.Stairs(Surfaces.Cobblestone, Facing.South, false), 0, BlockId.CobblestoneStairs),
            Shaped(BlockId.CobblestoneStairsWest, "cobblestone_stairs_west", "Cobblestone Stairs", BlockShape.Stairs(Surfaces.Cobblestone, Facing.West, false), 0, BlockId.CobblestoneStairs),
            Shaped(BlockId.CobblestoneStairsTop, "cobblestone_stairs_north_top", "Cobblestone Stairs", BlockShape.Stairs(Surfaces.Cobblestone, Facing.North, true), 0, BlockId.CobblestoneStairs),
            Shaped(BlockId.CobblestoneStairsEastTop, "cobblestone_stairs_east_top", "Cobblestone Stairs", BlockShape.Stairs(Surfaces.Cobblestone, Facing.East, true), 0, BlockId.CobblestoneStairs),
            Shaped(BlockId.CobblestoneStairsSouthTop, "cobblestone_stairs_south_top", "Cobblestone Stairs", BlockShape.Stairs(Surfaces.Cobblestone, Facing.South, true), 0, BlockId.CobblestoneStairs),
            Shaped(BlockId.CobblestoneStairsWestTop, "cobblestone_stairs_west_top", "Cobblestone Stairs", BlockShape.Stairs(Surfaces.Cobblestone, Facing.West, true), 0, BlockId.CobblestoneStairs),
            Shaped(BlockId.OakStairs, "oak_stairs", "Oak Stairs", BlockShape.Stairs(Surfaces.Planks, Facing.North, false), 0),
            Shaped(BlockId.OakStairsEast, "oak_stairs_east", "Oak Stairs", BlockShape.Stairs(Surfaces.Planks, Facing.East, false), 0, BlockId.OakStairs),
            Shaped(BlockId.OakStairsSouth, "oak_stairs_south", "Oak Stairs", BlockShape.Stairs(Surfaces.Planks, Facing.South, false), 0, BlockId.OakStairs),
            Shaped(BlockId.OakStairsWest, "oak_stairs_west", "Oak Stairs", BlockShape.Stairs(Surfaces.Planks, Facing.West, false), 0, BlockId.OakStairs),
            Shaped(BlockId.OakStairsTop, "oak_stairs_north_top", "Oak Stairs", BlockShape.Stairs(Surfaces.Planks, Facing.North, true), 0, BlockId.OakStairs),
            Shaped(BlockId.OakStairsEastTop, "oak_stairs_east_top", "Oak Stairs", BlockShape.Stairs(Surfaces.Planks, Facing.East, true), 0, BlockId.OakStairs),
            Shaped(BlockId.OakStairsSouthTop, "oak_stairs_south_top", "Oak Stairs", BlockShape.Stairs(Surfaces.Planks, Facing.South, true), 0, BlockId.OakStairs),
            Shaped(BlockId.OakStairsWestTop, "oak_stairs_west_top", "Oak Stairs", BlockShape.Stairs(Surfaces.Planks, Facing.West, true), 0, BlockId.OakStairs),
            Shaped(BlockId.SpruceStairs, "spruce_stairs", "Spruce Stairs", BlockShape.Stairs(Surfaces.SprucePlanks, Facing.North, false), 0),
            Shaped(BlockId.SpruceStairsEast, "spruce_stairs_east", "Spruce Stairs", BlockShape.Stairs(Surfaces.SprucePlanks, Facing.East, false), 0, BlockId.SpruceStairs),
            Shaped(BlockId.SpruceStairsSouth, "spruce_stairs_south", "Spruce Stairs", BlockShape.Stairs(Surfaces.SprucePlanks, Facing.South, false), 0, BlockId.SpruceStairs),
            Shaped(BlockId.SpruceStairsWest, "spruce_stairs_west", "Spruce Stairs", BlockShape.Stairs(Surfaces.SprucePlanks, Facing.West, false), 0, BlockId.SpruceStairs),
            Shaped(BlockId.SpruceStairsTop, "spruce_stairs_north_top", "Spruce Stairs", BlockShape.Stairs(Surfaces.SprucePlanks, Facing.North, true), 0, BlockId.SpruceStairs),
            Shaped(BlockId.SpruceStairsEastTop, "spruce_stairs_east_top", "Spruce Stairs", BlockShape.Stairs(Surfaces.SprucePlanks, Facing.East, true), 0, BlockId.SpruceStairs),
            Shaped(BlockId.SpruceStairsSouthTop, "spruce_stairs_south_top", "Spruce Stairs", BlockShape.Stairs(Surfaces.SprucePlanks, Facing.South, true), 0, BlockId.SpruceStairs),
            Shaped(BlockId.SpruceStairsWestTop, "spruce_stairs_west_top", "Spruce Stairs", BlockShape.Stairs(Surfaces.SprucePlanks, Facing.West, true), 0, BlockId.SpruceStairs),
            Shaped(BlockId.SandstoneStairs, "sandstone_stairs", "Sandstone Stairs", BlockShape.Stairs(Surfaces.Sandstone, Facing.North, false), 0),
            Shaped(BlockId.SandstoneStairsEast, "sandstone_stairs_east", "Sandstone Stairs", BlockShape.Stairs(Surfaces.Sandstone, Facing.East, false), 0, BlockId.SandstoneStairs),
            Shaped(BlockId.SandstoneStairsSouth, "sandstone_stairs_south", "Sandstone Stairs", BlockShape.Stairs(Surfaces.Sandstone, Facing.South, false), 0, BlockId.SandstoneStairs),
            Shaped(BlockId.SandstoneStairsWest, "sandstone_stairs_west", "Sandstone Stairs", BlockShape.Stairs(Surfaces.Sandstone, Facing.West, false), 0, BlockId.SandstoneStairs),
            Shaped(BlockId.SandstoneStairsTop, "sandstone_stairs_north_top", "Sandstone Stairs", BlockShape.Stairs(Surfaces.Sandstone, Facing.North, true), 0, BlockId.SandstoneStairs),
            Shaped(BlockId.SandstoneStairsEastTop, "sandstone_stairs_east_top", "Sandstone Stairs", BlockShape.Stairs(Surfaces.Sandstone, Facing.East, true), 0, BlockId.SandstoneStairs),
            Shaped(BlockId.SandstoneStairsSouthTop, "sandstone_stairs_south_top", "Sandstone Stairs", BlockShape.Stairs(Surfaces.Sandstone, Facing.South, true), 0, BlockId.SandstoneStairs),
            Shaped(BlockId.SandstoneStairsWestTop, "sandstone_stairs_west_top", "Sandstone Stairs", BlockShape.Stairs(Surfaces.Sandstone, Facing.West, true), 0, BlockId.SandstoneStairs),
            Shaped(BlockId.StoneBrickStairs, "stone_brick_stairs", "Stone Brick Stairs", BlockShape.Stairs(Surfaces.StoneBricks, Facing.North, false), 0),
            Shaped(BlockId.StoneBrickStairsEast, "stone_brick_stairs_east", "Stone Brick Stairs", BlockShape.Stairs(Surfaces.StoneBricks, Facing.East, false), 0, BlockId.StoneBrickStairs),
            Shaped(BlockId.StoneBrickStairsSouth, "stone_brick_stairs_south", "Stone Brick Stairs", BlockShape.Stairs(Surfaces.StoneBricks, Facing.South, false), 0, BlockId.StoneBrickStairs),
            Shaped(BlockId.StoneBrickStairsWest, "stone_brick_stairs_west", "Stone Brick Stairs", BlockShape.Stairs(Surfaces.StoneBricks, Facing.West, false), 0, BlockId.StoneBrickStairs),
            Shaped(BlockId.StoneBrickStairsTop, "stone_brick_stairs_north_top", "Stone Brick Stairs", BlockShape.Stairs(Surfaces.StoneBricks, Facing.North, true), 0, BlockId.StoneBrickStairs),
            Shaped(BlockId.StoneBrickStairsEastTop, "stone_brick_stairs_east_top", "Stone Brick Stairs", BlockShape.Stairs(Surfaces.StoneBricks, Facing.East, true), 0, BlockId.StoneBrickStairs),
            Shaped(BlockId.StoneBrickStairsSouthTop, "stone_brick_stairs_south_top", "Stone Brick Stairs", BlockShape.Stairs(Surfaces.StoneBricks, Facing.South, true), 0, BlockId.StoneBrickStairs),
            Shaped(BlockId.StoneBrickStairsWestTop, "stone_brick_stairs_west_top", "Stone Brick Stairs", BlockShape.Stairs(Surfaces.StoneBricks, Facing.West, true), 0, BlockId.StoneBrickStairs),
            Door(BlockId.OakDoor, "oak_door", BlockShape.Door(Surfaces.DoorWood, Facing.North, false, false), BlockId.OakDoorOpen),
            Door(BlockId.OakDoorEast, "oak_door_east", BlockShape.Door(Surfaces.DoorWood, Facing.East, false, false), BlockId.OakDoorEastOpen, BlockId.OakDoor),
            Door(BlockId.OakDoorSouth, "oak_door_south", BlockShape.Door(Surfaces.DoorWood, Facing.South, false, false), BlockId.OakDoorSouthOpen, BlockId.OakDoor),
            Door(BlockId.OakDoorWest, "oak_door_west", BlockShape.Door(Surfaces.DoorWood, Facing.West, false, false), BlockId.OakDoorWestOpen, BlockId.OakDoor),
            Door(BlockId.OakDoorTop, "oak_door_north_top", BlockShape.Door(Surfaces.DoorWood, Facing.North, true, false), BlockId.OakDoorTopOpen, BlockId.OakDoor),
            Door(BlockId.OakDoorEastTop, "oak_door_east_top", BlockShape.Door(Surfaces.DoorWood, Facing.East, true, false), BlockId.OakDoorEastTopOpen, BlockId.OakDoor),
            Door(BlockId.OakDoorSouthTop, "oak_door_south_top", BlockShape.Door(Surfaces.DoorWood, Facing.South, true, false), BlockId.OakDoorSouthTopOpen, BlockId.OakDoor),
            Door(BlockId.OakDoorWestTop, "oak_door_west_top", BlockShape.Door(Surfaces.DoorWood, Facing.West, true, false), BlockId.OakDoorWestTopOpen, BlockId.OakDoor),
            Door(BlockId.OakDoorOpen, "oak_door_north_open", BlockShape.Door(Surfaces.DoorWood, Facing.North, false, true), BlockId.OakDoor, BlockId.OakDoor),
            Door(BlockId.OakDoorEastOpen, "oak_door_east_open", BlockShape.Door(Surfaces.DoorWood, Facing.East, false, true), BlockId.OakDoorEast, BlockId.OakDoor),
            Door(BlockId.OakDoorSouthOpen, "oak_door_south_open", BlockShape.Door(Surfaces.DoorWood, Facing.South, false, true), BlockId.OakDoorSouth, BlockId.OakDoor),
            Door(BlockId.OakDoorWestOpen, "oak_door_west_open", BlockShape.Door(Surfaces.DoorWood, Facing.West, false, true), BlockId.OakDoorWest, BlockId.OakDoor),
            Door(BlockId.OakDoorTopOpen, "oak_door_north_top_open", BlockShape.Door(Surfaces.DoorWood, Facing.North, true, true), BlockId.OakDoorTop, BlockId.OakDoor),
            Door(BlockId.OakDoorEastTopOpen, "oak_door_east_top_open", BlockShape.Door(Surfaces.DoorWood, Facing.East, true, true), BlockId.OakDoorEastTop, BlockId.OakDoor),
            Door(BlockId.OakDoorSouthTopOpen, "oak_door_south_top_open", BlockShape.Door(Surfaces.DoorWood, Facing.South, true, true), BlockId.OakDoorSouthTop, BlockId.OakDoor),
            Door(BlockId.OakDoorWestTopOpen, "oak_door_west_top_open", BlockShape.Door(Surfaces.DoorWood, Facing.West, true, true), BlockId.OakDoorWestTop, BlockId.OakDoor),
            Same(BlockId.Water1, "water_1", "Flowing Water", Surfaces.Water, kind: BlockKind.Water) with { HeldAs = BlockId.Water },
            Same(BlockId.Water2, "water_2", "Flowing Water", Surfaces.Water, kind: BlockKind.Water) with { HeldAs = BlockId.Water },
            Same(BlockId.Water3, "water_3", "Flowing Water", Surfaces.Water, kind: BlockKind.Water) with { HeldAs = BlockId.Water },
            Same(BlockId.Water4, "water_4", "Flowing Water", Surfaces.Water, kind: BlockKind.Water) with { HeldAs = BlockId.Water },
            Same(BlockId.Water5, "water_5", "Flowing Water", Surfaces.Water, kind: BlockKind.Water) with { HeldAs = BlockId.Water },
            Same(BlockId.Water6, "water_6", "Flowing Water", Surfaces.Water, kind: BlockKind.Water) with { HeldAs = BlockId.Water },
            Same(BlockId.Water7, "water_7", "Flowing Water", Surfaces.Water, kind: BlockKind.Water) with { HeldAs = BlockId.Water },
            Same(BlockId.FallingWater, "falling_water", "Falling Water", Surfaces.Water, kind: BlockKind.Water) with { HeldAs = BlockId.Water },
            Same(BlockId.WhiteWool, "white_wool", "White Wool", Surfaces.WhiteWool),
            Same(BlockId.LightGrayWool, "light_gray_wool", "Light Gray Wool", Surfaces.LightGrayWool),
            Same(BlockId.GrayWool, "gray_wool", "Gray Wool", Surfaces.GrayWool),
            Same(BlockId.BlackWool, "black_wool", "Black Wool", Surfaces.BlackWool),
            Same(BlockId.BrownWool, "brown_wool", "Brown Wool", Surfaces.BrownWool),
            Same(BlockId.PinkWool, "pink_wool", "Pink Wool", Surfaces.PinkWool),
            Same(BlockId.BirchPlanks, "birch_planks", "Birch Planks", Surfaces.BirchPlanks),
            new(BlockId.CraftingTable, "crafting_table", "Crafting Table", Surfaces.CraftingTableTop, Surfaces.CraftingTableSide, Surfaces.Planks),
            Same(BlockId.CoalOre, "coal_ore", "Coal Ore", Surfaces.CoalOre),
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

    // The block each item places facing each way in each half, or air where it has no such block.
    private static readonly BlockId[,,] _variants = Variants();

    private static BlockId[,,] Variants()
    {
        var variants = new BlockId[_all.Length, 6, 2];
        foreach (var block in _all)
            if (block.Shape is { Open: false } shape) variants[(int)block.Item, (int)shape.Facing, shape.Upper ? 1 : 0] = block.Id;
        return variants;
    }

    // A fence's shape for each set of the sides it joins, north 1, east 2, south 4 and west 8.
    private static readonly BlockShape[] _fences = [.. Enumerable.Range(0, 16).Select(arms => BlockShape.Fence(Surfaces.Planks, arms))];

    /// <summary>The shape of a fence joined to the sides whose bits are set, north 1, east 2, south 4 and west 8.</summary>
    public static BlockShape Fence(int arms) => _fences[arms];

    /// <summary>Whether a fence beside a block joins it, as it joins a fence or a whole block.</summary>
    public static bool JoinsFence(BlockId neighbor) => neighbor == BlockId.OakFence || IsSturdy(neighbor);

    /// <summary>Whether a block hides the faces beside it, stops light and darkens the corners around it.</summary>
    public static bool IsOpaque(BlockId id) => _opaque[(int)id];

    /// <summary>Whether a body stands on and walks into a block, which it does all but air and water.</summary>
    public static bool Collides(BlockId id) => _collides[(int)id];

    /// <summary>Whether a block is drawn see-through, in its section's second mesh.</summary>
    public static bool IsSeeThrough(BlockId id) => _seeThrough[(int)id];

    /// <summary>Whether a block smaller than its cell can stand on, hang from or be fixed to a block, which it can on any whole block but water.</summary>
    public static bool IsSturdy(BlockId id) => _collides[(int)id];

    /// <summary>Whether a block smaller than its cell facing a way holds to a block, a whole one or the top of a fence, as a lantern stands on a lamp post.</summary>
    public static bool Holds(BlockId support, Facing facing) => IsSturdy(support) || facing == Facing.Up && support == BlockId.OakFence;

    /// <summary>The block an item places facing a way, in the upper half where asked, or air where it is not placed that way.</summary>
    public static BlockId Variant(BlockId item, Facing facing, bool upper = false) => _variants[(int)item, (int)facing, upper ? 1 : 0];

    /// <summary>Whether the crosshair rests on a block, which it does on all but air and water, as Minecraft's does.</summary>
    public static bool IsTarget(BlockId id) => id != BlockId.Air && !IsWater(id);

    /// <summary>Whether a block is water, still, flowing or falling.</summary>
    public static bool IsWater(BlockId id) => id == BlockId.Water || id >= BlockId.Water1 && id <= BlockId.FallingWater;

    /// <summary>
    /// How far water has flowed from its source, as Minecraft's level: 0 for a source, 1 to 7 for
    /// flowing water a block further each, and 0 for falling water, which spreads as from a source
    /// where it lands.
    /// </summary>
    public static int WaterLevel(BlockId id) => id is >= BlockId.Water1 and <= BlockId.Water7 ? id - BlockId.Water1 + 1 : 0;

    /// <summary>Flowing water of a level from 1 to 7.</summary>
    public static BlockId FlowingWater(int level) => (BlockId)((int)BlockId.Water1 + level - 1);

    /// <summary>
    /// The height of water's top in its cell: a source's an eighth low, flowing water's lower for each
    /// block it has flowed, and the whole cell under more water or falling.
    /// </summary>
    public static float WaterHeight(BlockId id, bool waterAbove) =>
        waterAbove || id == BlockId.FallingWater ? 1 : id == BlockId.Water ? 0.875f : (8 - WaterLevel(id)) / 9f;

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
