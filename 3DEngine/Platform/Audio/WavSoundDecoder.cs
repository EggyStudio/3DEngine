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
/// bits-per-sample, plus IEEE 32-bit float (<c>WAVE_FORMAT_IEEE_FLOAT</c>, code 0x0003), either
/// of them also inside <c>WAVE_FORMAT_EXTENSIBLE</c> (0xFFFE), as encoders write files of more
/// than 16 bits. Compressed formats inside a WAV (ADPCM, MP3) are not read. Ogg Vorbis files go
/// through <see cref="OggSoundDecoder"/>.
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
            $"WavSoundDecoder: '{context.Path}' decoded, {sound.SampleRate} Hz, " +
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
                // WAVE_FORMAT_EXTENSIBLE, which encoders write for more than 16 bits or two
                // channels, names the real format in the first two bytes of its sub-format.
                if (audioFormat == 0xFFFE && size >= 26)
                    audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 24, 2));
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
        var bytesPerSample = BytesPerSample(format, bps, sourcePath);
        var dst = new float[data.Length / bytesPerSample];
        Convert(data, format, bps, dst);
        return dst;
    }

    /// <summary>The bytes one sample of a format takes, or throws for a format the decoder does not read.</summary>
    /// <exception cref="NotSupportedException">The format is not 8, 16, 24 or 32-bit PCM or 32-bit float.</exception>
    internal static int BytesPerSample(int format, int bps, string sourcePath) => (format, bps) switch
    {
        (1, 8) => 1,
        (1, 16) => 2,
        (1, 24) => 3,
        (1, 32) or (3, 32) => 4,
        _ => throw new NotSupportedException($"WavSoundDecoder: '{sourcePath}' unsupported (format=0x{format:X4}, bps={bps})."),
    };

    /// <summary>Converts whole samples of <paramref name="data"/> into <paramref name="dst"/>, as many as fit.</summary>
    internal static void Convert(ReadOnlySpan<byte> data, int format, int bps, Span<float> dst)
    {
        // Format codes: 0x0001 = PCM (integer), 0x0003 = IEEE float.
        switch (format, bps)
        {
            case (1, 8):
                // Unsigned 8-bit PCM: bias 128.
                for (int i = 0; i < data.Length && i < dst.Length; i++)
                    dst[i] = (data[i] - 128) / 128f;
                break;
            case (1, 16):
                for (int i = 0; i < data.Length / 2 && i < dst.Length; i++)
                    dst[i] = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i * 2, 2)) / 32768f;
                break;
            case (1, 24):
                for (int i = 0; i < data.Length / 3 && i < dst.Length; i++)
                {
                    // 24-bit LE signed; sign-extend by shifting up to int32 then back.
                    int v = data[i * 3] | (data[i * 3 + 1] << 8) | (data[i * 3 + 2] << 16);
                    if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
                    dst[i] = v / 8388608f;
                }
                break;
            case (1, 32):
                for (int i = 0; i < data.Length / 4 && i < dst.Length; i++)
                    dst[i] = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(i * 4, 4)) / 2147483648f;
                break;
            case (3, 32):
                for (int i = 0; i < data.Length / 4 && i < dst.Length; i++)
                    dst[i] = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(i * 4, 4));
                break;
        }
    }
}

/// <summary>
/// Reads a WAV file's samples from the file as music plays, a piece at a time, rather than
/// decoding it whole, so a long piece costs a buffer rather than its length in memory.
/// </summary>
internal sealed class WavMusicDecoder : IMusicDecoder
{
    private readonly FileStream _file;
    private readonly long _dataStart;
    private readonly int _format, _bitsPerSample, _bytesPerFrame;
    private byte[] _scratch = [];
    private long _frame;

    /// <summary>Opens <paramref name="path"/> and finds its format and samples, reading nothing else.</summary>
    /// <exception cref="InvalidDataException">The file is not a WAV file with a format and samples.</exception>
    /// <exception cref="NotSupportedException">Its samples are in a format the decoder does not read.</exception>
    public WavMusicDecoder(string path)
    {
        _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        try
        {
            Span<byte> header = stackalloc byte[12];
            if (_file.Read(header) < 12 || !header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
                throw new InvalidDataException($"'{path}' is not a RIFF/WAVE file.");

            // Every chunk's header is read, and the bodies of all but the format chunk skipped.
            Span<byte> chunk = stackalloc byte[8];
            Span<byte> fmt = stackalloc byte[26];
            long dataStart = -1, dataLength = 0;
            while (_file.Read(chunk) == 8)
            {
                var size = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
                var body = _file.Position;
                if (chunk[..4].SequenceEqual("fmt "u8) && size >= 16 && _file.Read(fmt[..(int)Math.Min(size, 26)]) >= 16)
                {
                    _format = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
                    Channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
                    SampleRate = BinaryPrimitives.ReadInt32LittleEndian(fmt[4..]);
                    _bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);
                    // WAVE_FORMAT_EXTENSIBLE names the real format in its sub-format, as above.
                    if (_format == 0xFFFE && size >= 26) _format = BinaryPrimitives.ReadUInt16LittleEndian(fmt[24..]);
                }
                else if (chunk[..4].SequenceEqual("data"u8))
                {
                    dataStart = body;
                    dataLength = Math.Min(size, _file.Length - body);
                }
                _file.Position = body + size + (size & 1);
            }

            if (Channels <= 0 || SampleRate <= 0 || _bitsPerSample <= 0)
                throw new InvalidDataException($"'{path}' has no valid fmt chunk.");
            if (dataStart < 0)
                throw new InvalidDataException($"'{path}' has no data chunk.");
            _bytesPerFrame = WavSoundDecoder.BytesPerSample(_format, _bitsPerSample, path) * Channels;
            _dataStart = dataStart;
            TotalFrames = dataLength / _bytesPerFrame;
            _file.Position = _dataStart;
        }
        catch
        {
            _file.Dispose();
            throw;
        }
    }

    public int Channels { get; }

    public int SampleRate { get; }

    public long TotalFrames { get; }

    public int Read(Span<float> buffer)
    {
        var frames = (int)Math.Min(buffer.Length / Channels, TotalFrames - _frame);
        if (frames <= 0) return 0;
        var bytes = frames * _bytesPerFrame;
        if (_scratch.Length < bytes) _scratch = new byte[bytes];
        var read = _file.ReadAtLeast(_scratch.AsSpan(0, bytes), bytes, throwOnEndOfStream: false);
        frames = read / _bytesPerFrame;
        WavSoundDecoder.Convert(_scratch.AsSpan(0, frames * _bytesPerFrame), _format, _bitsPerSample, buffer);
        _frame += frames;
        return frames * Channels;
    }

    public void Seek(long frame)
    {
        _frame = Math.Clamp(frame, 0, TotalFrames);
        _file.Position = _dataStart + _frame * _bytesPerFrame;
    }

    public void Dispose() => _file.Dispose();
}
