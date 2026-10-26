using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL3;

namespace Engine;

/// <summary>
/// <see cref="IAudioBackend"/> backed by SDL3's audio subsystem (<c>SDL_audio.h</c>)
/// via the <c>SDL3-CS</c> managed bindings. Each voice of a <see cref="Sound"/> is an
/// <c>SDL_AudioStream</c> fed from its samples as the device asks, through the voice's low-pass
/// and reverb and panned; SDL resamples each stream and mixes any number bound to the device.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why SDL3:</b> SDL3-CS already ships with the engine for windowing + input,
/// the native binaries (Windows/Linux/macOS) come from <c>SDL3-CS.Native</c>, and the
/// stream API is rich enough to cover the gameplay-facing
/// <see cref="IAudioBackend"/> contract without any per-platform shim. The audio server's
/// <see cref="ISpatialAudioProcessor"/>, the engine's own unless another replaces it, supplies
/// each spatial voice's pan and its attenuation with distance, since SDL itself does no
/// positional audio.
/// </para>
/// <para>
/// <b>Voice model:</b> one <c>SDL_AudioStream</c> per voice, bound to the single
/// logical playback device opened during <see cref="Initialize"/>. A sound's voice is a stereo
/// stream at the sound's rate, which SDL's get callback asks for a block at a time, and which the
/// voice's feed fills from the samples, wrapping for a loop, through the voice's low-pass and
/// reverb, with its pan applied, and then with the room's tail, so the effects and the balance
/// change as it plays. SDL converts it to the device's mix format. The
/// <see cref="AudioServer"/>'s int voice id maps to the stream pointer through an internal
/// table; <c>0</c> always means "invalid".
/// </para>
/// <para>
/// <b>No device:</b> where no audio device opens, as on a machine with none or a runner, sound
/// goes to SDL's dummy driver, which takes samples at the rate a device would play them and plays
/// none, as raylib's goes to miniaudio's null device (REVIEW.md, Decision 13), so sounds end,
/// music moves on and streams ask for more as they do with a device. Where even that fails,
/// <see cref="Initialize"/> logs and leaves <see cref="IsInitialized"/> <c>false</c>, and every
/// call after is a no-op, as <see cref="NullAudioBackend"/>'s are.
/// </para>
/// <para>
/// <b>No window:</b> a run that shows no window, hidden, offscreen or headless, as a test, a soak
/// or <c>./e3d open --hidden</c> runs, opens the dummy driver first (<see cref="Silent"/>), so
/// nothing it plays reaches the machine's speakers while every sound still runs its course, unless
/// its <see cref="Config.AudibleWithoutWindow"/> asks for the device.
/// </para>
/// </remarks>
/// <seealso cref="SdlAudioPlugin"/>
/// <seealso cref="IAudioBackend"/>
internal sealed partial class SdlAudioBackend : IAudioBackend
{
    private static readonly ILogger Logger = Log.Category("Engine.Sound.Sdl");

    // The failures to open audio this process has warned of, by their message, so a machine with no
    // audio device says so once however many apps open audio on it, and after that at info.
    private static readonly HashSet<string> Warned = [];

    /// <summary>Logs a failure to open audio as a warning the first time this process meets it, and as info after.</summary>
    internal static void WarnOnce(string message)
    {
        bool first;
        lock (Warned) first = Warned.Add(message);
        if (first) Logger.Warn(message);
        else Logger.Info(message + " As before in this process.");
    }

    /// <summary>
    /// Output channel maps used to implement constant-power panning of a stream voice, whose
    /// samples its owner queues. SDL3 has no per-channel gain on a single stream, so a pannable
    /// stream voice owns two streams bound to the device:
    /// <list type="bullet">
    ///   <item><description><see cref="LeftOnlyMap"/> = <c>{0, -1}</c>, the L stream, which
    ///   plays its source on the device's left channel and is silent on the right.</description></item>
    ///   <item><description><see cref="RightOnlyMap"/> = <c>{-1, 1}</c>, the R stream, which
    ///   plays its source on the device's right channel and is silent on the left.</description></item>
    /// </list>
    /// We then split the per-voice gain into <c>(masterGain * leftPanGain)</c> and
    /// <c>(masterGain * rightPanGain)</c> via <see cref="SDL.SetAudioStreamGain"/>.
    /// </summary>
    private static readonly int[] LeftOnlyMap  = { 0, -1 };
    private static readonly int[] RightOnlyMap = { -1, 1 };

