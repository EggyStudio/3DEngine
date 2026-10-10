// A voxel game in the manner of Minecraft, built to test the light that bounces: an endless world
// of colored blocks to walk, fly and build in, blocks that give off light, and a sky with a sun
// that crosses it. `--flat` and `--seed <n>` pick the kind of world and its seed, `--world <name>`
// the save it is kept in, its kind and seed by default, and `--transient` keeps nothing on disk,
// as a test run does.
using Engine;
using Engine.Game;
using static Engine.Engine3D;

var seed = 1;
var flat = false;
var transient = false;
string? name = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--flat") flat = true;
    else if (args[i] == "--transient") transient = true;
    else if (args[i] == "--seed" && i + 1 < args.Length && int.TryParse(args[i + 1], out var number)) seed = number;
    else if (args[i] == "--world" && i + 1 < args.Length) name = args[i + 1];
}

SetConfigFlags(ConfigFlags.WindowResizable);
InitWindow(1280, 720, "3DEngine.Game");
// Escape frees the cursor, as it opens Minecraft's menu, rather than closing the window.
SetExitKey(Key.Null);

using (var game = new VoxelGame(flat ? "flat" : "overworld", seed, name, transient))
{
    GameCommands.Game = game;
    while (!WindowShouldClose())
    {
        game.Update();
        game.Draw();
    }
    GameCommands.Game = null;
}

CloseWindow();
