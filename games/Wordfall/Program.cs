// Wordfall, a typing game. Words fall along curving paths toward a town at the bottom of the window,
// and typing a word clears it before it lands. The words are the game's own or those of a text file
// dropped on the window, a word a line, accents and all, every sound is made as it plays by a
// callback feeding an audio stream, C copies a finished game's result to the clipboard, and F12
// saves a screenshot. Written against the engine's package, as a game outside this repository
// would be.
using System.Numerics;
using System.Text;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Wordfall");
InitAudioDevice();
SetTargetFPS(60);

const float Ground = 640;
const int Lives = 3;

// Latin-1, so a word such as café or smörgåsbord is drawn as it is typed.
var font = LoadFontEx("resources/Lato-Regular.ttf", 48);

string[] ownWords =
[
    "river", "lantern", "harbor", "meadow", "thunder", "copper", "willow", "candle", "orbit", "falcon",
    "glacier", "pepper", "violet", "anchor", "basket", "cinder", "dragon", "fossil", "ginger", "hollow",
    "island", "jasmine", "kettle", "ladder", "marble", "nectar", "oyster", "pillow", "quartz", "rocket",
    "saddle", "timber", "umbrella", "velvet", "walnut", "yonder", "zephyr", "beacon", "canyon", "dew",
    "café", "naïve", "über", "señor", "façade", "jalapeño", "smörgåsbord", "crème", "déjà", "fiancé",
];
var (words, wordsFrom) = (ownWords, "the game's own list");

// -- Sound, made as it plays. Each note is a voice the stream's callback adds up, a wave at a pitch
// for a length, starting after a delay, which the end of each frame asks for more samples of.

const int Rate = 44100;
var voices = new List<Voice>();
var stream = LoadAudioStream(Rate, 32, 1);
SetAudioStreamCallback(stream, samples =>
{
    samples.Clear();
    foreach (var voice in voices)
        for (int i = 0; i < samples.Length; i++)
        {
            var at = voice.Played++ - voice.Delay;
            if (at < 0 || at >= voice.Length) continue;
            var t = (float)at / Rate;
            var fade = 1 - (float)at / voice.Length;
            var phase = t * voice.Pitch % 1;
            var wave = voice.Shape switch
            {
                Shape.Square => phase < 0.5f ? 1f : -1f,
                Shape.Saw => phase * 2 - 1,
                Shape.Noise => Random.Shared.NextSingle() * 2 - 1,
                _ => MathF.Sin(phase * MathF.Tau),
            };
            samples[i] += wave * voice.Volume * fade * fade;
        }
    voices.RemoveAll(v => v.Played >= v.Delay + v.Length);
});
PlayAudioStream(stream);
void Note(float pitch, float seconds, float volume, Shape shape = Shape.Sine, float delay = 0) =>
    voices.Add(new Voice { Pitch = pitch, Length = (int)(seconds * Rate), Volume = volume, Shape = shape, Delay = (int)(delay * Rate) });

// -- The town the words fall on, its windows lit at random.

var random = new Random(9);
var houses = new List<(Rectangle Shape, List<Vector2> Windows)>();
for (float x = 0; x < 1280;)
{
    var width = 50 + random.Next(70);
    var height = 30 + random.Next(60);
    var house = new Rectangle(x, Ground + 80 - height, width - 6, height);
    var windows = new List<Vector2>();
    for (var wy = house.Y + 8; wy < house.Y + house.Height - 12; wy += 16)
        for (var wx = house.X + 8; wx < house.X + house.Width - 12; wx += 14)
            if (random.NextSingle() < 0.5f) windows.Add(new Vector2(wx, wy));
    houses.Add((house, windows));
    x += width;
}
var stars = Enumerable.Range(0, 120).Select(_ => new Vector2(random.Next(1280), random.Next(600))).ToArray();

// -- The game.

var screen = Screen.Title;
var falling = new List<Falling>();
var flyers = new List<Flyer>();
Falling? target = null;
var (score, cleared, misses, typedRight, lives, combo, level) = (0, 0, 0, 0, Lives, 0, 1);
var (spawnIn, played, flash, copied, shots, dropNote, pilotIn) = (0f, 0f, 0f, "", 0, "", 0f);
var spawned = 0;

void Start()
{
    falling.Clear();
    flyers.Clear();
    target = null;
    (score, cleared, misses, typedRight, lives, combo, level) = (0, 0, 0, 0, Lives, 0, 1);
    (spawnIn, played, flash, copied, spawned) = (0.5f, 0, 0, "", 0);
    screen = Screen.Play;
}

