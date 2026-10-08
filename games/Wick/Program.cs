// Wick, a puzzle in a dark house: the player carries a lamp through rooms no other light reaches,
// lights every wick on the way and leaves by the door. The lamp casts shadows, so its light stops
// at a wall and reaches round a corner or through a doorway only as light that bounces, and the
// pits in the polished floors show only where some light falls on them. Written against the
// engine's package, as a game outside this repository would be.
using System.Numerics;
using Engine;
using Wick;
using static Engine.Engine3D;

InitWindow(960, 540, "Wick");
InitAudioDevice();
SetTargetFPS(60);

var house = House.Load("resources/house.txt");
// Low by default, which a device that draws on its CPU keeps up with, and G steps through the rest.
var quality = GlobalIllumination.Low;
House.Light(quality);

// -- The player, a cloaked figure with a lamp held before it, the house's one light at the start

var cloak = LoadModelFromMesh(GenMeshCylinder(0.25f, 1.1f, 16));
cloak.Materials[0] = new ModelMaterial(new Color(120, 40, 44)) { Roughness = 0.8f };
var head = LoadModelFromMesh(GenMeshSphere(0.2f, 12, 12));
head.Materials[0] = new ModelMaterial(new Color(220, 190, 160)) { Roughness = 0.7f };
var glass = LoadModelFromMesh(GenMeshSphere(0.1f, 10, 10));
glass.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 200, 130), EmissiveIntensity = 8, CastsShadows = false };
var lamp = House.Lamp(Vector3.Zero);

// -- Sounds, made as waves: a flare as a wick catches, a falling note into a pit, a chord as the door opens

const int Rate = 22050;
Wave Make(float seconds, Func<float, float> sample) => new()
{
    Samples = [.. Enumerable.Range(0, (int)(seconds * Rate)).Select(i => sample((float)i / Rate))],
    SampleRate = Rate,
    Channels = 1,
};
var crackle = new Random(7);
var flare = LoadSoundFromWave(Make(0.4f, t => (MathF.Sin(MathF.Tau * (300 + 500 * t) * t) * 0.3f + (crackle.NextSingle() - 0.5f) * 0.2f) * (1 - t / 0.4f)));
var fall = LoadSoundFromWave(Make(0.6f, t => MathF.Sin(MathF.Tau * (420 - 260 * t) * t) * 0.35f * (1 - t / 0.6f)));
var chord = LoadSoundFromWave(Make(1.2f, t => (MathF.Sin(MathF.Tau * 392 * t) + MathF.Sin(MathF.Tau * 523 * t) + MathF.Sin(MathF.Tau * 659 * t)) * 0.15f * MathF.Pow(1 - t / 1.2f, 2)));

// -- The game

var lit = new bool[house.Wicks.Count];
var flames = new LightHandle?[house.Wicks.Count];
var screen = Screen.Title;
var position = House.Center(house.Start);
var facing = 0f;
var checkpoint = house.Start;
var falls = 0;
var falling = -1f;
var open = false;
var camera = new Camera3D(Vector3.Zero, Vector3.Zero, Vector3.UnitY, 50);
const float Radius = 0.3f, Speed = 3;

// Back to the start, every wick out.
void Restart()
{
    for (int i = 0; i < flames.Length; i++)
        if (flames[i] is { } flame) UnloadLight(flame);
    Array.Clear(flames);
    Array.Clear(lit);
    (position, checkpoint, falls, falling, open) = (House.Center(house.Start), house.Start, 0, -1, false);
}

// Whether the player's circle at a point stands clear of every wall.
bool Clear(Vector3 at) =>
    new[] { new Vector3(-Radius, 0, -Radius), new Vector3(Radius, 0, -Radius), new Vector3(-Radius, 0, Radius), new Vector3(Radius, 0, Radius) }
        .All(corner => !house.IsWall(House.CellAt(at + corner)));

