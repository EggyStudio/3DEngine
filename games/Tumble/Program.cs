// Tumble, a robot walked round an arena for the stars that appear in it, while cannons at its
// edges fire at it and a bar sweeps round its middle. The robot is a character controller drawn as
// a skinned model, walking and running by blending its clips by its speed, its upper body aimed at
// the pointer on a layer of its own, punching on that layer, and crouching by the character's
// height under the bar. Struck, it falls as a ragdoll of capsules on ball joints held in cones and
// hinges at its knees and elbows, and once it lies still it stands up by a blend from the pose it
// lies in. Written against the engine's package, as a game outside this repository would be.
using System.Numerics;
using Engine;
using static Engine.E3D;

InitWindow(1280, 720, "Tumble");
InitAudioDevice();
SetTargetFPS(60);
// The ragdoll's parts pass through one another.
SetPhysicsLayersCollide(Robot.Layer, Robot.Layer, false);

const float Arena = 12, BallRadius = 0.3f, BallMass = 6, BallSpeed = 12;
// How hard a ball meets the robot before it falls, in meters a second.
const float Knocks = 3;

// -- The arena, its cannons, the bar and the robot.

var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var ballModel = LoadModelFromMesh(GenMeshSphere(BallRadius, 16, 24));
CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(Arena * 2 + 2, 1, Arena * 2 + 2));
foreach (var side in new[] { -1, 1 })
{
    CreatePhysicsStaticBox(new Vector3(side * (Arena + 0.5f), 0.5f, 0), new Vector3(1, 1, Arena * 2 + 2));
    CreatePhysicsStaticBox(new Vector3(0, 0.5f, side * (Arena + 0.5f)), new Vector3(Arena * 2 + 2, 1, 1));
}
CreatePhysicsStaticBox(new Vector3(0, 1, 0), new Vector3(0.6f, 2, 0.6f));
// The bar, kinematic, turned by its own speed, at a height a standing robot meets with its chest
// and a crouched one passes under.
const float BarHeight = 1.65f, BarLength = 11, BarTurn = 0.7f;
var bar = CreatePhysicsKinematicBox(new Vector3(0, BarHeight, 0), new Vector3(BarLength, 0.3f, 0.3f));
Vector3[] cannons = [new(-Arena, 1.2f, 0), new(Arena, 1.2f, 0), new(0, 1.2f, -Arena), new(0, 1.2f, Arena)];

var model = LoadModel("resources/robot.glb");
var clips = LoadModelAnimations("resources/robot.glb");
var robot = new Robot(model, clips, new Vector3(0, 0, 8)) { Yaw = MathF.PI };

SetAmbientLight(new Color(150, 170, 210), 0.45f);
CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), new Color(255, 245, 230), 2.2f, castsShadows: true);
var camera = new Camera3D(new Vector3(0, 5.5f, 16), new Vector3(0, 1, 8), Vector3.UnitY, 50);

Sound Load(string name, float volume)
{
    var sound = LoadSound($"resources/sounds/{name}.wav");
    SetSoundVolume(sound, volume);
    return sound;
}
var (fireSound, hitSound, upSound, punchSound, starSound) =
    (Load("fire", 0.5f), Load("hit", 0.7f), Load("up", 0.5f), Load("punch", 0.5f), Load("star", 0.6f));

// -- The game.

var balls = new List<(PhysicsBody Body, float Age)>();
var punched = new HashSet<PhysicsBody>();
var random = new Random(7);
var nextShot = 3f;
var stars = 0;
var punches = 0;
var walked = 0f;
var star = PlaceStar();
var wasDown = false;

PhysicsBody PlaceStar()
{
    var angle = random.NextSingle() * MathF.Tau;
    var distance = 3 + random.NextSingle() * (Arena - 4);
    return CreatePhysicsTrigger(new Vector3(MathF.Cos(angle) * distance, 0.6f, MathF.Sin(angle) * distance), new Vector3(0.8f, 1.2f, 0.8f));
}

// Fires a ball from a point at the robot's chest, aimed above it by as much as it falls on the way.
void Fire(Vector3 from)
{
    var to = robot.Chest - from;
    var time = to.Length() / BallSpeed;
    var velocity = Vector3.Normalize(to) * BallSpeed + new Vector3(0, 9.81f * time / 2, 0);
    var ball = CreatePhysicsSphere(from, BallRadius, BallMass);
    SetPhysicsBodyContinuous(ball, true);
    SetPhysicsBodyVelocity(ball, velocity);
    balls.Add((ball, 0));
    PlaySound(fireSound);
}

