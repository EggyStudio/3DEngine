// Sumo, two marbles on a ring of clay, each a player's, and the first to knock the other off three
// times wins. The window is split down the middle, each half drawn into a render texture of its own
// through a camera behind its player, and the second player plays on a second gamepad or the arrow
// keys. The marbles are drawn by a shader of the game's own, the ring's floor is painted each frame
// by a compute shader with a ripple from every bump, and the crowd round the ring is one instanced
// draw. Written against the engine's package, as a game outside this repository would be.
using System.Numerics;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Sumo");
InitAudioDevice();
SetTargetFPS(60);

const float RingRadius = 8, MarbleRadius = 0.7f;
// How hard a player pushes, how hard a dash is and how long before the next, and the drag that
// keeps a marble from rolling ever faster. A dash is strong beside the push, so a marble hit
// squarely near the middle rolls most of the way to the edge before its player can stop it.
const float Push = 8, Dash = 11, DashWait = 1.2f, Drag = 0.8f;
// The points a match is won by, and the seconds a round lasts before the marble nearer the middle
// takes the point, as the judges give a bout that will not end.
const int ToWin = 3;
const float RoundLimit = 30;

// -- The ring, the stands below it, and the light.

var ring = LoadModelFromMesh(GenMeshCylinder(RingRadius, 1.2f, 64));
var pillar = LoadModelFromMesh(GenMeshCylinder(4.5f, 1.4f, 48));
var stands = LoadModelFromMesh(GenMeshCylinder(17, 0.6f, 64));
var marble = LoadModelFromMesh(GenMeshSphere(MarbleRadius, 24, 32));
var supporter = LoadModelFromMesh(GenMeshCube(0.45f, 0.9f, 0.3f));
var ringAt = new Vector3(0, -1.2f, 0);
CreatePhysicsStaticModel(ring, ringAt);
CreatePhysicsStaticModel(stands, new Vector3(0, -3.2f, 0));

CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), Color.White, 1.6f, castsShadows: true);
SetAmbientLight(new Color(140, 170, 220), 0.45f);
var sky = new Color(110, 165, 225);

// -- The floor, painted each frame by a compute shader into a texture the ring's own shader reads.

const int PaintSize = 512;
const float Extent = 9;
var paint = LoadTextureFromImage(GenImageColor(PaintSize, PaintSize, new Color(200, 160, 110)));
var painter = LoadComputeShader("resources/shaders/ring_paint.slang");
var bumps = new Vector4[8];
var bumpBuffer = LoadShaderBuffer<Vector4>(bumps);
var nextBump = 0;
SetShaderValueBuffer(painter, GetShaderLocation(painter, "bumps"), bumpBuffer);
SetShaderValueTexture(painter, GetShaderLocation(painter, "image"), paint);
SetShaderValue(painter, GetShaderLocation(painter, "extent"), Extent);
SetShaderValue(painter, GetShaderLocation(painter, "radius"), RingRadius);
SetShaderValue(painter, GetShaderLocation(painter, "size"), PaintSize);
var paintTime = GetShaderLocation(painter, "time");

var ringShader = LoadShader("resources/shaders/ring.slang");
SetShaderValueTexture(ringShader, GetShaderLocation(ringShader, "paint"), paint);
SetShaderValue(ringShader, GetShaderLocation(ringShader, "extent"), Extent);
ring.Materials[0] = ring.Materials[0] with { Shader = ringShader, Roughness = 0.9f };

// A ripple from a bump, which the next dispatch starts painting.
void Ripple(Vector3 at, float strength)
{
    bumps[nextBump] = new Vector4(at.X, at.Z, (float)GetTime(), strength);
    nextBump = (nextBump + 1) % bumps.Length;
    UpdateShaderBuffer<Vector4>(bumpBuffer, bumps, 0);
}

// -- The players: their colors, their marbles and the shader that draws them, and their labels.

