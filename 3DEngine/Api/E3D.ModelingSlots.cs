using System.Numerics;

namespace Engine;

public static partial class E3D
{
    // -- An edit mesh's material slots, each a name with a look, a color, a texture and the maps of
    // the engine's material, which a model built from the mesh is drawn with. Each change is a line
    // of the recipe, so a saved model is drawn as it was left.

    /// <summary>Adds a material slot no face is in yet, drawn with the prototype material of its name until it is given a look.</summary>
    /// <remarks>The recipe's line is <c>slot add trim</c>. A slot is also made by the first call that puts a face in it or gives it a look.</remarks>
    /// <exception cref="ArgumentException">The name is not one word, or the mesh has a slot of that name.</exception>
    public static void EditMeshSlotAdd(EditMesh mesh, string slot) => Edited(mesh, nameof(EditMeshSlotAdd))?.SlotAdd(slot);

    /// <summary>Gives a material slot another name, its faces and its look kept.</summary>
    /// <remarks>
    /// A slot with no look of its own is drawn with the prototype material of its name, so a slot
    /// renamed from <c>default</c> to <c>wall</c> is drawn as a wall's prototype after. The recipe's
    /// line is <c>slot rename default wall</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">The mesh has no slot of the first name, or the new name is not one word or is another slot's.</exception>
    public static void EditMeshSlotRename(EditMesh mesh, string slot, string name) => Edited(mesh, nameof(EditMeshSlotRename))?.SlotRename(slot, name);

    /// <summary>Takes a material slot away, its faces given the first slot, and the slots after it moving down by one.</summary>
    /// <remarks>A model built from the mesh has one mesh fewer after it. The recipe's line is <c>slot remove trim</c>.</remarks>
    /// <exception cref="ArgumentException">The mesh has no slot of that name.</exception>
    /// <exception cref="InvalidOperationException">The slot is the first, which every new face takes.</exception>
    public static void EditMeshSlotRemove(EditMesh mesh, string slot) => Edited(mesh, nameof(EditMeshSlotRemove))?.SlotRemove(slot);

    /// <summary>Gives a material slot a color, which tints its texture, or tints the grid in its place where it has none, an alpha below 255 drawing it blended.</summary>
    /// <remarks>
    /// The slot is made where the mesh has none of the name. The recipe keeps the color as sRGB
    /// numbers from zero to one to three places, as a color picker shows them, the line being
    /// <c>slot color wall 0.8 0.75 0.7</c>.
    /// </remarks>
    public static void EditMeshSlotColor(EditMesh mesh, string slot, Color color) =>
        Edited(mesh, nameof(EditMeshSlotColor))?.SlotColor(slot, new Vector4(Part(color.R), Part(color.G), Part(color.B), Part(color.A)));

    /// <summary>Gives a material slot a texture or a map from its file, which its faces show by their texture coordinates, or takes it away with null.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="MaterialMapIndex.Albedo"/> is the picture the faces show in the grid's place, and
    /// the normal, occlusion, metalness and roughness, and emission maps are the material's own, as
    /// <see cref="SetMaterialTexture"/> gives them. The file is found beside the program or in the
    /// working directory when the model is built, so a missing file is said then, with a warning,
    /// the slot drawn without it, and the file looked for again at the next build.
    /// </para>
    /// <para>
    /// The recipe's lines are <c>slot texture wall bricks.png</c>, <c>slot normal-map wall
    /// bricks_n.png</c>, <c>slot occlusion-map</c>, <c>slot metallic-roughness-map</c> and
    /// <c>slot emissive-map</c>, and <c>none</c> in the path's place takes the map away.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The path has a space in it, or the map is one the engine's materials do not have.</exception>
    public static void EditMeshSlotMap(EditMesh mesh, string slot, MaterialMapIndex map, string? fileName) =>
        Edited(mesh, nameof(EditMeshSlotMap))?.SlotMap(slot, map, fileName);

    /// <summary>Gives a material slot an emissive color, in linear numbers that may pass one for a light that blooms past white.</summary>
    /// <remarks>The recipe's line is <c>slot emissive lamp 4 3.6 3</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">A part is below zero or not finite.</exception>
    public static void EditMeshSlotEmissive(EditMesh mesh, string slot, Vector3 color) => Edited(mesh, nameof(EditMeshSlotEmissive))?.SlotEmissive(slot, color);

    /// <summary>Sets how metal a material slot's faces are, from zero to one.</summary>
    /// <remarks>The recipe's line is <c>slot metallic rail 1</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not from zero to one.</exception>
    public static void EditMeshSlotMetallic(EditMesh mesh, string slot, float metallic) => Edited(mesh, nameof(EditMeshSlotMetallic))?.SlotMetallic(slot, metallic);

    /// <summary>Sets how rough a material slot's faces are, from zero, a mirror, to one.</summary>
    /// <remarks>The prototype materials are 0.85 rough. The recipe's line is <c>slot roughness floor 0.6</c>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not from zero to one.</exception>
    public static void EditMeshSlotRoughness(EditMesh mesh, string slot, float roughness) => Edited(mesh, nameof(EditMeshSlotRoughness))?.SlotRoughness(slot, roughness);

