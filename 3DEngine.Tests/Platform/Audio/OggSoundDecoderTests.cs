using FluentAssertions;

namespace Engine.Tests.Platform.Audio;

[Trait("Category", "Unit")]
public class OggSoundDecoderTests
{
    // A tenth of a second of a 440 Hz tone, stereo at 22050 Hz, encoded by ffmpeg's libvorbis.
    private static byte[] Tone() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Platform", "Audio", "tone.ogg"));

    [Fact]
    public void An_Ogg_File_Decodes_To_Interleaved_Samples()
    {
        var sound = OggSoundDecoder.Decode(Tone(), "tone.ogg");

        sound.Channels.Should().Be(2);
        sound.SampleRate.Should().Be(22050);
        sound.DurationSeconds.Should().BeApproximately(0.1, 0.02);
        sound.Samples.Should().Contain(s => Math.Abs(s) > 0.05f, "a tone at ffmpeg's default amplitude of an eighth is not silence");
    }

    [Fact]
    public void Bytes_That_Are_Not_Ogg_Are_Refused()
    {
        var act = () => OggSoundDecoder.Decode([1, 2, 3, 4, 5, 6, 7, 8], "noise.ogg");

        act.Should().Throw<InvalidDataException>();
    }
}