// The cannons and the bar, or the bar alone.
void Hazards(bool cannonsOn, bool barOn)
{
    (TumbleCommands.Cannons, TumbleCommands.Bar) = (cannonsOn, barOn);
    // Stopped, the bar is lifted over the arena, out of the robot's way.
    SetPhysicsBodyPosition(bar, new Vector3(0, barOn ? BarHeight : 6, 0));
    SetPhysicsBodyAngularVelocity(bar, new Vector3(0, barOn ? BarTurn : 0, 0));
}
Hazards(true, true);

while (!WindowShouldClose())
{
    var dt = GetFrameTime();

    switch (TumbleCommands.Asked)
    {
        // From five meters to the robot's side, toward the open half of the arena.
        case "strike": Fire(robot.Chest + new Vector3(robot.Position.X < 0 ? 5 : -5, 0, 0)); break;
        case "on": Hazards(true, true); break;
        case "bar": Hazards(false, true); break;
        case "off": Hazards(false, false); break;
    }
    TumbleCommands.Asked = null;

    // -- The robot, walked relative to the camera, which looks along -z, run with Shift, crouched
    // with C, aimed at the pointer and punching with the left button.
    var way = Vector3.Zero;
    if (IsKeyDown(Key.W)) way.Z -= 1;
    if (IsKeyDown(Key.S)) way.Z += 1;
    if (IsKeyDown(Key.A)) way.X -= 1;
    if (IsKeyDown(Key.D)) way.X += 1;
    if (way != Vector3.Zero) way = Vector3.Normalize(way) * (IsKeyDown(Key.LeftShift) ? Robot.RunSpeed : Robot.WalkSpeed);
    var ray = GetScreenToWorldRay(GetMousePosition(), camera);
    var aimAt = MathF.Abs(ray.Direction.Y) > 1e-3f ? ray.Position + ray.Direction * ((1.2f - ray.Position.Y) / ray.Direction.Y) : robot.Position;
    if (IsMouseButtonPressed(MouseButton.Left) && robot.Punch())
    {
        punches++;
        punched.Clear();
        PlaySound(punchSound);
    }
    var before = robot.Position;
    robot.Move(way, IsKeyDown(Key.C), aimAt, dt);
    // Only what the keys walked, and not where the bar or a ball carried it.
    if (robot.State == Robot.Phase.Up && way != Vector3.Zero) walked += Vector3.Distance(before, robot.Position);
    if (wasDown && robot.State == Robot.Phase.Rising) PlaySound(upSound);
    wasDown = robot.State == Robot.Phase.Down;

    // A punch at the part of its swing that strikes sends a ball near the fist away.
    if (robot.Striking)
        foreach (var (ball, _) in balls)
        {
            var at = GetPhysicsBodyPosition(ball);
            if (punched.Contains(ball) || Vector3.Distance(at, robot.Fist) > 0.9f) continue;
            var away = Vector3.Normalize(at - robot.Position with { Y = at.Y - 0.3f });
            ApplyPhysicsImpulse(ball, away * BallMass * 14);
            punched.Add(ball);
        }

    // -- The cannons, firing in turn while the hazards are on, and the balls let go after a while.
    nextShot -= dt;
    if (TumbleCommands.Cannons && nextShot <= 0 && robot.State == Robot.Phase.Up)
    {
        Fire(cannons[random.Next(cannons.Length)]);
        nextShot = 2.5f + random.NextSingle() * 2.5f;
    }
    for (int i = balls.Count - 1; i >= 0; i--)
    {
        var (ball, age) = balls[i];
        if (age + dt > 8)
        {
            DestroyPhysicsBody(ball);
            balls.RemoveAt(i);
        }
        else balls[i] = (ball, age + dt);
    }

    // -- What meets the robot, a ball fast enough or the bar knocking it down, and a star taken.
    foreach (var contact in GetPhysicsContacts())
    {
        if (robot.State != Robot.Phase.Up || contact.BodyA != robot.Body && contact.BodyB != robot.Body) continue;
        var other = contact.BodyA == robot.Body ? contact.BodyB : contact.BodyA;
        if (other == star)
        {
            stars++;
            PlaySound(starSound);
            DestroyPhysicsBody(star);
            star = PlaceStar();
            continue;
        }
        if (balls.Any(b => b.Body == other) && contact.Speed > Knocks)
        {
            robot.Fall(contact.Point, GetPhysicsBodyVelocity(other) * BallMass * 1.5f);
            PlaySound(hitSound);
        }
        else if (other == bar)
        {
            var push = Vector3.Cross(new Vector3(0, BarTurn, 0), contact.Point with { Y = 0 });
            robot.Fall(contact.Point, push * 40);
            PlaySound(hitSound);
        }
    }

    // The bar knocks the robot down where it goes on pushing it, as where it stood up in the bar's
    // way, which no contact starting says.
    if (robot.State == Robot.Phase.Up && GetPhysicsContactImpulse(robot.Body, bar) > 0)
    {
        var at = robot.Chest;
        robot.Fall(at, Vector3.Cross(new Vector3(0, BarTurn, 0), at with { Y = 0 }) * 40);
        PlaySound(hitSound);
    }

    TumbleCommands.Status = $"{robot.State} falls {robot.Falls} rises {robot.Rises} walked {walked:0.0} at {robot.Position.X:0.00},{robot.Position.Z:0.00} " +
                            $"crouched {robot.Crouched} stars {stars} punches {punches} cannons {(TumbleCommands.Cannons ? "on" : "off")} bar {(TumbleCommands.Bar ? "on" : "off")}";

    // -- The camera, following the robot or where its ragdoll lies.
    var follow = robot.State == Robot.Phase.Down ? robot.Chest with { Y = 1 } : robot.Position + new Vector3(0, 1, 0);
    camera.Target = Vector3.Lerp(camera.Target, follow, MathF.Min(1, dt * 4));
    camera.Position = camera.Target + new Vector3(0, 4.5f, 8);

    BeginDrawing();
    ClearBackground(new Color(150, 185, 225));
    BeginMode3D(camera);
    DrawBox(new Vector3(0, -0.5f, 0), new Vector3(Arena * 2 + 2, 1, Arena * 2 + 2), Quaternion.Identity, new Color(110, 150, 90));
    foreach (var side in new[] { -1, 1 })
    {
        DrawBox(new Vector3(side * (Arena + 0.5f), 0.5f, 0), new Vector3(1, 1, Arena * 2 + 2), Quaternion.Identity, new Color(150, 140, 125));
        DrawBox(new Vector3(0, 0.5f, side * (Arena + 0.5f)), new Vector3(Arena * 2 + 2, 1, 1), Quaternion.Identity, new Color(150, 140, 125));
    }
    DrawBox(new Vector3(0, 1, 0), new Vector3(0.6f, 2, 0.6f), Quaternion.Identity, new Color(120, 100, 80));
    cube.Transform = Matrix4x4.CreateScale(BarLength, 0.3f, 0.3f) * GetPhysicsBodyTransform(bar);
    DrawModel(cube, Vector3.Zero, 1, new Color(200, 70, 50));
    foreach (var cannon in cannons) DrawBox(cannon, new Vector3(0.8f), Quaternion.Identity, new Color(50, 50, 60));
    foreach (var (ball, _) in balls)
    {
        ballModel.Transform = GetPhysicsBodyTransform(ball);
        DrawModel(ballModel, Vector3.Zero, 1, new Color(40, 40, 45));
    }
    var starAt = GetPhysicsBodyPosition(star);
    DrawBox(starAt + new Vector3(0, 0.2f * MathF.Sin((float)GetTime() * 3), 0), new Vector3(0.4f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)GetTime() * 2), new Color(250, 210, 60));
    robot.Draw();
    if (robot.State == Robot.Phase.Up) DrawSphere(aimAt with { Y = 0.05f }, 0.12f, new Color(255, 255, 255, 160));
    EndMode3D();
    DrawText("WASD walk, Shift run, C crouch under the bar, the pointer aims, the left button punches", 20, 20, 20, Color.Black);
    DrawText($"Stars {stars}   Falls {robot.Falls}", 20, 48, 20, new Color(30, 30, 40));
    EndDrawing();
}

