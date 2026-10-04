namespace Engine;

/// <summary>
/// How the alpha channel of a <see cref="MaterialDescription"/> is interpreted at render
/// time, as glTF 2.0 and <c>UsdPreviewSurface</c> define it, so a file's alpha mode is kept
/// as it was authored.
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