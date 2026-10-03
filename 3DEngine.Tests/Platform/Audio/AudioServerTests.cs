using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Audio;

/// <summary>
/// Unit tests for <see cref="AudioServer"/> - focused on the ticket / pending-voice
/// state machine that the gameplay-facing <see cref="AudioSource"/> handle relies on.
/// Uses a stub <see cref="IAudioBackend"/> so we can assert the exact backend call
/// pattern without dragging in a real audio device.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Module", "Engine.Sound")]
public class AudioServerTests
{
    /// <summary>Records every call the server forwards. Issues monotonically increasing voice ids.</summary>
    private sealed class RecordingBackend : IAudioBackend
    {
        public bool IsInitialized { get; private set; }
        public string BackendId => "recording";
        public int NextVoice = 100;
        public List<string> Calls { get; } = new();
        public Vector3 LastListener;
        public readonly Dictionary<int, bool> PlayingByVoice = new();

        public void Initialize() { IsInitialized = true; Calls.Add("Init"); }
        public int CreateVoice(Sound sound, in AudioVoiceParams parameters)
        {
            var v = NextVoice++;
            PlayingByVoice[v] = true;
            Calls.Add($"Create({sound.SourcePath},spatial={parameters.Position is not null},vol={parameters.Volume},loop={parameters.Looping},pause={parameters.Paused},rate={parameters.PlaybackRate})->{v}");
            return v;
        }
        public void StopVoice(int voiceId) { PlayingByVoice[voiceId] = false; Calls.Add($"Stop({voiceId})"); }
        public bool IsVoicePlaying(int voiceId) => PlayingByVoice.TryGetValue(voiceId, out var p) && p;
        public void SetVoicePosition(int voiceId, Vector3 position) => Calls.Add($"Pos({voiceId},{position})");
        public void SetVoiceVolume(int voiceId, float volume) => Calls.Add($"Vol({voiceId},{volume})");
        public void SetVoiceLooping(int voiceId, bool looping) => Calls.Add($"Loop({voiceId},{looping})");
        public void SetVoicePaused(int voiceId, bool paused) => Calls.Add($"Pause({voiceId},{paused})");
        public void SetVoicePan(int voiceId, float pan) => Calls.Add($"Pan({voiceId},{pan})");
        public void SetVoicePlaybackRate(int voiceId, float rate) => Calls.Add($"Rate({voiceId},{rate})");
        public void SetListenerPosition(Vector3 position) { LastListener = position; Calls.Add($"Listener({position})"); }
        public void Update() => Calls.Add("Update");
        public void Dispose() => Calls.Add("Dispose");
    }

    private static Sound MakeSound(string path = "test.wav") => new()
    {
        Samples = new float[] { 0f, 0f, 0f, 0f },
        SampleRate = 44100,
        Channels = 1,
        SourcePath = path,
        SourceFormat = "wav-builtin",
    };

    [Fact]
    public void Default_Backend_Is_Null_And_Listener_Position_Tracks()
    {
        using var server = new AudioServer();
        server.Backend.Should().BeOfType<NullAudioBackend>();
        server.Spatial.Should().BeNull();
        server.LiveSourceCount.Should().Be(0);

        server.ListenerPosition = new Vector3(1, 2, 3);
        server.ListenerPosition.Should().Be(new Vector3(1, 2, 3));
    }

    [Fact]
    public void SetBackend_Initialises_And_Disposes_Previous()
    {
        using var server = new AudioServer();
        var first = new RecordingBackend();
        server.SetBackend(first);
        first.IsInitialized.Should().BeTrue();
        first.Calls.Should().Contain("Init");

        var second = new RecordingBackend();
        server.SetBackend(second);
        first.Calls.Should().Contain("Dispose", "previous backend should be disposed on swap");
        server.Backend.Should().BeSameAs(second);
    }

