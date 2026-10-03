using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace Engine;

/// <summary>
/// <see cref="IAudioBackend"/> backed by SDL3's audio subsystem (<c>SDL_audio.h</c>)
/// via the <c>SDL3-CS</c> managed bindings. Each <see cref="Sound"/> is uploaded as
/// the body of an <c>SDL_AudioStream</c>; SDL handles per-stream resampling /
/// channel-mapping and mixes any number of streams bound to the playback device.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why SDL3:</b> SDL3-CS already ships with the engine for windowing + input,
/// the native binaries (Windows/Linux/macOS) come from <c>SDL3-CS.Native</c>, and the
/// stream API is rich enough to cover the gameplay-facing
/// <see cref="IAudioBackend"/> contract without any per-platform shim. The
/// <see cref="ISpatialAudioProcessor"/> slot (Steam Audio) supplies 3D distance
/// attenuation; SDL itself does no positional audio.
/// </para>
/// <para>
/// <b>Voice model:</b> one <c>SDL_AudioStream</c> per voice, bound to the single
/// logical playback device opened during <see cref="Initialize"/>. SDL converts each
/// stream's source format (mono / stereo, native sample rate) to the device's mix
/// format on the fly. The <see cref="AudioServer"/>'s int voice id maps to the stream
/// pointer through an internal table; <c>0</c> always means "invalid".
/// </para>
/// <para>
/// <b>Looping:</b> SDL streams are play-once queues; looping is implemented by
/// re-queueing the source samples from <see cref="Update"/> whenever the stream's
/// remaining queued bytes drop below one buffer's worth. This is the same pattern
/// SDL's own examples recommend for short looped SFX.
/// </para>
/// <para>
/// <b>Failure mode:</b> if the SDL audio subsystem fails to initialise (no audio
/// device, headless CI), <see cref="Initialize"/> logs and leaves
/// <see cref="IsInitialized"/> <c>false</c>; every subsequent call becomes a no-op,
/// matching <see cref="NullAudioBackend"/> semantics.
/// </para>
/// </remarks>
/// <seealso cref="SdlAudioPlugin"/>
/// <seealso cref="IAudioBackend"/>
public sealed class SdlAudioBackend : IAudioBackend
{
    private static readonly ILogger Logger = Log.Category("Engine.Sound.Sdl");

    /// <summary>One queued buffer worth of float samples we try to keep in flight per looped voice.</summary>
    private const int LoopRefillBytesThreshold = 4 * 4096;

    /// <summary>
    /// Output channel maps used to implement per-voice constant-power panning. SDL3 has
    /// no per-channel gain on a single stream, so each spatial voice owns two streams
    /// bound to the device:
    /// <list type="bullet">
    ///   <item><description><see cref="LeftOnlyMap"/> = <c>{0, -1}</c> - the L stream
    ///   plays its source on the device's left channel and is silent on the right.</description></item>
    ///   <item><description><see cref="RightOnlyMap"/> = <c>{-1, 1}</c> - the R stream
    ///   plays its source on the device's right channel and is silent on the left.</description></item>
    /// </list>
    /// We then split the per-voice gain into <c>(masterGain * leftPanGain)</c> and
    /// <c>(masterGain * rightPanGain)</c> via <see cref="SDL.SetAudioStreamGain"/>.
    /// </summary>
    private static readonly int[] LeftOnlyMap  = { 0, -1 };
    private static readonly int[] RightOnlyMap = { -1, 1 };

    private readonly object _lock = new();
    private readonly Dictionary<int, VoiceRecord> _voices = new();
    private readonly Dictionary<Sound, PinEntry> _samplePins = new();
    private uint _device;
    private SDL.AudioSpec _deviceSpec;
    private bool _initialized;
    private bool _ownsAudioSubsystem;
    private bool _disposed;
    private int _nextVoiceId = 1;

    /// <inheritdoc />
    public bool IsInitialized => _initialized;