UnloadModel(cube);
UnloadModel(ballModel);
UnloadModel(model);
UnloadModelAnimations(clips);
CloseAudioDevice();
CloseWindow();

void DrawBox(Vector3 center, Vector3 size, Quaternion turn, Color color)
{
    cube.Transform = Matrix4x4.CreateScale(size) * Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation(center);
    DrawModel(cube, Vector3.Zero, 1, color);
}

public static class TumbleCommands
{
    internal static string Status = "";
    internal static string? Asked;
    internal static bool Cannons, Bar;

    [Command("tumble.status", "Whether the robot is up, down or rising, its falls and rises, how far it walked, where it is, whether it crouches, the stars it took, its punches and whether the cannons and the bar are on")]
    internal static string Report() => Status;

    [Command("tumble.strike", "Fires a ball at the robot from five meters to its side")]
    internal static string Strike()
    {
        Asked = "strike";
        return "a ball is fired at the robot";
    }

    [Command("tumble.hazards", "Starts the cannons and the bar, the bar alone, or stops both: tumble.hazards <on|bar|off>")]
    internal static string Switch(string state)
    {
        if (state is not ("on" or "bar" or "off")) return $"the hazards are on, bar or off, not {state}";
        Asked = state;
        return state == "bar" ? "the bar alone turns" : $"the hazards are {state}";
    }
}