// A word starts above the window and falls on a path of five points that wanders as it comes down,
// the first and last beyond the ends a Catmull-Rom spline passes through.
void Spawn()
{
    var word = words[(spawned++ * 7 + random.Next(words.Length)) % words.Length];
    var width = MeasureTextEx(font, word, 32, 1).X;
    var x = 80 + random.NextSingle() * (1120 - width);
    var points = new Vector2[6];
    points[0] = new Vector2(x, -160);
    for (int i = 1; i < 5; i++)
        points[i] = new Vector2(Math.Clamp(x + (random.NextSingle() - 0.5f) * 360, 60, 1220 - width), -40 + (i - 1) * (Ground + 40) / 3);
    points[5] = points[4] + new Vector2(0, 120);
    falling.Add(new Falling(word, points, 0.075f + level * 0.012f));
}

// A character typed, by the player or the autopilot. Where no word is being typed it picks the
// lowest word it starts, and otherwise it is the next of the word being typed or a miss.
void Typed(int codepoint)
{
    var c = Lower(codepoint);
    if (target is null)
    {
        target = falling.Where(f => Lower(f.Runes[0]) == c).MaxBy(f => f.T);
        if (target is null)
        {
            Miss();
            return;
        }
    }
    else if (Lower(target.Runes[target.Typed]) != c)
    {
        Miss();
        return;
    }
    target.Typed++;
    typedRight++;
    Note(440 * MathF.Pow(2, Math.Min(target.Typed, 12) / 12f), 0.05f, 0.18f, Shape.Square);
    if (target.Typed < target.Runes.Length) return;

    // Cleared, its letters fly apart, and an arpeggio rises as the combo does.
    var at = target.Position;
    var pen = at.X;
    foreach (var rune in target.Runes)
    {
        var letter = char.ConvertFromUtf32(rune);
        flyers.Add(new Flyer(letter, new Vector2(pen, at.Y), new Vector2((random.NextSingle() - 0.5f) * 300, -150 - random.NextSingle() * 200),
            (random.NextSingle() - 0.5f) * 720));
        pen += MeasureTextEx(font, letter, 32, 1).X + 1;
    }
    combo++;
    cleared++;
    score += target.Runes.Length * 10 * (10 + Math.Min(combo, 20)) / 10;
    falling.Remove(target);
    target = null;
    var root = 330 * MathF.Pow(2, Math.Min(combo, 12) / 12f);
    for (int i = 0; i < 3; i++) Note(root * MathF.Pow(2, new[] { 0, 4, 7 }[i] / 12f), 0.18f, 0.16f, Shape.Sine, i * 0.06f);
    if (cleared % 10 == 0)
    {
        level++;
        Note(660, 0.12f, 0.2f, Shape.Square);
        Note(880, 0.25f, 0.2f, Shape.Square, 0.12f);
    }
}

void Miss()
{
    misses++;
    combo = 0;
    Note(110, 0.15f, 0.25f, Shape.Saw);
}

static int Lower(int codepoint) => System.Text.Rune.IsValid(codepoint) ? System.Text.Rune.ToLowerInvariant(new System.Text.Rune(codepoint)).Value : codepoint;

string Result() => $"Wordfall: {score} points, {cleared} words, {Wpm():0} words a minute, {Accuracy():0}% typed right";
float Wpm() => played > 0 ? typedRight / 5f / (played / 60) : 0;
float Accuracy() => typedRight + misses > 0 ? 100f * typedRight / (typedRight + misses) : 100;

