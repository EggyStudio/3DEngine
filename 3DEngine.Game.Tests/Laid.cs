namespace Engine.Game.Tests;

/// <summary>A generator of columns a test lays out, every column made by the same rule, and a world of them around the origin.</summary>
public sealed class Laid(Action<ChunkColumn> lay) : IWorldGenerator
{
    public string Name => "laid";

    public int Seed => 0;

    public ChunkColumn Generate(int columnX, int columnZ)
    {
        var column = new ChunkColumn(columnX, columnZ);
        lay(column);
        return column;
    }

    public void Paint(ChunkColumn column)
    {
    }

    /// <summary>A world of the columns within <paramref name="radius"/> of the origin's, each lit and taken in as the streamer takes them.</summary>
    public static VoxelWorld World(Action<ChunkColumn> lay, int radius = 2)
    {
        var world = new VoxelWorld(new Laid(lay));
        for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                var column = world.Generator.Generate(x, z);
                Lighting.Compute(column);
                world.Add(column);
            }
        return world;
    }

    /// <summary>Fills a column with a block from one height to another, both included.</summary>
    public static void Fill(ChunkColumn column, int from, int to, BlockId block)
    {
        for (int y = from; y <= to; y++)
            for (int z = 0; z < Section.Size; z++)
                for (int x = 0; x < Section.Size; x++)
                    column.Set(x, y, z, block);
    }
}
