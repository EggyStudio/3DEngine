// Manor, a first-person walk through an estate: a house of six rooms and the grounds round it, in
// which six lanterns are to be found. The estate is a grid of cells, each a prefab of models, lights,
// a reflection probe and particles, spawned as the player nears and let go behind. Its doors swing
// on hinges, opened by sensors as the player comes to them, and the whole of it, menus and settings
// included, is played with a gamepad alone or with the keyboard and mouse. Written against the
// engine's package, as a game outside this repository would be.
//
// `Manor build-level <folder>` writes the estate and the cells' prefabs as scene files into the
// folder, which is how resources/estate.json and resources/cells were made.
using System.Numerics;
using Engine;
using ImGuiNET;
using static Engine.Engine3D;

if (args is ["build-level", var folder])
{
    LevelBuilder.Build(folder);
    return;
}

var settings = Settings.Load();
var (startWidth, startHeight) = Settings.Resolutions[settings.Resolution];
SetConfigFlags(ConfigFlags.WindowResizable | (settings.Vsync ? ConfigFlags.VsyncHint : ConfigFlags.None));
InitWindow(startWidth, startHeight, "Manor");
InitAudioDevice();
SetTargetFPS(60);
// Escape pauses rather than closing the window.
SetExitKey(Key.Unknown);
settings.Apply();
var ecs = GetApp().World.Resource<EcsWorld>();

// -- The estate always there, the light of a clear afternoon, and the frame's effects

LoadScene("resources/estate.json");
CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.45f, -1, -0.3f)), new Color(255, 244, 226), 2.4f, castsShadows: true);
SetShadowDistance(70);
SetAmbientLight(new Color(150, 175, 215), 0.3f);
var sky = GenImageColor(256, 128, Color.Blank);
ImageDraw(ref sky, GenImageGradientLinear(256, 64, 0, new Color(70, 120, 210), new Color(205, 225, 245)),
    new Rectangle(0, 0, 256, 64), new Rectangle(0, 0, 256, 64), Color.White);
ImageDraw(ref sky, GenImageGradientLinear(256, 64, 0, new Color(120, 140, 110), new Color(60, 72, 60)),
    new Rectangle(0, 0, 256, 64), new Rectangle(0, 64, 256, 64), Color.White);
SetEnvironmentMap(sky, intensity: 0.6f);
SetBloom(0.45f);
// Stepping in from the sun the eye opens to the rooms' lamps, and closes again going out.
SetAutoExposure(true, 0.5f, 2.5f, 1.2f);
SetVignette(0.25f);
SetFxaa(true);

// -- The lanterns to find, each a brass frame round a glow, with a light of its own

var frame = LoadModelFromMesh(GenMeshCube(0.26f, 0.05f, 0.26f));
frame.Materials[0] = new ModelMaterial(new Color(200, 160, 80)) { Metallic = 1, Roughness = 0.25f };
var post = LoadModelFromMesh(GenMeshCube(0.03f, 0.32f, 0.03f));
post.Materials[0] = frame.Materials[0];
var glow = LoadModelFromMesh(GenMeshSphere(0.09f, 12, 12));
glow.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(255, 190, 90), EmissiveIntensity = 6, CastsShadows = false };
var lanterns = Level.Lanterns.Select(at => (
    At: at,
    Sensor: CreatePhysicsTrigger(at + new Vector3(0, 0.6f, 0), new Vector3(4.4f, 3, 4.4f)),
    Light: CreatePointLight(at + new Vector3(0, 0.2f, 0), new Color(255, 190, 110), 1.5f, range: 5))).ToArray();
var found = new bool[lanterns.Length];

// -- The doors, each a panel on a hinge to a post, swung by its motor while someone is at it

var doorTexture = LoadTexture("resources/models/textures/door.png");
var panel = LoadModelFromMesh(GenMeshCube(Level.DoorWidth - 0.1f, Level.DoorHeight - 0.05f, 0.08f));
panel.Materials[0] = new ModelMaterial(Color.White, doorTexture);
var doors = Level.Doors.Select(d => new Door(d.Name, d.Hinge, d.Along)).ToArray();

