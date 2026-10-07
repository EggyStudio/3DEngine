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

// -- The level, from a scene file whose colliders and bodies say what is solid

IReadOnlyList<Entity> level = [];
var walls = new List<(PhysicsBody Body, Vector3 Size)>();
var crates = new List<(PhysicsBody Body, Vector3 Start)>();
PhysicsBody goal = default, player = default;
var goalSize = Vector3.One;

// Loads the level, in place of the one before when it is loaded again to restart.
void LoadLevel()
{
    foreach (var entity in level)
        if (ecs.TryResolve(entity, out var id)) ecs.DespawnRecursive(id);
    level = LoadScene("resources/level.json");
    walls.Clear();
    crates.Clear();
    foreach (var entity in level)
    {
        var body = ecs.GetReadOnly<PhysicsBody>(entity);
        var size = ecs.GetReadOnly<Collider>(entity).Size;
        if (ecs.Has<Crate>(entity)) crates.Add((body, ecs.GetReadOnly<Transform>(entity).Position));
        else if (ecs.Has<Goal>(entity)) (goal, goalSize) = (body, size);
        else if (ecs.Has<Player>(entity)) player = body;
        else walls.Add((body, size));
    }
}
LoadLevel();
var facing = 0f;

// -- Light, sky, models and sound

CreateDirectionalLight(new Vector3(-0.4f, -0.8f, -0.45f), new Color(255, 248, 235), 2.5f, castsShadows: true);
var sky = GenImageGradientLinear(128, 64, 0, new Color(110, 160, 230), new Color(225, 215, 190));
SetEnvironmentMap(sky, intensity: 0.6f);

var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var arm = LoadModel("resources/arm.gltf");
var bend = LoadModelAnimations("resources/arm.gltf")[0];
var bendFrame = 0;
var hit = LoadSound("resources/coin.wav");

// -- States, for the menu, play and the pause

AddState(Screen.Menu);
var score = 0;
var camera = new Camera3D(new Vector3(0, 12, 12), Vector3.Zero, Vector3.UnitY, 45);

while (!WindowShouldClose())
{
    var screen = GetState<Screen>();
    if (screen == Screen.Menu && IsKeyPressed(Key.Enter)) SetState(Screen.Play);
    if (screen == Screen.Play && IsKeyPressed(Key.P)) SetState(Screen.Pause);
    if (screen == Screen.Pause && IsKeyPressed(Key.P)) SetState(Screen.Play);
    if (screen == Screen.Play && IsKeyPressed(Key.R)) LoadLevel();

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
    PusherCommands.Status = $"{GetState<Screen>()} at {playerAt.X:0.00} {playerAt.Z:0.00}";
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
    if (screen == Screen.Menu) DrawText("Push the crates into the green goal. Enter starts, P pauses, R restarts.", 120, 250, 20, Color.White);
    if (screen == Screen.Pause) DrawText("Paused", 420, 250, 40, Color.White);

    ImGui.SetNextWindowPos(new Vector2(760, 12), ImGuiCond.FirstUseEver);
    ImGui.Begin("Pusher", ImGuiWindowFlags.AlwaysAutoResize);
    ImGui.Text($"Score: {score}");
    ImGui.Text($"State: {screen}");
    if (screen != Screen.Menu && ImGui.Button(screen == Screen.Pause ? "Resume" : "Pause"))
        SetState(screen == Screen.Pause ? Screen.Play : Screen.Pause);
    ImGui.End();

    EndDrawing();
}

UnloadSound(hit);
UnloadModel(arm);
UnloadModel(cube);
UnloadEnvironmentMap();
CloseAudioDevice();
CloseWindow();

/// <summary>A crate to push into the goal.</summary>
[SceneComponent]
public struct Crate;

/// <summary>Where a crate scores.</summary>
[SceneComponent]
public struct Goal;

/// <summary>The player's character.</summary>
[SceneComponent]
public struct Player;

public enum Screen { Menu, Play, Pause }

public static class PusherCommands
{
    internal static string Status = "";

    [Command("pusher.status", "The screen and where the player stands across the floor, x and z")]
    internal static string Report() => Status;
}
