// Hopper, a small platformer: run and jump across a level of tiles, gather its coins, and beat the
// high score. Written against the engine's package with what its README and cheatsheet offer, as
// a 2D game outside this repository would be.
using System.Numerics;
using Engine;
using static Engine.Engine3D;

const int ViewWidth = 320, ViewHeight = 180, Tile = 16;

InitWindow(960, 540, "Hopper");
InitAudioDevice();
SetTargetFPS(60);

// The level, a character a tile: # grass, = dirt, B brick, o a coin, P where the player begins.
string[] map =
[
    "                                                            ",
    "                                                            ",
    "                                                            ",
    "                       o o                                  ",
    "                      BBBBB             o  o  o             ",
    "            o                         ######### ",
    "           BBB      o                                 o     ",
    "                   ###          o                    ###    ",
    "      o                        BBB          o               ",
    "  P  ###    o  o                          #####     o   o   ",
    "############################   ######################  #####",
    "============================   ======================  =====",
];
var solid = new List<Rectangle>();
var tiles = new List<(Rectangle Where, int Index)>();
var coins = new List<Vector2>();
var start = Vector2.Zero;
for (int row = 0; row < map.Length; row++)
for (int col = 0; col < map[row].Length; col++)
{
    var at = new Vector2(col * Tile, row * Tile);
    var cell = new Rectangle(at.X, at.Y, Tile, Tile);
    switch (map[row][col])
    {
        case '#': solid.Add(cell); tiles.Add((cell, 0)); break;
        case '=': solid.Add(cell); tiles.Add((cell, 1)); break;
        case 'B': solid.Add(cell); tiles.Add((cell, 2)); break;
        case 'o': coins.Add(at + new Vector2(Tile / 2f)); break;
        case 'P': start = at; break;
    }
}
var levelWidth = map.Max(r => r.Length) * Tile;
var levelHeight = map.Length * Tile;

// -- Art, sound and text

var hero = LoadTexture("resources/hero.png");
var atlas = LoadTexture("resources/tiles.png");
SetTextureFilter(hero, TextureFilter.Point);
SetTextureFilter(atlas, TextureFilter.Point);
var font = LoadFontEx("resources/Lato-Regular.ttf", 32);
var chime = LoadSound("resources/coin.wav");
var music = LoadMusicStream("resources/drone.ogg");
SetMusicVolume(music, 0.4f);
PlayMusicStream(music);

// The game draws at 320 by 180 into a target, which is scaled to the window in whole pixels.
var view = LoadRenderTexture(ViewWidth, ViewHeight);
SetTextureFilter(view.Texture, TextureFilter.Point);

// -- The high score, kept in a file beside the program

var highScore = FileExists("highscore.txt") && int.TryParse(LoadFileText("highscore.txt"), out var saved) ? saved : 0;

var player = new Rectangle(start.X + 3, start.Y, 10, 16);
var allCoins = coins.ToArray();
var velocity = Vector2.Zero;
var grounded = false;
var facingLeft = false;
var runTime = 0f;
var score = 0;
var camera = new Camera2D(new Vector2(ViewWidth / 2f, ViewHeight / 2f), Vector2.Zero);

