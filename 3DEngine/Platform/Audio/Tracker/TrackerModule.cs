namespace Engine;

/// <summary>
/// A tracker module, an XM or MOD file read whole, with its patterns of notes, the order they play in
/// and its instruments' samples, which <see cref="TrackerPlayer"/> plays.
/// </summary>
/// <remarks>
/// One model holds both formats, since XM grew out of MOD. A MOD's 31 samples are
/// instruments of one sample each with no envelopes, its notes are Amiga periods turned into note
/// numbers, and its channels are panned left and right by their place, as the Amiga's four voices
/// were.
/// </remarks>
internal sealed class TrackerModule
{
    /// <summary>The song's name, from the file.</summary>
    public string Name { get; init; } = "";

    /// <summary>How many channels each pattern has.</summary>
    public int Channels { get; init; }

    /// <summary>The patterns by number, in the order the song plays them.</summary>
    public byte[] Orders { get; init; } = [];

    /// <summary>The place in <see cref="Orders"/> a song that runs off its end goes back to.</summary>
    public int Restart { get; init; }

    public TrackerPattern[] Patterns { get; init; } = [];

    /// <summary>The instruments, instrument 1 at index 0, as a pattern names them.</summary>
    public TrackerInstrument[] Instruments { get; init; } = [];

    /// <summary>Whether pitches slide in equal steps of a note (XM's linear table) or in Amiga periods.</summary>
    public bool LinearFrequencies { get; init; }

    /// <summary>Ticks a row when the song starts.</summary>
    public int Speed { get; init; } = 6;

    /// <summary>Beats a minute when the song starts, a tick lasting 2.5 seconds over it.</summary>
    public int Tempo { get; init; } = 125;

    /// <summary>Each channel's panning when the song starts, from 0, left, to 1, right.</summary>
    public float[] Panning { get; init; } = [];

    /// <summary>Whether a note takes its sample's panning, as in XM, or keeps its channel's, as in MOD.</summary>
    public bool NotesTakeSamplePanning { get; init; }

    /// <summary>What the mix is multiplied by, so a MOD's hard panned channels are as loud as raylib plays them.</summary>
    public float Gain { get; init; } = 1f;
}

/// <summary>A pattern's rows, each a slot for every channel, row after row.</summary>
internal sealed record TrackerPattern(int Rows, TrackerSlot[] Slots);

/// <summary>
/// One channel's cell of a row, a note from 1 (C-0) to 96, or 97 to let the note go, an instrument
/// from 1, the volume column, and an effect with its parameter, as XM numbers them.
/// </summary>
internal readonly record struct TrackerSlot(byte Note, byte Instrument, byte Volume, byte Effect, byte Parameter);

/// <summary>An instrument, which of its samples each note plays and how its volume and panning move while a note sounds.</summary>
internal sealed class TrackerInstrument
{
    /// <summary>For each of the 96 notes, the index into <see cref="Samples"/> it plays.</summary>
    public byte[] SampleOfNote { get; init; } = new byte[96];

    public TrackerSample[] Samples { get; init; } = [];

    public TrackerEnvelope Volume { get; init; } = new();

    public TrackerEnvelope Panning { get; init; } = new();

    /// <summary>The instrument's own vibrato, its waveform, as <see cref="TrackerPlayer"/> numbers them, how many ticks it takes to reach its depth, its depth and its rate.</summary>
    public int VibratoType { get; init; }

    public int VibratoSweep { get; init; }

    public int VibratoDepth { get; init; }

    public int VibratoRate { get; init; }

    /// <summary>How fast the volume falls after the note is let go, in 65,536ths a tick.</summary>
    public int Fadeout { get; init; }
}

/// <summary>A volume or panning envelope, points of a value from 0 to 64 at a tick, with a point held while the note is down and a loop.</summary>
internal sealed class TrackerEnvelope
{
    public (int Tick, int Value)[] Points { get; init; } = [];

    public bool On { get; init; }

    public bool Sustains { get; init; }

    public int Sustain { get; init; }

    public bool Loops { get; init; }

    public int LoopStart { get; init; }

    public int LoopEnd { get; init; }
}

/// <summary>How a sample goes on past its loop's end.</summary>
internal enum TrackerLoop
{
    /// <summary>It ends.</summary>
    None,

    /// <summary>It starts its loop again.</summary>
    Forward,

    /// <summary>It plays its loop backward, then forward again.</summary>
    PingPong,
}

/// <summary>A sample's frames, from -1 to 1, with its loop, its volume and how far it is tuned from the note played.</summary>
internal sealed class TrackerSample
{
    public float[] Data { get; init; } = [];

    public TrackerLoop Loop { get; init; }

    /// <summary>The first frame of the loop.</summary>
    public int LoopStart { get; init; }

    /// <summary>The frame after the loop's last.</summary>
    public int LoopEnd { get; init; }

    /// <summary>The volume a note starts at, from 0 to 1.</summary>
    public float Volume { get; init; } = 1f;

    /// <summary>The panning a note in XM starts at, from 0 to 1.</summary>
    public float Panning { get; init; } = 0.5f;

    /// <summary>Notes the sample is played above the note in the pattern.</summary>
    public int RelativeNote { get; init; }

    /// <summary>Its tuning in 128ths of a note, from -128 to 127.</summary>
    public int Finetune { get; init; }
}
