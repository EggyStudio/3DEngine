// Span, a bridge built over a gap from planks and ropes for a heavy load to roll across. Seen from
// the side, a click on two nodes of the grid lays a plank between them, a right click on a tower
// and then on a node a plank ends at hangs a rope, and Space sends the load. Planks are joined by
// hinges where they meet and to the cliffs by hinges to kinematic anchors, ropes are distance
// joints, and a joint held harder than its strength breaks. Written against the engine's package,
// as a game outside this repository would be.
using System.Numerics;
using Engine;
using static Engine.E3D;

InitWindow(1280, 720, "Span");
InitAudioDevice();
SetTargetFPS(60);
// Planks, on layer 1, meet one another and the cliffs, on layer 2, only through their joints,
// since a plank's end pinned at a cliff's edge pressed into the cliff's face and loaded its joint
// at rest.
SetPhysicsLayersCollide(1, 1, false);
SetPhysicsLayersCollide(1, 2, false);

const float LoadRadius = 0.5f, LoadMass = 150, Roll = 2.5f;
var loadStart = new Vector3(-8, Bridge.CliffTop + LoadRadius, 0);

// -- The cliffs and their towers, the load, and the bridge.

var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
(Vector3 At, Vector3 Size, Color Color)[] ground =
[
    (new(-8, Bridge.CliffTop - 2.5f, 0), new(8, 5, 4), new Color(120, 100, 80)),
    (new(8, Bridge.CliffTop - 2.5f, 0), new(8, 5, 4), new Color(120, 100, 80)),
];
foreach (var (at, size, _) in ground) SetPhysicsBodyLayer(CreatePhysicsStaticBox(at, size), 2);
var sphere = LoadModelFromMesh(GenMeshSphere(LoadRadius, 24, 32));
var load = CreatePhysicsSphere(loadStart, LoadRadius, LoadMass);
SetPhysicsBodyMaterial(load, 0.8f, 0.05f);
var bridge = new Bridge();

CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.5f)), new Color(255, 240, 220), 2.4f, castsShadows: true);
SetAmbientLight(new Color(150, 175, 220), 0.45f);
var camera = new Camera3D(new Vector3(0, 0.5f, 15), new Vector3(0, -0.2f, 0), Vector3.UnitY, 50);
SpanCommands.Camera = camera;
SpanCommands.Bridge = bridge;

Sound Load(string name, float volume)
{
    var sound = LoadSound($"resources/sounds/{name}.wav");
    SetSoundVolume(sound, volume);
    return sound;
}
var (placeSound, ropeSound, goSound, creakSound, snapSound, crossedSound, fellSound) =
    (Load("place", 0.5f), Load("rope", 0.5f), Load("go", 0.6f), Load("creak", 0.4f), Load("snap", 0.7f), Load("crossed", 0.6f), Load("fell", 0.6f));

// -- The game.

AddState(Screen.Build);
SetPhysicsPaused(true);
Vector2? picked = null;
var pickedTower = false;
var most = 0f;

void Reset()
{
    bridge.Clear();
    SetPhysicsBodyPosition(load, loadStart);
    SetPhysicsBodyVelocity(load, Vector3.Zero);
    SetPhysicsBodyAngularVelocity(load, Vector3.Zero);
    (picked, most) = (null, 0);
    SetPhysicsPaused(true);
    SetState(Screen.Build);
}

void Go()
{
    if (GetState<Screen>() != Screen.Build) return;
    SetPhysicsPaused(false);
    PlaySound(goSound);
    SetState(Screen.Crossing);
}

