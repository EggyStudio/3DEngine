// raylib's textures_screen_buffer example, Copyright (c) 2025 Agnis Aldiņš (@nezvers), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesScreenBuffer
{
    private const int MAX_COLORS = 256;
    private const int SCALE_FACTOR = 2;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] screen buffer");

        int imageWidth = screenWidth/SCALE_FACTOR;
        int imageHeight = screenHeight/SCALE_FACTOR;
        int flameWidth = screenWidth/SCALE_FACTOR;

        Color[] palette = new Color[MAX_COLORS];
        byte[] indexBuffer = new byte[imageWidth*imageWidth];
        byte[] flameRootBuffer = new byte[flameWidth];

        Image screenImage = GenImageColor(imageWidth, imageHeight, Color.Black);
        Texture2D screenTexture = LoadTextureFromImage(screenImage);

        // Drawn at twice its size, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(screenTexture, TextureFilter.Point);

        // The flame's palette
        for (int i = 0; i < MAX_COLORS; i++)
        {
            float t = (float)i/(float)(MAX_COLORS - 1);
            float hue = t*t;
            float saturation = t;
            float value = t;
            palette[i] = ColorFromHSV(250.0f + 150.0f*hue, saturation, value);
        }

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // The flame's root grows,
            for (int x = 2; x < flameWidth; x++)
            {
                int flame = (int)flameRootBuffer[x];
                flame += GetRandomValue(0, 2);
                flameRootBuffer[x] = (flame > 255)? (byte)255 : (byte)flame;
            }

            // and becomes the bottom row.
            for (int x = 0; x < flameWidth; x++)
            {
                int i = x + (imageHeight - 1)*imageWidth;
                indexBuffer[i] = flameRootBuffer[x];
            }

            // The top row is cleared, nothing being able to rise above it,
            for (int x = 0; x < imageWidth; x++)
            {
                if (indexBuffer[x] != 0) indexBuffer[x] = 0;
            }

            // and every other pixel rises a row, drifting and fading.
            for (int y = 1; y < imageHeight; y++)
            {
                for (int x = 0; x < imageWidth; x++)
                {
                    int i = x + y*imageWidth;
                    byte colorIndex = indexBuffer[i];

                    if (colorIndex != 0)
                    {
                        indexBuffer[i] = 0;
                        int moveX = GetRandomValue(0, 2) - 1;
                        int newX = x + moveX;

                        if ((newX > 0) && (newX < imageWidth))
                        {
                            int iabove = i - imageWidth + moveX;
                            int decay = GetRandomValue(0, 3);
                            colorIndex -= (byte)((decay < colorIndex)? decay : colorIndex);
                            indexBuffer[iabove] = colorIndex;
                        }
                    }
                }
            }

            // The image takes each pixel's color from the palette.
            for (int y = 1; y < imageHeight; y++)
            {
                for (int x = 0; x < imageWidth; x++)
                {
                    int i = x + y*imageWidth;
                    byte colorIndex = indexBuffer[i];
                    Color col = palette[colorIndex];
                    ImageDrawPixel(ref screenImage, x, y, col);
                }
            }

            UpdateTexture(screenTexture, screenImage);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTextureEx(screenTexture, Vector2.Zero, 0.0f, 2.0f, Color.White);

            EndDrawing();
        }

        UnloadTexture(screenTexture);
        UnloadImage(screenImage);

        CloseWindow();
    }
}
