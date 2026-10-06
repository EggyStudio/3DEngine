namespace Engine;

/// <summary>Plays a <see cref="TrackerModule"/> into stereo frames, a row of notes at a time.</summary>
/// <remarks>
/// <para>
/// A row lasts as many ticks as the song's speed, and a tick 2.5 seconds over its tempo in beats a
/// minute, so 125 beats make a tick of 20 milliseconds. Notes start on a row's first tick and
/// effects move pitch and volume on the ticks after it, as FastTracker 2 plays them. This follows
/// raylib's player, jar_xm, in its effects, its frequencies and its mix, where a channel's frame is
/// multiplied by its volume and split by its panning and the channels are summed and clipped, so a
/// song is as loud as raylib plays it. Where jar_xm departs from FastTracker 2, in a volume column it
/// applies on every tick, ping-pong loops it plays forward, a sample offset counted in bytes and a
/// retrigger that subtracts whole volumes, this plays as FastTracker 2 does.
/// </para>
/// <para>
/// A song ends where it comes back to a row it has played outside a pattern's own loop, by its last
/// order running out or by a jump back, as raylib measures its length.
/// </para>
/// </remarks>
internal sealed class TrackerPlayer
{
    // Frames a channel's volume takes to move from silence to full, and a note cut off by another
    // takes to fade out, so neither clicks.
    private const int RampFrames = 128;
    private const int TailFrames = 64;

    private const int KeepVolume = 1, KeepPeriod = 2, KeepPosition = 4;

    private readonly TrackerModule _module;
    private readonly int _rate;
    private readonly TrackerChannel[] _channels;
    // How often each row of each order has been played, which says when the song comes round.
    private readonly byte[] _visits;

    private int _speed, _tempo;
    private float _globalVolume;
    private int _order, _row, _tick, _extraTicks;
    private bool _jump, _break;
    private int _jumpOrder, _jumpRow;
    private int _loops;
    private double _framesLeftInTick;
    private uint _random;
    private float[] _scratch = [];

    public TrackerPlayer(TrackerModule module, int rate)
    {
        _module = module;
        _rate = rate;
        _channels = new TrackerChannel[module.Channels];
        _visits = new byte[module.Orders.Length * 256];
        Reset();
    }

    /// <summary>How often the row last played had been played before, 0 until the song comes round.</summary>
    public int Loops => _loops;

    /// <summary>Goes back to the song's start, every channel silent.</summary>
    public void Reset()
    {
        for (int c = 0; c < _channels.Length; c++)
        {
            var panning = c < _module.Panning.Length ? _module.Panning[c] : 0.5f;
            _channels[c] = new TrackerChannel { Panning = panning, TargetPanning = panning, MixPanning = panning };
        }
        Array.Clear(_visits);
        _speed = _module.Speed;
        _tempo = _module.Tempo;
        _globalVolume = 1f;
        _order = _row = _tick = _extraTicks = 0;
        _jump = _break = false;
        _jumpOrder = _jumpRow = 0;
        _loops = 0;
        _framesLeftInTick = 0;
        _random = 24492;
    }

    private double FramesPerTick => _rate / (_tempo * 0.4);

    /// <summary>The frames the song plays before it comes round, up to <paramref name="limit"/>, read by playing its ticks without mixing them.</summary>
    /// <remarks>Each tick counts its whole frames alone, as jar_xm counts them, so the length is raylib's.</remarks>
    public long CountFrames(long limit)
    {
        Reset();
        long total = 0;
        while (total < limit)
        {
            Tick();
            if (_loops > 0) break;
            total += (long)FramesPerTick;
        }
        Reset();
        return Math.Min(total, limit);
    }

