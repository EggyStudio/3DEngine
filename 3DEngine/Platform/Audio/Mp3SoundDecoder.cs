using NLayer;

namespace Engine;

/// <summary><see cref="ISoundDecoder"/> for MP3 files, through NLayer, in managed code as NVorbis decodes Ogg.</summary>
/// <remarks>
/// The whole file is decoded into memory, as <see cref="OggSoundDecoder"/> does. Music played
/// through <c>LoadMusicStream</c> is decoded from its file as it plays instead, by
/// <c>Mp3MusicDecoder</c>. An encoder pads the start of a file with a few milliseconds of silence,
/// which is kept, so a sound starts that much late.
/// </remarks>
public sealed class Mp3SoundDecoder : ISoundDecoder
{
    /// <inheritdoc />
    public string[] Extensions => [".mp3"];

    /// <inheritdoc />
    public string FormatId => "mp3";

    /// <inheritdoc />
    public async Task<Sound> DecodeAsync(AssetLoadContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        var bytes = await context.ReadAllBytesAsync(ct);
        return Decode(bytes, context.Path.ToString());
    }

    /// <summary>Decodes an MP3 file held in memory into interleaved float samples.</summary>
    /// <exception cref="InvalidDataException">The bytes hold no MPEG audio.</exception>
    public static Sound Decode(byte[] bytes, string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(bytes);
        static InvalidDataException NotMp3(string path, string why, Exception? inner = null) =>
            new($"Mp3SoundDecoder: '{path}' is not an MP3 file: {why}", inner);

        MpegFile file;
        try
        {
            file = new MpegFile(new MemoryStream(bytes));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or InvalidDataException or EndOfStreamException)
        {
            throw NotMp3(sourcePath, ex.Message, ex);
        }

        using (file)
        {
            if (file.SampleRate <= 0 || file.Channels <= 0) throw NotMp3(sourcePath, "no MPEG audio frames were found");
            var samples = new List<float>();
            var buffer = new float[file.Channels * 4096];
            int read;
            while ((read = file.ReadSamples(buffer, 0, buffer.Length)) > 0)
                samples.AddRange(buffer.AsSpan(0, read));
            if (samples.Count == 0) throw NotMp3(sourcePath, "no MPEG audio frames were found");

            return new Sound
            {
                Samples = [.. samples],
                SampleRate = file.SampleRate,
                Channels = file.Channels,
                SourcePath = sourcePath,
                SourceFormat = "mp3",
            };
        }
    }
}