while (!WindowShouldClose())
{
    var dt = MathF.Min(GetFrameTime(), 0.1f);
    var start = IsKeyPressed(Key.Enter) || IsGamepadButtonPressed(0, GamepadButton.RightFaceDown);
    if (screen is Screen.Title or Screen.Won && start)
    {
        Restart();
        screen = Screen.Play;
    }
    else if (screen == Screen.Play && (IsKeyPressed(Key.P) || IsGamepadButtonPressed(0, GamepadButton.MiddleRight))) screen = Screen.Pause;
    else if (screen == Screen.Pause && (IsKeyPressed(Key.P) || IsGamepadButtonPressed(0, GamepadButton.MiddleRight))) screen = Screen.Play;
    if (screen == Screen.Play && IsKeyPressed(Key.R)) Restart();
    if (IsKeyPressed(Key.G))
    {
        quality = quality == GlobalIllumination.High ? GlobalIllumination.Low : quality + 1;
        SetGlobalIllumination(quality);
    }

    if (WickCommands.Warp is { } warp)
    {
        (position, WickCommands.Warp) = (warp, null);
        if (screen == Screen.Title) screen = Screen.Play;
    }

    if (screen == Screen.Play && falling < 0)
    {
        // The way to walk, from the keys, the left stick, or the autopilot's next cell.
        var move = Vector3.Zero;
        if (IsKeyDown(Key.W) || IsKeyDown(Key.Up)) move.Z -= 1;
        if (IsKeyDown(Key.S) || IsKeyDown(Key.Down)) move.Z += 1;
        if (IsKeyDown(Key.A) || IsKeyDown(Key.Left)) move.X -= 1;
        if (IsKeyDown(Key.D) || IsKeyDown(Key.Right)) move.X += 1;
        move += new Vector3(GetGamepadAxisMovement(0, GamepadAxis.LeftX), 0, GetGamepadAxisMovement(0, GamepadAxis.LeftY));
        if (WickCommands.Autopilot && Route.Next(house, House.CellAt(position), lit, open) is { } next)
            move = House.Center(next) - position;
        var length = move.Length();
        if (length > 0.05f)
        {
            move /= MathF.Max(length, 1);
            facing = MathF.Atan2(move.X, move.Z);
            // Each way alone, so the player slides along a wall rather than stopping at it.
            var step = move * Speed * dt;
            if (WickCommands.Autopilot) step = Vector3.Normalize(move) * MathF.Min(Speed * dt, length);
            if (Clear(position with { X = position.X + step.X })) position.X += step.X;
            if (Clear(position with { Z = position.Z + step.Z })) position.Z += step.Z;
        }

        // A wick catches from the lamp held near it, and the player starts there again after a fall.
        for (int i = 0; i < lit.Length; i++)
        {
            if (lit[i] || Vector3.Distance(position, House.Center(house.Wicks[i])) > 1) continue;
            lit[i] = true;
            checkpoint = house.Wicks[i];
            flames[i] = house.Flame(i);
            PlaySound(flare);
        }
        if (!open && lit.All(l => l))
        {
            open = true;
            PlaySound(chord);
        }
        if (house.IsPit(House.CellAt(position)))
        {
            falling = 0;
            PlaySound(fall);
        }
        if (open && Vector3.Distance(position, House.Center(house.Door)) < 0.6f) screen = Screen.Won;
    }
    else if (falling >= 0)
    {
        // Down into the pit for most of a second, then back at the last wick lit.
        falling += dt;
        if (falling > 0.8f)
        {
            (position, falling) = (House.Center(checkpoint), -1);
            falls++;
        }
    }

    var sink = falling > 0 ? falling * falling * 6 : 0;
    var forward = new Vector3(MathF.Sin(facing), 0, MathF.Cos(facing));
    var lampAt = position + forward * 0.4f + new Vector3(0, 1 - sink, 0);
    SetLightPosition(lamp, lampAt);
    camera.Position = position + new Vector3(0, 9, 6.5f);
    camera.Target = position;
    WickCommands.Status = $"{screen} at {position.X:0.00} {position.Z:0.00} lit {lit.Count(l => l)} of {lit.Length} falls {falls} open {(open ? "yes" : "no")} light {quality}";

    BeginDrawing();
    ClearBackground(Color.Black);
    BeginMode3D(camera);
    house.Draw(lit, open);
    DrawModel(cloak, position - new Vector3(0, sink, 0), 1, Color.White);
    DrawModel(head, position + new Vector3(0, 1.3f - sink, 0), 1, Color.White);
    DrawModel(glass, lampAt, 1, Color.White);
    EndMode3D();

    DrawText($"Wicks {lit.Count(l => l)} of {lit.Length}   Falls {falls}", 16, 12, 24, new Color(255, 210, 150));
    DrawText($"G light: {quality}", 16, GetScreenHeight() - 30, 18, new Color(180, 170, 160));
    if (screen == Screen.Title)
    {
        DrawText("WICK", GetScreenWidth() / 2 - MeasureText("WICK", 60) / 2, 150, 60, new Color(255, 200, 120));
        const string Line = "Carry the lamp through the dark house, light every wick, and leave by the door.";
        DrawText(Line, GetScreenWidth() / 2 - MeasureText(Line, 20) / 2, 240, 20, Color.White);
        const string Keys = "Enter starts, WASD or the stick walks, P pauses, R starts again.";
        DrawText(Keys, GetScreenWidth() / 2 - MeasureText(Keys, 20) / 2, 275, 20, Color.LightGray);
    }
    if (screen == Screen.Pause) DrawText("Paused", GetScreenWidth() / 2 - MeasureText("Paused", 40) / 2, 240, 40, Color.White);
    if (screen == Screen.Won)
    {
        var line = $"Out into the night, with {falls} {(falls == 1 ? "fall" : "falls")}. Enter plays again.";
        DrawText(line, GetScreenWidth() / 2 - MeasureText(line, 24) / 2, 240, 24, new Color(150, 230, 210));
    }
    EndDrawing();
}

