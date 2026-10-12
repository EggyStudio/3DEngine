namespace Engine.Game;

/// <summary>
/// The sections the eye can see through open cells, after Minecraft's visibility graph, in which
/// each section knows which of its six faces its open cells join, and a flood from the eye's
/// section passes from one section into the next only across faces so joined, never turning back
/// the way it came, so the land behind a closed room's walls or under the ground is left out. From
/// the eye's own section it leaves through the faces the open cells around the eye reach, where
/// Minecraft's leaves through all six, so a room closed within one section is drawn alone.
/// </summary>
/// <remarks>
/// <para>
/// A cell is open where its block is not opaque, as air, water, glass, torches and slabs are. A section's joins are worked out again the first time they are asked
/// after a block in it changes. The faces are numbered +x, -x, +y, -y, +z and -z, each the one before
/// its opposite.
/// </para>
/// <para>
/// The flood reaches the sections of the loaded columns within the render distance and the world's
/// heights. An eye above or below the world sees every section, since the sky over the world's top
/// is not a section the flood could pass through.
/// </para>
/// </remarks>
public sealed class Visibility
{
    private static readonly (int X, int Y, int Z)[] Steps = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    private readonly HashSet<SectionKey> _seen = [];
    private readonly Queue<(SectionKey Key, int From, int Ways)> _queue = new();

    /// <summary>The sections the last <see cref="Flood"/> reached.</summary>
    public IReadOnlySet<SectionKey> Seen => _seen;

    /// <summary>Whether the last flood left anything out, false where the eye stood outside the world's heights.</summary>
    public bool Culled { get; private set; }

    /// <summary>Which pairs of a section's faces its open cells join, a bit for each ordered pair, the bit of faces a and b at a times 6 plus b.</summary>
    public static ulong Joins(Section section)
    {
        if (section.Visibility.Version == section.Version) return section.Visibility.Joins;
        var joins = Work(section);
        section.Visibility = (joins, section.Version);
        return joins;
    }

    // The faces of a section that the run of open cells holding a cell reaches, every face where
    // the cell is not open, as for an eye inside a block.
    private static int FacesFrom(Section section, int cell)
    {
        if (Blocks.IsOpaque((BlockId)section.Blocks[cell])) return 63;
        var seen = new bool[Section.Volume];
        var stack = new int[Section.Volume];
        int faces = 0, top = 0;
        stack[top++] = cell;
        seen[cell] = true;
        while (top > 0)
        {
            var i = stack[--top];
            int x = i & Section.Mask, y = i >> 8, z = (i >> 4) & Section.Mask;
            faces |= (x == Section.Mask ? 1 : 0) | (x == 0 ? 2 : 0) | (y == Section.Mask ? 4 : 0) | (y == 0 ? 8 : 0) | (z == Section.Mask ? 16 : 0) | (z == 0 ? 32 : 0);
            foreach (var (step, edge) in (ReadOnlySpan<(int, bool)>)[(1, x < Section.Mask), (-1, x > 0), (256, y < Section.Mask), (-256, y > 0), (16, z < Section.Mask), (-16, z > 0)])
            {
                var n = i + step;
                if (!edge || seen[n] || Blocks.IsOpaque((BlockId)section.Blocks[n])) continue;
                seen[n] = true;
                stack[top++] = n;
            }
        }
        return faces;
    }

