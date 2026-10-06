// Tempo, a rhythm game. Notes come down four lanes of a road toward a line at its near end, and a
// key or a button played as each reaches the line scores by how near the beat it was. The song is
// a tracker module and its chart the notes of its rows, both written by make-music.py, and the game
// keeps time by the music heard, GetMusicTimePlayed, rather than by its frames, so a slow frame
// scores a note as late as it was played and a fast one no earlier.
using System.Numerics;
using System.Text.Json;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Tempo");
InitAudioDevice();
SetTargetFPS(60);
SetBloom(0.8f, 0.85f);

// -- The song, its chart, and how loud it is heard.

var music = LoadMusicStream("resources/song.mod");
music.Looping = false;
var chart = Chart.Load("resources/chart.json");
var font = LoadFontEx("resources/Lato-Regular.ttf", 64);

// The music's loudness in windows of 512 frames, measured as it is fed to its voice, which is half
// a second before it is heard, and read back at the window being heard, so the lights beside the
// road move with what is heard and not ahead of it.
const int LevelWindow = 512;
var levels = new List<float>();
var (windowSum, windowFrames) = (0f, 0);
AttachAudioStreamProcessor(music.Stream, samples =>
{
    for (int i = 0; i + 1 < samples.Length; i += 2)
    {
        var mono = (samples[i] + samples[i + 1]) * 0.5f;
        windowSum += mono * mono;
        if (++windowFrames < LevelWindow) continue;
        levels.Add(MathF.Sqrt(windowSum / windowFrames));
        (windowSum, windowFrames) = (0, 0);
    }
});
float LevelAt(double time)
{
    var window = (int)(time * music.Stream.SampleRate / LevelWindow);
    return window >= 0 && window < levels.Count ? levels[window] : 0;
}

// -- The lanes, their keys and buttons, and how each looks.

Key[] keys = [Key.D, Key.F, Key.J, Key.K];
GamepadButton[] buttons = [GamepadButton.LeftFaceLeft, GamepadButton.LeftFaceDown, GamepadButton.RightFaceDown, GamepadButton.RightFaceRight];
Color[] laneColors = [new(255, 70, 110), new(255, 180, 50), new(70, 230, 150), new(80, 150, 255)];
float LaneX(int lane) => (lane - 1.5f) * Road.LaneWidth;

var note = LoadModelFromMesh(GenMeshCube(Road.LaneWidth * 0.8f, 0.28f, 0.5f));
var pad = LoadModelFromMesh(GenMeshCube(Road.LaneWidth * 0.86f, 0.1f, 0.7f));
var column = LoadModelFromMesh(GenMeshCube(0.8f, 1, 0.8f));
void Glow(Model model, Color color, float intensity) =>
    model.Materials[0] = model.Materials[0] with { Emissive = color, EmissiveIntensity = intensity, Roughness = 0.4f };

CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.5f)), new Color(220, 220, 255), 1.2f);
SetAmbientLight(new Color(90, 80, 140), 0.4f);

// A burst of sparks at a lane's pad for each note played, its light added and bright enough to bloom.
var sparks = Enumerable.Range(0, 4).Select(lane => CreateParticleEmitter(new Vector3(LaneX(lane), 0.2f, 0), ParticleEmitter.Default with
{
    MaxParticles = 400, Emitting = false, Life = 0.55f, LifeVariation = 0.2f, Velocity = new Vector3(0, 5, 0), Spread = 55,
    SpeedVariation = 0.5f, Gravity = new Vector3(0, -9, 0), Radius = 0.3f, StartSize = 0.22f, EndSize = 0.04f,
    StartColor = laneColors[lane], EndColor = laneColors[lane] with { A = 0 }, Intensity = 3, Blend = ParticleBlend.Additive,
})).ToArray();

var camera = new Camera3D(new Vector3(0, 6, 9.5f), new Vector3(0, 0, -9), Vector3.UnitY, 45);

// -- What is kept between runs: the best score, and how late the player hears the music.

var best = FileExists(Run.BestFile) && int.TryParse(LoadFileText(Run.BestFile), out var savedBest) ? savedBest : 0;
var offsetMs = FileExists(Run.OffsetFile) && int.TryParse(LoadFileText(Run.OffsetFile), out var savedOffset) ? savedOffset : 0;

var screen = Screen.Title;
var run = new Run(chart);
var newBest = false;