foreach (var flame in flames)
    if (flame is { } light) UnloadLight(light);
UnloadLight(lamp);
foreach (var sound in new[] { flare, fall, chord }) UnloadSound(sound);
foreach (var model in new[] { cloak, head, glass }) UnloadModel(model);
house.UnloadModels();
CloseAudioDevice();
CloseWindow();

public enum Screen { Title, Play, Pause, Won }

/// <summary>The autopilot's way through the house, cell by cell round the walls and the pits.</summary>
public static class Route
{
    /// <summary>
    /// The next cell on the shortest way from <paramref name="from"/> to the nearest wick not yet
    /// lit, or to the door once it is open, or none where the player stands on it.
    /// </summary>
    public static (int X, int Z)? Next(House house, (int X, int Z) from, bool[] lit, bool open)
    {
        HashSet<(int X, int Z)> goals = open ? [house.Door] : [.. house.Wicks.Where((_, i) => !lit[i])];
        var came = new Dictionary<(int X, int Z), (int X, int Z)> { [from] = from };
        var queue = new Queue<(int X, int Z)>([from]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (goals.Contains(cell))
            {
                // Back along the way to the cell after the first.
                while (came[cell] != from) cell = came[cell];
                return cell;
            }
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = (cell.X + dx, cell.Z + dz);
                if (came.ContainsKey(next) || house.IsWall(next) || house.IsPit(next)) continue;
                came[next] = cell;
                queue.Enqueue(next);
            }
        }
        return null;
    }
}

public static class WickCommands
{
    internal static string Status = "";
    internal static bool Autopilot;
    internal static Vector3? Warp;

    [Command("wick.status", "The screen, where the player stands across the floor, x and z, the wicks lit, the falls, whether the door is open and the light's quality")]
    internal static string Report() => Status;

    [Command("wick.autopilot", "Walks round the walls and the pits to each wick and then out by the door: wick.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "the autopilot walks" : "the player walks";
    }

    [Command("wick.warp", "Puts the player at a point of the floor, x and z, starting play from the title: wick.warp <x> <z>")]
    internal static string Move(float x, float z)
    {
        Warp = new Vector3(x, 0, z);
        return $"the player stands at {x} {z}";
    }
}
