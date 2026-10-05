// raylib's textures_to_image example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesToImage
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] to image");

        // From memory to the GPU, back from the GPU into memory, and to the GPU again
        Image image = LoadImage("resources/raylib_logo.png");
        Texture2D texture = LoadTextureFromImage(image);
        UnloadImage(image);

        image = LoadImageFromTexture(texture);
        UnloadTexture(texture);

        texture = LoadTextureFromImage(image);
        UnloadImage(image);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(texture, screenWidth/2 - texture.Width/2, screenHeight/2 - texture.Height/2, Color.White);

                DrawText("this IS a texture loaded from an image!", 300, 370, 10, Color.Gray);

            EndDrawing();
        }

        UnloadTexture(texture);

        CloseWindow();
    }
}
