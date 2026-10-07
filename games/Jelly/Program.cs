// Jelly, a runner. A blob of jelly runs down a road of three lanes for as long as it lasts, jumping
// barriers, sliding under bars and changing lanes round blocks, faster the farther it goes, and it
// squashes and stretches as it runs, jumps and lands by the weights of its model's morph targets.
// Each run is recorded as automation events and can be watched again, the best kept in a file and
// played from the title. A run's course comes from its seed and each of its steps is a sixtieth of a
// second whatever the frame took, so its input played again makes the same run. Written against the
// engine's package, as a game outside this repository would be.
using System.Numerics;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Jelly");
InitAudioDevice();
SetTargetFPS(60);

var jelly = LoadModel("resources/jelly.gltf");
var font = LoadFontEx("resources/Lato-Regular.ttf", 64);
var (jumpSound, landSound, coinSound, crashSound) = (LoadSound("resources/sounds/jump.wav"), LoadSound("resources/sounds/land.wav"),
    LoadSound("resources/sounds/coin.wav"), LoadSound("resources/sounds/crash.wav"));
var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var coinModel = LoadModelFromMesh(GenMeshCylinder(0.35f, 0.08f, 20));
coinModel.Materials[0] = coinModel.Materials[0] with { Color = new Color(250, 196, 50), Emissive = new Color(255, 200, 60), EmissiveIntensity = 0.6f, Metallic = 0.8f, Roughness = 0.3f };

CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.2f)), Color.White, 1.5f, castsShadows: true);
SetAmbientLight(new Color(160, 180, 230), 0.5f);

// -- The runs: the one under way, the input recorded of it, and the best kept in a file with the
// seed its course came from and how far it went.

const string BestFile = "jelly-best.rae", BestOfFile = "jelly-best.txt";
var best = (Seed: 0, Distance: 0, Score: 0);
if (FileExists(BestOfFile) && LoadFileText(BestOfFile)?.Split(' ') is [var s, var d, var c] &&
    int.TryParse(s, out var bestSeed) && int.TryParse(d, out var bestDistance) && int.TryParse(c, out var bestScore))
    best = (bestSeed, bestDistance, bestScore);
var screen = Screen.Title;
var seed = best.Seed + 1;
var run = new Run(seed);
var recorded = LoadAutomationEventList(null);
var (lastSeed, lastEnd) = (0, "");
AutomationEventList? watched = null;
var (playAt, watchedName) = (0, "");
var camera = new Camera3D(new Vector3(0, 3.4f, 6), new Vector3(0, 1, -6), Vector3.UnitY, 55);

// A run begins, recorded from its first step as the frames are counted from 0, or a recorded run is
// played from its first step on the course its seed makes.
void Begin(int runSeed, AutomationEventList? play, string name)
{
    run = new Run(runSeed);
    (watched, playAt, watchedName) = (play, 0, name);
    screen = play is null ? Screen.Running : Screen.Watching;
    if (play is not null) return;
    UnloadAutomationEventList(recorded);
    recorded = LoadAutomationEventList(null);
    SetAutomationEventList(recorded);
}

