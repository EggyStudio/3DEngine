namespace Engine;

/// <summary>How a texture is sampled between its pixels.</summary>
/// <remarks>
/// Every filter but <see cref="Point"/> takes up to 16 samples along the direction a slanted texture
/// is squashed in, or as many as the device allows where it allows fewer, so a floor stretching away
/// stays sharp at a glancing angle, at little cost on any GPU.
/// </remarks>
public enum TextureFilter
{
    /// <summary>The nearest pixel, so pixel art stays sharp.</summary>
    Point,

    /// <summary>A blend of the nearest pixels, in the nearest mip level where there are mip levels.</summary>
    Bilinear,

    /// <summary>Bilinear, blended as well between the two nearest mip levels, so a texture drawn smaller and smaller shows no step between them.</summary>
    Trilinear,
}
