namespace Engine.Game;

/// <summary>
/// Land of the biomes a climate chooses: rolling plains and forests of oak and birch, taiga and
/// snowy plains of spruce where it is cold, flat desert of sand and cactus where it is hot and
/// dry, mountains of stone with snowy tops, sandy hollows, and caves under the ground.
/// </summary>
public sealed class Overworld : IWorldGenerator
{
    private const int SnowLine = 92;
    // Ground this high is mountains, bare stone, whatever the climate.
    private const int MountainsFrom = 82;
    private const int SandBelow = 50;
    // Features keep this far from a column's edge, the reach of the widest, so each stays in its column.
    private const int Edge = 2;

    private readonly Noise _hills, _mountains, _sand, _gravel, _tunnelA, _tunnelB, _caverns;
    private readonly Climate _climate;

    public Overworld(int seed)
    {
        Seed = seed;
        _hills = new Noise(seed);
        _mountains = new Noise(seed + 1);
        _sand = new Noise(seed + 2);
        _gravel = new Noise(seed + 3);
        _tunnelA = new Noise(seed + 4);
        _tunnelB = new Noise(seed + 5);
        _caverns = new Noise(seed + 6);
        _climate = new Climate(seed);
    }

    public string Name => "overworld";

    public int Seed { get; }

    /// <summary>
    /// The height of the ground's top block at a world column, its climate and its biome. A dry,
    /// hot climate lowers the hills and keeps the mountains away, so a desert lies flat and the land
    /// flattens smoothly as it nears one rather than at the biome's edge.
    /// </summary>
    public (int Height, float Temperature, float Humidity, Biome Biome) At(int x, int z)
    {
        float temperature = _climate.Temperature(x, z), humidity = _climate.Humidity(x, z);
        var dry = Climate.Dryness(temperature, humidity);
        var hills = _hills.Fractal(x / 160f, z / 160f, 5) * 18 * (1 - 0.6f * dry);
        var mountains = Math.Max(0, _mountains.Fractal(x / 420f, z / 420f, 3)) * (1 - dry);
        var height = Math.Clamp((int)(56 + hills + mountains * mountains * 110), 4, ChunkColumn.Height - 12);
        return (height, temperature, humidity, Climate.Choose(temperature, humidity, height, MountainsFrom));
    }

    public void Paint(ChunkColumn column)
    {
        for (int z = 0; z < Section.Size; z++)
            for (int x = 0; x < Section.Size; x++)
            {
                var (_, temperature, humidity, biome) = At(column.X * Section.Size + x, column.Z * Section.Size + z);
                Paint(column, x, z, temperature, humidity, biome);
            }
    }

    private static void Paint(ChunkColumn column, int x, int z, float temperature, float humidity, Biome biome)
    {
        var at = z * Section.Size + x;
        column.Biome[at] = biome.Id;
        column.GrassTint[at] = Climate.Grass(temperature, humidity);
        column.FoliageTint[at] = Climate.Foliage(temperature, humidity);
    }

    public ChunkColumn Generate(int columnX, int columnZ)
    {
        var column = new ChunkColumn(columnX, columnZ);
        var heights = new int[Section.Size * Section.Size];
        for (int z = 0; z < Section.Size; z++)
            for (int x = 0; x < Section.Size; x++)
            {
                int wx = columnX * Section.Size + x, wz = columnZ * Section.Size + z;
                var (height, temperature, humidity, biome) = At(wx, wz);
                heights[z * Section.Size + x] = height;
                Paint(column, x, z, temperature, humidity, biome);

                // Low ground in a temperate biome lies in patches of sand, and mountains in patches of gravel.
                var temperate = biome == Biomes.Plains || biome == Biomes.Forest || biome == Biomes.BirchForest;
                var sandy = temperate && height < SandBelow && _sand.At(wx / 48f, wz / 48f) > 0.05f;
                var top = height >= SnowLine ? BlockId.Snow
                    : sandy ? BlockId.Sand
                    : biome == Biomes.Mountains && _gravel.At(wx / 24f, wz / 24f) > 0.3f ? BlockId.Gravel
                    : biome.Top;
                var under = sandy ? BlockId.Sand : biome.Under;

                column.Set(x, 0, z, BlockId.Bedrock);
                for (int y = 1; y <= height; y++)
                {
                    var block = y == height ? top
                        : y > height - 4 ? under
                        : biome.Deep != BlockId.Air && y > height - 8 ? biome.Deep
                        : BlockId.Stone;
                    // The caves stay three blocks under the ground, so they are dark until dug into,
                    // where an emissive block is the only light.
                    if (y < height - 3 && IsCave(wx, y, wz)) continue;
                    column.Set(x, y, z, block);
                }
            }

        for (int z = Edge; z < Section.Size - Edge; z++)
            for (int x = Edge; x < Section.Size - Edge; x++)
            {
                int wx = columnX * Section.Size + x, wz = columnZ * Section.Size + z;
                var height = heights[z * Section.Size + x];
                var ground = column.Get(x, height, z);
                var hash = PlaceHash.Of(wx, wz, Seed);
                var roll = hash % 1000;
                foreach (var (feature, perThousand) in Biomes.All[column.Biome[z * Section.Size + x]].Trees)
                {
                    if (roll >= perThousand)
                    {
                        roll -= (uint)perThousand;
                        continue;
                    }
                    // Trees stand on grass or snow, and a cactus looks for its own sand.
                    if (feature is CactusFeature || ground is BlockId.Grass or BlockId.Snow) feature.Place(column, x, height, z, hash / 1000);
                    break;
                }
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