    /// <inheritdoc />
    public string BackendId => "sdl3";

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            try
            {
                // SDL_Init is additive: if Engine.App.Sdl already booted Video|Gamepad,
                // adding Audio just spins up the audio subsystem. Track ownership so we
                // only quit-subsystem what we initialised ourselves.
                if (!SDL.WasInit(SDL.InitFlags.Audio).HasFlag(SDL.InitFlags.Audio))
                {
                    if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
                    {
                        Logger.Warn($"SdlAudioBackend: SDL_InitSubSystem(Audio) failed: '{SDL.GetError()}' - backend disabled.");
                        return;
                    }
                    _ownsAudioSubsystem = true;
                }

                // Request a sensible default; SDL will negotiate something close.
                var desired = new SDL.AudioSpec
                {
                    Format = SDL.AudioFormat.AudioF32LE,
                    Channels = 2,
                    Freq = 48000,
                };
                _device = SDL.OpenAudioDevice(SDL.AudioDeviceDefaultPlayback, in desired);
                if (_device == 0)
                {
                    Logger.Warn($"SdlAudioBackend: SDL_OpenAudioDevice failed: '{SDL.GetError()}' - backend disabled.");
                    if (_ownsAudioSubsystem) { SDL.QuitSubSystem(SDL.InitFlags.Audio); _ownsAudioSubsystem = false; }
                    return;
                }
                if (!SDL.GetAudioDeviceFormat(_device, out _deviceSpec, out _))
                {
                    Logger.Debug($"SdlAudioBackend: GetAudioDeviceFormat failed ('{SDL.GetError()}'); falling back to desired spec.");
                    _deviceSpec = desired;
                }

                _initialized = true;
                Logger.Info(
                    $"SdlAudioBackend: device opened (id={_device}, format={_deviceSpec.Format}, " +
                    $"{_deviceSpec.Channels}ch @ {_deviceSpec.Freq}Hz).");
            }
            catch (DllNotFoundException ex)
            {
                Logger.Warn($"SdlAudioBackend: native 'SDL3' library not found ({ex.Message}). Backend disabled.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"SdlAudioBackend: initialisation failed ({ex.GetType().Name}: {ex.Message}). Backend disabled.");
            }
        }
    }

    /// <inheritdoc />
    public int CreateVoice(Sound sound, in AudioVoiceParams parameters)
    {
        if (!_initialized || sound is null) return 0;
        if (sound.Samples.Length == 0 || sound.Channels <= 0 || sound.SampleRate <= 0) return 0;

        var srcSpec = new SDL.AudioSpec
        {
            Format = SDL.AudioFormat.AudioF32LE,
            Channels = sound.Channels,
            Freq = sound.SampleRate,
        };
        // Spatial voices need a forced-stereo destination so the L/R channel-map split
        // works regardless of the actual hardware layout. Non-spatial voices follow the
        // device spec directly (no panning required).
        bool spatial = parameters.Position is not null;
        var dstSpec = spatial
            ? new SDL.AudioSpec { Format = SDL.AudioFormat.AudioF32LE, Channels = 2, Freq = _deviceSpec.Freq }
            : _deviceSpec;

        lock (_lock)
        {
            // Pin the float[] so SDL can read directly from managed memory. Pin entries
            // are reference-counted: every voice playing this Sound bumps RefCount;
            // StopVoice / Update reap / Dispose decrement it; the pin is freed at zero
            // so unloaded Sounds don't leave their sample buffers pinned forever.
            if (!_samplePins.TryGetValue(sound, out var pin))
            {
                pin = new PinEntry(GCHandle.Alloc(sound.Samples, GCHandleType.Pinned));
                _samplePins[sound] = pin;
            }
            int byteCount = sound.Samples.Length * sizeof(float);

            IntPtr streamL = CreateAndQueueStream(in srcSpec, in dstSpec, pin.Handle, byteCount, sound.SourcePath);
            if (streamL == IntPtr.Zero)
            {
                ReleasePin(sound); // never actually consumed - drop the (potential) fresh pin.
                return 0;
            }

            IntPtr streamR = IntPtr.Zero;
            if (spatial)
            {
                streamR = CreateAndQueueStream(in srcSpec, in dstSpec, pin.Handle, byteCount, sound.SourcePath);
                if (streamR == IntPtr.Zero)
                {
                    SDL.DestroyAudioStream(streamL);
                    ReleasePin(sound);
                    return 0;
                }
                // Route each stream to a single device channel; pan is then a pure gain split.
                // Note: SDL3-CS exposes this as nint (raw bool pointer-style return); 0 = failure.
                if (SDL.SetAudioStreamOutputChannelMap(streamL, LeftOnlyMap, LeftOnlyMap.Length) == 0)
                    Logger.Debug($"SdlAudioBackend: SetAudioStreamOutputChannelMap(L) failed: {SDL.GetError()}");
                if (SDL.SetAudioStreamOutputChannelMap(streamR, RightOnlyMap, RightOnlyMap.Length) == 0)
                    Logger.Debug($"SdlAudioBackend: SetAudioStreamOutputChannelMap(R) failed: {SDL.GetError()}");
            }

            // The pin is now owned by the streams we just created. Bump the refcount
            // by 1 (single voice = single logical reference, regardless of one or two
            // streams - they all share the same sample buffer and lifetime).
            pin.RefCount++;

            float vol = parameters.Volume;
            // Pan defaults to 0 (centre) -> equal-power split = sqrt(0.5) on each side.
            ApplyGainAndPan(streamL, streamR, vol, pan: 0f);

            float rate = parameters.PlaybackRate;
            if (rate > 0f && Math.Abs(rate - 1f) > 1e-6f)
            {
                ApplyPlaybackRate(streamL, rate);
                if (streamR != IntPtr.Zero) ApplyPlaybackRate(streamR, rate);
            }

            if (parameters.Paused)
            {
                SDL.PauseAudioStreamDevice(streamL);
                if (streamR != IntPtr.Zero) SDL.PauseAudioStreamDevice(streamR);
            }

            int id = _nextVoiceId++;
            _voices[id] = new VoiceRecord(streamL, streamR, sound, parameters.Looping, parameters.Paused, vol, 0f);
            return id;
        }
    }

    /// <summary>
    /// Helper: creates a stream, binds it to the playback device, and queues the
    /// initial body. Returns <see cref="IntPtr.Zero"/> on any failure (with a logged
    /// warning).
    /// </summary>
    private IntPtr CreateAndQueueStream(in SDL.AudioSpec srcSpec, in SDL.AudioSpec dstSpec,
        GCHandle pin, int byteCount, string sourcePath)
    {
        var stream = SDL.CreateAudioStream(in srcSpec, in dstSpec);
        if (stream == IntPtr.Zero)
        {
            Logger.Warn($"SdlAudioBackend: CreateAudioStream failed for '{sourcePath}': {SDL.GetError()}");
            return IntPtr.Zero;
        }
        if (!SDL.BindAudioStream(_device, stream))
        {
            Logger.Warn($"SdlAudioBackend: BindAudioStream failed for '{sourcePath}': {SDL.GetError()}");
            SDL.DestroyAudioStream(stream);
            return IntPtr.Zero;
        }
        if (!SDL.PutAudioStreamData(stream, pin.AddrOfPinnedObject(), byteCount))
        {
            Logger.Warn($"SdlAudioBackend: PutAudioStreamData failed for '{sourcePath}': {SDL.GetError()}");
            SDL.DestroyAudioStream(stream);
            return IntPtr.Zero;
        }
        return stream;
    }

    /// <inheritdoc />
    public void StopVoice(int voiceId)
    {
        if (!_initialized || voiceId == 0) return;
        lock (_lock)
        {
            if (!_voices.Remove(voiceId, out var rec)) return;
            SDL.DestroyAudioStream(rec.StreamL); // also unbinds from the device
            if (rec.StreamR != IntPtr.Zero) SDL.DestroyAudioStream(rec.StreamR);
            ReleasePin(rec.Sound);
        }
    }

    /// <inheritdoc />
    public bool IsVoicePlaying(int voiceId)
    {
        if (!_initialized || voiceId == 0) return false;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return false;
            // Loop voices are always "playing" until explicitly stopped.
            if (rec.Looping) return true;
            int queued = SDL.GetAudioStreamQueued(rec.StreamL);
            int avail = SDL.GetAudioStreamAvailable(rec.StreamL);
            if (queued > 0 || avail > 0) return true;
            if (rec.StreamR != IntPtr.Zero)
            {
                queued = SDL.GetAudioStreamQueued(rec.StreamR);
                avail = SDL.GetAudioStreamAvailable(rec.StreamR);
                if (queued > 0 || avail > 0) return true;
            }
            return false;
        }
    }

    /// <inheritdoc />
    public void SetVoicePosition(int voiceId, Vector3 position)
    {
        // SDL has no built-in spatial audio. The ISpatialAudioProcessor (Steam Audio)
        // computes attenuation each tick and the AudioServer feeds it through
        // SetVoiceVolume. Position is recorded by the AudioServer's voice table; this
        // method is intentionally a no-op for the SDL backend.
    }

    /// <inheritdoc />
    public void SetVoiceVolume(int voiceId, float volume)
    {
        if (!_initialized || voiceId == 0) return;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return;
            rec = rec with { Volume = volume };
            _voices[voiceId] = rec;
            ApplyGainAndPan(rec.StreamL, rec.StreamR, rec.Volume, rec.Pan);
        }
    }

    /// <inheritdoc />
    public void SetVoiceLooping(int voiceId, bool looping)
    {
        if (!_initialized || voiceId == 0) return;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return;
            _voices[voiceId] = rec with { Looping = looping };
        }
    }

    /// <inheritdoc />
    public void SetVoicePaused(int voiceId, bool paused)
    {
        if (!_initialized || voiceId == 0) return;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return;
            if (paused)
            {
                SDL.PauseAudioStreamDevice(rec.StreamL);
                if (rec.StreamR != IntPtr.Zero) SDL.PauseAudioStreamDevice(rec.StreamR);
            }
            else
            {
                SDL.ResumeAudioStreamDevice(rec.StreamL);
                if (rec.StreamR != IntPtr.Zero) SDL.ResumeAudioStreamDevice(rec.StreamR);
            }
            _voices[voiceId] = rec with { Paused = paused };
        }
    }

    /// <inheritdoc />
    public void SetListenerPosition(Vector3 position)
    {
        // Same rationale as SetVoicePosition: no built-in 3D in SDL audio.
    }

    /// <inheritdoc />
    /// <remarks>
    /// Real per-voice panning. Spatial voices are created with two streams (each routed
    /// to a single device channel via <see cref="SDL.SetAudioStreamOutputChannelMap"/>);
    /// pan in <c>[-1, +1]</c> is converted to constant-power L/R gains
    /// (<c>sqrt(0.5 * (1 ± pan))</c>) and applied via <see cref="SDL.SetAudioStreamGain"/>.
    /// Non-spatial voices have no R stream, so this call is a no-op for them - matching
    /// the interface's "balance hint" semantics.
    /// </remarks>
    public void SetVoicePan(int voiceId, float pan)
    {
        if (!_initialized || voiceId == 0) return;
        if (float.IsNaN(pan)) pan = 0f;
        if (pan < -1f) pan = -1f; else if (pan > 1f) pan = 1f;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return;
            if (rec.StreamR == IntPtr.Zero) return; // non-spatial voice: no pan support.
            rec = rec with { Pan = pan };
            _voices[voiceId] = rec;
            ApplyGainAndPan(rec.StreamL, rec.StreamR, rec.Volume, pan);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Forwards to <c>SDL_SetAudioStreamFrequencyRatio</c> on both streams (when the
    /// voice is spatial). The ratio is pre-clamped to <c>[0.01, 100]</c> to match
    /// SDL's accepted range; <c>1.0</c> = native pitch.
    /// </remarks>
    public void SetVoicePlaybackRate(int voiceId, float rate)
    {
        if (!_initialized || voiceId == 0) return;
        if (float.IsNaN(rate) || rate <= 0f) rate = 1f;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return;
            ApplyPlaybackRate(rec.StreamL, rate);
            if (rec.StreamR != IntPtr.Zero) ApplyPlaybackRate(rec.StreamR, rate);
        }
    }

    /// <summary>
    /// Splits <paramref name="volume"/> across the L/R streams using a constant-power
    /// pan law: <c>leftGain = sqrt(0.5 * (1 - pan))</c>, <c>rightGain = sqrt(0.5 * (1 + pan))</c>.
    /// At <c>pan = 0</c> both sides receive <c>~0.707 * volume</c>; at the extremes one
    /// side receives the full <c>volume</c> and the other is silent. For non-spatial
    /// voices (no R stream) the L stream just receives the raw <paramref name="volume"/>.
    /// </summary>
    private static void ApplyGainAndPan(IntPtr streamL, IntPtr streamR, float volume, float pan)
    {
        if (streamR == IntPtr.Zero)
        {
            SDL.SetAudioStreamGain(streamL, volume);
            return;
        }
        float leftGain  = MathF.Sqrt(0.5f * (1f - pan));
        float rightGain = MathF.Sqrt(0.5f * (1f + pan));
        SDL.SetAudioStreamGain(streamL, volume * leftGain);
        SDL.SetAudioStreamGain(streamR, volume * rightGain);
    }

    /// <inheritdoc />
    public void Update()
    {
        if (!_initialized) return;
        lock (_lock)
        {
            // 1) Refill loop voices that are about to drain (per stream).
            // 2) Reap one-shot voices whose stream(s) have been fully consumed.
            List<int>? toRemove = null;
            foreach (var (id, rec) in _voices)
            {
                if (rec.Looping)
                {
                    RefillIfDraining(rec.StreamL, rec.Sound);
                    if (rec.StreamR != IntPtr.Zero) RefillIfDraining(rec.StreamR, rec.Sound);
                }
                else if (StreamFullyConsumed(rec.StreamL) &&
                         (rec.StreamR == IntPtr.Zero || StreamFullyConsumed(rec.StreamR)))
                {
                    (toRemove ??= new()).Add(id);
                }
            }
            if (toRemove is { } list)
            {
                foreach (var id in list)
                {
                    if (_voices.Remove(id, out var rec))
                    {
                        SDL.DestroyAudioStream(rec.StreamL);
                        if (rec.StreamR != IntPtr.Zero) SDL.DestroyAudioStream(rec.StreamR);
                        ReleasePin(rec.Sound);
                    }
                }
            }
        }
    }

    private void RefillIfDraining(IntPtr stream, Sound sound)
    {
        int queued = SDL.GetAudioStreamQueued(stream);
        if (queued >= LoopRefillBytesThreshold) return;
        if (!_samplePins.TryGetValue(sound, out var pin)) return;
        int byteCount = sound.Samples.Length * sizeof(float);
        SDL.PutAudioStreamData(stream, pin.Handle.AddrOfPinnedObject(), byteCount);
    }

    /// <summary>
    /// Decrements the refcount on the pin shared by every voice playing
    /// <paramref name="sound"/>. When the last voice releases it, the pin is freed and
    /// the entry removed - so a Sound that's been unloaded (or never played again)
    /// stops keeping its sample buffer pinned in managed memory.
    /// </summary>
    private void ReleasePin(Sound sound)
    {
        if (!_samplePins.TryGetValue(sound, out var pin)) return;
        if (--pin.RefCount > 0) return;
        if (pin.Handle.IsAllocated) pin.Handle.Free();
        _samplePins.Remove(sound);
    }

    /// <summary>
    /// SDL3 clamps <c>SDL_SetAudioStreamFrequencyRatio</c> to <c>[0.01, 100]</c>; we
    /// pre-clamp on our side to keep the ratio in the documented range and so logging
    /// stays attributable to gameplay rather than to SDL.
    /// </summary>
    private static void ApplyPlaybackRate(IntPtr stream, float rate)
    {
        if (rate < 0.01f) rate = 0.01f; else if (rate > 100f) rate = 100f;
        SDL.SetAudioStreamFrequencyRatio(stream, rate);
    }

    private static bool StreamFullyConsumed(IntPtr stream) =>
        SDL.GetAudioStreamQueued(stream) <= 0 && SDL.GetAudioStreamAvailable(stream) <= 0;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            foreach (var rec in _voices.Values)
            {
                SDL.DestroyAudioStream(rec.StreamL);
                if (rec.StreamR != IntPtr.Zero) SDL.DestroyAudioStream(rec.StreamR);
            }
            _voices.Clear();
            foreach (var pin in _samplePins.Values)
                if (pin.Handle.IsAllocated) pin.Handle.Free();
            _samplePins.Clear();

            if (_initialized && _device != 0) SDL.CloseAudioDevice(_device);
            _device = 0;
            if (_ownsAudioSubsystem)
            {
                SDL.QuitSubSystem(SDL.InitFlags.Audio);
                _ownsAudioSubsystem = false;
            }
            _initialized = false;
        }
    }

    /// <summary>
    /// Per-voice record. Spatial voices have two streams bound to the device, each
    /// channel-mapped to a single output channel (L or R) so a constant-power pan can
    /// be implemented with two independent <see cref="SDL.SetAudioStreamGain"/> calls.
    /// Non-spatial voices leave <see cref="StreamR"/> as <see cref="IntPtr.Zero"/>;
    /// <see cref="Pan"/> is then ignored. <see cref="Sound"/> is retained so loop
    /// refills can find the original sample buffer without re-querying ECS / AssetServer.
    /// </summary>
    private sealed record VoiceRecord(
        IntPtr StreamL,
        IntPtr StreamR,
        Sound Sound,
        bool Looping,
        bool Paused,
        float Volume,
        float Pan);

    /// <summary>
    /// Mutable refcounted holder for a pinned sample buffer. <see cref="RefCount"/>
    /// counts the number of live voices (not streams) that still reference the pin;
    /// the pin is freed and the entry removed once the last voice releases it.
    /// </summary>
    private sealed class PinEntry(GCHandle handle)
    {
        public GCHandle Handle = handle;
        public int RefCount;
    }
}