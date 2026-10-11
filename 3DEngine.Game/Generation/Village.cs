namespace Engine.Game;

/// <summary>
/// A village in the plains, the taiga, the snowy plains or the desert: a well at its middle with a
/// lantern under its roof, four roads of dirt path from it, houses, a forge, farms of wheat and
/// piles of hay beside the roads, and lamp posts along them, each village of its biome's wood and
/// stone.
/// </summary>
/// <remarks>
/// <para>
/// Every piece stands on the ground the generator makes at its own place, which it can tell for a
/// column not generated, so each column the village crosses builds the same village. A road follows
/// the ground block by block, crossing water on planks, and a building stands at the height of its
/// middle on a footing of stone down to the ground.
/// </para>
/// <para>
/// Each road has a near plot on its right and a far one on its left, so the four near plots fill
/// the four corners around the well and the far ones lie past them, and no two meet. A plot's front
/// faces its road, and a building is laid out in the plot's own frame and turned with it, a torch
/// over its door included.
/// </para>
/// </remarks>
public sealed class Village : Structure
{
    public override string Name => "village";
    public override int Size => 2 * Half;
    protected override int Chance => 30;
    protected override int Salt => 0x7111;

    private const int Half = 24;
    // How far from the well along its road each plot stands, and how far from the road's middle
    // line a plot's front wall stands, which leaves a block between a road and a front for a lamp post.
    private const int Near = 9, Far = 19, Front = 4;

    // North, east, south and west, each road's way out from the well.
    private static readonly (int X, int Z)[] Ways = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    /// <summary>What a village of a biome is built from, its houses' roofs of stairs where it has them and flat where not.</summary>
    private sealed record Palette(BlockId Wall, BlockId Corner, BlockId Floor, BlockId Roof, BlockId Post, BlockId? Stairs);

    private static Palette? For(Biome biome) =>
        biome == Biomes.Plains ? new(BlockId.OakPlanks, BlockId.OakLog, BlockId.Cobblestone, BlockId.OakPlanks, BlockId.Lantern, BlockId.OakStairs)
        : biome == Biomes.Taiga ? new(BlockId.SprucePlanks, BlockId.SpruceLog, BlockId.Cobblestone, BlockId.SprucePlanks, BlockId.Lantern, BlockId.SpruceStairs)
        : biome == Biomes.SnowyPlains ? new(BlockId.SprucePlanks, BlockId.SpruceLog, BlockId.Cobblestone, BlockId.Snow, BlockId.Lantern, BlockId.SpruceStairs)
        : biome == Biomes.Desert ? new(BlockId.Sandstone, BlockId.Sandstone, BlockId.Sandstone, BlockId.Sandstone, BlockId.Torch, null)
        : null;

    protected override bool Suits(Biome biome, int ground, uint hash, out int y)
    {
        y = ground;
        return ground > Overworld.SeaLevel && For(biome) is not null;
    }

    public override bool ClearsTrees => true;

    // The ground at the middle and at the four roads' ends lies within ten blocks, so the roads climb
    // no cliff and the buildings stand on short footings, and all five are of one biome, so a village
    // of the plains does not run into a forest.
    protected override bool Fits(Overworld land, int x, int z)
    {
        int cx = x + Half, cz = z + Half, low = int.MaxValue, high = int.MinValue;
        var biome = land.At(cx, cz).Biome;
        foreach (var (ux, uz) in Ways)
        {
            var (height, _, _, here) = land.At(cx + ux * 18, cz + uz * 18);
            if (here != biome) return false;
            (low, high) = (Math.Min(low, height), Math.Max(high, height));
        }
        var middle = land.At(cx, cz).Height;
        return Math.Max(high, middle) - Math.Min(low, middle) <= 10;
    }

