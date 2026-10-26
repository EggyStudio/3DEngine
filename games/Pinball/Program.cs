// Pinball, three balls on a table of bumpers, slingshots and two flippers. The ball is a continuous
// body, swept over each step, since it crosses near a unit a step at speed and the rails are a
// tenth of a unit thick. The flippers turn on hinges driven by their motors, the plunger slides on
// a slider driven by its own, and a nudge too many in a short while tilts the table, the flippers
// dead until the ball drains. Written against the engine's package, as a game outside this
// repository would be.
using System.Numerics;
using Engine;
using static Engine.E3D;

InitWindow(1280, 720, "Pinball");
InitAudioDevice();
SetTargetFPS(60);
SetPhysicsGravity(Table.Down);

const int BallsAGame = 3;
const string BestFile = "pinball-best.txt";
// How fast a bumper and a sling send the ball off, along the way out of them.
const float BumperKick = 16, SlingKick = 11;
// The plunger's pull back, in units a second, and how fast its spring drives it home on release and
// with how much force. The force is held so the plunger speeds up over its travel as a spring does,
// the ball riding it and leaving it at the stop. Driven home at once, from rest to 33 units a
// second in one step, it passed through the ball, since the contacts a step makes are found from
// the velocities it began with.
const float Pull = 2.5f, Launch = 45, LaunchForce = 120;

// -- The table, its rails, its bumpers, the glass over it and the drain under the flippers.

var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var floorAt = new Vector3(0.3f, -0.1f, 0.45f);
var floorSize = new Vector3(6.4f, 0.2f, 13.5f);
SetPhysicsBodyMaterial(CreatePhysicsStaticBox(floorAt, floorSize), 0.02f, 0.1f);
// The glass, unseen, which keeps a ball kicked hard on the table as a real table's glass does.
CreatePhysicsStaticBox(floorAt with { Y = Table.WallHeight + 0.05f }, floorSize with { Y = 0.1f });
var walls = Table.Walls();

var bumperModel = LoadModelFromMesh(GenMeshCylinder(Table.BumperRadius, 0.4f, 32));
var capModel = LoadModelFromMesh(GenMeshCylinder(Table.BumperRadius * 0.75f, 0.06f, 32));
var bumpers = Table.Bumpers.Select(b => CreatePhysicsStaticModel(bumperModel, new Vector3(b.X, 0, b.Y))).ToArray();
foreach (var bumper in bumpers) SetPhysicsBodyMaterial(bumper, 0.05f, 0.6f);
var bumperLights = Table.Bumpers.Select(b => CreatePointLight(new Vector3(b.X, 0.9f, b.Y), new Color(255, 170, 90), 0, range: 3)).ToArray();
var glow = new float[bumpers.Length];

var drain = CreatePhysicsTrigger(new Vector3(0, 0.25f, 6.2f), new Vector3(Table.Edge * 2, 0.5f, 0.9f));

// -- The flippers on their hinges, the plunger on its slider, and the ball.

var flippers = new[] { new Flipper(true), new Flipper(false) };

var plungerAnchor = CreatePhysicsKinematicBox(new Vector3(Table.LaneX, -0.6f, 6.4f), new Vector3(0.1f, 0.1f, 0.1f));
// The plunger is swept over each step as the ball is, since driven home it moves 0.75 of a unit a
// step, and unswept it passed through the ball it was to strike.
var plungerSize = new Vector3(0.36f, 0.3f, 0.6f);
var plunger = CreatePhysicsBox(new Vector3(Table.LaneX, 0.17f, 5.77f), plungerSize, mass: 0.3f);
SetPhysicsBodyContinuous(plunger, true);
var slider = CreatePhysicsSliderJoint(plungerAnchor, plunger, Vector3.UnitZ);
SetPhysicsSliderLimits(slider, 0, 1.0f);
SetPhysicsSliderMotor(slider, -5, 40);

var ball = CreatePhysicsSphere(Table.BallStart, Table.BallRadius, Table.BallMass);
SetPhysicsBodyContinuous(ball, true);
SetPhysicsBodyMaterial(ball, 0.05f, 0.35f);
var ballModel = LoadModelFromMesh(GenMeshSphere(Table.BallRadius, 24, 32));
ballModel.Materials[0] = ballModel.Materials[0] with { Metallic = 0.5f, Roughness = 0.2f };

// -- Light, sound and the camera.

CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.3f, -1, -0.4f)), new Color(255, 245, 230), 2.2f, castsShadows: true);
SetAmbientLight(new Color(120, 140, 200), 0.4f);
SetShadowDistance(20);
var camera = new Camera3D(new Vector3(0.3f, 10.6f, 9.4f), new Vector3(0.3f, 0, -0.2f), Vector3.UnitY, 52);