// -- Sound

var music = LoadMusicStream("resources/sounds/music.wav");
var (chime, creak, step, shut, click) = (LoadSound("resources/sounds/chime.wav"), LoadSound("resources/sounds/creak.wav"),
    LoadSound("resources/sounds/step.wav"), LoadSound("resources/sounds/door-shut.wav"), LoadSound("resources/sounds/click.wav"));
PlayMusicStream(music);

// -- The player, a character controller seen from its eyes

var player = CreatePhysicsCharacter(Level.Start, radius: 0.35f, height: 1.75f);
SetPhysicsCharacterStepHeight(player, 0.4f);
var (yaw, pitch, stride, clock, worst) = (0f, 0f, 0f, 0f, 0f);
var route = 0;
var streamer = new Streamer(ecs);
AddState(Screen.Title);
var settingsFrom = Screen.Title;
Action? binding = null;
var bindingPad = false;
var menuOpened = true;

void Restart()
{
    Array.Clear(found);
    for (int i = 0; i < lanterns.Length; i++) SetLightColor(lanterns[i].Light, new Color(255, 190, 110), 1.5f);
    SetPhysicsBodyPosition(player, Level.Start + new Vector3(0, 0.9f, 0));
    SetPhysicsBodyVelocity(player, Vector3.Zero);
    (yaw, pitch, clock, route, worst) = (0, 0, 0, 0, 0);
}

void Go(Screen to)
{
    SetState(to);
    menuOpened = true;
    PlaySound(click);
}

