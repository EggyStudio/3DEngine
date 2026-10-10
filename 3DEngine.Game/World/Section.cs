namespace Engine.Game;

/// <summary>A cube of 16 blocks a side, the unit the world is meshed and drawn in, with each block's light levels.</summary>
public sealed class Section
{
    public const int Size = 16;
    public const int Shift = 4;
    public const int Mask = Size - 1;
    public const int Volume = Size * Size * Size;

    public readonly ushort[] Blocks = new ushort[Volume];

    /// <summary>Each block's light, the sky's level from 0 to 15 in the high four bits and the light-giving blocks' in the low four, as Minecraft keeps them.</summary>
    public readonly byte[] Light = new byte[Volume];

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

    public int SkyLight(int index) => Light[index] >> 4;

    public int BlockLight(int index) => Light[index] & 15;

    /// <summary>One channel's level at an index, the sky's or the blocks'.</summary>
    public int Level(int index, bool sky) => sky ? Light[index] >> 4 : Light[index] & 15;

    public void SetLevel(int index, bool sky, int level) =>
        Light[index] = (byte)(sky ? (Light[index] & 0x0F) | (level << 4) : (Light[index] & 0xF0) | level);
}

/// <summary>A section's place in the world, in sections along each axis.</summary>
public readonly record struct SectionKey(int X, int Y, int Z)
{
    /// <summary>The world position of its lowest corner.</summary>
    public System.Numerics.Vector3 Origin => new(X * Section.Size, Y * Section.Size, Z * Section.Size);
}
