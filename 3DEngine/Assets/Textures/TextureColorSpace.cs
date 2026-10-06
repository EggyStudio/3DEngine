namespace Engine;

/// <summary>How the GPU interprets the texture's stored values when sampling.</summary>
public enum TextureColorSpace
{
    /// <summary>Values are already linear; no conversion on sample.</summary>
    Linear,

    /// <summary>
    /// Values are sRGB-encoded, and the GPU makes them linear as it samples them, as base color
    /// and emissive textures are.
    /// </summary>
    Srgb,
}
