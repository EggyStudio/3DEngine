namespace Engine;

public static partial class E3D
{
    // -- Prototype materials, the grid tinted by a color for each part of a level, so a level reads
    // by color before it has textures of its own.

    // The grid every prototype material of the app shows, made the first time one is asked for.
    private static Texture2D _prototypeGrid;

    /// <summary>The pixels a meter of the prototype grid has, 1024.</summary>
    public const int PrototypePixelsPerMeter = 1024;

    /// <summary>The color of the prototype material for a part of a level, by the name of the material slot its faces are in.</summary>
    /// <remarks>
    /// Unreal's level prototyping palette, a floor a dark gray, a wall a light gray, a ceiling a
    /// lighter one and stairs, ramps and the parts that move the prototyping orange, with a door
    /// green, a window blue and a hazard red, as the prototype textures of the engines of the 2000s
    /// told a reader of a level what each surface was for, and the same in BevyCSharp. The names are
    /// <c>floor</c>, <c>wall</c>, <c>ceiling</c>, <c>stairs</c>, <c>door</c>, <c>window</c> and
    /// <c>hazard</c>, and any other name is white, which shows the grid's own gray.
    /// </remarks>
    public static Color GetPrototypeColor(string name) => name switch
    {
        "floor" => new Color(79, 79, 82, 255),
        "wall" => new Color(181, 181, 178, 255),
        "ceiling" => new Color(209, 209, 212, 255),
        "stairs" => new Color(245, 117, 33, 255),
        "door" => new Color(97, 184, 97, 255),
        "window" => new Color(92, 158, 235, 255),
        "hazard" => new Color(230, 56, 41, 255),
        _ => Color.White,
    };

    /// <summary>The prototype material of a part of a level, the grid tinted by the color <see cref="GetPrototypeColor"/> gives its name.</summary>
    /// <remarks>A model built from an edit mesh draws each material slot with the prototype material of the slot's name.</remarks>
    public static ModelMaterial LoadMaterialPrototype(string name) => LoadMaterialPrototype(GetPrototypeColor(name));

    /// <summary>A material of the grid of a meter's cells tinted by a color, rough and not metal, for blocking out a level.</summary>
    /// <remarks>
    /// <para>
    /// Every prototype material of the app shows one texture, <see cref="GenImageGrid(int)"/> at
    /// <see cref="PrototypePixelsPerMeter"/> with mip levels, repeating, filtered across sixteen
    /// samples along a slanted surface, so its lines stay sharp along a corridor. Rough and not
    /// metal, so the light shows the shape and a highlight never hides the lines.
    /// </para>
    /// <para>
    /// The texture is the engine's, freed by <see cref="CloseWindow"/>, so a prototype material is
    /// not unloaded with <see cref="UnloadMaterial"/>, which would free the grid the others show.
    /// </para>
    /// </remarks>
    public static ModelMaterial LoadMaterialPrototype(Color color) => new(color, PrototypeGrid()) { Roughness = 0.85f };

    // The prototype grid, made the first time it is asked for in the app and again where it was freed.
    private static Texture2D PrototypeGrid()
    {
        if (_prototypeGrid.IsValid && IsTextureValid(_prototypeGrid)) return _prototypeGrid;
        var texture = LoadTextureFromImage(GenImageGrid(PrototypePixelsPerMeter));
        GenTextureMipmaps(ref texture);
        SetTextureWrap(texture, TextureWrap.Repeat);
        SetTextureFilter(texture, TextureFilter.Anisotropic16x);
        return _prototypeGrid = texture;
    }

    // Frees the prototype grid and the textures edit meshes' slots named before the window closes, so
    // they are not counted among the program's textures left loaded.
    private static void ForgetPrototypes()
    {
        if (_prototypeGrid.IsValid && IsTextureValid(_prototypeGrid)) UnloadTexture(_prototypeGrid);
        _prototypeGrid = default;
        ForgetSlotTextures();
    }
}