    /// <summary>The material a slot of an edit mesh is drawn with, its look's, or the prototype material of its name where it has none.</summary>
    /// <remarks>
    /// A model built from the mesh is drawn with these, one a slot. The textures are loaded once for
    /// each file and are the engine's, freed by <see cref="CloseWindow"/>, so the material is not
    /// unloaded with <see cref="UnloadMaterial"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">The mesh has no slot of that name.</exception>
    public static ModelMaterial LoadMaterialFromEditMeshSlot(EditMesh mesh, string slot)
    {
        if (Edited(mesh, nameof(LoadMaterialFromEditMeshSlot)) is not { } data) return LoadMaterialDefault();
        var index = data.Slots.ToList().IndexOf(slot);
        if (index < 0) throw new ArgumentException($"There is no slot called {slot}.", nameof(slot));
        return SlotMaterial(data, index);
    }

    // A color's part as the recipe keeps it, to three places, which a byte comes back from whole.
    private static float Part(byte value) => MathF.Round(value / 255f, 3);

    // The material a slot is drawn with. A color tints the grid where no texture is given, so a
    // level's slots read by their colors before their textures come.
    internal static ModelMaterial SlotMaterial(EditMeshData data, int slot)
    {
        var (name, look) = (data.Slots[slot], data.LookAt(slot));
        if (look.IsPrototype) return LoadMaterialPrototype(name);

        var texture = look.Texture is { } picture ? SlotTexture(picture) : default;
        var color = look.Color is { } c
            ? new Color(Byte(c.X), Byte(c.Y), Byte(c.Z), Byte(c.W))
            : texture.IsValid ? Color.White : GetPrototypeColor(name);
        var material = new ModelMaterial(color, texture.IsValid ? texture : PrototypeGrid())
        {
            Roughness = look.Roughness ?? 0.85f,
            Metallic = look.Metallic ?? 0,
            NormalMap = look.NormalMap is { } normal ? SlotTexture(normal) : default,
            OcclusionMap = look.OcclusionMap is { } occlusion ? SlotTexture(occlusion) : default,
            MetallicRoughnessMap = look.MetallicRoughnessMap is { } metal ? SlotTexture(metal) : default,
            EmissiveMap = look.EmissiveMap is { } glow ? SlotTexture(glow) : default,
        };
        if (look.Emissive is { } light) (material.Emissive, material.EmissiveIntensity) = Emissive(light);
        // An emissive map alone shows its colors at their own brightness.
        else if (material.EmissiveMap.IsValid) material.Emissive = Color.White;
        return material;

        static byte Byte(float part) => (byte)MathF.Round(Math.Clamp(part, 0, 1) * 255);
    }

    // A slot's material as a glTF file holds it, its color in linear numbers and its textures by their
    // full paths where they are found, so the file names them from its own folder.
    internal static GltfWriter.Primitive SlotPrimitive(EditMeshData data, int slot, ModelVertex[] vertices, uint[] indices)
    {
        var (name, look) = (data.Slots[slot], data.LookAt(slot));
        var prototype = GetPrototypeColor(name);
        var srgb = look.Color ?? (look.Texture is null ? new Vector4(prototype.R, prototype.G, prototype.B, 255) / 255 : Vector4.One);
        static float Decode(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        static string? Found(string? path) => path is null ? null : ResolveFile(path) is { } found ? Path.GetFullPath(found) : path;
        return new GltfWriter.Primitive(name, new Vector4(Decode(srgb.X), Decode(srgb.Y), Decode(srgb.Z), srgb.W), vertices, indices)
        {
            Metallic = look.Metallic ?? 0,
            Roughness = look.Roughness ?? 0.85f,
            Emissive = look.Emissive ?? Vector3.Zero,
            Texture = Found(look.Texture),
            NormalMap = Found(look.NormalMap),
            OcclusionMap = Found(look.OcclusionMap),
            MetallicRoughnessMap = Found(look.MetallicRoughnessMap),
            EmissiveMap = Found(look.EmissiveMap),
        };
    }

    // A slot's texture, loaded once for each file in the app, repeating with its mip levels and
    // filtered as the grid is, or none where the file is missing, said once and looked for again at
    // the next build, so a texture added while a level is blocked out from the terminal is taken.
    private static Texture2D SlotTexture(string fileName)
    {
        if (ResolveFile(fileName) is not { } found)
        {
            if (_slotTexturesMissing.Add(fileName))
                ApiLogger.Warn($"An edit mesh's slot names '{fileName}', which is not beside the program or in the working directory, so it is drawn without it.");
            return default;
        }

        var key = Path.GetFullPath(found);
        if (_slotTextures.TryGetValue(key, out var known) && known.IsValid && IsTextureValid(known)) return known;
        var texture = LoadTexture(key);
        if (!texture.IsValid) return default;
        GenTextureMipmaps(ref texture);
        SetTextureWrap(texture, TextureWrap.Repeat);
        SetTextureFilter(texture, TextureFilter.Anisotropic16x);
        return _slotTextures[key] = texture;
    }

    // The textures slots named, by file, freed with the prototype grid as the window closes, and
    // the files said to be missing.
    private static readonly Dictionary<string, Texture2D> _slotTextures = [];
    private static readonly HashSet<string> _slotTexturesMissing = [];

    private static void ForgetSlotTextures()
    {
        foreach (var texture in _slotTextures.Values)
            if (texture.IsValid && IsTextureValid(texture)) UnloadTexture(texture);
        _slotTextures.Clear();
        _slotTexturesMissing.Clear();
    }
}
