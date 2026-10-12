using static Engine.E3D;

namespace Engine.Game;

/// <summary>The inventory's first nine slots, one of them chosen, picked by 1 to 9 and the wheel.</summary>
public sealed class Hotbar(Inventory inventory)
{
    public const int Size = 9;

    // Stone, planks and glass to build with, a torch, a lantern and two blocks that give off light,
    // and the concrete walls whose colors show bouncing, which a world in creative mode begins with.
    private static readonly BlockId[] Creative =
    [
        BlockId.Stone, BlockId.OakPlanks, BlockId.Glass, BlockId.Torch, BlockId.Lantern, BlockId.Glowstone, BlockId.SeaLantern,
        BlockId.WhiteConcrete, BlockId.RedConcrete,
    ];

    public Inventory Inventory => inventory;

    public int Selected { get; set; }

    /// <summary>The chosen slot's stack, or null where it is empty.</summary>
    public ItemStack? Current => inventory.Slots[Selected];

    /// <summary>The block the chosen slot's item places, or air where it holds none.</summary>
    public BlockId CurrentBlock => Current?.Item.Block ?? BlockId.Air;

    /// <summary>Fills the hotbar with the blocks a world in creative mode begins with.</summary>
    public void FillCreative()
    {
        for (int i = 0; i < Size; i++) inventory.Slots[i] = new ItemStack(Items.Of(Creative[i]), 1);
    }

    public void Update(bool keys, bool wheel)
    {
        if (keys)
            for (int i = 0; i < Size; i++)
                if (IsKeyPressed(Key.One + i)) Selected = i;
        if (!wheel) return;
        // The wheel turned away from the player moves left, as Minecraft's does.
        var turned = GetMouseWheelMove();
        if (turned > 0) Selected = (Selected + Size - 1) % Size;
        else if (turned < 0) Selected = (Selected + 1) % Size;
    }

    /// <summary>
    /// Selects the hotbar's slot holding an item, as picking a block does, or where none does, puts
    /// the item into the chosen slot in creative mode and in survival moves a stack of it from the
    /// rest of the inventory there, swapping what the slot held.
    /// </summary>
    public void Pick(Item item, bool creative)
    {
        for (int i = 0; i < Size; i++)
            if (inventory.Slots[i]?.Item == item)
            {
                Selected = i;
                return;
            }
        if (creative)
        {
            inventory.Slots[Selected] = new ItemStack(item, 1);
            return;
        }
        var found = inventory.Find(item);
        if (found >= Size) (inventory.Slots[Selected], inventory.Slots[found]) = (inventory.Slots[found], inventory.Slots[Selected]);
    }
}
