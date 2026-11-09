namespace Engine;

/// <summary>How much global illumination is worked out, from none to the full set of cascades.</summary>
/// <seealso cref="E3D.SetGlobalIllumination"/>
public enum GlobalIllumination
{
    /// <summary>No global illumination, as raylib draws a scene.</summary>
    Off,

    /// <summary>Two cascades of probes, 64 directions each, and no probes on the view's surfaces, every surface reading the cascades' light alone, for a slow GPU.</summary>
    Low,

    /// <summary>Three cascades, 64, 64 and 256 directions, and a probe on the view's surfaces every 12 pixels.</summary>
    Medium,

    /// <summary>Four cascades, 64 directions in the first and 256 in each after, and a probe on the view's surfaces every 8 pixels.</summary>
    High,
}
