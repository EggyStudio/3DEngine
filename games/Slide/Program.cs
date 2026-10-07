// Slide, the puzzle of numbered tiles on a board of four by four. A swipe or an arrow slides every
// tile one way, two tiles of a number that meet become one of twice it, and a new tile comes after
// each move, until the board is full and nothing can merge. It is played by touch or the mouse
// through gestures, a swipe to slide, a double tap to take the last move back, a hold to start
// again and a pinch to come nearer, or with the keyboard. The tiles' faces are drawn into images as
// the game starts, and its sounds are waves it makes, copies, cuts and converts. Written against the
// engine's package, as a game outside this repository would be.
using System.Numerics;
using Engine;
using static Engine.Engine3D;

InitWindow(960, 720, "Slide");
InitAudioDevice();
SetTargetFPS(60);

// The board's side, and how many powers of two a tile may be, up to 65536.
const int Size = 4, Kinds = 17;
const float SlideSeconds = 0.1f;
var font = LoadFontEx("resources/Lato-Regular.ttf", 128);

// -- The tiles' faces, each a color for its power of two with its number on it, drawn into an image
// and loaded as the texture of a block of its own.

Color[] colors =
[
    default, new(238, 228, 218), new(237, 224, 200), new(242, 177, 121), new(245, 149, 99), new(246, 124, 95), new(246, 94, 59),
    new(237, 207, 114), new(237, 204, 97), new(237, 200, 80), new(237, 197, 63), new(237, 194, 46), new(94, 218, 146),
    new(70, 190, 220), new(120, 110, 230), new(200, 80, 200), new(60, 58, 50),
];
var blocks = new Model[Kinds];
var faces = new Texture2D[Kinds];
for (int p = 1; p < Kinds; p++)
{
    var face = GenImageColor(256, 256, colors[p]);
    ImageDrawRectangleLinesEx(ref face, new Rectangle(0, 0, 256, 256), 12, Darker(colors[p]));
    var number = (1 << p).ToString();
    var size = number.Length switch { <= 2 => 130, 3 => 104, 4 => 80, _ => 62 };
    var measured = MeasureTextEx(font, number, size, 0);
    ImageDrawTextEx(ref face, font, number, (new Vector2(256) - measured) / 2, size, 0, p <= 2 ? new Color(119, 110, 101) : Color.White);
    // A cube's top is mapped with the image's top row toward the viewer, so it is turned over first.
    ImageFlipVertical(ref face);
    faces[p] = LoadTextureFromImage(face);
    UnloadImage(face);
    blocks[p] = LoadModelFromMesh(GenMeshCube(0.9f, 0.32f, 0.9f));
    blocks[p].Materials[0] = blocks[p].Materials[0] with { Texture = faces[p], Roughness = 0.6f };
}

CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.5f)), Color.White, 1.4f, castsShadows: true);
SetAmbientLight(new Color(190, 180, 170), 0.55f);

// -- Sounds, made as waves: a rustle for a slide, a pop for a merge, its first fifth cut off a copy
// for a tap, and a falling note when the board fills, converted to another rate and to stereo.

const int Rate = 22050;
Wave Make(float seconds, Func<float, float> sample) => new()
{
    Samples = [.. Enumerable.Range(0, (int)(seconds * Rate)).Select(i => sample((float)i / Rate))],
    SampleRate = Rate,
    Channels = 1,
};
var noise = new Random(4);
var slideWave = Make(0.09f, t => (noise.NextSingle() * 2 - 1) * 0.25f * (1 - t / 0.09f));
var popWave = Make(0.25f, t => MathF.Sin(MathF.Tau * 523 * t) * 0.4f * MathF.Pow(1 - t / 0.25f, 2));
var tapWave = WaveCopy(popWave);
WaveCrop(ref tapWave, 0, tapWave.FrameCount / 5);
var overWave = Make(0.8f, t => MathF.Sin(MathF.Tau * (220 - 60 * t) * t) * 0.4f * (1 - t / 0.8f));
WaveFormat(ref overWave, 44100, 16, 2);
var (slideSound, popSound, tapSound, overSound) = (LoadSoundFromWave(slideWave), LoadSoundFromWave(popWave), LoadSoundFromWave(tapWave), LoadSoundFromWave(overWave));

// -- The game.

