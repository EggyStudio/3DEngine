using System.Numerics;

namespace Engine.Game;

/// <summary>
/// The animals a column is generated with: in a tenth of the columns a herd of two to four of one
/// kind its biome holds, standing on its ground, as Minecraft places a group of animals as it
/// generates a chunk.
/// </summary>
/// <remarks>
/// Which columns hold one, its kind, its size and where each stands depend on the seed and the
/// column alone, and each animal stands on the column's top block, which must be the biome's ground
/// or grass with two cells of air over it, so none stands on a tree or in water.
/// </remarks>
public static class Herds
{
    private const int Salt = 0x4e4d;
    private const int PerHundred = 10;

    public static void Place(ChunkColumn column, int seed)
    {
        var hash = PlaceHash.Of(column.X, column.Z, seed ^ Salt);
        if (hash % 100 >= PerHundred) return;
        var biome = Biomes.All[column.Biome[8 * Section.Size + 8]];
        if (biome.Animals.Length == 0) return;
        var kind = biome.Animals[hash / 100 % (uint)biome.Animals.Length];
        var random = new Random((int)(hash >> 4));
        var count = 2 + random.Next(3);
        int mx = 4 + random.Next(8), mz = 4 + random.Next(8);
        for (int tries = 0; tries < count * 4 && count > 0; tries++)
        {
            int x = Math.Clamp(mx + random.Next(-3, 4), 0, Section.Mask), z = Math.Clamp(mz + random.Next(-3, 4), 0, Section.Mask);
            var y = column.Top(x, z);
            var ground = y < 0 ? BlockId.Air : column.Get(x, y, z);
            if (ground != biome.Top && ground != BlockId.Grass || y + 3 >= ChunkColumn.Height
                || column.Get(x, y + 1, z) != BlockId.Air || column.Get(x, y + 2, z) != BlockId.Air) continue;
            var at = new Vector3(column.X * Section.Size + x + 0.5f, y + 1, column.Z * Section.Size + z + 0.5f);
            column.Spawns.Add(new MobRecord(kind, kind.Variant(random.Next(1000)), at, (float)(random.NextDouble() * MathF.Tau), kind.Health, null));
            count--;
        }
    }
}
