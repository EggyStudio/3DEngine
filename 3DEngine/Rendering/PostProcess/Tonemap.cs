using System.Numerics;

namespace Engine;

/// <summary>
/// A curve that brings the HDR frame's light past 1 under it, for
/// <see cref="FrameEffects.Tonemap"/>: the engine's own, Narkowicz's fit of ACES and a cut at 1,
/// eight more by their usual names, which TonemapTests holds to pictures of the same ramp, one set
/// by its numbers and one loaded from a file.
/// </summary>
/// <remarks>
/// <see cref="AgX"/>, <see cref="TonyMcMapface"/> and <see cref="BlenderFilmic"/> look the light up
/// in tables of their own, 3D textures read the first time one is chosen, and the others are
/// worked out by their formulas, the tables and the formulas ported as THIRD-PARTY-NOTICES.md says.
/// </remarks>
public enum Tonemap
{
    /// <summary>
    /// The engine's own and the default, which leaves a color alone up to 0.9 and bends the
    /// brightest channel toward 1 past it, keeping the hue. A frame with no light, sky, reflection
    /// probe, global illumination, particle, bloom or exposure has nothing past white to bend, and
    /// is cut at 1 instead, so white stays white as raylib draws it.
    /// </summary>
    Engine,

    /// <summary>Reinhard's, each channel over one plus itself, its hues shifting as they brighten.</summary>
    Reinhard,

    /// <summary>The ACES filmic curve as Narkowicz fitted it, with deeper shadows and highlights that saturate toward white.</summary>
    Aces,

    /// <summary>No curve, each channel cut at 1.</summary>
    Clamp,

    /// <summary>No curve, each channel cut at 1 as the window's eight bits cut it.</summary>
    None,

    /// <summary>Reinhard's by luminance, the color over one plus its luminance, which keeps a bright color's hue better.</summary>
    ReinhardLuminance,

    /// <summary>The ACES filmic curve as Stephen Hill fitted it, film-like with deliberate hue shifts and high contrast.</summary>
    AcesFitted,

    /// <summary>Troy Sobotka's AgX, neutral and slightly desaturated, with almost no hue shift, from its table.</summary>
    AgX,

    /// <summary>Tomasz Stachowiak's somewhat boring display transform, a plain one to judge the others against.</summary>
    SomewhatBoring,

    /// <summary>Tomasz Stachowiak's Tony McMapface, neutral and keeping saturation in the highlights, from its table.</summary>
    TonyMcMapface,

    /// <summary>Blender's filmic view transform, for matching a render done there, from its table.</summary>
    BlenderFilmic,

    /// <summary>
    /// A curve set by four numbers, which <c>E3D.SetTonemapCurve</c> chooses, an exposure of 1.4, a
    /// white curve of 2, a lower curve of 1.2 and an upper of 1.5 until set.
    /// </summary>
    Curve,

    /// <summary>
    /// A table loaded from a file, which <c>E3D.SetTonemapLookup</c> loads and chooses, the
    /// light encoded by ARRI's LogC to look it up, the encoding such tables are made for, and cut
    /// at 1 until one is loaded.
    /// </summary>
    Lookup,
}
