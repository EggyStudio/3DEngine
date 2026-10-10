namespace Engine.Game;

/// <summary>A column of sections from the bottom of the world to its top, the unit the world is generated and loaded in.</summary>
/// <remarks>
/// Every section is made with the column, air or not, since the light in the air above the ground
/// and in a cave is kept as the blocks are.
/// </remarks>
public sealed class ChunkColumn
{
    public const int SectionCount = 8;
    public const int Height = SectionCount * Section.Size;

    public ChunkColumn(int x, int z)
    {
        X = x;
        Z = z;
        for (int i = 0; i < SectionCount; i++) Sections[i] = new Section();
    }

    public int X { get; }
    public int Z { get; }

    /// <summary>Whether a block of it changed since it was generated or last kept by the save.</summary>
    public bool Changed { get; set; }

    /// <summary>Its sections from the bottom.</summary>
    public readonly Section[] Sections = new Section[SectionCount];

    /// <summary>The block at a place inside the column, x and z from 0 to 15 and y from 0 to <see cref="Height"/> less one.</summary>
    public BlockId Get(int x, int y, int z) => Sections[y >> Section.Shift].Get(x, y & Section.Mask, z);

    public void Set(int x, int y, int z, BlockId block) => Sections[y >> Section.Shift].Set(x, y & Section.Mask, z, block);

    /// <summary>The height of the highest block that is not air, or -1 in a column of air.</summary>
    public int Top(int x, int z)
    {
        for (int y = Height - 1; y >= 0; y--)
            if (Get(x, y, z) != BlockId.Air) return y;
        return -1;
    }
}