    /// <summary>Writes the song's next frames into <paramref name="stereo"/>, a left and a right sample each.</summary>
    public void Render(Span<float> stereo)
    {
        var gain = _module.Gain;
        for (int i = 0; i + 1 < stereo.Length; i += 2)
        {
            if (_framesLeftInTick <= 0)
            {
                Tick();
                _framesLeftInTick += FramesPerTick;
            }
            _framesLeftInTick--;

            float left = 0, right = 0;
            foreach (var ch in _channels)
            {
                if (ch.TailFrames > 0)
                {
                    var share = ch.TailFrames-- / (float)TailFrames;
                    left += ch.TailLeft * share;
                    right += ch.TailRight * share;
                }
                if (ch.Sample is not { } sample || ch.Instrument is null || ch.Position < 0)
                {
                    ch.LastLeft = ch.LastRight = 0;
                    continue;
                }
                var value = NextFrame(ch, sample) * ch.MixVolume;
                ch.LastLeft = value * (1f - ch.MixPanning);
                ch.LastRight = value * ch.MixPanning;
                left += ch.LastLeft;
                right += ch.LastRight;
                ch.MixVolume = Toward(ch.MixVolume, ch.TargetVolume, 1f / RampFrames);
                ch.MixPanning = Toward(ch.MixPanning, ch.TargetPanning, 1f / RampFrames);
            }
            stereo[i] = Math.Clamp(left * _globalVolume * gain, -1f, 1f);
            stereo[i + 1] = Math.Clamp(right * _globalVolume * gain, -1f, 1f);
        }
    }

    /// <summary>Plays <paramref name="frames"/> frames and drops them, as a seek forward does.</summary>
    public void Skip(long frames)
    {
        if (_scratch.Length == 0) _scratch = new float[4096 * 2];
        while (frames > 0)
        {
            var now = (int)Math.Min(frames, _scratch.Length / 2);
            Render(_scratch.AsSpan(0, now * 2));
            frames -= now;
        }
    }

    // A channel's next frame, read between the two frames around its place, which then steps on,
    // round its loop if it has one.
    private static float NextFrame(TrackerChannel ch, TrackerSample sample)
    {
        var data = sample.Data;
        var at = Math.Min((int)ch.Position, data.Length - 1);
        if (at < 0)
        {
            ch.Position = -1;
            return 0f;
        }
        var next = at + 1;
        var after = sample.Loop switch
        {
            TrackerLoop.Forward => data[next >= sample.LoopEnd ? sample.LoopStart : next],
            TrackerLoop.PingPong => data[Math.Min(next, sample.LoopEnd - 1)],
            _ => next < data.Length ? data[next] : 0f,
        };
        var value = data[at] + (after - data[at]) * (float)(ch.Position - at);

        switch (sample.Loop)
        {
            case TrackerLoop.None:
                ch.Position += ch.Step;
                if (ch.Position >= data.Length) ch.Position = -1;
                break;
            case TrackerLoop.Forward:
                ch.Position += ch.Step;
                if (ch.Position >= sample.LoopEnd)
                    ch.Position = sample.LoopStart + (ch.Position - sample.LoopStart) % (sample.LoopEnd - sample.LoopStart);
                break;
            case TrackerLoop.PingPong:
                // The loop runs from its first frame to its last and back, turning at each.
                double first = sample.LoopStart, last = sample.LoopEnd - 1;
                if (!ch.Backward)
                {
                    ch.Position += ch.Step;
                    if (ch.Position > last)
                    {
                        ch.Backward = true;
                        ch.Position = Math.Max(first, 2 * last - ch.Position);
                    }
                }
                else
                {
                    ch.Position -= ch.Step;
                    if (ch.Position < first)
                    {
                        ch.Backward = false;
                        ch.Position = Math.Min(last, 2 * first - ch.Position);
                    }
                }
                break;
        }
        return value;
    }

    private static float Toward(float value, float goal, float step) =>
        value < goal ? Math.Min(value + step, goal) : Math.Max(value - step, goal);

    // -- Ticks and rows

