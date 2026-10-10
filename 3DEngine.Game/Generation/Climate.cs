namespace Engine.Game;

/// <summary>A kind of land, which the climate chooses: the blocks on its ground and what grows on it.</summary>
/// <param name="Name">The name the settings window and <c>voxel.state</c> show.</param>
/// <param name="Top">The block on the ground.</param>
/// <param name="Under">The three blocks under it.</param>
/// <param name="Deep">The blocks under those down to the stone, none for stone at once.</param>
/// <param name="Trees">What grows on its ground and how often, in thousandths of the columns where it can.</param>
public sealed record Biome(string Name, BlockId Top, BlockId Under, BlockId Deep, (IFeature Feature, int PerThousand)[] Trees)
{
    /// <summary>The biome's place in <see cref="Biomes.All"/>, which a column keeps for each of its places.</summary>
    public byte Id { get; init; }
}

/// <summary>Every biome, by its id.</summary>
public static class Biomes
{
    private static readonly IFeature Oak = new OakTree(), Birch = new BirchTree(), Spruce = new SpruceTree(), Cactus = new CactusFeature();

    public static readonly Biome Plains = new("plains", BlockId.Grass, BlockId.Dirt, BlockId.Air, [(Oak, 3)]) { Id = 0 };
    public static readonly Biome Forest = new("forest", BlockId.Grass, BlockId.Dirt, BlockId.Air, [(Oak, 30), (Birch, 8)]) { Id = 1 };
    public static readonly Biome BirchForest = new("birch forest", BlockId.Grass, BlockId.Dirt, BlockId.Air, [(Birch, 34)]) { Id = 2 };
    public static readonly Biome Taiga = new("taiga", BlockId.Grass, BlockId.Dirt, BlockId.Air, [(Spruce, 28)]) { Id = 3 };
    public static readonly Biome SnowyTaiga = new("snowy taiga", BlockId.Snow, BlockId.Dirt, BlockId.Air, [(Spruce, 22)]) { Id = 4 };
    public static readonly Biome SnowyPlains = new("snowy plains", BlockId.Snow, BlockId.Dirt, BlockId.Air, [(Spruce, 2)]) { Id = 5 };
    public static readonly Biome Desert = new("desert", BlockId.Sand, BlockId.Sand, BlockId.Sandstone, [(Cactus, 6)]) { Id = 6 };
    public static readonly Biome Mountains = new("mountains", BlockId.Stone, BlockId.Stone, BlockId.Air, [(Spruce, 3)]) { Id = 7 };

    public static readonly Biome[] All = [Plains, Forest, BirchForest, Taiga, SnowyTaiga, SnowyPlains, Desert, Mountains];
}

/// <summary>
/// The temperature and humidity of every place, from 0 to 1, which choose its biome and tint its
/// grass and leaves.
/// </summary>
/// <remarks>
/// Both vary over hundreds of blocks, so the tint of grass and leaves, which is a color looked up
/// from the two as Minecraft's colormaps are, changes smoothly across a biome's edge where the
/// biome itself changes at once. Read only once made, so the workers share one.
/// </remarks>
public sealed class Climate(int seed)
{
    /// <summary>The plains' temperature and humidity, which a tinted block's hotbar color is shown at.</summary>
    public const float PlainsTemperature = 0.65f, PlainsHumidity = 0.45f;

    private readonly Noise _temperature = new(seed + 10), _humidity = new(seed + 11);

    public float Temperature(int x, int z) => Math.Clamp(0.5f + 0.9f * _temperature.Fractal(x / 700f, z / 700f, 3), 0, 1);

    public float Humidity(int x, int z) => Math.Clamp(0.5f + 0.9f * _humidity.Fractal(x / 600f, z / 600f, 3), 0, 1);

    /// <summary>How flat the land lies from 0 to 1, high where it is hot and dry, which lowers the hills of a desert as it nears one.</summary>
    public static float Dryness(float temperature, float humidity) => SmoothStep(0.6f, 0.85f, temperature) * SmoothStep(0.45f, 0.2f, humidity);

    /// <summary>The biome of a place, by its climate and the height of its ground.</summary>
    public static Biome Choose(float temperature, float humidity, int height, int mountainsFrom)
    {
        if (height >= mountainsFrom) return Biomes.Mountains;
        if (temperature < 0.3f)
            return humidity > 0.4f ? (temperature < 0.18f ? Biomes.SnowyTaiga : Biomes.Taiga) : Biomes.SnowyPlains;
        if (temperature > 0.7f && humidity < 0.4f) return Biomes.Desert;
        if (humidity > 0.62f) return Biomes.Forest;
        if (humidity > 0.48f && temperature < 0.6f) return Biomes.BirchForest;
        return Biomes.Plains;
    }

    // The colors at the corners of Minecraft's grass and foliage colormaps: cold and dry, cold and
    // wet, hot and dry, hot and wet, between which a place's color is blended.
    private static readonly Color[] GrassCorners = [new(128, 180, 151), new(134, 183, 131), new(191, 183, 85), new(89, 201, 60)];
    private static readonly Color[] FoliageCorners = [new(96, 161, 123), new(104, 164, 100), new(174, 164, 42), new(48, 187, 43)];

    public static Color Grass(float temperature, float humidity) => Blend(GrassCorners, temperature, humidity);

    public static Color Foliage(float temperature, float humidity) => Blend(FoliageCorners, temperature, humidity);

    private static Color Blend(Color[] corners, float t, float h)
    {
        static float Lerp(float a, float b, float s) => a + (b - a) * s;
        byte Channel(Func<Color, byte> of) =>
            (byte)Lerp(Lerp(of(corners[0]), of(corners[1]), h), Lerp(of(corners[2]), of(corners[3]), h), t);
        return new Color(Channel(c => c.R), Channel(c => c.G), Channel(c => c.B));
    }

    private static float SmoothStep(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
