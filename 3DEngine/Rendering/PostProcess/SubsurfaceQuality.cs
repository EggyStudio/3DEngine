namespace Engine;

/// <summary>How finely the light under a surface is spread over the window's frame, from a slow GPU's to the full.</summary>
/// <seealso cref="Engine3D.SetSubsurfaceQuality"/>
public enum SubsurfaceQuality
{
    /// <summary>The marked surfaces drawn and spread at half the window's size, nine taps each way.</summary>
    Low,

    /// <summary>At the window's size, nine taps each way.</summary>
    Medium,

    /// <summary>At the window's size, seventeen taps each way, as by default.</summary>
    High,
}
