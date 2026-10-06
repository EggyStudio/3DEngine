namespace Engine;

/// <summary>
/// What a color or alpha is multiplied by as it is blended, for <see cref="Engine3D.rlSetBlendFactors"/>
/// and <see cref="Engine3D.rlSetBlendFactorsSeparate"/>, under rlgl's values, <c>RL_ZERO</c> to
/// <c>RL_SRC_ALPHA_SATURATE</c>.
/// </summary>
public enum RlBlendFactor
{
    /// <summary>Nothing, so the side it multiplies adds nothing.</summary>
    Zero = 0,
    /// <summary>One, so the side it multiplies is added as it is.</summary>
    One = 1,
    /// <summary>The color drawn.</summary>
    SrcColor = 0x0300,
    /// <summary>One less the color drawn.</summary>
    OneMinusSrcColor = 0x0301,
    /// <summary>The alpha drawn.</summary>
    SrcAlpha = 0x0302,
    /// <summary>One less the alpha drawn.</summary>
    OneMinusSrcAlpha = 0x0303,
    /// <summary>The alpha that is there.</summary>
    DstAlpha = 0x0304,
    /// <summary>One less the alpha that is there.</summary>
    OneMinusDstAlpha = 0x0305,
    /// <summary>The color that is there.</summary>
    DstColor = 0x0306,
    /// <summary>One less the color that is there.</summary>
    OneMinusDstColor = 0x0307,
    /// <summary>The smaller of the alpha drawn and one less the alpha there, for the color, and one for alpha.</summary>
    SrcAlphaSaturate = 0x0308,
}