while (!WindowShouldClose())
{
    var dt = MathF.Min(GetFrameTime(), 1 / 20f);

    // A text file dropped on the window gives the words of the next game, a word a line.
    if (IsFileDropped())
    {
        var files = LoadDroppedFiles();
        UnloadDroppedFiles();
        var dropped = files.Where(FileExists).SelectMany(f => (LoadFileText(f) ?? "").Split('\n'))
            .Select(line => line.Trim()).Where(line => line.Length is > 0 and <= 20 && line.EnumerateRunes().All(System.Text.Rune.IsLetter))
            .Distinct().ToArray();
        if (dropped.Length > 0) (words, wordsFrom, dropNote) = (dropped, Path.GetFileName(files[0]), "");
        else dropNote = $"{Path.GetFileName(files.FirstOrDefault() ?? "")} has no words, a word a line";
    }

    if (IsKeyPressed(Key.F12))
    {
        TakeScreenshot(Path.Combine(AppContext.BaseDirectory, "wordfall-shot.png"));
        shots++;
    }

    switch (screen)
    {
        case Screen.Title or Screen.Over when IsKeyPressed(Key.Enter):
            Start();
            break;
        case Screen.Over when IsKeyPressed(Key.C):
            SetClipboardText(Result());
            copied = GetClipboardText() == Result() ? "yes" : "differs";
            Note(990, 0.1f, 0.15f);
            break;
        case Screen.Play:
            Play();
            break;
    }
    // Typed characters are taken every frame, so those typed on another screen are not kept for play.
    if (screen != Screen.Play)
        while (GetCharPressed() != 0) { }

    // Letters of cleared words fly and fall, turning.
    foreach (var flyer in flyers)
    {
        flyer.Velocity += new Vector2(0, 600) * dt;
        flyer.At += flyer.Velocity * dt;
        flyer.Angle += flyer.Spin * dt;
        flyer.Life -= dt;
    }
    flyers.RemoveAll(f => f.Life <= 0);
    flash = MathF.Max(0, flash - dt * 2);

    WordfallCommands.Status = screen switch
    {
        Screen.Title => $"Title words {words.Length} from {wordsFrom} shots {shots}",
        Screen.Over => $"Over score {score} words {cleared} wpm {Wpm():0} misses {misses} copied {(copied == "" ? "no" : copied)} shots {shots}",
        _ => $"Play level {level} score {score} words {cleared} misses {misses} lives {lives} target {target?.Word ?? "-"} typed {target?.Typed ?? 0} " +
             $"falling {(falling.Count == 0 ? "-" : string.Join(",", falling.OrderByDescending(f => f.T).Select(f => f.Word)))}",
    };

    // -- Drawing.

    // The game is laid out at 1280 by 720 and drawn through a camera that scales it to the window,
    // centered, so a window of another size shows all of it.
    var zoom = MathF.Min(GetScreenWidth() / 1280f, GetScreenHeight() / 720f);
    BeginDrawing();
    ClearBackground(new Color(10, 11, 26));
    BeginMode2D(new Camera2D(new Vector2(GetScreenWidth() - 1280 * zoom, GetScreenHeight() - 720 * zoom) / 2, Vector2.Zero, 0, zoom));
    DrawRectangle(0, 0, 1280, 720, new Color(18, 20, 44));
    foreach (var star in stars) DrawPixelV(star, new Color(200, 200, 255, 160));
    foreach (var (house, windows) in houses)
    {
        DrawRectangleRec(house, new Color(40, 44, 70));
        foreach (var window in windows) DrawRectangleV(window, new Vector2(6, 8), new Color(255, 214, 120, lives > 0 ? (byte)220 : (byte)60));
    }
    DrawRectangle(0, (int)Ground + 80, 1280, 80, new Color(30, 32, 52));
    if (flash > 0) DrawRectangle(0, 0, 1280, 720, new Color(255, 60, 60, (byte)(flash * 90)));

    foreach (var word in falling)
    {
        // The way it falls, faint, and the word with what is typed of it lit.
        DrawSplineCatmullRom(word.Path, 2, new Color(120, 140, 220, word == target ? (byte)120 : (byte)45));
        var at = word.Position;
        var typedText = string.Concat(word.Runes.Take(word.Typed).Select(char.ConvertFromUtf32));
        var size = MeasureTextEx(font, word.Word, 32, 1);
        DrawRectangleRounded(new Rectangle(at.X - 8, at.Y - 4, size.X + 16, size.Y + 8), 0.4f, 6, new Color(10, 12, 30, 170));
        DrawTextEx(font, word.Word, at, 32, 1, word == target ? new Color(255, 240, 200) : Color.RayWhite);
        if (word.Typed > 0) DrawTextEx(font, typedText, at, 32, 1, Color.Gold);
    }
    foreach (var flyer in flyers)
        DrawTextPro(font, flyer.Letter, flyer.At, new Vector2(8, 16), flyer.Angle, 32, 1, Color.Gold with { A = (byte)(255 * Math.Clamp(flyer.Life, 0, 1)) });

    DrawTextEx(font, $"{score}", new Vector2(20, 14), 40, 1, Color.RayWhite);
    DrawTextEx(font, $"level {level}   combo {combo}", new Vector2(20, 58), 22, 1, new Color(180, 190, 230));
    for (int i = 0; i < Lives; i++) DrawCircle(1240 - i * 34, 34, 12, i < lives ? new Color(255, 120, 120) : new Color(70, 60, 80));

    if (screen == Screen.Title)
    {
        CenteredText("WORDFALL", 200, 110, Color.RayWhite);
        CenteredText("Type each word before it lands. Enter to play", 340, 30, new Color(200, 210, 255));
        CenteredText($"{words.Length} words from {wordsFrom}. Drop a text file on the window, a word a line, to play with its words", 390, 22, new Color(160, 170, 220));
        if (dropNote != "") CenteredText(dropNote, 430, 22, new Color(255, 160, 140));
        CenteredText("Backspace lets go of a word, F12 saves a screenshot", 470, 22, new Color(160, 170, 220));
    }
    else if (screen == Screen.Over)
    {
        DrawRectangle(0, 0, 1280, 720, new Color(0, 0, 0, 140));
        CenteredText("The town is buried", 190, 70, Color.RayWhite);
        CenteredText($"{score} points, {cleared} words, {Wpm():0} words a minute, {Accuracy():0}% typed right", 300, 30, Color.Gold);
        CenteredText(copied == "yes" ? "Copied to the clipboard" : "C copies the result, Enter plays again", 360, 26, new Color(200, 210, 255));
    }
    EndMode2D();
    EndDrawing();

    // -- Play, where the words fall and each typed character goes to the word being typed.
    void Play()
    {
        played += dt;
        spawnIn -= dt;
        if (spawnIn <= 0)
        {
            Spawn();
            spawnIn = MathF.Max(0.7f, 2.6f - level * 0.22f);
        }
        foreach (var word in falling) word.T += word.Speed * dt;

        // A word that lands takes a life, and a light of the town.
        foreach (var landed in falling.Where(f => f.T >= 1).ToList())
        {
            falling.Remove(landed);
            if (landed == target) target = null;
            lives--;
            combo = 0;
            flash = 1;
            Note(60, 0.35f, 0.4f);
            Note(0, 0.2f, 0.2f, Shape.Noise);
        }
        if (lives <= 0)
        {
            screen = Screen.Over;
            Note(330, 0.2f, 0.2f, Shape.Saw);
            Note(220, 0.5f, 0.2f, Shape.Saw, 0.2f);
            return;
        }

        if (IsKeyPressed(Key.Backspace)) target = null;
        for (int c = GetCharPressed(); c != 0; c = GetCharPressed()) Typed(c);

        // The autopilot types at a steady six characters a second, the lowest word first, the
        // level's speed overtaking it in time so a game it plays ends.
        if (WordfallCommands.Autopilot && (pilotIn -= dt) <= 0)
        {
            pilotIn += 1 / 6f;
            var next = target ?? falling.MaxBy(f => f.T);
            if (next is not null) Typed(next.Runes[target is null ? 0 : next.Typed]);
            pilotIn = MathF.Max(pilotIn, 0);
        }
    }
}

