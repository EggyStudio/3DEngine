// raylib's textures_sprite_stacking example, Copyright (c) 2025 Robin (@RobinsAviary), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesSpriteStacking
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] sprite stacking");

        Texture2D booth = LoadTexture("resources/booth.png");

        // Pixel art drawn three times its size, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(booth, TextureFilter.Point);

        float stackScale = 3.0f;        // The stacked sprite's scale
        float stackSpacing = 2.0f;      // The height between layers
        int stackCount = 122;           // The layers, which give the height of one
        float rotationSpeed = 30.0f;
        float rotation = 0.0f;
        const float speedChange = 0.25f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // The wheel spreads the layers, which looks as a change of angle.
            stackSpacing += GetMouseWheelMove()*0.1f;
            stackSpacing = Math.Clamp(stackSpacing, 0.0f, 5.0f);

            // A and D spin it one way faster and the other way slower.
            if (IsKeyDown(Key.Left) || IsKeyDown(Key.A)) rotationSpeed -= speedChange;
            if (IsKeyDown(Key.Right) || IsKeyDown(Key.D)) rotationSpeed += speedChange;

            rotation += rotationSpeed*GetFrameTime();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // One layer's size, and the size it is drawn at
                float frameWidth = (float)booth.Width;
                float frameHeight = (float)booth.Height/(float)stackCount;

                float scaledWidth = frameWidth*stackScale;
                float scaledHeight = frameHeight*stackScale;

                // The layers from the bottom up, each turned and raised by its place, centered
                for (int i = stackCount - 1; i >= 0; i--)
                {
                    Rectangle source = new(0.0f, (float)i*frameHeight, frameWidth, frameHeight);
                    Rectangle dest = new(screenWidth/2.0f, (screenHeight/2.0f) + (i*stackSpacing) - (stackSpacing*stackCount/2.0f), scaledWidth, scaledHeight);
                    Vector2 origin = new(scaledWidth/2.0f, scaledHeight/2.0f);

                    DrawTexturePro(booth, source, dest, origin, rotation, Color.White);
                }

                DrawText("A/D to spin\nmouse wheel to change separation (aka 'angle')", 10, 10, 20, Color.DarkGray);
                DrawText($"current spacing: {stackSpacing:0.0}", 10, 50, 20, Color.DarkGray);
                DrawText($"current speed: {rotationSpeed:0.00}", 10, 70, 20, Color.DarkGray);
                DrawText("redbooth model (c) kluchek under cc 4.0", 10, 420, 20, Color.DarkGray);

            EndDrawing();
        }

        UnloadTexture(booth);

        CloseWindow();
    }
}
