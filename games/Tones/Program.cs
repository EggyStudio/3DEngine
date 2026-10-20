// Tones, an instrument played on the keyboard, whose samples the program writes itself and pushes
// through an audio stream, an organ or a plucked string for each key held and drum hits read from
// waves as samples. The stream's pitch, pan and volume are set by keys and it pauses and resumes.
// Its echo and its filter are processors over the stream's samples, a third records the first
// fifteen seconds of them, and a recording is saved as a wave and read back to be drawn and played. A processor over what the
// device mixes reads how loud it plays. Written against the engine's package, as a game outside
// this repository would be.
using System.Numerics;
using Engine;
using static Engine.E3D;

InitWindow(1280, 720, "Tones");
InitAudioDevice();
SetTargetFPS(60);

const int Rate = 44100, Block = 512;
// The longest a recording runs, which keeps its first quarter of a minute. Its buffer is made that
// long once, at the start, and a recording read back is held once, shared by what draws it and the
// sound that plays it, so what recordings hold is bounded from the first frame. A recording as long
// as a slow turn of the soak, 30 seconds on a runner, once held 19 MB between turns.
const int RecordingSeconds = 15;
const string RecordingFile = "tones-recording.wav";

// A stream keeps a little queued, so a key is heard soon after it is pressed.
SetAudioStreamBufferSizeDefault(2048);
var stream = LoadAudioStream(Rate, 32, 1);
PlayAudioStream(stream);
var synth = new Synth(Rate);

// The drums, waves read as samples at the stream's rate and channels.
float[] Drum(string name)
{
    var wave = LoadWave($"resources/{name}.wav");
    WaveFormat(ref wave, Rate, 32, 1);
    var samples = IsWaveValid(wave) ? LoadWaveSamples(wave) : [];
    UnloadWave(wave);
    return samples;
}
var (kick, snare, hat) = (Drum("kick"), Drum("snare"), Drum("hat"));

// The keys a note each, the middle row the white keys from middle C and the row above the black.
(Key Key, int Note)[] keys =
[
    (Key.A, 60), (Key.W, 61), (Key.S, 62), (Key.E, 63), (Key.D, 64), (Key.F, 65), (Key.T, 66),
    (Key.G, 67), (Key.Y, 68), (Key.H, 69), (Key.U, 70), (Key.J, 71), (Key.K, 72), (Key.O, 73), (Key.L, 74),
];

// The processors over the stream, the echo and the filter where they are on and the recorder
// always, last, so it records what they made. Attached again in that order whenever one changes,
// since one attached is run after those attached before it.
Effects.Rate = Rate;
Effects.MaxRecording = Rate * RecordingSeconds;
Effects.Recording.Capacity = Effects.MaxRecording;
void Attach()
{
    DetachAudioStreamProcessor(stream, Effects.Echo);
    DetachAudioStreamProcessor(stream, Effects.Filter);
    DetachAudioStreamProcessor(stream, Effects.Record);
    if (Effects.EchoOn) AttachAudioStreamProcessor(stream, Effects.Echo);
    if (Effects.FilterOn) AttachAudioStreamProcessor(stream, Effects.Filter);
    AttachAudioStreamProcessor(stream, Effects.Record);
}
Attach();
AttachAudioMixedProcessor(Effects.Heard);

var (pitch, pan, volume, paused) = (1f, 0f, 0.8f, false);
var (notes, hits) = (0, 0);
// What the last recording read back held, its samples, how loud it was at most, and how loud it
// was from half a second to a second and a half after its last note was let go, where a note's own
// release has ended and an echo rings on.
float[] recorded = [];
var (savedPeak, savedTail) = (0f, 0f);
Sound? playback = null;

