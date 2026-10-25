using FluentAssertions;

namespace Engine.Tests.Platform.Audio.Sdl;

/// <summary>
/// A voice's low-pass and the room's reverb, heard in the samples SDL's dummy driver mixes, as a
/// run that shows no window plays them, read through a mixed processor.
/// </summary>
[Collection("Sdl audio")]
[Trait("Category", "Integration")]
[Trait("Backend", "Sdl")]
public class VoiceEffectsTests
{
    private const int Rate = 48000;

    // An app that plays to the dummy driver at 48 kHz, which a sound at that rate reaches unresampled.
    private static App Silent() => new App(Config.Default with { Hidden = true }).AddPlugin(new SoundsPlugin());

    // The left channel of what the device mixes from a moment before a voice starts until it ends,
    // and a tenth of a second more.
    private static float[] Heard(AudioServer server, Func<AudioSource> play)
    {
        var heard = new List<float>();
        server.SetMixedProcessors([samples =>
        {
            lock (heard)
                for (int i = 0; i < samples.Length; i += 2) heard.Add(samples[i]);
        }]);
        Thread.Sleep(50);
        var source = play();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (source.IsPlaying && clock.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(5);
        Thread.Sleep(100);
        server.SetMixedProcessors([]);
        lock (heard) return [.. heard];
    }

    // The power of one frequency in the samples, by Goertzel's recurrence.
    private static double Power(float[] samples, double frequency)
    {
        var coefficient = 2 * Math.Cos(2 * Math.PI * frequency / Rate);
        double s1 = 0, s2 = 0;
        foreach (var x in samples) (s2, s1) = (s1, x + coefficient * s1 - s2);
        return s1 * s1 + s2 * s2 - coefficient * s1 * s2;
    }

    private static double Decibels(double powerRatio) => 10 * Math.Log10(powerRatio);

    [NeedsAudioDeviceFact]
    public void A_Low_Pass_Takes_A_Tone_Above_Its_Cutoff_Down_And_Leaves_One_Below_It()
    {
        using var app = Silent();
        var server = app.World.Resource<AudioServer>();
        // Half a second of 300 Hz and 6 kHz at once.
        var samples = new float[Rate / 2];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = 0.25f * (MathF.Sin(2 * MathF.PI * 300 * i / Rate) + MathF.Sin(2 * MathF.PI * 6000 * i / Rate));
        var sound = new Sound { Samples = samples, SampleRate = Rate, Channels = 1 };
        (double Low, double High) Tones(float cutoff)
        {
            var heard = Heard(server, () => server.Play(sound, AudioVoiceParams.Default with { LowPass = cutoff }));
            return (Power(heard, 300), Power(heard, 6000));
        }

        var open = Tones(0);
        var muffled = Tones(1000);
        open.High.Should().BeGreaterThan(open.Low / 4, "unmuffled, both tones are heard");
        Decibels(muffled.High / open.High).Should().BeLessThan(-20, "a tone two and a half octaves past the cutoff falls by twelve decibels an octave");
        Decibels(muffled.Low / open.Low).Should().BeInRange(-1.5, 0.5, "a tone well under the cutoff passes");
    }

    [NeedsAudioDeviceFact]
    public void A_Clicks_Reverb_Tail_Lasts_As_Long_As_The_Rooms_Decay_And_The_Voice_Plays_Until_It_Dies()
    {
        using var app = Silent();
        var server = app.World.Resource<AudioServer>();
        // A millisecond's click.
        var sound = new Sound { Samples = Enumerable.Repeat(0.8f, Rate / 1000).ToArray(), SampleRate = Rate, Channels = 1 };
        // The energy a quarter of a second after the click begins, over a fifth of a second, and
        // how long the voice played.
        (double Tail, double Seconds) Click(float share, float decay)
        {
            server.ReverbDecay = decay;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var heard = Heard(server, () => server.Play(sound, AudioVoiceParams.Default with { Reverb = share }));
            var seconds = clock.Elapsed.TotalSeconds;
            var onset = Array.FindIndex(heard, x => Math.Abs(x) > 0.05f);
            onset.Should().BeGreaterThanOrEqualTo(0, "the click is heard");
            var tail = heard.Skip(onset + Rate / 4).Take(Rate / 5).Sum(x => (double)x * x);
            return (tail, seconds);
        }

        var dry = Click(0, 1.2f);
        var shortRoom = Click(1, 0.3f);
        var longRoom = Click(1, 1.2f);
        dry.Tail.Should().BeLessThan(1e-8, "a voice with no share sounds no room");
        longRoom.Tail.Should().BeGreaterThan(1e-4, "a room of a second and a fifth still rings a quarter of a second on");
        Decibels(longRoom.Tail / shortRoom.Tail).Should().BeGreaterThan(30, "a room of three tenths of a second has fallen by sixty decibels nearly as far on");
        longRoom.Seconds.Should().BeGreaterThan(1.2, "the voice plays until its room's tail has died").And.BeLessThan(5);
        dry.Seconds.Should().BeLessThan(1, "a voice with no share ends with its samples");
    }
}
