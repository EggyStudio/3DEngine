using System.Text;

namespace Engine;

/// <summary>Reads a ProTracker module (<c>.mod</c>), or one of the trackers that wrote the same layout, into a <see cref="TrackerModule"/>.</summary>
/// <remarks>
/// <para>
/// The mark at byte 1080 says how many channels the patterns hold: <c>M.K.</c> and its kin four,
/// <c>6CHN</c> and <c>8CHN</c> as many as they say, <c>16CH</c> and the like up to 32. A file with
/// no mark there is an older Soundtracker module of 15 samples and four channels.
/// </para>
/// <para>
/// A MOD is played as the XM it is the ancestor of. Its periods become the notes whose Amiga periods
/// they are (428, middle C at 8,287 Hz, is note 49, C-4), its samples instruments of one sample with
/// no envelopes, and its effects keep their numbers, which XM took from it. Its channels are panned
/// as the Amiga's were, the first and fourth of each four left and the others right, each with a
/// third of the other side, as raylib's player mixes them.
/// </para>
/// </remarks>
internal static class ModReader
{
    /// <exception cref="InvalidDataException">The bytes are not a module this reader knows.</exception>
    public static TrackerModule Read(byte[] data)
    {
        var channels = data.Length >= 1084 ? ChannelsOf(Encoding.ASCII.GetString(data, 1080, 4)) : 0;
        var sampleCount = channels > 0 ? 31 : 15;
        if (channels == 0) channels = 4;

        var orderAt = 20 + 30 * sampleCount;
        var patternAt = orderAt + 2 + 128 + (sampleCount == 31 ? 4 : 0);
        if (data.Length < patternAt) throw new InvalidDataException("it is shorter than a module's header");
        var length = data[orderAt];
        if (length is 0 or > 128) throw new InvalidDataException($"its song is {length} patterns long");
        var orders = data.AsSpan(orderAt + 2, 128).ToArray();
        // Every pattern the table names is in the file, those past the song's length too.
        var patternCount = orders.Max() + 1;
        if (orders.AsSpan(0, length).ContainsAnyInRange((byte)64, (byte)255) && sampleCount == 15)
            throw new InvalidDataException("its order table names patterns a Soundtracker module cannot hold");
        var patternBytes = 64 * channels * 4;
        if (data.Length < patternAt + patternCount * patternBytes)
            throw new InvalidDataException($"it is too short for the {patternCount} patterns its order table names");

        var patterns = new TrackerPattern[patternCount];
        for (int p = 0; p < patternCount; p++)
        {
            var slots = new TrackerSlot[64 * channels];
            var at = patternAt + p * patternBytes;
            for (int k = 0; k < slots.Length; k++, at += 4)
            {
                int sample = (data[at] & 0xF0) | (data[at + 2] >> 4);
                int period = ((data[at] & 0x0F) << 8) | data[at + 1];
                int effect = data[at + 2] & 0x0F, parameter = data[at + 3];
                // E8x pans in the few trackers that read it, as XM's 8xx does.
                if (effect == 0xE && parameter >> 4 == 8) (effect, parameter) = (8, (parameter & 0x0F) * 17);
                slots[k] = new TrackerSlot(NoteOf(period), (byte)sample, 0, (byte)effect, (byte)parameter);
            }
            patterns[p] = new TrackerPattern(64, slots);
        }

        var instruments = new TrackerInstrument[sampleCount];
        var dataAt = patternAt + patternCount * patternBytes;
        for (int s = 0; s < sampleCount; s++)
        {
            var header = 20 + 30 * s;
            var bytes = Words(data, header + 22);
            var finetune = data[header + 24] & 0x0F;
            var volume = Math.Min(data[header + 25], (byte)64);
            // Soundtracker counted a loop's start in bytes, ProTracker in words.
            var loopStart = sampleCount == 15 ? Words(data, header + 26) / 2 : Words(data, header + 26);
            var loopBytes = Words(data, header + 28);

            var frames = new float[bytes];
            for (int k = 0; k < bytes && dataAt + k < data.Length; k++) frames[k] = (sbyte)data[dataAt + k] / 128f;
            dataAt += bytes;

            var looped = loopBytes > 2 && loopStart < bytes;
            instruments[s] = new TrackerInstrument
            {
                Samples =
                [
                    new TrackerSample
                    {
                        Data = frames,
                        Loop = looped ? TrackerLoop.Forward : TrackerLoop.None,
                        LoopStart = looped ? loopStart : 0,
                        LoopEnd = looped ? Math.Min(bytes, loopStart + loopBytes) : bytes,
                        Volume = volume / 64f,
                        // A signed nibble in eighths of a note, which XM keeps in 128ths.
                        Finetune = (finetune > 7 ? finetune - 16 : finetune) * 16,
                    },
                ],
            };
        }

        var panning = new float[channels];
        for (int c = 0; c < channels; c++) panning[c] = c % 4 is 0 or 3 ? 1 / 3f : 2 / 3f;
        return new TrackerModule
        {
            Name = Encoding.Latin1.GetString(data, 0, 20).TrimEnd('\0', ' '),
            Channels = channels,
            Orders = orders[..length],
            Restart = data[orderAt + 1] < length ? data[orderAt + 1] : 0,
            Patterns = patterns,
            Instruments = instruments,
            Panning = panning,
            Gain = 0.375f,
        };
    }

    // The channels a module's mark says its patterns hold, or 0 for no mark this reader knows.
    private static int ChannelsOf(string mark) => mark switch
    {
        "M.K." or "M!K!" or "M&K!" or "FLT4" or "N.T." => 4,
        "OKTA" or "OCTA" or "CD81" => 8,
        _ when mark[1..] == "CHN" && char.IsAsciiDigit(mark[0]) => mark[0] - '0',
        _ when mark[2..] is "CH" or "CN" && char.IsAsciiDigit(mark[0]) && char.IsAsciiDigit(mark[1]) && int.Parse(mark[..2]) is > 0 and <= 32 => int.Parse(mark[..2]),
        _ => 0,
    };

    // The note whose Amiga period is nearest, C-4 at 428 and an octave for each halving.
    private static byte NoteOf(int period) =>
        period == 0 ? (byte)0 : (byte)Math.Clamp((int)Math.Round(49 + 12 * Math.Log2(428.0 / period)), 1, 96);

    private static int Words(byte[] data, int at) => (data[at] << 8 | data[at + 1]) * 2;
}
