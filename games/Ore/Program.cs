// Ore, a sample of a voxel landscape under global illumination, 48 by 48 columns of grass, dirt,
// stone and emissive ore, walked in the first person under a sun, or at night, where the ore lights
// the cliffs around it alone. Written against the engine's package, as a game outside this
// repository would be.
using System.Numerics;
using Engine;
using static Engine.E3D;

InitWindow(960, 540, "Ore");
SetTargetFPS(60);
SetSceneField(4, 0.5f, 2);
var quality = GlobalIllumination.High;
SetGlobalIllumination(quality);

// -- The landscape, its faces against the air in one mesh colored by block, the ore's in another

var heights = new int[Land.Size, Land.Size];
for (int x = 0; x < Land.Size; x++)
    for (int z = 0; z < Land.Size; z++)
        heights[x, z] = Land.Height(x, z);
var ground = LoadModelFromMesh(Land.Mesh(heights, ore: false));
var ore = LoadModelFromMesh(Land.Mesh(heights, ore: true));
ore.Materials[0] = new ModelMaterial(new Color(70, 50, 40)) { Emissive = new Color(255, 140, 50), EmissiveIntensity = 5 };
CreatePhysicsStaticModel(ground, Vector3.Zero);
CreatePhysicsStaticModel(ore, Vector3.Zero);

var sunColor = new Color(255, 244, 225);
var sun = CreateDirectionalLight(Vector3.Normalize(new Vector3(0.5f, -1, 0.3f)), sunColor, 2, castsShadows: true);
var shownNight = false;

// -- The player, a character controller seen from its eyes, and the game

const float Height = 1.7f;
var start = Land.Top(heights, Land.Size / 2, Land.Size / 2);
var player = CreatePhysicsCharacter(start, 0.3f, Height);
var (yaw, pitch, stuck, jumps, target) = (0f, -0.2f, 0f, 0, 0);
// Where the last jump left the ground and how high above it the player rose, which ore.status says.
var (takeoff, rise) = (0f, 0f);
var screen = Screen.Title;