void Record(bool start)
{
    if (start)
    {
        Effects.Recording.Clear();
        (Effects.Loudest, Effects.LetGoAt) = (0, -1);
        Effects.IsRecording = true;
        return;
    }
    if (!Effects.IsRecording) return;
    Effects.IsRecording = false;
    var wave = new Wave { Samples = [.. Effects.Recording], Channels = 1, SampleRate = Rate };
    if (!ExportWave(wave, RecordingFile)) return;
    // Read back from the file, as another program would read it.
    var back = LoadWave(RecordingFile);
    recorded = back.Samples;
    savedPeak = recorded.Length == 0 ? 0 : recorded.Max(MathF.Abs);
    var from = Math.Min(recorded.Length, (Effects.LetGoAt < 0 ? 0 : Effects.LetGoAt) + Rate / 2);
    var to = Math.Min(recorded.Length, from + Rate);
    savedTail = to > from ? recorded[from..to].Max(MathF.Abs) : 0;
    if (playback is { } old) UnloadSound(old);
    // Sharing the samples drawn, so one recording is held once.
    playback = LoadSoundFromWave(back);
    UnloadWave(back);
}

// Lets a note go, marking where in the recording it was let go.
void NoteOff(int note)
{
    synth.NoteOff(note);
    if (Effects.IsRecording) Effects.LetGoAt = Effects.Recording.Count;
}

void Toggle(string effect, bool on)
{
    if (effect == "echo") Effects.EchoOn = on;
    else Effects.FilterOn = on;
    Attach();
}

var block = new float[Block];
while (!WindowShouldClose())
{
    // -- What the commands ask.
    foreach (var asked in TonesCommands.Asked.ToArray())
    {
        var parts = asked.Split(' ');
        if (parts[0] == "effect") Toggle(parts[1], parts[2] == "on");
        if (parts[0] == "record") Record(parts[1] == "start");
    }
    TonesCommands.Asked.Clear();

    // -- The keys, notes down and up, drums, the effects, the recording and the stream's controls.
    foreach (var (key, note) in keys)
    {
        if (IsKeyPressed(key) && synth.NoteOn(note)) notes++;
        if (IsKeyReleased(key)) NoteOff(note);
    }
    foreach (var (key, drum) in new[] { (Key.Z, kick), (Key.X, snare), (Key.C, hat) })
        if (IsKeyPressed(key) && drum.Length > 0)
        {
            synth.Hit(drum);
            hits++;
        }
    if (IsKeyPressed(Key.One)) Toggle("echo", !Effects.EchoOn);
    if (IsKeyPressed(Key.Two)) Toggle("filter", !Effects.FilterOn);
    if (IsKeyPressed(Key.Three)) Record(!Effects.IsRecording);
    if (IsKeyPressed(Key.Four) && playback is { } heard) PlaySound(heard);
    if (IsKeyPressed(Key.Tab)) synth.Plucked = !synth.Plucked;
    var dt = GetFrameTime();
    if (IsKeyDown(Key.Left)) pan = MathF.Max(-1, pan - dt);
    if (IsKeyDown(Key.Right)) pan = MathF.Min(1, pan + dt);
    if (IsKeyDown(Key.Up)) volume = MathF.Min(1, volume + dt);
    if (IsKeyDown(Key.Down)) volume = MathF.Max(0, volume - dt);
    if (IsKeyDown(Key.Period)) pitch = MathF.Min(2, pitch + dt * 0.5f);
    if (IsKeyDown(Key.Comma)) pitch = MathF.Max(0.5f, pitch - dt * 0.5f);
    SetAudioStreamPan(stream, pan);
    SetAudioStreamVolume(stream, volume);
    SetAudioStreamPitch(stream, pitch);
    if (IsKeyPressed(Key.Space))
    {
        paused = !paused;
        if (paused) PauseAudioStream(stream);
        else ResumeAudioStream(stream);
    }

    // -- The stream fed as it plays, a block at a time, a few blocks a frame at most.
    for (int fed = 0; fed < 8 && IsAudioStreamProcessed(stream); fed++)
    {
        synth.Write(block);
        UpdateAudioStream(stream, block);
    }

    TonesCommands.Status = $"{(IsAudioStreamPlaying(stream) ? "Playing" : "Paused")} notes {notes} sounding {synth.Sounding} drums {hits} " +
                           $"echo {(Effects.EchoOn ? "on" : "off")} filter {(Effects.FilterOn ? "on" : "off")} {(synth.Plucked ? "string" : "organ")} " +
                           $"pitch {pitch:0.00} pan {pan:0.00} volume {volume:0.00} loudest {Effects.Loudest:0.000} heard {Effects.HeardPeak:0.000} " +
                           $"recording {(Effects.IsRecording ? "on" : "off")} saved {recorded.Length} peak {savedPeak:0.000} tail {savedTail:0.000}";

    // -- The frame, the keyboard, what the stream played last as a trace, the last recording read
    // back, and the controls.
    BeginDrawing();
    ClearBackground(new Color(24, 26, 34));
    DrawText("Keys A to L play, W E T Y U O the black keys, Z X C drums, 1 echo, 2 filter, 3 record, 4 play it back", 20, 20, 20, Color.LightGray);
    DrawText("Tab organ or string, Left and Right pan, Up and Down volume, comma and period pitch, Space pause", 20, 46, 20, Color.LightGray);
    DrawTrace(Effects.Trace, Effects.TraceAt, new Rectangle(40, 100, 1200, 200), new Color(120, 220, 160));
    DrawRecording(new Rectangle(40, 330, 1200, 80));
    DrawKeyboard(new Rectangle(240, 450, 800, 200));
    DrawText($"Echo {(Effects.EchoOn ? "on" : "off")}   Filter {(Effects.FilterOn ? "on" : "off")}   {(synth.Plucked ? "String" : "Organ")}   " +
             $"Pitch {pitch:0.00}   Pan {pan:0.00}   Volume {volume:0.00}   {(paused ? "Paused" : "")}   {(Effects.IsRecording ? "Recording" : "")}",
        40, 670, 20, Color.White);
    DrawRectangle(1100, 660, 140, 30, new Color(50, 54, 66));
    DrawRectangle(1100, 660, (int)(140 * Math.Clamp(Effects.HeardPeak, 0, 1)), 30, new Color(240, 180, 60));
    EndDrawing();
}

