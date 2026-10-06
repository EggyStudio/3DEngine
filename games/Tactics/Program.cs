// Tactics, a strategy board seen from above. Two sides of soldiers, archers and a knight take turns
// on a field of grass, woods, hills and water, each seeing only what its units can. A unit is picked
// with the mouse, by a ray from the camera through the pointer to the tile it meets, shows where it
// can walk this turn, walks there by the cheapest way, and strikes an enemy in range. Several are
// picked at once with a box dragged round them, or a shift click, and ordered together. The other
// side is played by the computer, every panel is ImGui, and a match is saved to a file and taken up
// again. Written against the engine's package, as a game outside this repository would be.
using System.Numerics;
using Engine;
using ImGuiNET;
using static Engine.Engine3D;

InitWindow(1280, 720, "Tactics");
InitAudioDevice();
SetTargetFPS(60);
// Escape lets go of the units picked, so it does not close the window.
SetExitKey(Key.Null);

const float Tile = 2;
const string SaveFile = "tactics-save.json";

// -- The pieces: tiles, trees, and a model for each kind of unit

var block = LoadModelFromMesh(GenMeshCube(Tile * 0.96f, 0.4f, Tile * 0.96f));
var trunk = LoadModelFromMesh(GenMeshCylinder(0.12f, 0.5f, 8));
var crown = LoadModelFromMesh(GenMeshCone(0.55f, 1.3f, 10));
var stand = LoadModelFromMesh(GenMeshCylinder(0.55f, 0.15f, 20));
var bodies = new Dictionary<Kind, Model>
{
    [Kind.Soldier] = LoadModelFromMesh(GenMeshCube(0.55f, 0.9f, 0.4f)),
    [Kind.Archer] = LoadModelFromMesh(GenMeshCone(0.35f, 1.1f, 12)),
    [Kind.Knight] = LoadModelFromMesh(GenMeshCylinder(0.38f, 1.25f, 14)),
};
var head = LoadModelFromMesh(GenMeshSphere(0.2f, 12, 12));
Color TerrainColor(Terrain t) => t switch
{
    Terrain.Grass => new Color(96, 150, 72), Terrain.Forest => new Color(58, 104, 52),
    Terrain.Hill => new Color(150, 132, 92), _ => new Color(60, 110, 170),
};
Color SideColor(Side s) => s == Side.Blue ? new Color(70, 110, 220) : new Color(210, 60, 50);

CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.5f)), new Color(255, 246, 230), 2.2f, castsShadows: true);
SetAmbientLight(new Color(160, 180, 220), 0.35f);
SetShadowDistance(60);
SetBloom(0.3f);
var sparks = CreateParticleEmitter(Vector3.Zero, ParticleEmitter.Default with
{
    MaxParticles = 300, Emitting = false, Life = 0.6f, Velocity = new Vector3(0, 3, 0), Spread = 70, SpeedVariation = 0.5f,
    Gravity = new Vector3(0, -8, 0), StartSize = 0.18f, EndSize = 0.02f,
    StartColor = new Color(255, 220, 120), EndColor = new Color(255, 80, 20, 0), Intensity = 4,
});

var (select, step, hit, fall, win) = (LoadSound("resources/select.wav"), LoadSound("resources/step.wav"),
    LoadSound("resources/hit.wav"), LoadSound("resources/fall.wav"), LoadSound("resources/win.wav"));

// -- The match, the camera over it, and what is under way

var board = Board.New(7);
AddState(Screen.Title);
var (yaw, distance) = (0.35f, 24f);
var focus = new Vector3((Board.Width - 1) * Tile / 2, 0, (Board.Depth - 1) * Tile / 2);
// The units picked, and where the one picked alone can walk this turn.
var chosen = new List<Unit>();
Dictionary<(int X, int Z), (int Cost, (int X, int Z) From)>? reach = null;
(Unit Unit, List<(int X, int Z)> Path, float Along, Unit? Strike)? walking = null;
// The walks and strikes ordered of several units at once, played one after another.
var orders = new Queue<(Unit Unit, List<(int X, int Z)> Path, Unit? Strike)>();
// Where a drag with the left button started, for the box it draws, and where a right press did.
Vector2? boxFrom = null;
var rightFrom = Vector2.Zero;
var thinking = 0f;
var damages = new List<(Vector3 At, int Amount, float Age)>();
var message = "";