while (!WindowShouldClose())
{
    var dt = GetFrameTime();
    UpdateMusicStream(music);
    var start = IsKeyPressed(Key.Enter) || IsGamepadButtonPressed(0, GamepadButton.MiddleRight);
    var pause = IsKeyPressed(Key.P) || IsGamepadButtonPressed(0, GamepadButton.MiddleRight);

    // -- The title and the results, which start a run, and the four beats counted in before it.
    if (screen is Screen.Title or Screen.Results && start)
    {
        run = new Run(chart) { Clock = -4 * 60.0 / chart.Bpm };
        StopMusicStream(music);
        levels.Clear();
        (windowSum, windowFrames) = (0, 0);
        screen = Screen.CountIn;
    }
    else if (screen == Screen.Title && (IsKeyPressed(Key.Left) || IsKeyPressed(Key.Right)))
    {
        // An offset a player sets once, for speakers or a television that play late.
        offsetMs = Math.Clamp(offsetMs + (IsKeyPressed(Key.Right) ? 10 : -10), -300, 300);
        SaveFileText(Run.OffsetFile, offsetMs.ToString());
    }
    else if (screen == Screen.CountIn)
    {
        var before = run.Clock;
        run.Clock += dt;
        run.Moved(before);
        if (run.Clock >= 0)
        {
            run.Clock = 0;
            PlayMusicStream(music);
            screen = Screen.Play;
        }
    }
    else if (screen is Screen.Play or Screen.Paused && pause)
    {
        if (screen == Screen.Play) PauseMusicStream(music); else ResumeMusicStream(music);
        screen = screen == Screen.Play ? Screen.Paused : Screen.Play;
    }
    else if (screen == Screen.Play)
    {
        // The song's time is the music's while it plays, never stepping back, and the frame's once
        // it has run out or where there is no audio device to play it.
        var before = run.Clock;
        if (IsMusicStreamPlaying(music)) run.Clock = Math.Max(run.Clock, GetMusicTimePlayed(music) - offsetMs / 1000.0);
        else run.Clock += dt;
        run.Moved(before);

        // A lane played this frame takes the next note in it, if that is near enough the line.
        for (int lane = 0; lane < 4; lane++)
        {
            var played = IsKeyPressed(keys[lane]) || IsGamepadButtonPressed(0, buttons[lane]);
            // The autopilot plays a note on the frame nearest its time, which is this one when the
            // note is less than half the song's step in a frame away. The step is the larger of the
            // last frame's and the frames' mean, so a frame slower than those before it is not
            // waited for past the note.
            if (TempoCommands.Autopilot && run.Due(lane) is { } due && due - run.Clock <= Math.Max(run.Last, run.Step) / 2) played = true;
            if (!played) continue;
            run.Flash[lane] = 0.12f;
            if (run.Play(lane) is { } grade)
            {
                EmitParticles(sparks[lane], grade == Grade.Perfect ? 40 : grade == Grade.Good ? 20 : 6);
                (run.Shown, run.ShownFor) = (grade, 0.5f);
            }
        }
        if (run.MissPassed()) (run.Shown, run.ShownFor) = (Grade.Miss, 0.5f);

        if (run.Finished(IsMusicStreamPlaying(music)))
        {
            newBest = run.Score > best;
            if (newBest)
            {
                best = run.Score;
                SaveFileText(Run.BestFile, best.ToString());
            }
            screen = Screen.Results;
        }
    }
    for (int lane = 0; lane < 4; lane++) run.Flash[lane] = MathF.Max(0, run.Flash[lane] - dt);
    run.ShownFor = MathF.Max(0, run.ShownFor - dt);

    TempoCommands.Status = $"{screen} time {run.Clock:0.00} perfect {run.Perfect} good {run.Good} bad {run.Bad} miss {run.Miss} " +
                           $"combo {run.Combo} longest {run.Longest} score {run.Score} judged {run.Judged}/{chart.Count} " +
                           $"error {run.MeanError * 1000:0} ms best {best}";

    // -- Drawing: the road and its notes, the lights beside it, and the score over them.

    BeginDrawing();
    ClearBackground(new Color(14, 10, 28));
    BeginMode3D(camera);

    // The road, its lanes, and a line across it on each beat, brighter on the first of a bar.
    DrawCubeV(new Vector3(0, -0.12f, -Road.Length / 2 + 3), new Vector3(Road.LaneWidth * 4 + 0.4f, 0.2f, Road.Length + 6), new Color(26, 22, 44));
    for (int edge = 0; edge <= 4; edge++)
        DrawCubeV(new Vector3((edge - 2) * Road.LaneWidth, 0, -Road.Length / 2 + 3), new Vector3(0.05f, 0.03f, Road.Length + 6), new Color(90, 80, 140));
    var beatSeconds = 60.0 / chart.Bpm;
    for (var beat = Math.Ceiling(run.Clock / beatSeconds); beat * beatSeconds < run.Clock + Road.Ahead; beat++)
    {
        var z = -(float)((beat * beatSeconds - run.Clock) * Road.Speed);
        var bar = (int)beat % 4 == 0;
        DrawCubeV(new Vector3(0, 0, z), new Vector3(Road.LaneWidth * 4, 0.02f, bar ? 0.08f : 0.04f), bar ? new Color(120, 110, 180) : new Color(60, 55, 100));
    }

    // The pads at the line, lit while a lane is played, and the notes coming down to them.
    for (int lane = 0; lane < 4; lane++)
    {
        var held = IsKeyDown(keys[lane]) || IsGamepadButtonDown(0, buttons[lane]) || run.Flash[lane] > 0;
        Glow(pad, laneColors[lane], held ? 2.5f : 0.35f);
        DrawModel(pad, new Vector3(LaneX(lane), 0, 0), 1, new Color(60, 60, 70));

        Glow(note, laneColors[lane], 1.6f);
        foreach (var (time, judged) in run.Coming(lane, Road.Ahead))
        {
            var z = -(float)((time - run.Clock) * Road.Speed);
            if (judged == Grade.Miss)
            {
                Glow(note, Color.Black, 0);
                DrawModel(note, new Vector3(LaneX(lane), 0.15f, z), 1, new Color(70, 70, 80));
                Glow(note, laneColors[lane], 1.6f);
            }
            else if (judged == Grade.None) DrawModel(note, new Vector3(LaneX(lane), 0.15f, z), 1, Color.White);
        }
    }

    // Lights down each side of the road, the nearest as loud as the music heard now and each one
    // further as loud as it was a moment before, so each beat runs away down the road.
    for (int i = 0; i < 18; i++)
    {
        var level = screen is Screen.Play or Screen.Paused ? MathF.Min(1, LevelAt(run.Clock - i * 0.05) * 3.5f) : 0;
        var height = 0.3f + level * 5;
        var color = ColorFromHSV((250 + i * 6 + level * 60) % 360, 0.75f, 1);
        Glow(column, color, 0.3f + level * 2.5f);
        foreach (var side in new[] { -1, 1 })
            DrawModelEx(column, new Vector3(side * (Road.LaneWidth * 2 + 1.4f), height / 2, -2 - i * 3.2f), Vector3.UnitY, 0,
                new Vector3(1, height, 1), new Color(40, 36, 60));
    }
    EndMode3D();

    DrawScreen();
    EndDrawing();
}