while (!WindowShouldClose())
{
    // The recording starts with the run's first step, counted as frame 0.
    if (screen == Screen.Running && run.Frames == 0)
    {
        SetAutomationEventBaseFrame(0);
        StartAutomationEventRecording();
    }

    // A recorded run's events of this step are played before its input is read, as a person's
    // input would have arrived.
    if (screen == Screen.Watching && watched is { } list)
        while (playAt < list.Count && list.Events[playAt].Frame <= run.Frames)
            PlayAutomationEvent(list.Events[playAt++]);

    var gesture = GetGestureDetected();
    var left = IsKeyPressed(Key.Left) || IsKeyPressed(Key.A) || IsGamepadButtonPressed(0, GamepadButton.LeftFaceLeft) || gesture == Gesture.SwipeLeft;
    var right = IsKeyPressed(Key.Right) || IsKeyPressed(Key.D) || IsGamepadButtonPressed(0, GamepadButton.LeftFaceRight) || gesture == Gesture.SwipeRight;
    var jump = IsKeyPressed(Key.Up) || IsKeyPressed(Key.W) || IsKeyPressed(Key.Space) || IsGamepadButtonPressed(0, GamepadButton.RightFaceDown) || gesture == Gesture.SwipeUp;
    var slide = IsKeyPressed(Key.Down) || IsKeyPressed(Key.S) || IsGamepadButtonPressed(0, GamepadButton.RightFaceRight) || gesture == Gesture.SwipeDown;

    switch (screen)
    {
        case Screen.Title or Screen.Crashed when IsKeyPressed(Key.Enter) || IsGamepadButtonPressed(0, GamepadButton.MiddleRight):
            Begin(++seed, null, "");
            break;
        case Screen.Crashed when IsKeyPressed(Key.R) && recorded.Count > 0:
            Begin(lastSeed, recorded, "this run");
            break;
        case Screen.Title or Screen.Crashed when IsKeyPressed(Key.B) && best.Seed > 0 && FileExists(BestFile):
            Begin(best.Seed, LoadAutomationEventList(BestFile), "the best run");
            break;
        case Screen.Running or Screen.Watching:
            if (JellyCommands.Autopilot && screen == Screen.Running) (left, right, jump, slide) = run.Pilot();
            var happened = run.Advance(left, right, jump, slide);
            if (happened.Jumped) PlaySound(jumpSound);
            if (happened.Landed) PlaySound(landSound);
            if (happened.Coin) PlaySound(coinSound);
            if (!run.Crashed) break;

            PlaySound(crashSound);
            lastEnd = $"distance {run.Distance} score {run.Score} coins {run.Coins} frames {run.Frames}";
            if (screen == Screen.Running)
            {
                StopAutomationEventRecording();
                lastSeed = run.Seed;
                // The best run's input kept, with its seed and how far it went, to be played again.
                if (run.Score > best.Score && ExportAutomationEventList(recorded, BestFile))
                {
                    best = (run.Seed, run.Distance, run.Score);
                    SaveFileText(BestOfFile, $"{best.Seed} {best.Distance} {best.Score}");
                }
            }
            screen = Screen.Crashed;
            break;
    }

    // Its shape follows the run, squashed as it lands or slides and stretched as it rises.
    SetModelMorphWeight(jelly, "Squash", run.Squash);
    SetModelMorphWeight(jelly, "Stretch", run.Stretch);

    // The camera behind and above, half following the lane changes.
    var at = new Vector3(run.X, run.Y, -run.Z);
    camera = camera with { Position = new Vector3(run.X * 0.5f, 3.4f, -run.Z + 6), Target = new Vector3(run.X * 0.7f, 1, -run.Z - 6) };

    JellyCommands.Status = screen switch
    {
        Screen.Title => $"Title best {best.Distance} seed {best.Seed}",
        Screen.Crashed => $"Crashed {lastEnd} watched {(watchedName == "" ? "none" : watchedName.Replace(' ', '-'))}",
        _ => $"{screen} distance {run.Distance} score {run.Score} coins {run.Coins} frames {run.Frames} lane {run.Lane} recorded {recorded.Count}",
    };

    // -- Drawing.

    BeginDrawing();
    ClearBackground(new Color(140, 190, 240));
    BeginMode3D(camera);
    // The road in pieces from a little behind to far ahead, its lanes striped, and posts beside it.
    var start = MathF.Floor(run.Z / 10) * 10 - 10;
    for (var z = start; z < start + 140; z += 10)
    {
        DrawCube(new Vector3(0, -0.05f, -z - 5), Road.LaneWidth * 3 + 0.6f, 0.1f, 10, (int)(z / 10) % 2 == 0 ? new Color(70, 70, 86) : new Color(76, 76, 92));
        for (int stripe = -1; stripe <= 1; stripe += 2)
            DrawCube(new Vector3(stripe * Road.LaneWidth / 2, 0.005f, -z - 2.5f), 0.08f, 0.02f, 3, new Color(230, 230, 240));
        foreach (var side in new[] { -1, 1 })
            DrawCube(new Vector3(side * (Road.LaneWidth * 1.5f + 0.6f), 0.6f, -z), 0.25f, 1.2f, 0.25f, new Color(240, 120, 90));
        DrawCube(new Vector3(0, -0.6f, -z - 5), 40, 1, 10, new Color(110, 170, 90));
    }
    foreach (var obstacle in run.Obstacles)
    {
        var x = (obstacle.Lane - 1) * Road.LaneWidth;
        var (center, size, color) = obstacle.Kind switch
        {
            Kind.Barrier => (new Vector3(x, 0.3f, -obstacle.Z), new Vector3(Road.LaneWidth * 0.85f, 0.6f, 0.5f), new Color(250, 200, 60)),
            Kind.Bar => (new Vector3(x, 1.05f, -obstacle.Z), new Vector3(Road.LaneWidth * 0.85f, 0.5f, 0.5f), new Color(230, 80, 80)),
            _ => (new Vector3(x, 1.25f, -obstacle.Z), new Vector3(Road.LaneWidth * 0.85f, 2.5f, 1.2f), new Color(120, 90, 200)),
        };
        DrawModelEx(block, center, Vector3.UnitY, 0, size, color);
        // A bar stands on two posts.
        if (obstacle.Kind == Kind.Bar)
            foreach (var side in new[] { -1, 1 })
                DrawCube(new Vector3(x + side * Road.LaneWidth * 0.4f, 0.4f, -obstacle.Z), 0.12f, 0.8f, 0.12f, new Color(200, 200, 210));
    }
    foreach (var coin in run.CoinsOnRoad)
        DrawModelEx(coinModel, new Vector3(coin.X, coin.Y, -coin.Z), Vector3.UnitX, 90, Vector3.One, Color.White);
    DrawModel(jelly, at, 1, Color.White);
    EndMode3D();

    DrawTextEx(font, $"{run.Distance} m", new Vector2(24, 16), 52, 0, Color.White);
    DrawTextEx(font, $"{run.Coins} coins   score {run.Score}", new Vector2(26, 74), 24, 0, Color.White);
    if (best.Distance > 0) DrawTextEx(font, $"best {best.Distance} m", new Vector2(GetScreenWidth() - 200, 20), 26, 0, Color.White);
    if (screen == Screen.Watching) Centered($"Watching {watchedName}", 110, 34, Color.Gold);
    if (screen == Screen.Title)
    {
        Centered("JELLY", GetScreenHeight() * 0.28f, 130, Color.White);
        Centered("Enter or Start to run. Left and right change lanes, up jumps, down slides", GetScreenHeight() * 0.52f, 28, Color.White);
        if (best.Seed > 0) Centered($"B watches the best run, {best.Distance} m", GetScreenHeight() * 0.6f, 28, Color.Gold);
    }
    else if (screen == Screen.Crashed)
    {
        // The title's size, so the font is baked once for both and not again at the first crash.
        Centered("Splat", GetScreenHeight() * 0.3f, 130, Color.White);
        Centered($"{run.Distance} m, {run.Score} points", GetScreenHeight() * 0.5f, 34, Color.Gold);
        Centered("Enter runs again, R watches this run again, B the best", GetScreenHeight() * 0.58f, 28, Color.White);
    }
    EndDrawing();
}

