using System.Buffers.Binary;
using FluentAssertions;

namespace Engine.Tests.Audio;

/// <summary>
/// Unit tests for <see cref="WavSoundDecoder"/>: builds canonical RIFF/WAVE byte
/// streams in-memory and asserts the decoder converts them to the engine's
/// interleaved float-PCM <see cref="Sound"/> shape.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Module", "Engine.Sound")]
public class WavSoundDecoderTests
{
    private static byte[] BuildPcmWav(short[] samples, int sampleRate, int channels, int bitsPerSample)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int bytesPerSample = bitsPerSample / 8;
        int dataBytes = samples.Length * bytesPerSample;
        int blockAlign = channels * bytesPerSample;
        int byteRate = sampleRate * blockAlign;

        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + dataBytes);
        bw.Write("WAVE".ToCharArray());
        bw.Write("fmt ".ToCharArray());
        bw.Write(16);                           // fmt chunk size
        bw.Write((ushort)1);                    // PCM format
        bw.Write((ushort)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((ushort)blockAlign);
        bw.Write((ushort)bitsPerSample);
        bw.Write("data".ToCharArray());
        bw.Write(dataBytes);
        foreach (var s in samples) bw.Write(s);
        return ms.ToArray();
    }

    private static byte[] BuildFloatWav(float[] samples, int sampleRate, int channels)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int dataBytes = samples.Length * 4;
        int blockAlign = channels * 4;
        int byteRate = sampleRate * blockAlign;

        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + dataBytes);
        bw.Write("WAVE".ToCharArray());
        bw.Write("fmt ".ToCharArray());
        bw.Write(16);
        bw.Write((ushort)3);                    // IEEE float
        bw.Write((ushort)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((ushort)blockAlign);
        bw.Write((ushort)32);
        bw.Write("data".ToCharArray());
        bw.Write(dataBytes);
        Span<byte> tmp = stackalloc byte[4];
        foreach (var f in samples)
        {
            BinaryPrimitives.WriteSingleLittleEndian(tmp, f);
            bw.Write(tmp);
        }
        return ms.ToArray();
    }

    private static AssetLoadContext OpenContext(string path, byte[] bytes) =>
        new AssetLoadContext(new MemoryStream(bytes), new AssetPath(path), _ => default);

    [Fact]
    public void Extensions_And_FormatId_Match_Spec()
    {
        var dec = new WavSoundDecoder();
        dec.Extensions.Should().BeEquivalentTo(new[] { ".wav", ".wave" });
        dec.FormatId.Should().Be("wav-builtin");
    }

    [Fact]
    public async Task Decodes_Pcm16_Mono_Into_Float_Samples()
    {
        var dec = new WavSoundDecoder();
        var bytes = BuildPcmWav(new short[] { 0, short.MaxValue, short.MinValue, 0 }, 22050, 1, 16);
        using var ctx = OpenContext("tests/inline.wav", bytes);

        var sound = await dec.DecodeAsync(ctx, CancellationToken.None);

        sound.SampleRate.Should().Be(22050);
        sound.Channels.Should().Be(1);
        sound.SourceFormat.Should().Be("wav-builtin");
        sound.Samples.Should().HaveCount(4);
        sound.Samples[0].Should().Be(0f);
        sound.Samples[1].Should().BeApproximately(0.99997f, 1e-3f);
        sound.Samples[2].Should().BeApproximately(-1.0f, 1e-3f);
        sound.DurationSeconds.Should().BeApproximately(4.0 / 22050.0, 1e-9);
    }

    [Fact]
    public async Task Decodes_Pcm16_Stereo_Preserves_Interleaving()
    {
        var dec = new WavSoundDecoder();
        // Two stereo frames: (L=1, R=2) then (L=3, R=4) - at full-scale fractions.
        var s = new short[] { 16384, -16384, 8192, -8192 };
        var bytes = BuildPcmWav(s, 48000, 2, 16);
        using var ctx = OpenContext("tests/stereo.wav", bytes);

        var sound = await dec.DecodeAsync(ctx, CancellationToken.None);

        sound.Channels.Should().Be(2);
        sound.Samples.Should().HaveCount(4);
        sound.Samples[0].Should().BeApproximately(0.5f, 0.01f);
        sound.Samples[1].Should().BeApproximately(-0.5f, 0.01f);
    }

    [Fact]
    public async Task Decodes_Float32_Wav()
    {
        var dec = new WavSoundDecoder();
        var bytes = BuildFloatWav(new float[] { -1f, 0f, 0.25f, 1f }, 48000, 1);
        using var ctx = OpenContext("tests/float.wav", bytes);

        var sound = await dec.DecodeAsync(ctx, CancellationToken.None);

        sound.Samples.Should().Equal(-1f, 0f, 0.25f, 1f);
    }

    [Fact]
    public async Task Throws_On_Garbage_Input()
    {
        var dec = new WavSoundDecoder();
        using var ctx = OpenContext("tests/garbage.wav", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
        var act = () => dec.DecodeAsync(ctx, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task SoundAssetLoader_Routes_Wav_Through_Decoder()
    {
        var registry = new SoundDecoderRegistry();
        registry.RegisterDecoder(new WavSoundDecoder());
        var loader = new SoundAssetLoader(registry);

        loader.Extensions.Should().Contain(".wav");

        using var ctx = OpenContext("tests/inline.wav",
            BuildPcmWav(new short[] { 0, 0, 0, 0 }, 44100, 1, 16));

        var result = await loader.LoadAsync(ctx, CancellationToken.None);
        result.Success.Should().BeTrue(result.Error);
        result.Asset!.SampleRate.Should().Be(44100);
        result.Asset.SourceFormat.Should().Be("wav-builtin");
    }

    [Fact]
    public async Task SoundAssetLoader_Fails_For_Unknown_Extension()
    {
        var registry = new SoundDecoderRegistry();
        registry.RegisterDecoder(new WavSoundDecoder());
        var loader = new SoundAssetLoader(registry);

        using var ctx = OpenContext("tests/foo.ogg", new byte[] { 0 });

        var result = await loader.LoadAsync(ctx, CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain(".ogg");
    }
}