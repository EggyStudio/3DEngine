// Siege, a catapult thrown at a castle until its banner falls. The arm turns on a hinge driven by
// its motor and lets its shot go at the angle the player chose, a stone or a bomb. The castle is
// blocks, boxes and columns shaped as convex hulls, every one welded to those it was laid against,
// and a weld breaks where it holds harder than its strength, read each frame. A stone wears down
// the blocks it presses by the push of their contact, a bomb bursts on what it meets and pushes
// every body near it from the side facing the burst, and the banner falling to the field enters a
// trigger, which ends the siege. Written against the engine's package, as a game outside this
// repository would be.
using System.Numerics;
using Engine;
using static Engine.E3D;

InitWindow(1280, 720, "Siege");
InitAudioDevice();
SetTargetFPS(60);

// How far a bomb's burst reaches, and the push it gives a body at its middle, falling to none at
// its edge, in kilogram meters a second.
const float BlastRadius = 3.5f, BlastPush = 700;
// The thrown stones kept lying about, the oldest taken away past this many.
const int StonesKept = 6;

// -- The field, the castle, the catapult and the lights.

var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var column = LoadModelFromMesh(GenMeshCylinder(0.5f, 1, 10));
var roof = LoadModelFromMesh(GenMeshCone(2.1f, Castle.RoofHeight, 12));
var stone = LoadModelFromMesh(GenMeshSphere(0.45f, 6, 8));
CreatePhysicsStaticBox(new Vector3(15, -0.5f, 0), new Vector3(140, 1, 90));
var castle = new Castle(new Castle.Models(cube, column, roof));
var catapult = new Catapult(stone);
// The field round the castle, a trigger the banner enters when it falls to it.
var fallen = CreatePhysicsKinematicBox(Castle.Keep with { Y = 0.75f }, new Vector3(30, 1.5f, 30));
SetPhysicsBodyTrigger(fallen, true);

SetAmbientLight(new Color(110, 130, 175), 0.35f);
CreateDirectionalLight(Vector3.Normalize(new Vector3(0.45f, -0.6f, -0.65f)), new Color(255, 190, 140), 1.4f, castsShadows: true);
// A light on the keep from the field before it, sweeping slowly across it, casting the keep's
// shadow on the walls behind.
var spotAt = new Vector3(20, 11, 10);
var spot = CreateSpotLight(spotAt, Vector3.Normalize(Castle.Keep + new Vector3(0, 7, 0) - spotAt), new Color(255, 225, 180), 120, 12, 18, 45, castsShadows: true);
var flash = CreatePointLight(Vector3.Zero, new Color(255, 170, 80), 0, 14);
var flashLeft = 0f;
SetShadowMapSize(4096);
SetTonemap(Tonemap.AgX);
// From the field's side, the catapult on the left and the castle on the right.
var camera = new Camera3D(new Vector3(13, 7, 33), new Vector3(15, 5, 0), Vector3.UnitY, 42);
// The siege in focus, the field near and far blurred.
SetDepthOfField(Vector3.Distance(camera.Position, camera.Target), 14, 0.012f);
SiegeCommands.Camera = camera;

Sound Load(string name, float volume)
{
    var sound = LoadSound($"resources/sounds/{name}.wav");
    SetSoundVolume(sound, volume);
    return sound;
}
var (throwSound, hitSound, crackSound, boomSound, loadSound, winSound) =
    (Load("throw", 0.6f), Load("hit", 0.6f), Load("crack", 0.5f), Load("boom", 0.8f), Load("load", 0.5f), Load("win", 0.6f));

// -- The game.

AddState(Screen.Aim);
var power = 260f;
var elevation = 40f;
var ammo = Catapult.Ammo.Stone;
var shots = 0;
var flightTime = 0f;
PhysicsBody? flying = null;
var flyingKind = Catapult.Ammo.Stone;
var thrown = new List<PhysicsBody>();
var touching = new HashSet<Castle.Block>();
// How fast the last shot left against how fast the arc foresaw, which the next arc is drawn by.
var speedRatio = 1f;
var lastSpeed = 0f;
var mostForce = 0f;
catapult.Load(ammo);

void Throw()
{
    if (GetState<Screen>() != Screen.Aim || !catapult.IsLoaded) return;
    catapult.Swing(power);
    SetState(Screen.Swing);
}

