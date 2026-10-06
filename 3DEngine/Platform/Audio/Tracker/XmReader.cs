using System.Buffers.Binary;
using System.Text;

namespace Engine;

/// <summary>Reads a FastTracker 2 module (<c>.xm</c>) into a <see cref="TrackerModule"/>.</summary>
/// <remarks>
/// The layout is that of FastTracker 2's own description, read as raylib's reader (jar_xm) reads it.
/// A field that runs past the end of the file reads as 0, so a file cut short plays what it holds
/// rather than failing, and a pattern the order table names that the file lacks is an empty one of
/// 64 rows, as FastTracker 2 plays it.
/// </remarks>
internal static class XmReader
{
    private const string Magic = "Extended Module: ";

    /// <exception cref="InvalidDataException">The bytes are not an XM module.</exception>
    public static TrackerModule Read(byte[] data)
    {
        if (data.Length < 80 || Encoding.ASCII.GetString(data, 0, Magic.Length) != Magic)
            throw new InvalidDataException("it does not begin with 'Extended Module: '");
        var bytes = new Bytes(data);

        var headerSize = bytes.U32(60);
        var length = bytes.U16(64);
        var restart = bytes.U16(66);
        var channels = bytes.U16(68);
        var patternCount = bytes.U16(70);
        var instrumentCount = bytes.U16(72);
        var flags = bytes.U16(74);
        if (length is 0 or > 256 || channels is 0 or > 64 || patternCount > 256 || instrumentCount > 128)
            throw new InvalidDataException($"its header names {length} orders, {channels} channels, {patternCount} patterns and {instrumentCount} instruments");

        var patterns = new List<TrackerPattern>();
        long offset = 60 + headerSize;
        for (int i = 0; i < patternCount; i++)
        {
            var rows = bytes.U16(offset + 5);
            var packed = bytes.U16(offset + 7);
            offset += bytes.U32(offset);
            patterns.Add(Unpack(bytes, offset, packed, rows is 0 or > 256 ? 64 : rows, channels));
            offset += packed;
        }

        // An order naming a pattern the file lacks plays an empty one.
        var orders = new byte[length];
        for (int i = 0; i < length; i++)
        {
            var pattern = bytes.U8(80 + i);
            if (pattern >= patterns.Count)
            {
                patterns.Add(new TrackerPattern(64, new TrackerSlot[64 * channels]));
                pattern = (byte)(patterns.Count - 1);
            }
            orders[i] = pattern;
        }

        var instruments = new TrackerInstrument[instrumentCount];
        for (int i = 0; i < instrumentCount; i++)
            instruments[i] = ReadInstrument(bytes, ref offset);

        var panning = new float[channels];
        Array.Fill(panning, 0.5f);
        return new TrackerModule
        {
            Name = Text(data, 17, 20),
            Channels = channels,
            Orders = orders,
            Restart = restart < length ? restart : 0,
            Patterns = [.. patterns],
            Instruments = instruments,
            LinearFrequencies = (flags & 1) != 0,
            Speed = Math.Max(1, (int)bytes.U16(76)),
            Tempo = Math.Max(32, (int)bytes.U16(78)),
            Panning = panning,
            NotesTakeSamplePanning = true,
        };
    }

    // A pattern's slots, each written whole as five bytes or packed behind a byte whose bits say
    // which of the five follow.
    private static TrackerPattern Unpack(Bytes bytes, long offset, int packed, int rows, int channels)
    {
        var slots = new TrackerSlot[rows * channels];
        long at = offset, end = offset + packed;
        for (int k = 0; k < slots.Length && at < end; k++)
        {
            var first = bytes.U8(at);
            if ((first & 0x80) == 0)
            {
                slots[k] = new TrackerSlot(first, bytes.U8(at + 1), bytes.U8(at + 2), bytes.U8(at + 3), bytes.U8(at + 4));
                at += 5;
                continue;
            }
            at++;
            byte Next(int bit) => (first & (1 << bit)) != 0 ? bytes.U8(at++) : (byte)0;
            var note = Next(0);
            var instrument = Next(1);
            var volume = Next(2);
            var effect = Next(3);
            slots[k] = new TrackerSlot(note, instrument, volume, effect, Next(4));
        }
        return new TrackerPattern(rows, slots);
    }