    public override void Build(ColumnClip clip, StructureStart start, Overworld land)
    {
        int cx = start.X + Half, cz = start.Z + Half;
        var palette = For(land.At(cx, cz).Biome) ?? For(Biomes.Plains)!;

        for (int road = 0; road < 4; road++)
        {
            var (ux, uz) = Ways[road];
            var length = 20 + (int)(Mix(start.Hash, road) % 4);
            for (int t = 3; t <= length; t++)
                for (int w = -1; w <= 1; w++)
                    Path(clip, land, cx + ux * t - uz * w, cz + uz * t + ux * w);
        }

        Well(clip, land, cx, cz);

        for (int road = 0; road < 4; road++)
        {
            var (ux, uz) = Ways[road];
            // The road's right, seen walking out along it, and the plot's front faces back across it.
            int vx = -uz, vz = ux;
            foreach (var (along, side, salt) in (ReadOnlySpan<(int, int, int)>)[(Near, 1, 10), (Far, -1, 20)])
            {
                int ox = cx + ux * along + vx * side * Front, oz = cz + uz * along + vz * side * Front;
                var turn = TurnFacing(-vx * side, -vz * side);
                var frame = new Frame(clip, ox, 0, oz, turn);
                var kind = Mix(start.Hash, road + salt) % 9;
                // The ground under the middle of the building's depth, 7 but for a pile of hay's 5.
                var (mx, mz) = frame.At(0, kind < 8 ? 3 : 2);
                frame = frame with { Y = land.At(mx, mz).Height };
                if (kind < 3) SmallHouse(frame, palette);
                else if (kind < 5) LongHouse(frame, palette);
                else if (kind < 7) Farm(frame, palette);
                else if (kind < 8) Forge(frame, palette);
                else HayPile(frame);
            }

            // Lamp posts beside the road, clear of the plots' doors.
            foreach (var (along, side) in (ReadOnlySpan<(int, int)>)[(5, 1), (14, -1)])
            {
                int x = cx + ux * along + vx * side * 2, z = cz + uz * along + vz * side * 2;
                if (!clip.Inside(x, 0, z)) continue;
                var ground = land.At(x, z).Height;
                if (ground < Overworld.SeaLevel) continue;
                clip.Fill(x, ground + 1, z, x, ground + 2, z, BlockId.OakFence);
                clip.Set(x, ground + 3, z, palette.Post);
            }
        }
    }

    private static uint Mix(uint hash, int part) => PlaceHash.Of(part, 0x51ed, (int)hash);

    // A block of road on the ground at a place, planks over water, with room cleared above it.
    private static void Path(ColumnClip clip, Overworld land, int x, int z)
    {
        if (!clip.Inside(x, 0, z)) return;
        var ground = land.At(x, z).Height;
        var y = Math.Max(ground, Overworld.SeaLevel);
        clip.Set(x, y, z, ground < Overworld.SeaLevel ? BlockId.OakPlanks : BlockId.DirtPath);
        clip.Fill(x, y + 1, z, x, y + 4, z, BlockId.Air);
    }

    // A well of cobblestone five blocks a side around water, its roof on four fence posts and a
    // lantern hanging under it over the water.
    private static void Well(ColumnClip clip, Overworld land, int cx, int cz)
    {
        var y = land.At(cx, cz).Height;
        clip.Fill(cx - 2, y - 3, cz - 2, cx + 2, y, cz + 2, BlockId.Cobblestone);
        clip.Fill(cx - 2, y + 1, cz - 2, cx + 2, y + 6, cz + 2, BlockId.Air);
        clip.Fill(cx - 2, y + 1, cz - 2, cx + 2, y + 1, cz + 2, BlockId.Cobblestone);
        clip.Fill(cx - 1, y - 2, cz - 1, cx + 1, y + 1, cz + 1, BlockId.Water);
        foreach (var (dx, dz) in (ReadOnlySpan<(int, int)>)[(-2, -2), (2, -2), (-2, 2), (2, 2)])
            clip.Fill(cx + dx, y + 2, cz + dz, cx + dx, y + 3, cz + dz, BlockId.OakFence);
        clip.Fill(cx - 2, y + 4, cz - 2, cx + 2, y + 4, cz + 2, BlockId.Cobblestone);
        clip.Set(cx, y + 3, cz, BlockId.HangingLantern);
    }