    private void Tick()
    {
        if (_tick == 0) Row();
        foreach (var ch in _channels)
        {
            Envelopes(ch);
            AutoVibrato(ch);
            if (ch.Arpeggio && !(ch.Current.Effect == 0 && ch.Current.Parameter != 0))
            {
                ch.Arpeggio = false;
                ch.ArpeggioOffset = 0;
                UpdateFrequency(ch);
            }
            if (ch.Vibrating && ch.Current.Effect is not (4 or 6) && ch.Current.Volume >> 4 != 0xB)
            {
                ch.Vibrating = false;
                ch.VibratoOffset = 0;
                UpdateFrequency(ch);
            }
            VolumeColumn(ch);
            EffectTick(ch);

            var panning = ch.Panning + (ch.EnvelopePanning - 0.5f) * (0.5f - MathF.Abs(ch.Panning - 0.5f)) * 2f;
            ch.TargetPanning = Math.Clamp(panning, 0f, 1f);
            ch.TargetVolume = ch.TremorSilent ? 0f : Math.Clamp(ch.Volume + ch.TremoloVolume, 0f, 1f) * ch.Fadeout * ch.EnvelopeVolume;
        }
        if (++_tick >= _speed + _extraTicks)
        {
            _tick = 0;
            _extraTicks = 0;
        }
    }

    private void Row()
    {
        if (_jump)
        {
            _order = _jumpOrder;
            _row = _jumpRow;
            _jump = _break = false;
            _jumpRow = 0;
            OrderChanged();
        }
        else if (_break)
        {
            _order++;
            _row = _jumpRow;
            _break = false;
            _jumpRow = 0;
            OrderChanged();
        }
        var pattern = _module.Patterns[_module.Orders[_order]];
        // A break into a row the next pattern lacks starts it from its first.
        if (_row >= pattern.Rows) _row = 0;

        var inLoop = false;
        for (int c = 0; c < _channels.Length; c++)
        {
            var ch = _channels[c];
            var slot = pattern.Slots[_row * _module.Channels + c];
            ch.Current = slot;
            if (slot.Effect == 0xE && slot.Parameter >> 4 == 0xD) ch.NoteDelay = slot.Parameter & 0x0F;
            else NoteAndInstrument(ch, slot);
            inLoop |= ch.LoopCount > 0;
        }

        // A row played again inside a pattern's loop is not the song coming round.
        if (!inLoop)
        {
            ref var visits = ref _visits[_order * 256 + _row];
            _loops = visits;
            if (visits < byte.MaxValue) visits++;
        }

        _row++;
        if (!_jump && !_break && _row >= pattern.Rows)
        {
            _order++;
            _row = 0;
            OrderChanged();
        }
    }

    // A song that runs off its last order goes back to its restart, at its first speed, tempo and
    // volume.
    private void OrderChanged()
    {
        if (_order < _module.Orders.Length) return;
        _order = _module.Restart;
        _speed = _module.Speed;
        _tempo = _module.Tempo;
        _globalVolume = 1f;
    }

    // -- Notes

    private static bool HasTonePortamento(TrackerSlot slot) => slot.Effect is 3 or 5 || slot.Volume >> 4 == 0xF;

    private static float NoteOf(int note, TrackerSample sample) => note + sample.RelativeNote + sample.Finetune / 128f - 1f;