Vector3 Center(int x, int z) => new(x * Tile, Height(x, z), z * Tile);
float Height(int x, int z) => board.Tiles[x, z] switch { Terrain.Hill => 0.4f, Terrain.Water => -0.25f, _ => 0 };

void Forget()
{
    chosen.Clear();
    orders.Clear();
    (reach, walking, boxFrom) = (null, null, null);
}

void NewMatch(int seed)
{
    board = Board.New(seed);
    Forget();
    damages.Clear();
}

void Pick(IEnumerable<Unit> units, bool adding)
{
    if (!adding) chosen.Clear();
    foreach (var unit in units)
        if (!chosen.Remove(unit) || !adding) chosen.Add(unit);
    reach = chosen is [{ Moved: false } alone] ? board.Reach(alone) : null;
    if (chosen.Count > 0) PlaySound(select);
}

bool Controls(Side side) => side == Side.Blue && !TacticsCommands.Autopilot;

void Strike(Unit attacker, Unit target)
{
    var amount = board.Damage(attacker, target);
    board.Strike(attacker, target);
    var at = Center(target.X, target.Z) + new Vector3(0, 1, 0);
    SetParticleEmitterPosition(sparks, at);
    EmitParticles(sparks, 40);
    damages.Add((at, amount, 0));
    PlaySound(target.Alive ? hit : fall);
    if (board.Winner is { } winner)
    {
        PlaySound(win);
        message = $"{winner} wins in round {board.Round}.";
        SetState(Screen.Over);
    }
}

