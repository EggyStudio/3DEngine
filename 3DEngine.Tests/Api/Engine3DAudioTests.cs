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
        public AudioCallback[] Mixed = [];
        public void SetMixedProcessors(AudioCallback[] processors) => Mixed = processors;
        public void Update() { }
        public void Dispose() { }
    }

    private readonly RecordingBackend _backend = new();
    private readonly TestFolder _folder = new("engine-audio-api-");

    public Engine3DAudioTests()
    {
        var app = new App();
        var audio = new AudioServer();
        audio.SetBackend(_backend);
        app.World.InsertResource(audio);
        UseApp(app);
    }

    // The app shuts down before the directory goes, which closes any music a test left open, as
    // Windows will not delete a file that is still open.
    public void Dispose()
    {
        var app = GetApp();
        UseApp(null);
        app.Shutdown();
        _folder.Dispose();
    }

    // A tenth of a second of 16-bit mono silence at 8 kHz, as a canonical WAV file.
    private int _written;

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
        // A file of its own each time, since one a piece of music has open cannot be written over on Windows.
        var path = Path.Combine(_folder.Path, $"blip{_written++}.wav");
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }

    [Fact]
    public void A_Missing_File_Gives_An_Invalid_Sound()
    {
        IsSoundValid(LoadSound(Path.Combine(_folder.Path, "missing.wav"))).Should().BeFalse();
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
    public void An_Alias_Plays_Over_Its_Source_From_The_Same_Samples()
    {
        var sound = LoadSound(WriteWav());
        var alias = LoadSoundAlias(sound);

        PlaySound(sound);
        PlaySound(alias);

        alias.Samples.Should().BeSameAs(sound.Samples, "an alias copies no samples");
        _backend.Stopped.Should().BeEmpty("the alias's play leaves the source's voice playing");
        IsSoundPlaying(sound).Should().BeTrue();
        IsSoundPlaying(alias).Should().BeTrue();

        UnloadSoundAlias(alias);
        IsSoundPlaying(alias).Should().BeFalse();
        IsSoundPlaying(sound).Should().BeTrue();
    }

    [Fact]
    public void An_Audio_Stream_Plays_What_It_Is_Given_And_Asks_For_More_Once_It_Runs_Low()
    {
        var stream = LoadAudioStream(8000, 32, 2);

        IsAudioStreamProcessed(stream).Should().BeTrue("an empty stream takes samples");
        UpdateAudioStream(stream, new float[5000 * 2]);
        IsAudioStreamPlaying(stream).Should().BeFalse("given samples, it waits to be played");
        var voice = _backend.Streams.Keys.Single();
        _backend.Paused.Should().Contain(voice);
        IsAudioStreamProcessed(stream).Should().BeFalse("5000 frames is past the 4096 it keeps");

        PlayAudioStream(stream);
        IsAudioStreamPlaying(stream).Should().BeTrue();
        _backend.Paused.Should().NotContain(voice);
        _backend.Play(voice, 2000);
        IsAudioStreamProcessed(stream).Should().BeTrue("3000 frames left is under what it keeps");

        UpdateAudioStream(stream, new short[] { 16384, -16384 });
        _backend.Streams[voice][^2..].Should().Equal(0.5f, -0.5f);

        StopAudioStream(stream);
        IsAudioStreamPlaying(stream).Should().BeFalse();
        UnloadAudioStream(stream);
        IsAudioStreamValid(stream).Should().BeFalse();
    }

    [Fact]
    public void A_Stream_With_A_Callback_Is_Topped_Up_At_The_End_Of_A_Frame()
    {
        var stream = LoadAudioStream(8000, 32, 1);
        var calls = 0;
        SetAudioStreamCallback(stream, samples =>
        {
            calls++;
            samples.Fill(0.25f);
        });

        FeedAudioStreams();
        calls.Should().Be(0, "a stream not playing is not fed");

        PlayAudioStream(stream);
        FeedAudioStreams();
        var voice = _backend.Streams.Keys.Single();
        _backend.QueuedVoiceFrames(voice).Should().Be(4096, "one buffer's worth tops it up");
        _backend.Streams[voice].Should().OnlyContain(s => s == 0.25f);

        _backend.Play(voice, 100);
        FeedAudioStreams();
        calls.Should().Be(2);
        UnloadAudioStream(stream);
    }

    [Fact]
    public void A_Wave_Is_Cut_Converted_Written_And_Read_Back()
    {
        var wave = LoadWave(WriteWav());
        IsWaveValid(wave).Should().BeTrue();
        (wave.FrameCount, wave.SampleRate, wave.Channels).Should().Be((800, 8000, 1));

        // A ramp in place of the silence, so the cut and the conversion can be seen.
        var ramp = new Wave { Samples = Enumerable.Range(0, 800).Select(i => i / 1000f).ToArray(), SampleRate = 8000, Channels = 1 };
        var cut = WaveCopy(ramp);
        WaveCrop(ref cut, 100, 300);
        cut.FrameCount.Should().Be(200);
        cut.Samples[0].Should().Be(0.1f);
        ramp.FrameCount.Should().Be(800, "the copy is cut, not the wave it came from");

        WaveFormat(ref cut, 16000, 16, 2);
        (cut.FrameCount, cut.Channels).Should().Be((400, 2));
        cut.Samples[2].Should().Be(cut.Samples[3], "one channel spread to two gives both the same");
        cut.Samples[2].Should().BeApproximately(0.1005f, 1e-4f, "the frame between the first two source frames is halfway between them");

        var file = Path.Combine(_folder.Path, "cut.wav");
        ExportWave(cut, file).Should().BeTrue();
        var back = LoadWave(file);
        (back.FrameCount, back.SampleRate, back.Channels).Should().Be((400, 16000, 2));
        back.Samples[100].Should().BeApproximately(cut.Samples[100], 1f / 32767);

        var sound = LoadSoundFromWave(back);
        IsSoundValid(sound).Should().BeTrue();
        LoadWaveFromMemory(".wav", File.ReadAllBytes(file)).FrameCount.Should().Be(400);
        LoadWaveSamples(back).Should().Equal(back.Samples).And.NotBeSameAs(back.Samples);
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
        UnloadMusicStream(music);
    }

    [Fact]
    public void Music_From_Memory_Streams_As_Music_From_A_File_Does()
    {
        var music = LoadMusicStreamFromMemory(".wav", File.ReadAllBytes(WriteWav()));
        IsMusicValid(music).Should().BeTrue();
        GetMusicTimeLength(music).Should().BeApproximately(0.1f, 1e-3f);

        PlayMusicStream(music);
        _backend.QueuedVoiceFrames(_backend.Streams.Keys.Single()).Should().BeGreaterThanOrEqualTo(4000);
        UnloadMusicStream(music);

        IsMusicValid(LoadMusicStreamFromMemory(".ogg", [1, 2, 3])).Should().BeFalse("bytes that are not Ogg give an empty piece");
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
        UnloadMusicStream(music);
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
        UnloadMusicStream(music);
    }

    [Fact]
    public void Music_Left_Loaded_Is_Closed_When_The_App_Shuts_Down()
    {
        var kept = LoadMusicStream(WriteWav());
        var unloaded = LoadMusicStream(WriteWav());
        UnloadMusicStream(unloaded);
        GetApp().World.Resource<LoadedMusic>().Count.Should().Be(1, "unloading takes a piece of music off the list");

        GetApp().Shutdown();

        kept.Closed.Should().BeTrue("shutting down closes the file of music the program did not unload");
        unloaded.Closed.Should().BeTrue();
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

    [Fact]
    public void A_Streams_Processors_Run_In_Order_Over_What_It_Queues_Until_Detached_And_Musics_Through_Its_Stream()
    {
        var stream = LoadAudioStream(8000, 32, 1);
        AudioCallback twice = samples => { foreach (ref var s in samples) s *= 2; };
        AudioCallback plusOne = samples => { foreach (ref var s in samples) s += 1; };
        AttachAudioStreamProcessor(stream, twice);
        AttachAudioStreamProcessor(stream, plusOne);

        UpdateAudioStream(stream, [1f, 2f]);
        DetachAudioStreamProcessor(stream, twice);
        UpdateAudioStream(stream, [1f]);

        _backend.Streams.Values.Single().Should().Equal([3f, 5f, 2f], "each processor in the order attached, and one detached no more");

        var music = LoadMusicStream(WriteWav());
        AttachAudioStreamProcessor(music.Stream, samples => samples.Fill(0.25f));
        PlayMusicStream(music);
        _backend.Streams.Values.Last().Should().OnlyContain(s => s == 0.25f, "the music's silence passes through its stream's processor");
        UnloadMusicStream(music);
    }

    [Fact]
    public void Mixed_Processors_Reach_The_Backend_In_Order_And_Leave_It_When_Detached()
    {
        AudioCallback first = _ => { }, second = _ => { };

        AttachAudioMixedProcessor(first);
        AttachAudioMixedProcessor(second);
        _backend.Mixed.Should().Equal(first, second);

        DetachAudioMixedProcessor(first);
        _backend.Mixed.Should().Equal(second);
        DetachAudioMixedProcessor(second);
        _backend.Mixed.Should().BeEmpty();
    }
}