const string BestFile = "slide-best.txt";
var best = FileExists(BestFile) && int.TryParse(LoadFileText(BestFile), out var saved) ? saved : 0;
var screen = Screen.Title;
var grid = new Piece?[Size, Size];
var pieces = new List<Piece>();
var random = new Random(12);
(int[,] Powers, int Score)? undo = null;
var (score, moves, undos, slide, banner, won, holdUsed) = (0, 0, 0, 1f, 0f, false, false);
var (distance, lean) = (8f, Vector2.Zero);
var pilotIn = 0;

void NewGame()
{
    Array.Clear(grid);
    pieces.Clear();
    (score, moves, undos, slide, banner, won, undo) = (0, 0, 0, 1, 0, false, null);
    Spawn();
    Spawn();
    screen = Screen.Play;
}

// A new tile, a 2 most times and a 4 now and then, on a free cell, growing in once the slide ends.
void Spawn()
{
    var free = Cells().Where(c => grid[c.X, c.Y] is null).ToList();
    if (free.Count == 0) return;
    var (x, y) = free[random.Next(free.Count)];
    var piece = new Piece(random.NextSingle() < 0.9f ? 1 : 2, Place(x, y)) { Grow = -SlideSeconds };
    grid[x, y] = piece;
    pieces.Add(piece);
}

// Slides every tile toward (dx, dy), each as far as it goes, merging once with a tile of its own
// number that it meets, and answers whether anything moved.
bool Move(int dx, int dy)
{
    var before = (Powers(), score);
    foreach (var piece in pieces) piece.From = piece.To;
    pieces.RemoveAll(p => p.Gone);
    var merged = new bool[Size, Size];
    var moved = false;
    var popped = 0;
    int[] forward = [0, 1, 2, 3], backward = [3, 2, 1, 0];
    foreach (var y in dy > 0 ? backward : forward)
    foreach (var x in dx > 0 ? backward : forward)
    {
        if (grid[x, y] is not { } piece) continue;
        var (nx, ny) = (x, y);
        while (Inside(nx + dx, ny + dy) && grid[nx + dx, ny + dy] is null) (nx, ny) = (nx + dx, ny + dy);
        if (Inside(nx + dx, ny + dy) && grid[nx + dx, ny + dy] is { } other && other.Power == piece.Power && !merged[nx + dx, ny + dy])
        {
            grid[x, y] = null;
            piece.To = Place(nx + dx, ny + dy);
            piece.Gone = true;
            other.Power++;
            other.Pop = -SlideSeconds;
            merged[nx + dx, ny + dy] = true;
            score += 1 << other.Power;
            popped = Math.Max(popped, other.Power);
            moved = true;
        }
        else if ((nx, ny) != (x, y))
        {
            grid[x, y] = null;
            grid[nx, ny] = piece;
            piece.To = Place(nx, ny);
            moved = true;
        }
    }
    if (!moved) return false;

    undo = before;
    moves++;
    slide = 0;
    PlaySound(slideSound);
    if (popped > 0)
    {
        SetSoundPitch(popSound, 1 + popped * 0.06f);
        PlaySound(popSound);
    }
    if (popped >= 11 && !won) (won, banner) = (true, 2.5f);
    if (score > best) SaveFileText(BestFile, (best = score).ToString());
    Spawn();
    if (!Cells().Any(c => grid[c.X, c.Y] is null) && !CanMerge())
    {
        screen = Screen.Over;
        PlaySound(overSound);
    }
    return true;
}

// Takes the last move back, once, the board and the score as they were before it.
void Undo()
{
    if (undo is not { } last) return;
    Array.Clear(grid);
    pieces.Clear();
    foreach (var (x, y) in Cells())
        if (last.Powers[x, y] > 0) pieces.Add(grid[x, y] = new Piece(last.Powers[x, y], Place(x, y)));
    (score, undo, slide) = (last.Score, null, 1);
    moves--;
    undos++;
    screen = Screen.Play;
    PlaySound(tapSound);
}

int[,] Powers()
{
    var powers = new int[Size, Size];
    foreach (var (x, y) in Cells()) powers[x, y] = grid[x, y]?.Power ?? 0;
    return powers;
}

bool CanMerge() => Cells().Any(c => grid[c.X, c.Y] is { } piece &&
    (Inside(c.X + 1, c.Y) && grid[c.X + 1, c.Y]?.Power == piece.Power || Inside(c.X, c.Y + 1) && grid[c.X, c.Y + 1]?.Power == piece.Power));

