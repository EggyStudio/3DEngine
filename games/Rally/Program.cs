// Rally, three laps of a dirt road over hills against the clock, and against the ghost of the best
// lap driven before. The car is a body held up by four rays, one a wheel, which push it as springs,
// grip the ground sideways and drive it forward, worked out on the physics' fixed steps.
using System.Numerics;
using Engine;
using ImGuiNET;
using static Engine.Engine3D;

InitWindow(1280, 720, "Rally");
InitAudioDevice();
SetTargetFPS(60);
SetBloom(0.25f);
SetExposure(1.1f);

// -- The course: hills from noise with a road around them, and eight gates along the road.

var heights = GenImagePerlinNoise(Course.Pixels, Course.Pixels, 40, 40, 2.5f);
var colors = GenImageColor(Course.Pixels, Course.Pixels, Color.Green);
for (int y = 0; y < Course.Pixels; y++)
    for (int x = 0; x < Course.Pixels; x++)
    {
        // Gentle hills, lower toward the middle, where the road runs flat enough to drive fast.
        var h = GetImageColor(heights, x, y).R / 255f;
        var (wx, wz) = Course.World(x, y);
        var toRoad = Course.DistanceToRoad(new Vector2(wx, wz));
        var level = 0.35f + h * 0.4f;
        var flat = Math.Clamp((toRoad - Course.RoadWidth) / 10, 0, 1);
        var height = level * flat + Course.RoadHeight(new Vector2(wx, wz)) * (1 - flat);
        ImageDrawPixel(ref heights, x, y, new Color((byte)(height * 255), (byte)(height * 255), (byte)(height * 255)));
        var grass = new Color((byte)(70 + h * 50), (byte)(120 + h * 60), (byte)(50 + h * 20));
        var dirt = new Color(150, 115, 80);
        ImageDrawPixel(ref colors, x, y, toRoad < Course.RoadWidth ? dirt : grass);
    }
var terrain = LoadModelFromMesh(GenMeshHeightmap(heights, new Vector3(Course.Size, Course.Height, Course.Size)));
terrain.Materials[0] = terrain.Materials[0] with { Texture = LoadTextureFromImage(colors), Roughness = 0.9f };
var terrainAt = new Vector3(-Course.Size / 2, 0, -Course.Size / 2);
CreatePhysicsStaticModel(terrain, terrainAt);

var gates = new PhysicsBody[Course.Gates];
for (int g = 0; g < Course.Gates; g++)
    gates[g] = CreatePhysicsTrigger(Course.Gate(g) + new Vector3(0, 3, 0), new Vector3(Course.RoadWidth * 3, 8, Course.RoadWidth * 3));

CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.5f, -1, -0.3f)), new Color(255, 245, 225), 2.6f, castsShadows: true);
SetAmbientLight(new Color(150, 180, 230), 0.35f);
SetShadowDistance(80);

// -- The car, its look and its sounds.

var car = new Car(new Vector3(0, 0, 0));
Car.Current = car;
car.Reset(Course.Start, Course.Along(0));
var body = LoadModelFromMesh(GenMeshCube(Car.Half.X * 2, Car.Half.Y * 2, Car.Half.Z * 2));
var cabin = LoadModelFromMesh(GenMeshCube(1.5f, 0.5f, 1.8f));
var wheel = LoadModelFromMesh(GenMeshCylinder(Car.WheelRadius, 0.3f, 16));
var post = LoadModelFromMesh(GenMeshCube(0.4f, 4, 0.4f));
var banner = LoadModelFromMesh(GenMeshCube(Course.RoadWidth * 2 + 0.4f, 0.6f, 0.2f));

var engine = LoadMusicStream("resources/engine.wav");
PlayMusicStream(engine);
SetMusicVolume(engine, 0.35f);
Sound Load(string name, float volume)
{
    var sound = LoadSound($"resources/{name}.wav");
    SetSoundVolume(sound, volume);
    return sound;
}
var (skid, checkpoint, beep, go, lapDone, finish) =
    (Load("skid", 0.25f), Load("checkpoint", 0.5f), Load("beep", 0.5f), Load("go", 0.6f), Load("lap", 0.6f), Load("finish", 0.7f));

