namespace Engine;

/// <summary>How a texture is sampled between its pixels.</summary>
public enum TextureFilter
{
    /// <summary>The nearest pixel, so pixel art stays sharp.</summary>
    Point,

    /// <summary>A blend of the four nearest pixels, in the nearest mip level where there are mip levels.</summary>
    Bilinear,

    /// <summary>Bilinear, blended as well between the two nearest mip levels, so a texture drawn smaller and smaller shows no step between them.</summary>
    Trilinear,

    /// <summary>Trilinear, with up to 4 samples along the direction a slanted texture is squashed in, so it stays sharp at a glancing angle.</summary>
    Anisotropic4x,

    /// <summary>Trilinear, with up to 8 samples along the squashed direction.</summary>
    Anisotropic8x,

    /// <summary>Trilinear, with up to 16 samples along the squashed direction, or as many as the device allows.</summary>
    Anisotropic16x,
}