UnloadMusicStream(music);
UnloadFont(font);
CloseAudioDevice();
CloseWindow();

// Text centered on a point across the screen, at a size.
void Centered(string text, float y, float size, Color color)
{
    var width = MeasureTextEx(font, text, size, 1).X;
    DrawTextEx(font, text, new Vector2((GetScreenWidth() - width) / 2, y), size, 1, color);
}

void DrawScreen()
{
    var (w, h) = (GetScreenWidth(), GetScreenHeight());
    var white = new Color(240, 236, 255);
    var dim = new Color(170, 160, 210);

    // The key under each pad, where the pad is on the screen.
    for (int lane = 0; lane < 4; lane++)
    {
        var at = GetWorldToScreen(new Vector3(LaneX(lane), 0, 1.1f), camera);
        var label = keys[lane].ToString();
        DrawTextEx(font, label, at - MeasureTextEx(font, label, 26, 1) / 2, 26, 1, Fade(laneColors[lane], 0.8f));
    }

    if (screen == Screen.Title)
    {
        DrawRectangle(0, 0, w, h, Fade(Color.Black, 0.45f));
        Centered("TEMPO", h * 0.2f, 96, white);
        Centered("Play each note as it reaches the line, with D, F, J and K,", h * 0.42f, 26, dim);
        Centered("or the pad's left and down and its bottom and right buttons.", h * 0.42f + 32, 26, dim);
        Centered(best > 0 ? $"Best {best:N0}" : "No song played yet", h * 0.58f, 30, white);
        Centered($"Audio offset {offsetMs:+0;-0;0} ms, Left and Right to set it", h * 0.66f, 22, dim);
        Centered("Enter or Start to play", h * 0.76f, 34, white);
        return;
    }

    if (screen == Screen.Results)
    {
        DrawRectangle(0, 0, w, h, Fade(Color.Black, 0.55f));
        Centered("RESULTS", h * 0.16f, 72, white);
        Centered($"Perfect {run.Perfect}   Good {run.Good}   Bad {run.Bad}   Miss {run.Miss}", h * 0.36f, 32, white);
        Centered($"Longest combo {run.Longest}   Accuracy {run.Accuracy * 100:0.0}%   Off the beat by {run.MeanError * 1000:0} ms", h * 0.44f, 26, dim);
        Centered($"Score {run.Score:N0}", h * 0.54f, 52, white);
        Centered(newBest ? "A new best score" : $"Best {best:N0}", h * 0.64f, 30, newBest ? new Color(255, 210, 90) : dim);
        Centered("Enter or Start to play again", h * 0.78f, 30, white);
        return;
    }

    // The score, the song's way through, and the combo.
    DrawTextEx(font, $"{run.Score:N0}", new Vector2(24, 18), 40, 1, white);
    DrawTextEx(font, $"Best {best:N0}", new Vector2(24, 62), 22, 1, dim);
    DrawRectangle(w - 324, 30, 300, 8, Fade(dim, 0.3f));
    DrawRectangle(w - 324, 30, (int)(300 * Math.Clamp(run.Clock / chart.Length, 0, 1)), 8, dim);
    if (run.Combo >= 4)
    {
        Centered($"{run.Combo}", h * 0.1f, 64, white);
        Centered("combo", h * 0.1f + 64, 22, dim);
    }

    // The last note's grade, rising a little as it fades.
    if (run.ShownFor > 0)
    {
        var (text, color) = run.Shown switch
        {
            Grade.Perfect => ("PERFECT", new Color(255, 230, 120)),
            Grade.Good => ("GOOD", new Color(120, 230, 170)),
            Grade.Bad => ("BAD", new Color(200, 160, 120)),
            _ => ("MISS", new Color(230, 90, 100)),
        };
        var fade = run.ShownFor / 0.5f;
        Centered(text, h * 0.56f - (1 - fade) * 20, 44, Fade(color, fade));
    }

    if (screen == Screen.CountIn)
        Centered($"{(int)Math.Ceiling(-run.Clock * chart.Bpm / 60)}", h * 0.32f, 110, white);
    if (screen == Screen.Paused)
    {
        DrawRectangle(0, 0, w, h, Fade(Color.Black, 0.5f));
        Centered("Paused, P or Start to go on", h * 0.42f, 40, white);
    }
}