while (!WindowShouldClose())
{
    var screen = GetState<Screen>();
    var at = GetPhysicsBodyPosition(load);

    // -- Building, by the mouse, or by the commands.
    switch (SpanCommands.Asked)
    {
        case "reset": Reset(); break;
        case "go": Go(); break;
        case { } design when design.StartsWith("build ", StringComparison.Ordinal) && screen == Screen.Build:
            foreach (var (a, b) in Bridge.Design(design[6..]))
                if (bridge.CanPlace(a, b)) bridge.Place(a, b);
            break;
    }
    SpanCommands.Asked = null;
    screen = GetState<Screen>();
    if (screen == Screen.Build)
    {
        var node = Pick(GetMousePosition());
        if (IsMouseButtonPressed(MouseButton.Left) && node is { } n)
        {
            if (picked is { } from && !pickedTower && bridge.CanPlace(from, n))
            {
                bridge.Place(from, n);
                PlaySound(placeSound);
                picked = null;
            }
            else (picked, pickedTower) = (n, false);
        }
        if (IsMouseButtonPressed(MouseButton.Right) && node is { } r)
        {
            if (picked is { } tower && pickedTower && bridge.Hang(tower, r))
            {
                PlaySound(ropeSound);
                picked = null;
            }
            else (picked, pickedTower) = (r, Bridge.IsTower(r));
        }
        if (IsKeyPressed(Key.Space) || IsKeyPressed(Key.Enter)) Go();
    }
    else if (IsKeyPressed(Key.R) || IsKeyPressed(Key.Space)) Reset();

    // -- The crossing, the load rolled on toward the far cliff, and every joint strained by it.
    if (screen == Screen.Crossing)
    {
        // Rolled on toward the far cliff and held to its track across the bridge, as a cart on the
        // road is, where small kicks from the planks across the way added up and took it off the
        // far cliff's side.
        var velocity = GetPhysicsBodyVelocity(load);
        if (velocity.X < Roll) ApplyPhysicsImpulse(load, new Vector3(LoadMass * 0.05f, 0, 0));
        SetPhysicsBodyVelocity(load, GetPhysicsBodyVelocity(load) with { Z = -at.Z * 2 });
        var broke = bridge.Strain();
        if (broke > 0) PlaySound(snapSound);
        else if (bridge.Links.Any(l => !l.Broken && l.Force > Bridge.Strength * 0.6f) && !IsSoundPlaying(creakSound)) PlaySound(creakSound);
        most = MathF.Max(most, bridge.Links.Where(l => !l.Broken).Select(l => l.Force).DefaultIfEmpty(0).Max());
        if (at.X > Bridge.Gap + 2 && at.Y > 0)
        {
            PlaySound(crossedSound);
            SetState(Screen.Crossed);
        }
        else if (at.Y < -6)
        {
            PlaySound(fellSound);
            SetState(Screen.Fell);
        }
    }

    SpanCommands.Status = $"{GetState<Screen>()} planks {bridge.Planks.Count} ropes {bridge.Ropes} joints {bridge.Links.Count} broken {bridge.Broken} " +
                          $"most {most:0} N strength {Bridge.Strength:0} N at {at.X:0.00},{at.Y:0.00},{at.Z:0.00}";

    // -- The frame.
    BeginDrawing();
    ClearBackground(new Color(150, 190, 230));
    BeginMode3D(camera);
    foreach (var (gAt, gSize, gColor) in ground) DrawBox(gAt, gSize, Quaternion.Identity, gColor);
    foreach (var tower in Bridge.Anchors.Where(Bridge.IsTower))
        DrawBox(new Vector3(tower.X, (tower.Y + Bridge.CliffTop) / 2, -0.6f), new Vector3(0.3f, tower.Y - Bridge.CliffTop, 0.3f), Quaternion.Identity, new Color(90, 75, 60));
    if (screen == Screen.Build)
        for (int x = -4; x <= 4; x++)
            for (int y = -2; y <= 3; y++)
            {
                var n = new Vector2(x, y);
                var anchor = Bridge.Anchors.Contains(n);
                var color = picked == n ? Color.Yellow : anchor ? new Color(200, 60, 50) : new Color(255, 255, 255, 120);
                DrawSphere(new Vector3(x, y, 0), anchor ? 0.12f : 0.07f, color);
            }
    foreach (var plank in bridge.Planks)
    {
        var length = Vector2.Distance(plank.A, plank.B);
        cube.Transform = Matrix4x4.CreateScale(length, Bridge.PlankThickness, Bridge.PlankDepth) * GetPhysicsBodyTransform(plank.Body);
        DrawModel(cube, Vector3.Zero, 1, new Color(170, 120, 70));
    }
    foreach (var link in bridge.Links)
    {
        if (link.Rope > 0 && !link.Broken) DrawCylinderEx(link.PointA, link.PointB, 0.04f, 0.04f, 6, new Color(80, 60, 40));
        // A joint drawn redder the nearer it is to breaking.
        if (!link.Broken && link.Rope == 0)
        {
            var strain = Math.Clamp(link.Force / Bridge.Strength, 0, 1);
            DrawSphere(link.PointB, 0.08f, new Color((byte)(120 + 135 * strain), (byte)(120 * (1 - strain)), (byte)(120 * (1 - strain))));
        }
    }
    sphere.Transform = GetPhysicsBodyTransform(load);
    DrawModel(sphere, Vector3.Zero, 1, new Color(70, 80, 95));
    EndMode3D();
    var message = GetState<Screen>() switch
    {
        Screen.Build => "Click two nodes to lay a plank, right click a tower and then a plank's end to hang a rope, Space to send the load",
        Screen.Crossing => "The load is crossing",
        Screen.Crossed => "Across. R to build again",
        _ => "It fell. R to build again",
    };
    DrawText(message, 20, 20, 20, Color.Black);
    DrawText($"Planks {bridge.Planks.Count} of {Bridge.MostPlanks}   Ropes {bridge.Ropes} of {Bridge.MostRopes}   Broken {bridge.Broken}", 20, 48, 20, new Color(40, 40, 40));
    EndDrawing();
}

