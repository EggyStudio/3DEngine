namespace Engine;

/// <summary>Texture filtering mode for minification and magnification.</summary>
public enum SamplerFilter
{
    /// <summary>Nearest-neighbor (point) filtering - no interpolation.</summary>
    Nearest,
    /// <summary>Bilinear filtering - smooth interpolation between texels.</summary>
    Linear
}

/// <summary>Texture coordinate addressing mode when UVs are outside [0, 1].</summary>
public enum SamplerAddressMode
{
    /// <summary>Clamp to the edge texel color.</summary>
    ClampToEdge,
    /// <summary>Repeat the texture (tile).</summary>
    Repeat,
    /// <summary>Repeat with mirroring on each boundary.</summary>
    MirrorRepeat
}

/// <summary>Descriptor for creating a texture sampler.</summary>
/// <param name="MinFilter">Filtering mode when the texture is minified.</param>
/// <param name="MagFilter">Filtering mode when the texture is magnified.</param>
/// <param name="AddressU">Addressing mode for the U (horizontal) texture coordinate.</param>
/// <param name="AddressV">Addressing mode for the V (vertical) texture coordinate.</param>
/// <param name="AddressW">Addressing mode for the W (depth) texture coordinate.</param>
public readonly record struct SamplerDesc(
    SamplerFilter MinFilter,
    SamplerFilter MagFilter,
    SamplerAddressMode AddressU,
    SamplerAddressMode AddressV,
    SamplerAddressMode AddressW);
