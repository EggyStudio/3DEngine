using System.Numerics;
using Engine;
using static Engine.E3D;

namespace FeatureTest;

/// <summary>
/// The audio, a drone played at a speaker to the left of the zone's start and heard from where the
/// camera is, a coin played every two seconds panned by where the player stands, music streamed
/// from a file, a stream whose samples a callback of the program's own makes, and a wave written to
/// a file and read back.
/// </summary>
/// <remarks>
/// <para>
/// The sounds play while the player is in the zone, and stop as the player leaves. For its first two
/// seconds there the drone plays alone, and a processor over the mix the device plays sums its
/// samples from the second half second on, the left channel's and the right's apart, so the drone
/// being heard from its side is read in the mix, louder on the left with the speaker on the left, as
/// the camera sees it from the zone's start. The music, the stream and the coin start after, since
/// the music's own channels are not alike. The drone is played at the speaker's place through the
/// audio server, with the listener set where the camera is and looking where it looks, so its pan
/// and its fall with distance are the engine's spatial processor's.
/// </para>
/// <para>
/// The wave is a half second of a tone at half the full scale, written with
/// <see cref="ExportWave"/> beside the program and read back with <see cref="LoadWave"/>, its
/// frames counted and its peak measured. The audio is ok when the wave comes back as it went, the
/// music has played over a second, the stream has been asked for samples, and the mix is louder on
/// the side the speaker stands.
/// </para>
/// <para>
/// The music is fed from the frame, so a machine whose frames come slowly feeds less of it in as
/// many frames. The status says the sounds are not heard yet until all four hold, or until
/// <see cref="Patience"/> frames after the drone's time alone have gone, after which it says what
/// fell short, so a run reads it until it is ok or says so.
/// </para>
/// </remarks>
sealed class Audio : Zone
{
    // The frames the drone plays alone on arriving, before the rest start, and the frames after
    // those the status waits for the music's second and the rest before it says what fell short.
    private const int Rate = 48000, Tone = Rate / 2, Alone = 120, Patience = 1200;
    private readonly Model _speaker = LoadModelFromMesh(GenMeshCube(1, 1, 1));
    private readonly Sound _coin = LoadSound("resources/coin.wav");
    private readonly Sound _drone = LoadSound("resources/drone.ogg");
    private readonly Music _music = LoadMusicStream("resources/drone.ogg");
    private readonly AudioStream _stream = LoadAudioStream(Rate, 32, 1);
    private readonly AudioServer? _server = GetApp().World.TryGetResource<AudioServer>(out var server) ? server : null;
    private readonly Toggle _sounding = new("sounds", "The zone's sounds play while you are here");
    private readonly object _gate = new();
    private AudioSource _source = AudioSource.Invalid;
    private double _left, _right, _mixLeft, _mixRight;
    private int _channels = 2, _frames, _coins, _asked;
    private float _phase, _clock, _played;
    private string _wave = "the wave not written yet";
    private bool _waveOk;

    public Audio()
    {
        SetSoundVolume(_coin, 0.6f);
        SetMusicVolume(_music, 0.3f);
        SetAudioStreamVolume(_stream, 0.2f);
        _channels = Math.Max(1, GetAudioDeviceChannels());
        SetAudioStreamCallback(_stream, Sine);
        AttachAudioMixedProcessor(Mix);
        Wave();
    }

    public override string Name => "Audio";
    public override Vector3 Center => Zones.At(225);
    public override Vector3 Start => Place(0, -8);
    public override IReadOnlyList<Toggle> Toggles => [_sounding];

    // Where the speaker stands, to the left of the start as the camera there looks in.
    private Vector3 Speaker => Place(-6, -2, 1);