    private void NoteAndInstrument(TrackerChannel ch, TrackerSlot slot)
    {
        var tonePortamento = HasTonePortamento(slot);
        if (slot.Instrument > 0)
        {
            if (tonePortamento && ch.Instrument is not null && ch.Sample is not null)
                Trigger(ch, KeepPeriod | KeepPosition);
            else if (slot.Instrument > _module.Instruments.Length)
            {
                Cut(ch);
                ch.Instrument = null;
                ch.Sample = null;
            }
            else
            {
                ch.Instrument = _module.Instruments[slot.Instrument - 1];
                // An instrument with no note starts its envelopes and volume again where the sample is.
                if (slot.Note == 0 && ch.Sample is not null) Trigger(ch, KeepPosition);
            }
        }

        if (slot.Note is > 0 and < 97)
        {
            var instrument = ch.Instrument;
            if (tonePortamento && instrument is not null && ch.Sample is not null)
            {
                ch.Note = NoteOf(slot.Note, ch.Sample);
                ch.TargetPeriod = PeriodOf(ch.Note);
            }
            else if (instrument is null || instrument.Samples.Length == 0 || instrument.SampleOfNote[slot.Note - 1] >= instrument.Samples.Length)
                Cut(ch);
            else
            {
                ch.Sample = instrument.Samples[instrument.SampleOfNote[slot.Note - 1]];
                ch.OriginalNote = ch.Note = NoteOf(slot.Note, ch.Sample);
                Trigger(ch, slot.Instrument > 0 ? 0 : KeepVolume);
            }
        }
        else if (slot.Note == 97)
            KeyOff(ch);

        RowEffect(ch, slot);
    }

    private void Trigger(TrackerChannel ch, int keep)
    {
        if ((keep & KeepPosition) == 0)
        {
            // What the channel was playing fades out under the new note, which rises from silence.
            if (ch.LastLeft != 0 || ch.LastRight != 0)
            {
                ch.TailLeft = ch.LastLeft;
                ch.TailRight = ch.LastRight;
                ch.TailFrames = TailFrames;
            }
            ch.Position = 0;
            ch.Backward = false;
            ch.MixVolume = 0;
        }
        if ((keep & KeepVolume) == 0 && ch.Sample is not null) ch.Volume = ch.Sample.Volume;
        if (_module.NotesTakeSamplePanning && ch.Sample is not null) ch.Panning = ch.Sample.Panning;
        ch.Sustained = true;
        ch.Fadeout = ch.EnvelopeVolume = 1f;
        ch.EnvelopePanning = 0.5f;
        ch.VolumeTick = ch.PanningTick = 0;
        ch.VibratoOffset = 0;
        ch.TremoloVolume = 0;
        ch.TremorSilent = false;
        ch.AutoVibratoTicks = 0;
        if (ch.VibratoRetrigger) ch.VibratoTicks = 0;
        if (ch.TremoloRetrigger) ch.TremoloTicks = 0;
        if ((keep & KeepPeriod) == 0)
        {
            ch.Period = PeriodOf(ch.Note);
            UpdateFrequency(ch);
        }
    }

    private static void Cut(TrackerChannel ch) => ch.Volume = 0;

    // Lets a note go, so its envelopes leave their sustain and its volume fades, or stops it where
    // its instrument has no volume envelope to fade by.
    private static void KeyOff(TrackerChannel ch)
    {
        ch.Sustained = false;
        if (ch.Instrument is not { Volume.On: true }) Cut(ch);
    }

    // -- Pitch

    private float PeriodOf(float note) => _module.LinearFrequencies ? 7680f - note * 64f : AmigaPeriod(note);

    // An Amiga period for a note, 1712 at C-2 and halving each octave up.
    private static float AmigaPeriod(float note) => 1712f * MathF.Pow(2f, (24f - note) / 12f);

    private static float AmigaFrequency(float period) => period <= 0 ? 0f : 7093789.2f / (period * 2f);

    // A period's frequency with a note's worth of offset from an arpeggio or a vibrato.
    private float FrequencyOf(float period, float offset)
    {
        if (_module.LinearFrequencies) return 8363f * MathF.Pow(2f, (4608f - (period - 64f * offset)) / 768f);
        if (offset == 0 || period <= 0) return AmigaFrequency(period);
        var note = 24f + 12f * MathF.Log2(1712f / period);
        return AmigaFrequency(AmigaPeriod(note + offset));
    }

    private void UpdateFrequency(TrackerChannel ch)
    {
        var offset = ch.ArpeggioOffset > 0 ? ch.ArpeggioOffset : ch.VibratoOffset + ch.AutoVibratoOffset;
        ch.Step = FrequencyOf(ch.Period, offset) / _rate;
    }

