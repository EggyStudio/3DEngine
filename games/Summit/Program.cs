// Summit, a small 3D platformer. The player runs and jumps from the island up to the house on the
// summit, collecting the glowing orbs on the way, over a carousel turned by a motor and a bridge
// hung on ropes. Written against the engine's package with what its cheatsheet and guide offer, as a game
// outside this repository would be.
//
// `Summit build-level <folder>` writes the level and its prefabs as scene files into the folder,
// which is how resources/level.json and resources/prefabs were made.
using System.Numerics;
using Engine;
using static Engine.Engine3D;

if (args is ["build-level", var folder])
{
    LevelBuilder.Build(folder);
    return;
}

InitWindow(960, 540, "Summit");
InitAudioDevice();
SetTargetFPS(60);
var ecs = GetApp().World.Resource<EcsWorld>();

// -- The level, a scene of prefabs whose meshes are solid as drawn

var level = LoadScene("resources/level.json");
var orbs = new List<(PhysicsBody Body, Vector3 At)>();
PhysicsBody exit = default;
var start = Vector3.Zero;
foreach (var entity in level)
{
    if (ecs.Has<Orb>(entity)) orbs.Add((ecs.GetReadOnly<PhysicsBody>(entity), ecs.GetReadOnly<Transform>(entity).Position));
    else if (ecs.Has<Exit>(entity)) exit = ecs.GetReadOnly<PhysicsBody>(entity);
    else if (ecs.Has<Start>(entity)) start = ecs.GetReadOnly<Transform>(entity).Position;
}
var collected = new bool[orbs.Count];

// A bridge of one plank hung on two ropes from a beam, which sways as it is crossed.
var beam = CreatePhysicsKinematicBox(new Vector3(5, 9, -27), new Vector3(6, 0.4f, 0.4f));
var bridge = CreatePhysicsBox(new Vector3(5, 3, -27), new Vector3(6, 0.3f, 1.4f), mass: 120);
CreatePhysicsDistanceJoint(beam, bridge, new Vector3(2.5f, 9, -27), new Vector3(2.5f, 3, -27), 5.9f, 6);
CreatePhysicsDistanceJoint(beam, bridge, new Vector3(7.5f, 9, -27), new Vector3(7.5f, 3, -27), 5.9f, 6);
// A lift from the bridge's far side up to the house, moved by the program.
var lift = CreatePhysicsKinematicBox(new Vector3(12, 3, -31), new Vector3(3, 0.4f, 3));

// -- Light outdoors and in, the sky, and what glows

CreateDirectionalLight(new Vector3(-0.5f, -1, -0.35f), new Color(255, 244, 225), 1.6f, castsShadows: true);
CreatePointLight(new Vector3(12, 8.6f, -38), new Color(255, 200, 150), 2.5f, range: 9, castsShadows: true);
SetShadowDistance(60);
var sky = GenImageColor(256, 128, Color.Blank);
ImageDraw(ref sky, GenImageGradientLinear(256, 64, 0, new Color(60, 110, 200), new Color(200, 220, 240)),
    new Rectangle(0, 0, 256, 64), new Rectangle(0, 0, 256, 64), Color.White);
ImageDraw(ref sky, GenImageGradientLinear(256, 64, 0, new Color(150, 160, 140), new Color(70, 80, 70)),
    new Rectangle(0, 0, 256, 64), new Rectangle(0, 64, 256, 64), Color.White);
SetEnvironmentMap(sky, intensity: 0.5f);
// The room reflects itself rather than the sky, which the brass ball by the exit shows.
CreateReflectionProbe(new Vector3(12, 7.6f, -38), new Vector3(8, 3.2f, 8));
SetBloom(0.7f);

var hero = LoadModel("resources/hero.gltf");
var clips = LoadModelAnimations("resources/hero.gltf");
ModelAnimation Clip(string name) => clips.First(c => c.Name == name);
var (idle, run, jump) = (Clip("idle"), Clip("run"), Clip("jump"));
var orb = LoadModelFromMesh(GenMeshSphere(0.3f, 16, 16));
// A glow casts no shadow, which would darken the ground under each orb.
orb.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 200, 90), EmissiveIntensity = 5, CastsShadows = false };
var ball = LoadModelFromMesh(GenMeshSphere(0.5f, 32, 32));
ball.Materials[0] = new ModelMaterial(new Color(230, 190, 110)) { Metallic = 1, Roughness = 0.15f };
var plank = LoadModel("resources/models/plank.obj");
var slab = LoadModelFromMesh(GenMeshCube(3, 0.4f, 3));
var glow = LoadModelFromMesh(GenMeshCube(1.6f, 0.05f, 1.6f));
glow.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(120, 255, 170), EmissiveIntensity = 3, CastsShadows = false };

