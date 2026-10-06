using FluentAssertions;

namespace Engine.Tests.Audio.Sdl;

/// <summary>
/// Tests for the SDL3 audio backend wiring. CI hosts typically lack an audio device,
/// so the tests assert the backend's fail-soft contract. It installs on the
/// <see cref="AudioServer"/>, leaves <see cref="IAudioBackend.IsInitialized"/> at
/// <c>false</c> when no device is available, and turns every subsequent method call
/// into a no-op so gameplay code never has to null-check the backend.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Backend", "Sdl")]
public class SdlAudioBackendTests
{
    [Fact]
    public void BackendId_Is_Stable()
    {
        var backend = new SdlAudioBackend();
        backend.BackendId.Should().Be("sdl3");
    }

    [Fact]
    public void Initialize_Is_Idempotent_And_NeverThrows()
    {
        var backend = new SdlAudioBackend();
        // Whether the host has an audio device or not, Initialize must never throw.
        var act = () => { backend.Initialize(); backend.Initialize(); };
        act.Should().NotThrow();
        backend.Dispose();
    }

    [Fact]
    public void A_Machine_With_No_Audio_Device_Is_Warned_Of_Once_A_Process()
    {
        // A message of its own, since the probe for a device and other tests open audio in this process too.
        var message = $"SdlAudioBackend: SDL_OpenAudioDevice failed: 'no device {Guid.NewGuid():N}', so the backend is disabled.";
        var heard = new Heard();
        var logger = Log.Factory.CreateLogger("Engine.Sound.Sdl").UseProvider(heard);
        try
        {
            SdlAudioBackend.WarnOnce(message);
            SdlAudioBackend.WarnOnce(message);
        }
        finally
        {
            logger.RemoveProvider(heard);
        }

        heard.Lines.Where(line => line.Message.StartsWith(message, StringComparison.Ordinal)).Select(line => line.Level)
            .Should().Equal([LogLevel.Warning, LogLevel.Info], "every app a machine with no audio device opens fails the same way, which a log says once");
    }

    // Hears what one category logs, from any thread, for as long as it is added.
    private sealed class Heard : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Message)> Lines { get; } = new();

        public void Log(LogLevel level, string category, string message, Exception? exception = null) => Lines.Enqueue((level, message));
    }

    // Opens a backend as on a machine with no audio device, SDL's audio driver named as one that is
    // not there, which no other test's backend has open while these run one at a time.
    private static SdlAudioBackend WithNoDevice(bool fallBackToDummy)
    {
        SDL3.SDL.SetHintWithPriority(SDL3.SDL.Hints.AudioDriver, "no-such-driver", SDL3.SDL.HintPriority.Override);
        try
        {
            var backend = new SdlAudioBackend { FallBackToDummy = fallBackToDummy };
            backend.Initialize();
            return backend;
        }
        finally
        {
            SDL3.SDL.ResetHint(SDL3.SDL.Hints.AudioDriver);
        }
    }

    [Fact]
    public void A_Machine_With_No_Audio_Device_Plays_To_The_Dummy_Driver_Where_A_Stream_Moves_On()
    {
        using var backend = WithNoDevice(fallBackToDummy: true);
        backend.IsInitialized.Should().BeTrue("where no device opens, sound goes to SDL's dummy driver, as raylib's to miniaudio's null device");
        backend.Driver.Should().Be("dummy");

        // Half a second queued, which the dummy driver takes at the rate it would play.
        var voice = backend.CreateStreamVoice(2, 48000, new AudioVoiceParams { Volume = 1, PlaybackRate = 1 });
        voice.Should().NotBe(0);
        backend.QueueVoiceSamples(voice, new float[48000]);
        var queued = backend.QueuedVoiceFrames(voice);
        for (int wait = 0; wait < 100 && backend.QueuedVoiceFrames(voice) >= queued; wait++) Thread.Sleep(20);

        backend.QueuedVoiceFrames(voice).Should().BeLessThan(queued, "the stream's position moves on with no device, so music and streams go on as they do with one");
    }

    [Fact]
    public void Method_Calls_Are_Safe_When_Backend_Failed_To_Initialise()
    {
        var backend = WithNoDevice(fallBackToDummy: false);
        backend.IsInitialized.Should().BeFalse();

        var sound = new Sound { Samples = new float[1024], SampleRate = 44100, Channels = 1 };
        backend.CreateVoice(sound, default).Should().Be(0);
        backend.IsVoicePlaying(0).Should().BeFalse();
        backend.SetListenerPosition(default);
        backend.SetVoicePosition(0, default);
        backend.SetVoiceVolume(0, 0.5f);
        backend.SetVoiceLooping(0, true);
        backend.SetVoicePaused(0, false);
        backend.StopVoice(0);
        backend.Update();
        backend.Dispose();
    }

    [NeedsAudioDeviceFact]
    public void CreateVoice_Issues_Distinct_Ids_When_Backend_Is_Live()
    {
        var backend = new SdlAudioBackend();
        backend.Initialize();
        backend.IsInitialized.Should().BeTrue();

        var sound = new Sound
        {
            Samples = new float[44100], // ~0.5s of silence at 44.1k mono
            SampleRate = 44100,
            Channels = 1,
            SourcePath = "tests/silence.wav",
        };

        int a = backend.CreateVoice(sound, new AudioVoiceParams { Volume = 1f });
        int b = backend.CreateVoice(sound, new AudioVoiceParams { Volume = 0.5f });

        a.Should().NotBe(0);
        b.Should().NotBe(0);
        a.Should().NotBe(b);

        backend.IsVoicePlaying(a).Should().BeTrue();
        backend.SetVoiceVolume(a, 0.25f);
        backend.SetVoicePaused(a, true);
        backend.SetVoicePaused(a, false);
        backend.StopVoice(a);
        backend.StopVoice(b);

        backend.Dispose();
    }

    [Fact]
    public void SdlAudioPlugin_Replaces_Backend_On_AudioServer()
    {
        using var server = new AudioServer();
        var beforeBackendId = server.Backend.BackendId;

        // Mirror what SdlAudioPlugin.Build does without spinning up a full App.
        server.SetBackend(new SdlAudioBackend());

        beforeBackendId.Should().Be("null");
        server.Backend.BackendId.Should().Be("sdl3");
    }
}