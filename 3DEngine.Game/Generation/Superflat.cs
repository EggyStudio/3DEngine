namespace Engine.Game;

/// <summary>A flat world of bedrock, two layers of dirt and grass, a plain floor to build a test on.</summary>
public sealed class Superflat(int seed) : IWorldGenerator
{
    public string Name => "flat";

    public int Seed { get; } = seed;

    /// <summary>Paints every place plains, its grass and leaves at the plains' tint.</summary>
    public void Paint(ChunkColumn column)
    {
        Array.Fill(column.Biome, Biomes.Plains.Id);
        Array.Fill(column.GrassTint, Climate.Grass(Climate.PlainsTemperature, Climate.PlainsHumidity));
        Array.Fill(column.FoliageTint, Climate.Foliage(Climate.PlainsTemperature, Climate.PlainsHumidity));
    }

    public ChunkColumn Generate(int columnX, int columnZ)
    {
        var column = new ChunkColumn(columnX, columnZ);
        Paint(column);
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
