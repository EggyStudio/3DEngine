using System.Numerics;
using FluentAssertions;

namespace Engine.Tests.Platform.Audio;

/// <summary>
/// A sound placed by its position, played through SDL's dummy driver as a run with no window plays,
/// and read in the mix the device plays, heard from the listener's side it is on and silent past its
/// far distance.
/// </summary>
/// <remarks>
/// SDL's audio is the process's, and its driver is a hint the backend's own tests set for the whole
/// process, so the tests that open it run in a collection of their own, one at a time. Run beside
/// those, an app's backend opened and closed under another's ended the test host in the C heap.
/// </remarks>
[Collection("Sdl audio")]
[Trait("Category", "Integration")]
public sealed class SpatialSoundTests
{
    // A second of a tone at half the full scale, one channel at the rate the dummy driver plays.
    private static readonly Sound Tone = new()
    {
        Samples = [.. Enumerable.Range(0, 48000).Select(i => 0.5f * MathF.Sin(MathF.Tau * 440 * i / 48000))],
        SampleRate = 48000,
        Channels = 1,
    };

    // The energy of each side of the mix, over the frames the device has played, which the test
    // waits on by their count rather than by the clock, the dummy driver taking them at the rate
    // they play.
    private sealed class Ears
    {
        private readonly object _gate = new();
        private readonly ManualResetEventSlim _reached = new(false);
        private long _frames, _target = long.MaxValue;
        private double _left, _right;

        public void Hear(Span<float> samples)
        {
            double left = 0, right = 0;
            for (var i = 0; i + 1 < samples.Length; i += 2) (left, right) = (left + samples[i] * samples[i], right + samples[i + 1] * samples[i + 1]);
            lock (_gate)
            {
                (_left, _right, _frames) = (_left + left, _right + right, _frames + samples.Length / 2);
                if (_frames >= _target) _reached.Set();
            }
        }

        // Waits for the device to play this many frames more, a minute at most.
        public void Play(int frames)
        {
            lock (_gate)
            {
                _reached.Reset();
                _target = _frames + frames;
            }
            _reached.Wait(TimeSpan.FromMinutes(1)).Should().BeTrue("the dummy driver plays the frames asked for");
        }

        public void Clear()
        {
            lock (_gate) (_left, _right) = (0, 0);
        }

        public (double Left, double Right) Sides
        {
            get { lock (_gate) return (_left, _right); }
        }
    }

    // The sides of the mix while a looping tone plays at a position from the listener at the
    // origin facing -Z, read over three tenths of a second once a tick of the server has worked out
    // its pan and its volume and the samples queued before that, a sixth of a second, have played.
    private static (double Left, double Right) Heard(Vector3 position)
    {
        using var app = new App(Config.Default with { Headless = true }).AddPlugin(new SoundsPlugin());
        var server = app.World.Resource<AudioServer>();
        var ears = new Ears();
        server.SetMixedProcessors([ears.Hear]);
        var source = server.Play(Tone, new AudioVoiceParams { Position = position, Looping = true, Volume = 1 });
        server.Tick();
        ears.Play(8000);
        ears.Clear();
        ears.Play(14400);
        source.Stop();
        return ears.Sides;
    }

    [NeedsAudioDeviceFact]
    public void A_Sound_To_The_Listeners_Left_Is_Heard_On_The_Left_Of_The_Mix()
    {
        var (left, right) = Heard(new Vector3(-3, 0, 0));

        left.Should().BeGreaterThan(1, "the tone is heard");
        right.Should().BeLessThan(left / 100, "a sound straight to the listener's left is panned to the left alone");
    }

    [NeedsAudioDeviceFact]
    public void A_Sound_At_Twice_Its_Far_Distance_Is_Not_Heard()
    {
        var (nearLeft, nearRight) = Heard(new Vector3(0, 0, -0.5f));
        var (farLeft, farRight) = Heard(new Vector3(0, 0, -2 * AudioServer.DefaultFarDistance));

        (nearLeft + nearRight).Should().BeGreaterThan(1, "within its near distance the tone is heard at its full volume");
        (farLeft + farRight).Should().BeLessThan((nearLeft + nearRight) / 1000, "past its far distance it is silent");
    }
}

/// <summary>The tests that open SDL's audio, which run one at a time and beside no other.</summary>
[CollectionDefinition("Sdl audio", DisableParallelization = true)]
public sealed class SdlAudioCollection;
