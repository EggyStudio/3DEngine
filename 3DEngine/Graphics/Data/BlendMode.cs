namespace Engine;

/// <summary>How what is drawn is laid over what is there, raylib's blend modes.</summary>
/// <remarks>
/// The mode sets how the colors combine. Alpha combines as it does in <see cref="Alpha"/> in every
/// mode but the two custom ones, the source's laid over the destination's, so a render target drawn
/// in any of them keeps the coverage of what was drawn into it. The custom modes combine alpha as
/// their factors say, as raylib's do.
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
    /// <summary>The factors and equation <see cref="Engine3D.rlSetBlendFactors"/> set, for the color and its alpha alike.</summary>
    Custom,
    /// <summary>The factors and equations <see cref="Engine3D.rlSetBlendFactorsSeparate"/> set, the color's apart from its alpha's.</summary>
    CustomSeparate,
}

/// <summary>The factors and equations a custom blend mode combines the color and the alpha by.</summary>
internal readonly record struct BlendFactors(
    RlBlendFactor SrcColor, RlBlendFactor DstColor, RlBlendEquation ColorEquation,
    RlBlendFactor SrcAlpha, RlBlendFactor DstAlpha, RlBlendEquation AlphaEquation)
{
    /// <summary>rlgl's factors before a program sets any, the color laid over by its alpha.</summary>
    public static readonly BlendFactors Default = new(
        RlBlendFactor.SrcAlpha, RlBlendFactor.OneMinusSrcAlpha, RlBlendEquation.FuncAdd,
        RlBlendFactor.SrcAlpha, RlBlendFactor.OneMinusSrcAlpha, RlBlendEquation.FuncAdd);
}