UnloadAutomationEventList(recorded);
foreach (var model in new[] { jelly, block, coinModel }) UnloadModel(model);
foreach (var sound in new[] { jumpSound, landSound, coinSound, crashSound }) UnloadSound(sound);
UnloadFont(font);
CloseAudioDevice();
CloseWindow();

void Centered(string text, float y, float size, Color color)
{
    var measured = MeasureTextEx(font, text, size, 0);
    DrawTextEx(font, text, new Vector2((GetScreenWidth() - measured.X) / 2, y), size, 0, color);
}

public enum Screen { Title, Running, Watching, Crashed }

public enum Kind { Barrier, Bar, Block }

public readonly record struct Obstacle(Kind Kind, int Lane, float Z);

// A run, which moves only by its steps and the input each is given, so the same seed and the same
// input make the same run on any machine and at any frame rate.
public sealed class Run(int seed)
{
    private const float Step = 1 / 60f, Gravity = 26, JumpSpeed = 9.5f, SlideSeconds = 0.6f;
    private readonly Random _random = new(seed);
    private float _nextRow = 40, _slideLeft;

    public int Seed { get; } = seed;
    public float X { get; private set; }
    public float Y { get; private set; }
    public float Z { get; private set; }
    public int Lane { get; private set; } = 1;
    public int Coins { get; private set; }
    public int Frames { get; private set; }
    public bool Crashed { get; private set; }
    public float Squash { get; private set; }
    public float Stretch { get; private set; }
    public List<Obstacle> Obstacles { get; } = [];
    public List<Vector3> CoinsOnRoad { get; } = [];
    private float _vy;
    private bool Grounded => Y <= 0 && _vy <= 0;
    private bool Sliding => _slideLeft > 0;
    public int Distance => (int)Z;
    public int Score => Distance + Coins * 25;

