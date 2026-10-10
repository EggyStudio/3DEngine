namespace Engine.Game;

/// <summary>Where a structure stands: its corner at the lowest x and z, the height it is built from, and a hash that varies it.</summary>
public readonly record struct StructureStart(int X, int Y, int Z, uint Hash);

/// <summary>
/// A building or a room the generator places, one at most in each cell of 64 by 64 blocks, where its
/// chance falls and the land at its corner suits it.
/// </summary>
/// <remarks>
/// Where a structure starts depends on the seed and the cell alone, and whether it is built there on
/// the land the generator makes at that place, so every column it crosses finds the same start and
/// builds its own part through a <see cref="ColumnClip"/>. A structure is built after the trees, and
/// clears its own space, so no tree grows inside a house.
/// </remarks>
public abstract class Structure
{
    public const int Cell = 64;

    /// <summary>Its name, which <c>voxel.find</c> takes.</summary>
    public abstract string Name { get; }

    /// <summary>How wide it is along x and z, which must fit in a cell.</summary>
    public abstract int Size { get; }

    /// <summary>The share of cells, in hundredths, that try to hold one.</summary>
    protected abstract int Chance { get; }

    /// <summary>A number of its own mixed into the seed, so the structures' cells do not line up.</summary>
    protected abstract int Salt { get; }

    /// <summary>Whether it is built where the land at its corner is of a biome and height, the height it is built from given back.</summary>
    protected abstract bool Suits(Biome biome, int ground, uint hash, out int y);

    /// <summary>Whether the land around a start suits it beyond its corner's, as a village asks for ground that is nearly level.</summary>
    protected virtual bool Fits(Overworld land, int x, int z) => true;

    /// <summary>Builds the part of it that falls in a column, reading the ground of any place it stands on from <paramref name="land"/>.</summary>
    public abstract void Build(ColumnClip clip, StructureStart start, Overworld land);

    /// <summary>Where it starts in a cell, or null where the cell holds none.</summary>
    public StructureStart? StartIn(int cellX, int cellZ, Overworld land)
    {
        var hash = PlaceHash.Of(cellX, cellZ, land.Seed ^ Salt);
        if (hash % 100 >= Chance) return null;
        var room = Cell - Size;
        int x = cellX * Cell + (int)(hash / 100 % (uint)room), z = cellZ * Cell + (int)(hash / 10000 % (uint)room);
        var (ground, _, _, biome) = land.At(x + Size / 2, z + Size / 2);
        return Suits(biome, ground, hash, out var y) && Fits(land, x, z) ? new StructureStart(x, y, z, hash) : null;
    }

    /// <summary>How far past its size it builds on each side, as a roof's eaves or the space a pyramid clears around it.</summary>
    protected const int Margin = 2;

    /// <summary>Whether no tree grows within its area and margin, as none stands on a village's roads.</summary>
    public virtual bool ClearsTrees => false;

    /// <summary>Every start of it whose area, its margin included, reaches into a box between two corners at the ground.</summary>
    public IEnumerable<StructureStart> StartsIn(int minX, int minZ, int maxX, int maxZ, Overworld land)
    {
        int cx0 = FloorDiv(minX - Size - Margin, Cell), cx1 = FloorDiv(maxX + Margin, Cell);
        int cz0 = FloorDiv(minZ - Size - Margin, Cell), cz1 = FloorDiv(maxZ + Margin, Cell);
        for (int cz = cz0; cz <= cz1; cz++)
            for (int cx = cx0; cx <= cx1; cx++)
                if (StartIn(cx, cz, land) is { } start && start.X - Margin <= maxX && start.X + Size - 1 + Margin >= minX
                    && start.Z - Margin <= maxZ && start.Z + Size - 1 + Margin >= minZ)
                    yield return start;
    }

    /// <summary>Builds every start of it that reaches into a column, its margin included.</summary>
    public void BuildIn(ColumnClip clip, Overworld land)
    {
        foreach (var start in StartsIn(clip.MinX, clip.MinZ, clip.MinX + Section.Mask, clip.MinZ + Section.Mask, land))
            Build(clip, start, land);
    }

    private static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);
}

/// <summary>Every kind of structure, built in this order.</summary>
public static class Structures
{
    public static readonly Structure[] All = [new Ravine(), new Dungeon(), new Village(), new Igloo(), new DesertPyramid()];

    public static Structure? Find(string name) => All.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A dome of snow on the snowy plains, entered through a low door on its west, a shroomlight on its floor.</summary>
public sealed class Igloo : Structure
{
    public override string Name => "igloo";
    public override int Size => 9;
    protected override int Chance => 40;
    protected override int Salt => 0x1c10;

