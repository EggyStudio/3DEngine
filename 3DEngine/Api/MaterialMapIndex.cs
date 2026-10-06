namespace Engine;

/// <summary>
/// A material's maps by raylib's names, for <see cref="Engine3D.SetMaterialTexture"/>. Metalness
/// and roughness share one map here, as glTF packs them, and the last four name maps this
/// engine's materials do not have.
/// </summary>
public enum MaterialMapIndex
{
    /// <summary>The color, <see cref="ModelMaterial.Texture"/>. raylib also calls it diffuse.</summary>
    Albedo,
    /// <summary>The metallic-roughness map, metallic in its blue.</summary>
    Metalness,
    /// <summary>The normal map.</summary>
    Normal,
    /// <summary>The metallic-roughness map, roughness in its green.</summary>
    Roughness,
    /// <summary>The occlusion map.</summary>
    Occlusion,
    /// <summary>The emissive map.</summary>
    Emission,
    /// <summary>A height map, which the engine's materials do not have.</summary>
    Height,
    /// <summary>A cube map, which the engine's materials do not hold, a shader's <c>SamplerCube</c> being given one by <see cref="Engine3D.SetShaderValueTexture"/>.</summary>
    Cubemap,
    /// <summary>An irradiance map, which the environment map gives instead.</summary>
    Irradiance,
    /// <summary>A prefiltered map, which the environment map gives instead.</summary>
    Prefilter,
    /// <summary>A BRDF lookup, which the model pass works out instead.</summary>
    Brdf,
}
