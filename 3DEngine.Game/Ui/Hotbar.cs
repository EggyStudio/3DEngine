using static Engine.Engine3D;

namespace Engine.Game;

/// <summary>Nine slots of blocks to place, one of them chosen, picked by 1 to 9 and the wheel.</summary>
public sealed class Hotbar
{
    public const int Size = 9;

    // Stone, planks and glass to build with, each light, and the concrete walls whose colors show bouncing.
    public readonly BlockId[] Slots =
    [
        BlockId.Stone, BlockId.OakPlanks, BlockId.Glass, BlockId.Glowstone, BlockId.SeaLantern, BlockId.Shroomlight,
        BlockId.WhiteConcrete, BlockId.RedConcrete, BlockId.GreenConcrete,
    ];

    public int Selected { get; set; }

    public BlockId Current => Slots[Selected];

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

    /// <summary>Selects the slot holding a block, or puts the block into the chosen slot when none does, as picking a block does.</summary>
    public void Pick(BlockId block)
    {
        var slot = Array.IndexOf(Slots, block);
        if (slot >= 0) Selected = slot;
        else Slots[Selected] = block;
    }
}