    // Moves the period by a slide's parameter, four times as far in a linear table, whose periods
    // are four times as fine.
    private void PitchSlide(TrackerChannel ch, float by)
    {
        ch.Period = Math.Max(0f, ch.Period + by * (_module.LinearFrequencies ? 4f : 1f));
        UpdateFrequency(ch);
    }

    private void TonePortamento(TrackerChannel ch)
    {
        if (ch.TargetPeriod == 0 || ch.Period == ch.TargetPeriod) return;
        var by = ch.TonePortamento * (_module.LinearFrequencies ? 4f : 1f);
        ch.Period = ch.Period < ch.TargetPeriod ? Math.Min(ch.Period + by, ch.TargetPeriod) : Math.Max(ch.Period - by, ch.TargetPeriod);
        UpdateFrequency(ch);
    }

    // A waveform's value at a step of its 64, for vibrato and tremolo, the waveforms numbered as
    // jar_xm numbers them, sine, ramp down, square, random and ramp up.
    private float Waveform(int type, int step)
    {
        step &= 0x3F;
        switch (type)
        {
            case 1: return (0x20 - step) / (float)0x20;
            case 2: return step >= 0x20 ? 1f : -1f;
            case 3:
                _random = _random * 1103515245 + 12345;
                return ((_random >> 16) & 0x7FFF) / (float)0x4000 - 1f;
            case 4: return (step - 0x20) / (float)0x20;
            default: return -MathF.Sin(2f * MathF.PI * step / 0x40);
        }
    }

    private void AutoVibrato(TrackerChannel ch)
    {
        if (ch.Instrument is not { VibratoDepth: > 0 } instrument) return;
        var sweep = ch.AutoVibratoTicks < instrument.VibratoSweep ? ch.AutoVibratoTicks / (float)instrument.VibratoSweep : 1f;
        var step = (ch.AutoVibratoTicks++ * instrument.VibratoRate) >> 2;
        ch.AutoVibratoOffset = 0.25f * Waveform(instrument.VibratoType, step) * instrument.VibratoDepth / 15f * sweep;
        UpdateFrequency(ch);
    }

    private void Vibrato(TrackerChannel ch)
    {
        ch.Vibrating = true;
        var step = ch.VibratoTicks++ * (ch.Vibrato >> 4);
        ch.VibratoOffset = 2f * Waveform(ch.VibratoWave, step) * (ch.Vibrato & 0x0F) / 15f;
        UpdateFrequency(ch);
    }

    // -- Volume and panning

    // Up by the high nibble, or down by the low one where the high is 0, in 64ths.
    private static void VolumeSlide(TrackerChannel ch, int parameter)
    {
        var by = (parameter & 0xF0) != 0 ? (parameter >> 4) / 64f : -(parameter & 0x0F) / 64f;
        ch.Volume = Math.Clamp(ch.Volume + by, 0f, 1f);
    }

    private static void PanningSlide(TrackerChannel ch, int parameter)
    {
        var by = (parameter & 0xF0) != 0 ? (parameter >> 4) / 255f : -(parameter & 0x0F) / 255f;
        ch.Panning = Math.Clamp(ch.Panning + by, 0f, 1f);
    }

    private static void Envelopes(TrackerChannel ch)
    {
        if (ch.Instrument is not { } instrument) return;
        if (instrument.Volume.On)
        {
            if (!ch.Sustained) ch.Fadeout = Math.Max(0f, ch.Fadeout - instrument.Fadeout / 65536f);
            Envelope(ch, instrument.Volume, ref ch.VolumeTick, ref ch.EnvelopeVolume);
        }
        if (instrument.Panning.On) Envelope(ch, instrument.Panning, ref ch.PanningTick, ref ch.EnvelopePanning);
    }

