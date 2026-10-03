using NVorbis;

namespace Engine;

/// <summary><see cref="ISoundDecoder"/> for Ogg Vorbis files, through NVorbis.</summary>
/// <remarks>
/// The whole file is decoded into memory, as <see cref="WavSoundDecoder"/> does, so a long piece of
/// music costs its full length in samples. Streaming is not written (see TODO.md).
/// </remarks>
public sealed class OggSoundDecoder : ISoundDecoder
{
    /// <inheritdoc />
    public string[] Extensions => [".ogg"];

    /// <inheritdoc />
    public string FormatId => "ogg-vorbis";

    /// <inheritdoc />
    public async Task<Sound> DecodeAsync(AssetLoadContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        var bytes = await context.ReadAllBytesAsync(ct);
        return Decode(bytes, context.Path.ToString());
    }

    /// <summary>Decodes an Ogg Vorbis file held in memory into interleaved float samples.</summary>
    /// <exception cref="InvalidDataException">The bytes are not an Ogg Vorbis stream.</exception>
    public static Sound Decode(byte[] bytes, string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var reader = new VorbisReader(new MemoryStream(bytes), closeOnDispose: true);
            var samples = new List<float>((int)Math.Min(int.MaxValue, reader.TotalSamples * reader.Channels));
            var buffer = new float[reader.Channels * 4096];
            int read;
            while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
                samples.AddRange(buffer.AsSpan(0, read));

            return new Sound
            {
                Samples = [.. samples],
                SampleRate = reader.SampleRate,
                Channels = reader.Channels,
                SourcePath = sourcePath,
                SourceFormat = "ogg-vorbis",
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or EndOfStreamException)
        {
            throw new InvalidDataException($"OggSoundDecoder: '{sourcePath}' is not an Ogg Vorbis file: {ex.Message}", ex);
        }
    }
}
