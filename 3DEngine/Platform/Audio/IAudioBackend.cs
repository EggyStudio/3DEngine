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
    /// Optional source orientation as a quaternion (world space). When non-null the
    /// spatial processor uses it - together with <see cref="DipoleWeight"/> /
    /// <see cref="DipolePower"/> - to compute directivity attenuation. <c>null</c>
    /// = the source radiates omni-directionally.
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
    /// for Doppler when the gameplay layer wants to drive it.
    /// </summary>
    public float PlaybackRate { get; init; }

    /// <summary>Reusable defaults: non-spatial, unity volume, no loop, playing, omni.</summary>
    public static AudioVoiceParams Default => new() { Volume = 1f, DipolePower = 1f, PlaybackRate = 1f };
}

/// <summary>
/// Backend-agnostic playback abstraction. Concrete implementations live in backend
/// modules: <c>SdlAudioBackend</c> in <c>Engine.Sound.Sdl</c>, future
/// <c>FMODAudioBackend</c>, etc. Mirrors the registration shape of <see cref="ITextureDecoder"/>:
/// <see cref="AudioServer"/> holds a single active backend that can be swapped at startup.
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
    /// Pumps backend bookkeeping (3D recompute, voice recycling). Called once per frame
    /// by <see cref="AudioUpdateSystem"/>; backends may also do this internally on their
    /// own thread.
    /// </summary>
    void Update();
}

/// <summary>
/// Optional spatial post-processor that augments raw <see cref="IAudioBackend"/> output
/// with engine-quality 3D parameters (distance attenuation, occlusion, HRTF direction).
/// Implemented by <c>SteamAudioProcessor</c> in <c>Engine.Sound.SteamAudio</c>; absence
/// is fine - the backend's built-in 3D maths is used instead.
/// </summary>
public interface ISpatialAudioProcessor : IDisposable
{
    /// <summary>Stable processor identifier (e.g. <c>"steamaudio"</c>).</summary>
    string ProcessorId { get; }

    /// <summary>Initialises the processor (allocates DSP context, loads HRTF).</summary>
    void Initialize();

    /// <summary>Computes per-voice spatial parameters for the current frame.</summary>
    SpatialResult Compute(int voiceId, Vector3 sourcePosition, Vector3 listenerPosition, in SpatialContext context);
}

/// <summary>Static per-frame inputs the spatial processor can use (room, occlusion geometry, etc.).</summary>
/// <remarks>
/// <para>
/// <b>Convention:</b> orientation vectors are in world space and right-handed
/// (<see cref="ListenerForward"/> = camera "look" direction; <see cref="ListenerUp"/>
/// = camera "up"). The engine's standard view matrix is
/// <c>Matrix4x4.CreateLookAt(eye, target, +Y)</c>, so the rotated <c>-Z</c> axis is
/// "forward" for both listener and source.
/// </para>
/// <para>
/// All vectors default to the identity orientation (forward = <c>-Z</c>, up = <c>+Y</c>).
/// <see cref="DipoleWeight"/> defaults to <c>0</c> so directivity is neutral when not
/// configured by the caller.
/// </para>
/// </remarks>
public readonly record struct SpatialContext
{
    /// <summary>Source forward direction (unit length, world space).</summary>
    public Vector3 SourceForward { get; init; }

    /// <summary>Source up direction (unit length, world space).</summary>
    public Vector3 SourceUp { get; init; }

    /// <summary>Listener forward direction (unit length, world space).</summary>
    public Vector3 ListenerForward { get; init; }

    /// <summary>Listener up direction (unit length, world space).</summary>
    public Vector3 ListenerUp { get; init; }

    /// <summary>
    /// Directivity dipole weight in <c>[0, 1]</c>. <c>0</c> = omni-directional;
    /// <c>1</c> = pure dipole (cardioid front, silent rear). Mid-values blend.
    /// </summary>
    public float DipoleWeight { get; init; }

    /// <summary>
    /// Directivity dipole exponent (≥ 0). Higher values sharpen the front lobe.
    /// </summary>
    public float DipolePower { get; init; }

    /// <summary>Empty context: identity orientations, omni directivity.</summary>
    public static SpatialContext Empty => new()
    {
        SourceForward = -Vector3.UnitZ, SourceUp = Vector3.UnitY,
        ListenerForward = -Vector3.UnitZ, ListenerUp = Vector3.UnitY,
        DipoleWeight = 0f, DipolePower = 1f,
    };
}

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

    /// <summary>Stereo pan in <c>[-1, +1]</c>: -1 = full left, 0 = centre, +1 = full right.</summary>
    public float Pan { get; init; }

    /// <summary>Pass-through (no spatial change): all gains <c>1</c>, pan centred.</summary>
    public static SpatialResult Pass => new()
    {
        VolumeAttenuation = 1f,
        DistanceAttenuation = 1f,
        DirectivityAttenuation = 1f,
        Pan = 0f,
    };
}