// Whether a slide toward (dx, dy) would move anything, for the autopilot, tried on the powers alone.
bool Moves(int dx, int dy) => Cells().Any(c =>
    grid[c.X, c.Y] is { } piece && Inside(c.X + dx, c.Y + dy) &&
    (grid[c.X + dx, c.Y + dy] is not { } next || next.Power == piece.Power));

string Board() => string.Join("/", Enumerable.Range(0, Size).Select(y =>
    string.Join(".", Enumerable.Range(0, Size).Select(x => grid[x, y] is { } p ? 1 << p.Power : 0))));

while (!WindowShouldClose())
{
    var dt = GetFrameTime();
    var gesture = GetGestureDetected();
    slide = MathF.Min(1, slide + dt / SlideSeconds);
    banner = MathF.Max(0, banner - dt);
    foreach (var piece in pieces)
    {
        piece.Grow += dt;
        piece.Pop += dt;
    }

    // -- Input, the keyboard's or the gestures of a finger or the mouse.
    var (dx, dy) = (0, 0);
    if (IsKeyPressed(Key.Left) || IsKeyPressed(Key.A) || gesture == Gesture.SwipeLeft) dx = -1;
    else if (IsKeyPressed(Key.Right) || IsKeyPressed(Key.D) || gesture == Gesture.SwipeRight) dx = 1;
    else if (IsKeyPressed(Key.Up) || IsKeyPressed(Key.W) || gesture == Gesture.SwipeUp) dy = -1;
    else if (IsKeyPressed(Key.Down) || IsKeyPressed(Key.S) || gesture == Gesture.SwipeDown) dy = 1;

    // A hold of a second starts again, once a hold.
    var restart = IsKeyPressed(Key.R) || gesture == Gesture.Hold && GetGestureHoldDuration() >= 1 && !holdUsed;
    holdUsed = gesture == Gesture.Hold && (holdUsed || restart);

    switch (screen)
    {
        case Screen.Title or Screen.Over when IsKeyPressed(Key.Enter) || gesture == Gesture.Tap:
            PlaySound(tapSound);
            NewGame();
            break;
        case Screen.Play or Screen.Over when gesture == Gesture.DoubleTap || IsKeyPressed(Key.U) || IsKeyPressed(Key.Backspace):
            Undo();
            break;
        case Screen.Play or Screen.Over when restart:
            PlaySound(tapSound);
            NewGame();
            break;
        case Screen.Play when dx != 0 || dy != 0:
            Move(dx, dy);
            break;
        case Screen.Play when SlideCommands.Autopilot && slide >= 1 && ++pilotIn >= 4:
            // The autopilot keeps the biggest tiles in a corner, sliding down, then left, then
            // right, and up only when nothing else moves.
            pilotIn = 0;
            foreach (var (mx, my) in new[] { (0, 1), (-1, 0), (1, 0), (0, -1) })
                if (Moves(mx, my) && Move(mx, my)) break;
            break;
    }

    // Nearer with a pinch out or the wheel, farther with a pinch in, and the board leaning with a drag.
    distance = Math.Clamp(distance - GetMouseWheelMove() * 0.5f
        + (gesture == Gesture.PinchIn ? 0.2f : gesture == Gesture.PinchOut ? -0.2f : 0), 5, 12);
    lean = Vector2.Lerp(lean, gesture == Gesture.Drag ? GetGestureDragVector() : Vector2.Zero, 1 - MathF.Exp(-dt * 10));
    var camera = new Camera3D(new Vector3(lean.X * 3, distance * 0.85f, distance * 0.55f + lean.Y * 3), new Vector3(0, 0, 0.3f), Vector3.UnitY, 45);

    var biggest = pieces.Where(p => !p.Gone).Select(p => 1 << p.Power).DefaultIfEmpty(0).Max();
    SlideCommands.Status = screen == Screen.Title ? $"Title best {best}"
        : $"{screen} score {score} best {best} moves {moves} undos {undos} biggest {biggest} board {Board()}";

    // -- Drawing.

    BeginDrawing();
    ClearBackground(new Color(250, 248, 239));
    BeginMode3D(camera);
    DrawCube(new Vector3(0, -0.2f, 0), 4.4f, 0.3f, 4.4f, new Color(187, 173, 160));
    foreach (var (x, y) in Cells())
    {
        var at = Place(x, y);
        DrawCube(new Vector3(at.X, -0.04f, at.Y), 0.94f, 0.02f, 0.94f, new Color(205, 193, 180));
    }
    var eased = slide * slide * (3 - 2 * slide);
    foreach (var piece in pieces)
    {
        // A tile slides, a merged one grows a little and settles, and a new one grows in. One that
        // slid into another is gone once it arrives, the other showing their sum.
        if (piece.Gone && slide >= 1) continue;
        var at = Vector2.Lerp(piece.From, piece.To, eased);
        var scale = piece.Grow < 0 ? 0 : MathF.Min(1, piece.Grow / 0.12f);
        if (piece.Pop is > 0 and < 0.15f) scale *= 1 + 0.15f * MathF.Sin(piece.Pop / 0.15f * MathF.PI);
        // A merged tile shows its old number until the one sliding into it arrives.
        var shown = piece.Pop < 0 ? piece.Power - 1 : piece.Power;
        if (scale > 0 && shown > 0) DrawModelEx(blocks[Math.Min(shown, Kinds - 1)], new Vector3(at.X, 0.16f, at.Y), Vector3.UnitY, 0, new Vector3(scale), Color.White);
    }
    EndMode3D();

    DrawTextEx(font, $"{score}", new Vector2(24, 16), 52, 0, new Color(119, 110, 101));
    DrawTextEx(font, $"best {best}   moves {moves}", new Vector2(26, 74), 22, 0, new Color(150, 140, 130));
    DrawTextEx(font, "Swipe or arrows slide, a double tap or U takes a move back, a hold or R starts again", new Vector2(24, GetScreenHeight() - 40), 20, 0, new Color(150, 140, 130));
    if (gesture == Gesture.Hold && !holdUsed)
        DrawRing(GetMousePosition(), 22, 30, -90, -90 + 360 * MathF.Min(1, GetGestureHoldDuration()), 32, new Color(119, 110, 101, 200));
    if (screen == Screen.Title) Banner("SLIDE", "Tap or Enter to play");
    else if (screen == Screen.Over) Banner("No moves left", "Tap or Enter to play again, a double tap to take the last move back");
    else if (banner > 0) Banner("2048", "Keep going");
    EndDrawing();
}

