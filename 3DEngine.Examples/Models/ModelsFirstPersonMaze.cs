// raylib's models_first_person_maze example, Copyright (c) 2019-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsFirstPersonMaze
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] first person maze");

        Camera3D camera = new(new Vector3(0.2f, 0.4f, 0.2f), new Vector3(0.185f, 0.4f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Image imMap = LoadImage("resources/cubicmap.png");
        Texture2D cubicmap = LoadTextureFromImage(imMap);

        // Pixel art drawn at four times its size, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(cubicmap, TextureFilter.Point);

        ModelMesh mesh = GenMeshCubicmap(imMap, new Vector3(1.0f, 1.0f, 1.0f));
        Model model = LoadModelFromMesh(mesh);

        // Each cube's faces take their part of the atlas.
        Texture2D texture = LoadTexture("resources/cubicmap_atlas.png");
        SetTextureFilter(texture, TextureFilter.Point);
        model.Materials[0].Texture = texture;

        // The map's pixels, which the walls are found in
        Color[] mapPixels = LoadImageColors(imMap);
        UnloadImage(imMap);

        Vector3 mapPosition = new(-16.0f, 0.0f, -8.0f);

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector3 oldCamPos = camera.Position;

            UpdateCamera(ref camera, CameraMode.FirstPerson);

            // The player is a circle on the ground for colliding with the walls.
            Vector2 playerPos = new(camera.Position.X, camera.Position.Z);
            float playerRadius = 0.1f;

            int playerCellX = (int)(playerPos.X - mapPosition.X + 0.5f);
            int playerCellY = (int)(playerPos.Y - mapPosition.Z + 0.5f);

            // Kept on the map
            if (playerCellX < 0) playerCellX = 0;
            else if (playerCellX >= cubicmap.Width) playerCellX = cubicmap.Width - 1;

            if (playerCellY < 0) playerCellY = 0;
            else if (playerCellY >= cubicmap.Height) playerCellY = cubicmap.Height - 1;

            // Against the walls of the cells around the player, a white pixel being a wall
            for (int y = playerCellY - 1; y <= playerCellY + 1; y++)
            {
                if ((y >= 0) && (y < cubicmap.Height))
                {
                    for (int x = playerCellX - 1; x <= playerCellX + 1; x++)
                    {
                        if (((x >= 0) && (x < cubicmap.Width)) &&
                            (mapPixels[y*cubicmap.Width + x].R == 255) &&
                            CheckCollisionCircleRec(playerPos, playerRadius,
                            new Rectangle(mapPosition.X - 0.5f + x*1.0f, mapPosition.Z - 0.5f + y*1.0f, 1.0f, 1.0f)))
                        {
                            // A wall puts the camera back where it was.
                            camera.Position = oldCamPos;
                        }
                    }
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawModel(model, mapPosition, 1.0f, Color.White);
                EndMode3D();

                DrawTextureEx(cubicmap, new Vector2(GetScreenWidth() - cubicmap.Width*4.0f - 20, 20.0f), 0.0f, 4.0f, Color.White);
                DrawRectangleLines(GetScreenWidth() - cubicmap.Width*4 - 20, 20, cubicmap.Width*4, cubicmap.Height*4, Color.Green);

                // The player on the map
                DrawRectangle(GetScreenWidth() - cubicmap.Width*4 - 20 + playerCellX*4, 20 + playerCellY*4, 4, 4, Color.Red);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadTexture(cubicmap);
        UnloadTexture(texture);
        UnloadModel(model);

        CloseWindow();
    }
}