while (!WindowShouldClose() && !TacticsCommands.Quit)
{
    var dt = GetFrameTime();
    var screen = GetState<Screen>();
    var camera = new Camera3D(focus + new Vector3(MathF.Sin(yaw), 1.25f, MathF.Cos(yaw)) * distance, focus, Vector3.UnitY, 45);

    if (TacticsCommands.Request is { } request)
    {
        TacticsCommands.Request = null;
        switch (request)
        {
            case "save": SaveFileText(SaveFile, board.Save()); break;
            case "load" when FileExists(SaveFile) && LoadFileText(SaveFile) is { } text && Board.Load(text) is { } loaded:
                board = loaded; Forget(); SetState(Screen.Play); break;
            case var r when r.StartsWith("new "): NewMatch(int.Parse(r[4..])); SetState(Screen.Play); break;
        }
    }
    TacticsCommands.Units = () => string.Join("\n", board.Units.Where(u => u.Alive).Select(u => $"{u.Side} {u.Kind} {u.X} {u.Z} {u.Health}"));
    TacticsCommands.Where = (x, z) => GetWorldToScreen(Center(x, z) + new Vector3(0, 0.3f, 0), camera);

    // -- The camera turns with Q and E or a right drag, and the wheel brings it nearer.
    if (IsKeyDown(Key.Q)) yaw -= dt * 1.5f;
    if (IsKeyDown(Key.E)) yaw += dt * 1.5f;
    if (IsMouseButtonDown(MouseButton.Right) && !ImGui.GetIO().WantCaptureMouse) yaw -= GetMouseDelta().X * 0.006f;
    if (!ImGui.GetIO().WantCaptureMouse) distance = Math.Clamp(distance - GetMouseWheelMove() * 1.5f, 10, 40);

    // What the blue side sees this frame. The rest of the board is in fog, and the units there are
    // neither drawn nor picked.
    var seen = board.Seen(Side.Blue);
    bool Visible(Unit unit) => unit.Side == Side.Blue || seen.Contains((unit.X, unit.Z));

    // -- The tile under the pointer, the nearest of those the ray from the camera meets.
    (int X, int Z)? hovered = null;
    if (screen == Screen.Play && !ImGui.GetIO().WantCaptureMouse)
    {
        // Each tile by its top, and each unit by a box its own height, so a slanting ray meets the
        // unit it passes over rather than the tile in front of it.
        var ray = GetScreenToWorldRay(GetMousePosition(), camera);
        var nearest = float.MaxValue;
        for (int x = 0; x < Board.Width; x++)
            for (int z = 0; z < Board.Depth; z++)
            {
                var c = Center(x, z);
                var top = new BoundingBox(c - new Vector3(Tile / 2, 0.2f, Tile / 2), c + new Vector3(Tile / 2, 0.02f, Tile / 2));
                if (GetRayCollisionBox(ray, top) is { Hit: true } meets && meets.Distance < nearest)
                    (nearest, hovered) = (meets.Distance, (x, z));
            }
        foreach (var unit in board.Units.Where(u => u.Alive && Visible(u)))
        {
            var c = Center(unit.X, unit.Z);
            var body = new BoundingBox(c - new Vector3(0.5f, 0, 0.5f), c + new Vector3(0.5f, 1.7f, 0.5f));
            if (GetRayCollisionBox(ray, body) is { Hit: true } meets && meets.Distance < nearest)
                (nearest, hovered) = (meets.Distance, (unit.X, unit.Z));
        }
    }

    var endAsked = TacticsCommands.EndTurnAsked;
    TacticsCommands.EndTurnAsked = false;

    // -- A unit walking along its path, a tile at a time, then striking if it meant to, and the next
    // order of a group begun once it has.
    if (walking is null && orders.TryDequeue(out var order))
    {
        if (order.Path.Count > 0) walking = (order.Unit, order.Path, 0, order.Strike);
        else if (order.Strike is { } target && board.CanAttack(order.Unit, target)) Strike(order.Unit, target);
    }
    if (walking is { } walk)
    {
        var along = walk.Along + dt * 6;
        var before = (int)walk.Along;
        if ((int)along > before && before < walk.Path.Count && Visible(walk.Unit)) PlaySound(step);
        if (along >= walk.Path.Count)
        {
            if (walk.Path.Count > 0) board.MoveTo(walk.Unit, walk.Path[^1]);
            if (walk.Strike is { } target && board.CanAttack(walk.Unit, target)) Strike(walk.Unit, target);
            walking = null;
            if (chosen is [var alone] && alone == walk.Unit) reach = null;
        }
        else walking = walk with { Along = along };
    }
    else if (screen == Screen.Play && Controls(board.Turn) && orders.Count == 0)
    {
        // -- The player's turn: pick units, a tile to walk to, an enemy to strike. A press and
        // release in one place is a click, and a drag between them a box round the units to pick.
        var mouse = GetMousePosition();
        var shift = IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift);
        if (IsMouseButtonPressed(MouseButton.Left) && !ImGui.GetIO().WantCaptureMouse) boxFrom = mouse;
        if (IsMouseButtonReleased(MouseButton.Left) && boxFrom is { } from)
        {
            boxFrom = null;
            if (Vector2.Distance(from, mouse) > 8)
            {
                var (min, max) = (Vector2.Min(from, mouse), Vector2.Max(from, mouse));
                // With shift the box adds to those picked already, and takes none away.
                var boxed = board.Units.Where(u => u.Alive && u.Side == board.Turn && !u.Acted).Where(u =>
                {
                    var p = GetWorldToScreen(Center(u.X, u.Z) + new Vector3(0, 0.8f, 0), camera);
                    return p.X >= min.X && p.Y >= min.Y && p.X <= max.X && p.Y <= max.Y;
                });
                Pick(shift ? chosen.Union(boxed).ToList() : boxed, false);
            }
            else if (hovered is { } tile) Order(tile, shift);
        }
        if (IsMouseButtonPressed(MouseButton.Right)) rightFrom = GetMousePosition();
        if ((IsMouseButtonReleased(MouseButton.Right) && Vector2.Distance(rightFrom, mouse) < 4) || IsKeyPressed(Key.Escape))
            Pick([], false);
        if (IsKeyPressed(Key.Enter) || endAsked) EndTurn();
    }
    else if (screen == Screen.Play)
    {
        // -- The computer's turn: one unit at a time, a moment apart.
        thinking += dt;
        if (thinking > 0.25f)
        {
            thinking = 0;
            var next = board.Units.FirstOrDefault(u => u.Alive && u.Side == board.Turn && !u.Acted && !u.Moved);
            if (next is null) EndTurn();
            else
            {
                var (path, target) = board.Plan(next);
                next.Moved = true;
                if (path.Count == 0 && target is not null && board.CanAttack(next, target)) Strike(next, target);
                else if (path.Count > 0) walking = (next, path, 0, target);
            }
        }
    }

    // What a click on a tile asks of the units picked: its own unit picked, or added with shift; an
    // enemy struck by each that can, those out of range walking near enough first; or an empty tile
    // walked to, by the one picked alone, or as near as each of several can come.
    void Order((int X, int Z) tile, bool adding)
    {
        var there = board.UnitAt(tile.X, tile.Z) is { } u && Visible(u) ? u : null;
        if (there is { } own && own.Side == board.Turn)
        {
            if (!own.Acted) Pick([own], adding);
            return;
        }
        var ready = chosen.Where(u => u.Alive && !u.Acted).ToList();
        var claimed = new HashSet<(int X, int Z)>();
        if (there is { } enemy)
        {
            foreach (var unit in ready)
                if (board.CanAttack(unit, enemy)) orders.Enqueue((unit, [], enemy));
                else if (!unit.Moved && board.Approach(unit, tile, unit.Range, claimed) is { Count: > 0 } path)
                    orders.Enqueue((unit, path, enemy));
            if (orders.Count > 0) Pick([], false);
        }
        else if (chosen is [{ Moved: false } alone] && reach is { } can && can.ContainsKey(tile))
        {
            walking = (alone, board.PathTo(alone, tile), 0, null);
            reach = null;
        }
        else if (chosen.Count > 1)
        {
            foreach (var unit in ready.Where(u => !u.Moved))
                if (board.Approach(unit, tile, 0, claimed) is { Count: > 0 } path) orders.Enqueue((unit, path, null));
        }
    }

    void EndTurn()
    {
        board.EndTurn();
        Forget();
        // A match that runs too long is called for the side with more health left.
        if (board.Round > 60 && board.Winner is null)
        {
            var blue = board.Units.Where(u => u.Side == Side.Blue).Sum(u => u.Health);
            var red = board.Units.Where(u => u.Side == Side.Red).Sum(u => u.Health);
            message = $"{(blue >= red ? Side.Blue : Side.Red)} wins on health after 60 rounds.";
            SetState(Screen.Over);
        }
    }

    for (int i = damages.Count - 1; i >= 0; i--)
        if ((damages[i] = damages[i] with { Age = damages[i].Age + dt }).Age > 1.2f) damages.RemoveAt(i);

    TacticsCommands.Status = $"{screen} round {board.Round} turn {board.Turn} " +
        $"blue {board.Units.Count(u => u.Alive && u.Side == Side.Blue)}/{board.Units.Where(u => u.Side == Side.Blue).Sum(u => u.Health)} " +
        $"red {board.Units.Count(u => u.Alive && u.Side == Side.Red)}/{board.Units.Where(u => u.Side == Side.Red).Sum(u => u.Health)} " +
        $"selected {chosen switch { [] => "none", [var one] => $"{one.Kind} {one.X},{one.Z}", _ => $"{chosen.Count} units" }} " +
        $"ready {board.Units.Count(u => u.Alive && u.Side == board.Turn && !u.Moved && !u.Acted)} " +
        $"in sight {board.Units.Count(u => u.Alive && u.Side == Side.Red && Visible(u))} winner {board.Winner?.ToString() ?? "none"}";

    // -- Drawing
    BeginDrawing();
    ClearBackground(new Color(30, 36, 48));
    BeginMode3D(camera);
    for (int x = 0; x < Board.Width; x++)
        for (int z = 0; z < Board.Depth; z++)
        {
            var c = Center(x, z);
            // A tile in fog keeps its terrain, which a map shows, darkened.
            var dim = seen.Contains((x, z)) ? 0 : -0.6f;
            DrawModel(block, c - new Vector3(0, 0.2f, 0), 1, ColorBrightness(TerrainColor(board.Tiles[x, z]), dim));
            if (board.Tiles[x, z] == Terrain.Forest)
                foreach (var (dx, dz) in new[] { (-0.45f, -0.4f), (0.45f, 0.3f) })
                {
                    DrawModel(trunk, c + new Vector3(dx, 0, dz), 1, ColorBrightness(new Color(100, 70, 40), dim));
                    DrawModel(crown, c + new Vector3(dx, 0.4f, dz), 1, ColorBrightness(new Color(40, 90, 45), dim));
                }
            if (reach is { } can && can.ContainsKey((x, z)))
                DrawCube(c + new Vector3(0, 0.03f, 0), Tile * 0.9f, 0.04f, Tile * 0.9f, new Color(255, 255, 255, 70));
        }
    if (hovered is { } h) DrawCubeWires(Center(h.X, h.Z) + new Vector3(0, 0.1f, 0), Tile * 0.96f, 0.2f, Tile * 0.96f, Color.Yellow);
    if (chosen is [var leader] && walking is null && reach is { } paths && hovered is { } goal && paths.ContainsKey(goal))
    {
        var path = board.PathTo(leader, goal);
        var from = Center(leader.X, leader.Z) + new Vector3(0, 0.15f, 0);
        foreach (var t in path)
        {
            var to = Center(t.X, t.Z) + new Vector3(0, 0.15f, 0);
            DrawLine3D(from, to, Color.Yellow);
            from = to;
        }
    }
    foreach (var unit in board.Units.Where(u => u.Alive))
    {
        var at = Center(unit.X, unit.Z);
        var shown = Visible(unit);
        if (walking is { } w && w.Unit == unit && w.Path.Count > 0)
        {
            var i = Math.Min((int)w.Along, w.Path.Count - 1);
            var a = i == 0 ? (unit.X, unit.Z) : w.Path[i - 1];
            var b = w.Path[i];
            at = Vector3.Lerp(Center(a.X, a.Z), Center(b.X, b.Z), w.Along - i) + new Vector3(0, 0.15f * MathF.Abs(MathF.Sin((w.Along - i) * MathF.PI)), 0);
            // A unit walking is seen while either tile of its step is.
            shown = unit.Side == Side.Blue || seen.Contains(a) || seen.Contains(b);
        }
        if (!shown) continue;
        var color = SideColor(unit.Side);
        if (unit.Acted || (unit.Moved && unit.Side == board.Turn)) color = ColorBrightness(color, -0.35f);
        DrawModel(stand, at, 1, chosen.Contains(unit) ? Color.Yellow : new Color(40, 40, 46));
        var height = unit.Kind == Kind.Knight ? 1.25f : unit.Kind == Kind.Archer ? 1.1f : 0.9f;
        DrawModel(bodies[unit.Kind], at + new Vector3(0, unit.Kind == Kind.Soldier ? 0.6f : 0.15f, 0), 1, color);
        DrawModel(head, at + new Vector3(0, 0.15f + height + 0.12f, 0), 1, new Color(230, 200, 170));
    }
    EndMode3D();

    // Each unit's health over it, and the damage of a strike rising from where it landed.
    foreach (var unit in board.Units.Where(u => u.Alive && Visible(u) && !(walking is { } w && w.Unit == u)))
    {
        var p = GetWorldToScreen(Center(unit.X, unit.Z) + new Vector3(0, 2.0f, 0), camera);
        DrawRectangle((int)p.X - 20, (int)p.Y, 40, 5, new Color(0, 0, 0, 160));
        DrawRectangle((int)p.X - 20, (int)p.Y, 40 * unit.Health / unit.MaxHealth, 5, SideColor(unit.Side));
    }
    foreach (var (at, amount, age) in damages)
    {
        var p = GetWorldToScreen(at + new Vector3(0, age, 0), camera);
        DrawText($"-{amount}", (int)p.X - 8, (int)p.Y, 24, new Color(255, 230, 120, (byte)(255 * (1 - age / 1.2f))));
    }

    if (boxFrom is { } corner && Vector2.Distance(corner, GetMousePosition()) > 8)
    {
        var (min, max) = (Vector2.Min(corner, GetMousePosition()), Vector2.Max(corner, GetMousePosition()));
        DrawRectangle((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y), new Color(255, 230, 120, 40));
        DrawRectangleLines((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y), new Color(255, 230, 120));
    }

    Panels(screen, hovered, Visible);
    EndDrawing();
}