Color[] colors = [new(230, 60, 70), new(60, 120, 240)];
Color[] swirls = [new(255, 225, 205), new(205, 230, 255)];
Vector3[] starts = [new(-2.4f, MarbleRadius + 0.05f, 0), new(2.4f, MarbleRadius + 0.05f, 0)];
var marbleShader = LoadShader("resources/shaders/marble.slang");
var (tintAt, swirlAt) = (GetShaderLocation(marbleShader, "tint"), GetShaderLocation(marbleShader, "swirl"));
var glass = marble.Materials[0] with { Shader = marbleShader, Roughness = 0.15f };
var bodies = starts.Select(start => CreatePhysicsSphere(start, MarbleRadius)).ToArray();
foreach (var body in bodies) SetPhysicsBodyMaterial(body, 0.8f, 0.55f);

var font = LoadFontEx("resources/Lato-Regular.ttf", 64);
// The score and the screens' words, large, in a distance field, which stays sharp at any size.
var bigFont = LoadFontEx("resources/Lato-Regular.ttf", 48, null, FontType.Sdf);
Texture2D Label(string text, Color color)
{
    var image = ImageTextEx(font, text, 64, 0, color);
    var texture = LoadTextureFromImage(image);
    UnloadImage(image);
    return texture;
}
Texture2D[] labels = [Label("1", colors[0]), Label("2", colors[1])];

// -- The crowd, three rows round the stands, each supporter facing the ring.

var crowdShader = LoadShader("resources/shaders/crowd.slang");
SetShaderValue(crowdShader, GetShaderLocation(crowdShader, "first"), Encoded(colors[0]));
SetShaderValue(crowdShader, GetShaderLocation(crowdShader, "second"), Encoded(colors[1]));
var crowdMaterial = new ModelMaterial(Color.White) { Shader = crowdShader, Roughness = 0.8f };
var seats = new List<(Vector3 At, float Facing, float Phase)>();
var random = new Random(3);
foreach (var row in new[] { 11.5f, 12.8f, 14.1f })
{
    var count = (int)(MathF.Tau * row / 0.75f);
    for (int k = 0; k < count; k++)
    {
        var angle = MathF.Tau * k / count;
        var at = new Vector3(MathF.Cos(angle) * row, -2.6f + 0.45f, MathF.Sin(angle) * row);
        seats.Add((at, MathF.PI / 2 - angle, random.NextSingle() * MathF.Tau));
    }
}
var crowd = new Matrix4x4[seats.Count];
var excitement = 1f;

// -- Sounds, and the dust thrown up where the marbles meet.

var (bumpSound, dashSound, fallSound, pointSound) = (LoadSound("resources/sounds/bump.wav"), LoadSound("resources/sounds/dash.wav"),
    LoadSound("resources/sounds/fall.wav"), LoadSound("resources/sounds/point.wav"));
var (tickSound, goSound, winSound) = (LoadSound("resources/sounds/tick.wav"), LoadSound("resources/sounds/go.wav"), LoadSound("resources/sounds/win.wav"));
var dust = CreateParticleEmitter(Vector3.Zero, ParticleEmitter.Default with
{
    MaxParticles = 300, Emitting = false, Life = 0.6f, LifeVariation = 0.3f, Velocity = new Vector3(0, 2.5f, 0), Spread = 80,
    SpeedVariation = 0.6f, Gravity = new Vector3(0, -4, 0), Radius = 0.3f, StartSize = 0.35f, EndSize = 0.05f,
    StartColor = new Color(235, 215, 180, 220), EndColor = new Color(235, 215, 180, 0),
});

// -- The two views, a render texture each, made again when the window's size changes.

var views = new RenderTexture2D[2];
var (viewWidth, viewHeight) = (0, 0);
var cameras = new Camera3D[2];
for (int i = 0; i < 2; i++) cameras[i] = new Camera3D(new Vector3(0, 7, 14), Vector3.Zero, Vector3.UnitY, 50);

// -- The match.

var screen = Screen.Title;
var score = new int[2];
var cooldown = new float[2];
var wander = new float[2];
var (round, winner, lastScorer, timer, lastCount, roundTime) = (1, -1, -1, 0f, 0, 0f);
var positions = new Vector3[2];

void Place()
{
    for (int i = 0; i < 2; i++)
    {
        SetPhysicsBodyPosition(bodies[i], starts[i]);
        SetPhysicsBodyVelocity(bodies[i], Vector3.Zero);
        SetPhysicsBodyAngularVelocity(bodies[i], Vector3.Zero);
        cooldown[i] = 0;
    }
}