while (!WindowShouldClose())
{
    var dt = MathF.Min(GetFrameTime(), 0.1f);
    var pad = IsGamepadAvailable(0);
    var begin = IsKeyPressed(Key.Enter) || pad && IsGamepadButtonPressed(0, GamepadButton.RightFaceDown);
    if (screen == Screen.Title && begin) screen = Screen.Play;
    else if (IsKeyPressed(Key.P) || pad && IsGamepadButtonPressed(0, GamepadButton.MiddleRight))
        screen = screen == Screen.Play ? Screen.Pause : screen == Screen.Pause ? Screen.Play : screen;
    if (IsKeyPressed(Key.G))
    {
        quality = quality == GlobalIllumination.High ? GlobalIllumination.Low : quality + 1;
        SetGlobalIllumination(quality);
    }
    if (IsKeyPressed(Key.N)) OreCommands.Night = !OreCommands.Night;
    if (OreCommands.Night != shownNight)
    {
        // At night the sun goes down and the ore is the only light.
        shownNight = OreCommands.Night;
        SetLightColor(sun, shownNight ? Color.Black : sunColor, shownNight ? 0 : 2);
    }
    if (OreCommands.Warp is { } warp)
    {
        SetPhysicsBodyPosition(player, Land.Top(heights, (int)warp.X, (int)warp.Z) + new Vector3(0, Height / 2, 0));
        SetPhysicsBodyVelocity(player, Vector3.Zero);
        if (!float.IsNaN(warp.Y)) (yaw, pitch) = (warp.Y * MathF.PI / 180, -0.2f);
        (OreCommands.Warp, screen) = (null, screen == Screen.Title ? Screen.Play : screen);
    }
    var playing = screen == Screen.Play;
    SetPhysicsPaused(!playing);
    if (playing && !IsCursorHidden()) DisableCursor();
    if (!playing && IsCursorHidden()) EnableCursor();

    // -- Walking by the keys or the left stick, looking by the mouse or the right stick, as Manor does
    var feet = GetPhysicsBodyPosition(player) - new Vector3(0, Height / 2, 0);
    var move = Vector2.Zero;
    if (playing)
    {
        var look = GetMouseDelta() * 0.0025f;
        if (pad)
        {
            look += new Vector2(GetGamepadAxisMovement(0, GamepadAxis.RightX), GetGamepadAxisMovement(0, GamepadAxis.RightY)) * 2.6f * dt;
            move += new Vector2(GetGamepadAxisMovement(0, GamepadAxis.LeftX), GetGamepadAxisMovement(0, GamepadAxis.LeftY));
        }
        yaw -= look.X;
        pitch = Math.Clamp(pitch - look.Y, -1.4f, 1.4f);
        if (IsKeyDown(Key.W)) move.Y -= 1;
        if (IsKeyDown(Key.S)) move.Y += 1;
        if (IsKeyDown(Key.A)) move.X -= 1;
        if (IsKeyDown(Key.D)) move.X += 1;
        if (OreCommands.Autopilot)
        {
            // From point to point round the landscape, a new point every ten seconds where one is not reached.
            var to = Land.Point(target) - feet;
            to.Y = 0;
            stuck += dt;
            if (to.Length() < 1 || stuck > 10) (target, stuck) = (target + 1, 0);
            yaw += MathF.IEEERemainder(MathF.Atan2(-to.X, -to.Z) - yaw, MathF.Tau) * MathF.Min(1, dt * 6);
            move = new Vector2(0, -1);
        }
    }
    if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
    var forward = new Vector3(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
    var right = new Vector3(-forward.Z, 0, forward.X);
    MovePhysicsCharacter(player, (right * move.X - forward * move.Y) * (IsKeyDown(Key.LeftShift) ? 6 : 3.6f));
    var velocity = GetPhysicsBodyVelocity(player);
    // A block is a step higher than the character climbs, so the player jumps up it, and the
    // autopilot jumps where it walks and goes nowhere.
    var blocked = OreCommands.Autopilot && move != Vector2.Zero && new Vector2(velocity.X, velocity.Z).Length() < 0.5f;
    var jump = IsKeyPressed(Key.Space) || pad && IsGamepadButtonPressed(0, GamepadButton.RightFaceDown) || blocked;
    if (playing && jump && IsPhysicsCharacterGrounded(player))
    {
        JumpPhysicsCharacter(player, 5.2f);
        (jumps, takeoff, rise) = (jumps + 1, feet.Y, 0);
    }
    if (jumps > 0) rise = MathF.Max(rise, feet.Y - takeoff);
    // Back to the middle after a fall off the edge.
    if (feet.Y < -20)
    {
        SetPhysicsBodyPosition(player, start + new Vector3(0, Height / 2, 0));
        SetPhysicsBodyVelocity(player, Vector3.Zero);
    }

    var eye = feet + new Vector3(0, Height - 0.15f, 0);
    var camera = new Camera3D(eye, eye + new Vector3(MathF.Cos(pitch) * forward.X, MathF.Sin(pitch), MathF.Cos(pitch) * forward.Z), Vector3.UnitY, 70);
    OreCommands.Status = $"{screen} at {feet.X:0.00} {feet.Y:0.00} {feet.Z:0.00} {(IsPhysicsCharacterGrounded(player) ? "grounded" : "in the air")} " +
                         $"jumps {jumps} rise {rise:0.00} light {quality} {(shownNight ? "night" : "day")}";

    BeginDrawing();
    ClearBackground(shownNight ? new Color(8, 10, 20) : new Color(150, 195, 240));
    BeginMode3D(camera);
    DrawModel(ground, Vector3.Zero, 1, Color.White);
    DrawModel(ore, Vector3.Zero, 1, Color.White);
    EndMode3D();
    DrawText($"G light: {quality}   N {(shownNight ? "day" : "night")}   B global illumination", 16, GetScreenHeight() - 30, 18, Color.White);
    if (screen == Screen.Title)
    {
        DrawText("ORE", GetScreenWidth() / 2 - MeasureText("ORE", 60) / 2, 150, 60, new Color(255, 170, 90));
        const string Keys = "Enter starts, WASD or the stick walks, Space jumps, the mouse looks, P pauses.";
        DrawText(Keys, GetScreenWidth() / 2 - MeasureText(Keys, 20) / 2, 240, 20, Color.White);
    }
    if (screen == Screen.Pause) DrawText("Paused", GetScreenWidth() / 2 - MeasureText("Paused", 40) / 2, 240, 40, Color.White);
    if (IsKeyDown(Key.B)) DrawBounceWindow();
    EndDrawing();
}

UnloadLight(sun);
UnloadModel(ground);
UnloadModel(ore);
CloseWindow();

public enum Screen { Title, Play, Pause }
