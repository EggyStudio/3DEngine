using System.Numerics;

namespace Engine;

/// <summary>A curve that brings the HDR frame's light past 1 under it, for <see cref="FrameEffects.Tonemap"/>.</summary>
public enum Tonemap
{
    /// <summary>
    /// The engine's own, which leaves a color alone up to 0.9 and bends the brightest channel toward
    /// 1 past it, keeping the hue, so a scene looks as it does with every effect off.
    /// </summary>
    Engine,

    /// <summary>Reinhard's, light over one plus light by luminance, soft and dim in the highlights.</summary>
    Reinhard,

    /// <summary>The ACES filmic curve as Narkowicz fitted it, with deeper shadows and highlights that saturate toward white.</summary>
    Aces,

    /// <summary>No curve, each channel cut at 1.</summary>
    Clamp,
}
