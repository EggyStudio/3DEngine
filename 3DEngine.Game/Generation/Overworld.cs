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

    /// <summary>
    /// The ground at a world column: its height, biome and climate, and its top block and the three
    /// under it. Low ground in a temperate biome lies in patches of sand, and mountains in patches of
    /// gravel. A feature or a structure starting in a column not generated reads its ground here.
    /// </summary>
    public (int Height, Biome Biome, BlockId Top, BlockId Under, float Temperature, float Humidity) Ground(int x, int z)
    {
        var (height, temperature, humidity, biome) = At(x, z);
        var temperate = biome == Biomes.Plains || biome == Biomes.Forest || biome == Biomes.BirchForest;
        var sandy = temperate && height < SandBelow && _sand.At(x / 48f, z / 48f) > 0.05f;
        var top = height >= SnowLine ? BlockId.Snow
            : sandy ? BlockId.Sand
            : biome == Biomes.Mountains && _gravel.At(x / 24f, z / 24f) > 0.3f ? BlockId.Gravel
            : biome.Top;
        return (height, biome, top, sandy ? BlockId.Sand : biome.Under, temperature, humidity);
    }

    public ChunkColumn Generate(int columnX, int columnZ)
    {
        var column = new ChunkColumn(columnX, columnZ);
        for (int z = 0; z < Section.Size; z++)
            for (int x = 0; x < Section.Size; x++)
            {
                int wx = columnX * Section.Size + x, wz = columnZ * Section.Size + z;
                var (height, biome, top, under, temperature, humidity) = Ground(wx, wz);
                Paint(column, x, z, temperature, humidity, biome);
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

        var clip = new ColumnClip(column);
        PlaceFeatures(clip, columnX, columnZ);
        foreach (var structure in Structures.All) structure.BuildIn(clip, this);
        return column;
    }

    // The features that start in this column and in the eight around it within reach of it, in one
    // order every column keeps, so two that overlap overlap the same way wherever they are placed.
    private void PlaceFeatures(ColumnClip clip, int columnX, int columnZ)
    {
        for (int cz = columnZ - 1; cz <= columnZ + 1; cz++)
            for (int cx = columnX - 1; cx <= columnX + 1; cx++)
                for (int z = 0; z < Section.Size; z++)
                    for (int x = 0; x < Section.Size; x++)
                    {
                        int wx = cx * Section.Size + x, wz = cz * Section.Size + z;
                        var hash = PlaceHash.Of(wx, wz, Seed);
                        var roll = hash % 1000;
                        if (roll >= MostFeatures) continue;
                        if (wx < clip.MinX - MostReach || wx > clip.MinX + Section.Mask + MostReach
                            || wz < clip.MinZ - MostReach || wz > clip.MinZ + Section.Mask + MostReach) continue;
                        var (height, biome, top, _, _, _) = Ground(wx, wz);
                        foreach (var (feature, perThousand) in biome.Trees)
                        {
                            if (roll >= perThousand)
                            {
                                roll -= (uint)perThousand;
                                continue;
                            }
                            if (feature.GrowsOn(top)) feature.Place(clip, wx, height, wz, hash / 1000);
                            break;
                        }
                    }
    }

    // The most features any biome places in a thousand places, and the farthest any reaches, so a
    // place that cannot hold one or cannot reach the column is passed over without reading its ground.
    private static readonly int MostFeatures = Biomes.All.Max(b => b.Trees.Sum(t => t.PerThousand));
    private static readonly int MostReach = Biomes.All.SelectMany(b => b.Trees).Max(t => t.Feature.Reach);

    // Tunnels where two noises are both near zero, and wide caverns where a third is high.
    private bool IsCave(int x, int y, int z)
    {
        var a = _tunnelA.At(x / 40f, y / 28f, z / 40f);
        var b = _tunnelB.At(x / 40f, y / 28f, z / 40f);
        if (a * a + b * b < 0.0035f) return true;
        return y < 40 && _caverns.At(x / 64f, y / 32f, z / 64f) > 0.38f;
    }
}
