// raylib's textures_logo_raylib example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesLogoRaylib
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] logo raylib");

        Texture2D texture = LoadTexture("resources/raylib_logo.png");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(texture, screenWidth/2 - texture.Width/2, screenHeight/2 - texture.Height/2, Color.White);

                DrawText("this IS a texture!", 360, 370, 10, Color.Gray);

            EndDrawing();
        }

        UnloadTexture(texture);

        CloseWindow();
    }
}
