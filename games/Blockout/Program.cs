// Blockout, a level blocked out from one recipe of the modeling tools and walked in, three rooms, a
// corridor, stairs up to a second floor, two doors and a window, in the prototype materials, under a
// sun and global illumination. WASD walks, the mouse looks about and the arrows too, Space jumps,
// Tab opens the modeling window on the level so it is reshaped while it is walked in, and F5 saves
// the recipe. Written against the engine's package, as a game outside this repository would be.
using System.Numerics;
using Engine;
using ImGuiNET;
using static Engine.E3D;

InitWindow(1280, 720, "Blockout");
SetTargetFPS(60);

// -- The level, made from its recipe in the game's resources, and named for the model commands

var level = LoadEditMesh(Level.File);
SetEditMeshName(level, Level.Name);
Console.WriteLine($"[blockout] {Level.File}, {GetEditMeshInfo(level).FaceCount} faces from {Level.Lines(level)} lines");
Model? model = null;
PhysicsBody collider = default;
var built = -1;

// -- The light

// The day sky standing at two in the afternoon on a path leaning 40 degrees toward -Z, which puts
// the sun high in the west over the second room's window as BevyCSharp's Blockout has it, and gives
// global illumination the sky's light through the doors, the window and the open room. Two
// cascades of the scene field of 0.25 reach 32 meters across, the level's 22 in the second, as
// Wick's house has them. A lamp hangs under the hall's ceiling, since the room over it keeps the sun
// out, as strong as Wick's lamp a player carries.
SetSkyDay(new SkyDay { Minutes = 0, Tilt = -40 });
SetSkyTime(14);
SetSceneField(2, 0.25f, 1);
SetGlobalIllumination(GlobalIllumination.Medium);
SetBloom(0.6f);
CreatePointLight(new Vector3(5, 2.9f, 4), new Color(255, 235, 204), 7, range: 16, castsShadows: true);

// -- The walker, a capsule as tall as a person in the middle of the hall, seen from its eyes

var walker = CreatePhysicsCharacter(Level.Home, radius: 0.35f, height: 1.8f, mass: 70);
SetPhysicsCharacterStepHeight(walker, 0.3f);
var (yaw, pitch) = (0f, 0f);

while (!WindowShouldClose())
{
    // A model loaded under the level's name by a command is the level from then on.
    if (GetEditMeshByName(Level.Name) is { IsValid: true } named && named != level)
        (level, built) = (named, -1);

    // The level's model and collider made again where it changed, by the window, the commands or
    // the program, the collider whole, since a static body's shape is fixed once it is made.
    if (GetEditMeshVersion(level) != built)
    {
        if (model is null) model = LoadModelFromEditMesh(level);
        else UpdateModelFromEditMesh(model, level);
        Level.Glow(model, level);
        if (collider.IsValid) DestroyPhysicsBody(collider);
        collider = CreatePhysicsStaticModel(model, Vector3.Zero);
        Play.Colliders++;
        built = GetEditMeshVersion(level);
    }
    Play.Level = level;

    var dt = GetFrameTime();
    var typing = ImGui.GetIO().WantTextInput;
    if (!typing && IsKeyPressed(Key.Tab)) Play.Editing = !Play.Editing;
    if (!typing && IsKeyPressed(Key.F5) && SaveEditMesh(level, Level.File))
        Console.WriteLine($"[blockout] saved the level to {Path.Combine(AppContext.BaseDirectory, Level.File)}");

    // The mouse looks about while the modeling window is shut, its pointer hidden, as Manor's does,
    // and while it is open the right button held looks, the pointer left to the window. The arrows
    // turn the view too, as a drive's held keys do.
    if (!Play.Editing && !IsCursorHidden()) DisableCursor();
    if (Play.Editing && IsCursorHidden()) EnableCursor();
    var look = !Play.Editing || IsMouseButtonDown(MouseButton.Right) && !ImGui.GetIO().WantCaptureMouse ? GetMouseDelta() * 0.004f : Vector2.Zero;
    if (!typing)
    {
        look.X += ((IsKeyDown(Key.Right) ? 1 : 0) - (IsKeyDown(Key.Left) ? 1 : 0)) * 2 * dt;
        look.Y += ((IsKeyDown(Key.Down) ? 1 : 0) - (IsKeyDown(Key.Up) ? 1 : 0)) * 2 * dt;
    }
    yaw -= look.X;
    pitch = Math.Clamp(pitch - look.Y, -1.4f, 1.4f);

    // Walking along the view by the keys, or where the autopilot steers, facing the way it walks so
    // the view and a capture see where it goes.
    var feet = GetPhysicsBodyPosition(walker) - new Vector3(0, 0.9f, 0);
    var forward = new Vector3(-MathF.Sin(yaw), 0, -MathF.Cos(yaw));
    var right = new Vector3(-forward.Z, 0, forward.X);
    var move = Vector3.Zero;
    if (Autopilot.Steer(feet, dt) is { } toward)
    {
        move = toward;
        (yaw, pitch) = (MathF.Atan2(-toward.X, -toward.Z), 0);
    }
    else if (!typing)
        move = forward * ((IsKeyDown(Key.W) ? 1 : 0) - (IsKeyDown(Key.S) ? 1 : 0)) + right * ((IsKeyDown(Key.D) ? 1 : 0) - (IsKeyDown(Key.A) ? 1 : 0));
    MovePhysicsCharacter(walker, move == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(move) * 4);
    if (!typing && IsKeyPressed(Key.Space) && IsPhysicsCharacterGrounded(walker)) JumpPhysicsCharacter(walker, 4.5f);
    (Play.Feet, Play.Grounded) = (feet, IsPhysicsCharacterGrounded(walker));

    var eye = feet + new Vector3(0, 1.6f, 0);
    var camera = new Camera3D(eye, eye + new Vector3(MathF.Cos(pitch) * forward.X, MathF.Sin(pitch), MathF.Cos(pitch) * forward.Z), Vector3.UnitY, 70);

    BeginDrawing();
    ClearBackground(new Color(140, 179, 230));
    BeginMode3D(camera);
    DrawSkybox();
    DrawModel(model!, Vector3.Zero, 1, Color.White);
    EndMode3D();

    // The words and the modeling window are the interface F1 hides, the level drawing on.
    BeginUi();
    DrawText("WASD to walk, the mouse or the arrows to look, Space to jump", 12, 10, 20, Color.White);
    DrawText("Tab for the modeling window on the level, F5 to save it", 12, 34, 20, Color.White);
    if (Play.Editing) DrawModelingWindow(level);
    EndUi();
    EndDrawing();
}