UnloadAudioStream(stream);
UnloadFont(font);
CloseAudioDevice();
CloseWindow();

// A line of text across the window, centered.
void CenteredText(string text, float y, float size, Color color) =>
    DrawTextEx(font, text, new Vector2((1280 - MeasureTextEx(font, text, size, 1).X) / 2, y), size, 1, color);

public enum Screen { Title, Play, Over }

public enum Shape { Sine, Square, Saw, Noise }

// A note the stream's callback plays, and how many of its samples it has played.
public sealed class Voice
{
    public float Pitch, Volume;
    public int Length, Delay, Played;
    public Shape Shape;
}

// A word on its way down: its path, how far along it is from 0 to 1, how fast, and how much of it
// is typed.
public sealed class Falling(string word, Vector2[] path, float speed)
{
    public string Word { get; } = word;
    public int[] Runes { get; } = [.. word.EnumerateRunes().Select(r => r.Value)];
    public Vector2[] Path { get; } = path;
    public float Speed { get; } = speed;
    public float T { get; set; }
    public int Typed { get; set; }

    // Where it is on its path, a Catmull-Rom spline through every point but the first and last.
    public Vector2 Position
    {
        get
        {
            var segments = Path.Length - 3;
            var along = Math.Clamp(T, 0, 0.9999f) * segments;
            var i = (int)along;
            return GetSplinePointCatmullRom(Path[i], Path[i + 1], Path[i + 2], Path[i + 3], along - i);
        }
    }
}

// A letter of a cleared word, flying apart and falling.
public sealed class Flyer(string letter, Vector2 at, Vector2 velocity, float spin)
{
    public string Letter { get; } = letter;
    public Vector2 At { get; set; } = at;
    public Vector2 Velocity { get; set; } = velocity;
    public float Angle { get; set; }
    public float Spin { get; } = spin;
    public float Life { get; set; } = 1.2f;
}

public static class WordfallCommands
{
    internal static bool Autopilot;
    internal static string Status = "";

    [Command("wordfall.status", "The screen, the level, the score, the words cleared, the misses and lives, the word being typed and how much of it, and the words falling, lowest first")]
    internal static string Report() => Status;

    [Command("wordfall.autopilot", "Types the lowest word at six characters a second, for a game that plays itself: wordfall.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "typing by itself" : "typed by the player";
    }
}
