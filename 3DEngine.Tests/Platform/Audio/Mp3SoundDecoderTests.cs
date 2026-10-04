using FluentAssertions;

namespace Engine.Tests.Platform.Audio;

[Trait("Category", "Unit")]
public class Mp3SoundDecoderTests
{
    // Half a second of a 440 Hz tone, stereo at 22050 Hz, encoded by ffmpeg's libmp3lame.
    private static string TonePath => Path.Combine(AppContext.BaseDirectory, "Platform", "Audio", "tone.mp3");

    [Fact]
    public void An_Mp3_File_Decodes_To_Interleaved_Samples()
    {
        var sound = Mp3SoundDecoder.Decode(File.ReadAllBytes(TonePath), "tone.mp3");

        sound.Channels.Should().Be(2);
        sound.SampleRate.Should().Be(22050);
        sound.DurationSeconds.Should().BeApproximately(0.5, 0.1, "the encoder pads a little at either end");
        sound.Samples.Should().Contain(s => Math.Abs(s) > 0.05f, "a tone at ffmpeg's default amplitude of an eighth is not silence");
    }

    [Fact]
    public void Bytes_That_Are_Not_Mp3_Are_Refused()
    {
        var act = () => Mp3SoundDecoder.Decode([1, 2, 3, 4, 5, 6, 7, 8], "noise.mp3");

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Music_Streams_From_An_Mp3_File_And_Seeks()
    {
        using var music = new Mp3MusicDecoder(File.OpenRead(TonePath));
        (music.Channels, music.SampleRate).Should().Be((2, 22050));
        (music.TotalFrames / 22050.0).Should().BeApproximately(0.5, 0.1);

        var buffer = new float[2048];
        var read = music.Read(buffer);
        read.Should().BeGreaterThan(0);
        music.Seek(music.TotalFrames / 2);
        var half = 0;
        while ((read = music.Read(buffer)) > 0) half += read;
        (half / 2).Should().BeCloseTo((int)(music.TotalFrames / 2), 1200, "seeking to the middle leaves half the frames");
    }
}
