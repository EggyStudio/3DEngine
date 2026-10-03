using FluentAssertions;

namespace Engine.Tests.Audio.Sdl;

/// <summary>
/// Tests for the SDL3 audio backend wiring. CI hosts typically lack an audio device,
/// so the backend's "fail-soft" contract is the headline assertion: install on the
/// <see cref="AudioServer"/>, leave <see cref="IAudioBackend.IsInitialized"/> at
/// <c>false</c> when no device is available, and turn every subsequent method call
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
    public void Method_Calls_Are_Safe_When_Backend_Failed_To_Initialise()
    {
        var backend = new SdlAudioBackend();
        backend.Initialize();
        if (backend.IsInitialized) return; // host has audio - the no-op contract isn't exercised

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

    [Fact]
    public void CreateVoice_Issues_Distinct_Ids_When_Backend_Is_Live()
    {
        var backend = new SdlAudioBackend();
        backend.Initialize();
        if (!backend.IsInitialized) return; // skip when host has no audio device

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