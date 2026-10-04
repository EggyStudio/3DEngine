using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// The flat API's sound and music functions, run against an app with a backend that records
/// what it is asked to do. The flat API holds one app in a static field, so these tests do not
/// run beside other tests that set it.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class Engine3DAudioTests : IDisposable
{
    private sealed class RecordingBackend : IAudioBackend
    {
        private int _next = 1;
        public readonly Dictionary<int, AudioVoiceParams> Voices = [];
        public readonly List<int> Stopped = [];
        public readonly Dictionary<int, float> Volumes = [];
        public readonly Dictionary<int, float> Rates = [];
        public readonly HashSet<int> Paused = [];

        public bool IsInitialized => true;
        public string BackendId => "recording";
        public void Initialize() { }
        public int CreateVoice(Sound sound, in AudioVoiceParams parameters)
        {
            Voices[_next] = parameters;
            return _next++;
        }
        public void StopVoice(int voiceId) { Stopped.Add(voiceId); Voices.Remove(voiceId); }
        public bool IsVoicePlaying(int voiceId) => Voices.ContainsKey(voiceId) && !Paused.Contains(voiceId);
        public void SetVoicePosition(int voiceId, Vector3 position) { }
        public void SetVoiceVolume(int voiceId, float volume) => Volumes[voiceId] = volume;
        public void SetVoiceLooping(int voiceId, bool looping) { }
        public void SetVoicePaused(int voiceId, bool paused) { if (paused) Paused.Add(voiceId); else Paused.Remove(voiceId); }
        public void SetListenerPosition(Vector3 position) { }
        public void SetVoicePlaybackRate(int voiceId, float rate) => Rates[voiceId] = rate;
        public readonly Dictionary<int, float> Pans = [];
        public void SetVoicePan(int voiceId, float pan) => Pans[voiceId] = pan;

        // Stream voices keep what is queued, and Play stands in for the device taking it.
        public readonly Dictionary<int, List<float>> Streams = [];
        public readonly Dictionary<int, int> StreamChannels = [];
        public int CreateStreamVoice(int channels, int sampleRate, in AudioVoiceParams parameters)
        {
            Voices[_next] = parameters;
            Streams[_next] = [];
            StreamChannels[_next] = channels;
            return _next++;
        }
        public void QueueVoiceSamples(int voiceId, ReadOnlySpan<float> samples) => Streams[voiceId].AddRange(samples);
        public long QueuedVoiceFrames(int voiceId) => Streams.TryGetValue(voiceId, out var queued) && Voices.ContainsKey(voiceId) ? queued.Count / StreamChannels[voiceId] : 0;
        public void Play(int voiceId, int frames) => Streams[voiceId].RemoveRange(0, Math.Min(Streams[voiceId].Count, frames * StreamChannels[voiceId]));
        public void Update() { }
        public void Dispose() { }
    }

    private readonly RecordingBackend _backend = new();
    private readonly string _directory = Directory.CreateTempSubdirectory("engine-audio-api-").FullName;

    public Engine3DAudioTests()
    {
        var app = new App();
        var audio = new AudioServer();
        audio.SetBackend(_backend);
        app.World.InsertResource(audio);
        UseApp(app);
    }

    public void Dispose()
    {
        UseApp(null);
        Directory.Delete(_directory, recursive: true);
    }

    // A tenth of a second of 16-bit mono silence at 8 kHz, as a canonical WAV file.
    private string WriteWav()
    {
        const int sampleRate = 8000, frames = 800;
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            w.Write("RIFF"u8); w.Write(36 + frames * 2); w.Write("WAVE"u8);
            w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(sampleRate); w.Write(sampleRate * 2); w.Write((short)2); w.Write((short)16);
            w.Write("data"u8); w.Write(frames * 2); w.Write(new byte[frames * 2]);
        }
        var path = Path.Combine(_directory, "blip.wav");
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }

    [Fact]
    public void A_Missing_File_Gives_An_Invalid_Sound()
    {
        IsSoundValid(LoadSound(Path.Combine(_directory, "missing.wav"))).Should().BeFalse();
    }

    [Fact]
    public void PlaySound_Restarts_The_Sound_On_A_New_Voice()
    {
        var sound = LoadSound(WriteWav());

        PlaySound(sound);
        PlaySound(sound);

        _backend.Stopped.Should().Equal(1);
        _backend.Voices.Keys.Should().Equal(2);
        IsSoundPlaying(sound).Should().BeTrue();
    }

    [Fact]
    public void Stop_Pause_And_Resume_Reach_The_Voice()
    {
        var sound = LoadSound(WriteWav());
        PlaySound(sound);

        PauseSound(sound);
        IsSoundPlaying(sound).Should().BeFalse();
        ResumeSound(sound);
        IsSoundPlaying(sound).Should().BeTrue();
        StopSound(sound);
        IsSoundPlaying(sound).Should().BeFalse();
    }

    [Fact]
    public void Volume_And_Pitch_Apply_Now_And_To_The_Next_Play()
    {
        var sound = LoadSound(WriteWav());
        PlaySound(sound);

        SetSoundVolume(sound, 0.25f);
        SetSoundPitch(sound, 1.5f);
        PlaySound(sound);

        _backend.Volumes[1].Should().Be(0.25f);
        _backend.Voices[2].Volume.Should().Be(0.25f);
        _backend.Rates[2].Should().Be(1.5f);
    }

    [Fact]
    public void Looping_Music_Keeps_Half_A_Second_Queued_On_A_Stream_Voice()
    {
        var music = LoadMusicStream(WriteWav());

        PlayMusicStream(music);

        var voice = _backend.Streams.Keys.Should().ContainSingle().Subject;
        // A tenth of a second at 8 kHz, read round and round until half a second is queued.
        _backend.QueuedVoiceFrames(voice).Should().BeGreaterThanOrEqualTo(4000);
        IsMusicStreamPlaying(music).Should().BeTrue();
        GetMusicTimeLength(music).Should().BeApproximately(0.1f, 1e-3f);

        _backend.Play(voice, 4000);
        GetMusicTimePlayed(music).Should().BeApproximately(0f, 1e-3f, "4000 frames is five whole rounds of 800");
        UpdateMusicStream(music);
        _backend.QueuedVoiceFrames(voice).Should().BeGreaterThanOrEqualTo(4000);
    }

    [Fact]
    public void Music_That_Does_Not_Loop_Stops_Playing_Once_Its_Queue_Runs_Out()
    {
        var music = LoadMusicStream(WriteWav());
        music.Looping = false;

        PlayMusicStream(music);
        var voice = _backend.Streams.Keys.Single();
        _backend.QueuedVoiceFrames(voice).Should().Be(800);
        IsMusicStreamPlaying(music).Should().BeTrue();

        _backend.Play(voice, 200);
        GetMusicTimePlayed(music).Should().BeApproximately(0.025f, 1e-4f);

        _backend.Play(voice, 600);
        UpdateMusicStream(music);
        IsMusicStreamPlaying(music).Should().BeFalse();
    }

    [Fact]
    public void Seeking_Restarts_The_Voice_At_The_Time_Asked_And_Keeps_A_Pause()
    {
        var music = LoadMusicStream(WriteWav());
        music.Looping = false;
        PlayMusicStream(music);
        PauseMusicStream(music);

        SeekMusicStream(music, 0.05f);

        var voice = _backend.Streams.Keys.Max();
        _backend.Stopped.Should().ContainSingle();
        _backend.QueuedVoiceFrames(voice).Should().Be(400);
        GetMusicTimePlayed(music).Should().BeApproximately(0.05f, 1e-4f);
        _backend.Paused.Should().Contain(voice);
        IsMusicStreamPlaying(music).Should().BeFalse();

        ResumeMusicStream(music);
        IsMusicStreamPlaying(music).Should().BeTrue();
    }

    [Fact]
    public void The_Device_Is_Ready_With_A_Backend_That_Makes_Sound()
    {
        IsAudioDeviceReady().Should().BeTrue();
        SetMasterVolume(0.5f);
        GetMasterVolume().Should().Be(0.5f);
    }

    [Fact]
    public void A_Sounds_Pan_Is_Raylibs_Half_For_The_Middle_On_A_Voice_Ready_To_Pan()
    {
        var sound = LoadSound(WriteWav());
        SetSoundPan(sound, 0);
        PlaySound(sound);

        var voice = _backend.Voices.Keys.Single();
        _backend.Voices[voice].Pannable.Should().BeTrue("a sound can be panned while it plays");
        _backend.Voices[voice].Pan.Should().Be(-1, "0 is full left");

        SetSoundPan(sound, 0.75f);
        _backend.Pans[voice].Should().Be(0.5f, "0.75 is halfway from the middle to the right");
    }

    [Fact]
    public void Music_Pans_As_A_Sound_Does()
    {
        var music = LoadMusicStream(WriteWav());
        SetMusicPan(music, 1);
        PlayMusicStream(music);

        var voice = _backend.Voices.Keys.Single();
        _backend.Voices[voice].Pannable.Should().BeTrue();
        _backend.Voices[voice].Pan.Should().Be(1, "1 is full right");

        SetMusicPan(music, 0.25f);
        _backend.Pans[voice].Should().Be(-0.5f);
        UnloadMusicStream(music);
    }
}