// Dust kicked up behind each rear wheel, laid over by alpha and lit by the sun.
var dust = new[] { 0, 1 }.Select(_ => CreateParticleEmitter(Vector3.Zero, ParticleEmitter.Default with
{
    MaxParticles = 600, Emitting = false, Life = 1.6f, LifeVariation = 0.4f, Velocity = new Vector3(0, 1.2f, 0), Spread = 50,
    SpeedVariation = 0.5f, Gravity = new Vector3(0, -0.6f, 0), Radius = 0.25f, StartSize = 0.5f, EndSize = 2.2f,
    StartColor = new Color(170, 140, 105, 140), EndColor = new Color(190, 170, 140, 0), Lit = true, Blend = ParticleBlend.Alpha,
})).ToArray();

// -- The race.

AddState(Screen.Menu);
var race = new Race();
var best = float.TryParse(FileExists(Race.BestFile) ? LoadFileText(Race.BestFile) : null, System.Globalization.CultureInfo.InvariantCulture, out var saved) ? saved : 0f;
var camera = new Camera3D(car.Position + new Vector3(0, 4, 8), car.Position, Vector3.UnitY, 60);
var skidCooldown = 0f;

while (!WindowShouldClose())
{
    var dt = GetFrameTime();
    var screen = GetState<Screen>();
    UpdateMusicStream(engine);

    // -- Menu, countdown, race and finish.
    if (screen == Screen.Menu && (IsKeyPressed(Key.Enter) || IsGamepadButtonPressed(0, GamepadButton.Start)))
    {
        race = new Race();
        car.Reset(Course.Start, Course.Along(0));
        SetState(Screen.Countdown);
    }
    if (screen == Screen.Countdown)
    {
        var before = race.Countdown;
        race.Countdown -= dt;
        if (MathF.Ceiling(race.Countdown) < MathF.Ceiling(before) && race.Countdown > 0) PlaySound(beep);
        if (race.Countdown <= 0)
        {
            PlaySound(go);
            SetState(Screen.Race);
        }
    }
    if (screen == Screen.Race)
    {
        race.LapTime += dt;
        race.Ghost.Add((race.LapTime, car.Position, car.Rotation));
        foreach (var contact in GetPhysicsContacts())
            if (contact.BodyA == car.Body || contact.BodyB == car.Body)
            {
                var other = contact.BodyA == car.Body ? contact.BodyB : contact.BodyA;
                if (other != gates[race.Next % Course.Gates]) continue;
                race.Next++;
                if (race.Next % Course.Gates != 1 || race.Next == 1) { PlaySound(checkpoint); continue; }

                // Through the first gate again, a lap: its time, the best kept, its ghost kept with it.
                race.Laps.Add(race.LapTime);
                if (best == 0 || race.LapTime < best)
                {
                    best = race.LapTime;
                    race.BestGhost = race.Ghost.ToArray();
                    SaveFileText(Race.BestFile, best.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                (race.LapTime, race.Ghost) = (0, []);
                if (race.Laps.Count == Race.LapsToRun)
                {
                    PlaySound(finish);
                    SetState(Screen.Finished);
                }
                else PlaySound(lapDone);
            }
    }
    if (screen == Screen.Finished && (IsKeyPressed(Key.Enter) || IsGamepadButtonPressed(0, GamepadButton.Start))) SetState(Screen.Menu);

    // -- Driving, from the keys or a pad, or the autopilot the e3d command turns on.
    var (throttle, steer, brake) = (0f, 0f, false);
    if (screen == Screen.Race)
    {
        // The autopilot follows the road a little ahead of the car, which passes every gate in turn,
        // where heading for a gate's middle could circle it.
        if (RallyCommands.Autopilot) (throttle, steer) = car.Toward(Course.Ahead(car.Position, 0.015f));
        else
        {
            throttle = (IsKeyDown(Key.W) || IsKeyDown(Key.Up) ? 1 : 0) - (IsKeyDown(Key.S) || IsKeyDown(Key.Down) ? 1 : 0);
            steer = (IsKeyDown(Key.A) || IsKeyDown(Key.Left) ? 1 : 0) - (IsKeyDown(Key.D) || IsKeyDown(Key.Right) ? 1 : 0);
            if (IsGamepadAvailable(0))
            {
                throttle += GetGamepadAxisMovement(0, GamepadAxis.RightTrigger) - GetGamepadAxisMovement(0, GamepadAxis.LeftTrigger);
                var stick = GetGamepadAxisMovement(0, GamepadAxis.LeftX);
                if (MathF.Abs(stick) > 0.15f) steer = -stick;
            }
            brake = IsKeyDown(Key.Space) || IsGamepadButtonDown(0, GamepadButton.South);
        }
        // Back on the road at the last gate passed, upright, after a roll or on R.
        if (IsKeyPressed(Key.R) || car.Stuck > 3)
        {
            // Before the first gate, the last place passed is the start.
            var at = (race.Next + Course.Gates - 1) % Course.Gates;
            if (race.Next == 0) car.Reset(Course.Start, Course.Along(0));
            else car.Reset(Course.Gate(at) + new Vector3(0, 1, 0), Course.Along(at));
        }
    }
    car.Input = (Math.Clamp(throttle, -1, 1), Math.Clamp(steer, -1, 1), brake);
    SetPhysicsPaused(screen is Screen.Menu or Screen.Finished);
    RallyCommands.Status = $"{screen} lap {race.Laps.Count + 1}/{Race.LapsToRun} gate {race.Next} speed {car.Speed * 3.6f:0} at {car.Position.X:0},{car.Position.Y:0},{car.Position.Z:0} " +
                           $"time {race.LapTime:0.00} best {best:0.00} laps [{string.Join(",", race.Laps.Select(l => l.ToString("0.00")))}]";

    // -- Sound and dust from how the car drives.
    var speed = car.Speed;
    SetMusicPitch(engine, 0.6f + MathF.Min(speed, 45) / 30 + MathF.Max(0, throttle) * 0.15f);
    skidCooldown -= dt;
    for (int side = 0; side < 2; side++)
    {
        var w = car.Wheels[2 + side];
        if (!w.Grounded) continue;
        SetParticleEmitterPosition(dust[side], w.Contact);
        var kicked = (int)MathF.Min(8, (MathF.Abs(w.Slip) * 0.5f + speed * 0.04f) * dt * 60);
        if (kicked > 0) EmitParticles(dust[side], kicked);
        if (MathF.Abs(w.Slip) > 4 && skidCooldown <= 0)
        {
            SetSoundPitch(skid, 0.8f + Random.Shared.NextSingle() * 0.4f);
            PlaySound(skid);
            skidCooldown = 0.25f;
        }
    }
    SetMotionBlur(Math.Clamp((speed - 10) / 60, 0, 0.45f));

    // -- A camera behind the car, easing after it, looking a little ahead.
    var forward = Vector3.Transform(-Vector3.UnitZ, car.Rotation) with { Y = 0 };
    forward = forward.LengthSquared() > 1e-4f ? Vector3.Normalize(forward) : -Vector3.UnitZ;
    var eye = car.Position - forward * 8 + new Vector3(0, 3.2f, 0);
    var ease = 1 - MathF.Exp(-5 * dt);
    camera = camera with { Position = Vector3.Lerp(camera.Position, eye, ease), Target = Vector3.Lerp(camera.Target, car.Position + forward * 3 + Vector3.UnitY, ease) };

    BeginDrawing();
    ClearBackground(new Color(150, 190, 235));
    BeginMode3D(camera);
    DrawModel(terrain, terrainAt, 1, Color.White);
    for (int g = 0; g < Course.Gates; g++)
    {
        var at = Course.Gate(g);
        var side = Vector3.Normalize(Vector3.Cross(Course.Along(g), Vector3.UnitY)) * (Course.RoadWidth + 0.2f);
        var next = g == race.Next % Course.Gates && screen != Screen.Menu;
        var color = next ? new Color(255, 120, 40) : new Color(220, 220, 225);
        var yaw = MathF.Atan2(Course.Along(g).X, Course.Along(g).Z) * 180 / MathF.PI;
        DrawModel(post, at + side + new Vector3(0, 2, 0), 1, color);
        DrawModel(post, at - side + new Vector3(0, 2, 0), 1, color);
        banner.Materials[0] = banner.Materials[0] with { Emissive = next ? new Color(255, 120, 40) : Color.Black, EmissiveIntensity = 2.5f };
        DrawModelEx(banner, at + new Vector3(0, 4, 0), Vector3.UnitY, yaw, Vector3.One, color);
    }
    DrawCar(car.Position, car.Rotation, new Color(200, 40, 40), car.Wheels.Select(w => w.Center).ToArray(), car.Steer);
    if (screen == Screen.Race && race.BestGhost is { Length: > 1 } ghost)
    {
        var (gp, gr) = Race.At(ghost, race.LapTime);
        DrawCar(gp, gr, new Color(120, 200, 255, 90), null, 0);
    }
    EndMode3D();
    DrawHud();
    EndDrawing();
}

UnloadMusicStream(engine);
CloseAudioDevice();
CloseWindow();

// Draws the car's body, cabin and wheels at a pose, the wheels where their rays found the ground.
void DrawCar(Vector3 position, Quaternion rotation, Color color, Vector3[]? wheels, float steerAngle)
{
    var pose = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
    body.Transform = pose;
    DrawModel(body, Vector3.Zero, 1, color);
    cabin.Transform = Matrix4x4.CreateTranslation(0, Car.Half.Y + 0.25f, 0.2f) * pose;
    DrawModel(cabin, Vector3.Zero, 1, color.A < 255 ? color : new Color(40, 40, 50));
    if (wheels is null) return;
    for (int i = 0; i < wheels.Length; i++)
    {
        var turn = i < 2 ? steerAngle : 0;
        wheel.Transform = Matrix4x4.CreateTranslation(0, -0.15f, 0) * Matrix4x4.CreateRotationZ(MathF.PI / 2)
                          * Matrix4x4.CreateRotationX(car.Wheels[i].Roll) * Matrix4x4.CreateRotationY(turn)
                          * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(wheels[i]);
        DrawModel(wheel, Vector3.Zero, 1, new Color(30, 30, 30));
    }
}

void DrawHud()
{
    var screen = GetState<Screen>();
    const ImGuiWindowFlags still = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize
                                   | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav;
    if (screen is Screen.Race or Screen.Countdown)
    {
        ImGui.SetNextWindowPos(new Vector2(12, 12));
        ImGui.SetNextWindowBgAlpha(0.55f);
        ImGui.Begin("Lap", still);
        ImGui.Text($"Lap {Math.Min(race.Laps.Count + 1, Race.LapsToRun)} of {Race.LapsToRun}");
        ImGui.Text($"Time {race.LapTime:0.00}");
        ImGui.Text(best > 0 ? $"Best {best:0.00}" : "Best -");
        foreach (var (lap, n) in race.Laps.Select((l, i) => (l, i + 1))) ImGui.Text($"  {n}: {lap:0.00}");
        ImGui.End();
        DrawText($"{car.Speed * 3.6f:0} km/h", GetScreenWidth() - 200, GetScreenHeight() - 60, 40, Color.White);
        if (screen == Screen.Countdown) DrawText($"{MathF.Ceiling(race.Countdown):0}", GetScreenWidth() / 2 - 20, 200, 80, Color.White);
        return;
    }
    ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() / 2f, GetScreenHeight() / 2f), ImGuiCond.Always, new Vector2(0.5f));
    ImGui.SetNextWindowBgAlpha(0.75f);
    ImGui.Begin("Menu", still);
    if (screen == Screen.Menu)
    {
        ImGui.Text("RALLY");
        ImGui.Separator();
        ImGui.Text($"Three laps of the dirt road, through the orange gate each time.");
        ImGui.Text("W and S or the triggers drive, A and D or the stick steer, Space brakes, R puts the car back.");
        ImGui.Text(best > 0 ? $"Best lap {best:0.00}, whose ghost drives with you." : "No lap driven yet.");
    }
    else
    {
        ImGui.Text("FINISHED");
        ImGui.Separator();
        ImGui.Text($"Total {race.Laps.Sum():0.00}, best lap {best:0.00}");
    }
    ImGui.Text("Enter to drive");
    ImGui.End();
}

