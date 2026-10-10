namespace Engine.Game;

/// <summary>
/// Rolling hills of grass over dirt and stone, mountains with snowy tops, sandy hollows, caves
/// under the ground and oaks on the grass.
/// </summary>
public sealed class Overworld : IWorldGenerator
{
    private const int SnowLine = 92;
    private const int SandBelow = 50;

    private readonly Noise _hills, _mountains, _sand, _forest, _tunnelA, _tunnelB, _caverns;
    private readonly IFeature _tree = new OakTree();

    public Overworld(int seed)
    {
        Seed = seed;
        _hills = new Noise(seed);
        _mountains = new Noise(seed + 1);
        _sand = new Noise(seed + 2);
        _forest = new Noise(seed + 3);
        _tunnelA = new Noise(seed + 4);
        _tunnelB = new Noise(seed + 5);
        _caverns = new Noise(seed + 6);
    }

    public string Name => "overworld";

    public int Seed { get; }

    /// <summary>The height of the ground's top block at a world column.</summary>
    public int SurfaceHeight(int x, int z)
    {
        var hills = _hills.Fractal(x / 160f, z / 160f, 5);
        var mountains = Math.Max(0, _mountains.Fractal(x / 420f, z / 420f, 3));
        return Math.Clamp((int)(56 + hills * 18 + mountains * mountains * 110), 4, ChunkColumn.Height - 12);
    }

    public ChunkColumn Generate(int columnX, int columnZ)
    {
        var column = new ChunkColumn(columnX, columnZ);
        var heights = new int[Section.Size * Section.Size];
        for (int z = 0; z < Section.Size; z++)
            for (int x = 0; x < Section.Size; x++)
            {
                int wx = columnX * Section.Size + x, wz = columnZ * Section.Size + z;
                var height = heights[z * Section.Size + x] = SurfaceHeight(wx, wz);
                var sandy = height < SandBelow && _sand.At(wx / 48f, wz / 48f) > 0.05f;
                var top = height >= SnowLine ? BlockId.Snow : sandy ? BlockId.Sand : BlockId.Grass;
                var under = sandy ? BlockId.Sand : BlockId.Dirt;

                column.Set(x, 0, z, BlockId.Bedrock);
                for (int y = 1; y <= height; y++)
                {
                    var block = y == height ? top : y > height - 4 ? under : BlockId.Stone;
                    // The caves stay three blocks under the ground, so they are dark until dug into,
                    // where an emissive block is the only light.
                    if (y < height - 3 && IsCave(wx, y, wz)) continue;
                    column.Set(x, y, z, block);
                }
            }

        for (int z = _tree.Reach; z < Section.Size - _tree.Reach; z++)
            for (int x = _tree.Reach; x < Section.Size - _tree.Reach; x++)
            {
                int wx = columnX * Section.Size + x, wz = columnZ * Section.Size + z;
                var height = heights[z * Section.Size + x];
                if (column.Get(x, height, z) != BlockId.Grass) continue;
                var hash = PlaceHash.Of(wx, wz, Seed);
                var chance = _forest.At(wx / 90f, wz / 90f) > 0.15f ? 30u : 3u;
                if (hash % 1000 < chance) _tree.Place(column, x, height, z, hash / 1000);
            }
        return column;
    }

    // Tunnels where two noises are both near zero, and wide caverns where a third is high.
    private bool IsCave(int x, int y, int z)
    {
        var a = _tunnelA.At(x / 40f, y / 28f, z / 40f);
        var b = _tunnelB.At(x / 40f, y / 28f, z / 40f);
        if (a * a + b * b < 0.0035f) return true;
        return y < 40 && _caverns.At(x / 64f, y / 32f, z / 64f) > 0.38f;
    }
}
