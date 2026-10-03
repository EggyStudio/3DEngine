using System.Numerics;

namespace Engine;

/// <summary>
/// World-resource entry point for audio playback. Wraps the active
/// <see cref="IAudioBackend"/> (and optional <see cref="ISpatialAudioProcessor"/>) behind
/// a stable, gameplay-friendly API that returns durable <see cref="AudioSource"/>
/// handles even when the underlying voice doesn't exist yet (e.g. the
/// <see cref="Sound"/> asset is still loading on a worker thread).
/// </summary>
/// <remarks>
/// <para>
/// <b>Ticket indirection:</b> <see cref="AudioSource"/> stores a server-issued
/// <i>ticket id</i> (not a backend voice id). The server keeps a table mapping ticket
/// → voice; <see cref="AudioSource.SetPosition"/>, <see cref="AudioSource.Stop"/>, etc.
/// route through the table. This lets calls like
/// <c>ctx.PlaySpatialSound("a.wav", pos)</c> succeed synchronously - the ticket is
/// minted immediately, the actual voice is created later from
/// <see cref="ResolvePending"/> once the asset loader finishes.
/// </para>
/// <para>
/// <b>Backend swap:</b> backends are hot-swappable via <see cref="SetBackend"/>; the
/// previous backend is stopped and disposed. The default is <see cref="NullAudioBackend"/>
/// so all calls are safe before any real backend is wired.
/// </para>
/// </remarks>
public sealed class AudioServer : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.Sound");

    private readonly object _lock = new();
    private readonly Dictionary<int, VoiceRecord> _voices = new();
    private readonly List<PendingVoice> _pending = new();

    private IAudioBackend _backend = new NullAudioBackend();
    private ISpatialAudioProcessor? _spatial;
    private int _nextTicket = 1;
    private Vector3 _listenerPosition;
    private Quaternion _listenerOrientation = Quaternion.Identity;
    private float _masterVolume = 1f;
    private bool _warnedSpatialFlip;

    /// <summary>The currently active backend. Defaults to <see cref="NullAudioBackend"/>.</summary>
    public IAudioBackend Backend
    {
        get { lock (_lock) return _backend; }
    }

    /// <summary>Optional spatial post-processor. Returns <c>null</c> when none is wired.</summary>
    public ISpatialAudioProcessor? Spatial
    {
        get { lock (_lock) return _spatial; }
    }

    /// <summary>Current listener position in world space (used for 3D voices).</summary>
    public Vector3 ListenerPosition
    {
        get { lock (_lock) return _listenerPosition; }
        set
        {
            lock (_lock)
            {
                _listenerPosition = value;
                _backend.SetListenerPosition(value);
            }
        }
    }

    /// <summary>
    /// Current listener orientation as a world-space quaternion (default = identity).
    /// Used by the spatial processor to compute relative direction (panning) and to
    /// project source orientations into listener space (directivity). Updated each
    /// frame by <see cref="AudioListenerSystem"/> from the active listener entity's
    /// <see cref="Transform.Rotation"/>.
    /// </summary>
    public Quaternion ListenerOrientation
    {
        get { lock (_lock) return _listenerOrientation; }
        set { lock (_lock) _listenerOrientation = value; }
    }

    /// <summary>
    /// Global linear gain multiplier folded into every voice's per-frame output volume
    /// by <see cref="Tick"/>. <c>1.0</c> = unity (default). Set by
    /// <see cref="AudioListenerSystem"/> from the active <see cref="AudioListener.MasterVolume"/>
    /// each frame, but can also be driven directly (e.g. options menu, mute toggle).
    /// Negative or NaN values are clamped to <c>0</c>.
    /// </summary>
    public float MasterVolume
    {
        get { lock (_lock) return _masterVolume; }
        set
        {
            if (float.IsNaN(value) || value < 0f) value = 0f;
            lock (_lock) _masterVolume = value;
        }
    }

    /// <summary>Replaces the active backend. Disposes the previous one.</summary>
    public void SetBackend(IAudioBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        IAudioBackend? previous;
        lock (_lock)
        {
            previous = _backend is NullAudioBackend ? null : _backend;
            _backend = backend;
            if (!_backend.IsInitialized) _backend.Initialize();
            _backend.SetListenerPosition(_listenerPosition);
        }
        previous?.Dispose();
        Logger.Info($"AudioServer: backend set to '{backend.BackendId}'.");
    }

    /// <summary>Installs (or replaces) the spatial post-processor.</summary>
    public void SetSpatialProcessor(ISpatialAudioProcessor? processor)
    {
        ISpatialAudioProcessor? previous;
        lock (_lock)
        {
            previous = _spatial;
            _spatial = processor;
            if (processor is { } p && !object.ReferenceEquals(previous, p)) p.Initialize();
        }
        if (previous is not null && !object.ReferenceEquals(previous, processor))
            previous.Dispose();
        Logger.Info($"AudioServer: spatial processor set to '{processor?.ProcessorId ?? "<none>"}'.");
    }

    /// <summary>
    /// Plays <paramref name="sound"/> as a non-spatial 2D voice. Returns an
    /// <see cref="AudioSource"/> ticket usable for the lifetime of the voice.
    /// </summary>
    public AudioSource Play(Sound sound, AudioVoiceParams parameters = default)
    {
        ArgumentNullException.ThrowIfNull(sound);
        if (parameters.Volume == 0f) parameters = parameters with { Volume = 1f };
        return CreateInternal(sound, parameters);
    }

    /// <summary>Plays <paramref name="sound"/> as a 3D voice positioned at <paramref name="position"/>.</summary>
    public AudioSource PlaySpatial(Sound sound, Vector3 position, AudioVoiceParams parameters = default)
    {
        ArgumentNullException.ThrowIfNull(sound);
        if (parameters.Volume == 0f) parameters = parameters with { Volume = 1f };
        return CreateInternal(sound, parameters with { Position = position });
    }

    /// <summary>
    /// Like <see cref="Play(Sound, AudioVoiceParams)"/> but accepts an asset
    /// <see cref="Handle{T}"/>. If the asset hasn't finished loading yet the source is
