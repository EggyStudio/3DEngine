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
    public void Music_Plays_On_A_Looping_Voice()
    {
        var music = LoadMusicStream(WriteWav());

        PlayMusicStream(music);

        _backend.Voices.Values.Should().ContainSingle().Which.Looping.Should().BeTrue();
        IsMusicStreamPlaying(music).Should().BeTrue();
        GetMusicTimeLength(music).Should().BeApproximately(0.1f, 1e-3f);
    }

    [Fact]
    public void The_Device_Is_Ready_With_A_Backend_That_Makes_Sound()
    {
        IsAudioDeviceReady().Should().BeTrue();
        SetMasterVolume(0.5f);
        GetMasterVolume().Should().Be(0.5f);
    }
}
