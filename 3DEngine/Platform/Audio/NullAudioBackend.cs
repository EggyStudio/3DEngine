using System.Numerics;

namespace Engine;

/// <summary>
/// No-op <see cref="IAudioBackend"/>. Returned from <see cref="AudioServer.Backend"/>
/// when no real backend (SDL3, FMOD, ...) is registered. Exists so gameplay code can
/// use the audio API unconditionally without null-checking - calls just become silent.
/// </summary>
/// <remarks>
/// Mirrors the engine's pattern of failing soft when an optional native dependency is
/// missing: the renderer logs and skips a frame; here we log once and silently absorb
/// every call.
/// </remarks>
public sealed class NullAudioBackend : IAudioBackend
{
    /// <inheritdoc />
    public bool IsInitialized => true;

    /// <inheritdoc />
    public string BackendId => "null";

    /// <inheritdoc />
    public void Initialize() { }

    /// <inheritdoc />
    public int CreateVoice(Sound sound, in AudioVoiceParams parameters) => 0;

    /// <inheritdoc />
    public void StopVoice(int voiceId) { }

    /// <inheritdoc />
    public bool IsVoicePlaying(int voiceId) => false;

    /// <inheritdoc />
    public void SetVoicePosition(int voiceId, Vector3 position) { }

    /// <inheritdoc />
    public void SetVoiceVolume(int voiceId, float volume) { }

    /// <inheritdoc />
    public void SetVoiceLooping(int voiceId, bool looping) { }

    /// <inheritdoc />
    public void SetVoicePaused(int voiceId, bool paused) { }

    /// <inheritdoc />
    public void SetListenerPosition(Vector3 position) { }

    /// <inheritdoc />
    public void Update() { }

    /// <inheritdoc />
    public void Dispose() { }
}