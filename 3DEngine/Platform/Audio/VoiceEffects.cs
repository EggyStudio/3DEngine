namespace Engine;

/// <summary>The room every voice's reverb sounds, its size and decay, which the server sets and a voice's effects read as they run.</summary>
internal sealed class ReverbRoom
{
    private volatile float _size = AudioServer.DefaultReverbSize, _decay = AudioServer.DefaultReverbDecay;

    /// <summary>The room's size in world units, which its echoes' delays grow with.</summary>
    public float Size
    {
        get => _size;
        set => _size = float.IsNaN(value) ? AudioServer.DefaultReverbSize : Math.Clamp(value, 1f, 100f);
    }

    /// <summary>The seconds the room's sound takes to fall by sixty decibels.</summary>
    public float Decay
    {
        get => _decay;
        set => _decay = float.IsNaN(value) ? AudioServer.DefaultReverbDecay : Math.Clamp(value, 0.05f, 20f);
    }
}

/// <summary>
/// One voice's low-pass and reverb, run over its samples a frame at a time as they are fed to the
/// device, with no allocation once its reverb is made.
/// </summary>
/// <remarks>
/// <para>
/// The low-pass is a second-order filter (Robert Bristow-Johnson's), which takes twelve decibels an
/// octave off above its cutoff, as a wall between a sound and its hearer muffles it. The reverb is
/// Schroeder's and Freeverb's, eight combs in parallel, each with a low-pass in its loop as a room's
/// walls take the highs first, into four allpasses in series. The room's size scales the combs'
/// delays from Freeverb's at ten units, and its decay sets each comb's feedback so the sound falls
/// sixty decibels over it. A reverb is linear, so one a voice sounds as one the voices share would.
/// </para>
/// <para>
/// The cutoff, the share and the room are written by the program's thread and read by the audio
/// thread each frame, so a change takes effect within the block being fed.
/// </para>
/// </remarks>
internal sealed class VoiceEffects(int sampleRate, ReverbRoom room)
{
    // Freeverb's comb and allpass delays at 44.1 kHz, in seconds.
    private static readonly float[] CombSeconds = [1116 / 44100f, 1188 / 44100f, 1277 / 44100f, 1356 / 44100f,
        1422 / 44100f, 1491 / 44100f, 1557 / 44100f, 1617 / 44100f];
    private static readonly float[] AllpassSeconds = [556 / 44100f, 441 / 44100f, 341 / 44100f, 225 / 44100f];
    private const float Damping = 0.2f;
    private const float InputGain = 0.1f;

    private volatile float _cutoff, _share;

    // The low-pass's coefficients for the cutoff they were made for, and each channel's history.
    private float _madeFor = -1, _b0, _b1, _b2, _a1, _a2;
    private float _x1L, _x2L, _y1L, _y2L, _x1R, _x2R, _y1R, _y2R;

    // The reverb's delay lines, made the first time a share asks for one and again for another
    // room, with each comb's feedback and filter state.
    private float[][]? _combs, _allpasses;
    private readonly int[] _combAt = new int[8], _allpassAt = new int[4];
    private readonly float[] _feedback = new float[8], _filtered = new float[8];
    private float _roomSize = -1, _roomDecay = -1;

    /// <summary>The frequency above which the voice is muffled, or 0 for none.</summary>
    public float Cutoff
    {
        get => _cutoff;
        set => _cutoff = float.IsNaN(value) || value <= 0 ? 0 : value;
    }

    /// <summary>The share of the voice sent through the room's reverb, from 0 for none to 1.</summary>
    public float Share
    {
        get => _share;
        set => _share = float.IsNaN(value) ? 0 : Math.Clamp(value, 0f, 1f);
    }

    /// <summary>Whether the voice sounds the room at all, which keeps it playing for its tail once its samples end.</summary>
    public bool Reverberates => _share > 0;

    /// <summary>How many frames the room's tail takes to die once the samples end.</summary>
    public int TailFrames => (int)(room.Decay * sampleRate) + (int)(CombSeconds[^1] * Scale(room.Size) * sampleRate);

