namespace Engine;

/// <summary>How what is drawn is laid over what is there, raylib's blend modes.</summary>
/// <remarks>
/// The mode sets how the colors combine. Alpha combines as it does in <see cref="Alpha"/> in every
/// mode, the source's laid over the destination's, so a render target drawn in any mode keeps the
/// coverage of what was drawn into it.
/// </remarks>
public enum BlendMode
{
    /// <summary>The color laid over what is there by its alpha.</summary>
    Alpha,
    /// <summary>The color, scaled by its alpha, added to what is there, as light and fire are drawn.</summary>
    Additive,
    /// <summary>What is there multiplied by the color, and darkened by its alpha.</summary>
    Multiplied,
    /// <summary>The color added to what is there, its alpha ignored.</summary>
    AddColors,
    /// <summary>What is there taken away from the color, its alpha ignored.</summary>
    SubtractColors,
    /// <summary>A color already multiplied by its alpha laid over what is there.</summary>
    AlphaPremultiply,
}