while (!WindowShouldClose())
{
    var dt = MathF.Min(GetFrameTime(), 1 / 30f);
    UpdateMusicStream(music);

    // Input from the keys, or the first pad's stick, d-pad and south button.
    var run = 0f;
    if (IsKeyDown(Key.Left) || IsKeyDown(Key.A)) run -= 1;
    if (IsKeyDown(Key.Right) || IsKeyDown(Key.D)) run += 1;
    var jump = IsKeyPressed(Key.Space) || IsKeyPressed(Key.Up) || IsKeyPressed(Key.W);
    // R starts again, at the start with every coin back.
    if (IsKeyPressed(Key.R))
    {
        coins.Clear();
        coins.AddRange(allCoins);
        player = player with { X = start.X + 3, Y = start.Y };
    }
    if (IsGamepadAvailable(0))
    {
        var stick = GetGamepadAxisMovement(0, GamepadAxis.LeftX);
        if (MathF.Abs(stick) > 0.25f) run = stick;
        if (IsGamepadButtonDown(0, GamepadButton.LeftFaceLeft)) run = -1;
        if (IsGamepadButtonDown(0, GamepadButton.LeftFaceRight)) run = 1;
        jump |= IsGamepadButtonPressed(0, GamepadButton.RightFaceDown);
    }

    // Movement, one axis at a time, so a wall stops the run and a floor or ceiling the fall.
    velocity.X = run * 90;
    velocity.Y = MathF.Min(velocity.Y + 600 * dt, 400);
    if (jump && grounded) velocity.Y = -250;
    if (run != 0) facingLeft = run < 0;

    player = player with { X = player.X + velocity.X * dt };
    foreach (var wall in solid)
    {
        if (!CheckCollisionRecs(player, wall)) continue;
        player = player with { X = velocity.X > 0 ? wall.X - player.Width : wall.X + wall.Width };
    }

    grounded = false;
    player = player with { Y = player.Y + velocity.Y * dt };
    foreach (var wall in solid)
    {
        if (!CheckCollisionRecs(player, wall)) continue;
        if (velocity.Y > 0) grounded = true;
        player = player with { Y = velocity.Y > 0 ? wall.Y - player.Height : wall.Y + wall.Height };
        velocity.Y = 0;
    }

    // Fallen out of the level: back to the start.
    if (player.Y > levelHeight + 64)
    {
        player = player with { X = start.X + 3, Y = start.Y };
        velocity = Vector2.Zero;
    }

    // Coins are circles, gathered when one touches the player's rectangle.
    for (int i = coins.Count - 1; i >= 0; i--)
        if (CheckCollisionCircleRec(coins[i], 5, player))
        {
            coins.RemoveAt(i);
            score++;
            PlaySound(chime);
            if (score > highScore)
            {
                highScore = score;
                SaveFileText("highscore.txt", highScore.ToString());
            }
        }

    runTime = run != 0 && grounded ? runTime + dt : 0;

    // The camera follows the player, held inside the level.
    camera.Target = new Vector2(
        Math.Clamp(player.X + player.Width / 2, ViewWidth / 2f, levelWidth - ViewWidth / 2f),
        Math.Clamp(player.Y, ViewHeight / 2f, levelHeight - ViewHeight / 2f));

    // -- Drawing, into the small view first

    BeginDrawing();
    BeginTextureMode(view);
    ClearBackground(new Color(120, 180, 240));
    BeginMode2D(camera);

    foreach (var (where, index) in tiles)
        DrawTextureRec(atlas, new Rectangle(index * Tile, 0, Tile, Tile), new Vector2(where.X, where.Y), Color.White);

    // The coin turns through four frames of the atlas, eight a second.
    var coinFrame = (int)(GetTime() * 8) % 4;
    foreach (var coin in coins)
        DrawTextureRec(atlas, new Rectangle((3 + coinFrame) * Tile, 0, Tile, Tile), coin - new Vector2(Tile / 2f), Color.White);

    // The run cycle, ten frames a second while running, and a negative width faces it left.
    var heroFrame = runTime > 0 ? (int)(runTime * 10) % 4 : 0;
    var source = new Rectangle(heroFrame * Tile, 0, facingLeft ? -Tile : Tile, Tile);
    DrawTextureRec(hero, source, new Vector2(player.X - 3, player.Y), Color.White);

    EndMode2D();
    DrawTextEx(font, $"Coins {score}", new Vector2(6, 4), 16, 1, Color.White);
    DrawTextEx(font, $"Best {highScore}", new Vector2(ViewWidth - 60, 4), 16, 1, Color.White);
    if (coins.Count == 0) DrawTextEx(font, "Every coin gathered!", new Vector2(90, 80), 20, 1, Color.Yellow);
    EndTextureMode();

    // The view, scaled to the window by the largest whole factor and centered on black.
    ClearBackground(Color.Black);
    var scale = Math.Max(1, Math.Min(GetScreenWidth() / ViewWidth, GetScreenHeight() / ViewHeight));
    var size = new Vector2(ViewWidth * scale, ViewHeight * scale);
    var corner = (new Vector2(GetScreenWidth(), GetScreenHeight()) - size) / 2;
    DrawTexturePro(view.Texture, new Rectangle(0, 0, ViewWidth, ViewHeight), new Rectangle(corner.X, corner.Y, size.X, size.Y),
        Vector2.Zero, 0, Color.White);
    EndDrawing();
}

UnloadRenderTexture(view);
UnloadMusicStream(music);
UnloadSound(chime);
UnloadFont(font);
UnloadTexture(atlas);
UnloadTexture(hero);
CloseAudioDevice();
CloseWindow();