DetachAudioMixedProcessor(Effects.Heard);
if (playback is { } last) UnloadSound(last);
UnloadWaveSamples(kick);
UnloadWaveSamples(snare);
UnloadWaveSamples(hat);
UnloadAudioStream(stream);
CloseAudioDevice();
CloseWindow();

// The samples a processor saw last, as a line across a box.
void DrawTrace(float[] samples, int at, Rectangle box, Color color)
{
    DrawRectangleRec(box, new Color(34, 38, 50));
    var middle = box.Y + box.Height / 2;
    for (int i = 1; i < samples.Length; i++)
    {
        var a = samples[(at + i - 1) % samples.Length];
        var b = samples[(at + i) % samples.Length];
        var x = box.X + box.Width * i / samples.Length;
        DrawLineV(new Vector2(x - box.Width / samples.Length, middle - a * box.Height / 2), new Vector2(x, middle - b * box.Height / 2), color);
    }
}

// The last recording read back, its loudest sample in each column.
void DrawRecording(Rectangle box)
{
    DrawRectangleRec(box, new Color(34, 38, 50));
    if (recorded.Length == 0)
    {
        DrawText("Press 3 to record, and 3 again to save it", (int)box.X + 10, (int)box.Y + 30, 20, Color.Gray);
        return;
    }
    var columns = (int)box.Width;
    for (int c = 0; c < columns; c++)
    {
        var from = (long)recorded.Length * c / columns;
        var to = Math.Max(from + 1, (long)recorded.Length * (c + 1) / columns);
        var loud = 0f;
        for (var i = from; i < to && i < recorded.Length; i++) loud = MathF.Max(loud, MathF.Abs(recorded[i]));
        var h = loud * box.Height / 2;
        DrawLineV(new Vector2(box.X + c, box.Y + box.Height / 2 - h), new Vector2(box.X + c, box.Y + box.Height / 2 + h), new Color(110, 160, 230));
    }
}

// The keyboard, a white key for each natural note and the black keys over them, lit while held.
void DrawKeyboard(Rectangle box)
{
    var whites = keys.Where(k => !IsBlack(k.Note)).ToArray();
    var width = box.Width / whites.Length;
    for (int i = 0; i < whites.Length; i++)
    {
        var down = IsKeyDown(whites[i].Key);
        DrawRectangleRec(new Rectangle(box.X + i * width + 2, box.Y, width - 4, box.Height), down ? new Color(250, 210, 90) : Color.RayWhite);
        DrawText(whites[i].Key.ToString(), (int)(box.X + i * width + width / 2 - 6), (int)(box.Y + box.Height - 30), 20, Color.DarkGray);
    }
    foreach (var (key, note) in keys.Where(k => IsBlack(k.Note)))
    {
        var after = whites.Count(w => w.Note < note);
        var x = box.X + after * width - width * 0.3f;
        DrawRectangleRec(new Rectangle(x, box.Y, width * 0.6f, box.Height * 0.6f), IsKeyDown(key) ? new Color(220, 150, 40) : new Color(30, 30, 30));
        DrawText(key.ToString(), (int)(x + width * 0.3f - 6), (int)(box.Y + box.Height * 0.6f - 26), 20, Color.LightGray);
    }
}