    private static TrackerInstrument ReadInstrument(Bytes bytes, ref long offset)
    {
        var start = offset;
        var size = bytes.U32(start);
        var sampleCount = bytes.U16(start + 27);
        // A header too short to hold its own count is read as the 29 bytes an instrument with no
        // samples has, so the next is found.
        offset += Math.Max(29u, size);
        if (sampleCount == 0 || sampleCount > 256) return new TrackerInstrument();

        var sampleHeaderSize = bytes.U32(start + 29);
        var sampleOfNote = new byte[96];
        for (int n = 0; n < 96; n++) sampleOfNote[n] = bytes.U8(start + 33 + n);

        TrackerEnvelope Envelope(long points, long count, long sustain, long type) => new()
        {
            Points = [.. Enumerable.Range(0, Math.Min(12, (int)bytes.U8(start + count)))
                .Select(j => ((int)bytes.U16(start + points + 4 * j), (int)bytes.U16(start + points + 4 * j + 2)))],
            On = (bytes.U8(start + type) & 1) != 0,
            Sustains = (bytes.U8(start + type) & 2) != 0,
            Loops = (bytes.U8(start + type) & 4) != 0,
            Sustain = bytes.U8(start + sustain),
            LoopStart = bytes.U8(start + sustain + 1),
            LoopEnd = bytes.U8(start + sustain + 2),
        };

        // The headers of the samples come first, then their frames in the same order.
        var headers = new (uint Length, uint LoopStart, uint LoopLength, byte Volume, sbyte Finetune, byte Type, byte Panning, sbyte Relative)[sampleCount];
        for (int j = 0; j < sampleCount; j++)
        {
            var at = offset;
            headers[j] = (bytes.U32(at), bytes.U32(at + 4), bytes.U32(at + 8), bytes.U8(at + 12), (sbyte)bytes.U8(at + 13),
                bytes.U8(at + 14), bytes.U8(at + 15), (sbyte)bytes.U8(at + 16));
            offset += sampleHeaderSize;
        }
        var samples = new TrackerSample[sampleCount];
        for (int j = 0; j < sampleCount; j++)
        {
            samples[j] = ReadSample(bytes, offset, headers[j]);
            offset += headers[j].Length;
        }

        // FastTracker 2 numbers its own vibrato's waveforms sine, square, ramp down and ramp up,
        // where the vibrato effect numbers them sine, ramp down, square and random.
        var vibrato = bytes.U8(start + 235) switch { 1 => 2, 2 => 1, 3 => 4, _ => 0 };
        return new TrackerInstrument
        {
            SampleOfNote = sampleOfNote,
            Samples = samples,
            Volume = Envelope(129, 225, 227, 233),
            Panning = Envelope(177, 226, 230, 234),
            VibratoType = vibrato,
            VibratoSweep = bytes.U8(start + 236),
            VibratoDepth = bytes.U8(start + 237),
            VibratoRate = bytes.U8(start + 238),
            Fadeout = bytes.U16(start + 239),
        };
    }

    // A sample's frames, each stored as its difference from the one before, in 8 or 16 bits. A
    // stereo sample, which some trackers write, holds its left frames and then its right, each run
    // of differences starting again from 0, and is played as their mean.
    private static TrackerSample ReadSample(Bytes bytes, long offset,
        (uint Length, uint LoopStart, uint LoopLength, byte Volume, sbyte Finetune, byte Type, byte Panning, sbyte Relative) header)
    {
        var wide = (header.Type & 0x10) != 0;
        var stereo = (header.Type & 0x20) != 0;
        var unit = wide ? 2 : 1;
        var frames = (int)Math.Min(header.Length / (uint)unit, 64u << 20);
        var decoded = new float[frames];
        int value = 0;
        for (int k = 0; k < frames; k++)
        {
            if (stereo && k == frames / 2) value = 0;
            if (wide)
            {
                value = (short)(value + (short)bytes.U16(offset + 2 * k));
                decoded[k] = value / 32768f;
            }
            else
            {
                value = (sbyte)(value + (sbyte)bytes.U8(offset + k));
                decoded[k] = value / 128f;
            }
        }

        var loopStart = header.LoopStart / (uint)unit;
        var loopLength = header.LoopLength / (uint)unit;
        if (stereo)
        {
            var half = frames / 2;
            var mono = new float[half];
            for (int k = 0; k < half; k++) mono[k] = (decoded[k] + decoded[half + k]) / 2f;
            decoded = mono;
            loopStart /= 2;
            loopLength /= 2;
        }

        var loopEnd = Math.Min(loopStart + loopLength, (uint)decoded.Length);
        var loop = (header.Type & 3) switch { 0 => TrackerLoop.None, 1 => TrackerLoop.Forward, _ => TrackerLoop.PingPong };
        if (loopStart >= loopEnd) loop = TrackerLoop.None;
        return new TrackerSample
        {
            Data = decoded,
            Loop = loop,
            LoopStart = loop == TrackerLoop.None ? 0 : (int)loopStart,
            LoopEnd = loop == TrackerLoop.None ? decoded.Length : (int)loopEnd,
            Volume = Math.Min(header.Volume, (byte)64) / 64f,
            Panning = header.Panning / 255f,
            RelativeNote = header.Relative,
            Finetune = header.Finetune,
        };
    }

    private static string Text(byte[] data, int at, int length) =>
        Encoding.Latin1.GetString(data, at, Math.Min(length, Math.Max(0, data.Length - at))).TrimEnd('\0', ' ');

    // The file's bytes read little-endian, 0 past its end.
    private readonly struct Bytes(byte[] data)
    {
        public byte U8(long at) => at >= 0 && at < data.Length ? data[at] : (byte)0;

        public ushort U16(long at) => at >= 0 && at + 2 <= data.Length
            ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)at)) : (ushort)(U8(at) | U8(at + 1) << 8);

        public uint U32(long at) => (uint)(U16(at) | U16(at + 2) << 16);
    }
}