void Reset()
{
    // Not while the arm swings, whose shot is still held and let go by the swing.
    if (GetState<Screen>() == Screen.Swing) return;
    castle.Rebuild();
    foreach (var body in thrown.Where(IsPhysicsBodyValid)) DestroyPhysicsBody(body);
    thrown.Clear();
    touching.Clear();
    if (flying is { } f && IsPhysicsBodyValid(f)) DestroyPhysicsBody(f);
    flying = null;
    (shots, mostForce) = (0, 0);
    SetState(catapult.IsLoaded ? Screen.Aim : Screen.Flight);
}

// Pushes every block and stone near a burst away from it, at the side facing it, and wears the
// blocks down by that push.
void Blast(Vector3 center)
{
    foreach (var body in GetPhysicsBodiesInSphere(center, BlastRadius))
    {
        var block = castle.Of(body);
        if (block is null && !thrown.Contains(body)) continue;
        // A column's position is its foot, where the hull's model stands.
        var at = GetPhysicsBodyPosition(body) + (block?.Shape == Castle.Shape.Column ? new Vector3(0, 0.5f, 0) : Vector3.Zero);
        var away = at - center;
        var distance = away.Length();
        var way = distance > 0.01f ? away / distance : Vector3.UnitY;
        var push = BlastPush * (1 - distance / BlastRadius);
        if (push <= 0) continue;
        ApplyPhysicsImpulseAt(body, way * push, at - way * 0.4f);
        if (block is null) continue;
        block.Damage += push;
        if (block.Damage > Castle.Toughness) castle.Crumble(block);
    }
    SetLightPosition(flash, center + new Vector3(0, 1, 0));
    flashLeft = 0.5f;
    PlaySound(boomSound);
}

