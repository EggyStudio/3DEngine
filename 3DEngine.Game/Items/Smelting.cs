namespace Engine.Game;

/// <summary>
/// What a furnace makes of what it is given and how long each fuel burns, as Minecraft's furnace
/// does: ten seconds an item, raw meat cooked, sand to glass, cobblestone to stone, iron ore to an
/// ingot and a log to charcoal, burning coal or charcoal 80 seconds, a log, planks or a crafting
/// table 15, a wooden tool 10, a slab 7.5 and a stick 5.
/// </summary>
public static class Smelting
{
    /// <summary>Seconds an item takes to smelt.</summary>
    public const float Seconds = 10;

    private static Item I(BlockId block) => Items.Of(block);

    private static readonly Dictionary<Item, Item> _results = new()
    {
        [Items.RawPorkchop] = Items.Find("cooked_porkchop")!,
        [Items.RawBeef] = Items.Find("steak")!,
        [Items.RawMutton] = Items.Find("cooked_mutton")!,
        [Items.RawChicken] = Items.Find("cooked_chicken")!,
        [I(BlockId.Sand)] = I(BlockId.Glass),
        [I(BlockId.Cobblestone)] = I(BlockId.Stone),
        [I(BlockId.IronOre)] = Items.IronIngot,
        [I(BlockId.OakLog)] = Items.Charcoal,
        [I(BlockId.BirchLog)] = Items.Charcoal,
        [I(BlockId.SpruceLog)] = Items.Charcoal,
    };

    /// <summary>What an item smelts into, or null for one that does not smelt.</summary>
    public static Item? Result(Item item) => _results.GetValueOrDefault(item);

    /// <summary>Seconds an item burns as fuel, or 0 for one that does not burn.</summary>
    public static float Burns(Item item) => item.Block switch
    {
        _ when item == Items.Coal || item == Items.Charcoal => 80,
        _ when item == Items.Stick => 5,
        _ when item.Tool != ToolKind.None && item.Key.StartsWith("wooden_", StringComparison.Ordinal) => 10,
        BlockId.OakLog or BlockId.BirchLog or BlockId.SpruceLog or BlockId.OakPlanks or BlockId.SprucePlanks or BlockId.BirchPlanks
            or BlockId.CraftingTable or BlockId.OakFence or BlockId.OakStairs or BlockId.SpruceStairs => 15,
        BlockId.OakSlab or BlockId.SpruceSlab => 7.5f,
        _ => 0,
    };
}

/// <summary>
/// A furnace's three slots, what it smelts, its fuel and what it made, with the fuel burning in it
/// and how far the item in it is smelted.
/// </summary>
/// <remarks>
/// It burns a new fuel when the last has burned out and there is an item to smelt with room for what
/// it makes, so fuel is not wasted on nothing, and an item's smelting goes back to the start when
/// the fire goes out, as Minecraft's furnace's does.
/// </remarks>
public sealed class Furnace
{
    public const int Input = 0, Fuel = 1, Output = 2;

    /// <summary>Its slots: what it smelts, its fuel, and what it made.</summary>
    public readonly ItemStack?[] Slots = new ItemStack?[3];

    /// <summary>Seconds left of the fuel burning, and seconds that fuel burns in all.</summary>
    public float Burning { get; set; }

    public float BurnTotal { get; set; }

    /// <summary>Seconds the item in it has smelted, out of <see cref="Smelting.Seconds"/>.</summary>
    public float Cooked { get; set; }

    public bool Lit => Burning > 0;

    // Whether the item in it smelts into something the output has room for.
    private bool CanSmelt()
    {
        if (Slots[Input] is not { } input || Smelting.Result(input.Item) is not { } result) return false;
        return Slots[Output] is not { } output || output.Item == result && output.Count < result.MostInStack;
    }

    /// <summary>Burns and smelts for so many seconds.</summary>
    public void Update(float seconds)
    {
        var smelts = CanSmelt();
        if (!Lit && smelts && Slots[Fuel] is { } fuel && Smelting.Burns(fuel.Item) is > 0 and var burns)
        {
            (Burning, BurnTotal) = (burns, burns);
            Slots[Fuel] = fuel.Count > 1 ? fuel with { Count = fuel.Count - 1 } : null;
        }
        if (!Lit)
        {
            Cooked = 0;
            return;
        }
        Burning = MathF.Max(0, Burning - seconds);
        if (!smelts)
        {
            Cooked = 0;
            return;
        }
        Cooked += seconds;
        if (Cooked < Smelting.Seconds) return;
        Cooked = 0;
        var input = Slots[Input]!.Value;
        var result = Smelting.Result(input.Item)!;
        Slots[Input] = input.Count > 1 ? input with { Count = input.Count - 1 } : null;
        Slots[Output] = new ItemStack(result, (Slots[Output]?.Count ?? 0) + 1);
    }
}
