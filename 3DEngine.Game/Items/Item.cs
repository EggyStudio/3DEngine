namespace Engine.Game;

/// <summary>The kinds of tool, each breaking its own blocks faster, as Minecraft's are.</summary>
public enum ToolKind
{
    None,
    Pickaxe,
    Axe,
    Shovel,
    Sword,
}

/// <summary>
/// A kind of thing the player holds and keeps in the inventory: a block it places, or a thing of
/// its own that a mob or a block drops, as raw meat, leather, feathers and wheat are.
/// </summary>
/// <param name="Key">The name commands take and a save keeps, a block's own key for the item placing it.</param>
/// <param name="Name">The name shown.</param>
/// <param name="Color">The color of its slot and of its cube on the ground, a block's top's as it looks in the plains.</param>
/// <param name="Block">The block it places, or null for an item that places none.</param>
public sealed record Item(string Key, string Name, Color Color, BlockId? Block)
{
    /// <summary>Its place in <see cref="Items.All"/>.</summary>
    public int Id { get; init; }

    /// <summary>How many stack in one slot, 64 as most of Minecraft's do, and one for a tool.</summary>
    public int MostInStack { get; init; } = 64;

    /// <summary>The kind of tool it is, or none.</summary>
    public ToolKind Tool { get; init; }

    /// <summary>How many times faster than a hand it breaks the blocks its kind is for, 2 for wood and 4 for stone.</summary>
    public float Speed { get; init; } = 1;

    /// <summary>How many uses wear it out, or 0 for an item that does not wear.</summary>
    public int Durability { get; init; }

    /// <summary>The health a hit with it takes from a mob, a hand's 1 for an item that is not a weapon or a tool.</summary>
    public int Damage { get; init; } = 1;
}

/// <summary>So many of an item in one slot, and for a tool how many of its uses are worn away.</summary>
public readonly record struct ItemStack(Item Item, int Count, int Wear = 0);

/// <summary>Every kind of item: one for each block held to place it, then the things of their own, then the tools.</summary>
public static class Items
{
    private static readonly Item?[] _byBlock = new Item?[Blocks.All.Count];
    private static readonly Item[] _all = Build();

    public static readonly Item RawPorkchop = Find("raw_porkchop")!;
    public static readonly Item RawBeef = Find("raw_beef")!;
    public static readonly Item RawMutton = Find("raw_mutton")!;
    public static readonly Item RawChicken = Find("raw_chicken")!;
    public static readonly Item Leather = Find("leather")!;
    public static readonly Item Feather = Find("feather")!;
    public static readonly Item Wheat = Find("wheat_sheaf")!;
    public static readonly Item Stick = Find("stick")!;
    public static readonly Item Coal = Find("coal")!;

    private static Item[] Build()
    {
        var all = new List<Item>();
        foreach (var block in Blocks.All)
        {
            // A block placed in more than one way is one item, the block held to place it, and the
            // water that flows from a source is none.
            if (block.Id == BlockId.Air || block.Item != block.Id) continue;
            // The crop's item plants it, as Minecraft's seeds do, its wheat being an item of its own.
            var name = block.Id == BlockId.Wheat ? "Wheat Seeds" : block.Name;
            var item = new Item(block.Key, name, block.Swatch, block.Id) { Id = all.Count };
            all.Add(item);
            _byBlock[(int)block.Id] = item;
        }
        (string Key, string Name, Color Color)[] own =
        [
            ("raw_porkchop", "Raw Porkchop", new Color(238, 140, 142)),
            ("raw_beef", "Raw Beef", new Color(186, 58, 50)),
            ("raw_mutton", "Raw Mutton", new Color(200, 82, 72)),
            ("raw_chicken", "Raw Chicken", new Color(238, 198, 176)),
            ("leather", "Leather", new Color(150, 84, 44)),
            ("feather", "Feather", new Color(244, 244, 244)),
            ("wheat_sheaf", "Wheat", new Color(220, 188, 88)),
            ("stick", "Stick", new Color(120, 90, 54)),
            ("coal", "Coal", new Color(58, 58, 66)),
        ];
        foreach (var (key, name, color) in own) all.Add(new Item(key, name, color, null) { Id = all.Count });
        // Wooden and stone tools with Minecraft's speeds, uses and damage.
        foreach (var (material, name, color, speed, uses, bonus) in (ReadOnlySpan<(string, string, Color, float, int, int)>)
            [("wooden", "Wooden", new Color(162, 130, 78), 2, 59, 0), ("stone", "Stone", new Color(130, 130, 130), 4, 131, 1)])
            foreach (var (tool, damage) in (ReadOnlySpan<(ToolKind, int)>)[(ToolKind.Pickaxe, 3), (ToolKind.Axe, 4), (ToolKind.Shovel, 2), (ToolKind.Sword, 5)])
                all.Add(new Item($"{material}_{tool.ToString().ToLowerInvariant()}", $"{name} {tool}", color, null)
                {
                    Id = all.Count, MostInStack = 1, Tool = tool, Speed = speed, Durability = uses, Damage = damage + bonus,
                });
        return [.. all];
    }

    /// <summary>Every item, its index its id.</summary>
    public static IReadOnlyList<Item> All => _all;

    /// <summary>The item that places a block, or that its other ways are placed with, as a wall torch's is the torch.</summary>
    public static Item Of(BlockId block) => _byBlock[(int)Blocks.Get(block).Item]!;

    /// <summary>An item by its key or its name, or by a block's key or name that it places, or null.</summary>
    public static Item? Find(string text)
    {
        foreach (var item in _all)
            if (item.Key.Equals(text, StringComparison.OrdinalIgnoreCase) || item.Name.Equals(text, StringComparison.OrdinalIgnoreCase)) return item;
        return Blocks.TryFind(text, out var block) && block != BlockId.Air ? Of(block) : null;
    }
}