while (!WindowShouldClose() && !ManorCommands.Quit)
{
    var screen = GetState<Screen>();
    var dt = GetFrameTime();
    var playing = screen == Screen.Play;
    var pad = IsGamepadAvailable(0);
    UpdateMusicStream(music);
    SetMusicVolume(music, settings.Music);

    if (ManorCommands.Warp is { } warp)
    {
        SetPhysicsBodyPosition(player, new Vector3(warp.X, 0.9f, warp.Z));
        if (!float.IsNaN(warp.Y)) (yaw, pitch) = (warp.Y * MathF.PI / 180, 0);
        ManorCommands.Warp = null;
    }

    // Pausing, and the back button in a menu, which go before anything else reads a press.
    if (playing && settings.Pressed(Action.Pause)) Go(Screen.Pause);
    else if (screen == Screen.Pause && binding is null && settings.Pressed(Action.Pause)) Go(Screen.Play);
    // The pad's back button first leaves a slider or a list it is in, which ImGui sees to.
    else if (screen == Screen.Settings && binding is null && !ImGui.IsAnyItemActive() && !ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup)
             && (IsKeyPressed(Key.Escape) || pad && IsGamepadButtonPressed(0, GamepadButton.East)))
        Go(settingsFrom);
    screen = GetState<Screen>();
    playing = screen == Screen.Play;
    SetPhysicsPaused(!playing);

    if (playing && !IsCursorHidden()) DisableCursor();
    if (!playing && IsCursorHidden()) EnableCursor();

    // -- Walking by the bound keys or the left stick, and looking by the mouse or the right stick.
    var feet = GetPhysicsBodyPosition(player) - new Vector3(0, 0.875f, 0);
    var move = Vector2.Zero;
    if (playing)
    {
        clock += dt;
        // The longest frame of the walk after its first second, which a cell streamed in too
        // slowly shows as a hitch.
        if (clock > 1) worst = MathF.Max(worst, dt);
        var look = GetMouseDelta() * 0.0025f * settings.LookSpeed;
        if (pad)
        {
            var stick = Deadzone(GetGamepadAxisMovement(0, GamepadAxis.RightX), GetGamepadAxisMovement(0, GamepadAxis.RightY));
            look += stick * 2.6f * settings.LookSpeed * dt;
            move += Deadzone(GetGamepadAxisMovement(0, GamepadAxis.LeftX), GetGamepadAxisMovement(0, GamepadAxis.LeftY));
        }
        yaw -= look.X;
        pitch = Math.Clamp(pitch - look.Y * (settings.InvertY ? -1 : 1), -1.4f, 1.4f);
        if (settings.Down(Action.Forward)) move.Y -= 1;
        if (settings.Down(Action.Back)) move.Y += 1;
        if (settings.Down(Action.Left)) move.X -= 1;
        if (settings.Down(Action.Right)) move.X += 1;

        // The autopilot walks the route instead, turning to face each point as it goes.
        if (ManorCommands.Autopilot && route < Level.Route.Length)
        {
            var to = Level.Route[route] - feet;
            to.Y = 0;
            if (to.Length() < 0.7f) route++;
            else
            {
                var wanted = MathF.Atan2(-to.X, -to.Z);
                yaw += MathF.IEEERemainder(wanted - yaw, MathF.Tau) * MathF.Min(1, dt * 6);
                pitch *= 1 - MathF.Min(1, dt * 4);
                move = new Vector2(0, -1);
            }
        }
    }
    if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
    var forward = new Vector3(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
    var right = new Vector3(-forward.Z, 0, forward.X);
    var speed = settings.Down(Action.Run) ? 6f : 3.6f;
    MovePhysicsCharacter(player, (right * move.X - forward * move.Y) * speed);
    if (playing && settings.Pressed(Action.Jump) && IsPhysicsCharacterGrounded(player)) JumpPhysicsCharacter(player, 4.2f);

    // A step every stride, a little higher or lower each time.
    var walked = new Vector2(GetPhysicsBodyVelocity(player).X, GetPhysicsBodyVelocity(player).Z).Length();
    if (playing && IsPhysicsCharacterGrounded(player) && walked > 0.5f)
    {
        stride += walked * dt;
        if (stride > 0.75f)
        {
            stride = 0;
            SetSoundPitch(step, 0.85f + Random.Shared.NextSingle() * 0.3f);
            PlaySound(step);
        }
    }

    // -- What the player passes through: the lanterns' sensors and the doors'.
    foreach (var contact in GetPhysicsContacts())
    {
        if (contact.BodyA != player && contact.BodyB != player) continue;
        var other = contact.BodyA == player ? contact.BodyB : contact.BodyA;
        for (int i = 0; i < lanterns.Length; i++)
            if (other == lanterns[i].Sensor && !found[i] && playing)
            {
                found[i] = true;
                SetLightColor(lanterns[i].Light, Color.Black, 0);
                PlaySound(chime);
                if (found.All(f => f)) Go(Screen.Finished);
            }
        foreach (var door in doors) if (other == door.Sensor) door.Inside++;
    }
    foreach (var contact in GetPhysicsContactsEnded())
    {
        if (contact.BodyA != player && contact.BodyB != player) continue;
        var other = contact.BodyA == player ? contact.BodyB : contact.BodyA;
        foreach (var door in doors) if (other == door.Sensor) door.Inside = Math.Max(0, door.Inside - 1);
    }
    foreach (var door in doors)
        switch (door.Swing(feet))
        {
            case Door.Event.Opened: PlaySound(creak); break;
            case Door.Event.Shut: PlaySound(shut); break;
        }

    // -- The cells near the player streamed in, and those far behind let go.
    var eye = feet + new Vector3(0, 1.62f, 0);
    if (screen == Screen.Title) eye = new Vector3(-8, 2.2f, 46);
    streamer.Update(eye);
    SetMotionBlur(settings.MotionBlur && playing ? 0.3f : 0);

    var look3 = playing || screen != Screen.Title
        ? new Vector3(MathF.Cos(pitch) * forward.X, MathF.Sin(pitch), MathF.Cos(pitch) * forward.Z)
        : Vector3.Normalize(new Vector3(0.05f, -0.02f, -1));
    var camera = new Camera3D(eye, eye + look3, Vector3.UnitY, 70);
    ManorCommands.Status = $"{screen} lanterns {found.Count(f => f)}/{found.Length} at {feet.X:0.0},{feet.Y:0.0},{feet.Z:0.0} " +
                           $"cells {streamer.Loaded} doors {doors.Count(d => d.Open)} route {route}/{Level.Route.Length} time {clock:0.0} worst {worst * 1000:0}ms";

    BeginDrawing();
    ClearBackground(new Color(150, 190, 235));
    BeginMode3D(camera);
    for (int i = 0; i < lanterns.Length; i++)
    {
        if (found[i]) continue;
        var at = lanterns[i].At + new Vector3(0, 0.05f * MathF.Sin((float)GetTime() * 2 + i), 0);
        DrawModel(frame, at + new Vector3(0, 0.025f, 0), 1, Color.White);
        DrawModel(frame, at + new Vector3(0, 0.375f, 0), 1, Color.White);
        foreach (var (x, z) in new[] { (-0.11f, -0.11f), (0.11f, -0.11f), (-0.11f, 0.11f), (0.11f, 0.11f) })
            DrawModel(post, at + new Vector3(x, 0.2f, z), 1, Color.White);
        DrawModel(glow, at + new Vector3(0, 0.2f, 0), 1, Color.White);
    }
    foreach (var door in doors)
    {
        panel.Transform = GetPhysicsBodyTransform(door.Panel);
        DrawModel(panel, Vector3.Zero, 1, Color.White);
    }
    EndMode3D();

    if (playing)
    {
        DrawCircle(GetScreenWidth() / 2, GetScreenHeight() / 2, 2.5f, new Color(255, 255, 255, 180));
        Hud(found.Count(f => f), found.Length, clock);
    }
    else Menu(screen);
    EndDrawing();
}