while (!WindowShouldClose())
{
    var dt = GetFrameTime();
    var screen = GetState<Screen>();

    // -- Aiming, by the keys or by the commands.
    switch (SiegeCommands.Asked)
    {
        case "reset": Reset(); break;
        case { } asked when asked.StartsWith("throw ", StringComparison.Ordinal) && screen == Screen.Aim:
            var parts = asked.Split(' ');
            (power, elevation) = (float.Parse(parts[1]), float.Parse(parts[2]));
            var kind = parts[3] == "bomb" ? Catapult.Ammo.Bomb : Catapult.Ammo.Stone;
            if (kind != catapult.Loaded)
            {
                catapult.Unload();
                catapult.Load(ammo = kind);
            }
            Throw();
            break;
    }
    SiegeCommands.Asked = null;
    screen = GetState<Screen>();
    if (IsKeyPressed(Key.R)) Reset();
    if (screen == Screen.Aim)
    {
        if (IsKeyDown(Key.Right)) power = MathF.Min(420, power + 120 * dt);
        if (IsKeyDown(Key.Left)) power = MathF.Max(120, power - 120 * dt);
        if (IsKeyDown(Key.Up)) elevation = MathF.Min(60, elevation + 20 * dt);
        if (IsKeyDown(Key.Down)) elevation = MathF.Max(15, elevation - 20 * dt);
        if (IsKeyPressed(Key.Tab))
        {
            ammo = ammo == Catapult.Ammo.Stone ? Catapult.Ammo.Bomb : Catapult.Ammo.Stone;
            catapult.Unload();
            catapult.Load(ammo);
            PlaySound(loadSound);
        }
        if (IsKeyPressed(Key.Space)) Throw();
    }

    // -- The swing, the shot let go once the arm points where it leaves from. It is let go in the
    // frame the arm passes that angle, so a frame of several steps, on a device that draws slowly,
    // lets it go late and high, and the scripted runs set each frame to a step.
    if (screen == Screen.Swing && catapult.Angle <= 90 + elevation)
    {
        var shot = catapult.Release();
        (flying, flyingKind, flightTime) = (shot, catapult.Loaded, 0);
        lastSpeed = GetPhysicsBodyVelocity(shot).Length();
        speedRatio = lastSpeed / Foreseen(power, elevation).Length();
        shots++;
        if (flyingKind == Catapult.Ammo.Stone) thrown.Add(shot);
        if (thrown.Count > StonesKept)
        {
            DestroyPhysicsBody(thrown[0]);
            thrown.RemoveAt(0);
        }
        PlaySound(throwSound);
        SetState(Screen.Flight);
    }

    // -- What the shot meets, a stone wearing the blocks it presses down, a bomb bursting.
    foreach (var contact in GetPhysicsContacts())
    {
        if (contact.BodyA == castle.Banner.Body && contact.BodyB == fallen || contact.BodyB == castle.Banner.Body && contact.BodyA == fallen)
        {
            if (GetState<Screen>() != Screen.Won) PlaySound(winSound);
            SetState(Screen.Won);
        }
        if (flying is not { } f || contact.BodyA != f && contact.BodyB != f) continue;
        var other = contact.BodyA == f ? contact.BodyB : contact.BodyA;
        if (other == fallen || other == catapult.Arm) continue;
        if (flyingKind == Catapult.Ammo.Bomb)
        {
            var at = GetPhysicsBodyPosition(f);
            DestroyPhysicsBody(f);
            flying = null;
            touching.Clear();
            Blast(at);
            break;
        }
        if (castle.Of(other) is { } block) touching.Add(block);
        if (contact.Speed > 3) PlaySound(hitSound);
    }
    foreach (var ended in GetPhysicsContactsEnded())
        touching.RemoveWhere(b => b.Body == ended.BodyA || b.Body == ended.BodyB);
    if (flying is { } stoneFlying)
        foreach (var block in touching.ToArray())
        {
            if (block.Gone) continue;
            block.Damage += GetPhysicsContactImpulse(stoneFlying, block.Body);
            if (block.Damage > Castle.Toughness) castle.Crumble(block);
        }
    touching.RemoveWhere(b => b.Gone);

    // -- Every weld strained, the arm brought back, and the next shot loaded once the last rests.
    if (castle.Strain() > 0) PlaySound(crackSound);
    mostForce = MathF.Max(mostForce, castle.Welds.Where(w => !w.Broken).Select(w => w.Force).DefaultIfEmpty(0).Max());
    if (GetState<Screen>() is Screen.Flight or Screen.Won)
    {
        flightTime += dt;
        var home = catapult.Return();
        var resting = flying is not { } r || GetPhysicsBodyVelocity(r).Length() < 0.5f && flightTime > 1;
        if (home && !catapult.IsLoaded && (resting || flightTime > 6))
        {
            catapult.Load(ammo);
            PlaySound(loadSound);
            (flying, flightTime) = (null, 0);
            touching.Clear();
            if (GetState<Screen>() == Screen.Flight) SetState(Screen.Aim);
        }
    }
    flashLeft = MathF.Max(0, flashLeft - dt);
    SetLightColor(flash, new Color(255, 170, 80), flashLeft * 4000);
    var sweep = MathF.Sin((float)GetTime() * 0.3f) * 5;
    SetLightDirection(spot, Vector3.Normalize(Castle.Keep + new Vector3(0, 7, sweep) - spotAt));

    var banner = GetPhysicsBodyPosition(castle.Banner.Body);
    SiegeCommands.Status = $"{GetState<Screen>()} shots {shots} {catapult.Loaded} blocks {castle.Standing} of {castle.Blocks.Count} " +
                           $"welds {castle.Welds.Count} broken {castle.Broken} most {mostForce:0} N strength {Castle.Strength:0} N " +
                           $"banner {banner.Y:0.0} left {lastSpeed:0.0} arm {catapult.Angle:0} spin {GetPhysicsBodyAngularVelocity(catapult.Arm).Z:0.0} shot {(flying is { } sh ? Place(sh) : "none")}";
    SiegeCommands.Welds = castle.Welds;

    // -- The frame.
    BeginDrawing();
    ClearBackground(new Color(120, 150, 200));
    BeginMode3D(camera);
    cube.Transform = Matrix4x4.CreateScale(140, 1, 90) * Matrix4x4.CreateTranslation(15, -0.5f, 0);
    DrawModel(cube, Vector3.Zero, 1, new Color(90, 120, 70));
    castle.Draw();
    catapult.Draw(cube);
    foreach (var body in thrown.Where(IsPhysicsBodyValid)) DrawShot(body, Catapult.Ammo.Stone);
    if (catapult.IsLoaded) DrawShot(catapult.Shot, catapult.Loaded);
    if (flying is { } fl && flyingKind == Catapult.Ammo.Bomb) DrawShot(fl, Catapult.Ammo.Bomb);
    if (GetState<Screen>() == Screen.Aim) DrawArc();
    EndMode3D();
    var message = GetState<Screen>() switch
    {
        Screen.Aim => "Left and Right the power, Up and Down the angle, Tab the shot, Space throws, R builds the castle again",
        Screen.Won => $"The banner is down in {shots} shots. R builds the castle again",
        _ => "",
    };
    DrawText(message, 20, 20, 20, Color.Black);
    DrawText($"Power {power:0}   Angle {elevation:0}   {ammo}   Shots {shots}   Blocks {castle.Standing} of {castle.Blocks.Count}   Welds broken {castle.Broken} of {castle.Welds.Count}",
        20, 48, 20, new Color(30, 30, 40));
    EndDrawing();
}

