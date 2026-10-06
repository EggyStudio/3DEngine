// raylib's textures_image_drawing example, Copyright (c) 2016-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesImageDrawing
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] image drawing");

        // Images are edited in memory, then made one texture on the GPU.
        Image cat = LoadImage("resources/cat.png");
        ImageCrop(ref cat, new Rectangle(100, 10, 280, 380));
        ImageFlipHorizontal(ref cat);
        ImageResize(ref cat, 150, 200);

        Image parrots = LoadImage("resources/parrots.png");

        // The cat drawn over the parrots half as large again, and the result cropped.
        ImageDrawImagePro(ref parrots, cat, new Rectangle(0, 0, cat.Width, cat.Height),
            new Rectangle(30, 40, cat.Width*1.5f, cat.Height*1.5f), Vector2.Zero, 0.0f, Color.White);
        ImageCrop(ref parrots, new Rectangle(0, 50, parrots.Width, parrots.Height - 100));

        ImageDrawPixel(ref parrots, 10, 10, Color.RayWhite);
        ImageDrawCircleLines(ref parrots, 10, 10, 5, Color.RayWhite);
        ImageDrawRectangle(ref parrots, 5, 20, 10, 10, Color.RayWhite);

        UnloadImage(cat);

        // Text drawn into the image in a font of its own, let go once drawn.
        Font font = LoadFont("resources/custom_jupiter_crash.png");
        ImageDrawTextEx(ref parrots, font, "PARROTS & CAT", new Vector2(300, 230), font.BaseSize, -2, Color.White);
        UnloadFont(font);

        Texture2D texture = LoadTextureFromImage(parrots);
        UnloadImage(parrots);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(texture, screenWidth/2 - texture.Width/2, screenHeight/2 - texture.Height/2 - 40, Color.White);
                DrawRectangleLines(screenWidth/2 - texture.Width/2, screenHeight/2 - texture.Height/2 - 40, texture.Width, texture.Height, Color.DarkGray);

                DrawText("We are drawing only one texture from various images composed!", 240, 350, 10, Color.DarkGray);
                DrawText("Source images have been cropped, scaled, flipped and copied one over the other.", 190, 370, 10, Color.DarkGray);

            EndDrawing();
        }

        UnloadTexture(texture);

        CloseWindow();
    }
}