    // An envelope's value at its tick, between the points around it, the tick moving on unless the
    // note is held at the sustain point, and back to the loop's start at its end.
    private static void Envelope(TrackerChannel ch, TrackerEnvelope envelope, ref int tick, ref float value)
    {
        var points = envelope.Points;
        if (points.Length < 2)
        {
            if (points.Length == 1) value = Math.Min(points[0].Value / 64f, 1f);
            return;
        }
        if (envelope.Loops && envelope.LoopEnd < points.Length && envelope.LoopStart <= envelope.LoopEnd)
        {
            var (start, end) = (points[envelope.LoopStart].Tick, points[envelope.LoopEnd].Tick);
            if (tick >= end) tick -= end - start;
        }
        if (tick >= points[^1].Tick) value = points[^1].Value / 64f;
        else
            for (int j = 0; j < points.Length - 1; j++)
            {
                var (a, b) = (points[j], points[j + 1]);
                if (a.Tick > tick || b.Tick < tick) continue;
                var t = b.Tick > a.Tick ? (tick - a.Tick) / (float)(b.Tick - a.Tick) : 0f;
                value = (a.Value + (b.Value - a.Value) * t) / 64f;
                break;
            }
        var held = ch.Sustained && envelope.Sustains && envelope.Sustain < points.Length && tick == points[envelope.Sustain].Tick;
        if (!held) tick++;
    }

    // The volume column, which sets a volume, a vibrato, a panning or a tone portamento on the
    // row's first tick and slides the volume or the panning on the others.
    private void VolumeColumn(TrackerChannel ch)
    {
        var column = ch.Current.Volume;
        var first = _tick == 0;
        var low = column & 0x0F;
        switch (column >> 4)
        {
            case >= 1 and <= 4 when first: ch.Volume = (column - 0x10) / 64f; break;
            case 5 when first && column == 0x50: ch.Volume = 1f; break;
            case 6 when !first: VolumeSlide(ch, low); break;
            case 7 when !first: VolumeSlide(ch, low << 4); break;
            case 8 when first: VolumeSlide(ch, low); break;
            case 9 when first: VolumeSlide(ch, low << 4); break;
            case 0xA when first: ch.Vibrato = (ch.Vibrato & 0x0F) | (low << 4); break;
            case 0xB:
                if (first && low != 0) ch.Vibrato = (ch.Vibrato & 0xF0) | low;
                else if (!first) Vibrato(ch);
                break;
            case 0xC when first: ch.Panning = low / 15f; break;
            case 0xD when !first: PanningSlide(ch, low); break;
            case 0xE when !first: PanningSlide(ch, low << 4); break;
            case 0xF:
                if (first && low != 0) ch.TonePortamento = low << 4;
                else if (!first) TonePortamento(ch);
                break;
        }
    }

    // -- Effects

