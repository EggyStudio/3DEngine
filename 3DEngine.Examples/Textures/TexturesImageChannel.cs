// raylib's textures_image_channel example, Copyright (c) 2024-2025 Bruno Cabral (@brccabral) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesImageChannel
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] image channel");

        Image fudesumiImage = LoadImage("resources/fudesumi.png");

        // Each channel as a gray image, cut out by the picture's alpha
        Image imageAlpha = ImageFromChannel(fudesumiImage, 3);
        ImageAlphaMask(ref imageAlpha, imageAlpha);

        Image imageRed = ImageFromChannel(fudesumiImage, 0);
        ImageAlphaMask(ref imageRed, imageAlpha);

        Image imageGreen = ImageFromChannel(fudesumiImage, 1);
        ImageAlphaMask(ref imageGreen, imageAlpha);

        Image imageBlue = ImageFromChannel(fudesumiImage, 2);
        ImageAlphaMask(ref imageBlue, imageAlpha);

        Image backgroundImage = GenImageChecked(screenWidth, screenHeight, screenWidth/20, screenHeight/20, Color.Orange, Color.Yellow);

        Texture2D fudesumiTexture = LoadTextureFromImage(fudesumiImage);
        Texture2D textureAlpha = LoadTextureFromImage(imageAlpha);
        Texture2D textureRed = LoadTextureFromImage(imageRed);
        Texture2D textureGreen = LoadTextureFromImage(imageGreen);
        Texture2D textureBlue = LoadTextureFromImage(imageBlue);
        Texture2D backgroundTexture = LoadTextureFromImage(backgroundImage);

        UnloadImage(fudesumiImage);
        UnloadImage(imageAlpha);
        UnloadImage(imageRed);
        UnloadImage(imageGreen);
        UnloadImage(imageBlue);
        UnloadImage(backgroundImage);

        Rectangle fudesumiRec = new(0, 0, (float)fudesumiImage.Width, (float)fudesumiImage.Height);

        Rectangle fudesumiPos = new(50, 10, fudesumiImage.Width*0.8f, fudesumiImage.Height*0.8f);
        Rectangle redPos = new(410, 10, fudesumiPos.Width/2.0f, fudesumiPos.Height/2.0f);
        Rectangle greenPos = new(600, 10, fudesumiPos.Width/2.0f, fudesumiPos.Height/2.0f);
        Rectangle bluePos = new(410, 230, fudesumiPos.Width/2.0f, fudesumiPos.Height/2.0f);
        Rectangle alphaPos = new(600, 230, fudesumiPos.Width/2.0f, fudesumiPos.Height/2.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                DrawTexture(backgroundTexture, 0, 0, Color.White);
                DrawTexturePro(fudesumiTexture, fudesumiRec, fudesumiPos, Vector2.Zero, 0, Color.White);

                DrawTexturePro(textureRed, fudesumiRec, redPos, Vector2.Zero, 0, Color.Red);
                DrawTexturePro(textureGreen, fudesumiRec, greenPos, Vector2.Zero, 0, Color.Green);
                DrawTexturePro(textureBlue, fudesumiRec, bluePos, Vector2.Zero, 0, Color.Blue);
                DrawTexturePro(textureAlpha, fudesumiRec, alphaPos, Vector2.Zero, 0, Color.White);

            EndDrawing();
        }

        UnloadTexture(backgroundTexture);
        UnloadTexture(fudesumiTexture);
        UnloadTexture(textureRed);
        UnloadTexture(textureGreen);
        UnloadTexture(textureBlue);
        UnloadTexture(textureAlpha);

        CloseWindow();
    }
}
