// A voxel game in the manner of Minecraft, built to test the light that bounces: an endless world
// of colored blocks to walk, fly and build in, blocks that give off light, and a sky with a sun
// that crosses it. `--flat` begins on a flat world and `--seed <n>` picks the world's seed.
using Engine;
using Engine.Game;
using static Engine.Engine3D;

var seed = 1;
var flat = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--flat") flat = true;
    else if (args[i] == "--seed" && i + 1 < args.Length && int.TryParse(args[i + 1], out var number)) seed = number;
}

SetConfigFlags(ConfigFlags.WindowResizable);
InitWindow(1280, 720, "3DEngine.Game");
// Escape frees the cursor, as it opens Minecraft's menu, rather than closing the window.
SetExitKey(Key.Null);

using (var game = new VoxelGame(flat ? new Superflat(seed) : new Overworld(seed)))
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