    // The effect column on a row's first tick, where most set what the later ticks do.
    private void RowEffect(TrackerChannel ch, TrackerSlot slot)
    {
        int parameter = slot.Parameter, low = parameter & 0x0F;
        switch (slot.Effect)
        {
            case 1 when parameter > 0: ch.PortamentoUp = parameter; break;
            case 2 when parameter > 0: ch.PortamentoDown = parameter; break;
            case 3 when parameter > 0: ch.TonePortamento = parameter; break;
            case 4:
                if (low != 0) ch.Vibrato = (ch.Vibrato & 0xF0) | low;
                if (parameter >> 4 != 0) ch.Vibrato = (parameter & 0xF0) | (ch.Vibrato & 0x0F);
                break;
            case 5 or 6 or 0xA when parameter > 0: ch.VolumeSlide = parameter; break;
            case 7:
                if (low != 0) ch.Tremolo = (ch.Tremolo & 0xF0) | low;
                if (parameter >> 4 != 0) ch.Tremolo = (parameter & 0xF0) | (ch.Tremolo & 0x0F);
                break;
            case 8: ch.Panning = parameter / 255f; break;
            case 9 when ch.Sample is { } sample:
                // An offset into the sample in 256 frames, past its end silent unless it loops.
                var offset = parameter * 256;
                ch.Position = offset < sample.Data.Length ? offset : sample.Loop == TrackerLoop.None ? -1 : sample.LoopStart;
                break;
            case 0xB when parameter < _module.Orders.Length:
                _jump = true;
                _jumpOrder = parameter;
                break;
            case 0xC: ch.Volume = Math.Min(parameter, 0x40) / 64f; break;
            case 0xD:
                _break = true;
                _jumpRow = (parameter >> 4) * 10 + low;
                break;
            case 0xE: ExtendedRowEffect(ch, slot, parameter >> 4, low); break;
            case 0xF when parameter > 0:
                if (parameter < 0x20) _speed = parameter;
                else _tempo = parameter;
                break;
            case 16: _globalVolume = Math.Min(parameter, 0x40) / 64f; break;
            case 17 when parameter > 0: ch.GlobalVolumeSlide = parameter; break;
            case 21: ch.VolumeTick = ch.PanningTick = parameter; break;
            case 25 when parameter > 0: ch.PanningSlide = parameter; break;
            case 27 when parameter > 0:
                ch.MultiRetrig = parameter >> 4 == 0 ? (ch.MultiRetrig & 0xF0) | low : parameter;
                break;
            case 29 when parameter > 0: ch.Tremor = parameter; break;
            case 33 when parameter >> 4 == 1:
                if (low != 0) ch.ExtraFineUp = low;
                PitchSlide(ch, -ch.ExtraFineUp / 4f);
                break;
            case 33 when parameter >> 4 == 2:
                if (low != 0) ch.ExtraFineDown = low;
                PitchSlide(ch, ch.ExtraFineDown / 4f);
                break;
        }
    }

    private void ExtendedRowEffect(TrackerChannel ch, TrackerSlot slot, int command, int low)
    {
        switch (command)
        {
            case 1:
                if (low != 0) ch.FinePortamentoUp = low;
                PitchSlide(ch, -ch.FinePortamentoUp);
                break;
            case 2:
                if (low != 0) ch.FinePortamentoDown = low;
                PitchSlide(ch, ch.FinePortamentoDown);
                break;
            case 4:
                ch.VibratoWave = low & 3;
                ch.VibratoRetrigger = (low & 4) == 0;
                break;
            case 5 when slot.Note is > 0 and < 97 && ch.Sample is { } sample:
                ch.Note = slot.Note + sample.RelativeNote + ((low - 8) << 4) / 128f - 1f;
                ch.Period = PeriodOf(ch.Note);
                UpdateFrequency(ch);
                break;
            case 6:
                // A pattern loop: E60 marks its start, and E6x plays back to it x times.
                if (low == 0) ch.LoopRow = _row;
                else if (ch.LoopCount == low)
                {
                    ch.LoopCount = 0;
                    _jump = false;
                }
                else
                {
                    ch.LoopCount++;
                    _jump = true;
                    _jumpRow = ch.LoopRow;
                    _jumpOrder = _order;
                }
                break;
            case 7:
                ch.TremoloWave = low & 3;
                ch.TremoloRetrigger = (low & 4) == 0;
                break;
            case 0xA:
                if (low != 0) ch.FineVolumeSlide = low;
                VolumeSlide(ch, ch.FineVolumeSlide << 4);
                break;
            case 0xB:
                if (low != 0) ch.FineVolumeSlide = low;
                VolumeSlide(ch, ch.FineVolumeSlide);
                break;
            case 0xD when slot.Note == 0 && slot.Instrument == 0:
                // A delayed row with no note plays the last note again, from its start where the
                // delay is not 0.
                if (low != 0)
                {
                    ch.Note = ch.OriginalNote;
                    Trigger(ch, KeepVolume);
                }
                else Trigger(ch, KeepVolume | KeepPeriod | KeepPosition);
                break;
            case 0xE: _extraTicks = low * _speed; break;
        }
    }