// How large the window's words are drawn, scaled from the window's own size of 1280 by 720 so a
// smaller window fits them.
float Ui() => MathF.Min(GetScreenWidth() / 1280f, GetScreenHeight() / 720f);

// A line of the large font across the window, centered.
void Centered(string text, float y, float size, Color color)
{
    size *= Ui();
    var measured = MeasureTextEx(bigFont, text, size, 2);
    DrawTextEx(bigFont, text, new Vector2((GetScreenWidth() - measured.X) / 2, y), size, 2, color);
}

void Begin(int nextRound)
{
    Place();
    round = nextRound;
    (screen, timer, lastCount, roundTime) = (Screen.Countdown, 3, 4, 0);
}

while (!WindowShouldClose())
{
    var dt = MathF.Min(GetFrameTime(), 1 / 20f);
    var time = (float)GetTime();
    for (int i = 0; i < 2; i++) positions[i] = GetPhysicsBodyPosition(bodies[i]);
    bool Pressed(GamepadButton button) => IsGamepadButtonPressed(0, button) || IsGamepadButtonPressed(1, button);
    var start = IsKeyPressed(Key.Enter) || Pressed(GamepadButton.MiddleRight) || Pressed(GamepadButton.RightFaceDown);

    // -- The screens: the title, the count before a round, play, a point scored, a pause and the win.
    switch (screen)
    {
        case Screen.Title when start:
            score[0] = score[1] = 0;
            Begin(1);
            break;
        case Screen.Countdown:
            timer -= dt;
            if ((int)MathF.Ceiling(timer) is var count && count < lastCount && count > 0) PlaySound(tickSound);
            lastCount = (int)MathF.Ceiling(timer);
            if (timer <= 0)
            {
                PlaySound(goSound);
                screen = Screen.Play;
            }
            break;
        case Screen.Play when IsKeyPressed(Key.P) || Pressed(GamepadButton.MiddleRight):
            screen = Screen.Paused;
            break;
        case Screen.Paused when IsKeyPressed(Key.P) || Pressed(GamepadButton.MiddleRight):
            screen = Screen.Play;
            break;
        case Screen.Play:
            roundTime += dt;
            Play();
            break;
        case Screen.Point:
            timer -= dt;
            if (timer <= 0)
            {
                if (winner >= 0) (screen, timer) = (Screen.Won, 1);
                else Begin(round + 1);
            }
            break;
        case Screen.Won:
            timer -= dt;
            if (timer <= 0 && start)
            {
                score[0] = score[1] = 0;
                winner = -1;
                Begin(1);
            }
            break;
    }
    SetPhysicsPaused(screen is Screen.Title or Screen.Countdown or Screen.Paused);

    // Marbles meeting thud, throw up dust and send a ripple across the floor, louder and stronger
    // the faster they closed.
    foreach (var contact in GetPhysicsContacts())
    {
        if (!(contact.BodyA == bodies[0] && contact.BodyB == bodies[1] || contact.BodyA == bodies[1] && contact.BodyB == bodies[0])) continue;
        if (contact.Speed < 1) continue;
        SetSoundVolume(bumpSound, Math.Clamp(contact.Speed / 8, 0.2f, 1));
        PlaySound(bumpSound);
        SetParticleEmitterPosition(dust, contact.Point with { Y = 0.1f });
        EmitParticles(dust, (int)Math.Clamp(contact.Speed * 8, 10, 60));
        Ripple(contact.Point, Math.Clamp(contact.Speed / 6, 0.3f, 1));
        excitement = MathF.Max(excitement, Math.Clamp(contact.Speed / 6, 0.3f, 1));
    }

    // -- The cameras, circling the ring on the title and the win, and otherwise each behind its
    // player, facing the other, eased toward where it would be so a bump does not shake it.
    for (int i = 0; i < 2; i++)
    {
        if (screen is Screen.Title or Screen.Won)
        {
            var angle = time * 0.2f + i * MathF.PI;
            cameras[i] = cameras[i] with { Position = new Vector3(MathF.Cos(angle) * 15, 7, MathF.Sin(angle) * 15), Target = Vector3.Zero };
            continue;
        }
        var me = Flat(positions[i]);
        var them = Flat(positions[1 - i]);
        var away = me - them;
        away = away.LengthSquared() > 0.0001f ? Vector3.Normalize(away) : new Vector3(i == 0 ? -1 : 1, 0, 0);
        var follow = 1 - MathF.Exp(-dt * 4);
        cameras[i] = cameras[i] with
        {
            Position = Vector3.Lerp(cameras[i].Position, me + away * 7.5f + new Vector3(0, 5, 0), follow),
            Target = Vector3.Lerp(cameras[i].Target, Vector3.Lerp(me, them, 0.4f), 1 - MathF.Exp(-dt * 6)),
        };
    }

    // The crowd bobs, and jumps after a bump or a point until it calms.
    excitement = MathF.Max(0.1f, excitement - dt * 0.35f);
    for (int k = 0; k < seats.Count; k++)
    {
        var jump = MathF.Max(0, MathF.Sin(time * 11 + seats[k].Phase)) * 0.45f * excitement;
        crowd[k] = Matrix4x4.CreateRotationY(seats[k].Facing) * Matrix4x4.CreateTranslation(seats[k].At + new Vector3(0, jump, 0));
    }

    SumoCommands.Status = screen switch
    {
        Screen.Title => "Title",
        Screen.Won => $"Won {winner + 1} score {score[0]} {score[1]}",
        _ => $"{screen} round {round} score {score[0]} {score[1]} p1 {positions[0].X:0.00} {positions[0].Y:0.00} {positions[0].Z:0.00} p2 {positions[1].X:0.00} {positions[1].Y:0.00} {positions[1].Z:0.00}",
    };

    // -- Drawing: each player's view into its render texture, then both side by side.

    var (width, height) = (Math.Max(1, GetScreenWidth() / 2), Math.Max(1, GetScreenHeight()));
    if (width != viewWidth || height != viewHeight)
    {
        foreach (var view in views) if (IsRenderTextureValid(view)) UnloadRenderTexture(view);
        for (int i = 0; i < 2; i++) views[i] = LoadRenderTexture(width, height);
        (viewWidth, viewHeight) = (width, height);
    }

    BeginDrawing();
    for (int i = 0; i < 2; i++)
    {
        BeginTextureMode(views[i]);
        ClearBackground(sky);
        BeginMode3D(cameras[i]);
        DrawModel(stands, new Vector3(0, -3.2f, 0), 1, new Color(96, 120, 84));
        DrawModel(pillar, new Vector3(0, -2.6f, 0), 1, new Color(120, 98, 80));
        DrawModel(ring, ringAt, 1, Color.White);
        DrawMeshInstanced(supporter.Meshes[0], crowdMaterial, crowd);
        for (int p = 0; p < 2; p++)
        {
            // The shader's colors set before each marble, which that draw keeps.
            SetShaderValue(marbleShader, tintAt, Encoded(colors[p]));
            SetShaderValue(marbleShader, swirlAt, Encoded(swirls[p]));
            DrawMesh(marble.Meshes[0], glass, GetPhysicsBodyTransform(bodies[p]));
        }
        for (int p = 0; p < 2; p++) DrawBillboard(cameras[i], labels[p], positions[p] + new Vector3(0, 1.5f, 0), 0.8f, Color.White);
        EndMode3D();

        // The half's own corner: whose it is, and the dash filling up again.
        DrawTextEx(font, $"Player {i + 1}", new Vector2(16, 12), 28, 0, colors[i]);
        var charge = 1 - Math.Clamp(cooldown[i] / DashWait, 0, 1);
        DrawRectangle(16, 48, 120, 10, new Color(0, 0, 0, 110));
        DrawRectangle(16, 48, (int)(120 * charge), 10, charge >= 1 ? Color.Gold : colors[i]);
        EndTextureMode();
    }

    ClearBackground(Color.Black);
    var source = new Rectangle(0, 0, viewWidth, viewHeight);
    DrawTextureRec(views[0].Texture, source, Vector2.Zero, Color.White);
    DrawTextureRec(views[1].Texture, source, new Vector2(GetScreenWidth() - viewWidth, 0), Color.White);
    DrawRectangle(viewWidth - 2, 0, 4, GetScreenHeight(), new Color(20, 22, 30));

    // Each player's score at the top of their half.
    for (int i = 0; i < 2; i++)
    {
        var measured = MeasureTextEx(bigFont, $"{score[i]}", 80 * Ui(), 0);
        var x = (i == 0 ? viewWidth : GetScreenWidth() * 2 - viewWidth) / 2f - measured.X / 2;
        DrawTextEx(bigFont, $"{score[i]}", new Vector2(x, 10 * Ui()), 80 * Ui(), 0, colors[i]);
    }
    switch (screen)
    {
        case Screen.Title:
            Centered("SUMO", GetScreenHeight() * 0.28f, 150, Color.RayWhite);
            Centered("Enter, Start or A to play", GetScreenHeight() * 0.55f, 40, Color.RayWhite);
            Centered("Red: WASD, Space to dash   Blue: arrows, Right Ctrl to dash   or a gamepad each", GetScreenHeight() * 0.65f, 24, Color.RayWhite);
            break;
        case Screen.Countdown:
            Centered($"{Math.Max(1, lastCount)}", GetScreenHeight() * 0.35f, 160, Color.Gold);
            Centered($"Round {round}", GetScreenHeight() * 0.62f, 40, Color.RayWhite);
            break;
        case Screen.Paused:
            Centered("Paused", GetScreenHeight() * 0.4f, 110, Color.RayWhite);
            break;
        case Screen.Point:
            Centered(lastScorer >= 0 ? $"Point to player {lastScorer + 1}" : "Both fell, no point", GetScreenHeight() * 0.4f, 90,
                lastScorer >= 0 ? colors[lastScorer] : Color.RayWhite);
            break;
        case Screen.Won:
            Centered($"Player {winner + 1} wins", GetScreenHeight() * 0.35f, 120, colors[winner]);
            if (timer <= 0) Centered("Enter, Start or A to play again", GetScreenHeight() * 0.58f, 36, Color.RayWhite);
            break;
    }
    EndDrawing();

    // The floor painted for the next frame, which the dispatch runs on the GPU ahead of.
    SetShaderValue(painter, paintTime, (float)GetTime());
    ComputeShaderDispatch(painter, PaintSize / 8, PaintSize / 8, 1);

    // -- Play: each marble pushed by its player or the autopilot, dashes, and a fall scoring.
    void Play()
    {
        for (int i = 0; i < 2; i++)
        {
            var (move, dash) = SumoCommands.Autopilot ? Pilot(i) : Controls(i);
            var velocity = GetPhysicsBodyVelocity(bodies[i]);
            ApplyPhysicsImpulse(bodies[i], (move * Push - Flat(velocity) * Drag) * dt);
            cooldown[i] -= dt;
            if (dash && cooldown[i] <= 0)
            {
                var toward = Flat(positions[1 - i] - positions[i]);
                var way = move != Vector3.Zero ? move : toward.LengthSquared() > 0.0001f ? Vector3.Normalize(toward) : Vector3.UnitX;
                ApplyPhysicsImpulse(bodies[i], way * Dash);
                cooldown[i] = DashWait;
                PlaySound(dashSound);
            }
        }

        // A marble below the ring's top has gone over, and the other scores, both at once none. At
        // the round's end the one nearer the middle scores.
        var fallen = positions.Select(p => p.Y < -1).ToArray();
        if (!fallen[0] && !fallen[1] && roundTime >= RoundLimit)
        {
            var nearer = Flat(positions[0]).Length() <= Flat(positions[1]).Length() ? 0 : 1;
            fallen[1 - nearer] = true;
        }
        if (!fallen[0] && !fallen[1]) return;
        PlaySound(fallSound);
        excitement = 1;
        if (fallen[0] != fallen[1])
        {
            var scorer = fallen[0] ? 1 : 0;
            score[scorer]++;
            PlaySound(score[scorer] >= ToWin ? winSound : pointSound);
            if (score[scorer] >= ToWin) winner = scorer;
            lastScorer = scorer;
        }
        else lastScorer = -1;
        (screen, timer) = (Screen.Point, 2);
    }

    (Vector3 Move, bool Dash) Controls(int i)
    {
        var stick = new Vector2(GetGamepadAxisMovement(i, GamepadAxis.LeftX), GetGamepadAxisMovement(i, GamepadAxis.LeftY));
        if (stick.Length() < 0.2f) stick = Vector2.Zero;
        var (up, left, down, right) = i == 0 ? (Key.W, Key.A, Key.S, Key.D) : (Key.Up, Key.Left, Key.Down, Key.Right);
        stick += new Vector2((IsKeyDown(right) ? 1 : 0) - (IsKeyDown(left) ? 1 : 0), (IsKeyDown(down) ? 1 : 0) - (IsKeyDown(up) ? 1 : 0));
        // Forward is the way the player's camera looks.
        var forward = Flat(cameras[i].Target - cameras[i].Position);
        forward = forward.LengthSquared() > 0.0001f ? Vector3.Normalize(forward) : -Vector3.UnitZ;
        var side = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var move = side * stick.X - forward * stick.Y;
        if (move.LengthSquared() > 1) move = Vector3.Normalize(move);
        var dash = IsGamepadButtonPressed(i, GamepadButton.RightFaceDown) || IsGamepadButtonPressed(i, GamepadButton.RightTrigger1)
            || IsKeyPressed(i == 0 ? Key.Space : Key.RightControl);
        return (move, dash);
    }

    // The autopilot plays as a wrestler does. It works round to the middle's side of the other
    // marble, from where a push sends that one outward, and dashes when it is there and close. It
    // leans off its line by an angle that drifts, so two pilots do not circle each other forever,
    // and backs toward the middle near the edge, nearer the edge the longer a round goes.
    (Vector3 Move, bool Dash) Pilot(int i)
    {
        var me = Flat(positions[i]);
        var them = Flat(positions[1 - i]);
        var toward = them - me;
        var distance = toward.Length();
        toward = distance > 0.01f ? toward / distance : Vector3.UnitX;
        var theirOutward = them.LengthSquared() > 0.0001f ? Vector3.Normalize(them) : toward;
        var toSpot = them - theirOutward * 1.8f - me;
        var heading = toSpot.Length() > 0.4f ? Vector3.Normalize(toSpot) : toward;
        wander[i] = Math.Clamp(wander[i] + (random.NextSingle() - 0.5f) * 3 * dt, -0.5f, 0.5f);
        heading = Vector3.Transform(heading, Quaternion.CreateFromAxisAngle(Vector3.UnitY, wander[i]));
        var outward = me.Length();
        var margin = MathF.Max(0.5f, 2.5f - roundTime * 0.1f);
        if (outward > RingRadius - margin) heading = Vector3.Normalize(heading - me / outward * (outward - (RingRadius - margin)) * 1.5f);
        var placed = Vector3.Dot(toward, theirOutward) > 0.5f;
        return (heading, distance < 3.2f && (placed || roundTime > 15) && random.NextSingle() < dt * 6);
    }
}