    // The quarter turns that face a plot's front, which its own frame gives as north, the way given.
    private static int TurnFacing(int x, int z) => (x, z) switch
    {
        (0, -1) => 0,
        (1, 0) => 1,
        (0, 1) => 2,
        _ => 3,
    };

    /// <summary>
    /// A plot's own frame: x along its front, z from its front wall back, y from its floor, turned a
    /// number of quarters clockwise so its front, north in its frame, faces its road.
    /// </summary>
    private readonly record struct Frame(ColumnClip Clip, int X, int Y, int Z, int Turn)
    {
        public (int X, int Z) At(int x, int z) => Turn switch
        {
            0 => (X + x, Z + z),
            1 => (X - z, Z + x),
            2 => (X - x, Z - z),
            _ => (X + z, Z - x),
        };

        public BlockId Get(int x, int y, int z)
        {
            var (wx, wz) = At(x, z);
            return Clip.Get(wx, Y + y, wz);
        }

        public void Set(int x, int y, int z, BlockId block)
        {
            var (wx, wz) = At(x, z);
            Clip.Set(wx, Y + y, wz, Turned(block));
        }

        public void Fill(int x0, int y0, int z0, int x1, int y1, int z1, BlockId block)
        {
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        Set(x, y, z, block);
        }

        // A block facing a side is turned with the plot, so a torch or stairs given facing north face its front.
        private BlockId Turned(BlockId block)
        {
            var info = Blocks.Get(block);
            if (info.Shape is not { Facing: not (Facing.Up or Facing.Down) } shape) return block;
            Facing[] round = [Facing.North, Facing.East, Facing.South, Facing.West];
            return Blocks.Variant(info.Item, round[(Array.IndexOf(round, shape.Facing) + Turn) % 4], shape.Upper);
        }

        // Stone under each place of a footprint down to the ground, so a building on a slope stands
        // on it rather than over a gap, and the space above it cleared of the trees.
        public void Ground(int x0, int x1, int depth, BlockId footing, int height)
        {
            for (int z = 0; z < depth; z++)
                for (int x = x0; x <= x1; x++)
                    for (int d = 1; d <= 6 && Get(x, -d, z) is BlockId.Air or BlockId.Water or BlockId.OakLeaves or BlockId.SpruceLeaves or BlockId.BirchLeaves; d++)
                        Set(x, -d, z, footing);
            Fill(x0 - 1, 1, -1, x1 + 1, height, depth, BlockId.Air);
        }
    }

    // Walls of a palette around a floor, logs at the corners, the inside cleared, and a shut door in
    // the front's middle, facing in, with a torch over it outside.
    private static void Shell(Frame f, Palette p, int half, int depth)
    {
        f.Ground(-half, half, depth, p.Floor, 9);
        f.Fill(-half, 0, 0, half, 0, depth - 1, p.Floor);
        f.Fill(-half, 1, 0, half, 4, depth - 1, p.Wall);
        f.Fill(-half + 1, 1, 1, half - 1, 4, depth - 2, BlockId.Air);
        foreach (var (x, z) in (ReadOnlySpan<(int, int)>)[(-half, 0), (half, 0), (-half, depth - 1), (half, depth - 1)])
            f.Fill(x, 1, z, x, 4, z, p.Corner);
        f.Set(0, 1, 0, BlockId.OakDoorSouth);
        f.Set(0, 2, 0, BlockId.OakDoorSouthTop);
        f.Set(0, 3, -1, BlockId.WallTorchNorth);
    }

    // A small house of seven by seven, a window of glass in each other wall and a lantern hanging
    // from its roof.
    private static void SmallHouse(Frame f, Palette p)
    {
        Shell(f, p, 3, 7);
        var hang = Roof(f, p, 3);
        foreach (var (x, z) in (ReadOnlySpan<(int, int)>)[(-3, 3), (3, 3), (0, 6)]) f.Set(x, 2, z, BlockId.Glass);
        f.Set(0, hang, 3, BlockId.HangingLantern);
    }

