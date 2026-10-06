namespace Engine;

/// <summary>How an emitter's particles are laid over what is behind them.</summary>
public enum ParticleBlend
{
    /// <summary>Their light added to what is behind, as fire, sparks and magic are, which needs no order.</summary>
    Additive,

    /// <summary>Laid over what is behind by their alpha, as smoke and dust are, in the order they were born.</summary>
    Alpha,
}