    public (bool Jumped, bool Landed, bool Coin) Advance(bool left, bool right, bool jump, bool slide)
    {
        var (jumped, landed, coin) = (false, false, false);
        Frames++;
        if (left && Lane > 0) Lane--;
        if (right && Lane < 2) Lane++;
        X += ((Lane - 1) * Road.LaneWidth - X) * 0.25f;
        if (jump && Grounded)
        {
            (_vy, _slideLeft, jumped) = (JumpSpeed, 0, true);
        }
        if (slide)
        {
            // In the air a slide brings it down at once, as a runner drops to the ground.
            if (Grounded) _slideLeft = SlideSeconds;
            else _vy = -JumpSpeed * 1.5f;
        }
        if (!Grounded || _vy > 0)
        {
            _vy -= Gravity * Step;
            Y += _vy * Step;
            if (Y <= 0)
            {
                (Y, _vy, landed) = (0, 0, true);
                Squash = 1;
            }
        }
        _slideLeft = MathF.Max(0, _slideLeft - Step);
        Z += MathF.Min(30, 12 + Z * 0.012f) * Step;

        // The course is made ahead of the runner, a row every 13 to 20 units.
        while (_nextRow < Z + 130)
        {
            MakeRow(_nextRow);
            _nextRow += 13 + _random.NextSingle() * 7;
        }
        Obstacles.RemoveAll(o => o.Z < Z - 10);
        CoinsOnRoad.RemoveAll(c => c.Z < Z - 10);

        Stretch = Y > 0 ? Math.Clamp(_vy / JumpSpeed, 0, 1) : 0;
        Squash = Sliding ? 1 : MathF.Max(Squash - 5 * Step, Y > 0 ? 0 : 0.12f * MathF.Abs(MathF.Sin(Frames * 0.45f)));

        // What the runner is in, by the lane it is nearest.
        var lane = (int)MathF.Round(X / Road.LaneWidth) + 1;
        foreach (var obstacle in Obstacles)
        {
            if (obstacle.Lane != lane || MathF.Abs(obstacle.Z - Z) > (obstacle.Kind == Kind.Block ? 1.0f : 0.65f)) continue;
            Crashed |= obstacle.Kind switch
            {
                Kind.Barrier => Y < 0.6f,
                Kind.Bar => !Sliding && Y < 1.3f,
                _ => true,
            };
        }
        for (int i = CoinsOnRoad.Count - 1; i >= 0; i--)
        {
            var c = CoinsOnRoad[i];
            if (MathF.Abs(c.X - X) > 0.8f || MathF.Abs(c.Z - Z) > 0.8f || Y > 1.4f) continue;
            CoinsOnRoad.RemoveAt(i);
            Coins++;
            coin = true;
        }
        return (jumped, landed, coin);
    }

    // A row of one or two obstacles in lanes the seed picks, never all three, and coins down a free one.
    private void MakeRow(float z)
    {
        int[] lanes = [.. new[] { 0, 1, 2 }.OrderBy(_ => _random.Next())];
        var count = _random.NextSingle() < 0.35f ? 2 : 1;
        for (int i = 0; i < count; i++) Obstacles.Add(new Obstacle((Kind)_random.Next(3), lanes[i], z));
        for (int k = 0; k < 4; k++) CoinsOnRoad.Add(new Vector3((lanes[count] - 1) * Road.LaneWidth, 0.6f, z - 4 + k * 1.6f));
    }

    // The autopilot looks a little ahead in its lane and the others, and does what clears the
    // nearest obstacle: a jump over a barrier, a slide under a bar, and another lane round a block.
    public (bool Left, bool Right, bool Jump, bool Slide) Pilot()
    {
        Obstacle? Ahead(int lane) => Obstacles.Where(o => o.Lane == lane && o.Z > Z - 0.5f && o.Z < Z + 9).OrderBy(o => o.Z).Cast<Obstacle?>().FirstOrDefault();
        if (Ahead(Lane) is not { } next) return (false, false, false, false);
        var near = next.Z - Z < 3.2f;
        if (next.Kind == Kind.Block || next.Kind == Kind.Bar && !near)
        {
            // Another lane clear ahead, the nearer first.
            foreach (var other in new[] { Lane - 1, Lane + 1 }.Where(l => l is >= 0 and <= 2))
                if (Ahead(other) is not { Kind: Kind.Block })
                    return ((other < Lane), (other > Lane), false, false);
        }
        return near ? (false, false, next.Kind == Kind.Barrier, next.Kind == Kind.Bar) : (false, false, false, false);
    }
}

public static class Road
{
    // How far apart the lanes' middles are.
    public const float LaneWidth = 2.2f;
}

public static class JellyCommands
{
    internal static bool Autopilot;
    internal static string Status = "";

    [Command("jelly.status", "The screen, how far the run went, its score, coins and steps, its lane and the events recorded, or how the last run ended and which was watched")]
    internal static string Report() => Status;

    [Command("jelly.autopilot", "Clears what is ahead by itself, a jump, a slide or another lane, for a run that plays itself: jelly.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "running by itself" : "run by the player";
    }
}