CloseWindow();

/// <summary>The level's file and name, where the walker starts, and how many lines its recipe has.</summary>
internal static class Level
{
    /// <summary>The recipe's file, beside the program.</summary>
    public const string File = "resources/levels/blockout.model";

    /// <summary>The name the model commands reach the level by.</summary>
    public const string Name = "level";

    /// <summary>Where the walker's feet start, in the middle of the hall.</summary>
    public static readonly Vector3 Home = new(5, 0.2f, 4);

    /// <summary>
    /// Draws each slot that gives off light at a ten-thousandth of the recipe's number, since the
    /// recipe is BevyCSharp's Blockout's, whose emission is a luminance in its units, which this
    /// engine's linear light reads ten thousand times as bright, its lamp of 40,000 a light of 4.
    /// </summary>
    /// <remarks>Each slot's material is made again from its look first, so a look changed in the window is scaled once.</remarks>
    public static void Glow(Model model, EditMesh mesh)
    {
        var slots = GetEditMeshSlots(mesh);
        for (int slot = 0; slot < slots.Length && slot < model.Materials.Length; slot++)
            if (LoadMaterialFromEditMeshSlot(mesh, slots[slot]) is { EmissiveIntensity: > 0 } material && material.Emissive != Color.Black)
                model.Materials[slot] = material with { EmissiveIntensity = material.EmissiveIntensity / 10_000 };
    }

    /// <summary>The recipe's lines of operations, its comments and blank lines left out.</summary>
    public static int Lines(EditMesh mesh) =>
        GetEditMeshRecipe(mesh).Split('\n').Count(line => line.Trim() is { Length: > 0 } text && !text.StartsWith('#'));
}

/// <summary>What the frame left for the commands to say, the walker, the window and the level.</summary>
internal static class Play
{
    public static Vector3 Feet;
    public static bool Grounded, Editing;
    public static EditMesh Level;

    /// <summary>How many times the collider was made, the first included.</summary>
    public static int Colliders;
}

/// <summary>Walks the walker along a route of points, for the drive and the soak, as a player would walk it.</summary>
/// <remarks>
/// A route is points on the floors, reached in turn, each within a third of a meter along the ground,
/// the stairs climbed by walking at their top as a player climbs them. Where the walker comes no
/// nearer its point for three seconds it is stuck, which the status says, so a drive fails with
/// where rather than waiting on a walk that will never end. The routes are BevyCSharp's Blockout's.
/// </remarks>
internal static class Autopilot
{
    // The foot of the stairs, their top on the floor over the hall, and the middle of that room.
    private static readonly Vector3[] Up = [new(2.5f, 0, 1), new(0.8f, 0, 1), new(0.8f, 0, 6.8f), new(5, 0, 6.8f), new(5, 0, 4.5f)];

