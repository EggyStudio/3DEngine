namespace Engine.Game;

/// <summary>
/// The look of a block's face: its color, which a section's mesh carries in the face's vertices,
/// and for a block that gives off light, the light, which its cube is drawn with as a material.
/// </summary>
/// <remarks>
/// A material's light is one for its whole draw, so a surface that gives off light is drawn as
/// cubes of its own rather than as faces of a section, whose one material lights nothing.
/// </remarks>
public sealed record Surface(int Id, string Name, Color Color, float Roughness, Color Emissive, float Glow, SurfaceTint Tint)
{
    /// <summary>Whether the surface gives off light of its own.</summary>
    public bool Emits => Glow > 0;

    /// <summary>The surface's color under a tint, which a tinted surface multiplies its gray by and any other ignores.</summary>
    public Color Under(Color grass, Color foliage) => Tint switch
    {
        SurfaceTint.Grass => Multiply(Color, grass),
        SurfaceTint.Foliage => Multiply(Color, foliage),
        _ => Color,
    };

    /// <summary>The surface's color as it looks in the plains, which the hotbar shows.</summary>
    public Color Plain => Under(Climate.Grass(Climate.PlainsTemperature, Climate.PlainsHumidity), Climate.Foliage(Climate.PlainsTemperature, Climate.PlainsHumidity));

    private static Color Multiply(Color a, Color b) => new((byte)(a.R * b.R / 255), (byte)(a.G * b.G / 255), (byte)(a.B * b.B / 255));

    /// <summary>The material a lamp's cube of this surface is drawn with, its glow scaled by <paramref name="glowScale"/>.</summary>
    public ModelMaterial Material(float glowScale) => new(Color)
    {
        AlphaMode = MaterialAlphaMode.Opaque,
        Roughness = Roughness,
        Emissive = Emits ? Emissive : Color.Black,
        EmissiveIntensity = Emits ? Glow * glowScale : 1,
    };
}

/// <summary>Which of the climate's colors a surface is tinted by, as Minecraft tints its gray grass and leaves.</summary>
public enum SurfaceTint
{
    None,
    Grass,
    Foliage,
}

/// <summary>Every surface a block shows, by its id.</summary>
public static class Surfaces
{
    private static readonly List<Surface> _all = [];

    private static int Add(string name, Color color, float roughness = 0.9f, Color emissive = default, float glow = 0, SurfaceTint tint = SurfaceTint.None)
    {
        _all.Add(new Surface(_all.Count, name, color, roughness, emissive, glow, tint));
        return _all.Count - 1;
    }

    // The gray a tinted surface multiplies its climate's color by, about the brightness of Minecraft's
    // gray grass and leaf textures, which its colormap tints.
    public static readonly int Grass = Add("grass", new Color(184, 184, 184), tint: SurfaceTint.Grass);
    public static readonly int Dirt = Add("dirt", new Color(134, 96, 67));
    public static readonly int Stone = Add("stone", new Color(125, 125, 125));
    public static readonly int Cobblestone = Add("cobblestone", new Color(98, 98, 98));
    public static readonly int Bedrock = Add("bedrock", new Color(52, 52, 52));
    public static readonly int Sand = Add("sand", new Color(219, 207, 163));
    public static readonly int Bark = Add("bark", new Color(102, 81, 51));
    public static readonly int LogTop = Add("log top", new Color(168, 136, 86));
    public static readonly int Leaves = Add("leaves", new Color(150, 150, 150), tint: SurfaceTint.Foliage);
    public static readonly int Planks = Add("planks", new Color(162, 130, 78));
    public static readonly int Snow = Add("snow", new Color(240, 247, 250));
    // Concrete in saturated colors, so the color a wall gives its neighbors is plain to see.
    public static readonly int WhiteConcrete = Add("white concrete", new Color(212, 216, 217), 0.8f);
    public static readonly int RedConcrete = Add("red concrete", new Color(170, 35, 35), 0.8f);
    public static readonly int GreenConcrete = Add("green concrete", new Color(75, 140, 40), 0.8f);
    public static readonly int BlueConcrete = Add("blue concrete", new Color(45, 60, 160), 0.8f);
    public static readonly int Glowstone = Add("glowstone", new Color(230, 190, 110), emissive: new Color(255, 200, 120), glow: 4);
    public static readonly int SeaLantern = Add("sea lantern", new Color(200, 225, 220), emissive: new Color(190, 230, 255), glow: 4);
    public static readonly int Shroomlight = Add("shroomlight", new Color(240, 150, 70), emissive: new Color(255, 150, 60), glow: 4);
    public static readonly int Magma = Add("magma", new Color(120, 40, 20), emissive: new Color(255, 90, 30), glow: 1.5f);
    public static readonly int BirchBark = Add("birch bark", new Color(216, 212, 198));
    public static readonly int BirchTop = Add("birch top", new Color(194, 174, 120));
    // Birch and spruce leaves keep a color of their own whatever the climate, as Minecraft's do.
    public static readonly int BirchLeaves = Add("birch leaves", new Color(102, 134, 68));
    public static readonly int SpruceBark = Add("spruce bark", new Color(60, 44, 28));
    public static readonly int SpruceTop = Add("spruce top", new Color(116, 86, 52));
    public static readonly int SpruceLeaves = Add("spruce leaves", new Color(49, 79, 49));
    public static readonly int Sandstone = Add("sandstone", new Color(216, 202, 155));
    public static readonly int CactusSide = Add("cactus side", new Color(78, 128, 42));
    public static readonly int CactusTop = Add("cactus top", new Color(108, 150, 64));
    public static readonly int Gravel = Add("gravel", new Color(132, 126, 124));
    // A see-through surface's alpha is how much of it covers what is behind.
    public static readonly int Glass = Add("glass", new Color(214, 232, 236, 64), roughness: 0.05f);
    public static readonly int Water = Add("water", new Color(44, 92, 196, 168), roughness: 0.05f);
    public static readonly int Ice = Add("ice", new Color(164, 200, 242, 190), roughness: 0.1f);

    /// <summary>Every surface, its index its id.</summary>
    public static IReadOnlyList<Surface> All => _all;
}