    /// <summary>Starts a frame of the block, the low-pass's coefficients made again where the cutoff moved and the reverb where the room did.</summary>
    public void Begin()
    {
        var cutoff = _cutoff;
        if (cutoff != _madeFor) MakeLowPass(cutoff);
        if (_share > 0 && (room.Size != _roomSize || room.Decay != _roomDecay || _combs is null)) MakeReverb();
    }

    /// <summary>A stereo frame through the low-pass, and the room's sound of it to add to both sides, which is 0 without a share.</summary>
    public float Run(ref float left, ref float right)
    {
        if (_madeFor > 0)
        {
            left = LowPass(left, ref _x1L, ref _x2L, ref _y1L, ref _y2L);
            right = LowPass(right, ref _x1R, ref _x2R, ref _y1R, ref _y2R);
        }
        var share = _share;
        return share > 0 && _combs is not null ? Reverb((left + right) * 0.5f) * share : 0;
    }

    private float LowPass(float x, ref float x1, ref float x2, ref float y1, ref float y2)
    {
        var y = _b0 * x + _b1 * x1 + _b2 * x2 - _a1 * y1 - _a2 * y2;
        (x2, x1, y2, y1) = (x1, x, y1, y);
        return y;
    }

    // A cutoff at or past nine tenths of the highest frequency the rate holds leaves the voice as
    // it is, the filter turned off.
    private void MakeLowPass(float cutoff)
    {
        _madeFor = cutoff > 0 && cutoff < sampleRate * 0.45f ? cutoff : 0;
        if (_madeFor == 0) return;
        var w = 2 * MathF.PI * cutoff / sampleRate;
        var alpha = MathF.Sin(w) / (2 * MathF.Sqrt(0.5f));
        var a0 = 1 + alpha;
        var cos = MathF.Cos(w);
        (_b0, _b1, _b2) = ((1 - cos) / 2 / a0, (1 - cos) / a0, (1 - cos) / 2 / a0);
        (_a1, _a2) = (-2 * cos / a0, (1 - alpha) / a0);
    }

    private static float Scale(float size) => Math.Clamp(size / 10f, 0.2f, 4f);

    private void MakeReverb()
    {
        (_roomSize, _roomDecay) = (room.Size, room.Decay);
        var scale = Scale(_roomSize);
        _combs ??= new float[8][];
        _allpasses ??= new float[4][];
        for (int i = 0; i < 8; i++)
        {
            var length = Math.Max(1, (int)(CombSeconds[i] * scale * sampleRate));
            if (_combs[i]?.Length != length) (_combs[i], _combAt[i], _filtered[i]) = (new float[length], 0, 0);
            // A comb's echo comes back once a delay, so it falls sixty decibels in decay seconds at
            // a gain of ten to the minus three each decay's worth of delays.
            _feedback[i] = MathF.Pow(10, -3 * length / (_roomDecay * sampleRate));
        }
        for (int i = 0; i < 4; i++)
        {
            var length = Math.Max(1, (int)(AllpassSeconds[i] * sampleRate));
            if (_allpasses[i]?.Length != length) (_allpasses[i], _allpassAt[i]) = (new float[length], 0);
        }
    }

    private float Reverb(float x)
    {
        var input = x * InputGain;
        var sum = 0f;
        for (int i = 0; i < 8; i++)
        {
            var line = _combs![i];
            ref var at = ref _combAt[i];
            var y = line[at];
            _filtered[i] = y * (1 - Damping) + _filtered[i] * Damping;
            line[at] = input + _filtered[i] * _feedback[i];
            if (++at == line.Length) at = 0;
            sum += y;
        }
        for (int i = 0; i < 4; i++)
        {
            var line = _allpasses![i];
            ref var at = ref _allpassAt[i];
            var held = line[at];
            line[at] = sum + held * 0.5f;
            sum = held - sum;
            if (++at == line.Length) at = 0;
        }
        return sum;
    }
}
