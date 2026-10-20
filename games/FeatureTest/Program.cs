// The feature test, every area of the engine on one map, for a tester to try a build with and for
// the workflow to walk. A hub in the middle has a road and a signpost to each zone, and each zone
// exercises its area's calls and says what it found in a status line the console reads
// (`./e3d command feature.status`). The tester walks between them as a capsule on the character
// controller. Written against the engine's package, as a game outside this repository would be.
using System.Numerics;
using Engine;
using FeatureTest;
using static Engine.E3D;

Crash.Install();
InitWindow(1280, 720, "Feature test");
SetTargetFPS(60);
if (Crash.Last() is { } crashed) TraceLog(LogLevel.Info, $"FeatureTest: the last crash is written in {crashed} beside the program.");

// The ground under the whole map, and light from a low sun.
CreatePhysicsStaticBox(new Vector3(0, -0.5f, 0), new Vector3(400, 1, 400));
SetAmbientLight(new Color(160, 175, 210), 0.5f);
var sun = CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), new Color(255, 244, 228), 2, castsShadows: true);
SetShadowDistance(50);

Zones.All = [new Hub(), new Drawing(), new Course(), new Models(), new Lights()];
var player = new Player(Zones.All[0].Start, Zones.All[0].Facing);
var panel = new Panel(sun, Crash.Last());
FeatureCommands.Admin = panel;

while (!WindowShouldClose())
{
    var dt = GetFrameTime();
    FeatureCommands.Apply(player);
    panel.Update();
    var here = Zones.Around(player.Feet);
    player.Deaf = Panel.Typing;
    player.Update(dt, here);
    foreach (var zone in Zones.All) zone.Update(dt, player);
    FeatureCommands.Publish(player, here);

    BeginDrawing();
    foreach (var zone in Zones.All) zone.DrawOffscreen();
    player.Follow();
    var camera = player.Camera;
    ClearBackground(new Color(150, 190, 230));
    BeginMode3D(camera);
    DrawPlane(Vector3.Zero, new Vector2(400, 400), new Color(120, 160, 100));
    foreach (var zone in Zones.All) zone.Draw(camera);
    player.Draw();
    EndMode3D();
    foreach (var zone in Zones.All) zone.DrawScreen(camera);
    Heading(here);
    panel.Draw(player);
    EndDrawing();
    if (FeatureCommands.CrashAsked) throw new InvalidOperationException("The feature test was asked to crash by feature.crash, to write its crash file.");
}

CloseWindow();

// The zone's name and status at the top and the keys at the bottom, each wrapped to the window.
void Heading(Zone? here)
{
    var width = GetScreenWidth();
    string[] status = here is null ? [] : Wrapped(here.Status(), 18, width - 140).ToArray();
    DrawRectangle(0, 0, width, 46 + 20 * status.Length, new Color(0, 0, 0, 120));
    DrawText(here?.Name ?? "The roads", 16, 10, 24, Color.White);
    for (var i = 0; i < status.Length; i++) DrawText(status[i], 16, 40 + 20 * i, 18, new Color(230, 230, 230));
    DrawFPS(width - 100, 12);
    var keys = Wrapped("W A S D walk, Shift runs, Space jumps, C crouches, F flies, R goes back to the start, Tab holds the mouse, the arrows look", 18, width - 32).ToArray();
    for (var i = 0; i < keys.Length; i++) DrawText(keys[i], 16, GetScreenHeight() - 26 - 20 * (keys.Length - 1 - i), 18, new Color(30, 30, 30));
}

// Words laid in lines no wider than a width at a size.
static IEnumerable<string> Wrapped(string text, int size, int width)
{
    var line = "";
    foreach (var word in text.Split(' '))
    {
        var longer = line.Length == 0 ? word : $"{line} {word}";
        if (line.Length > 0 && MeasureText(longer, size) > width)
        {
            yield return line;
            line = word;
        }
        else line = longer;
    }
    if (line.Length > 0) yield return line;
}
