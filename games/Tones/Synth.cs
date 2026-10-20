using Engine;

/// <summary>
/// The instrument, which writes its own samples, an organ of three partials or a plucked string
/// for each key held, its notes shaped by an envelope, and drum hits mixed in from waves the
/// program read as samples.
/// </summary>
public sealed class Synth(int sampleRate)
{
    public readonly int SampleRate = sampleRate;
    public bool Plucked;

    // A note sounding, from its key down through its release.
    private sealed class Voice(float frequency)
    {
        public readonly float Frequency = frequency;
        public float Phase, Level;
        public bool Held = true, Rising = true;
        public float[]? String;
        public int At;
    }

    private readonly Dictionary<int, Voice> _held = [];
    private readonly List<Voice> _voices = [];
    private readonly List<(float[] Samples, int At)> _hits = [];
    private readonly Random _random = new(5);

    // An envelope's rise and fall a sample, from silent to full in 10 ms, down to what it holds
    // at in 150, and to silence in 350 once let go.
    private float Rise => 1f / (0.01f * SampleRate);
    private float Fall => 0.4f / (0.15f * SampleRate);
    private float Release => 0.6f / (0.35f * SampleRate);
    private const float Sustain = 0.6f;

    /// <summary>Starts a note by its number, 60 being middle C, unless that note is held.</summary>
    public bool NoteOn(int note)
    {
        if (_held.ContainsKey(note)) return false;
        var voice = new Voice(440 * MathF.Pow(2, (note - 69) / 12f));
        if (Plucked)
        {
            // A string is a delay line a period long, filled with noise it then smooths away.
            voice.String = new float[Math.Max(2, (int)(SampleRate / voice.Frequency))];
            for (int i = 0; i < voice.String.Length; i++) voice.String[i] = _random.NextSingle() * 2 - 1;
            voice.Level = 1;
        }
        _held[note] = voice;
        _voices.Add(voice);
        return true;
    }

    /// <summary>Lets a note go, into its release.</summary>
    public void NoteOff(int note)
    {
        if (_held.Remove(note, out var voice)) voice.Held = false;
    }

    /// <summary>Mixes a drum's samples in from where the next samples are written.</summary>
    public void Hit(float[] samples) => _hits.Add((samples, 0));

    /// <summary>How many notes sound, held or fading.</summary>
    public int Sounding => _voices.Count;

    /// <summary>Writes the next samples, voices and hits summed, held within -1 and 1.</summary>
    public void Write(Span<float> into)
    {
        into.Clear();
        foreach (var voice in _voices)
            for (int i = 0; i < into.Length; i++) into[i] += Next(voice) * 0.3f;
        _voices.RemoveAll(v => !v.Held && v.Level <= 0);
        for (int h = _hits.Count - 1; h >= 0; h--)
        {
            var (samples, at) = _hits[h];
            var n = Math.Min(into.Length, samples.Length - at);
            for (int i = 0; i < n; i++) into[i] += samples[at + i] * 0.8f;
            if (at + n >= samples.Length) _hits.RemoveAt(h);
            else _hits[h] = (samples, at + n);
        }
        for (int i = 0; i < into.Length; i++) into[i] = MathF.Tanh(into[i]);
    }

    private float Next(Voice voice)
    {
        if (voice.String is { } line)
        {
            // Each sample read is the average of two, so the string loses its highs as it rings,
            // and it fades faster once its key is let go.
            var a = line[voice.At];
            var b = line[(voice.At + 1) % line.Length];
            line[voice.At] = (a + b) * 0.5f * (voice.Held ? 0.998f : 0.97f);
            voice.At = (voice.At + 1) % line.Length;
            voice.Level = voice.Held ? 1 : MathF.Max(0, voice.Level - Release);
            return a * 0.8f;
        }
        if (voice.Held && voice.Rising)
        {
            voice.Level = MathF.Min(1, voice.Level + Rise);
            voice.Rising = voice.Level < 1;
        }
        else if (voice.Held) voice.Level = MathF.Max(Sustain, voice.Level - Fall);
        else voice.Level = MathF.Max(0, voice.Level - Release);
        voice.Phase += voice.Frequency / SampleRate;
        if (voice.Phase >= 1) voice.Phase -= 1;
        var t = voice.Phase * MathF.Tau;
        return voice.Level * (MathF.Sin(t) + 0.5f * MathF.Sin(2 * t) + 0.25f * MathF.Sin(3 * t)) / 1.75f;
    }
}