UnloadMusicStream(music);
CloseAudioDevice();
CloseWindow();

static Vector2 Deadzone(float x, float y)
{
    var v = new Vector2(x, y);
    return v.Length() < 0.18f ? Vector2.Zero : v;
}

void Hud(int count, int of, float seconds)
{
    ImGui.SetNextWindowPos(new Vector2(14, 14));
    ImGui.SetNextWindowBgAlpha(0.35f);
    ImGui.Begin("hud", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing);
    ImGui.Text($"Lanterns {count} of {of}");
    ImGui.Text($"{(int)seconds / 60}:{(int)seconds % 60:00}");
    ImGui.End();
}

// The menus, each a window in the middle that takes the pad's focus when it opens.
void Menu(Screen screen)
{
    var size = new Vector2(GetScreenWidth(), GetScreenHeight());
    ImGui.SetNextWindowPos(size / 2, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
    if (menuOpened) ImGui.SetNextWindowFocus();
    menuOpened = false;
    // A window of each screen's own, so the pad starts on a screen's first item, and comes back
    // to the item it left a screen on.
    ImGui.Begin("##" + screen, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings);
    var button = new Vector2(260, 0);
    switch (screen)
    {
        case Screen.Title:
            ImGui.Text("MANOR");
            ImGui.Text("Six lanterns are lit somewhere about the house and its grounds.");
            ImGui.Separator();
            if (ImGui.Button("Walk", button)) { Restart(); Go(Screen.Play); }
            if (ImGui.Button("Settings", button)) { settingsFrom = Screen.Title; Go(Screen.Settings); }
            if (ImGui.Button("Quit", button)) ManorCommands.Quit = true;
            break;
        case Screen.Pause:
            ImGui.Text("Paused");
            if (ImGui.Button("Resume", button)) Go(Screen.Play);
            if (ImGui.Button("Settings", button)) { settingsFrom = Screen.Pause; Go(Screen.Settings); }
            if (ImGui.Button("Leave the estate", button)) Go(Screen.Title);
            break;
        case Screen.Finished:
            ImGui.Text($"All six lanterns found, in {(int)clock / 60}:{(int)clock % 60:00}.");
            if (ImGui.Button("Walk again", button)) { Restart(); Go(Screen.Play); }
            if (ImGui.Button("Title", button)) Go(Screen.Title);
            break;
        case Screen.Settings:
            SettingsMenu(button);
            break;
    }
    ImGui.End();
}

void SettingsMenu(Vector2 button)
{
    var changed = false;
    ImGui.Text("Settings");
    ImGui.Separator();
    var names = Settings.Resolutions.Select(r => $"{r.Width} x {r.Height}").ToArray();
    ImGui.SetNextItemWidth(260);
    changed |= ImGui.Combo("Resolution", ref settings.Resolution, names, names.Length);
    changed |= ImGui.Checkbox("Fullscreen", ref settings.Fullscreen);
    changed |= ImGui.Checkbox("Vertical sync", ref settings.Vsync);
    ImGui.SetNextItemWidth(260);
    changed |= ImGui.SliderFloat("Volume", ref settings.Volume, 0, 1);
    ImGui.SetNextItemWidth(260);
    changed |= ImGui.SliderFloat("Music", ref settings.Music, 0, 1);
    ImGui.SetNextItemWidth(260);
    changed |= ImGui.SliderFloat("Look speed", ref settings.LookSpeed, 0.2f, 3);
    changed |= ImGui.Checkbox("Invert looking up and down", ref settings.InvertY);
    changed |= ImGui.Checkbox("Motion blur", ref settings.MotionBlur);

    ImGui.Separator();
    ImGui.Text("Controls: pick one, then press the key or button for it.");
    if (ImGui.BeginTable("bindings", 3))
    {
        foreach (var action in Enum.GetValues<Action>())
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(action.ToString());
            ImGui.TableNextColumn();
            var waiting = binding == action && !bindingPad;
            if (ImGui.Button(waiting ? "press a key...##k" + action : settings.Keys[action] + "##k" + action, new Vector2(150, 0)))
                (binding, bindingPad) = (action, false);
            ImGui.TableNextColumn();
            if (settings.Buttons.TryGetValue(action, out var padButton) || action is Action.Jump or Action.Run or Action.Pause)
            {
                var waitingPad = binding == action && bindingPad;
                if (ImGui.Button(waitingPad ? "press a button...##p" + action : (settings.Buttons.TryGetValue(action, out var b) ? b.ToString() : "none") + "##p" + action, new Vector2(150, 0)))
                    (binding, bindingPad) = (action, true);
            }
        }
        ImGui.EndTable();
    }

    // A key or a button pressed while one waits binds it, from the frame after the press that
    // picked it, which is still down.
    if (binding is { } wanted)
    {
        if (!bindingPad && GetKeyPressed() is var key && key != Key.Unknown)
        {
            settings.Keys[wanted] = key;
            binding = null;
            changed = true;
        }
        else if (bindingPad && GetGamepadButtonPressed() is { } pressed && pressed != GamepadButton.South)
        {
            settings.Buttons[wanted] = pressed;
            binding = null;
            changed = true;
        }
    }

    ImGui.Separator();
    if (ImGui.Button("Back", button)) Go(settingsFrom);
    if (changed)
    {
        settings.Apply();
        settings.Save();
    }
}

