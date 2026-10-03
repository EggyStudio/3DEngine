namespace Engine;

/// <summary>
/// How the alpha channel of a <see cref="MaterialDescription"/> is interpreted at render
/// time. Mirrors the glTF 2.0 / <c>UsdPreviewSurface</c> convention so backend readers
/// (USD, MaterialX) round-trip 1:1.
/// </summary>
/// <seealso cref="MaterialDescription"/>
public enum MaterialAlphaMode : byte
{
    /// <summary>Alpha is ignored; the surface is fully opaque.</summary>
    Opaque,

    /// <summary>
    /// Alpha-to-coverage style cutout: sampled alpha values below
    /// <see cref="MaterialDescription.AlphaCutoff"/> discard the fragment.
    /// </summary>
    Mask,

    /// <summary>Alpha-blended translucency. Requires depth-sorted rendering.</summary>
    Blend,
}