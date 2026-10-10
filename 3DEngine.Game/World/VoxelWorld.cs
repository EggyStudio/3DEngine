namespace Engine.Game;

/// <summary>The loaded columns of an endless world, read and changed by world position.</summary>
/// <remarks>
/// Only the main thread reads or changes it. Workers generate a column apart and hand it over
/// whole, so nothing here is shared with them.
/// </remarks>
public sealed class VoxelWorld(IWorldGenerator generator)
{
    private readonly Dictionary<(int X, int Z), ChunkColumn> _columns = [];

    public IWorldGenerator Generator { get; } = generator;

    /// <summary>Sections an edit changed, which are meshed again before the frame is drawn.</summary>
    public HashSet<SectionKey> Edited { get; } = [];

    /// <summary>Sections a column's arrival left to mesh, which are meshed a few at a time, nearest first.</summary>
    public HashSet<SectionKey> Loaded { get; } = [];

    public int ColumnCount => _columns.Count;

    public IEnumerable<ChunkColumn> Columns => _columns.Values;

    public static int ColumnOf(int block) => block >> Section.Shift;

    public bool TryGetColumn(int x, int z, out ChunkColumn column) => _columns.TryGetValue((x, z), out column!);

    public bool HasColumn(int x, int z) => _columns.ContainsKey((x, z));

    /// <summary>
    /// Takes a generated column into the world, and leaves its sections and those of its four
    /// neighbors to mesh, since a face on a column's edge is shown or hidden by the neighbor's
    /// block beside it.
    /// </summary>
    public void Add(ChunkColumn column)
    {
        _columns[(column.X, column.Z)] = column;
        for (int y = 0; y < ChunkColumn.SectionCount; y++)
        {
            Loaded.Add(new SectionKey(column.X, y, column.Z));
            Loaded.Add(new SectionKey(column.X - 1, y, column.Z));
            Loaded.Add(new SectionKey(column.X + 1, y, column.Z));
            Loaded.Add(new SectionKey(column.X, y, column.Z - 1));
            Loaded.Add(new SectionKey(column.X, y, column.Z + 1));
        }
    }

    public bool Remove(int x, int z) => _columns.Remove((x, z));

    public void Clear()
    {
        _columns.Clear();
        Edited.Clear();
        Loaded.Clear();
    }

    /// <summary>The block at a world position, air above and below the world and in a column not loaded.</summary>
    public BlockId GetBlock(int x, int y, int z)
    {
        if ((uint)y >= ChunkColumn.Height || !_columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column))
            return BlockId.Air;
        return column.Get(x & Section.Mask, y, z & Section.Mask);
    }

    /// <summary>
    /// Whether a body collides with the block at a world position. Below the world and in a column
    /// not yet loaded it does, so the player cannot fall out of the world or walk into a column
    /// that has not arrived.
    /// </summary>
    public bool Collides(int x, int y, int z)
    {
        if (y < 0) return true;
        if (y >= ChunkColumn.Height) return false;
        if (!_columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column)) return true;
        return Blocks.IsSolid(column.Get(x & Section.Mask, y, z & Section.Mask));
    }

    /// <summary>
    /// Places a block, and marks the section it is in to mesh again, with the neighbor across any
    /// face of the section it touches.
    /// </summary>
    /// <returns>Whether the block changed. A place above or below the world or in a column not loaded is left alone.</returns>
    public bool SetBlock(int x, int y, int z, BlockId block)
    {
        if ((uint)y >= ChunkColumn.Height || !_columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column))
            return false;
        int lx = x & Section.Mask, lz = z & Section.Mask, ly = y & Section.Mask;
        if (column.Get(lx, y, lz) == block) return false;
        column.Set(lx, y, lz, block);

        var key = new SectionKey(column.X, y >> Section.Shift, column.Z);
        Edited.Add(key);
        if (lx == 0) Edited.Add(key with { X = key.X - 1 });
        if (lx == Section.Mask) Edited.Add(key with { X = key.X + 1 });
        if (lz == 0) Edited.Add(key with { Z = key.Z - 1 });
        if (lz == Section.Mask) Edited.Add(key with { Z = key.Z + 1 });
        if (ly == 0 && key.Y > 0) Edited.Add(key with { Y = key.Y - 1 });
        if (ly == Section.Mask && key.Y < ChunkColumn.SectionCount - 1) Edited.Add(key with { Y = key.Y + 1 });
        return true;
    }

    /// <summary>The height of the highest block that is not air at a world column, or -1 where it is all air or not loaded.</summary>
    public int Top(int x, int z) =>
        _columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column) ? column.Top(x & Section.Mask, z & Section.Mask) : -1;
}