UnloadModel(cube);
UnloadModel(column);
UnloadModel(roof);
UnloadModel(stone);
CloseAudioDevice();
CloseWindow();

// Where a body is, for the status.
string Place(PhysicsBody body)
{
    var at = GetPhysicsBodyPosition(body);
    return $"{at.X:0.0},{at.Y:0.0},{at.Z:0.0}";
}

// How fast and which way a shot leaves the cup, from the arm's speed and the angle it is let go at.
Vector3 Foreseen(float degreesPerSecond, float angle)
{
    var r = Catapult.Cup(90 + angle) - Catapult.Pivot;
    var w = degreesPerSecond * MathF.PI / 180;
    return new Vector3(r.Y, -r.X, 0) * w;
}

// The path the shot is foreseen to take, each stretch of it swept by a ball the shot's size to
// the first body it meets, which is marked.
void DrawArc()
{
    var at = Catapult.Cup(90 + elevation);
    var velocity = Foreseen(power, elevation) * speedRatio;
    for (int i = 0; i < 100; i++)
    {
        var next = at + velocity * 0.05f + new Vector3(0, -9.81f, 0) * 0.5f * 0.05f * 0.05f;
        velocity += new Vector3(0, -9.81f, 0) * 0.05f;
        var stretch = next - at;
        var hit = GetSphereCastPhysicsEx(new Ray(at, Vector3.Normalize(stretch)), 0.45f, stretch.Length(), catapult.Shot);
        if (hit.Hit)
        {
            DrawSphere(hit.Point, 0.35f, new Color(240, 80, 60));
            return;
        }
        if (i % 2 == 0) DrawSphere(at, 0.08f, new Color(255, 255, 255, 180));
        at = next;
    }
}

void DrawShot(PhysicsBody body, Catapult.Ammo kind)
{
    var transform = GetPhysicsBodyTransform(body);
    if (kind == Catapult.Ammo.Stone)
    {
        stone.Transform = transform;
        DrawModel(stone, Vector3.Zero, 1, new Color(100, 95, 90));
        return;
    }
    var up = Vector3.TransformNormal(Vector3.UnitY, transform);
    var middle = transform.Translation;
    DrawCapsule(middle - up * 0.2f, middle + up * 0.2f, 0.3f, 8, 12, new Color(40, 40, 45));
}

public enum Screen { Aim, Swing, Flight, Won }

public static class SiegeCommands
{
    internal static string Status = "";
    internal static string? Asked;
    internal static Camera3D Camera;
    internal static List<Castle.Weld> Welds = [];

    [Command("siege.status", "The screen, the shots, the shot loaded, the blocks standing, the welds and how many broke, the most force a weld held and the strength it breaks past, how high the banner stands and how fast the last shot left")]
    internal static string Report() => Status;

    [Command("siege.throw", "Throws at a power in degrees a second of the arm and an angle in degrees above the field, a stone or a bomb: siege.throw <power> <angle> <stone|bomb>")]
    internal static string Throw(float power, float angle, string kind)
    {
        if (kind is not ("stone" or "bomb")) return $"no shot called {kind}, stone or bomb";
        Asked = $"throw {power} {angle} {kind}";
        return $"throwing a {kind}";
    }

    [Command("siege.reset", "Lays the castle again and clears the field")]
    internal static string Again()
    {
        Asked = "reset";
        return "the castle is laid again";
    }

    [Command("siege.welds", "The welds holding most, the blocks each joins and the force it held with over the last step: siege.welds <count>")]
    internal static string Most(int count) =>
        string.Join("\n", Welds.Where(w => !w.Broken).OrderByDescending(w => w.Force).Take(count).Select(w =>
            $"{Where(w.A)} to {Where(w.B)} {w.Force:0} N"));

    private static string Where(Castle.Block block)
    {
        var at = GetPhysicsBodyPosition(block.Body);
        return $"{block.Shape} {at.X:0.0},{at.Y:0.0},{at.Z:0.0}";
    }
}
