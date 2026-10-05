// raylib's textures_sprite_animation example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesSpriteAnimation
{
    private const int MAX_FRAME_SPEED = 15;
    private const int MIN_FRAME_SPEED = 1;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] sprite animation");

        Texture2D scarfy = LoadTexture("resources/scarfy.png");

        // Pixel art, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(scarfy, TextureFilter.Point);

        Vector2 position = new(350.0f, 280.0f);
        Rectangle frameRec = new(0.0f, 0.0f, (float)scarfy.Width/6, (float)scarfy.Height);
        int currentFrame = 0;

        int framesCounter = 0;
        int framesSpeed = 8;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            framesCounter++;

            if (framesCounter >= (60/framesSpeed))
            {
                framesCounter = 0;
                currentFrame++;

                if (currentFrame > 5) currentFrame = 0;

                frameRec = frameRec with { X = (float)currentFrame*(float)scarfy.Width/6 };
            }

            if (IsKeyPressed(Key.Right)) framesSpeed++;
            else if (IsKeyPressed(Key.Left)) framesSpeed--;

            if (framesSpeed > MAX_FRAME_SPEED) framesSpeed = MAX_FRAME_SPEED;
            else if (framesSpeed < MIN_FRAME_SPEED) framesSpeed = MIN_FRAME_SPEED;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexture(scarfy, 15, 40, Color.White);
                DrawRectangleLines(15, 40, scarfy.Width, scarfy.Height, Color.Lime);
                DrawRectangleLines(15 + (int)frameRec.X, 40 + (int)frameRec.Y, (int)frameRec.Width, (int)frameRec.Height, Color.Red);

                DrawText("FRAME SPEED: ", 165, 210, 10, Color.DarkGray);
                DrawText($"{framesSpeed:D2} FPS", 575, 210, 10, Color.DarkGray);
                DrawText("PRESS RIGHT/LEFT KEYS to CHANGE SPEED!", 290, 240, 10, Color.DarkGray);

                for (int i = 0; i < MAX_FRAME_SPEED; i++)
                {
                    if (i < framesSpeed) DrawRectangle(250 + 21*i, 205, 20, 20, Color.Red);
                    DrawRectangleLines(250 + 21*i, 205, 20, 20, Color.Maroon);
                }

                DrawTextureRec(scarfy, frameRec, position, Color.White);

                DrawText("(c) Scarfy sprite by Eiden Marsal", screenWidth - 200, screenHeight - 20, 10, Color.Gray);

            EndDrawing();
        }

        UnloadTexture(scarfy);

        CloseWindow();
    }
}
