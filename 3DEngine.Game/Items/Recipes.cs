namespace Engine.Game;

/// <summary>
/// A shaped recipe, as Minecraft's are: rows of letters, each letter the items any one of which
/// fills its cell, a space an empty cell, and what the grid makes.
/// </summary>
/// <remarks>
/// A recipe matches wherever its shape lies in the grid with every other cell empty, and its mirror
/// image matches too, so an axe's head may face either way.
/// </remarks>
public sealed record Recipe(string[] Rows, IReadOnlyDictionary<char, Item[]> Keys, Item Result, int Count)
{
    public int Width => Rows.Max(r => r.Length);

    public int Height => Rows.Length;

    /// <summary>The items that fill a cell of the shape, none for an empty cell.</summary>
    public Item[] At(int x, int y) => x < Rows[y].Length && Rows[y][x] != ' ' ? Keys[Rows[y][x]] : [];

    /// <summary>Whether the shape, or its mirror image, fills a grid's stacks within their bounding box, with nothing else in it.</summary>
    public bool Matches(ReadOnlySpan<ItemStack?> grid, int size)
    {
        int minX = size, minY = size, maxX = -1, maxY = -1;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (grid[y * size + x] is not null) (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
        if (maxX < 0 || maxX - minX + 1 != Width || maxY - minY + 1 != Height) return false;
        return Fits(grid, size, minX, minY, mirror: false) || Fits(grid, size, minX, minY, mirror: true);
    }

    private bool Fits(ReadOnlySpan<ItemStack?> grid, int size, int left, int top, bool mirror)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var wanted = At(mirror ? Width - 1 - x : x, y);
                var there = grid[(top + y) * size + left + x];
                if (wanted.Length == 0 ? there is not null : there is not { } stack || Array.IndexOf(wanted, stack.Item) < 0) return false;
            }
        return true;
    }
}

/// <summary>Every recipe, after Minecraft's for the blocks and tools the game has.</summary>
public static class Recipes
{
    private static Item I(BlockId block) => Items.Of(block);

    private static readonly Item[] Planks = [I(BlockId.OakPlanks), I(BlockId.SprucePlanks), I(BlockId.BirchPlanks)];
    private static readonly Item[] Stick = [Items.Stick];

    private static Recipe R(Item result, int count, string[] rows, params (char Key, Item[] Items)[] keys) =>
        new(rows, keys.ToDictionary(k => k.Key, k => k.Items), result, count);

    public static readonly IReadOnlyList<Recipe> All = Build();

    private static List<Recipe> Build()
    {
        var all = new List<Recipe>
        {
            R(I(BlockId.OakPlanks), 4, ["L"], ('L', [I(BlockId.OakLog)])),
            R(I(BlockId.SprucePlanks), 4, ["L"], ('L', [I(BlockId.SpruceLog)])),
            R(I(BlockId.BirchPlanks), 4, ["L"], ('L', [I(BlockId.BirchLog)])),
            R(Items.Stick, 4, ["P", "P"], ('P', Planks)),
            R(I(BlockId.CraftingTable), 1, ["PP", "PP"], ('P', Planks)),
            R(I(BlockId.Torch), 4, ["C", "S"], ('C', [Items.Coal, Items.Charcoal]), ('S', Stick)),
            R(I(BlockId.Furnace), 1, ["CCC", "C C", "CCC"], ('C', [I(BlockId.Cobblestone)])),
            R(I(BlockId.OakFence), 3, ["PSP", "PSP"], ('P', Planks), ('S', Stick)),
            R(I(BlockId.OakDoor), 3, ["PP", "PP", "PP"], ('P', Planks)),
            R(I(BlockId.HayBale), 1, ["WWW", "WWW", "WWW"], ('W', [Items.Wheat])),
            R(Items.Bread, 1, ["WWW"], ('W', [Items.Wheat])),
            R(I(BlockId.Sandstone), 1, ["SS", "SS"], ('S', [I(BlockId.Sand)])),
            R(I(BlockId.StoneBricks), 4, ["SS", "SS"], ('S', [I(BlockId.Stone)])),
        };
        // A tool of each kind from planks and from cobblestone, with sticks for its handle.
        foreach (var (material, head) in (ReadOnlySpan<(string, Item[])>)[("wooden", Planks), ("stone", [I(BlockId.Cobblestone)]), ("iron", [Items.IronIngot])])
        {
            Item Tool(string kind) => Items.Find($"{material}_{kind}")!;
            all.Add(R(Tool("pickaxe"), 1, ["MMM", " S ", " S "], ('M', head), ('S', Stick)));
            all.Add(R(Tool("axe"), 1, ["MM", "MS", " S"], ('M', head), ('S', Stick)));
            all.Add(R(Tool("shovel"), 1, ["M", "S", "S"], ('M', head), ('S', Stick)));
            all.Add(R(Tool("sword"), 1, ["M", "M", "S"], ('M', head), ('S', Stick)));
        }
        // Six slabs from a row of three blocks, and four stairs from a staircase of six.
        foreach (var (block, slab, stairs) in (ReadOnlySpan<(BlockId, BlockId?, BlockId?)>)
            [(BlockId.OakPlanks, BlockId.OakSlab, BlockId.OakStairs), (BlockId.SprucePlanks, BlockId.SpruceSlab, BlockId.SpruceStairs),
             (BlockId.Cobblestone, BlockId.CobblestoneSlab, BlockId.CobblestoneStairs), (BlockId.Sandstone, BlockId.SandstoneSlab, BlockId.SandstoneStairs),
             (BlockId.Stone, BlockId.StoneSlab, null), (BlockId.StoneBricks, null, BlockId.StoneBrickStairs)])
        {
            if (slab is { } s) all.Add(R(I(s), 6, ["MMM"], ('M', [I(block)])));
            if (stairs is { } t) all.Add(R(I(t), 4, ["M  ", "MM ", "MMM"], ('M', [I(block)])));
        }
        return all;
    }