public enum Screen { Menu, Countdown, Race, Finished }

/// <summary>A race under way: the countdown, the gates passed, the laps and the poses of this lap, kept as a ghost when it is the best.</summary>
public sealed class Race
{
    public const int LapsToRun = 3;
    public const string BestFile = "rally-best.txt";
    public float Countdown = 3;
    public int Next;
    public float LapTime;
    public readonly List<float> Laps = [];
    public List<(float Time, Vector3 Position, Quaternion Rotation)> Ghost = [];
    public (float Time, Vector3 Position, Quaternion Rotation)[]? BestGhost;

    // A ghost's pose at a time into its lap, between the two kept either side.
    public static (Vector3, Quaternion) At((float Time, Vector3 Position, Quaternion Rotation)[] ghost, float time)
    {
        int lo = 0, hi = ghost.Length - 1;
        if (time >= ghost[hi].Time) return (ghost[hi].Position, ghost[hi].Rotation);
        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;
            if (ghost[mid].Time <= time) lo = mid; else hi = mid;
        }
        var t = Math.Clamp((time - ghost[lo].Time) / MathF.Max(1e-5f, ghost[hi].Time - ghost[lo].Time), 0, 1);
        return (Vector3.Lerp(ghost[lo].Position, ghost[hi].Position, t), Quaternion.Slerp(ghost[lo].Rotation, ghost[hi].Rotation, t));
    }
}