    private static ulong Work(Section section)
    {
        const ulong all = (1UL << 36) - 1;
        if (section.Filled == 0) return all;
        var open = new bool[Section.Volume];
        var any = false;
        for (int i = 0; i < Section.Volume; i++) any |= open[i] = !Blocks.IsOpaque((BlockId)section.Blocks[i]);
        if (!any) return 0;

        ulong joins = 0;
        var stack = new int[Section.Volume];
        for (int start = 0; start < Section.Volume; start++)
        {
            if (!open[start]) continue;
            // A flood through this run of open cells, noting the faces it touches, its cells closed as it goes.
            int faces = 0, top = 0;
            stack[top++] = start;
            open[start] = false;
            while (top > 0)
            {
                var i = stack[--top];
                int x = i & Section.Mask, y = i >> 8, z = (i >> 4) & Section.Mask;
                if (x == Section.Mask) faces |= 1;
                if (x == 0) faces |= 2;
                if (y == Section.Mask) faces |= 4;
                if (y == 0) faces |= 8;
                if (z == Section.Mask) faces |= 16;
                if (z == 0) faces |= 32;
                if (x < Section.Mask && open[i + 1]) { open[i + 1] = false; stack[top++] = i + 1; }
                if (x > 0 && open[i - 1]) { open[i - 1] = false; stack[top++] = i - 1; }
                if (y < Section.Mask && open[i + 256]) { open[i + 256] = false; stack[top++] = i + 256; }
                if (y > 0 && open[i - 256]) { open[i - 256] = false; stack[top++] = i - 256; }
                if (z < Section.Mask && open[i + 16]) { open[i + 16] = false; stack[top++] = i + 16; }
                if (z > 0 && open[i - 16]) { open[i - 16] = false; stack[top++] = i - 16; }
            }
            for (int a = 0; a < 6; a++)
                for (int b = 0; b < 6; b++)
                    if ((faces >> a & 1) != 0 && (faces >> b & 1) != 0) joins |= 1UL << (a * 6 + b);
        }
        return joins;
    }

    /// <summary>
    /// Floods from the section holding the eye through the sections of the loaded columns within a
    /// render distance of it, as <see cref="Seen"/> then holds.
    /// </summary>
    public void Flood(VoxelWorld world, System.Numerics.Vector3 eye, int renderDistance)
    {
        _seen.Clear();
        _queue.Clear();
        var start = new SectionKey((int)MathF.Floor(eye.X) >> Section.Shift, (int)MathF.Floor(eye.Y) >> Section.Shift, (int)MathF.Floor(eye.Z) >> Section.Shift);
        Culled = start.Y is >= 0 and < ChunkColumn.SectionCount;
        if (!Culled) return;
        var reach = renderDistance * renderDistance + renderDistance;
        _seen.Add(start);
        _queue.Enqueue((start, -1, 0));
        var exits = world.TryGetColumn(start.X, start.Z, out var home)
            ? FacesFrom(home.Sections[start.Y], Section.Index((int)MathF.Floor(eye.X) & Section.Mask, (int)MathF.Floor(eye.Y) & Section.Mask, (int)MathF.Floor(eye.Z) & Section.Mask))
            : 63;
        while (_queue.TryDequeue(out var at))
        {
            if (!world.TryGetColumn(at.Key.X, at.Key.Z, out var column)) continue;
            var joins = Joins(column.Sections[at.Key.Y]);
            for (int d = 0; d < 6; d++)
            {
                // Never back the way any step of the flood went, and out only through a face the cells
                // joined to the one it came in by.
                if ((at.Ways & 1 << (d ^ 1)) != 0) continue;
                if (at.From >= 0 ? (joins >> (at.From * 6 + d) & 1) == 0 : (exits >> d & 1) == 0) continue;
                var next = new SectionKey(at.Key.X + Steps[d].X, at.Key.Y + Steps[d].Y, at.Key.Z + Steps[d].Z);
                if (next.Y is < 0 or >= ChunkColumn.SectionCount || !_seen.Add(next)) continue;
                int dx = next.X - start.X, dz = next.Z - start.Z;
                if (dx * dx + dz * dz > reach || !world.HasColumn(next.X, next.Z)) continue;
                _queue.Enqueue((next, d ^ 1, at.Ways | 1 << d));
            }
        }
    }

    /// <summary>Whether the last flood reached a section, every one where it left nothing out.</summary>
    public bool Sees(SectionKey key) => !Culled || _seen.Contains(key);
}