    // The effect column on the ticks after the first, and the few that act on a tick of their own.
    private void EffectTick(TrackerChannel ch)
    {
        var slot = ch.Current;
        int parameter = slot.Parameter, low = parameter & 0x0F;
        var later = _tick > 0;
        switch (slot.Effect)
        {
            case 0 when parameter > 0:
                // An arpeggio plays the note, then the note raised by x, then by y, round again.
                ch.ArpeggioOffset = (_tick % 3) switch { 1 => parameter >> 4, 2 => low, _ => 0 };
                ch.Arpeggio = ch.ArpeggioOffset != 0;
                UpdateFrequency(ch);
                break;
            case 1 when later: PitchSlide(ch, -ch.PortamentoUp); break;
            case 2 when later: PitchSlide(ch, ch.PortamentoDown); break;
            case 3 when later: TonePortamento(ch); break;
            case 4 when later: Vibrato(ch); break;
            case 5 when later:
                TonePortamento(ch);
                VolumeSlide(ch, ch.VolumeSlide);
                break;
            case 6 when later:
                Vibrato(ch);
                VolumeSlide(ch, ch.VolumeSlide);
                break;
            case 7 when later:
                var step = ch.TremoloTicks++ * (ch.Tremolo >> 4);
                ch.TremoloVolume = -Waveform(ch.TremoloWave, step) * (ch.Tremolo & 0x0F) / 15f;
                break;
            case 0xA when later: VolumeSlide(ch, ch.VolumeSlide); break;
            case 0xE when parameter >> 4 == 9:
                if (later && low != 0 && _tick % low == 0)
                {
                    Trigger(ch, 0);
                    Envelopes(ch);
                }
                break;
            case 0xE when parameter >> 4 == 0xC:
                if (low == _tick) Cut(ch);
                break;
            case 0xE when parameter >> 4 == 0xD:
                if (ch.NoteDelay == _tick)
                {
                    NoteAndInstrument(ch, slot);
                    Envelopes(ch);
                }
                break;
            case 17 when later:
                var global = ch.GlobalVolumeSlide;
                if ((global & 0xF0) != 0 && (global & 0x0F) != 0) break;
                _globalVolume = Math.Clamp(_globalVolume + ((global & 0xF0) != 0 ? (global >> 4) / 64f : -(global & 0x0F) / 64f), 0f, 1f);
                break;
            case 20:
                if (_tick == parameter) KeyOff(ch);
                break;
            case 25 when later: PanningSlide(ch, ch.PanningSlide); break;
            case 27 when later:
                var every = ch.MultiRetrig & 0x0F;
                if (every == 0 || _tick % every != 0) break;
                // The volume after each retrigger, changed as the high nibble says, in 64ths.
                var volume = (ch.MultiRetrig >> 4) switch
                {
                    1 => ch.Volume - 1 / 64f, 2 => ch.Volume - 2 / 64f, 3 => ch.Volume - 4 / 64f, 4 => ch.Volume - 8 / 64f,
                    5 => ch.Volume - 16 / 64f, 6 => ch.Volume * 2 / 3f, 7 => ch.Volume / 2f,
                    9 => ch.Volume + 1 / 64f, 0xA => ch.Volume + 2 / 64f, 0xB => ch.Volume + 4 / 64f, 0xC => ch.Volume + 8 / 64f,
                    0xD => ch.Volume + 16 / 64f, 0xE => ch.Volume * 1.5f, 0xF => ch.Volume * 2f,
                    _ => ch.Volume,
                };
                Trigger(ch, 0);
                ch.Volume = Math.Clamp(volume, 0f, 1f);
                break;
            case 29 when later:
                // A tremor sounds for x + 1 ticks, then is silent for y + 1.
                ch.TremorSilent = (_tick - 1) % ((ch.Tremor >> 4) + (ch.Tremor & 0x0F) + 2) > ch.Tremor >> 4;
                break;
        }
    }
}
