namespace Engine.Game;

/// <summary>
/// Water that flows, as Minecraft's does: down into the air under it as falling water, and where it
/// cannot fall, sideways one level further from its source for each block, up to seven, drying
/// back as its source goes. Two sources beside each other over ground make a third between them.
/// </summary>
/// <remarks>
/// <para>
/// Water moves in ticks of 20 a second, a block every five as Minecraft's does. A block that
/// changes asks the water at and around it to look again at what feeds it five ticks on, and each
/// cell of water so asked takes the level its neighbors give it and spreads from there, so a still
/// lake stays still until something beside it changes. Each tick moves at most so many cells, so a
/// flood is spread over frames rather than taken in one.
/// </para>
/// <para>
/// What is waiting is not saved, so water half flowed when the world is left stands as it was when
/// the world is opened again, until a block beside it changes.
/// </para>
/// </remarks>
public sealed class Fluids(VoxelWorld world)
{
    private const float Tick = 1 / 20f;
    private const int Delay = 5;
    private const int MostPerTick = 4096;

    private readonly Queue<(int X, int Y, int Z, long Due)> _due = new();
    private readonly HashSet<(int X, int Y, int Z)> _waiting = [];
    private long _tick;
    private float _time;

    /// <summary>Cells of water waiting to look again at what feeds them.</summary>
    public int Waiting => _waiting.Count;

    /// <summary>Asks a cell and the six around it to look again, five ticks on.</summary>
    public void Around(int x, int y, int z)
    {
        Ask(x, y, z);
        for (int d = 0; d < 6; d++) Ask(x + Lighting.Dx[d], y + Lighting.Dy[d], z + Lighting.Dz[d]);
    }

    private void Ask(int x, int y, int z)
    {
        if (Blocks.IsWater(world.GetBlock(x, y, z)) && _waiting.Add((x, y, z))) _due.Enqueue((x, y, z, _tick + Delay));
    }

    /// <summary>Runs the ticks that <paramref name="seconds"/> brings.</summary>
    public void Update(float seconds)
    {
        _time += seconds;
        while (_time >= Tick)
        {
            _time -= Tick;
            _tick++;
            for (int moved = 0; moved < MostPerTick && _due.TryPeek(out var next) && next.Due <= _tick; moved++)
            {
                _due.Dequeue();
                _waiting.Remove((next.X, next.Y, next.Z));
                Flow(next.X, next.Y, next.Z);
            }
        }
    }

    // A cell of water takes what feeds it, a source what it is, flowing water one level further than
    // the nearest beside it, or falling water under water, and dries where nothing does; then it
    // falls into the air under it, or where it stands on something spreads sideways a level further.
    private void Flow(int x, int y, int z)
    {
        var block = world.GetBlock(x, y, z);
        if (!Blocks.IsWater(block)) return;

        if (block != BlockId.Water)
        {
            var fed = Fed(x, y, z);
            if (fed != block)
            {
                world.SetBlock(x, y, z, fed);
                return;
            }
        }

        var below = world.GetBlock(x, y - 1, z);
        if (below == BlockId.Air)
        {
            world.SetBlock(x, y - 1, z, BlockId.FallingWater);
            return;
        }
        // Water spreads sideways from ground under it or from a source, and not from the top of
        // flowing water, which carries it on itself.
        if (!Blocks.Collides(below) && below != BlockId.Water) return;
        var next = Blocks.WaterLevel(block) + 1;
        if (next > 7) return;
        for (int d = 0; d < 6; d++)
        {
            if (Lighting.Dy[d] != 0) continue;
            int nx = x + Lighting.Dx[d], nz = z + Lighting.Dz[d];
            var beside = world.GetBlock(nx, y, nz);
            if (beside == BlockId.Air || beside is >= BlockId.Water1 and <= BlockId.Water7 && Blocks.WaterLevel(beside) > next)
                world.SetBlock(nx, y, nz, Blocks.FlowingWater(next));
        }
    }

    // What a cell that is not a source should be by what feeds it.
    private BlockId Fed(int x, int y, int z)
    {
        if (Blocks.IsWater(world.GetBlock(x, y + 1, z))) return BlockId.FallingWater;
        int nearest = 8, sources = 0;
        for (int d = 0; d < 6; d++)
        {
            if (Lighting.Dy[d] != 0) continue;
            var beside = world.GetBlock(x + Lighting.Dx[d], y, z + Lighting.Dz[d]);
            if (!Blocks.IsWater(beside)) continue;
            if (beside == BlockId.Water) sources++;
            // Water beside that stands on falling water or on air carries none sideways.
            var under = world.GetBlock(x + Lighting.Dx[d], y - 1, z + Lighting.Dz[d]);
            if (beside != BlockId.Water && !Blocks.Collides(under) && under != BlockId.Water) continue;
            nearest = Math.Min(nearest, Blocks.WaterLevel(beside));
        }
        var ground = world.GetBlock(x, y - 1, z);
        if (sources >= 2 && (Blocks.Collides(ground) || ground == BlockId.Water)) return BlockId.Water;
        return nearest + 1 <= 7 ? Blocks.FlowingWater(nearest + 1) : BlockId.Air;
    }
}
