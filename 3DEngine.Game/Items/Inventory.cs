namespace Engine.Game;

/// <summary>
/// The player's 36 slots of items, the first nine of them the hotbar, as Minecraft's inventory is,
/// each holding a stack of one item.
/// </summary>
public sealed class Inventory
{
    public const int Size = 36;

    /// <summary>Each slot's stack, or null where it is empty, the hotbar's first.</summary>
    public readonly ItemStack?[] Slots = new ItemStack?[Size];

    /// <summary>
    /// Puts so many of an item in, onto the stacks of it that have room first and then into the
    /// empty slots, each in order from the hotbar's first, as a picked-up item goes.
    /// </summary>
    /// <returns>How many did not fit.</returns>
    public int Add(Item item, int count, int wear = 0)
    {
        for (int i = 0; i < Size && count > 0; i++)
            if (Slots[i] is { } stack && stack.Item == item && stack.Count < item.MostInStack)
            {
                var moved = Math.Min(count, item.MostInStack - stack.Count);
                Slots[i] = stack with { Count = stack.Count + moved };
                count -= moved;
            }
        for (int i = 0; i < Size && count > 0; i++)
            if (Slots[i] is null)
            {
                var moved = Math.Min(count, item.MostInStack);
                Slots[i] = new ItemStack(item, moved, wear);
                count -= moved;
            }
        return count;
    }

    /// <summary>Takes so many from a slot's stack, emptying the slot when none are left.</summary>
    /// <returns>How many were taken, fewer where the stack held fewer.</returns>
    public int Take(int slot, int count = 1)
    {
        if (Slots[slot] is not { } stack) return 0;
        var taken = Math.Min(count, stack.Count);
        Slots[slot] = stack.Count > taken ? stack with { Count = stack.Count - taken } : null;
        return taken;
    }

    /// <summary>How many of an item all the slots hold.</summary>
    public int Count(Item item) => Slots.Sum(s => s is { } stack && stack.Item == item ? stack.Count : 0);

    /// <summary>The first slot holding an item, or -1.</summary>
    public int Find(Item item) => Array.FindIndex(Slots, s => s is { } stack && stack.Item == item);

    public void Clear() => Array.Clear(Slots);

    /// <summary>Each slot as a save keeps it, an item's key, a count and a tool's wear, or an empty string for an empty slot.</summary>
    public string[] Save() => [.. Slots.Select(s => s is { } stack ? stack.Wear > 0 ? $"{stack.Item.Key} {stack.Count} {stack.Wear}" : $"{stack.Item.Key} {stack.Count}" : "")];

    /// <summary>Fills the slots from what <see cref="Save"/> gave, a slot whose item is no longer known left empty.</summary>
    public void Load(string[] slots)
    {
        Clear();
        for (int i = 0; i < Math.Min(Size, slots.Length); i++)
        {
            var parts = slots[i].Split(' ');
            if (parts.Length is 2 or 3 && Items.Find(parts[0]) is { } item && int.TryParse(parts[1], out var count) && count > 0)
            {
                var wear = parts.Length == 3 && int.TryParse(parts[2], out var worn) ? Math.Clamp(worn, 0, Math.Max(0, item.Durability - 1)) : 0;
                Slots[i] = new ItemStack(item, Math.Min(count, item.MostInStack), wear);
            }
        }
    }
}