/// <summary>The course's shape: the road, a loop around the middle of the hills, and its gates.</summary>
public static class Course
{
    public const int Pixels = 256;
    public const float Size = 260, Height = 14, RoadWidth = 5;
    public const int Gates = 8;

    // The road's middle line at a share of the way round, an oval with a wobble.
    public static Vector2 Road(float share)
    {
        var a = share * MathF.Tau;
        var wobble = 1 + 0.18f * MathF.Sin(a * 3) + 0.08f * MathF.Cos(a * 5);
        return new Vector2(MathF.Cos(a) * 85 * wobble, MathF.Sin(a) * 60 * wobble);
    }

    // How high the road is at a point of it, rising and falling twice round the loop.
    public static float RoadHeight(Vector2 at) => 0.4f + 0.12f * MathF.Sin(MathF.Atan2(at.Y, at.X) * 2);

    public static (float X, float Z) World(int x, int y) => (x / (float)(Pixels - 1) * Size - Size / 2, y / (float)(Pixels - 1) * Size - Size / 2);

    // How far a point is from the road's middle line, measured to its nearest of 360 points round it.
    public static float DistanceToRoad(Vector2 at)
    {
        var best = float.MaxValue;
        for (int i = 0; i < 360; i++) best = MathF.Min(best, Vector2.DistanceSquared(at, Road(i / 360f)));
        return MathF.Sqrt(best);
    }

