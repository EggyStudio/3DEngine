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

    /// <summary>Builds the part of it that falls in a column.</summary>
    public abstract void Build(ColumnClip clip, StructureStart start);

    /// <summary>Where it starts in a cell, or null where the cell holds none.</summary>
    public StructureStart? StartIn(int cellX, int cellZ, Overworld land)
    {
        var hash = PlaceHash.Of(cellX, cellZ, land.Seed ^ Salt);
        if (hash % 100 >= Chance) return null;
        var room = Cell - Size;
        int x = cellX * Cell + (int)(hash / 100 % (uint)room), z = cellZ * Cell + (int)(hash / 10000 % (uint)room);
        var (ground, _, _, biome) = land.At(x + Size / 2, z + Size / 2);
        return Suits(biome, ground, hash, out var y) ? new StructureStart(x, y, z, hash) : null;
    }

    /// <summary>How far past its size it builds on each side, as a roof's eaves or the space a pyramid clears around it.</summary>
    protected const int Margin = 2;

    /// <summary>Builds every start of it that reaches into a column, its margin included.</summary>
    public void BuildIn(ColumnClip clip, Overworld land)
    {
        int cx0 = FloorDiv(clip.MinX - Size - Margin, Cell), cx1 = FloorDiv(clip.MinX + Section.Size + Margin, Cell);
        int cz0 = FloorDiv(clip.MinZ - Size - Margin, Cell), cz1 = FloorDiv(clip.MinZ + Section.Size + Margin, Cell);
        for (int cz = cz0; cz <= cz1; cz++)
            for (int cx = cx0; cx <= cx1; cx++)
                if (StartIn(cx, cz, land) is { } start && clip.Touches(start.X - Margin, start.Z - Margin, start.X + Size - 1 + Margin, start.Z + Size - 1 + Margin))
                    Build(clip, start);
    }

    private static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);
}

/// <summary>Every kind of structure, built in this order.</summary>
public static class Structures
{
    public static readonly Structure[] All = [new Dungeon(), new VillageHouse(), new Igloo(), new DesertPyramid()];

    public static Structure? Find(string name) => All.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A small house of planks with log corners on a cobblestone floor, a door with a torch over it, windows, and a lantern hanging from its ceiling.</summary>
public sealed class VillageHouse : Structure
{
    public override string Name => "house";
    public override int Size => 7;
    protected override int Chance => 35;
    protected override int Salt => 0x5a17;

    protected override bool Suits(Biome biome, int ground, uint hash, out int y)
    {
        y = ground;
        return ground >= Overworld.SeaLevel && (biome == Biomes.Plains || biome == Biomes.Forest || biome == Biomes.BirchForest || biome == Biomes.Taiga);
    }

    public override void Build(ColumnClip clip, StructureStart start)
    {
        int x0 = start.X, z0 = start.Z, y = start.Y, x1 = x0 + 6, z1 = z0 + 6;
        // A cobblestone footing down to the ground under each place of the floor, so a house on a
        // slope stands on it rather than over a gap.
        for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                for (int d = 1; d <= 6 && clip.Get(x, y - d, z) is BlockId.Air or BlockId.OakLeaves or BlockId.BirchLeaves or BlockId.SpruceLeaves; d++)
                    clip.Set(x, y - d, z, BlockId.Cobblestone);
        clip.Fill(x0, y, z0, x1, y, z1, BlockId.Cobblestone);
        clip.Fill(x0, y + 1, z0, x1, y + 8, z1, BlockId.Air);
        clip.Fill(x0, y + 1, z0, x1, y + 4, z1, BlockId.OakPlanks);
        clip.Fill(x0 + 1, y + 1, z0 + 1, x1 - 1, y + 4, z1 - 1, BlockId.Air);
        foreach (var (cx, cz) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
            clip.Fill(cx, y + 1, cz, cx, y + 4, cz, BlockId.OakLog);
        // A roof of planks a block wider than the walls, and a smaller one above it.
        clip.Fill(x0 - 1, y + 5, z0 - 1, x1 + 1, y + 5, z1 + 1, BlockId.OakPlanks);
        clip.Fill(x0 + 1, y + 6, z0 + 1, x1 - 1, y + 6, z1 - 1, BlockId.OakPlanks);
        clip.Set(x0 + 3, y + 4, z0 + 3, BlockId.HangingLantern);

        // The door in a wall the hash picks, a torch on the wall over it outside, and a window in the
        // middle of each other wall.
        var door = (int)(start.Hash >> 20 & 3);
        (int X, int Z, int Dx, int Dz, BlockId Torch)[] middles =
        [
            (x0 + 3, z0, 0, -1, BlockId.WallTorchNorth), (x1, z0 + 3, 1, 0, BlockId.WallTorchEast),
            (x0 + 3, z1, 0, 1, BlockId.WallTorchSouth), (x0, z0 + 3, -1, 0, BlockId.WallTorchWest),
        ];
        for (int side = 0; side < 4; side++)
        {
            var (mx, mz, dx, dz, torch) = middles[side];
            if (side != door)
            {
                clip.Set(mx, y + 2, mz, BlockId.Air);
                continue;
            }
            clip.Fill(mx, y + 1, mz, mx, y + 2, mz, BlockId.Air);
            clip.Set(mx + dx, y + 3, mz + dz, torch);
        }
    }
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

    public override void Build(ColumnClip clip, StructureStart start)
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

    public override void Build(ColumnClip clip, StructureStart start)
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

    public override void Build(ColumnClip clip, StructureStart start)
    {
        int x0 = start.X, z0 = start.Z, y = start.Y;
        clip.Fill(x0, y, z0, x0 + 6, y + 5, z0 + 6, BlockId.Cobblestone);
        clip.Fill(x0 + 1, y + 1, z0 + 1, x0 + 5, y + 4, z0 + 5, BlockId.Air);
        clip.Set(x0 + 3, y + 1, z0 + 3, BlockId.Magma);
    }
}
