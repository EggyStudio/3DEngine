namespace Engine;

/// <summary>
/// How the two sides of a blend, each multiplied by its factor, are put together, for
/// <see cref="Engine3D.rlSetBlendFactors"/> and <see cref="Engine3D.rlSetBlendFactorsSeparate"/>,
/// under rlgl's values, <c>RL_FUNC_ADD</c> to <c>RL_FUNC_REVERSE_SUBTRACT</c>.
/// </summary>
public enum RlBlendEquation
{
    /// <summary>The two added.</summary>
    FuncAdd = 0x8006,
    /// <summary>The smaller of the two, their factors left out.</summary>
    Min = 0x8007,
    /// <summary>The larger of the two, their factors left out.</summary>
    Max = 0x8008,
    /// <summary>What is there taken from what is drawn.</summary>
    FuncSubtract = 0x800A,
    /// <summary>What is drawn taken from what is there.</summary>
    FuncReverseSubtract = 0x800B,
}