    // Back down the stairs from the room over the hall, to the middle of the hall.
    private static readonly Vector3[] Down = [new(5, 0, 6.8f), new(0.8f, 0, 6.8f), new(0.8f, 0, 1), new(2.5f, 0, 1), Level.Home with { Y = 0 }];

    // Through the doors and the corridor to the second room's window and back.
    private static readonly Vector3[] Across =
        [Level.Home with { Y = 0 }, new(11, 0, 4), new(19, 0, 4), new(19, 0, 1.5f), new(19, 0, 4), new(11, 0, 4), Level.Home with { Y = 0 }];

    // Two meters out from the middle of the hall and back, which a soak's round walks.
    private static readonly Vector3[] Loop = [new(7, 0, 4), Level.Home with { Y = 0 }];

    /// <summary>The routes by name, each walked from where the one before it ends, the tour and the loop from the middle of the hall.</summary>
    public static readonly IReadOnlyDictionary<string, Vector3[]> Routes = new Dictionary<string, Vector3[]>(StringComparer.Ordinal)
    {
        ["upstairs"] = Up,
        ["downstairs"] = Down,
        ["loop"] = Loop,
        ["tour"] = [.. Across, .. Up, .. Down],
    };

    private static Vector3[] _route = [];
    private static int _next;
    private static float _nearest, _since;

    /// <summary>The route being walked, or none.</summary>
    public static string? Route { get; private set; }

    /// <summary>Whether the route was walked to its end.</summary>
    public static bool Done { get; private set; }

    /// <summary>Whether the walker came no nearer its point for three seconds.</summary>
    public static bool Stuck { get; private set; }

    /// <summary>Which point of the route is next, from one, and how many it has.</summary>
    public static (int Next, int Of) Progress => (Math.Min(_next + 1, _route.Length), _route.Length);

    /// <summary>Starts walking a route from its first point, or stops with none.</summary>
    public static void Walk(string? route)
    {
        Route = route;
        _route = route is null ? [] : Routes[route];
        (_next, _nearest, _since, Done, Stuck) = (0, float.MaxValue, 0, false, false);
    }

    /// <summary>The way to walk from where the walker's feet are, of length one, or none where no route is being walked.</summary>
    public static Vector3? Steer(Vector3 at, float seconds)
    {
        if (Route is null || Done || Stuck) return null;
        var toward = _route[_next] - at with { Y = 0 };
        var far = toward.Length();
        if (far < 0.3f)
        {
            if (++_next == _route.Length)
            {
                Done = true;
                return null;
            }
            (_nearest, _since) = (float.MaxValue, 0);
            return Steer(at, seconds);
        }
        // Nearer by a centimeter starts the three seconds again.
        if (far < _nearest - 0.01f) (_nearest, _since) = (far, 0);
        else if ((_since += seconds) > 3) Stuck = true;
        return toward / far;
    }
}

/// <summary>What the drive and the soak ask the game and tell it.</summary>
internal static class BlockoutCommands
{
    [Command("blockout.status", "Says where the walker stands, its floor, the lighting, the modeling window, the autopilot and the level")]
    internal static string Status()
    {
        var (next, of) = Autopilot.Progress;
        var autopilot = Autopilot.Route is null ? "off"
            : Autopilot.Stuck ? $"{Autopilot.Route} stuck before point {next} of {of}"
            : Autopilot.Done ? $"{Autopilot.Route} done"
            : $"{Autopilot.Route} at point {next} of {of}";
        var info = GetEditMeshInfo(Play.Level);
        var at = Play.Feet;
        return string.Join("\n",
            FormattableString.Invariant($"at {at.X:0.00} {at.Y:0.00} {at.Z:0.00}, {(Play.Grounded ? "on the ground" : "in the air")}, on the {(at.Y > 3 ? "upper" : "ground")} floor"),
            "lighting the sun, the day sky and global illumination",
            $"modeling window {(Play.Editing ? "open" : "closed")}",
            $"autopilot {autopilot}",
            $"level {info.VertexCount} vertices, {info.EdgeCount} edges, {info.FaceCount} faces, collider made {Play.Colliders} times");
    }

    [Command("blockout.autopilot", "Walks a route by itself: blockout.autopilot upstairs|downstairs|loop|tour|off")]
    internal static string Pilot(string route)
    {
        if (route == "off")
        {
            Autopilot.Walk(null);
            return "autopilot off";
        }
        if (!Autopilot.Routes.ContainsKey(route))
            return $"There is no route called {route}, only {string.Join(", ", Autopilot.Routes.Keys)} and off.";
        Autopilot.Walk(route);
        return $"autopilot walking {route}, {Autopilot.Progress.Of} points";
    }
}
