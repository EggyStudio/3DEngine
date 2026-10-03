using System.Buffers.Binary;

namespace Engine;

/// <summary>
/// Built-in <see cref="ISoundDecoder"/> for canonical RIFF/WAVE files. Lives in the
/// capability module (<c>Engine.Sound</c>) rather than a backend module because the
/// format is so simple it has no native dependencies and ships with virtually every
/// game-audio asset pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coverage:</b> uncompressed PCM (<c>WAVE_FORMAT_PCM</c>, code 0x0001) at 8/16/24/32
/// bits-per-sample, plus IEEE 32-bit float (<c>WAVE_FORMAT_IEEE_FLOAT</c>, code 0x0003).
/// Compressed flavours (ADPCM, MP3-in-WAV, etc.) are out of scope - those should arrive
/// via a future <c>Engine.Sound.Vorbis</c> / <c>.Opus</c> / <c>.Mp3</c> backend.
/// </para>
/// <para>
/// <b>Channel order:</b> samples are interleaved per the canonical WAV layout
/// (frame = N channel samples back-to-back); we don't transcode to deinterleaved.
/// </para>
/// </remarks>
public sealed class WavSoundDecoder : ISoundDecoder
{
    private static readonly ILogger Logger = Log.Category("Engine.Sound");

    /// <inheritdoc />
    public string[] Extensions => [".wav", ".wave"];

    /// <inheritdoc />
    public string FormatId => "wav-builtin";

    /// <inheritdoc />
    public async Task<Sound> DecodeAsync(AssetLoadContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        var bytes = await context.ReadAllBytesAsync(ct);
        var sound = Decode(bytes, context.Path.ToString());
        Logger.Debug(
            $"WavSoundDecoder: '{context.Path}' decoded - {sound.SampleRate} Hz, " +
            $"{sound.Channels} ch, {sound.Samples.Length / Math.Max(1, sound.Channels)} frames " +
            $"({sound.DurationSeconds:F3}s).");
        return sound;
    }

    /// <summary>
    /// Hand-parses a canonical RIFF/WAVE byte buffer into a <see cref="Sound"/>. Public
    /// for tests; production callers go through <see cref="DecodeAsync"/>.
    /// </summary>
    public static Sound Decode(byte[] bytes, string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 44)
            throw new InvalidDataException($"WavSoundDecoder: '{sourcePath}' too small to be WAV ({bytes.Length} bytes).");

        // RIFF header: "RIFF" <size32> "WAVE"
        if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F'
            || bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
        {
            throw new InvalidDataException($"WavSoundDecoder: '{sourcePath}' is not a RIFF/WAVE file.");
        }

        // Walk chunks looking for "fmt " then "data". Skip anything else (LIST, fact, JUNK, ...).
        int audioFormat = 0, channels = 0, sampleRate = 0, bitsPerSample = 0;
        ReadOnlySpan<byte> dataChunk = default;
        int pos = 12;
        while (pos + 8 <= bytes.Length)
        {
            var id = bytes.AsSpan(pos, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(pos + 4, 4));
            int body = pos + 8;
            if (body + size > bytes.Length) break; // truncated; bail.

            if (id[0] == 'f' && id[1] == 'm' && id[2] == 't' && id[3] == ' ')
            {
                if (size < 16) throw new InvalidDataException($"WavSoundDecoder: '{sourcePath}' fmt chunk too small ({size}).");
                audioFormat   = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body, 2));
                channels      = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 2, 2));
                sampleRate    = BinaryPrimitives.ReadInt32LittleEndian( bytes.AsSpan(body + 4, 4));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 14, 2));
            }
            else if (id[0] == 'd' && id[1] == 'a' && id[2] == 't' && id[3] == 'a')
            {
                dataChunk = bytes.AsSpan(body, size);
                // Don't break: a fmt chunk may still appear later in malformed files,
                // but the canonical layout puts fmt before data so we usually have both
                // by now. Step over to allow trailing chunks.
            }

            // Chunks are padded to even sizes.
            pos = body + size + (size & 1);
        }

        if (channels <= 0 || sampleRate <= 0 || bitsPerSample <= 0)
            throw new InvalidDataException($"WavSoundDecoder: '{sourcePath}' missing or invalid fmt chunk.");
        if (dataChunk.IsEmpty)
            throw new InvalidDataException($"WavSoundDecoder: '{sourcePath}' missing data chunk.");

        var samples = ConvertToFloat(dataChunk, audioFormat, bitsPerSample, sourcePath);
        return new Sound
        {
            Samples = samples,
            SampleRate = sampleRate,
            Channels = channels,
            SourcePath = sourcePath,
            SourceFormat = "wav-builtin",
        };
    }

    private static float[] ConvertToFloat(ReadOnlySpan<byte> data, int format, int bps, string sourcePath)
    {
        // Format codes: 0x0001 = PCM (integer), 0x0003 = IEEE float.
        switch (format, bps)
        {
            case (1, 8):
                {
                    // Unsigned 8-bit PCM: bias 128.
                    var dst = new float[data.Length];
                    for (int i = 0; i < data.Length; i++)
                        dst[i] = (data[i] - 128) / 128f;
                    return dst;
                }
            case (1, 16):
                {
                    int n = data.Length / 2;
                    var dst = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        short s = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 2, 2));
                        dst[i] = s / 32768f;
                    }
                    return dst;
                }
            case (1, 24):
                {
                    int n = data.Length / 3;
                    var dst = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        // 24-bit LE signed; sign-extend by shifting up to int32 then back.
                        int b0 = data[i * 3 + 0];
                        int b1 = data[i * 3 + 1];
                        int b2 = data[i * 3 + 2];
                        int v = (b0) | (b1 << 8) | (b2 << 16);
                        if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                        dst[i] = v / 8388608f;
                    }
                    return dst;
                }
            case (1, 32):
                {
                    int n = data.Length / 4;
                    var dst = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        int v = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(i * 4, 4));
                        dst[i] = v / 2147483648f;
                    }
                    return dst;
                }
            case (3, 32):
                {
                    int n = data.Length / 4;
                    var dst = new float[n];
                    for (int i = 0; i < n; i++)
                        dst[i] = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(i * 4, 4));
                    return dst;
                }
            default:
                throw new NotSupportedException(
                    $"WavSoundDecoder: '{sourcePath}' unsupported (format=0x{format:X4}, bps={bps}).");
        }
    }
}