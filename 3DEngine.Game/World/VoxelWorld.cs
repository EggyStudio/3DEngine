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
    /// The boxes in a block's cell that stop a body, the whole cell for a solid block. Below the
    /// world and in a column not yet loaded the cell is solid, so the player cannot fall out of the
    /// world or walk into a column that has not arrived.
    /// </summary>
    public IReadOnlyList<Box> BoxesAt(int x, int y, int z)
    {
        if (y < 0) return [Box.Cell];
        if (y >= ChunkColumn.Height) return [];
        if (!_columns.TryGetValue((x >> Section.Shift, z >> Section.Shift), out var column)) return [Box.Cell];
        var block = column.Get(x & Section.Mask, y, z & Section.Mask);
        return block == BlockId.OakFence ? FenceAt(x, y, z).Collision : Blocks.Get(block).Boxes;
    }

    /// <summary>The shape of the block at a world position, a fence's joined to the fences and whole blocks beside it, or null for a whole block.</summary>
    public BlockShape? ShapeAt(int x, int y, int z)
    {
        var block = GetBlock(x, y, z);
        return block == BlockId.OakFence ? FenceAt(x, y, z) : Blocks.Get(block).Shape;
    }

    private BlockShape FenceAt(int x, int y, int z) => Blocks.Fence(
        (Blocks.JoinsFence(GetBlock(x, y, z - 1)) ? 1 : 0) | (Blocks.JoinsFence(GetBlock(x + 1, y, z)) ? 2 : 0)
        | (Blocks.JoinsFence(GetBlock(x, y, z + 1)) ? 4 : 0) | (Blocks.JoinsFence(GetBlock(x - 1, y, z)) ? 8 : 0));

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
        var was = column.Get(lx, y, lz);
        if (was == block) return false;
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

        // A door's other half goes with it, unless the door is only opened or shut.
        if (Blocks.Get(was) is { Toggled: not null, Shape: { } half } && Blocks.Get(block).Toggled is null)
        {
            var other = half.Upper ? y - 1 : y + 1;
            if (Blocks.Get(GetBlock(x, other, z)).Toggled is not null) SetBlock(x, other, z, BlockId.Air);
        }

        // A block smaller than its cell that held to this one falls with it, as a torch does when the
        // wall behind it is broken.
        for (int d = 0; d < 6; d++)
        {
            int nx = x + Lighting.Dx[d], ny = y + Lighting.Dy[d], nz = z + Lighting.Dz[d];
            if (Blocks.Get(GetBlock(nx, ny, nz)).Shape is { Falls: true, Support: var (sx, sy, sz) } shape
                && (nx + sx, ny + sy, nz + sz) == (x, y, z) && !Blocks.Holds(block, shape.Facing))
                SetBlock(nx, ny, nz, BlockId.Air);
        }
        return true;
    }

    /// <summary>
    /// The block an item makes placed at a world position against a face whose outward normal is
    /// given: the item itself for a whole block; for a slab, stairs or a fence the way of it that
    /// faces <paramref name="look"/>, the way the player looks along the ground, in the upper half
    /// where it is placed against a ceiling or on the upper half of a side, <paramref name="height"/>
    /// being how far up its cell the side was met; and for a block that holds to another the way of
    /// it that faces away from that face, or else the first way of it with a block to hold to,
    /// standing before hanging before a wall, or air where none has one, as a lantern placed on a
    /// wall stands on the floor below or hangs from the ceiling.
    /// </summary>
    public BlockId Fit(BlockId item, int x, int y, int z, int normalX, int normalY, int normalZ, Facing look = Facing.North, float height = 0)
    {
        if (Blocks.Get(item).Shape is not { } shape) return item;
        if (!shape.Falls)
        {
            var upper = normalY < 0 || normalY == 0 && height > 0.5f;
            foreach (var way in (ReadOnlySpan<Facing>)[look, Facing.Up])
                foreach (var half in (ReadOnlySpan<bool>)[upper, false])
                    if (Blocks.Variant(item, way, half) is not BlockId.Air and var variant) return variant;
            return item;
        }
        ReadOnlySpan<Facing> ways = [BlockShape.Away(normalX, normalY, normalZ), Facing.Up, Facing.Down, Facing.North, Facing.South, Facing.West, Facing.East];
        foreach (var way in ways)
        {
            var variant = Blocks.Variant(item, way);
            if (variant == BlockId.Air) continue;
            var (sx, sy, sz) = Blocks.Get(variant).Shape!.Support;
            if (Blocks.Holds(GetBlock(x + sx, y + sy, z + sz), way)) return variant;
        }
        return BlockId.Air;
    }

    /// <summary>Opens a shut door or shuts an open one, both its halves, where a half of one stands.</summary>
    /// <returns>Whether a door stood there.</returns>
    public bool Toggle(int x, int y, int z)
    {
        if (Blocks.Get(GetBlock(x, y, z)) is not { Toggled: { } turned, Shape: { } half }) return false;
        var other = half.Upper ? y - 1 : y + 1;
        SetBlock(x, y, z, turned);
        if (Blocks.Get(GetBlock(x, other, z)).Toggled is { } partner) SetBlock(x, other, z, partner);
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
