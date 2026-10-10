namespace Engine.Game;

/// <summary>A flat world of bedrock, two layers of dirt and grass, a plain floor to build a test on.</summary>
public sealed class Superflat(int seed) : IWorldGenerator
{
    public string Name => "flat";

    public int Seed { get; } = seed;

    public ChunkColumn Generate(int columnX, int columnZ)
    {
        var column = new ChunkColumn(columnX, columnZ);
        for (int z = 0; z < Section.Size; z++)
            for (int x = 0; x < Section.Size; x++)
            {
                column.Set(x, 0, z, BlockId.Bedrock);
                column.Set(x, 1, z, BlockId.Dirt);
                column.Set(x, 2, z, BlockId.Dirt);
                column.Set(x, 3, z, BlockId.Grass);
            }
        return column;
    }
}
