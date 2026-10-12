namespace Engine.Game;

/// <summary>
/// What breaking a block in survival and killing a mob give, and how long a block takes to break,
/// as Minecraft's do.
/// </summary>
/// <remarks>
/// A block takes its hardness times one and a half seconds to break, divided by the speed of the
/// tool held where it is of the kind the block is for, as a pickaxe is for stone and an axe for
/// wood. A block that asks for a pickaxe, as stone, coal and concrete do, takes its hardness times
/// five seconds without one and drops nothing, as in Minecraft.
/// </remarks>
public static class Loot
{
    /// <summary>
    /// The items a block broken in survival drops with an item held: grass, farmland and a path their
    /// dirt, stone its cobblestone, coal ore its coal, oak leaves an apple now and then, other leaves,
    /// glass and ice nothing, wheat its wheat and a few seeds, any other block the item that places it,
    /// and a block that asks for a pickaxe broken without one nothing.
    /// </summary>
    public static IEnumerable<(Item Item, int Count)> Of(BlockId block, Random random, Item? held = null)
    {
        if (!Harvests(block, held)) yield break;
        var item = Blocks.Get(block).Item;
        switch (item)
        {
            case BlockId.OakLeaves:
                // An apple now and then, one in two hundred, as Minecraft's oak leaves drop.
                if (random.Next(200) == 0) yield return (Items.Apple, 1);
                yield break;
            case BlockId.Air or BlockId.Water or BlockId.Bedrock or BlockId.BirchLeaves or BlockId.SpruceLeaves or BlockId.Glass or BlockId.Ice:
                yield break;
            case BlockId.Grass or BlockId.Farmland or BlockId.DirtPath:
                yield return (Items.Of(BlockId.Dirt), 1);
                yield break;
            case BlockId.Stone:
                yield return (Items.Of(BlockId.Cobblestone), 1);
                yield break;
            case BlockId.CoalOre:
                yield return (Items.Coal, 1);
                yield break;
            case BlockId.Wheat:
                yield return (Items.Wheat, 1);
                yield return (Items.Of(BlockId.Wheat), 1 + random.Next(3));
                yield break;
            default:
                yield return (Items.Of(item), 1);
                yield break;
        }
    }

    // The wool a sheep drops by its variant, the colors of its coat in order.
    private static readonly BlockId[] Wool = [BlockId.WhiteWool, BlockId.LightGrayWool, BlockId.GrayWool, BlockId.BlackWool, BlockId.BrownWool, BlockId.PinkWool];

    /// <summary>
    /// The items a mob drops as it dies: a pig one to three porkchops, a cow up to two leather and one
    /// to three beef, a sheep a block of its wool and one or two mutton, a chicken up to two feathers
    /// and its meat, and a villager nothing.
    /// </summary>
    public static IEnumerable<(Item Item, int Count)> Of(Mob mob, Random random)
    {
        (Item, int)[] drops = mob.Kind.Name switch
        {
            "pig" => [(Items.RawPorkchop, 1 + random.Next(3))],
            "cow" => [(Items.Leather, random.Next(3)), (Items.RawBeef, 1 + random.Next(3))],
            "sheep" => [(Items.Of(Wool[mob.Variant % Wool.Length]), 1), (Items.RawMutton, 1 + random.Next(2))],
            "chicken" => [(Items.Feather, random.Next(3)), (Items.RawChicken, 1)],
            _ => [],
        };
        return drops.Where(d => d.Item2 > 0);
    }

    /// <summary>Seconds a block takes to break in survival with an item held, or by hand, 0 for one that breaks at once.</summary>
    public static float BreakSeconds(BlockId block, Item? held = null)
    {
        var (tool, _) = ToolFor(block);
        var speed = tool != ToolKind.None && held?.Tool == tool ? held.Speed : 1;
        return Hardness(block) * (Harvests(block, held) ? 1.5f : 5) / speed;
    }

    /// <summary>Whether a block broken with an item held, or by hand, drops anything, which a block that asks for a pickaxe does only to one.</summary>
    public static bool Harvests(BlockId block, Item? held) => ToolFor(block) is not (ToolKind.Pickaxe, true) || held?.Tool == ToolKind.Pickaxe;

    /// <summary>The kind of tool a block breaks fastest to, and whether it drops anything broken without one, as Minecraft's stone does not.</summary>
    public static (ToolKind Tool, bool Needed) ToolFor(BlockId block) => Blocks.Get(block).Item switch
    {
        BlockId.Stone or BlockId.Cobblestone or BlockId.CoalOre or BlockId.Sandstone or BlockId.StoneBricks or BlockId.Magma
            or BlockId.WhiteConcrete or BlockId.RedConcrete or BlockId.GreenConcrete or BlockId.BlueConcrete
            or BlockId.Lantern or BlockId.SoulLantern or BlockId.StoneSlab or BlockId.CobblestoneSlab or BlockId.SandstoneSlab
            or BlockId.CobblestoneStairs or BlockId.SandstoneStairs or BlockId.StoneBrickStairs => (ToolKind.Pickaxe, true),
        BlockId.Ice => (ToolKind.Pickaxe, false),
        BlockId.OakLog or BlockId.BirchLog or BlockId.SpruceLog or BlockId.OakPlanks or BlockId.SprucePlanks or BlockId.BirchPlanks
            or BlockId.CraftingTable or BlockId.OakFence or BlockId.OakDoor or BlockId.OakSlab or BlockId.SpruceSlab
            or BlockId.OakStairs or BlockId.SpruceStairs => (ToolKind.Axe, false),
        BlockId.Dirt or BlockId.Grass or BlockId.Sand or BlockId.Gravel or BlockId.Snow or BlockId.Farmland or BlockId.DirtPath => (ToolKind.Shovel, false),
        _ => (ToolKind.None, false),
    };

    /// <summary>A block's hardness, Minecraft's, by the item that places it.</summary>
    public static float Hardness(BlockId block) => Blocks.Get(block).Item switch
    {
        BlockId.Torch or BlockId.SoulTorch or BlockId.EndRod or BlockId.Wheat => 0,
        BlockId.OakLeaves or BlockId.BirchLeaves or BlockId.SpruceLeaves or BlockId.Snow => 0.2f,
        BlockId.Glass or BlockId.Glowstone or BlockId.SeaLantern => 0.3f,
        BlockId.Cactus => 0.4f,
        BlockId.Dirt or BlockId.Sand or BlockId.Ice or BlockId.Magma or BlockId.HayBale => 0.5f,
        BlockId.Grass or BlockId.Gravel or BlockId.Farmland => 0.6f,
        BlockId.DirtPath => 0.65f,
        BlockId.Sandstone or BlockId.SandstoneStairs or BlockId.WhiteWool or BlockId.LightGrayWool or BlockId.GrayWool
            or BlockId.BlackWool or BlockId.BrownWool or BlockId.PinkWool => 0.8f,
        BlockId.Shroomlight => 1,
        BlockId.Stone or BlockId.StoneBricks or BlockId.StoneBrickStairs => 1.5f,
        BlockId.CraftingTable => 2.5f,
        BlockId.CoalOre => 3,
        BlockId.WhiteConcrete or BlockId.RedConcrete or BlockId.GreenConcrete or BlockId.BlueConcrete => 1.8f,
        BlockId.OakDoor => 3,
        BlockId.Lantern or BlockId.SoulLantern => 3.5f,
        // Cobblestone, logs, planks, fences, and slabs and stairs of wood and stone.
        _ => 2,
    };
}