static bool IsBlack(int note) => (note % 12) is 1 or 3 or 6 or 8 or 10;

/// <summary>
/// The processors, the echo and the filter over the stream's samples on the main thread as the
/// stream queues them, the recorder after them, and the meter over what the device mixes on the
/// audio thread.
/// </summary>
public static class Effects
{
    public static int Rate, MaxRecording;
    public static bool EchoOn, FilterOn, IsRecording;
    public static readonly List<float> Recording = [];
    public static float Loudest;
    public static int LetGoAt = -1;

    // The last samples the stream queued, as the recorder saw them, which the screen draws.
    public static readonly float[] Trace = new float[2048];
    public static int TraceAt;

    // An echo a third of a second behind, fed back at a little over half, so it rings on for a
    // couple of seconds after a note ends.
    private static readonly float[] Delay = new float[44100 / 3];
    private static int _delayAt;
    private const float Feedback = 0.55f, Wet = 0.6f;

    public static void Echo(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            var echoed = Delay[_delayAt];
            Delay[_delayAt] = samples[i] + echoed * Feedback;
            _delayAt = (_delayAt + 1) % Delay.Length;
            samples[i] = MathF.Tanh(samples[i] + echoed * Wet);
        }
    }

    // A low pass of one pole at 600 hertz, which dulls the organ's upper partials and the hats.
    private static float _low;

    public static void Filter(Span<float> samples)
    {
        var a = 1 - MathF.Exp(-MathF.Tau * 600 / Rate);
        for (int i = 0; i < samples.Length; i++) samples[i] = _low += a * (samples[i] - _low);
    }

    public static void Record(Span<float> samples)
    {
        foreach (var sample in samples)
        {
            Trace[TraceAt] = sample;
            TraceAt = (TraceAt + 1) % Trace.Length;
            if (!IsRecording || Recording.Count >= MaxRecording) continue;
            Recording.Add(sample);
            Loudest = MathF.Max(Loudest, MathF.Abs(sample));
        }
    }

    // How loud the device played its last mixed samples, written on the audio thread and read on
    // the main one, a float written whole.
    private static float _heard;
    public static float HeardPeak => Volatile.Read(ref _heard);

    public static void Heard(Span<float> samples)
    {
        var loud = 0f;
        foreach (var sample in samples) loud = MathF.Max(loud, MathF.Abs(sample));
        Volatile.Write(ref _heard, MathF.Max(loud, HeardPeak * 0.95f));
    }
}

public static class TonesCommands
{
    internal static string Status = "";
    internal static readonly List<string> Asked = [];

    [Command("tones.status", "Whether the stream plays, the notes and drums played, the notes sounding, the effects, the instrument, the stream's pitch, pan and volume, the loudest sample recorded, how loud the device plays, and the last recording read back, its samples, its peak and its tail")]
    internal static string Report() => Status;

    [Command("tones.effect", "Turns the echo or the filter on or off: tones.effect <echo|filter> <on|off>")]
    internal static string Effect(string effect, string state)
    {
        if (effect is not ("echo" or "filter") || state is not ("on" or "off")) return "tones.effect <echo|filter> <on|off>";
        Asked.Add($"effect {effect} {state}");
        return $"the {effect} is {state}";
    }

    [Command("tones.record", "Starts a recording, or stops it, saves it as a wave and reads it back: tones.record <start|stop>")]
    internal static string Recording(string state)
    {
        if (state is not ("start" or "stop")) return "tones.record <start|stop>";
        Asked.Add($"record {state}");
        return state == "start" ? "recording" : "saving the recording";
    }
}