var music = LoadMusicStream("resources/music.wav");
SetMusicVolume(music, 0.5f);
var (jumpSound, orbSound, landSound, fallSound, winSound) = (LoadSound("resources/sounds/jump.wav"),
    LoadSound("resources/sounds/orb.wav"), LoadSound("resources/sounds/land.wav"),
    LoadSound("resources/sounds/fall.wav"), LoadSound("resources/sounds/win.wav"));

// -- The player, the camera that follows, and the run

var player = CreatePhysicsCharacter(start, radius: 0.35f, height: 1.8f);
// The steps rise 0.4, past the radius a character climbs unless told.
SetPhysicsCharacterStepHeight(player, 0.45f);
var checkpoint = start;
var (facing, yaw, clock, blend, air, falling) = (MathF.PI, 0f, 0f, 0f, 0f, 0f);
var (time, best) = (0f, 0f);
AddState(Screen.Menu);

void Restart()
{
    Array.Clear(collected);
    checkpoint = start;
    SetPhysicsBodyPosition(player, start + Vector3.UnitY * 0.9f);
    SetPhysicsBodyVelocity(player, Vector3.Zero);
    (time, facing, yaw) = (0, MathF.PI, 0);
    SeekMusicStream(music, 0);
}

while (!WindowShouldClose())
{
    var screen = GetState<Screen>();
    var pad = IsGamepadAvailable(0);
    bool Pressed(Key key, GamepadButton button) => IsKeyPressed(key) || pad && IsGamepadButtonPressed(0, button);
    var dt = GetFrameTime();

    // The level's meshes are made solid a frame or two after it loads, so play waits for them.
    var ready = ecs.Query<Collider>().All(c => ecs.Has<PhysicsBody>(c.Entity));
    if (screen == Screen.Menu && ready && Pressed(Key.Enter, GamepadButton.Start))
    {
        Restart();
        PlayMusicStream(music);
        SetState(Screen.Play);
    }
    else if (screen == Screen.Play && Pressed(Key.P, GamepadButton.Start)) SetState(Screen.Pause);
    else if (screen == Screen.Pause && Pressed(Key.P, GamepadButton.Start)) SetState(Screen.Play);
    else if (screen == Screen.Won && Pressed(Key.R, GamepadButton.Start))
    {
        Restart();
        SetState(Screen.Play);
    }
    SetPhysicsPaused(screen != Screen.Play);
    if (screen == Screen.Pause) PauseMusicStream(music);
    else ResumeMusicStream(music);
    UpdateMusicStream(music);

    // Moving relative to the camera, which the arrow keys, the right stick or a right drag turn.
    var input = Vector2.Zero;
    if (screen == Screen.Play)
    {
        if (IsKeyDown(Key.W)) input.Y -= 1;
        if (IsKeyDown(Key.S)) input.Y += 1;
        if (IsKeyDown(Key.A)) input.X -= 1;
        if (IsKeyDown(Key.D)) input.X += 1;
        if (pad) input += Deadzone(GetGamepadAxisMovement(0, GamepadAxis.LeftX), GetGamepadAxisMovement(0, GamepadAxis.LeftY));
        var turn = (IsKeyDown(Key.Left) ? 1 : 0) - (IsKeyDown(Key.Right) ? 1 : 0);
        if (pad) turn -= (int)MathF.Round(Deadzone(GetGamepadAxisMovement(0, GamepadAxis.RightX), 0).X);
        yaw += turn * 2 * dt;
        if (IsMouseButtonDown(MouseButton.Right)) yaw -= GetMouseDelta().X * 0.008f;
        time += dt;
    }
    if (input.Length() > 1) input = Vector2.Normalize(input);
    var forward = new Vector3(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
    var right = new Vector3(-forward.Z, 0, forward.X);
    var walk = (right * input.X - forward * input.Y) * 5.5f;
    MovePhysicsCharacter(player, walk);
    if (walk != Vector3.Zero) facing = MathF.Atan2(walk.X, walk.Z);

    var grounded = IsPhysicsCharacterGrounded(player);
    if (screen == Screen.Play && grounded && Pressed(Key.Space, GamepadButton.South))
    {
        JumpPhysicsCharacter(player, 6.5f);
        PlaySound(jumpSound);
    }
    // A landing after a real fall thuds, and a fall off the world goes back to the last orb.
    var velocity = GetPhysicsBodyVelocity(player);
    if (!grounded) falling = MathF.Min(falling, velocity.Y);
    else
    {
        if (falling < -5) PlaySound(landSound);
        falling = 0;
    }
    if (Warp.To is { } to)
    {
        SetPhysicsBodyPosition(player, to);
        SetPhysicsBodyVelocity(player, Vector3.Zero);
        Warp.To = null;
    }
    if (GetPhysicsBodyPosition(player).Y < -15)
    {
        PlaySound(fallSound);
        SetPhysicsBodyPosition(player, checkpoint + Vector3.UnitY * 1.5f);
        SetPhysicsBodyVelocity(player, Vector3.Zero);
    }

    // The lift rises and falls between the bridge's far side and the house's door, moved by its
    // velocity, toward where it is due a moment from now, so what stands on it rides along.
    var liftY = 4.5f - 1.5f * MathF.Cos((float)GetTime() * 0.6f);
    var liftAt = GetPhysicsBodyPosition(lift);
    SetPhysicsBodyVelocity(lift, screen == Screen.Play ? new Vector3(0, (liftY - liftAt.Y) * 8, 0) : Vector3.Zero);

    // An orb is collected by touching it, and the house's glowing floor ends the run once all are.
    foreach (var contact in GetPhysicsContacts())
    {
        if (contact.BodyA != player && contact.BodyB != player) continue;
        var other = contact.BodyA == player ? contact.BodyB : contact.BodyA;
        var index = orbs.FindIndex(o => o.Body == other);
        if (index >= 0 && !collected[index] && screen == Screen.Play)
        {
            collected[index] = true;
            checkpoint = orbs[index].At;
            PlaySound(orbSound);
            if (pad) SetGamepadVibration(0, 0.3f, 0.3f, 0.15f);
        }
        if (other == exit && screen == Screen.Play && collected.All(c => c))
        {
            PlaySound(winSound);
            best = best == 0 ? time : MathF.Min(best, time);
            SetState(Screen.Won);
        }
    }

    // The clips by time, idle turning into run with speed, and the jump pose in the air.
    var speed = new Vector2(velocity.X, velocity.Z).Length();
    clock += dt;
    blend += ((grounded ? MathF.Min(speed / 5.5f, 1) : 1) - blend) * MathF.Min(1, dt * 10);
    air += ((grounded ? 0 : 1) - air) * MathF.Min(1, dt * 12);
    if (air > 0.5f) UpdateModelAnimationBlend(hero, run, clock, jump, clock, air);
    else UpdateModelAnimationBlend(hero, idle, clock, run, clock, blend);

    var at = GetPhysicsBodyTransform(player).Translation;
    Warp.WhereAt = at;
    // The camera follows seven back and above, and comes in front of a wall or roof between it and
    // the player, as inside the house, ignoring the triggers, which hide nothing.
    var head = at + Vector3.UnitY;
    var back = Vector3.Normalize(-forward * 7 + new Vector3(0, 2.2f, 0));
    var distance = 7.3f;
    if (GetRayCollisionPhysics(new Ray(head + back * 0.5f, back), distance, out var blocked)
        && blocked.Body != exit && !orbs.Exists(o => o.Body == blocked.Body))
        distance = MathF.Max(1, Vector3.Distance(head, blocked.Point) - 0.3f);
    var camera = new Camera3D(head + back * distance, head, Vector3.UnitY, 55);

    BeginDrawing();
    ClearBackground(new Color(60, 110, 200));

    BeginMode3D(camera);
    DrawSkybox();
    // The island, the steps, the blocks, the house and the carousel draw themselves from the scene.
    plank.Transform = GetPhysicsBodyTransform(bridge);
    DrawModel(plank, Vector3.Zero, 1, Color.White);
    slab.Transform = GetPhysicsBodyTransform(lift);
    DrawModel(slab, Vector3.Zero, 1, new Color(170, 165, 155));
    DrawLine3D(new Vector3(2.5f, 9, -27), Vector3.Transform(new Vector3(-2.5f, 0, 0), GetPhysicsBodyTransform(bridge)), new Color(90, 70, 50));
    DrawLine3D(new Vector3(7.5f, 9, -27), Vector3.Transform(new Vector3(2.5f, 0, 0), GetPhysicsBodyTransform(bridge)), new Color(90, 70, 50));
    for (int i = 0; i < orbs.Count; i++)
        if (!collected[i])
            DrawModel(orb, orbs[i].At + Vector3.UnitY * 0.15f * MathF.Sin((float)GetTime() * 2 + i), 1, Color.White);
    DrawModel(ball, new Vector3(9.5f, 6.5f, -40.5f), 1, Color.White);
    if (collected.All(c => c)) DrawModel(glow, new Vector3(12, 6.03f, -40.5f), 1, Color.White);
    // The hero stands on the character's feet, half its height below its middle.
    hero.Transform = Matrix4x4.CreateRotationY(facing) * Matrix4x4.CreateTranslation(at - new Vector3(0, 0.9f, 0));
    DrawModel(hero, Vector3.Zero, 1, Color.White);
    EndMode3D();

    DrawText($"Orbs {collected.Count(c => c)}/{orbs.Count}", 16, 12, 30, Color.White);
    DrawText($"{time:0.0} s", 16, 46, 20, Color.White);
    if (best > 0) DrawText($"Best {best:0.0} s", 16, 70, 20, Color.Gold);
    var center = GetScreenWidth() / 2;
    switch (screen)
    {
        case Screen.Menu:
            DrawText("SUMMIT", center - MeasureText("SUMMIT", 60) / 2, 160, 60, Color.White);
            var prompt = ready ? "Enter or Start to climb" : "Loading the level...";
            DrawText(prompt, center - MeasureText(prompt, 24) / 2, 240, 24, Color.White);
            DrawText("WASD or the left stick to run, Space or South to jump, arrows or the right stick to look, P or Start to pause",
                center - MeasureText("WASD or the left stick to run, Space or South to jump, arrows or the right stick to look, P or Start to pause", 16) / 2,
                290, 16, Color.LightGray);
            break;
        case Screen.Pause:
            DrawText("Paused", center - MeasureText("Paused", 40) / 2, 240, 40, Color.White);
            break;
        case Screen.Won:
            var line = $"The summit in {time:0.0} seconds. R or Start to climb again.";
            DrawText(line, center - MeasureText(line, 28) / 2, 240, 28, Color.Gold);
            break;
        default:
            if (collected.All(c => c)) DrawText("Every orb. Into the house!", center - MeasureText("Every orb. Into the house!", 24) / 2, 12, 24, Color.Gold);
            break;
    }
    EndDrawing();
}

UnloadMusicStream(music);
foreach (var sound in new[] { jumpSound, orbSound, landSound, fallSound, winSound }) UnloadSound(sound);
UnloadModelAnimations(clips);
foreach (var model in new[] { hero, orb, ball, plank, slab, glow }) UnloadModel(model);
UnloadEnvironmentMap();
CloseAudioDevice();
CloseWindow();

static Vector2 Deadzone(float x, float y) => new(MathF.Abs(x) < 0.2f ? 0 : x, MathF.Abs(y) < 0.2f ? 0 : y);

/// <summary>An orb to collect, a trigger in the level.</summary>
[SceneComponent]
public struct Orb;

/// <summary>The trigger in the house that ends the run once every orb is collected.</summary>
[SceneComponent]
public struct Exit;

/// <summary>Where the player starts, on the island.</summary>
[SceneComponent]
public struct Start;

public enum Screen { Menu, Play, Pause, Won }

/// <summary>Moves the player from the console or from <c>./e3d</c>, to try a stretch of the level without climbing to it.</summary>
public static class Warp
{
    internal static Vector3? To;

    [Command("summit.warp", "Puts the player's middle at a point: summit.warp <x> <y> <z>")]
    internal static string Move(float x, float y, float z)
    {
        To = new Vector3(x, y, z);
        return $"warping to {x}, {y}, {z}";
    }

    [Command("summit.where", "Where the player's middle is")]
    internal static string Where() => $"{WhereAt.X:0.00} {WhereAt.Y:0.00} {WhereAt.Z:0.00}";

    internal static Vector3 WhereAt;
}

/// <summary>Writes the level and its prefabs as scene files, built by the calls that make them.</summary>
public static class LevelBuilder
{
    public static void Build(string folder)
    {
        SetConfigFlags(ConfigFlags.WindowHidden);
        InitWindow(320, 180, "Summit level");
        var ecs = GetApp().World.Resource<EcsWorld>();
        Directory.CreateDirectory(Path.Combine(folder, "prefabs"));

        // A prefab is one entity naming a model, solid as the model is drawn, found beside the
        // program as the prefabs themselves are.
        foreach (var name in new[] { "island", "block", "steps", "house" })
        {
            var piece = ecs.Spawn();
            ecs.SetName(piece, name);
            ecs.Add(piece, new ModelRef { Path = $"resources/models/{name}.obj" });
            ecs.Add(piece, new Transform(Vector3.Zero));
            ecs.Add(piece, Collider.Mesh);
            ecs.Add(piece, RigidBody.Static);
            SaveScene(Path.Combine(folder, "prefabs", name + ".json"), [ecs.Handle(piece)]);
            ecs.Despawn(piece);
        }

        var placed = new List<Entity>();
        Entity Place(string name, string prefab, Vector3 at)
        {
            var entity = ecs.Spawn();
            ecs.SetName(entity, name);
            ecs.Add(entity, new SceneRef { Path = $"resources/prefabs/{prefab}.json" });
            ecs.Add(entity, new Transform(at));
            placed.Add(ecs.Handle(entity));
            return ecs.Handle(entity);
        }
        Place("Island", "island", Vector3.Zero);
        Place("Steps", "steps", new Vector3(0, 0, -10));
        Place("Block A", "block", new Vector3(0, 2, -15.5f));
        Place("Block B", "block", new Vector3(0, 2.6f, -27));
        Place("Block C", "block", new Vector3(11.5f, 3, -27));
        Place("House", "house", new Vector3(12, 6, -38));

        // The carousel, a plank turned about a post by a hinge's motor, in the file as a joint.
        var post = ecs.Spawn();
        ecs.SetName(post, "Carousel post");
        ecs.Add(post, new Transform(new Vector3(0, 2, -21)));
        ecs.Add(post, Collider.Box(new Vector3(0.6f, 0.4f, 0.6f)));
        ecs.Add(post, RigidBody.Kinematic);
        var board = ecs.Spawn();
        ecs.SetName(board, "Carousel");
        ecs.Add(board, new Transform(new Vector3(0, 2.35f, -21), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), Vector3.One));
        ecs.Add(board, new ModelRef { Path = "resources/models/plank.obj" });
        ecs.Add(board, Collider.Box(new Vector3(6, 0.3f, 1.4f)));
        ecs.Add(board, RigidBody.Dynamic(60));
        var hinge = ecs.Spawn();
        ecs.SetName(hinge, "Carousel hinge");
        ecs.Add(hinge, new Transform(new Vector3(0, 2.2f, -21)));
        ecs.Add(hinge, new Joint { Kind = JointKind.Hinge, A = ecs.Handle(post), B = ecs.Handle(board), MotorSpeed = 25, MotorTorque = 20000 });
        placed.AddRange([ecs.Handle(post), ecs.Handle(board), ecs.Handle(hinge)]);

        Vector3[] orbs = [new(-8, 2.2f, -2), new(7, 1.6f, 5), new(0, 3.2f, -15.5f), new(0, 3.8f, -27), new(5, 4.6f, -27), new(11.5f, 4.2f, -27)];
        for (int i = 0; i < orbs.Length; i++)
        {
            var orb = ecs.Spawn();
            ecs.SetName(orb, $"Orb {i + 1}");
            ecs.Add(orb, new Orb());
            ecs.Add(orb, new Transform(orbs[i]));
            ecs.Add(orb, Collider.Box(new Vector3(0.9f)) with { IsTrigger = true });
            ecs.Add(orb, RigidBody.Static);
            placed.Add(ecs.Handle(orb));
        }
        var exit = ecs.Spawn();
        ecs.SetName(exit, "Exit");
        ecs.Add(exit, new Exit());
        ecs.Add(exit, new Transform(new Vector3(12, 7, -40.5f)));
        ecs.Add(exit, Collider.Box(new Vector3(1.6f, 2, 1.6f)) with { IsTrigger = true });
        ecs.Add(exit, RigidBody.Static);
        var start = ecs.Spawn();
        ecs.SetName(start, "Start");
        ecs.Add(start, new Start());
        ecs.Add(start, new Transform(new Vector3(0, 0, 8)));
        placed.AddRange([ecs.Handle(exit), ecs.Handle(start)]);

        SaveScene(Path.Combine(folder, "level.json"), placed);
        CloseWindow();
    }
}
