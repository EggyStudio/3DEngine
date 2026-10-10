namespace Engine.Game;

/// <summary>The loaded columns of an endless world, read and changed by world position, with their light.</summary>
/// <remarks>
/// Only the main thread reads or changes it. Workers generate a column apart, its own light
/// included, and hand it over whole, so nothing here is shared with them.
/// </remarks>
public sealed class VoxelWorld
{
    private readonly Dictionary<(int X, int Z), ChunkColumn> _columns = [];
    private readonly LightEngine _light;

    public VoxelWorld(IWorldGenerator generator)
    {
        Generator = generator;
        _light = new LightEngine(this);
    }

    public IWorldGenerator Generator { get; }

    /// <summary>Sections an edit changed the faces of, which are meshed again before the frame is drawn.</summary>
    public HashSet<SectionKey> Edited { get; } = [];

    /// <summary>Sections whose faces' shading changed, by light or by a block beside a corner, which keep their faces and take new colors.</summary>
    public HashSet<SectionKey> Reshaded { get; } = [];

    /// <summary>Sections a column's arrival left to mesh, which are meshed a few at a time, nearest first.</summary>
    public HashSet<SectionKey> Loaded { get; } = [];

    public int ColumnCount => _columns.Count;

    public IEnumerable<ChunkColumn> Columns => _columns.Values;

    public static int ColumnOf(int block) => block >> Section.Shift;

    public bool TryGetColumn(int x, int z, out ChunkColumn column) => _columns.TryGetValue((x, z), out column!);

    public bool HasColumn(int x, int z) => _columns.ContainsKey((x, z));

    /// <summary>
    /// Takes a generated column into the world, joins its light to its neighbors', and leaves its
    /// sections and those of the eight columns around it to mesh, since a face on a column's edge is
    /// shown or hidden, and its corners shaded, by the blocks across the edge.
    /// </summary>
    public void Add(ChunkColumn column)
    {
        _columns[(column.X, column.Z)] = column;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                for (int y = 0; y < ChunkColumn.SectionCount; y++)
                    Loaded.Add(new SectionKey(column.X + dx, y, column.Z + dz));
        _light.Join(column);
    }

    public bool Remove(int x, int z) => _columns.Remove((x, z));

    /// <summary>The block at a world position, air above and below the world and in a column not loaded.</summary>
    public BlockId GetBlock(int x, int y, int z)
    {
        if ((uint)y >= ChunkColumn.Height || !_columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column))
            return BlockId.Air;
        return column.Get(x & Section.Mask, y, z & Section.Mask);
    }

    /// <summary>The sky's and the blocks' light levels at a world position, the open sky's above the world and none where nothing is loaded.</summary>
    public (int Sky, int Block) GetLight(int x, int y, int z)
    {
        if (y >= ChunkColumn.Height) return (Lighting.Max, 0);
        if (y < 0 || !_columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column)) return (0, 0);
        var index = Section.Index(x & Section.Mask, y & Section.Mask, z & Section.Mask);
        var section = column.Sections[y >> Section.Shift];
        return (section.SkyLight(index), section.BlockLight(index));
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
        return Blocks.Collides(column.Get(x & Section.Mask, y, z & Section.Mask));
    }

    /// <summary>
    /// Places a block, spreads the light around it again, and marks the section it is in to mesh
    /// again, with the neighbor across any face of the section it touches.
    /// </summary>
    /// <returns>Whether the block changed. A place above or below the world or in a column not loaded is left alone.</returns>
    public bool SetBlock(int x, int y, int z, BlockId block)
    {
        if ((uint)y >= ChunkColumn.Height || !_columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column))
            return false;
        int lx = x & Section.Mask, lz = z & Section.Mask, ly = y & Section.Mask;
        if (column.Get(lx, y, lz) == block) return false;
        column.Set(lx, y, lz, block);
        column.Changed = true;
        _light.Changed(x, y, z, block);

        var key = new SectionKey(column.X, y >> Section.Shift, column.Z);
        Edited.Add(key);
        if (lx == 0) Edited.Add(key with { X = key.X - 1 });
        if (lx == Section.Mask) Edited.Add(key with { X = key.X + 1 });
        if (lz == 0) Edited.Add(key with { Z = key.Z - 1 });
        if (lz == Section.Mask) Edited.Add(key with { Z = key.Z + 1 });
        if (ly == 0 && key.Y > 0) Edited.Add(key with { Y = key.Y - 1 });
        if (ly == Section.Mask && key.Y < ChunkColumn.SectionCount - 1) Edited.Add(key with { Y = key.Y + 1 });
        // A block shades the corners of faces in the sections across its edges and corners as well.
        Reshade(x, y, z);
        return true;
    }

    /// <summary>Marks the sections whose faces a block's light or presence shades: its own, and those across each edge of it on a section's side.</summary>
    public void Reshade(int x, int y, int z)
    {
        int cx = x >> Section.Shift, cy = y >> Section.Shift, cz = z >> Section.Shift;
        int lx = x & Section.Mask, ly = y & Section.Mask, lz = z & Section.Mask;
        for (int dy = ly == 0 ? -1 : 0; dy <= (ly == Section.Mask ? 1 : 0); dy++)
        {
            if ((uint)(cy + dy) >= ChunkColumn.SectionCount) continue;
            for (int dz = lz == 0 ? -1 : 0; dz <= (lz == Section.Mask ? 1 : 0); dz++)
                for (int dx = lx == 0 ? -1 : 0; dx <= (lx == Section.Mask ? 1 : 0); dx++)
                    Reshaded.Add(new SectionKey(cx + dx, cy + dy, cz + dz));
        }
    }

    /// <summary>Marks every loaded section to take new colors, as when the way faces are shaded changes.</summary>
    public void ReshadeAll()
    {
        foreach (var column in _columns.Values)
            for (int y = 0; y < ChunkColumn.SectionCount; y++) Reshaded.Add(new SectionKey(column.X, y, column.Z));
    }

    /// <summary>The biome at a world column, or null where it is not loaded.</summary>
    public Biome? BiomeAt(int x, int z) =>
        _columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column)
            ? Biomes.All[column.Biome[(z & Section.Mask) * Section.Size + (x & Section.Mask)]]
            : null;

    /// <summary>The height of the highest block that is not air at a world column, or -1 where it is all air or not loaded.</summary>
    public int Top(int x, int z) =>
        _columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column) ? column.Top(x & Section.Mask, z & Section.Mask) : -1;
}
