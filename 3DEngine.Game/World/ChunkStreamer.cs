using System.Collections.Concurrent;
using System.Numerics;

namespace Engine.Game;

/// <summary>
/// Loads the columns around the player and unloads those left behind, generating each on a worker
/// and taking it into the world on the main thread.
/// </summary>
/// <remarks>
/// Columns are loaded two further than the render distance, since a section on the edge of what is
/// drawn is meshed only once the eight columns around it are there, its corners shaded by their
/// blocks and light, and unloaded three further, so walking back and forth over a boundary does
/// not load and unload the same columns each time.
/// </remarks>
public sealed class ChunkStreamer(VoxelWorld world, ChunkRenderer renderer)
{
    private readonly ConcurrentQueue<(int X, int Z, ChunkColumn? Column)> _done = new();
    private readonly HashSet<(int X, int Z)> _pending = [];
    private readonly List<(int X, int Z)> _wanted = [];
    private readonly List<(int X, int Z)> _leaving = [];
    private readonly int _workers = Math.Clamp(Environment.ProcessorCount - 2, 1, 8);
    private (int X, int Z, int Distance)? _planned;
    private int _running;

    /// <summary>Columns asked for and not yet in the world.</summary>
    public int Pending => _pending.Count;

    /// <summary>Generates the columns around a point on this thread, so the player has ground under them before the first frame.</summary>
    public void LoadNow(Vector3 at, int radius)
    {
        int cx = VoxelWorld.ColumnOf((int)MathF.Floor(at.X)), cz = VoxelWorld.ColumnOf((int)MathF.Floor(at.Z));
        for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
                if (!world.HasColumn(x, z)) world.Add(Generate(world.Generator, x, z));
    }

    /// <summary>Takes in the columns the workers finished, asks for those now in reach, and unloads those out of it.</summary>
    public void Update(Vector3 player, int renderDistance)
    {
        while (_done.TryDequeue(out var result))
        {
            Interlocked.Decrement(ref _running);
            _pending.Remove((result.X, result.Z));
            if (result.Column is not null) world.Add(result.Column);
        }

        int cx = VoxelWorld.ColumnOf((int)MathF.Floor(player.X)), cz = VoxelWorld.ColumnOf((int)MathF.Floor(player.Z));
        var load = renderDistance + 2;
        if (_planned != (cx, cz, renderDistance))
        {
            _planned = (cx, cz, renderDistance);
            Plan(cx, cz, load);
            Unload(cx, cz, load + 1);
        }

        foreach (var (x, z) in _wanted)
        {
            if (_running >= _workers) break;
            if (world.HasColumn(x, z) || !_pending.Add((x, z))) continue;
            Interlocked.Increment(ref _running);
            var generator = world.Generator;
            Task.Run(() =>
            {
                ChunkColumn? column = null;
                try
                {
                    column = Generate(generator, x, z);
                }
                catch (Exception ex)
                {
                    // The worker's place is given back and the column is asked for again once the
                    // player moves, rather than the exception ending a task nobody waits on.
                    Console.Error.WriteLine($"Column {x}, {z} could not be generated: {ex}");
                }
                _done.Enqueue((x, z, column));
            });
        }
        _wanted.RemoveAll(c => world.HasColumn(c.X, c.Z) || _pending.Contains(c));
    }

    // A column with its own light worked out, which leaves the main thread only the light across its edges.
    private static ChunkColumn Generate(IWorldGenerator generator, int x, int z)
    {
        var column = generator.Generate(x, z);
        Lighting.Compute(column);
        return column;
    }

    // The columns within the load distance, nearest first.
    private void Plan(int cx, int cz, int load)
    {
        _wanted.Clear();
        var reach = load * load + load;
        for (int z = cz - load; z <= cz + load; z++)
            for (int x = cx - load; x <= cx + load; x++)
            {
                int dx = x - cx, dz = z - cz;
                if (dx * dx + dz * dz <= reach && !world.HasColumn(x, z)) _wanted.Add((x, z));
            }
        _wanted.Sort((a, b) => ((a.X - cx) * (a.X - cx) + (a.Z - cz) * (a.Z - cz)).CompareTo((b.X - cx) * (b.X - cx) + (b.Z - cz) * (b.Z - cz)));
    }

    private void Unload(int cx, int cz, int keep)
    {
        var reach = keep * keep + keep;
        _leaving.Clear();
        foreach (var column in world.Columns)
        {
            int dx = column.X - cx, dz = column.Z - cz;
            if (dx * dx + dz * dz > reach) _leaving.Add((column.X, column.Z));
        }
        foreach (var (x, z) in _leaving)
        {
            world.Remove(x, z);
            renderer.ForgetColumn(x, z);
        }
    }
}