UnloadModel(cube);
UnloadModel(sphere);
CloseAudioDevice();
CloseWindow();

// The node of the grid under a point of the window, where the ray through it meets the bridge's
// plane within a third of a meter of one.
Vector2? Pick(Vector2 mouse)
{
    var ray = GetScreenToWorldRay(mouse, camera);
    if (MathF.Abs(ray.Direction.Z) < 1e-4f) return null;
    var t = -ray.Position.Z / ray.Direction.Z;
    var hit = ray.Position + ray.Direction * t;
    var node = new Vector2(MathF.Round(hit.X), MathF.Round(hit.Y));
    return node.X is >= -4 and <= 4 && node.Y is >= -2 and <= 3 && Vector2.Distance(node, new Vector2(hit.X, hit.Y)) < 0.33f ? node : null;
}

void DrawBox(Vector3 center, Vector3 size, Quaternion turn, Color color)
{
    cube.Transform = Matrix4x4.CreateScale(size) * Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation(center);
    DrawModel(cube, Vector3.Zero, 1, color);
}

public enum Screen { Build, Crossing, Crossed, Fell }

public static class SpanCommands
{
    internal static string Status = "";
    internal static string? Asked;

    [Command("span.status", "The screen, the planks, the ropes, the joints and how many broke, the most force a joint held with and the strength it breaks past, and where the load is")]
    internal static string Report() => Status;

    [Command("span.build", "Lays the planks of a bridge the game knows, a truss under the road or the road alone: span.build <truss|road>")]
    internal static string Build(string design)
    {
        if (Bridge.Design(design).Length == 0) return $"no bridge called {design}, truss or road";
        Asked = "build " + design;
        return $"building the {design}";
    }

    [Command("span.go", "Sends the load across what is built")]
    internal static string Send()
    {
        Asked = "go";
        return "the load goes";
    }

    [Command("span.reset", "Takes the bridge down and puts the load back")]
    internal static string Again()
    {
        Asked = "reset";
        return "the bridge is down";
    }

    [Command("span.node", "Where a node of the grid is in the window, for a click on it: span.node <x> <y>")]
    internal static string Node(int x, int y)
    {
        var at = GetWorldToScreen(new Vector3(x, y, 0), Camera);
        return $"{at.X:0} {at.Y:0}";
    }

    [Command("span.joints", "Each joint, a hinge or a rope, where it holds, the force it held with over the last step of the crossing, and whether it broke")]
    internal static string Joints() =>
        string.Join("\n", Bridge!.Links.Select(l =>
            $"{(l.Rope > 0 ? "rope" : "hinge")} {l.PointB.X:0.0},{l.PointB.Y:0.0} {l.Force:0} N{(l.Broken ? " broken" : "")}"));

    internal static Camera3D Camera;
    internal static Bridge? Bridge;
}