    // The point of the road a share of the way round past the point of it nearest a position.
    public static Vector3 Ahead(Vector3 position, float share)
    {
        var (nearest, best) = (0f, float.MaxValue);
        for (int i = 0; i < 720; i++)
        {
            var d = Vector2.DistanceSquared(new Vector2(position.X, position.Z), Road(i / 720f));
            if (d < best) (nearest, best) = (i / 720f, d);
        }
        var at = Road(nearest + share);
        return new Vector3(at.X, RoadHeight(at) * Height, at.Y);
    }

    // Where the car waits for the countdown, far enough back that it is outside the first gate's
    // sensor, which would otherwise be touching it already when the race starts and never start.
    public static Vector3 Start => Gate(0) - Along(0) * (RoadWidth * 1.5f + Car.Half.Z + 4);

    public static Vector3 Gate(int g)
    {
        var at = Road(g / (float)Gates);
        return new Vector3(at.X, RoadHeight(at) * Height, at.Y);
    }

    public static Vector3 Along(int g)
    {
        var (a, b) = (Road(g / (float)Gates - 0.002f), Road(g / (float)Gates + 0.002f));
        var way = Vector3.Normalize(new Vector3(b.X - a.X, 0, b.Y - a.Y));
        return way;
    }
}

public static class RallyCommands
{
    internal static bool Autopilot;
    internal static string Status = "";

    [Command("rally.status", "The screen, the lap, the gate due next, the speed in km/h, where the car is, the lap's time, the best lap and the laps driven")]
    internal static string Report() => Status;

    [Command("rally.autopilot", "Drives the car toward each gate in turn, for a run that plays itself: rally.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "driving itself" : "driven by the player";
    }

    [Command("rally.reset", "Puts the car back upright where it is, a little above the ground")]
    internal static string Reset()
    {
        Car.Current?.Reset(Car.Current.Position + new Vector3(0, 1, 0), Vector3.Transform(-Vector3.UnitZ, Car.Current.Rotation) with { Y = 0 });
        return "reset";
    }
}