public enum Screen { Title, CountIn, Play, Paused, Results }

public enum Grade { None, Perfect, Good, Bad, Miss }

/// <summary>The road's size and how fast the notes come down it.</summary>
public static class Road
{
    public const float LaneWidth = 1.3f, Length = 60, Speed = 14;

    /// <summary>How many seconds of the song the road shows ahead of the line.</summary>
    public const float Ahead = Length / Speed;
}

/// <summary>The notes of a song, each lane's in the order they are played, and its beat and length.</summary>
public sealed class Chart
{
    public required double[][] Lanes { get; init; }
    public required double Bpm { get; init; }
    public required double Length { get; init; }
    public int Count => Lanes.Sum(l => l.Length);

    public static Chart Load(string path)
    {
        using var json = JsonDocument.Parse(LoadFileText(path) ?? throw new FileNotFoundException("The chart is missing.", path));
        var root = json.RootElement;
        var lanes = Enumerable.Range(0, 4).Select(_ => new List<double>()).ToArray();
        foreach (var n in root.GetProperty("notes").EnumerateArray())
            lanes[n.GetProperty("lane").GetInt32()].Add(n.GetProperty("time").GetDouble());
        return new Chart
        {
            Lanes = lanes.Select(l => l.Order().ToArray()).ToArray(),
            Bpm = root.GetProperty("bpm").GetDouble(),
            Length = root.GetProperty("length").GetDouble(),
        };
    }
}