CloseAudioDevice();
CloseWindow();

void Panels(Screen screen, (int X, int Z)? hovered, Func<Unit, bool> visible)
{
    var size = new Vector2(GetScreenWidth(), GetScreenHeight());
    if (screen != Screen.Play)
    {
        ImGui.SetNextWindowPos(size / 2, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.Begin("##" + screen, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.Text(screen == Screen.Title ? "TACTICS" : message);
        if (screen == Screen.Title)
        {
            ImGui.Text("Lead the blue side. Pick a unit, a tile to walk to, an enemy to strike.");
            ImGui.Text("Drag a box round several, or shift click them, to order them together.");
        }
        ImGui.Separator();
        if (ImGui.Button("New match", new Vector2(240, 0))) { NewMatch(Environment.TickCount % 1000); SetState(Screen.Play); }
        if (FileExists(SaveFile) && ImGui.Button("Continue the saved match", new Vector2(240, 0))) TacticsCommands.Request = "load";
        if (ImGui.Button("Quit", new Vector2(240, 0))) TacticsCommands.Quit = true;
        ImGui.End();
        return;
    }

    ImGui.SetNextWindowPos(new Vector2(12, 12));
    ImGui.Begin("Match", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
    ImGui.Text($"Round {board.Round}, {board.Turn} to move");
    var mine = Controls(board.Turn) && walking is null && orders.Count == 0;
    if (!mine) ImGui.BeginDisabled();
    if (ImGui.Button("End turn (Enter)", new Vector2(200, 0))) TacticsCommands.EndTurnAsked = true;
    if (!mine) ImGui.EndDisabled();
    if (ImGui.Button("Save", new Vector2(98, 0))) TacticsCommands.Request = "save";
    ImGui.SameLine();
    if (ImGui.Button("Load", new Vector2(98, 0))) TacticsCommands.Request = "load";
    if (ImGui.Button("Leave to the title", new Vector2(200, 0))) SetState(Screen.Title);
    ImGui.End();

    // Every unit of the blue side, picked from the list as from the board, and the enemies in sight.
    ImGui.SetNextWindowPos(new Vector2(12, 150), ImGuiCond.FirstUseEver);
    ImGui.SetNextWindowSize(new Vector2(250, 0), ImGuiCond.FirstUseEver);
    ImGui.Begin("Army", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse);
    var army = board.Units.Where(u => u.Side == Side.Blue && u.Alive).ToList();
    if (!mine) ImGui.BeginDisabled();
    if (ImGui.Button("Pick every unit ready", new Vector2(230, 0)))
        Pick(army.Where(u => !u.Acted && !u.Moved), false);
    if (!mine) ImGui.EndDisabled();
    foreach (var unit in army)
    {
        ImGui.PushID(board.Units.IndexOf(unit));
        var state = unit.Acted ? "done" : unit.Moved ? "walked" : "ready";
        if (ImGui.Selectable($"{unit.Kind,-8} {unit.X,2},{unit.Z,-2} {state}", chosen.Contains(unit)) && mine && !unit.Acted)
            Pick([unit], ImGui.GetIO().KeyShift || ImGui.GetIO().KeyCtrl);
        ImGui.ProgressBar(unit.Health / (float)unit.MaxHealth, new Vector2(230, 6), "");
        ImGui.PopID();
    }
    var sighted = board.Units.Where(u => u.Side == Side.Red && u.Alive && visible(u)).ToList();
    ImGui.Separator();
    ImGui.TextColored(new Vector4(1, 0.4f, 0.35f, 1), sighted.Count == 0 ? "No enemy in sight." : $"In sight, {sighted.Count} of the enemy:");
    foreach (var enemy in sighted) ImGui.Text($"{enemy.Kind,-8} {enemy.X,2},{enemy.Z,-2} {enemy.Health} of {enemy.MaxHealth}");
    if (chosen is [var unitPicked])
    {
        ImGui.Separator();
        ImGui.Text($"Strikes for {unitPicked.Attack} at {unitPicked.Range} {(unitPicked.Range == 1 ? "tile" : "tiles")}, walks {unitPicked.Move}, sees {unitPicked.Sight}");
    }
    ImGui.End();

    if (hovered is { } t)
    {
        ImGui.SetNextWindowPos(new Vector2(size.X - 12, size.Y - 12), ImGuiCond.Always, new Vector2(1, 1));
        ImGui.Begin("Tile", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs);
        ImGui.Text($"{board.Tiles[t.X, t.Z]} at {t.X}, {t.Z}");
        ImGui.Text(board.Tiles[t.X, t.Z] switch
        {
            Terrain.Grass => "Costs one to cross.",
            Terrain.Water => "Cannot be crossed.",
            Terrain.Forest => "Costs two to cross, shelters a unit from a point of each strike and hides what is past it.",
            _ => "Costs two to cross, shelters a unit from a point of each strike and sees one tile further.",
        });
        if (board.UnitAt(t.X, t.Z) is { } there && visible(there)) ImGui.Text($"{there.Side} {there.Kind}, {there.Health} of {there.MaxHealth}");
        ImGui.End();
    }
}

public enum Screen { Title, Play, Over }

public static class TacticsCommands
{
    internal static string Status = "";
    internal static bool Autopilot, Quit, EndTurnAsked;
    internal static string? Request;
    internal static Func<int, int, Vector2>? Where;

    [Command("tactics.status", "The screen, the round, whose turn it is, each side's units and health, the unit picked and the winner")]
    internal static string Report() => Status;

    [Command("tactics.autopilot", "Lets the computer play the blue side too, for a match that plays itself: tactics.autopilot <on>")]
    internal static string Pilot(bool on)
    {
        Autopilot = on;
        return on ? "the computer plays both sides" : "blue is the player's again";
    }

    [Command("tactics.new", "Starts a match on the map a seed makes: tactics.new <seed>")]
    internal static string New(int seed)
    {
        Request = $"new {seed}";
        return $"a match on map {seed}";
    }

    [Command("tactics.save", "Writes the match to tactics-save.json")]
    internal static string Save()
    {
        Request = "save";
        return "saving";
    }

    [Command("tactics.load", "Takes up the match tactics-save.json holds")]
    internal static string Load()
    {
        Request = "load";
        return "loading";
    }

    [Command("tactics.end", "Ends the player's turn, as the button does")]
    internal static string End()
    {
        EndTurnAsked = true;
        return "ending the turn";
    }

    [Command("tactics.units", "Every unit alive: its side, kind, tile and health, a line each")]
    internal static string List() => Units?.Invoke() ?? "";

    internal static Func<string>? Units;

    [Command("tactics.where", "Where a tile's middle is on the screen, in pixels, to click it: tactics.where <x> <z>")]
    internal static string Locate(int x, int z) => Where?.Invoke(x, z) is { } p ? $"{p.X:0} {p.Y:0}" : "not drawn yet";
}
