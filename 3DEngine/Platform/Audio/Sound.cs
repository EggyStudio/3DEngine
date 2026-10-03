namespace Engine;

/// <summary>
/// CPU-side decoded audio asset. Holds interleaved 32-bit float PCM samples plus the
/// metadata an <see cref="IAudioBackend"/> needs to upload them as a playable voice.
/// Backend-agnostic: produced by any <see cref="ISoundDecoder"/> (built-in WAV today;
/// future Vorbis / Opus / MP3 backends) and consumed by <see cref="AudioServer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Sample layout:</b> <see cref="Samples"/> is a tightly packed, interleaved
/// <c>float</c> buffer in <c>[-1, 1]</c> (no clamp enforced - HDR-style overshoot is
/// allowed). Frame count is <c>Samples.Length / Channels</c>.
/// </para>
/// <para>
/// <b>Why float-PCM only:</b> SDL3's audio mixer and Steam Audio both process in
/// float internally; decoding once to float keeps the upload path uniform and avoids
/// per-format quantisation drift in the spatial pipeline.
/// </para>
/// </remarks>
/// <seealso cref="ISoundDecoder"/>
/// <seealso cref="SoundAssetLoader"/>
/// <seealso cref="AudioServer"/>
public sealed class Sound
{
    /// <summary>Interleaved 32-bit float PCM samples (range conventionally [-1, 1]).</summary>
    public required float[] Samples { get; init; }

    /// <summary>Sample rate in Hz (e.g. 44100, 48000).</summary>
    public required int SampleRate { get; init; }

    /// <summary>Channel count: 1 = mono, 2 = stereo, 6 = 5.1, etc.</summary>
    public required int Channels { get; init; }

    /// <summary>Source asset path the loader resolved this from. Diagnostic only.</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>Decoder backend identifier (e.g. <c>"wav-builtin"</c>). Mirrors <see cref="Texture.SourceFormat"/>.</summary>
    public string SourceFormat { get; init; } = string.Empty;

    /// <summary>Total duration in seconds, derived from sample count, rate, and channels.</summary>
    public double DurationSeconds => SampleRate <= 0 || Channels <= 0
        ? 0.0
        : (double)Samples.Length / (Channels * SampleRate);
}