/// <summary>A song being played: the song's time, each note's grade, and the score.</summary>
public sealed class Run(Chart chart)
{
    public const string BestFile = "tempo-best.txt", OffsetFile = "tempo-offset.txt";

    // How far from its time a note may be played for each grade, in seconds. A note played further
    // off than Bad is not taken, and one left past it is missed.
    public const double PerfectWindow = 0.05, GoodWindow = 0.10, BadWindow = 0.15;

    /// <summary>The song's time in seconds, below zero while four beats are counted in.</summary>
    public double Clock;

    /// <summary>How far the song's time moved in the last frame it moved, and in a frame on average, which the autopilot plays by.</summary>
    public double Last, Step = 1 / 60.0;

    public int Perfect, Good, Bad, Miss, Combo, Longest, Score;
    public int Judged => Perfect + Good + Bad + Miss;
    public double MeanError => _played > 0 ? _error / _played : 0;
    public double Accuracy => chart.Count > 0 ? (Perfect + Good * 0.6 + Bad * 0.2) / chart.Count : 0;

    public readonly float[] Flash = new float[4];
    public Grade Shown;
    public float ShownFor;

    private readonly Grade[][] _grades = chart.Lanes.Select(l => new Grade[l.Length]).ToArray();
    private readonly int[] _next = new int[4];
    private double _error;
    private int _played;

    /// <summary>Keeps how far the song's time moved this frame, from where it was before.</summary>
    public void Moved(double before)
    {
        if (Clock <= before) return;
        Last = Clock - before;
        Step += (Last - Step) * 0.2;
    }

    /// <summary>The time of the next note in a lane not yet played or missed, or null when none is left.</summary>
    public double? Due(int lane) => _next[lane] < chart.Lanes[lane].Length ? chart.Lanes[lane][_next[lane]] : null;

    /// <summary>Grades a lane's next note when the lane is played within the Bad window of it, and gives the grade, or null where no note is that near.</summary>
    public Grade? Play(int lane)
    {
        if (Due(lane) is not { } time || Math.Abs(time - Clock) > BadWindow) return null;
        var off = Math.Abs(time - Clock);
        var grade = off <= PerfectWindow ? Grade.Perfect : off <= GoodWindow ? Grade.Good : Grade.Bad;
        _grades[lane][_next[lane]++] = grade;
        (_error, _played) = (_error + off, _played + 1);
        switch (grade)
        {
            case Grade.Perfect: Perfect++; break;
            case Grade.Good: Good++; break;
            default: Bad++; break;
        }
        Combo = grade == Grade.Bad ? 0 : Combo + 1;
        Longest = Math.Max(Longest, Combo);
        // Each tenth of the combo adds the note's worth again, to four times it.
        var worth = grade switch { Grade.Perfect => 300, Grade.Good => 100, _ => 30 };
        Score += worth * (10 + Math.Min(Combo, 30)) / 10;
        return grade;
    }

    /// <summary>Misses each note left more than the Bad window behind the song's time, and says whether any was.</summary>
    public bool MissPassed()
    {
        var any = false;
        for (int lane = 0; lane < 4; lane++)
            while (Due(lane) is { } time && Clock - time > BadWindow)
            {
                _grades[lane][_next[lane]++] = Grade.Miss;
                (Miss, Combo, any) = (Miss + 1, 0, true);
            }
        return any;
    }

    /// <summary>Whether the song is over, every note graded and the music run out or a second past its end.</summary>
    public bool Finished(bool musicPlaying) =>
        Judged == chart.Count && (!musicPlaying || Clock > chart.Length + 1);

    /// <summary>The notes of a lane from a little behind the line to some seconds ahead of it, with their grades.</summary>
    public IEnumerable<(double Time, Grade Grade)> Coming(int lane, double ahead)
    {
        var times = chart.Lanes[lane];
        for (int i = Math.Max(0, _next[lane] - 8); i < times.Length && times[i] < Clock + ahead; i++)
            if (times[i] > Clock - 0.5) yield return (times[i], _grades[lane][i]);
    }
}

public static class TempoCommands
{
    internal static bool Autopilot;
    internal static string Status = "";

    [Command("tempo.status", "The screen, the song's time, the notes played perfect, good and bad and those missed, the combo, the score and the mean error")]
    internal static string Report() => Status;

    [Command("tempo.autopilot", "Plays each note on the frame nearest its time, for a run that plays itself: tempo.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "playing itself" : "played by the player";
    }
}