Sound Load(string name, float volume)
{
    var sound = LoadSound($"resources/sounds/{name}.wav");
    SetSoundVolume(sound, volume);
    return sound;
}
var (flipSound, bumperSound, slingSound, launchSound, drainSound, tiltSound, startSound) =
    (Load("flip", 0.5f), Load("bumper", 0.6f), Load("sling", 0.5f), Load("launch", 0.6f), Load("drain", 0.6f), Load("tilt", 0.7f), Load("start", 0.6f));

// -- The game.

AddState(Screen.Title);
var best = int.TryParse(FileExists(BestFile) ? LoadFileText(BestFile) : null, out var saved) ? saved : 0;
int score = 0, ballNumber = 0, launches = 0, flips = 0, hits = 0, drains = 0;
var launched = false;
var tilted = false;
var tiltMeter = 0f;
var pulling = false;
var pilotPull = 0f;
var pilotHold = new[] { -1f, -1f };

void Serve()
{
    SetPhysicsBodyPosition(ball, Table.BallStart);
    SetPhysicsBodyVelocity(ball, Vector3.Zero);
    SetPhysicsBodyAngularVelocity(ball, Vector3.Zero);
    (launched, tilted, tiltMeter) = (false, false, 0);
}

while (!WindowShouldClose())
{
    var dt = GetFrameTime();
    var screen = GetState<Screen>();
    var at = GetPhysicsBodyPosition(ball);
    var velocity = GetPhysicsBodyVelocity(ball);

    if (screen != Screen.Play && (IsKeyPressed(Key.Enter) || IsGamepadButtonPressed(0, GamepadButton.MiddleRight)))
    {
        (score, ballNumber, launches, flips, hits, drains) = (0, 1, 0, 0, 0, 0);
        Serve();
        PlaySound(startSound);
        SetState(Screen.Play);
        screen = Screen.Play;
    }

    // -- The flippers and the plunger, from the keys or a pad, or the autopilot.
    var press = new bool[2];
    var pull = false;
    if (screen == Screen.Play)
    {
        press[0] = IsKeyDown(Key.LeftShift) || IsKeyDown(Key.Z) || IsKeyDown(Key.Left) || IsGamepadButtonDown(0, GamepadButton.LeftTrigger1);
        press[1] = IsKeyDown(Key.RightShift) || IsKeyDown(Key.Slash) || IsKeyDown(Key.Right) || IsGamepadButtonDown(0, GamepadButton.RightTrigger1);
        pull = IsKeyDown(Key.Space) || IsKeyDown(Key.Down) || IsGamepadButtonDown(0, GamepadButton.RightFaceDown);
        if (PinballCommands.Autopilot)
        {
            // In the lane, the plunger is pulled most of the way and let go; on the table, a
            // flipper is raised as the ball comes down within its reach and held a moment, and not
            // raised again for a while after, so a ball cradled on a raised flipper rolls down it
            // and is struck again rather than held there for good.
            if (!launched && at.X > Table.Edge && MathF.Abs(velocity.Z) < 0.5f) pilotPull += dt;
            pull |= pilotPull is > 0 and < 0.35f;
            if (pilotPull > 0.6f) pilotPull = 0;
            for (int i = 0; i < 2; i++)
            {
                var pivot = flippers[i].Pivot;
                var near = at.Z > pivot.Y - 0.7f && at.Z < pivot.Y + 0.5f && (i == 0 ? at.X < 0.15f && at.X > pivot.X - 0.3f : at.X > -0.15f && at.X < pivot.X + 0.3f);
                if (near && velocity.Z > -1 && pilotHold[i] < -0.4f) pilotHold[i] = 0.2f;
                pilotHold[i] -= dt;
                press[i] |= pilotHold[i] > 0;
            }
        }
        if (IsKeyPressed(Key.T) || IsGamepadButtonPressed(0, GamepadButton.RightFaceLeft)) Nudge();
    }
    for (int i = 0; i < 2; i++)
    {
        var up = press[i] && !tilted;
        if (up && !flippers[i].Up) PlaySound(flipSound);
        flippers[i].Drive(up);
    }
    if (pull) SetPhysicsSliderMotor(slider, Pull, 40);
    else if (pulling)
    {
        SetPhysicsSliderMotor(slider, -Launch, LaunchForce);
        SetSoundVolume(launchSound, Math.Clamp(GetPhysicsSliderPosition(slider), 0.2f, 1));
        PlaySound(launchSound);
    }
    else if (GetPhysicsSliderPosition(slider) < 0.02f) SetPhysicsSliderMotor(slider, -5, 40);
    pulling = pull;
    tiltMeter = MathF.Max(0, tiltMeter - 0.6f * dt);

    // -- What the ball met this frame.
    // A bumper or a sling kicks it, a raised flipper's touch is counted, and the drain ends it.
    foreach (var contact in GetPhysicsContacts())
    {
        if (contact.BodyA != ball && contact.BodyB != ball) continue;
        var other = contact.BodyA == ball ? contact.BodyB : contact.BodyA;
        var bumper = Array.IndexOf(bumpers, other);
        if (bumper >= 0)
        {
            var away = Flat(at - new Vector3(Table.Bumpers[bumper].X, 0, Table.Bumpers[bumper].Y));
            SetPhysicsBodyVelocity(ball, Vector3.Normalize(away) * BumperKick);
            glow[bumper] = 1;
            Score(100);
            PlaySound(bumperSound);
            continue;
        }
        var wall = walls.FindIndex(w => w.Body == other);
        if (wall >= 0 && walls[wall].Kind == Table.Kind.Sling)
        {
            // The sling's face toward the ball, its normal the long side's across turned to the
            // ball.
            var normal = Vector3.Transform(Vector3.UnitZ, walls[wall].Turn);
            if (Vector3.Dot(normal, at - walls[wall].Center) < 0) normal = -normal;
            SetPhysicsBodyVelocity(ball, Flat(velocity) - 2 * Vector3.Dot(Flat(velocity), normal) * normal * 0.5f + normal * SlingKick);
            Score(10);
            PlaySound(slingSound);
            continue;
        }
        if (flippers.Any(f => f.Body == other && f.Up)) flips++;
        if (other == drain && screen == Screen.Play) Drained();
    }
    // A ball that leaves the table some other way is drained as well.
    if (screen == Screen.Play && (at.Y < -2 || at.Z > 7.5f)) Drained();
    // A ball out of the lane is launched, and one back down the lane at its plunger is ready again.
    if (!launched && at.X < Table.Edge - 0.1f) (launched, launches) = (true, launches + 1);
    if (launched && at.X > Table.Edge + 0.05f && at.Z > 4.5f) launched = false;

    for (int i = 0; i < bumpers.Length; i++)
    {
        glow[i] = MathF.Max(0, glow[i] - 4 * dt);
        SetLightColor(bumperLights[i], new Color(255, 170, 90), glow[i] * 6);
    }

    PinballCommands.Status = $"{screen} ball {ballNumber}/{BallsAGame} score {score} best {best} at {at.X:0.00},{at.Y:0.00},{at.Z:0.00} speed {velocity.Length():0.0} " +
                             $"launches {launches} flips {flips} hits {hits} drains {drains} lane {(launched ? "no" : "yes")} tilt {(tilted ? "yes" : "no")} " +
                             $"plunger {GetPhysicsSliderPosition(slider):0.00} flippers {flippers[0].Raised():0},{flippers[1].Raised():0}";

    // -- The frame.
    BeginDrawing();
    ClearBackground(new Color(12, 14, 24));
    BeginMode3D(camera);
    DrawBox(floorAt, floorSize, Quaternion.Identity, new Color(28, 40, 86));
    foreach (var w in walls) DrawBox(w.Center, w.Size, w.Turn, w.Color);
    for (int i = 0; i < bumpers.Length; i++)
    {
        var b = new Vector3(Table.Bumpers[i].X, 0, Table.Bumpers[i].Y);
        DrawModel(bumperModel, b, 1, new Color(200, 40, 50));
        capModel.Materials[0] = capModel.Materials[0] with { Emissive = new Color(255, 200, 120), EmissiveIntensity = glow[i] * 3 };
        DrawModel(capModel, b + new Vector3(0, 0.4f, 0), 1, Color.White);
    }
    foreach (var f in flippers) DrawBody(f.Body, new Vector3(Table.FlipperLength, 0.25f, Table.FlipperWidth), new Color(240, 240, 245));
    DrawBody(plunger, plungerSize, new Color(180, 185, 195));
    ballModel.Transform = GetPhysicsBodyTransform(ball);
    DrawModel(ballModel, Vector3.Zero, 1, Color.White);
    EndMode3D();
    DrawText($"{score}", 30, 24, 48, Color.White);
    DrawText($"Ball {Math.Max(ballNumber, 1)} of {BallsAGame}   Best {best}", 30, 78, 20, Color.LightGray);
    if (tilted) DrawText("TILT", GetScreenWidth() / 2 - MeasureText("TILT", 80) / 2, 300, 80, Color.Red);
    if (screen == Screen.Title) DrawText("Press Enter to play", GetScreenWidth() / 2 - MeasureText("Press Enter to play", 36) / 2, 320, 36, Color.White);
    if (screen == Screen.Over) DrawText($"Game over, {score}. Enter to play again", GetScreenWidth() / 2 - MeasureText($"Game over, {score}. Enter to play again", 30) / 2, 320, 30, Color.White);
    DrawText("Shift or the arrows flip, Space pulls the plunger, T nudges", 30, 112, 18, Color.Gray);
    EndDrawing();
}

