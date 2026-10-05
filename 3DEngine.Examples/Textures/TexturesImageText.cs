// raylib's textures_image_text example, Copyright (c) 2017-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesImageText
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] image text");

        Image parrots = LoadImage("resources/parrots.png");

        // ASCII without '@', which KAISG.ttf has no glyph for
        int[] codepoints = LoadCodepoints(" !\"#$%&'()*+,-./0123456789:;<=>?ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~");

        Font font = LoadFontEx("resources/KAISG.ttf", 64, codepoints);

        // The text drawn into the image, on the CPU, before it becomes a texture
        ImageDrawTextEx(ref parrots, font, "[Parrots font drawing]", new Vector2(20.0f, 20.0f), (float)font.BaseSize, 0.0f, Color.Red);

        Texture2D texture = LoadTextureFromImage(parrots);
        UnloadImage(parrots);

        Vector2 position = new((float)screenWidth/2 - (float)texture.Width/2, (float)screenHeight/2 - (float)texture.Height/2 - 20);

        bool showFont = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Space)) showFont = true;
            else showFont = false;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (!showFont)
                {
                    // The texture with the text already in it, and the same text drawn on the screen
                    DrawTextureV(texture, position, Color.White);

                    DrawTextEx(font, "[Parrots font drawing]", new Vector2(position.X + 20, position.Y + 20 + 280), (float)font.BaseSize, 0.0f, Color.White);
                }
                else
                {
                    // The font's atlas, scaled to fit the screen
                    float scale = 1.0f;
                    float atlasRatio = (float)font.Texture.Width/(float)font.Texture.Height;
                    float screenRatio = (float)screenWidth/(float)screenHeight;

                    if (atlasRatio >= screenRatio) scale = (float)screenWidth/(float)font.Texture.Width;
                    else scale = (float)screenHeight/(float)font.Texture.Height;

                    float width = (float)font.Texture.Width*scale;
                    float height = (float)font.Texture.Height*scale;
                    DrawTextureEx(font.Texture, new Vector2(((float)screenWidth - width)/2, ((float)screenHeight - height)/2), 0, scale, Color.Black);
                }

                DrawText("PRESS SPACE to SHOW FONT ATLAS USED", 290, 420, 10, Color.DarkGray);

            EndDrawing();
        }

        UnloadTexture(texture);
        UnloadFont(font);

        CloseWindow();
    }
}