/// recorded as pending and resolved by <see cref="ResolvePending"/> once
/// <c>Assets&lt;Sound&gt;.TryGet</c> succeeds.
    /// </summary>
    public AudioSource Play(Handle<Sound> handle, Assets<Sound> assets, AudioVoiceParams parameters = default)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return CreateFromHandle(handle, assets, parameters);
    }

    /// <summary>Spatial variant of <see cref="Play(Handle{Sound}, Assets{Sound}, AudioVoiceParams)"/>.</summary>
    public AudioSource PlaySpatial(Handle<Sound> handle, Assets<Sound> assets, Vector3 position, AudioVoiceParams parameters = default)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return CreateFromHandle(handle, assets, parameters with { Position = position });
    }

    private AudioSource CreateFromHandle(Handle<Sound> handle, Assets<Sound> assets, AudioVoiceParams parameters)
    {
        if (parameters.Volume == 0f) parameters = parameters with { Volume = 1f };
        if (parameters.PlaybackRate <= 0f) parameters = parameters with { PlaybackRate = 1f };

        if (assets.TryGet(handle, out var sound) && sound is not null)
            return CreateInternal(sound, parameters);

        // Defer until the asset finishes loading.
        int ticket;
        lock (_lock)
        {
            ticket = _nextTicket++;
            _voices[ticket] = new VoiceRecord(VoiceId: 0, IsSpatial: parameters.Position is not null,
                Position: parameters.Position ?? Vector3.Zero, Volume: parameters.Volume,
                Looping: parameters.Looping, Paused: parameters.Paused,
                Orientation: parameters.Orientation,
                DipoleWeight: parameters.DipoleWeight, DipolePower: parameters.DipolePower,
                PlaybackRate: parameters.PlaybackRate);
            _pending.Add(new PendingVoice(ticket, handle, parameters));
        }
        return new AudioSource(ticket, this);
    }

    private AudioSource CreateInternal(Sound sound, AudioVoiceParams parameters)
    {
        if (parameters.PlaybackRate <= 0f) parameters = parameters with { PlaybackRate = 1f };
        int voiceId;
        lock (_lock)
        {
            voiceId = _backend.CreateVoice(sound, parameters);
            int ticket = _nextTicket++;
            _voices[ticket] = new VoiceRecord(voiceId,
                IsSpatial: parameters.Position is not null,
                Position: parameters.Position ?? Vector3.Zero,
                Volume: parameters.Volume,
                Looping: parameters.Looping,
                Paused: parameters.Paused,
                Orientation: parameters.Orientation,
                DipoleWeight: parameters.DipoleWeight,
                DipolePower: parameters.DipolePower,
                PlaybackRate: parameters.PlaybackRate);
            return new AudioSource(ticket, this);
        }
    }

    /// <summary>
    /// Walks the pending-voice list and instantiates any whose asset has finished loading.
    /// Pumped each frame by <see cref="AudioUpdateSystem"/>.
    /// </summary>
    /// <remarks>
    /// Mutations made on the <see cref="AudioSource"/> handle while the asset was still
    /// loading (e.g. <see cref="AudioSource.SetPosition"/>, <see cref="AudioSource.SetVolume"/>,
    /// <see cref="AudioSource.SetOrientation"/>, ...) are recorded on the
    /// <see cref="VoiceRecord"/> immediately. When the asset finally resolves we rebuild
    /// the <see cref="AudioVoiceParams"/> from the live record so the backend voice
    /// starts with the *current* state, not the snapshot captured at <c>Play</c> time.
    /// </remarks>
    public void ResolvePending(Assets<Sound> assets)
    {
        if (_pending.Count == 0) return;
        ArgumentNullException.ThrowIfNull(assets);

        lock (_lock)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (!assets.TryGet(p.Handle, out var sound) || sound is null) continue;

                // Reconcile against any state the gameplay set on the handle while we
                // were waiting on the asset loader. The original p.Parameters are only
                // used as a fallback for fields the VoiceRecord doesn't carry.
                AudioVoiceParams effective = p.Parameters;
                if (_voices.TryGetValue(p.Ticket, out var rec))
                {
                    effective = effective with
                    {
                        Position     = rec.IsSpatial ? rec.Position : null,
                        Volume       = rec.Volume,
                        Looping      = rec.Looping,
                        Paused       = rec.Paused,
                        Orientation  = rec.Orientation,
                        DipoleWeight = rec.DipoleWeight,
                        DipolePower  = rec.DipolePower,
                        PlaybackRate = rec.PlaybackRate,
                    };
                }

                int voiceId = _backend.CreateVoice(sound, effective);
                if (_voices.TryGetValue(p.Ticket, out rec))
                {
                    _voices[p.Ticket] = rec with { VoiceId = voiceId };
                }
                _pending.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Per-frame pump:
    /// <list type="number">
    ///   <item><description>For every live voice, computes its effective output volume
    ///   (<c>MasterVolume × Volume × spatial.VolumeAttenuation</c>) and pushes it to the
    ///   backend.</description></item>
    ///   <item><description>For spatial voices, runs the optional
    ///   <see cref="ISpatialAudioProcessor"/>: distance + directivity feed the volume
    ///   factor and the computed pan is forwarded via
    ///   <see cref="IAudioBackend.SetVoicePan"/>.</description></item>
    ///   <item><description>Forwards the backend's own <see cref="IAudioBackend.Update"/>.</description></item>
    /// </list>
    /// Non-spatial voices skip the spatial processor (gain factor = <c>1</c>) but still
    /// receive the master-volume multiplication so a single options-menu slider scales
    /// every voice in the mix.
    /// </summary>
    public void Tick()
    {
        lock (_lock)
        {
            float master = _masterVolume;
            ISpatialAudioProcessor? sp = _spatial;

            // Pre-compute listener basis once per tick. Engine convention (matches
            // Matrix4x4.CreateLookAt usage in Engine.Renderer.Extracts):
            //   forward = rotated -Z, up = rotated +Y.
            Vector3 listenerForward = Vector3.Transform(-Vector3.UnitZ, _listenerOrientation);
            Vector3 listenerUp      = Vector3.Transform( Vector3.UnitY, _listenerOrientation);

            foreach (var (ticket, rec) in _voices)
            {
                if (rec.VoiceId == 0) continue;

                float spatialGain = 1f;
                if (rec.IsSpatial && sp is not null)
                {
                    Vector3 srcForward = -Vector3.UnitZ, srcUp = Vector3.UnitY;
                    if (rec.Orientation is { } q)
                    {
                        srcForward = Vector3.Transform(-Vector3.UnitZ, q);
                        srcUp      = Vector3.Transform( Vector3.UnitY, q);
                    }

                    var ctx = new SpatialContext
                    {
                        SourceForward = srcForward, SourceUp = srcUp,
                        ListenerForward = listenerForward, ListenerUp = listenerUp,
                        DipoleWeight = rec.Orientation is null ? 0f : rec.DipoleWeight,
                        DipolePower = rec.DipolePower <= 0f ? 1f : rec.DipolePower,
                    };

                    var spatial = sp.Compute(rec.VoiceId, rec.Position, _listenerPosition, ctx);
                    spatialGain = spatial.VolumeAttenuation;
                    _backend.SetVoicePan(rec.VoiceId, spatial.Pan);
                }

                _backend.SetVoiceVolume(rec.VoiceId, rec.Volume * master * spatialGain);
            }
            _backend.Update();
        }
    }

    // -- Internal calls invoked by AudioSource --

    internal void SetPosition(int ticket, Vector3 position)
    {
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return;
            // Honesty over silent surprise: a non-spatial voice can't be promoted to
            // spatial after creation. Backends like SDL3 don't even allocate the
            // dual-stream / channel-map machinery for non-spatial voices, so flipping
            // the flag in the record (as we used to) would have left the backend
            // unable to actually pan / attenuate the voice. Future backends (FMOD,
            // OpenAL) make this distinction even more explicit.
            if (!rec.IsSpatial)
            {
                if (!_warnedSpatialFlip)
                {
                    _warnedSpatialFlip = true;
                    Logger.Warn(
                        "AudioServer: SetPosition() ignored on a non-spatial voice " +
                        $"(ticket {ticket}). Use PlaySpatial / PlaySpatialSound at create " +
                        "time to allocate a 3D voice. This warning is logged once per server.");
                }
                return;
            }
            rec = rec with { Position = position };
            _voices[ticket] = rec;
            if (rec.VoiceId != 0) _backend.SetVoicePosition(rec.VoiceId, position);
        }
    }

    internal void SetVolume(int ticket, float volume)
    {
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return;
            rec = rec with { Volume = volume };
            _voices[ticket] = rec;
            // Push the master-scaled value immediately; Tick() will re-apply on the
            // next frame anyway (folding in the current spatial gain), but doing it
            // here keeps unscaled volume changes audible without a one-frame delay.
            if (rec.VoiceId != 0) _backend.SetVoiceVolume(rec.VoiceId, volume * _masterVolume);
        }
    }

    internal void SetLooping(int ticket, bool looping)
    {
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return;
            _voices[ticket] = rec with { Looping = looping };
            if (rec.VoiceId != 0) _backend.SetVoiceLooping(rec.VoiceId, looping);
        }
    }

    internal void SetPaused(int ticket, bool paused)
    {
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return;
            _voices[ticket] = rec with { Paused = paused };
            if (rec.VoiceId != 0) _backend.SetVoicePaused(rec.VoiceId, paused);
        }
    }

    internal void SetOrientation(int ticket, Quaternion orientation)
    {
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return;
            _voices[ticket] = rec with { Orientation = orientation };
            // Backends generally don't consume orientation directly - the spatial
            // processor folds it into volume on the next Tick().
        }
    }

    internal void SetDirectivity(int ticket, float dipoleWeight, float dipolePower)
    {
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return;
            _voices[ticket] = rec with { DipoleWeight = dipoleWeight, DipolePower = dipolePower };
        }
    }

    internal void SetPlaybackRate(int ticket, float rate)
    {
        if (float.IsNaN(rate) || rate <= 0f) rate = 1f;
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return;
            _voices[ticket] = rec with { PlaybackRate = rate };
            if (rec.VoiceId != 0) _backend.SetVoicePlaybackRate(rec.VoiceId, rate);
        }
    }

    internal void Stop(int ticket)
    {
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return;
            if (rec.VoiceId != 0) _backend.StopVoice(rec.VoiceId);
            _voices.Remove(ticket);
            // Drop any matching pending entry.
            for (int i = _pending.Count - 1; i >= 0; i--)
                if (_pending[i].Ticket == ticket) _pending.RemoveAt(i);
        }
    }

    internal bool IsPlaying(int ticket)
    {
        lock (_lock)
        {
            if (!_voices.TryGetValue(ticket, out var rec)) return false;
            // Pending (voiceId 0) counts as "playing" from the gameplay perspective:
            // the source has been queued and will start as soon as the asset loads.
            if (rec.VoiceId == 0) return true;
            return _backend.IsVoicePlaying(rec.VoiceId);
        }
    }

    /// <summary>Diagnostic: total live tickets (spatial + non-spatial, including pending).</summary>
    public int LiveSourceCount
    {
        get { lock (_lock) return _voices.Count; }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        IAudioBackend? backend;
        ISpatialAudioProcessor? spatial;
        lock (_lock)
        {
            backend = _backend;
            spatial = _spatial;
            _voices.Clear();
            _pending.Clear();
            _backend = new NullAudioBackend();
            _spatial = null;
        }
        if (backend is not NullAudioBackend) backend.Dispose();
        spatial?.Dispose();
    }

    // -- Internal records --

    private sealed record VoiceRecord(
        int VoiceId,
        bool IsSpatial,
        Vector3 Position,
        float Volume,
        bool Looping,
        bool Paused,
        Quaternion? Orientation = null,
        float DipoleWeight = 0f,
        float DipolePower = 1f,
        float PlaybackRate = 1f);

    private readonly record struct PendingVoice(int Ticket, Handle<Sound> Handle, AudioVoiceParams Parameters);
}