foreach (var model in blocks.Skip(1)) UnloadModel(model);
foreach (var face in faces.Skip(1)) UnloadTexture(face);
foreach (var sound in new[] { slideSound, popSound, tapSound, overSound }) UnloadSound(sound);
UnloadFont(font);
CloseAudioDevice();
CloseWindow();

// A large word across the middle of the window with a line under it, on a pale band.
void Banner(string title, string line)
{
    var middle = GetScreenHeight() / 2f;
    DrawRectangle(0, (int)middle - 110, GetScreenWidth(), 200, new Color(250, 248, 239, 210));
    var big = MeasureTextEx(font, title, 110, 0);
    DrawTextEx(font, title, new Vector2((GetScreenWidth() - big.X) / 2, middle - 105), 110, 0, new Color(119, 110, 101));
    var small = MeasureTextEx(font, line, 26, 0);
    DrawTextEx(font, line, new Vector2((GetScreenWidth() - small.X) / 2, middle + 30), 26, 0, new Color(150, 140, 130));
}

static IEnumerable<(int X, int Y)> Cells()
{
    for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            yield return (x, y);
}

static bool Inside(int x, int y) => x is >= 0 and < Size && y is >= 0 and < Size;

// Where a cell is on the board, its x the world's x and its y the world's z, toward the viewer.
static Vector2 Place(int x, int y) => new(x - 1.5f, y - 1.5f);

static Color Darker(Color color) => new((byte)(color.R * 0.85f), (byte)(color.G * 0.85f), (byte)(color.B * 0.85f), color.A);

public enum Screen { Title, Play, Over }

// A tile: its power of two, where it slides from and to, and how long since it was made and since
// it last merged, negative while the slide that brings it is still under way.
public sealed class Piece(int power, Vector2 at)
{
    public int Power { get; set; } = power;
    public Vector2 From { get; set; } = at;
    public Vector2 To { get; set; } = at;
    public bool Gone { get; set; }
    public float Grow { get; set; } = 1;
    public float Pop { get; set; } = 1;
}

public static class SlideCommands
{
    internal static bool Autopilot;
    internal static string Status = "";

    [Command("slide.status", "The screen, the score and the best, the moves made and taken back, the biggest tile, and the board, its rows from the far one, a cell's number or 0")]
    internal static string Report() => Status;

    [Command("slide.autopilot", "Slides down, left, right and up in that order of choice, a move every four frames, for a game that plays itself: slide.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "playing itself" : "played by the player";
    }
}
