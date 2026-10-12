namespace Engine.Game;

/// <summary>
/// Veins of ore in the stone, as Minecraft's are: of coal 18 a column at heights from 5 to 100, each
/// eight blocks along a short wandering line, and of iron 10 a column from 5 to 64, each six, taking
/// the place of stone alone, so a cave's walls and a ravine's show them.
/// </summary>
/// <remarks>Each vein stays within its column and depends on the seed and the column alone, so a column is generated the same every time.</remarks>
public static class Ores
{
    private const int Salt = 0x0c0a;

    public static void Place(ChunkColumn column, int seed)
    {
        var random = new Random((int)PlaceHash.Of(column.X, column.Z, seed ^ Salt));
        Veins(column, random, BlockId.CoalOre, 18, 8, 96);
        Veins(column, random, BlockId.IronOre, 10, 6, 60);
    }

    // So many veins of an ore, each so many steps long, at heights from 5 up through a span.
    private static void Veins(ChunkColumn column, Random random, BlockId ore, int veins, int length, int span)
    {
        for (int vein = 0; vein < veins; vein++)
        {
            int x = random.Next(Section.Size), y = 5 + random.Next(span), z = random.Next(Section.Size);
            for (int step = 0; step < length; step++)
            {
                if ((uint)x < Section.Size && (uint)z < Section.Size && column.Get(x, y, z) == BlockId.Stone) column.Set(x, y, z, ore);
                var way = random.Next(6);
                x += way == 0 ? 1 : way == 1 ? -1 : 0;
                y += way == 2 ? 1 : way == 3 ? -1 : 0;
                z += way == 4 ? 1 : way == 5 ? -1 : 0;
            }
        }
    }
}