foreach (var view in views) if (IsRenderTextureValid(view)) UnloadRenderTexture(view);
foreach (var label in labels) UnloadTexture(label);
UnloadShaderBuffer(bumpBuffer);
UnloadShader(painter);
UnloadShader(ringShader);
UnloadShader(marbleShader);
UnloadShader(crowdShader);
UnloadTexture(paint);
UnloadFont(font);
UnloadFont(bigFont);
foreach (var sound in new[] { bumpSound, dashSound, fallSound, pointSound, tickSound, goSound, winSound }) UnloadSound(sound);
foreach (var model in new[] { ring, pillar, stands, marble, supporter }) UnloadModel(model);
CloseAudioDevice();
CloseWindow();

static Vector3 Flat(Vector3 v) => v with { Y = 0 };

// A color as the shaders take it, its channels from 0 to 1 as encoded for the display.
static Vector4 Encoded(Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

public enum Screen { Title, Countdown, Play, Paused, Point, Won }

public static class SumoCommands
{
    internal static bool Autopilot;
    internal static string Status = "";

    [Command("sumo.status", "The screen, the round, the score, and where each marble is, p1 and p2 each x y z")]
    internal static string Report() => Status;

    [Command("sumo.autopilot", "Plays both marbles, each heading for the other and dashing when close, for a match that plays itself: sumo.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "both marbles play themselves" : "the players play";
    }
}
