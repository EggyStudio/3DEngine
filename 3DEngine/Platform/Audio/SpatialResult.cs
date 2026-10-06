using System.Numerics;

namespace Engine;

/// <summary>Per-voice output of <see cref="ISpatialAudioProcessor.Compute"/>.</summary>
/// <remarks>
/// <para>
/// <see cref="VolumeAttenuation"/> is the multiplicative gain to apply on top of the voice's
/// own <see cref="AudioVoiceParams.Volume"/>. <c>1.0</c> = no change. It already folds in
/// every per-component contribution (<see cref="DistanceAttenuation"/>,
/// <see cref="DirectivityAttenuation"/>) so backends with no breakdown awareness can use a
/// single scalar.
/// </para>
/// <para>
/// <see cref="Pan"/> is a stereo balance hint in <c>[-1, +1]</c> derived from the
/// source's position in the listener's coordinate frame. Backends that support
/// per-voice panning can consume it; backends that don't (e.g. the current SDL3
/// backend) ignore it without harm.
/// </para>
/// </remarks>
public readonly record struct SpatialResult
{
    /// <summary>Combined linear gain (distance × directivity × ...). <c>1.0</c> = no change.</summary>
    public float VolumeAttenuation { get; init; }

    /// <summary>Distance-only contribution (already folded into <see cref="VolumeAttenuation"/>).</summary>
    public float DistanceAttenuation { get; init; }

    /// <summary>Directivity-only contribution (already folded into <see cref="VolumeAttenuation"/>).</summary>
    public float DirectivityAttenuation { get; init; }

    /// <summary>Stereo pan in <c>[-1, +1]</c>: -1 = full left, 0 = center, +1 = full right.</summary>
    public float Pan { get; init; }

    /// <summary>Pass-through (no spatial change): all gains <c>1</c>, pan centered.</summary>
    public static SpatialResult Pass => new()
    {
        VolumeAttenuation = 1f,
        DistanceAttenuation = 1f,
        DirectivityAttenuation = 1f,
        Pan = 0f,
    };
}