    // A long house of nine by seven, two windows a wall and two lanterns.
    private static void LongHouse(Frame f, Palette p)
    {
        Shell(f, p, 4, 7);
        var hang = Roof(f, p, 4);
        foreach (var (x, z) in (ReadOnlySpan<(int, int)>)[(-2, 0), (2, 0), (-2, 6), (2, 6), (-4, 3), (4, 3)]) f.Set(x, 2, z, BlockId.Glass);
        f.Set(-2, hang, 3, BlockId.HangingLantern);
        f.Set(2, hang, 3, BlockId.HangingLantern);
    }

    // The roof over walls seven deep and a half width across: of stairs climbing from the front and
    // the back to a ridge along the width, the gable ends filled with the wall's block, or flat of
    // the roof's block in two layers where the village has no stairs. Gives the height a lantern
    // hangs at under its middle.
    private static int Roof(Frame f, Palette p, int half)
    {
        if (p.Stairs is not { } stairs)
        {
            f.Fill(-half - 1, 5, -1, half + 1, 5, 7, p.Roof);
            f.Fill(-half + 1, 6, 1, half - 1, 6, 5, p.Roof);
            return 4;
        }
        var back = Blocks.Variant(stairs, Facing.North);
        var front = Blocks.Variant(stairs, Facing.South);
        for (int k = 0; k <= 3; k++)
        {
            f.Fill(-half - 1, 4 + k, -1 + k, half + 1, 4 + k, -1 + k, front);
            f.Fill(-half - 1, 4 + k, 7 - k, half + 1, 4 + k, 7 - k, back);
            if (k == 0) continue;
            f.Fill(-half, 4 + k, k, -half, 4 + k, 6 - k, p.Wall);
            f.Fill(half, 4 + k, k, half, 4 + k, 6 - k, p.Wall);
        }
        f.Fill(-half - 1, 8, 3, half + 1, 8, 3, p.Roof);
        return 7;
    }

    // A forge open at its front, a hearth of magma in its back corner under a chimney, and a torch on
    // its back wall.
    private static void Forge(Frame f, Palette p)
    {
        f.Ground(-3, 3, 7, BlockId.Cobblestone, 8);
        f.Fill(-3, 0, 0, 3, 0, 6, BlockId.Cobblestone);
        f.Fill(-3, 1, 6, 3, 4, 6, BlockId.Cobblestone);
        f.Fill(-3, 1, 3, -3, 2, 5, BlockId.Cobblestone);
        f.Fill(3, 1, 3, 3, 2, 5, BlockId.Cobblestone);
        foreach (var (x, z) in (ReadOnlySpan<(int, int)>)[(-3, 0), (3, 0), (-3, 6), (3, 6)])
            f.Fill(x, 1, z, x, 4, z, p.Corner);
        f.Fill(-3, 5, 0, 3, 5, 6, p.Roof);
        f.Fill(1, 1, 4, 2, 1, 5, BlockId.Cobblestone);
        f.Set(2, 1, 5, BlockId.Magma);
        f.Fill(2, 2, 5, 2, 7, 5, BlockId.Cobblestone);
        f.Set(-1, 3, 5, BlockId.WallTorchNorth);
    }

    // A field of wheat on farmland in a border of logs, a row of water through its middle.
    private static void Farm(Frame f, Palette p)
    {
        f.Ground(-4, 4, 7, BlockId.Dirt, 5);
        f.Fill(-4, 0, 0, 4, 0, 6, p.Corner);
        f.Fill(-3, 0, 1, 3, 0, 5, BlockId.Farmland);
        f.Fill(-3, 0, 3, 3, 0, 3, BlockId.Water);
        f.Fill(-3, 1, 1, 3, 1, 2, BlockId.Wheat);
        f.Fill(-3, 1, 4, 3, 1, 5, BlockId.Wheat);
    }

    // A few bales of hay piled on the ground.
    private static void HayPile(Frame f)
    {
        f.Ground(-2, 2, 5, BlockId.Dirt, 4);
        f.Fill(-1, 1, 1, 1, 1, 3, BlockId.HayBale);
        f.Fill(-1, 2, 2, 0, 2, 2, BlockId.HayBale);
    }
}