    /// <summary>The recipe a grid of a size makes, or null.</summary>
    public static Recipe? Match(ReadOnlySpan<ItemStack?> grid, int size)
    {
        foreach (var recipe in All)
            if (recipe.Width <= size && recipe.Height <= size && recipe.Matches(grid, size)) return recipe;
        return null;
    }

    /// <summary>
    /// Makes an item once from the inventory's stacks as a recipe book does, the first recipe for it
    /// whose cells the inventory can fill, one item a cell, taking those items out.
    /// </summary>
    /// <returns>The recipe used, or null where none could be filled.</returns>
    public static Recipe? CraftFrom(Inventory inventory, Item result)
    {
        foreach (var recipe in All)
        {
            if (recipe.Result != result) continue;
            var needed = new Dictionary<Item, int>();
            var filled = true;
            for (int y = 0; y < recipe.Height && filled; y++)
                for (int x = 0; x < recipe.Width && filled; x++)
                {
                    var choices = recipe.At(x, y);
                    if (choices.Length == 0) continue;
                    // The first of a cell's items the inventory holds one more of than the cells before took.
                    var chosen = choices.FirstOrDefault(c => inventory.Count(c) > needed.GetValueOrDefault(c));
                    if (chosen is null) filled = false;
                    else needed[chosen] = needed.GetValueOrDefault(chosen) + 1;
                }
            if (!filled) continue;
            foreach (var (item, count) in needed)
                for (int left = count; left > 0;)
                    left -= inventory.Take(inventory.Find(item), left);
            return recipe;
        }
        return null;
    }
}

/// <summary>A crafting grid of two by two or three by three slots, and what its stacks make.</summary>
public sealed class CraftingGrid(int size)
{
    public int Size => size;

    public readonly ItemStack?[] Slots = new ItemStack?[size * size];

    /// <summary>The recipe the grid's stacks make, or null.</summary>
    public Recipe? Recipe => Recipes.Match(Slots, size);

    /// <summary>Makes what the grid's stacks make once, using up one item of each stack, or null where they make nothing.</summary>
    public ItemStack? Craft()
    {
        if (Recipe is not { } recipe) return null;
        for (int i = 0; i < Slots.Length; i++)
            if (Slots[i] is { } stack) Slots[i] = stack.Count > 1 ? stack with { Count = stack.Count - 1 } : null;
        return new ItemStack(recipe.Result, recipe.Count);
    }

    /// <summary>Takes every stack out of the grid, as it closes.</summary>
    public List<ItemStack> Empty()
    {
        var stacks = Slots.OfType<ItemStack>().ToList();
        Array.Clear(Slots);
        return stacks;
    }
}