UnloadModel(cube);
UnloadModel(bumperModel);
UnloadModel(capModel);
UnloadModel(ballModel);
CloseAudioDevice();
CloseWindow();

void Score(int points)
{
    if (tilted) return;
    score += points;
    hits++;
}

// A nudge sends the ball up the table a little, and three close together tilt it.
void Nudge()
{
    var side = (Random.Shared.NextSingle() - 0.5f) * 1.5f;
    ApplyPhysicsImpulse(ball, new Vector3(side, 0, -4) * Table.BallMass);
    tiltMeter += 1;
    if (tiltMeter > 2.5f && !tilted)
    {
        tilted = true;
        PlaySound(tiltSound);
    }
}

void Drained()
{
    drains++;
    PlaySound(drainSound);
    if (ballNumber >= BallsAGame)
    {
        if (score > best)
        {
            best = score;
            SaveFileText(BestFile, best.ToString());
        }
        SetState(Screen.Over);
        Serve();
        return;
    }
    ballNumber++;
    Serve();
}

void DrawBox(Vector3 center, Vector3 size, Quaternion turn, Color color)
{
    cube.Transform = Matrix4x4.CreateScale(size) * Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation(center);
    DrawModel(cube, Vector3.Zero, 1, color);
}

void DrawBody(PhysicsBody body, Vector3 size, Color color)
{
    cube.Transform = Matrix4x4.CreateScale(size) * GetPhysicsBodyTransform(body);
    DrawModel(cube, Vector3.Zero, 1, color);
}

