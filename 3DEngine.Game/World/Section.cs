namespace Engine.Game;

/// <summary>A cube of 16 blocks a side, the unit the world is meshed and drawn in.</summary>
public sealed class Section
{
    public const int Size = 16;
    public const int Shift = 4;
    public const int Mask = Size - 1;
    public const int Volume = Size * Size * Size;

    public readonly ushort[] Blocks = new ushort[Volume];

    /// <summary>How many of its blocks are not air, so an empty section is skipped by the mesher.</summary>
    public int Filled { get; private set; }

    public static int Index(int x, int y, int z) => (y << 8) | (z << 4) | x;

    public BlockId Get(int x, int y, int z) => (BlockId)Blocks[Index(x, y, z)];

    public void Set(int x, int y, int z, BlockId block)
    {
        ref var slot = ref Blocks[Index(x, y, z)];
        if (slot == (ushort)block) return;
        Filled += (slot == 0 ? 1 : 0) - (block == BlockId.Air ? 1 : 0);
        slot = (ushort)block;
    }
}

/// <summary>A section's place in the world, in sections along each axis.</summary>
public readonly record struct SectionKey(int X, int Y, int Z)
{
    /// <summary>The world position of its lowest corner.</summary>
    public System.Numerics.Vector3 Origin => new(X * Section.Size, Y * Section.Size, Z * Section.Size);
}