    [Fact]
    public void Play_Returns_Valid_AudioSource_And_Routes_To_Backend()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);

        var src = server.Play(MakeSound("a.wav"));

        src.IsValid.Should().BeTrue();
        src.IsPlaying.Should().BeTrue();
        backend.Calls.Should().Contain(c => c.StartsWith("Create(a.wav,spatial=False"));
        server.LiveSourceCount.Should().Be(1);
    }

    [Fact]
    public void PlaySpatial_Marks_Voice_Spatial_And_Forwards_Position()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);

        var src = server.PlaySpatial(MakeSound("b.wav"), new Vector3(5, 0, 0));

        src.IsValid.Should().BeTrue();
        backend.Calls.Should().Contain(c => c.StartsWith("Create(b.wav,spatial=True"));

        src.SetPosition(new Vector3(7, 8, 9));
        backend.Calls.Should().Contain(c => c.Contains("Pos(") && c.Contains("<7"));
    }

    [Fact]
    public void Stop_Removes_Voice_And_Forwards_To_Backend()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);

        var src = server.Play(MakeSound());
        src.Stop();

        server.LiveSourceCount.Should().Be(0);
        backend.Calls.Should().Contain(c => c.StartsWith("Stop("));
        src.IsPlaying.Should().BeFalse();
    }

    [Fact]
    public void Default_AudioSource_Is_Inert()
    {
        AudioSource src = default;
        src.IsValid.Should().BeFalse();
        src.IsPlaying.Should().BeFalse();
        // None of these should throw on the default value.
        src.SetPosition(Vector3.Zero);
        src.SetVolume(0.5f);
        src.SetLooping(true);
        src.SetPaused(true);
        src.Stop();
    }

    [Fact]
    public void Pending_Handle_Is_Resolved_When_Asset_Becomes_Available()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);

        var assets = new Assets<Sound>();
        var path = new AssetPath("c.wav");
        // Build a Handle<Sound> via internal ctor (Engine.Tests is InternalsVisibleTo).
        var id = AssetId.Next();
        var handle = new Handle<Sound>(id, path, strong: true);

        // Play first - asset isn't in the registry yet → pending.
        var src = server.PlaySpatial(handle, assets, new Vector3(1, 2, 3));
        src.IsValid.Should().BeTrue("ticket is minted synchronously");
        src.IsPlaying.Should().BeTrue("pending voices count as playing from gameplay");
        backend.Calls.Should().NotContain(c => c.StartsWith("Create("),
            "backend should not see the voice until the asset has loaded");

        // Now drop the asset in and pump.
        assets.Set(id, MakeSound("c.wav"));
        server.ResolvePending(assets);

        backend.Calls.Should().Contain(c => c.StartsWith("Create(c.wav,spatial=True"));

        // Subsequent setters should now hit the backend with the resolved voice id.
        src.SetVolume(0.25f);
        backend.Calls.Should().Contain(c => c.StartsWith("Vol(") && c.EndsWith(",0.25)"));
    }

    [Fact]
    public void Pending_Handle_Picks_Up_Mutations_Made_While_Loading()
    {
        // Regression: setters invoked on the AudioSource handle BEFORE the asset finishes
        // loading must be reflected in the AudioVoiceParams the backend ultimately sees.
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);

        var assets = new Assets<Sound>();
        var path = new AssetPath("d.wav");
        var id = AssetId.Next();
        var handle = new Handle<Sound>(id, path, strong: true);

        // Mint the pending voice with one set of params...
        var src = server.PlaySpatial(handle, assets, new Vector3(0, 0, 0),
            new AudioVoiceParams { Volume = 1f });

        // ...then mutate it before the asset arrives.
        src.SetPosition(new Vector3(11, 22, 33));
        src.SetVolume(0.4f);
        src.SetLooping(true);
        src.SetPaused(true);

        backend.Calls.Should().NotContain(c => c.StartsWith("Create("),
            "no backend voice exists yet, so the mutations only update the AudioServer record");

        // Resolve the asset; the rebuilt params should carry the mutations across.
        assets.Set(id, MakeSound("d.wav"));
        server.ResolvePending(assets);

        backend.Calls.Should().Contain(c =>
            c.StartsWith("Create(d.wav,spatial=True,vol=0.4,loop=True,pause=True"),
            "ResolvePending must rebuild AudioVoiceParams from the live VoiceRecord state");
    }

    [Fact]
    public void Spatial_Processor_Is_Folded_Into_Voice_Volume_Each_Tick()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);
        server.SetSpatialProcessor(new HalfGainProcessor());

        var src = server.PlaySpatial(MakeSound(), new Vector3(10, 0, 0), new AudioVoiceParams { Volume = 1f });

        backend.Calls.Clear();
        server.Tick();

        backend.Calls.Should().Contain(c => c.StartsWith("Vol(") && c.EndsWith(",0.5)"),
            "HalfGainProcessor should halve the per-voice gain via SetVoiceVolume");
        backend.Calls.Should().Contain("Update");
    }

    private sealed class HalfGainProcessor : ISpatialAudioProcessor
    {
        public string ProcessorId => "half";
        public void Initialize() { }
        public SpatialResult Compute(int voiceId, Vector3 sourcePosition, Vector3 listenerPosition, in SpatialContext context)
            => new() { VolumeAttenuation = 0.5f };
        public void Dispose() { }
    }

    [Fact]
    public void MasterVolume_Multiplies_Per_Voice_Output_Each_Tick()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);

        // Two voices: one non-spatial (no spatial processor wired anyway), one spatial.
        server.Play(MakeSound("a.wav"), new AudioVoiceParams { Volume = 1f });
        server.PlaySpatial(MakeSound("b.wav"), new Vector3(5, 0, 0), new AudioVoiceParams { Volume = 0.5f });

        server.MasterVolume = 0.25f;
        backend.Calls.Clear();
        server.Tick();

        // Both voices should have been rewritten with their (Volume × Master) value.
        backend.Calls.Should().Contain(c => c.StartsWith("Vol(100,") && c.EndsWith(",0.25)"),
            "non-spatial voice = 1.0 * MasterVolume(0.25)");
        backend.Calls.Should().Contain(c => c.StartsWith("Vol(101,") && c.EndsWith(",0.125)"),
            "spatial voice = 0.5 * MasterVolume(0.25) (no spatial processor → spatial gain = 1)");
    }

    [Fact]
    public void MasterVolume_Combines_With_Spatial_Processor_Gain()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);
        server.SetSpatialProcessor(new HalfGainProcessor());
        server.MasterVolume = 0.5f;

        server.PlaySpatial(MakeSound(), new Vector3(10, 0, 0), new AudioVoiceParams { Volume = 1f });

        backend.Calls.Clear();
        server.Tick();

        backend.Calls.Should().Contain(c => c.StartsWith("Vol(") && c.EndsWith(",0.25)"),
            "1.0 (Volume) × 0.5 (Master) × 0.5 (HalfGain spatial) = 0.25");
    }

    [Fact]
    public void MasterVolume_Setter_Clamps_Negative_And_NaN_To_Zero()
    {
        using var server = new AudioServer();
        server.MasterVolume = -3f;
        server.MasterVolume.Should().Be(0f);
        server.MasterVolume = float.NaN;
        server.MasterVolume.Should().Be(0f);
        server.MasterVolume = 1.5f;
        server.MasterVolume.Should().Be(1.5f, "values > 1 are allowed (post-amp)");
    }

    [Fact]
    public void SetPlaybackRate_Is_Forwarded_To_Backend_And_Defaults_Apply()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);

        var src = server.Play(MakeSound("p.wav"), new AudioVoiceParams { Volume = 1f, PlaybackRate = 1.5f });
        backend.Calls.Should().Contain(c => c.StartsWith("Create(p.wav,") && c.Contains("rate=1.5"),
            "playback rate must round-trip through CreateVoice");

        src.SetPlaybackRate(0.5f);
        backend.Calls.Should().Contain(c => c.StartsWith("Rate(") && c.EndsWith(",0.5)"));

        // Invalid rate → clamped to 1.0 by the server.
        src.SetPlaybackRate(-2f);
        backend.Calls.Should().Contain(c => c.StartsWith("Rate(") && c.EndsWith(",1)"),
            "negative / NaN rates fall back to native pitch (1.0)");
    }

    [Fact]
    public void SetPosition_On_Non_Spatial_Voice_Is_Ignored()
    {
        using var server = new AudioServer();
        var backend = new RecordingBackend();
        server.SetBackend(backend);

        var src = server.Play(MakeSound("ns.wav")); // non-spatial: parameters.Position == null

        backend.Calls.Clear();
        src.SetPosition(new Vector3(99, 99, 99));

        backend.Calls.Should().NotContain(c => c.StartsWith("Pos("),
            "the AudioServer must refuse to silently promote a 2D voice to spatial - " +
            "the SDL backend (and most others) allocate different machinery up front");
    }
}