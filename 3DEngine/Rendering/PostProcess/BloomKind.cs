namespace Engine;

/// <summary>Which bloom is drawn (<c>E3D.SetBloomKind</c>).</summary>
public enum BloomKind
{
    /// <summary>The engine's bloom, every level of the chain added alike, the default.</summary>
    Added,

    /// <summary>
    /// Each level of the chain laid over the wider one below it by the scatter
    /// (<c>E3D.SetBloomScatter</c>), as Unity's HDRP spreads its bloom, the threshold measured
    /// after the exposure.
    /// </summary>
    Scattered,
}
