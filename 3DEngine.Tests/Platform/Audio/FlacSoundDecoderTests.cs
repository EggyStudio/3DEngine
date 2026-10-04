using FluentAssertions;

namespace Engine.Tests.Platform.Audio;

/// <summary>
/// FLAC files decoded against the WAV files they were encoded from by the reference encoder,
/// which they match sample for sample, since FLAC is lossless.
/// </summary>
/// <remarks>
/// <c>flac16.wav</c> is a quarter second of stereo at 44,100 Hz, silent for its first 50 ms, with
/// a tone and a little noise in each channel. <c>flac16_fast.flac</c> is it at <c>flac -0</c>, of
/// constant and fixed subframes, and <c>flac16_best.flac</c> at <c>flac -8</c>, of LPC subframes
/// with the right channel and the side. <c>flac16ms.wav</c> is a tone plus noise on the left and
/// minus it on the right, which <c>flac -8</c> codes as mid and side. <c>flac24</c> is 24-bit mono
/// at 48,000 Hz.
/// </remarks>
[Trait("Category", "Unit")]
public class FlacSoundDecoderTests
{
    private static byte[] Bytes(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Platform", "Audio", name));

    [Theory]
    [InlineData("flac16_fast.flac", "flac16.wav")]
    [InlineData("flac16_best.flac", "flac16.wav")]
    [InlineData("flac16ms.flac", "flac16ms.wav")]
    [InlineData("flac24.flac", "flac24.wav")]
    public void A_Flac_File_Decodes_To_The_Samples_It_Was_Encoded_From(string flac, string wav)
    {
        var decoded = FlacSoundDecoder.Decode(Bytes(flac), flac);
        var reference = WavSoundDecoder.Decode(Bytes(wav), wav);

        (decoded.Channels, decoded.SampleRate).Should().Be((reference.Channels, reference.SampleRate));
        decoded.Samples.Length.Should().Be(reference.Samples.Length);
        var worst = 0f;
        for (int i = 0; i < decoded.Samples.Length; i++) worst = MathF.Max(worst, MathF.Abs(decoded.Samples[i] - reference.Samples[i]));
        worst.Should().BeLessThan(1e-6f, "FLAC is lossless");
        decoded.Samples.Should().Contain(s => Math.Abs(s) > 0.1f, "the files are not silence");
    }

    [Fact]
    public void Music_Read_From_A_Flac_File_A_Piece_At_A_Time_And_Moved_Matches_The_Whole()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Platform", "Audio", "flac16_best.flac");
        var whole = FlacSoundDecoder.Decode(File.ReadAllBytes(path)).Samples;
        using var music = new FlacMusicDecoder(File.OpenRead(path));
        music.TotalFrames.Should().Be(whole.Length / 2);

        var pieces = new List<float>();
        var buffer = new float[1000];
        int read;
        while ((read = music.Read(buffer)) > 0) pieces.AddRange(buffer.AsSpan(0, read));
        pieces.Should().Equal(whole);

        music.Seek(5000);
        music.Read(buffer.AsSpan(0, 10)).Should().Be(10);
        buffer.AsSpan(0, 10).ToArray().Should().Equal(whole.AsSpan(10000, 10).ToArray(), "frame 5000 is where it was moved to");
    }

    [Fact]
    public void Bytes_That_Are_Not_Flac_Are_Refused()
    {
        var act = () => FlacSoundDecoder.Decode([1, 2, 3, 4, 5, 6, 7, 8], "noise.flac");

        act.Should().Throw<InvalidDataException>();
    }
}
