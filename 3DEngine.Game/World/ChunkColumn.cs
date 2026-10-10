namespace Engine.Game;

/// <summary>A column of sections from the bottom of the world to its top, the unit the world is generated and loaded in.</summary>
public sealed class ChunkColumn(int x, int z)
{
    public const int SectionCount = 8;
    public const int Height = SectionCount * Section.Size;

    public int X { get; } = x;
    public int Z { get; } = z;

    /// <summary>Its sections from the bottom, null where a section has held nothing but air.</summary>
    public readonly Section?[] Sections = new Section?[SectionCount];

    /// <summary>The block at a place inside the column, x and z from 0 to 15 and y from 0 to <see cref="Height"/> less one.</summary>
    public BlockId Get(int x, int y, int z) =>
        Sections[y >> Section.Shift] is { } section ? section.Get(x, y & Section.Mask, z) : BlockId.Air;

    public void Set(int x, int y, int z, BlockId block)
    {
        var section = Sections[y >> Section.Shift];
        if (section is null)
        {
            if (block == BlockId.Air) return;
            section = Sections[y >> Section.Shift] = new Section();
        }
        section.Set(x, y & Section.Mask, z, block);
    }

    /// <summary>The height of the highest block that is not air, or -1 in a column of air.</summary>
    public int Top(int x, int z)
    {
        for (int y = Height - 1; y >= 0; y--)
            if (Get(x, y, z) != BlockId.Air) return y;
        return -1;
    }
}
