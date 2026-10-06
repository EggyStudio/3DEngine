using System.Numerics;

namespace Engine;

/// <summary>
/// Per-voice playback parameters passed at <see cref="IAudioBackend.CreateVoice"/> time.
/// </summary>
/// <remarks>
/// <see cref="Position"/> being non-null marks the voice as <i>spatial</i>: the backend
/// is expected to apply 3D distance attenuation, panning, and (when an
/// <see cref="ISpatialAudioProcessor"/> is wired) HRTF / occlusion. Null position
/// requests a 2D mix at the supplied <see cref="Volume"/>.
/// </remarks>
public readonly record struct AudioVoiceParams
{
    /// <summary>Initial 3D position. <c>null</c> = non-spatial 2D voice.</summary>
    public Vector3? Position { get; init; }

    /// <summary>Linear gain. <c>1.0</c> = unity. Range conventionally <c>[0, 4]</c>.</summary>
    public float Volume { get; init; }

    /// <summary>When <c>true</c>, the voice loops at end-of-buffer until explicitly stopped.</summary>
    public bool Looping { get; init; }

    /// <summary>When <c>true</c>, the voice is created paused; the caller must un-pause to start playback.</summary>
    public bool Paused { get; init; }

    /// <summary>
    /// The source's orientation in world space, which the spatial processor uses with
    /// <see cref="DipoleWeight"/> and <see cref="DipolePower"/> to weaken the sound away from
    /// the way the source faces. <c>null</c> for a source heard alike in every direction.
    /// </summary>
    public Quaternion? Orientation { get; init; }

    /// <summary>
    /// Directivity dipole weight in <c>[0, 1]</c>. <c>0</c> = omni; <c>1</c> = pure
    /// dipole (silent rear). Ignored when <see cref="Orientation"/> is <c>null</c>.
    /// </summary>
    public float DipoleWeight { get; init; }

    /// <summary>Directivity dipole exponent. Default <c>1</c>; higher = sharper front lobe.</summary>
    public float DipolePower { get; init; }

    /// <summary>
    /// Sample-rate ratio: <c>1.0</c> = native pitch, <c>2.0</c> = one octave up
    /// (twice as fast), <c>0.5</c> = one octave down. Backends typically clamp to a
    /// safe range (SDL3 enforces <c>[0.01, 100]</c>). Used both for pitch effects and
    /// for Doppler when the gameplay layer drives it.
    /// </summary>
    public float PlaybackRate { get; init; }

    /// <summary>
    /// Whether the voice's balance can be set, through <see cref="IAudioBackend.SetVoicePan"/>,
    /// as a positional voice's always can. A backend may spend a second stream on it.
    /// </summary>
    public bool Pannable { get; init; }

    /// <summary>The balance a pannable voice starts at, from -1 (left) to 1 (right).</summary>
    public float Pan { get; init; }

    /// <summary>Reusable defaults: non-spatial, unity volume, no loop, playing, omni.</summary>
    public static AudioVoiceParams Default => new() { Volume = 1f, DipolePower = 1f, PlaybackRate = 1f };
}
