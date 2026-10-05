namespace Engine;

/// <summary><see cref="ISoundDecoder"/> for FLAC files, decoded in managed code by <see cref="FlacReader"/>.</summary>
/// <remarks>
/// The whole file is decoded into memory, as <see cref="OggSoundDecoder"/> does. Music played
/// through <c>LoadMusicStream</c> is decoded from its file as it plays instead.
/// </remarks>
internal sealed class FlacSoundDecoder : ISoundDecoder
{
    /// <inheritdoc />
    public string[] Extensions => [".flac"];

    /// <inheritdoc />
    public string FormatId => "flac";

    /// <inheritdoc />
    public async Task<Sound> DecodeAsync(AssetLoadContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        var bytes = await context.ReadAllBytesAsync(ct);
        return Decode(bytes, context.Path.ToString());
    }

    /// <summary>Decodes a FLAC file held in memory into interleaved float samples.</summary>
    /// <exception cref="InvalidDataException">The bytes are not a FLAC stream this reader decodes.</exception>
    public static Sound Decode(byte[] bytes, string sourcePath = "")
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var reader = new FlacReader(new MemoryStream(bytes));
            var samples = new float[checked((int)(reader.TotalFrames > 0 ? reader.TotalFrames * reader.Channels : 0))];
            var filled = 0;
            var buffer = new float[reader.Channels * 4096];
            int read;
            while ((read = reader.Read(buffer)) > 0)
            {
                if (filled + read > samples.Length) Array.Resize(ref samples, Math.Max(samples.Length * 2, filled + read));
                buffer.AsSpan(0, read).CopyTo(samples.AsSpan(filled));
                filled += read;
            }
            if (filled == 0) throw new InvalidDataException("it holds no audio frames");
            if (filled < samples.Length) Array.Resize(ref samples, filled);
            return new Sound
            {
                Samples = samples,
                SampleRate = reader.SampleRate,
                Channels = reader.Channels,
                SourcePath = sourcePath,
                SourceFormat = "flac",
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or OverflowException)
        {
            throw new InvalidDataException($"FlacSoundDecoder: '{sourcePath}' is not a FLAC file this reader decodes: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Reads a FLAC stream a frame at a time into interleaved float samples, as the format's
/// specification lays it out (RFC 9639): fixed and linear-predictive subframes, Rice-coded
/// residuals, and the stereo decorrelations.
/// </summary>
/// <remarks>
/// <para>
/// Written for the engine, since no managed FLAC decoder of the standing of NVorbis or NLayer is
/// available. The checksums of each frame are read and not checked, so a damaged frame decodes
/// to noise rather than failing the file.
/// </para>
/// <para>
/// A frame records no length of its own, so <see cref="Seek"/> decodes forward from the first
/// frame to the one asked for, which music does when it is moved to a time.
/// </para>
/// </remarks>
internal sealed class FlacReader : IDisposable
{
    private readonly BitReader _bits;
    private readonly long _firstFrame;
    private readonly int _streamBitsPerSample;

    // The frame decoded last, interleaved, and how much of it has been handed out.
    private float[] _frame = [];
    private int _frameSamples;
    private int _frameRead;
    private long _position;
    private int[][] _channelSamples = [];
    private long[] _scratch = [];

    public int Channels { get; }

    public int SampleRate { get; }

    public int BitsPerSample => _streamBitsPerSample;

    /// <summary>The frames, a sample for each channel, the stream holds, or 0 when it does not say.</summary>
    public long TotalFrames { get; }

    public FlacReader(Stream stream)
    {
        _bits = new BitReader(stream);
        if (_bits.Read(32) != 0x664C6143) throw new InvalidDataException("it does not start with fLaC");

        var seenInfo = false;
        bool last;
        do
        {
            last = _bits.Read(1) == 1;
            var type = (int)_bits.Read(7);
            var length = (int)_bits.Read(24);
            if (type == 0)
            {
                _bits.Read(16); // minimum block size
                _bits.Read(16); // maximum block size
                _bits.Read(24); // minimum frame size
                _bits.Read(24); // maximum frame size
                SampleRate = (int)_bits.Read(20);
                Channels = (int)_bits.Read(3) + 1;
                _streamBitsPerSample = (int)_bits.Read(5) + 1;
                TotalFrames = (long)_bits.Read(36);
                _bits.Skip(16 * 8); // the MD5 of the samples
                if (length > 34) _bits.Skip((length - 34) * 8);
                seenInfo = true;
            }
            else _bits.Skip(length * 8L);
        } while (!last);
        if (!seenInfo) throw new InvalidDataException("it has no STREAMINFO block");
        if (SampleRate <= 0) throw new InvalidDataException("its sample rate is 0");
        _firstFrame = _bits.BytePosition;
    }

    /// <summary>Fills <paramref name="buffer"/> with interleaved samples from where the last read ended.</summary>
    /// <returns>How many samples were written, 0 at the end.</returns>
    public int Read(Span<float> buffer)
    {
        var written = 0;
        while (written < buffer.Length)
        {
            if (_frameRead == _frameSamples && !NextFrame()) break;
            var count = Math.Min(buffer.Length - written, _frameSamples - _frameRead);
            _frame.AsSpan(_frameRead, count).CopyTo(buffer[written..]);
            _frameRead += count;
            written += count;
        }
        _position += written / Channels;
        return written;
    }

    /// <summary>Moves to a frame, from which the next read starts, by decoding from the stream's first frame up to it.</summary>
    public void Seek(long frame)
    {
        frame = Math.Max(0, frame);
        _bits.Seek(_firstFrame);
        _frameSamples = _frameRead = 0;
        _position = 0;
        while (_position < frame)
        {
            if (!NextFrame()) return;
            var frames = _frameSamples / Channels;
            if (_position + frames > frame)
            {
                _frameRead = (int)(frame - _position) * Channels;
                _position = frame;
                return;
            }
            _position += frames;
            _frameRead = _frameSamples;
        }
    }

    public void Dispose() => _bits.Dispose();

    // Decodes the next frame into _frame, or answers false at the end of the stream.
    private bool NextFrame()
    {
        if (!_bits.AtByteBoundary) _bits.AlignToByte();
        if (_bits.AtEnd) return false;
        if (_bits.Read(14) != 0x3FFE) throw new InvalidDataException("a frame does not start with the sync code");
        _bits.Read(1); // reserved
        _bits.Read(1); // blocking strategy, fixed or variable, which the header's number reflects
        var blockCode = (int)_bits.Read(4);
        var rateCode = (int)_bits.Read(4);
        var assignment = (int)_bits.Read(4);
        var sizeCode = (int)_bits.Read(3);
        _bits.Read(1); // reserved
        ReadUtf8Number();

        var blockSize = blockCode switch
        {
            0 => throw new InvalidDataException("a frame's block size code is reserved"),
            1 => 192,
            >= 2 and <= 5 => 576 << (blockCode - 2),
            6 => (int)_bits.Read(8) + 1,
            7 => (int)_bits.Read(16) + 1,
            _ => 256 << (blockCode - 8),
        };
        switch (rateCode)
        {
            case 12: _bits.Read(8); break;
            case 13: case 14: _bits.Read(16); break;
            case 15: throw new InvalidDataException("a frame's sample rate code is invalid");
        }
        var bitsPerSample = sizeCode switch
        {
            0 => _streamBitsPerSample,
            1 => 8,
            2 => 12,
            4 => 16,
            5 => 20,
            6 => 24,
            7 => 32,
            _ => throw new InvalidDataException("a frame's sample size code is reserved"),
        };
        _bits.Read(8); // CRC-8 of the header

        var channels = assignment < 8 ? assignment + 1 : assignment <= 10 ? 2 : throw new InvalidDataException("a frame's channel assignment is reserved");
        if (channels != Channels) throw new InvalidDataException($"a frame has {channels} channels where the stream has {Channels}");
        if (_channelSamples.Length < channels || _channelSamples[0].Length < blockSize)
            _channelSamples = Enumerable.Range(0, channels).Select(_ => new int[blockSize]).ToArray();

        for (int c = 0; c < channels; c++)
        {
            // The side channel of a stereo pair has a bit more than the samples, its difference.
            var side = assignment == 8 && c == 1 || assignment == 9 && c == 0 || assignment == 10 && c == 1;
            ReadSubframe(_channelSamples[c].AsSpan(0, blockSize), bitsPerSample + (side ? 1 : 0));
        }
        _bits.AlignToByte();
        _bits.Read(16); // CRC-16 of the frame

        Decorrelate(assignment, blockSize);
        if (_frame.Length < blockSize * channels) _frame = new float[blockSize * channels];
        var scale = 1f / (1L << (bitsPerSample - 1));
        for (int i = 0; i < blockSize; i++)
            for (int c = 0; c < channels; c++)
                _frame[i * channels + c] = _channelSamples[c][i] * scale;
        _frameSamples = blockSize * channels;
        _frameRead = 0;
        return true;
    }

    // The frame or sample number, UTF-8 coded, which is read past.
    private void ReadUtf8Number()
    {
        var first = (int)_bits.Read(8);
        var more = first < 0x80 ? 0 : first < 0xE0 ? 1 : first < 0xF0 ? 2 : first < 0xF8 ? 3 : first < 0xFC ? 4 : first < 0xFE ? 5 : 6;
        for (int i = 0; i < more; i++) _bits.Read(8);
    }

    private void Decorrelate(int assignment, int blockSize)
    {
        if (assignment < 8) return;
        var a = _channelSamples[0];
        var b = _channelSamples[1];
        for (int i = 0; i < blockSize; i++)
        {
            switch (assignment)
            {
                case 8: // left, side
                    b[i] = a[i] - b[i];
                    break;
                case 9: // side, right
                    a[i] = a[i] + b[i];
                    break;
                default: // mid, side
                    long mid = (long)a[i] * 2 + (b[i] & 1);
                    long sideSample = b[i];
                    a[i] = (int)((mid + sideSample) >> 1);
                    b[i] = (int)((mid - sideSample) >> 1);
                    break;
            }
        }
    }

    private void ReadSubframe(Span<int> samples, int bitsPerSample)
    {
        if (_bits.Read(1) != 0) throw new InvalidDataException("a subframe's padding bit is set");
        var type = (int)_bits.Read(6);
        var wasted = 0;
        if (_bits.Read(1) == 1)
        {
            wasted = 1;
            while (_bits.Read(1) == 0) wasted++;
        }
        bitsPerSample -= wasted;

        if (type == 0)
        {
            var value = _bits.ReadSigned(bitsPerSample);
            samples.Fill(value);
        }
        else if (type == 1)
        {
            for (int i = 0; i < samples.Length; i++) samples[i] = _bits.ReadSigned(bitsPerSample);
        }
        else if (type >= 8 && type <= 12)
        {
            var order = type - 8;
            for (int i = 0; i < order; i++) samples[i] = _bits.ReadSigned(bitsPerSample);
            ReadResidual(samples, order);
            Fixed(samples, order);
        }
        else if (type >= 32)
        {
            var order = type - 31;
            for (int i = 0; i < order; i++) samples[i] = _bits.ReadSigned(bitsPerSample);
            var precision = (int)_bits.Read(4) + 1;
            if (precision == 16) throw new InvalidDataException("a subframe's coefficient precision is invalid");
            var shift = _bits.ReadSigned(5);
            if (shift < 0) throw new InvalidDataException("a subframe's coefficients shift by a negative amount");
            Span<int> coefficients = stackalloc int[order];
            for (int i = 0; i < order; i++) coefficients[i] = _bits.ReadSigned(precision);
            ReadResidual(samples, order);
            Predict(samples, order, coefficients, shift);
        }
        else throw new InvalidDataException($"a subframe's type {type} is reserved");

        if (wasted > 0)
            for (int i = 0; i < samples.Length; i++) samples[i] <<= wasted;
    }

    // The residual after the warm-up samples, into the samples it follows, to which prediction adds.
    private void ReadResidual(Span<int> samples, int order)
    {
        var method = (int)_bits.Read(2);
        if (method > 1) throw new InvalidDataException("a residual's coding method is reserved");
        var (parameterBits, escape) = method == 0 ? (4, 15) : (5, 31);
        var partitionOrder = (int)_bits.Read(4);
        var partitions = 1 << partitionOrder;
        var perPartition = samples.Length >> partitionOrder;
        var at = order;
        for (int p = 0; p < partitions; p++)
        {
            var count = p == 0 ? perPartition - order : perPartition;
            var parameter = (int)_bits.Read(parameterBits);
            if (parameter == escape)
            {
                var raw = (int)_bits.Read(5);
                for (int i = 0; i < count; i++) samples[at++] = raw == 0 ? 0 : _bits.ReadSigned(raw);
            }
            else
                for (int i = 0; i < count; i++) samples[at++] = _bits.ReadRice(parameter);
        }
    }

    private static void Fixed(Span<int> s, int order)
    {
        for (int i = order; i < s.Length; i++)
            s[i] += order switch
            {
                0 => 0,
                1 => s[i - 1],
                2 => 2 * s[i - 1] - s[i - 2],
                3 => 3 * s[i - 1] - 3 * s[i - 2] + s[i - 3],
                _ => 4 * s[i - 1] - 6 * s[i - 2] + 4 * s[i - 3] - s[i - 4],
            };
    }

    private static void Predict(Span<int> s, int order, ReadOnlySpan<int> coefficients, int shift)
    {
        for (int i = order; i < s.Length; i++)
        {
            long sum = 0;
            for (int j = 0; j < order; j++) sum += (long)coefficients[j] * s[i - 1 - j];
            s[i] += (int)(sum >> shift);
        }
    }

    // Bits read most significant first from a stream, through a buffer of its own.
    private sealed class BitReader(Stream stream) : IDisposable
    {
        private readonly byte[] _buffer = new byte[64 * 1024];
        private int _length;
        private int _index;
        private long _bufferStart;
        private ulong _cache;
        private int _cached;

        // Where the next whole byte not yet in the cache comes from, as the stream counts it.
        private long NextByte => _bufferStart + _index;

        public long BytePosition => NextByte - _cached / 8;

        public bool AtByteBoundary => _cached % 8 == 0;

        public bool AtEnd => _cached == 0 && !Fill();

        public void AlignToByte() => Read(_cached % 8);

        public ulong Read(int count)
        {
            if (count == 0) return 0;
            if (count > 32)
                return (Read(count - 32) << 32) | Read(32);
            while (_cached < count)
            {
                if (_index == _length && !Fill()) throw new EndOfStreamException("the stream ends inside a frame");
                _cache = (_cache << 8) | _buffer[_index++];
                _cached += 8;
            }
            _cached -= count;
            return (_cache >> _cached) & ((1UL << count) - 1);
        }

        public int ReadSigned(int count)
        {
            if (count == 0) return 0;
            var value = (long)Read(count);
            return (int)((value << (64 - count)) >> (64 - count));
        }

        public int ReadRice(int parameter)
        {
            uint quotient = 0;
            while (Read(1) == 0) quotient++;
            var value = (quotient << parameter) | (uint)Read(parameter);
            return (int)(value >> 1) ^ -(int)(value & 1);
        }

        public void Skip(long bits)
        {
            while (bits > 32)
            {
                Read(32);
                bits -= 32;
            }
            Read((int)bits);
        }

        public void Seek(long position)
        {
            stream.Seek(position, SeekOrigin.Begin);
            _bufferStart = position;
            _length = _index = 0;
            _cache = 0;
            _cached = 0;
        }

        private bool Fill()
        {
            if (_index < _length) return true;
            _bufferStart += _length;
            _length = stream.Read(_buffer, 0, _buffer.Length);
            _index = 0;
            return _length > 0;
        }

        public void Dispose() => stream.Dispose();
    }
}
