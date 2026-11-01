namespace Engine;

/// <summary>How the exposure that follows the scene weighs the picture it measures (<see cref="E3D.SetAutoExposureMetering"/>).</summary>
public enum ExposureMetering
{
    /// <summary>The whole picture, its middle counting four times as much as its edges, the default.</summary>
    Average,

    /// <summary>
    /// The middle of the picture most, falling away as a bell curve a fifth of the picture wide, so
    /// a bright sky or a dark wall at the edges moves it little, as a camera's center-weighted meter.
    /// </summary>
    CenterWeighted,
}