static Vector3 Flat(Vector3 v) => v with { Y = 0 };

public enum Screen { Title, Play, Over }

/// <summary>
/// A flipper, a box on a hinge to a kinematic post under the table, driven up by the hinge's motor
/// while its button is held and back down when it is let go, the hinge's limits stopping it at rest
/// and at the top.
/// </summary>
public sealed class Flipper
{
    public readonly bool Left;
    public readonly Vector2 Pivot;
    public readonly PhysicsBody Body;
    private readonly PhysicsJoint _hinge;
    private readonly Vector3 _rest;
    public bool Up { get; private set; } = true;

    public Flipper(bool left)
    {
        Left = left;
        Pivot = left ? Table.LeftPivot : Table.RightPivot;
        var pivot = new Vector3(Pivot.X, 0.155f, Pivot.Y);
        _rest = Table.Along(left, Table.RestDegrees);
        // The post stands under the table, so the ball and the flipper never touch it.
        var post = CreatePhysicsKinematicBox(pivot with { Y = -0.6f }, new Vector3(0.1f, 0.1f, 0.1f));
        Body = CreatePhysicsBox(pivot + _rest * (Table.FlipperLength / 2 - 0.1f), new Vector3(Table.FlipperLength, 0.25f, Table.FlipperWidth), mass: 0.4f);
        SetPhysicsBodyRotation(Body, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(-_rest.Z, _rest.X)));
        SetPhysicsBodyMaterial(Body, 0.3f, 0.2f);
        _hinge = CreatePhysicsHingeJoint(post, Body, pivot, Vector3.UnitY);
        // Raised, the left flipper turns about +y by a positive angle and the right by a negative
        // one.
        if (left) SetPhysicsHingeLimits(_hinge, 0, Table.UpDegrees);
        else SetPhysicsHingeLimits(_hinge, -Table.UpDegrees, 0);
        Drive(false);
    }

    /// <summary>Drives the flipper up fast or back down a little slower.</summary>
    public void Drive(bool up)
    {
        if (up == Up) return;
        Up = up;
        var speed = up ? 1500f : -900f;
        SetPhysicsHingeMotor(_hinge, Left ? speed : -speed, 60);
    }

    /// <summary>How far above its rest the flipper stands, in degrees.</summary>
    public float Raised()
    {
        var along = Vector3.Transform(Vector3.UnitX, GetPhysicsBodyRotation(Body));
        var turned = MathF.Atan2(Vector3.Cross(_rest, along).Y, Vector3.Dot(_rest, along)) * 180 / MathF.PI;
        return Left ? turned : -turned;
    }
}

public static class PinballCommands
{
    internal static bool Autopilot;
    internal static string Status = "";

    [Command("pinball.status", "The screen, the ball, the score and the best, where the ball is and how fast, the launches, the flips that met it, the hits, the drains, whether the ball is in its lane at the plunger, the tilt, the plunger's pull and each flipper's angle")]
    internal static string Report() => Status;

    [Command("pinball.autopilot", "Launches each ball and flips as it comes down to a flipper, for a game that plays itself: pinball.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "the table plays itself" : "the player plays";
    }
}
