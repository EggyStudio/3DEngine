namespace Engine.Game;

/// <summary>
/// Veins of coal in the stone, as Minecraft's are: 18 a column at heights from 5 to 100, each a few
/// blocks along a short wandering line, taking the place of stone alone, so a cave's walls and a
/// ravine's show them.
/// </summary>
/// <remarks>Each vein stays within its column and depends on the seed and the column alone, so a column is generated the same every time.</remarks>
public static class Ores
{
    private const int Salt = 0x0c0a, Veins = 18, Length = 8;

    public static void Place(ChunkColumn column, int seed)
    {
        var random = new Random((int)PlaceHash.Of(column.X, column.Z, seed ^ Salt));
        for (int vein = 0; vein < Veins; vein++)
        {
            int x = random.Next(Section.Size), y = 5 + random.Next(96), z = random.Next(Section.Size);
            for (int step = 0; step < Length; step++)
            {
                if ((uint)x < Section.Size && (uint)z < Section.Size && column.Get(x, y, z) == BlockId.Stone) column.Set(x, y, z, BlockId.CoalOre);
                var way = random.Next(6);
                x += way == 0 ? 1 : way == 1 ? -1 : 0;
                y += way == 2 ? 1 : way == 3 ? -1 : 0;
                z += way == 4 ? 1 : way == 5 ? -1 : 0;
            }
        }
    }
}