    protected override bool Suits(Biome biome, int ground, uint hash, out int y)
    {
        y = ground;
        // Open snow alone, since the snowy taiga's spruces would hide it.
        return ground >= Overworld.SeaLevel && biome == Biomes.SnowyPlains;
    }

    public override void Build(ColumnClip clip, StructureStart start, Overworld land)
    {
        int cx = start.X + 4, cz = start.Z + 4, y = start.Y;
        // A hemisphere of radius 4 a block thick, its floor of snow, standing on a footing of snow.
        for (int dz = -4; dz <= 4; dz++)
            for (int dx = -4; dx <= 4; dx++)
            {
                var flat = dx * dx + dz * dz;
                if (flat > 20) continue;
                for (int d = 1; d <= 4 && clip.Get(cx + dx, y - d, cz + dz) == BlockId.Air; d++)
                    clip.Set(cx + dx, y - d, cz + dz, BlockId.Snow);
                clip.Set(cx + dx, y, cz + dz, BlockId.Snow);
                for (int dy = 1; dy <= 4; dy++)
                {
                    var r = flat + dy * dy;
                    clip.Set(cx + dx, y + dy, cz + dz, r > 9 && r <= 20 ? BlockId.Snow : BlockId.Air);
                }
            }
        clip.Fill(cx - 4, y + 1, cz, cx - 2, y + 2, cz, BlockId.Air);
        clip.Set(cx, y, cz, BlockId.Shroomlight);
    }
}

/// <summary>A stepped pyramid of sandstone in the desert, a dark chamber at its heart with a floor of colored blocks, entered from the north.</summary>
public sealed class DesertPyramid : Structure
{
    public override string Name => "pyramid";
    public override int Size => 15;
    protected override int Chance => 45;
    protected override int Salt => 0x7e3a;

    protected override bool Suits(Biome biome, int ground, uint hash, out int y)
    {
        y = ground;
        return ground >= Overworld.SeaLevel && biome == Biomes.Desert;
    }

    public override void Build(ColumnClip clip, StructureStart start, Overworld land)
    {
        int x0 = start.X, z0 = start.Z, y = start.Y;
        // Sandstone under the base down to the sand, then a layer for each step, two blocks narrower each.
        for (int z = z0; z < z0 + 15; z++)
            for (int x = x0; x < x0 + 15; x++)
                for (int d = 1; d <= 6 && clip.Get(x, y - d, z) is BlockId.Air or BlockId.Cactus; d++)
                    clip.Set(x, y - d, z, BlockId.Sandstone);
        clip.Fill(x0 - 2, y + 1, z0 - 2, x0 + 16, y + 12, z0 + 16, BlockId.Air);
        for (int step = 0; step <= 7; step++)
            clip.Fill(x0 + step, y + step, z0 + step, x0 + 14 - step, y + step, z0 + 14 - step, BlockId.Sandstone);
        clip.Fill(x0 + 4, y + 1, z0 + 4, x0 + 10, y + 3, z0 + 10, BlockId.Air);
        // The way in, three wide and two high, through the north face to the chamber.
        clip.Fill(x0 + 6, y + 1, z0, x0 + 8, y + 2, z0 + 4, BlockId.Air);
        // The chamber's floor, blue at its middle and red at its corners.
        clip.Fill(x0 + 6, y, z0 + 6, x0 + 8, y, z0 + 8, BlockId.BlueConcrete);
        foreach (var (cx, cz) in new[] { (x0 + 4, z0 + 4), (x0 + 10, z0 + 4), (x0 + 4, z0 + 10), (x0 + 10, z0 + 10) })
            clip.Set(cx, y, cz, BlockId.RedConcrete);
    }
}

/// <summary>A room of cobblestone deep under the ground, a magma block at its middle where Minecraft's has its spawner.</summary>
public sealed class Dungeon : Structure
{
    public override string Name => "dungeon";
    public override int Size => 7;
    protected override int Chance => 60;
    protected override int Salt => 0x0d06;

    protected override bool Suits(Biome biome, int ground, uint hash, out int y)
    {
        y = 12 + (int)(hash >> 24 & 31);
        return y < ground - 10;
    }

    public override void Build(ColumnClip clip, StructureStart start, Overworld land)
    {
        int x0 = start.X, z0 = start.Z, y = start.Y;
        clip.Fill(x0, y, z0, x0 + 6, y + 5, z0 + 6, BlockId.Cobblestone);
        clip.Fill(x0 + 1, y + 1, z0 + 1, x0 + 5, y + 4, z0 + 5, BlockId.Air);
        clip.Set(x0 + 3, y + 1, z0 + 3, BlockId.Magma);
    }
}
