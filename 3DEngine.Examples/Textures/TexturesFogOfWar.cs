// raylib's textures_fog_of_war example, Copyright (c) 2018-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesFogOfWar
{
    private const int MAP_TILE_SIZE = 32;
    private const int PLAYER_SIZE = 16;
    private const int PLAYER_TILE_VISIBILITY = 2;   // The tiles the player sees around it

    private sealed class Map
    {
        public int tilesX;
        public int tilesY;
        public byte[] tileIds = [];     // Which tile each is
        public byte[] tileFog = [];     // Each tile's fog, none, seen or in sight
    }

    public static void Run()
    {
        int screenWidth = 800;
        int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] fog of war");

        Map map = new() { tilesX = 25, tilesY = 15 };

        // A byte a tile for its id and its fog, where two bits would hold the fog
        map.tileIds = new byte[map.tilesX*map.tilesY];
        map.tileFog = new byte[map.tilesX*map.tilesY];

        // Two kinds of tile at random, which a game would read from its map's file
        for (int i = 0; i < map.tilesY*map.tilesX; i++) map.tileIds[i] = (byte)GetRandomValue(0, 1);

        // The player's place in pixels
        Vector2 playerPosition = new(180, 130);
        int playerTileX = 0;
        int playerTileY = 0;

        // The fog is drawn a pixel a tile and scaled up through a bilinear filter, which smooths it.
        RenderTexture2D fogOfWar = LoadRenderTexture(map.tilesX, map.tilesY);
        SetTextureFilter(fogOfWar.Texture, TextureFilter.Bilinear);
        SetTextureWrap(fogOfWar.Texture, TextureWrap.Clamp);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Right)) playerPosition.X += 5;
            if (IsKeyDown(Key.Left)) playerPosition.X -= 5;
            if (IsKeyDown(Key.Down)) playerPosition.Y += 5;
            if (IsKeyDown(Key.Up)) playerPosition.Y -= 5;

            // Kept on the map
            if (playerPosition.X < 0) playerPosition.X = 0;
            else if ((playerPosition.X + PLAYER_SIZE) > (map.tilesX*MAP_TILE_SIZE)) playerPosition.X = (float)map.tilesX*MAP_TILE_SIZE - PLAYER_SIZE;
            if (playerPosition.Y < 0) playerPosition.Y = 0;
            else if ((playerPosition.Y + PLAYER_SIZE) > (map.tilesY*MAP_TILE_SIZE)) playerPosition.Y = (float)map.tilesY*MAP_TILE_SIZE - PLAYER_SIZE;

            // Tiles in sight last frame are now only seen.
            for (int i = 0; i < map.tilesX*map.tilesY; i++) if (map.tileFog[i] == 1) map.tileFog[i] = 2;

            playerTileX = (int)((playerPosition.X + (float)MAP_TILE_SIZE/2)/MAP_TILE_SIZE);
            playerTileY = (int)((playerPosition.Y + (float)MAP_TILE_SIZE/2)/MAP_TILE_SIZE);

            // The tiles around the player are in sight, those on the map.
            for (int y = (playerTileY - PLAYER_TILE_VISIBILITY); y < (playerTileY + PLAYER_TILE_VISIBILITY); y++)
                for (int x = (playerTileX - PLAYER_TILE_VISIBILITY); x < (playerTileX + PLAYER_TILE_VISIBILITY); x++)
                    if ((x >= 0) && (x < map.tilesX) && (y >= 0) && (y < map.tilesY)) map.tileFog[y*map.tilesX + x] = 1;

            BeginTextureMode(fogOfWar);
                ClearBackground(Color.Blank);
                for (int y = 0; y < map.tilesY; y++)
                    for (int x = 0; x < map.tilesX; x++)
                        if (map.tileFog[y*map.tilesX + x] == 0) DrawRectangle(x, y, 1, 1, Color.Black);
                        else if (map.tileFog[y*map.tilesX + x] == 2) DrawRectangle(x, y, 1, 1, Fade(Color.Black, 0.8f));
            EndTextureMode();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int y = 0; y < map.tilesY; y++)
                {
                    for (int x = 0; x < map.tilesX; x++)
                    {
                        // Each tile by its id, with its border
                        DrawRectangle(x*MAP_TILE_SIZE, y*MAP_TILE_SIZE, MAP_TILE_SIZE, MAP_TILE_SIZE,
                                      (map.tileIds[y*map.tilesX + x] == 0)? Color.Blue : Fade(Color.Blue, 0.9f));
                        DrawRectangleLines(x*MAP_TILE_SIZE, y*MAP_TILE_SIZE, MAP_TILE_SIZE, MAP_TILE_SIZE, Fade(Color.DarkBlue, 0.5f));
                    }
                }

                DrawRectangleV(playerPosition, new Vector2(PLAYER_SIZE, PLAYER_SIZE), Color.Red);

                // The fog over the whole map, its height as it is, a target being stored the right
                // way up here.
                DrawTexturePro(fogOfWar.Texture, new Rectangle(0, 0, (float)fogOfWar.Texture.Width, (float)fogOfWar.Texture.Height),
                               new Rectangle(0, 0, (float)map.tilesX*MAP_TILE_SIZE, (float)map.tilesY*MAP_TILE_SIZE),
                               Vector2.Zero, 0.0f, Color.White);

                DrawText($"Current tile: [{playerTileX},{playerTileY}]", 10, 10, 20, Color.RayWhite);
                DrawText("ARROW KEYS to move", 10, screenHeight - 25, 20, Color.RayWhite);

            EndDrawing();
        }

        UnloadRenderTexture(fogOfWar);

        CloseWindow();
    }
}