public enum Screen { Title, Play, Pause, Settings, Finished }

/// <summary>A door: a panel on a hinge to a post, a sensor round its doorway, and the motor that swings it.</summary>
public sealed class Door
{
    public enum Event { None, Opened, Shut }

    public string Name { get; }
    public PhysicsBody Post { get; }
    public PhysicsBody Panel { get; }
    public PhysicsBody Sensor { get; }
    public PhysicsJoint Hinge { get; }
    // Who is in the doorway's sensor, counted in as they enter and out as they leave.
    public int Inside;
    public bool Open => MathF.Abs(Angle) > 30;
    public float Angle { get; private set; }
    private readonly Vector3 _hinge, _along;
    private float _toward;
    private bool _wasOpen;

    public Door(string name, Vector3 hinge, Vector3 along)
    {
        Name = name;
        (_hinge, _along) = (hinge, along);
        var middle = new Vector3(0, Level.DoorHeight / 2, 0);
        // The hinge sits a little into the doorway, so the door turned wide clears its frame.
        var axis = hinge + along * 0.06f + middle;
        Post = CreatePhysicsKinematicBox(axis, new Vector3(0.05f, Level.DoorHeight, 0.05f));
        Panel = CreatePhysicsBox(hinge + along * (Level.DoorWidth / 2) + middle, new Vector3(Level.DoorWidth - 0.1f, Level.DoorHeight - 0.05f, 0.08f), mass: 25);
        Hinge = CreatePhysicsHingeJoint(Post, Panel, axis, Vector3.UnitY);
        SetPhysicsHingeLimits(Hinge, -95, 95);
        Sensor = CreatePhysicsTrigger(hinge + along * (Level.DoorWidth / 2) + new Vector3(0, 1.5f, 0), new Vector3(3.2f, 3, 5.5f));
    }

