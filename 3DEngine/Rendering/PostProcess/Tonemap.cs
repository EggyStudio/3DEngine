using System.Numerics;

namespace Engine;

/// <summary>
/// A curve that brings the HDR frame's light past 1 under it, for <see cref="FrameEffects.Tonemap"/>:
/// the engine's own, Narkowicz's fit of ACES and a cut at 1, and the eight Bevy offers, named as
/// BevyCSharp names them and drawn as Bevy draws them.
/// </summary>
/// <remarks>
/// <see cref="AgX"/>, <see cref="TonyMcMapface"/> and <see cref="BlenderFilmic"/> look the light up
/// in Bevy's own tables, 3D textures read the first time one is chosen, and the others are worked
/// out as Bevy's <c>tonemapping_shared.wgsl</c> works them out (THIRD-PARTY-NOTICES.md).
/// </remarks>
public enum Tonemap
{
    /// <summary>
    /// The engine's own and the default, which leaves a color alone up to 0.9 and bends the
    /// brightest channel toward 1 past it, keeping the hue. A frame with no light, sky, reflection
    /// probe, light that bounces, particle, bloom or exposure has nothing past white to bend, and
    /// is cut at 1 instead, so white stays white as raylib draws it.
    /// </summary>
    Engine,

    /// <summary>Reinhard's, each channel over one plus itself, as Bevy draws it, its hues shifting as they brighten.</summary>
    Reinhard,

    /// <summary>The ACES filmic curve as Narkowicz fitted it, with deeper shadows and highlights that saturate toward white.</summary>
    Aces,

    /// <summary>No curve, each channel cut at 1.</summary>
    Clamp,

    /// <summary>Bevy's none, no curve, each channel cut at 1 as the window's eight bits cut it.</summary>
    None,

    /// <summary>Reinhard's by luminance, the color over one plus its luminance, which keeps a bright color's hue better.</summary>
    ReinhardLuminance,

    /// <summary>The ACES filmic curve as Stephen Hill fitted it, film-like with deliberate hue shifts and high contrast.</summary>
    AcesFitted,

    /// <summary>Troy Sobotka's AgX, neutral and slightly desaturated, with almost no hue shift, from Bevy's table.</summary>
    AgX,

    /// <summary>Tomasz Stachowiak's somewhat boring display transform, a plain one to judge the others against.</summary>
    SomewhatBoring,

    /// <summary>Tomasz Stachowiak's Tony McMapface, Bevy's default, neutral and keeping saturation in the highlights, from Bevy's table.</summary>
    TonyMcMapface,

    /// <summary>Blender's filmic view transform, for matching a render done there, from Bevy's table.</summary>
    BlenderFilmic,
}