    private readonly object _lock = new();
    private readonly Dictionary<int, VoiceRecord> _voices = new();
    private uint _device;
    private SDL.AudioSpec _deviceSpec;
    private bool _initialized;
    private bool _ownsAudioSubsystem;
    private bool _disposed;
    private int _nextVoiceId = 1;

    /// <inheritdoc />
    public bool IsInitialized => _initialized;

    /// <inheritdoc />
    /// <remarks>What SDL gave for the 48,000 asked, the device's own where it has one.</remarks>
    public int DeviceSampleRate => _initialized ? _deviceSpec.Freq : 0;

    /// <inheritdoc />
    /// <remarks>What SDL gave for the two asked for, more on a device of more speakers.</remarks>
    public int DeviceChannels => _initialized ? _deviceSpec.Channels : 0;

    /// <inheritdoc />
    public string BackendId => "sdl3";

    /// <summary>Whether SDL's dummy driver is tried where no device opens, false only for a test of a backend with none.</summary>
    internal bool FallBackToDummy { get; init; } = true;

    /// <summary>The audio driver SDL opened the device through, <c>dummy</c> where no device opened, or null before one has.</summary>
    internal string? Driver { get; private set; }

    /// <summary>Whether sound goes to SDL's dummy driver whatever devices the machine has, as for a run that shows no window.</summary>
    internal bool Silent { get; init; }

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;
            try
            {
                if (Silent)
                {
                    if (TryOpenDummy(out var why))
                        Logger.Info("SdlAudioBackend: the run shows no window, so sound goes to SDL's dummy driver, which takes it at the rate it plays and plays none.");
                    else
                        WarnOnce($"SdlAudioBackend: the run shows no window, and with SDL's dummy driver {why}, so the backend is disabled.");
                    return;
                }
                if (TryOpen(out var failure)) return;
                if (!FallBackToDummy)
                {
                    WarnOnce($"SdlAudioBackend: {failure}, so the backend is disabled.");
                    return;
                }

                // No audio device opens, so sound goes to SDL's dummy driver, as raylib's goes to
                // miniaudio's null device.
                if (TryOpenDummy(out var dummyFailure))
                    WarnOnce($"SdlAudioBackend: {failure}, so sound goes to SDL's dummy driver, which takes it at the rate it plays and plays none.");
                else
                    WarnOnce($"SdlAudioBackend: {failure}, and with SDL's dummy driver {dummyFailure}, so the backend is disabled.");
            }
            catch (DllNotFoundException ex)
            {
                Logger.Warn($"SdlAudioBackend: native 'SDL3' library not found ({ex.Message}). Backend disabled.");
            }
            catch (Exception ex)
            {
                Logger.Warn($"SdlAudioBackend: initialization failed ({ex.GetType().Name}: {ex.Message}). Backend disabled.");
            }
        }
    }

    // Opens SDL's dummy driver, chosen over what the environment names. Where another backend of
    // the process started SDL's audio through a device's driver, the device it opens is that
    // driver's, which is closed again and said, as nothing here may play through it.
    private bool TryOpenDummy(out string failure)
    {
        SDL.SetHintWithPriority(SDL.Hints.AudioDriver, "dummy", SDL.HintPriority.Override);
        try
        {
            if (!TryOpen(out failure)) return false;
            if (Driver == "dummy") return true;
            failure = $"SDL's audio was started through '{Driver}' already";
            SDL.CloseAudioDevice(_device);
            _device = 0;
            _initialized = false;
            if (_ownsAudioSubsystem) { SDL.QuitSubSystem(SDL.InitFlags.Audio); _ownsAudioSubsystem = false; }
            return false;
        }
        finally
        {
            SDL.ResetHint(SDL.Hints.AudioDriver);
        }
    }

    // Starts SDL's audio, unless the app's SDL already has, and opens the default playback device,
    // saying what failed where it cannot. SDL_Init is additive, so where the app's SDL booted video
    // and gamepads, adding audio starts audio alone, and only what was started here is quit.
    private bool TryOpen(out string failure)
    {
        if (!SDL.WasInit(SDL.InitFlags.Audio).HasFlag(SDL.InitFlags.Audio))
        {
            if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
            {
                failure = $"SDL_InitSubSystem(Audio) failed with '{SDL.GetError()}'";
                return false;
            }
            _ownsAudioSubsystem = true;
        }

        // A sensible default, which SDL negotiates something close to.
        var desired = new SDL.AudioSpec
        {
            Format = SDL.AudioFormat.AudioF32LE,
            Channels = 2,
            Freq = 48000,
        };
        _device = SDL.OpenAudioDevice(SDL.AudioDeviceDefaultPlayback, in desired);
        if (_device == 0)
        {
            failure = $"SDL_OpenAudioDevice failed with '{SDL.GetError()}'";
            if (_ownsAudioSubsystem) { SDL.QuitSubSystem(SDL.InitFlags.Audio); _ownsAudioSubsystem = false; }
            return false;
        }
        if (!SDL.GetAudioDeviceFormat(_device, out _deviceSpec, out _))
        {
            Logger.Debug($"SdlAudioBackend: GetAudioDeviceFormat failed ('{SDL.GetError()}'); falling back to desired spec.");
            _deviceSpec = desired;
        }

        _initialized = true;
        Driver = SDL.GetCurrentAudioDriver();
        Logger.Info(
            $"SdlAudioBackend: device opened through '{Driver}' (id={_device}, format={_deviceSpec.Format}, " +
            $"{_deviceSpec.Channels}ch @ {_deviceSpec.Freq}Hz).");
        failure = "";
        return true;
    }

    /// <inheritdoc />
    public int CreateVoice(Sound sound, in AudioVoiceParams parameters)
    {
        if (!_initialized || sound is null) return 0;
        if (sound.Samples.Length == 0 || sound.Channels <= 0 || sound.SampleRate <= 0) return 0;
        lock (_lock)
        {
            var (stream, feed) = CreateFedStream(sound, parameters);
            if (feed is null) return 0;
            // Its gain and rate are set before it is bound, so its first block plays at them.
            float vol = parameters.Volume;
            SDL.SetAudioStreamGain(stream, vol);
            float rate = parameters.PlaybackRate;
            if (rate > 0f && Math.Abs(rate - 1f) > 1e-6f) ApplyPlaybackRate(stream, rate);
            int id = _nextVoiceId++;
            var rec = new VoiceRecord(stream, IntPtr.Zero, sound, parameters.Looping, parameters.Paused, vol, feed.Pan, Feed: feed);
            _voices[id] = rec;
            if (!parameters.Paused) SetBound(rec, bound: true);
            return id;
        }
    }

    /// <inheritdoc />
    public int CreateStreamVoice(int channels, int sampleRate, in AudioVoiceParams parameters)
    {
        if (!_initialized || channels <= 0 || sampleRate <= 0) return 0;
        var srcSpec = new SDL.AudioSpec { Format = SDL.AudioFormat.AudioF32LE, Channels = channels, Freq = sampleRate };
        // A pannable voice is two streams fed the same samples, one to each channel, as a
        // positional voice is.
        var dstSpec = parameters.Pannable
            ? new SDL.AudioSpec { Format = SDL.AudioFormat.AudioF32LE, Channels = 2, Freq = _deviceSpec.Freq }
            : _deviceSpec;
        lock (_lock)
        {
            var stream = CreateBoundStream(in srcSpec, in dstSpec);
            if (stream == IntPtr.Zero) return 0;
            var streamR = IntPtr.Zero;
            if (parameters.Pannable)
            {
                streamR = CreateBoundStream(in srcSpec, in dstSpec);
                if (streamR == IntPtr.Zero)
                {
                    SDL.DestroyAudioStream(stream);
                    return 0;
                }
                SDL.SetAudioStreamOutputChannelMap(stream, LeftOnlyMap, LeftOnlyMap.Length);
                SDL.SetAudioStreamOutputChannelMap(streamR, RightOnlyMap, RightOnlyMap.Length);
            }

            var volume = parameters.Volume;
            var pan = parameters.Pannable ? Math.Clamp(parameters.Pan, -1f, 1f) : 0f;
            ApplyGainAndPan(stream, streamR, volume, pan);
            if (parameters.PlaybackRate > 0f && Math.Abs(parameters.PlaybackRate - 1f) > 1e-6f)
            {
                ApplyPlaybackRate(stream, parameters.PlaybackRate);
                if (streamR != IntPtr.Zero) ApplyPlaybackRate(streamR, parameters.PlaybackRate);
            }

            int id = _nextVoiceId++;
            var rec = new VoiceRecord(stream, streamR, null, Looping: false, parameters.Paused, volume, pan, Channels: channels);
            _voices[id] = rec;
            if (parameters.Paused) SetBound(rec, bound: false);
            return id;
        }
    }

    // A stream from a source to a destination format, bound to the device, or 0 with the reason logged.
    private IntPtr CreateBoundStream(in SDL.AudioSpec source, in SDL.AudioSpec destination)
    {
        var stream = SDL.CreateAudioStream(in source, in destination);
        if (stream == IntPtr.Zero)
        {
            Logger.Warn($"SdlAudioBackend: CreateAudioStream failed for a stream voice. {SDL.GetError()}");
            return IntPtr.Zero;
        }
        if (!SDL.BindAudioStream(_device, stream))
        {
            Logger.Warn($"SdlAudioBackend: BindAudioStream failed for a stream voice. {SDL.GetError()}");
            SDL.DestroyAudioStream(stream);
            return IntPtr.Zero;
        }
        return stream;
    }

    /// <inheritdoc />
    public unsafe void QueueVoiceSamples(int voiceId, ReadOnlySpan<float> samples)
    {
        if (!_initialized || samples.IsEmpty) return;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec) || rec.Channels == 0) return;
            fixed (float* data = samples)
            {
                SDL.PutAudioStreamData(rec.StreamL, (IntPtr)data, samples.Length * sizeof(float));
                if (rec.StreamR != IntPtr.Zero) SDL.PutAudioStreamData(rec.StreamR, (IntPtr)data, samples.Length * sizeof(float));
            }
        }
    }

    /// <inheritdoc />
    public long QueuedVoiceFrames(int voiceId)
    {
        if (!_initialized) return 0;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec) || rec.Channels == 0) return 0;
            return SDL.GetAudioStreamQueued(rec.StreamL) / (sizeof(float) * rec.Channels);
        }
    }

    /// <inheritdoc />
    public void StopVoice(int voiceId)
    {
        if (!_initialized || voiceId == 0) return;
        lock (_lock)
        {
            if (!_voices.Remove(voiceId, out var rec)) return;
            Destroy(rec);
        }
    }

    /// <inheritdoc />
    public bool IsVoicePlaying(int voiceId)
    {
        if (!_initialized || voiceId == 0) return false;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return false;
            // A paused voice is not mixing, whatever it has queued.
            if (rec.Paused) return false;
            // Loop voices are always "playing" until explicitly stopped, and so are stream
            // voices, which their owner feeds and stops, and a sound's voice whose feed has not
            // given its samples and tail.
            if (rec.Looping || rec.Channels != 0 || rec.Feed is { Ended: false }) return true;
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
        // SDL has no built-in spatial audio. The audio server's spatial processor computes
        // the pan and the attenuation each tick and feeds them through SetVoicePan and
        // SetVoiceVolume. Position is recorded by the AudioServer's voice table, so this
        // method is a no-op for the SDL backend.
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
            // A loop let go plays to the end of its samples, its tail and the frames its
            // resampling holds back, which the feed flushes as it ends.
            if (rec.Feed is { } feed) feed.Looping = looping;
        }
    }

    /// <inheritdoc />
    public void SetVoicePaused(int voiceId, bool paused)
    {
        if (!_initialized || voiceId == 0) return;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return;
            if (paused == rec.Paused) return;
            SetBound(rec, !paused);
            _voices[voiceId] = rec with { Paused = paused };
        }
    }

    /// <inheritdoc />
    public void SetListenerPosition(Vector3 position)
    {
        // Same rationale as SetVoicePosition, since SDL audio has no built-in 3D.
    }

    /// <inheritdoc />
    /// <remarks>
    /// Real per-voice panning. Spatial voices are created with two streams (each routed
    /// to a single device channel via <see cref="SDL.SetAudioStreamOutputChannelMap"/>);
    /// pan in <c>[-1, +1]</c> is converted to constant-power L/R gains
    /// (<c>sqrt(0.5 * (1 ± pan))</c>) and applied via <see cref="SDL.SetAudioStreamGain"/>.
    /// A voice that is not spatial has no R stream, so the call does nothing for it, as the
    /// interface allows of a balance it calls a hint.
    /// </remarks>
    public void SetVoicePan(int voiceId, float pan)
    {
        if (!_initialized || voiceId == 0) return;
        if (float.IsNaN(pan)) pan = 0f;
        if (pan < -1f) pan = -1f; else if (pan > 1f) pan = 1f;
        lock (_lock)
        {
            if (!_voices.TryGetValue(voiceId, out var rec)) return;
            // A sound's voice is panned as it is fed, where it was made pannable or positional.
            if (rec.Feed is { } feed)
            {
                if (feed.Panned) feed.Pan = pan;
                _voices[voiceId] = rec with { Pan = pan };
                return;
            }
            if (rec.StreamR == IntPtr.Zero) return; // A voice that is not spatial has no pan.
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

    // Pauses a voice by taking its streams off the device, which keeps what they have queued
    // and stops them mixing, and resumes it by putting them back. Pausing the device instead
    // would pause every voice, because they all share it.
    private void SetBound(VoiceRecord rec, bool bound)
    {
        foreach (var stream in new[] { rec.StreamL, rec.StreamR })
        {
            if (stream == IntPtr.Zero) continue;
            if (!bound) SDL.UnbindAudioStream(stream);
            else if (!SDL.BindAudioStream(_device, stream))
                Logger.Warn($"SdlAudioBackend: BindAudioStream failed putting a voice on the device. {SDL.GetError()}");
        }
    }

    /// <summary>
    /// Splits <paramref name="volume"/> across the L/R streams using a constant-power
    /// pan law, <c>leftGain = sqrt(0.5 * (1 - pan))</c>, <c>rightGain = sqrt(0.5 * (1 + pan))</c>.
    /// At <c>pan = 0</c> both sides receive <c>~0.707 * volume</c>; at the extremes one
    /// side receives the full <c>volume</c> and the other is silent. For non-spatial
    /// voices (no R stream) the L stream receives the whole <paramref name="volume"/>.
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
            // A sound's voice played once is let go once its feed has ended and its stream has
            // drained. A loop's feed wraps, and a stream voice is fed by its owner until stopped.
            List<int>? toRemove = null;
            foreach (var (id, rec) in _voices)
                if (rec.Feed is { Ended: true } && StreamFullyConsumed(rec.StreamL))
                    (toRemove ??= new()).Add(id);
            if (toRemove is { } list)
                foreach (var id in list)
                    if (_voices.Remove(id, out var rec))
                        Destroy(rec);
        }
    }

    // Destroys a voice's streams, a fed one's handle freed after SDL lets go of its stream.
    private static void Destroy(VoiceRecord rec)
    {
        if (rec.Feed is { } feed)
        {
            Release(rec.StreamL, feed);
            return;
        }
        SDL.DestroyAudioStream(rec.StreamL); // also unbinds from the device
        if (rec.StreamR != IntPtr.Zero) SDL.DestroyAudioStream(rec.StreamR);
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
            foreach (var rec in _voices.Values) Destroy(rec);
            _voices.Clear();

            if (_postmixHandle.IsAllocated) StopPostmix();
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
    /// Per-voice record. A sound's voice is one stream its <see cref="Feed"/> fills, which keeps
    /// the voice's effects and balance. A pannable stream voice has two streams bound to the
    /// device, each channel-mapped to a single output channel (L or R) so a constant-power pan is
    /// two independent <see cref="SDL.SetAudioStreamGain"/> calls, and one that is not leaves
    /// <see cref="StreamR"/> as <see cref="IntPtr.Zero"/>. A stream voice has no <see cref="Sound"/>
    /// and records its <see cref="Channels"/>, which are 0 for a sound's voice.
    /// </summary>
    private sealed record VoiceRecord(
        IntPtr StreamL,
        IntPtr StreamR,
        Sound? Sound,
        bool Looping,
        bool Paused,
        float Volume,
        float Pan,
        int Channels = 0,
        VoiceFeed? Feed = null);
}