    /// <summary>Drives the motor open, away from whoever is at it, while anyone is, and shut otherwise.</summary>
    public Event Swing(Vector3 player)
    {
        var turned = Vector3.Transform(_along, GetPhysicsBodyRotation(Panel));
        Angle = MathF.Atan2(Vector3.Cross(_along, turned).Y, Vector3.Dot(_along, turned)) * 180 / MathF.PI;
        if (Inside > 0)
        {
            // Turning about +Y swings a door along +X toward -Z, which is away from a player on the +Z side.
            if (_toward == 0) _toward = player.Z > _hinge.Z ? 1 : -1;
            SetPhysicsHingeMotor(Hinge, _toward * 150, 4000);
        }
        else
        {
            _toward = 0;
            var speed = MathF.Abs(Angle) < 1.5f ? 0 : -MathF.Sign(Angle) * MathF.Min(150, MathF.Abs(Angle) * 5);
            SetPhysicsHingeMotor(Hinge, speed, 4000);
        }
        var open = MathF.Abs(Angle) > 12;
        var happened = open && !_wasOpen ? Event.Opened : !open && _wasOpen ? Event.Shut : Event.None;
        _wasOpen = open;
        return happened;
    }
}

/// <summary>
/// Spawns the cells within reach of a point and despawns those beyond, each an entity with a
/// <see cref="SceneRef"/> naming its prefab, a few a frame so a walk into new ground does not stall.
/// </summary>
public sealed class Streamer(EcsWorld ecs)
{
    public const float Near = 40, Far = 52;
    private readonly Dictionary<(int, int), int> _cells = [];

    public int Loaded => _cells.Count;

    public void Update(Vector3 at)
    {
        float Distance(int i, int j) => Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(Level.Center(i, j).X, Level.Center(i, j).Z));

        foreach (var ((i, j), entity) in _cells.ToArray())
            if (Distance(i, j) > Far)
            {
                ecs.DespawnRecursive(entity);
                _cells.Remove((i, j));
            }

        // The nearest missing cells first, two a frame.
        var wanted = Enumerable.Range(0, Level.Cells * Level.Cells)
            .Select(n => (I: n % Level.Cells, J: n / Level.Cells))
            .Where(c => !_cells.ContainsKey(c) && Distance(c.I, c.J) < Near)
            .OrderBy(c => Distance(c.I, c.J))
            .Take(2);
        foreach (var (i, j) in wanted)
        {
            var cell = ecs.Spawn();
            ecs.Add(cell, new SceneRef { Path = $"resources/cells/{Level.PrefabOf(i, j)}.json" });
            ecs.Add(cell, new Transform(Level.Center(i, j)));
            _cells[(i, j)] = cell;
        }
    }
}

public static class ManorCommands
{
    internal static string Status = "";
    internal static bool Autopilot, Quit;
    internal static Vector3? Warp;

    [Command("manor.status", "The screen, the lanterns found, where the player stands, the cells loaded, the doors open, the autopilot's place on its route, the time walked and its longest frame")]
    internal static string Report() => Status;

    [Command("manor.autopilot", "Walks the route through the house and the grounds past every lantern: manor.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "the autopilot walks" : "the autopilot stops";
    }

    [Command("manor.warp", "Puts the player at a point of the ground, facing a way in degrees from north when given: manor.warp <x> <z> [facing]")]
    internal static string Move(float x, float z, float facing = float.NaN)
    {
        // The facing rides in the middle of the vector, which the ground's height does not need.
        Warp = new Vector3(x, facing, z);
        return $"warping to {x}, {z}";
    }
}
