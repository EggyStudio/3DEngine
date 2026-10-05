// raylib's textures_image_rotate example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesImageRotate
{
    private const int NUM_TEXTURES = 3;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] image rotate");

        Image image45 = LoadImage("resources/raylib_logo.png");
        Image image90 = LoadImage("resources/raylib_logo.png");
        Image imageNeg90 = LoadImage("resources/raylib_logo.png");

        ImageRotate(ref image45, 45);
        ImageRotate(ref image90, 90);
        ImageRotate(ref imageNeg90, -90);

        Texture2D[] textures =
        [
            LoadTextureFromImage(image45),
            LoadTextureFromImage(image90),
            LoadTextureFromImage(imageNeg90),
        ];

        UnloadImage(image45);
        UnloadImage(image90);
        UnloadImage(imageNeg90);

        int currentTexture = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsMouseButtonPressed(MouseButton.Left) || IsKeyPressed(Key.Right))
            {
                currentTexture = (currentTexture + 1)%NUM_TEXTURES;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(textures[currentTexture], screenWidth/2 - textures[currentTexture].Width/2, screenHeight/2 - textures[currentTexture].Height/2, Color.White);

                DrawText("Press LEFT MOUSE BUTTON to rotate the image clockwise", 250, 420, 10, Color.DarkGray);

            EndDrawing();
        }

        for (int i = 0; i < NUM_TEXTURES; i++) UnloadTexture(textures[i]);

        CloseWindow();
    }
}
