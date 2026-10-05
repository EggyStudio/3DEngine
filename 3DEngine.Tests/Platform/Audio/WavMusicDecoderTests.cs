using FluentAssertions;

namespace Engine.Tests.Platform.Audio;

[Trait("Category", "Unit")]
public sealed class WavMusicDecoderTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-wav-music-");

    public void Dispose() => _folder.Dispose();

    // A stereo 16-bit file of the given frames, a ramp on each channel, with a LIST chunk before
    // its samples as editors write one.
    private string Write(int frames)
    {
        using var stream = new MemoryStream();
        using (var w = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var data = frames * 4;
            w.Write("RIFF"u8); w.Write(4 + 24 + 12 + 8 + data); w.Write("WAVE"u8);
            w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(8000); w.Write(8000 * 4); w.Write((short)4); w.Write((short)16);
            w.Write("LIST"u8); w.Write(4); w.Write("INFO"u8);
            w.Write("data"u8); w.Write(data);
            for (int i = 0; i < frames; i++) { w.Write((short)(i * 7)); w.Write((short)(-i * 7)); }
        }
        var path = Path.Combine(_folder.Path, "ramp.wav");
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }

    [Fact]
    public void Streaming_Reads_The_Same_Samples_As_Decoding_Whole_And_Seeks()
    {
        var path = Write(3000);
        var whole = WavSoundDecoder.Decode(File.ReadAllBytes(path)).Samples;
        using var music = new WavMusicDecoder(File.OpenRead(path), path);

        music.Channels.Should().Be(2);
        music.SampleRate.Should().Be(8000);
        music.TotalFrames.Should().Be(3000);

        var streamed = new List<float>();
        var buffer = new float[1000];
        int read;
        while ((read = music.Read(buffer)) > 0) streamed.AddRange(buffer.AsSpan(0, read).ToArray());
        streamed.Should().Equal(whole);

        music.Seek(2500);
        music.Read(buffer).Should().Be(1000, "500 frames of two channels are left");
        buffer[0].Should().Be(whole[5000]);
    }

    [Fact]
    public void A_File_That_Is_Not_WAV_Is_Refused()
    {
        var path = Path.Combine(_folder.Path, "not.wav");
        File.WriteAllText(path, "not a wave file at all, but long enough");
        var open = () => new WavMusicDecoder(File.OpenRead(path), path);
        open.Should().Throw<InvalidDataException>();
    }
}
