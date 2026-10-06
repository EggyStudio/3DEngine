using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic playback, which <see cref="SdlAudioBackend"/> implements.
/// <see cref="AudioServer"/> holds one active backend, which can be swapped at startup.
/// </summary>
/// <remarks>
/// <para>
/// <b>Voice IDs:</b> non-zero handles returned by <see cref="CreateVoice"/> are stable
/// for the lifetime of the voice and reused by every per-voice setter. <c>0</c> always
/// means "invalid / playback failed" so callers can treat the return type as a try-pattern.
/// </para>
/// <para>
/// <b>Thread model:</b> callers invoke methods from the main game thread; backends are
/// free to dispatch the actual audio work onto their own worker threads. <see cref="Update"/>
/// is pumped once per frame from <see cref="Stage.PostUpdate"/> by <see cref="AudioUpdateSystem"/>.
/// </para>
/// </remarks>
public interface IAudioBackend : IDisposable
{
    /// <summary>True once <see cref="Initialize"/> has succeeded.</summary>
    bool IsInitialized { get; }

    /// <summary>Stable backend identifier (e.g. <c>"sdl3"</c>, <c>"null"</c>).</summary>
    string BackendId { get; }

    /// <summary>Initialises the backend (opens device, allocates mixer). Idempotent.</summary>
    void Initialize();

    /// <summary>Creates a playing voice from <paramref name="sound"/>. Returns <c>0</c> on failure.</summary>
    int CreateVoice(Sound sound, in AudioVoiceParams parameters);

    /// <summary>Stops and recycles a voice. Safe to call with an already-stopped or unknown id.</summary>
    void StopVoice(int voiceId);

    /// <summary>True while the voice is mixing (not stopped, not finished, not invalid).</summary>
    bool IsVoicePlaying(int voiceId);

    /// <summary>Sets a spatial voice's position. No-op for 2D voices and unknown ids.</summary>
    void SetVoicePosition(int voiceId, Vector3 position);

    /// <summary>Sets per-voice linear gain. Combines multiplicatively with the asset's authored level.</summary>
    void SetVoiceVolume(int voiceId, float volume);

    /// <summary>Toggles the voice's loop flag mid-playback.</summary>
    void SetVoiceLooping(int voiceId, bool looping);

    /// <summary>Pauses or resumes a playing voice.</summary>
    void SetVoicePaused(int voiceId, bool paused);

    /// <summary>Updates the listener position used for 3D voices' attenuation/panning.</summary>
    void SetListenerPosition(Vector3 position);

    /// <summary>
    /// Optional stereo balance hint in <c>[-1, +1]</c> (-1 = full left, +1 = full
    /// right). Backends that don't implement panning should leave this as a no-op.
    /// Default implementation is a no-op so existing backends don't have to change.
    /// </summary>
    void SetVoicePan(int voiceId, float pan) { }

    /// <summary>
    /// Optional pitch / playback-rate setter. <c>1.0</c> = native, <c>2.0</c> = one
    /// octave up, <c>0.5</c> = one octave down. Backends that can't change rate at
    /// runtime should leave this as a no-op (the default implementation does so).
    /// </summary>
    void SetVoicePlaybackRate(int voiceId, float rate) { }

    /// <summary>
    /// Creates a voice with no samples of its own, which plays what
    /// <see cref="QueueVoiceSamples"/> gives it and stays until stopped, for music streamed from
    /// its file a piece at a time.
    /// </summary>
    /// <returns>The voice id, or 0 when the backend cannot stream, which the default does.</returns>
    int CreateStreamVoice(int channels, int sampleRate, in AudioVoiceParams parameters) => 0;

    /// <summary>Appends interleaved samples to a voice from <see cref="CreateStreamVoice"/>.</summary>
    void QueueVoiceSamples(int voiceId, ReadOnlySpan<float> samples) { }

    /// <summary>How many frames (one sample per channel) a stream voice has queued and not yet played.</summary>
    long QueuedVoiceFrames(int voiceId) => 0;

    /// <summary>
    /// Runs <paramref name="processors"/> in order over every buffer of mixed samples the device
    /// plays, interleaved floats in the device's channels, on the audio thread, as raylib's
    /// <c>AttachAudioMixedProcessor</c> runs its own. None, the default, runs nothing.
    /// </summary>
    void SetMixedProcessors(AudioCallback[] processors) { }

    /// <summary>
    /// Pumps backend bookkeeping (3D recompute, voice recycling). Called once per frame
    /// by <see cref="AudioUpdateSystem"/>; backends may also do this internally on their
    /// own thread.
    /// </summary>
    void Update();
}
