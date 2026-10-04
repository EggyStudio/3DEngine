// Pusher, where the player pushes crates into the goal at the far end of a room. Written against
// the engine's package with what its README and cheatsheet offer, as a game outside this
// repository would be.
using System.Numerics;
using Engine;
using ImGuiNET;
using static Engine.Engine3D;

InitWindow(960, 540, "Pusher");
InitAudioDevice();
SetTargetFPS(60);
var world = GetApp().World;
var ecs = world.Resource<EcsWorld>();

// -- The level, from a scene file of the game's own components

var level = LoadScene("resources/level.json");
var walls = new List<(PhysicsBody Body, Vector3 Size)>();
var crates = new List<(PhysicsBody Body, Vector3 Start)>();
PhysicsBody goal = default;
var goalSize = Vector3.One;
var start = Vector3.Zero;
foreach (var entity in level)
{
    var at = ecs.GetReadOnly<Transform>(entity).Position;
    if (ecs.TryGet<Wall>(entity, out var wall)) walls.Add((CreatePhysicsStaticBox(at, wall.Size), wall.Size));
    if (ecs.TryGet<Crate>(entity, out var crate)) crates.Add((CreatePhysicsBox(at, new Vector3(crate.Size), mass: 2), at));
    if (ecs.TryGet<Goal>(entity, out var g)) (goal, goalSize) = (CreatePhysicsStaticBox(at, g.Size), g.Size);
    if (ecs.Has<PlayerStart>(entity)) start = at;
}

// The player is a character, which walls stop and which pushes the lighter crates.
var player = CreatePhysicsCharacter(start, 0.4f, 1.8f);
var facing = 0f;

// -- Light, sky, models and sound

var sun = ecs.Spawn();
ecs.Add(sun, Light.Directional(new Vector3(1, 0.95f, 0.85f), 2.5f) with { CastsShadows = true });
ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(0.5f, -0.9f, 0), Vector3.One));
var sky = GenImageGradientLinear(128, 64, 0, new Color(110, 160, 230), new Color(225, 215, 190));
SetEnvironmentMap(sky, intensity: 0.6f);

var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var arm = LoadModel("resources/arm.gltf");
var bend = LoadModelAnimations("resources/arm.gltf")[0];
var bendFrame = 0;
var hit = LoadSound("resources/coin.wav");

// -- States, for the menu, play and the pause

GetApp().AddState(Screen.Menu);
var score = 0;
var camera = new Camera3D(new Vector3(0, 12, 12), Vector3.Zero, Vector3.UnitY, 45);

while (!WindowShouldClose())
{
    var screen = world.Resource<State<Screen>>().Current;
    var next = world.Resource<NextState<Screen>>();
    if (screen == Screen.Menu && IsKeyPressed(Key.Enter)) next.Set(Screen.Play);
    if (screen == Screen.Play && IsKeyPressed(Key.P)) next.Set(Screen.Pause);
    if (screen == Screen.Pause && IsKeyPressed(Key.P)) next.Set(Screen.Play);

    // Moving the player, and with it the arm's bend, only while playing.
    var move = Vector3.Zero;
    if (screen == Screen.Play)
    {
        if (IsKeyDown(Key.W)) move.Z -= 1;
        if (IsKeyDown(Key.S)) move.Z += 1;
        if (IsKeyDown(Key.A)) move.X -= 1;
        if (IsKeyDown(Key.D)) move.X += 1;
    }
    if (move != Vector3.Zero)
    {
        move = Vector3.Normalize(move);
        facing = MathF.Atan2(move.X, move.Z);
        UpdateModelAnimation(arm, bend, bendFrame++);
    }
    MovePhysicsCharacter(player, move * 4);
    SetPhysicsPaused(screen != Screen.Play);

    // A crate that reaches the goal scores, chimes, and goes back where it began.
    foreach (var contact in GetPhysicsContacts())
    {
        if (contact.BodyA != goal && contact.BodyB != goal) continue;
        var other = contact.BodyA == goal ? contact.BodyB : contact.BodyA;
        var index = crates.FindIndex(c => c.Body == other);
        if (index < 0) continue;
        score++;
        PlaySound(hit);
        SetPhysicsBodyPosition(other, crates[index].Start + Vector3.UnitY);
        SetPhysicsBodyVelocity(other, Vector3.Zero);
    }

    var playerAt = GetPhysicsBodyPosition(player);
    camera.Position = playerAt + new Vector3(0, 9, 8);
    camera.Target = playerAt;

    BeginDrawing();
    ClearBackground(new Color(30, 34, 44));

    BeginMode3D(camera);
    foreach (var (body, size) in walls)
    {
        cube.Transform = Matrix4x4.CreateScale(size) * GetPhysicsBodyTransform(body);
        DrawModel(cube, Vector3.Zero, 1, new Color(200, 196, 186));
    }
    cube.Transform = Matrix4x4.CreateScale(goalSize) * GetPhysicsBodyTransform(goal);
    DrawModel(cube, Vector3.Zero, 1, new Color(90, 200, 120));
    foreach (var (body, _) in crates)
    {
        cube.Transform = GetPhysicsBodyTransform(body);
        DrawModel(cube, Vector3.Zero, 1, new Color(190, 120, 60));
    }
    // The arm stands on the character's feet, half its 1.8 below its middle.
    arm.Transform = Matrix4x4.CreateRotationY(facing) * Matrix4x4.CreateTranslation(playerAt - new Vector3(0, 0.9f, 0));
    DrawModel(arm, Vector3.Zero, 1, new Color(230, 200, 90));
    EndMode3D();

    DrawText($"Score {score}", 16, 12, 30, Color.White);
    if (screen == Screen.Menu) DrawText("Push the crates into the green goal. Enter starts, P pauses.", 160, 250, 20, Color.White);
    if (screen == Screen.Pause) DrawText("Paused", 420, 250, 40, Color.White);

    ImGui.SetNextWindowPos(new Vector2(760, 12), ImGuiCond.FirstUseEver);
    ImGui.Begin("Pusher", ImGuiWindowFlags.AlwaysAutoResize);
    ImGui.Text($"Score: {score}");
    ImGui.Text($"State: {screen}");
    if (screen != Screen.Menu && ImGui.Button(screen == Screen.Pause ? "Resume" : "Pause"))
        next.Set(screen == Screen.Pause ? Screen.Play : Screen.Pause);
    ImGui.End();

    EndDrawing();
}

UnloadSound(hit);
UnloadModel(arm);
UnloadModel(cube);
UnloadEnvironmentMap();
CloseAudioDevice();
CloseWindow();

/// <summary>A wall, the floor among them, a box of this size that never moves.</summary>
[SceneComponent]
public struct Wall
{
    public Vector3 Size;
}

/// <summary>A crate to push, a cube this wide.</summary>
[SceneComponent]
public struct Crate
{
    public float Size;
}

/// <summary>Where a crate scores, a flat box of this size.</summary>
[SceneComponent]
public struct Goal
{
    public Vector3 Size;
}

/// <summary>Where the player begins.</summary>
[SceneComponent]
public struct PlayerStart;

public enum Screen { Menu, Play, Pause }