    // A tone of 440 hertz at a fifth of the full scale, from the program's own callback.
    private void Sine(Span<float> samples)
    {
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.2f * MathF.Sin(_phase);
            _phase += MathF.Tau * 440 / Rate;
            if (_phase > MathF.Tau) _phase -= MathF.Tau;
        }
        _asked++;
    }

    // Each channel's energy in the mix the device plays, on the audio thread.
    private void Mix(Span<float> samples)
    {
        double left = 0, right = 0;
        for (var i = 0; i + _channels - 1 < samples.Length; i += _channels)
        {
            left += samples[i] * samples[i];
            right += _channels > 1 ? samples[i + 1] * samples[i + 1] : samples[i] * samples[i];
        }
        lock (_gate)
        {
            _left += left;
            _right += right;
        }
    }

    // A half second of a tone written beside the program and read back.
    private void Wave()
    {
        var samples = new float[Tone];
        for (var i = 0; i < Tone; i++) samples[i] = 0.5f * MathF.Sin(MathF.Tau * 440 * i / Rate);
        var file = Path.Combine(AppContext.BaseDirectory, "feature-tone.wav");
        if (!ExportWave(new Wave { Samples = samples, SampleRate = Rate, Channels = 1 }, file))
        {
            _wave = "the wave could not be written";
            return;
        }
        var read = LoadWave(file);
        if (!IsWaveValid(read))
        {
            _wave = "the wave written could not be read back";
            return;
        }
        var peak = read.Samples.Max(MathF.Abs);
        _waveOk = read.FrameCount == Tone && MathF.Abs(peak - 0.5f) < 0.01f;
        _wave = $"the wave written and read back as {read.FrameCount:N0} frames peaking at {peak:0.000}";
        UnloadWave(read);
    }

    public override void Update(float dt, Player player)
    {
        var here = Zones.Around(player.Feet) == this && _sounding.On;
        if (here && _frames == 0) Begin();
        else if (!here && _frames > 0) End();
        _frames = here ? _frames + 1 : 0;

        // The listener where the camera is, looking where it looks, which the drone is heard from.
        if (_server is not null)
        {
            var camera = player.Camera;
            _server.ListenerPosition = camera.Position;
            _server.ListenerOrientation = Quaternion.CreateFromRotationMatrix(
                Matrix4x4.CreateWorld(Vector3.Zero, Vector3.Normalize(camera.Target - camera.Position), Vector3.UnitY));
        }
        if (!here) return;

        if (_frames == 30) lock (_gate) (_left, _right) = (0, 0);
        if (_frames == Alone)
        {
            lock (_gate) (_mixLeft, _mixRight) = (Math.Sqrt(_left), Math.Sqrt(_right));
            PlayMusicStream(_music);
            PlayAudioStream(_stream);
        }
        if (_frames < Alone) return;
        UpdateMusicStream(_music);
        _played = GetMusicTimePlayed(_music);
        _clock += dt;
        if (_clock >= 2)
        {
            _clock = 0;
            // Panned toward the side of the player the hub's middle is on, and quieter further off.
            var toward = Vector3.Normalize(Center - player.Feet with { Y = 0 } + new Vector3(0.001f, 0, 0));
            var right = new Vector3(-Ahead(player.Yaw).Z, 0, Ahead(player.Yaw).X);
            SetSoundPan(_coin, 0.5f - 0.5f * Vector3.Dot(toward, right));
            PlaySound(_coin);
            _coins++;
        }
    }

    private void Begin()
    {
        if (_server is not null)
            _source = _server.Play(_drone, new AudioVoiceParams { Position = Speaker, Looping = true, Volume = 1, NearDistance = 6, FarDistance = 40 });
        (_clock, _coins, _asked, _played) = (0, 0, 0, 0);
    }

    private void End()
    {
        StopMusicStream(_music);
        StopAudioStream(_stream);
        if (_source.IsValid) _source.Stop();
        _source = AudioSource.Invalid;
    }

    public override void Draw(Camera3D camera)
    {
        Box(_speaker, Speaker, new Vector3(0.8f, 1.2f, 0.8f), new Color(60, 60, 70));
        DrawSphere(Speaker + new Vector3(0, 0.75f, 0), 0.25f, _source.IsValid ? new Color(240, 200, 60) : new Color(90, 90, 90));
    }

    public override string Status()
    {
        var device = IsAudioDeviceReady() ? $"the device at {GetAudioDeviceSampleRate():N0} hertz in {GetAudioDeviceChannels()} channels" : "no audio device";
        if (_frames < Alone + 60) return $"{device}, {_wave}, the sounds not heard yet, which waits for you to be here";
        var sided = _mixLeft > _mixRight * 1.2;
        var ok = _waveOk && _played > 1 && _asked > 0 && sided;
        if (!ok && _frames < Alone + Patience)
            return $"{device}, {_wave}, the sounds not heard yet, the music {_played:0.0} s played of the second it is to play";
        var missing = new List<string>();
        if (!_waveOk) missing.Add("the wave not read back as written");
        if (_played <= 1) missing.Add("the music short of a second");
        if (_asked == 0) missing.Add("the stream never asked for samples");
        if (!sided) missing.Add("not heard from its side");
        return $"{device}, {_wave}, the music {_played:0.0} s played, the stream asked for samples {_asked} times, the coin played {_coins} times, "
               + $"the drone at the speaker, the mix's left {_mixLeft:0.00} and right {_mixRight:0.00} with it alone"
               + (ok ? ", audio ok" : $", {string.Join(", ", missing)} after {Patience} frames");
    }
}
