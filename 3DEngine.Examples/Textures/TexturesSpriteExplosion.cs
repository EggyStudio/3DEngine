// raylib's textures_sprite_explosion example, Copyright (c) 2019-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesSpriteExplosion
{
    private const int NUM_FRAMES_PER_LINE = 5;
    private const int NUM_LINES = 5;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] sprite explosion");

        InitAudioDevice();

        Sound fxBoom = LoadSound("resources/boom.wav");

        Texture2D explosion = LoadTexture("resources/explosion.png");

        // A frame of the sheet
        float frameWidth = (float)explosion.Width/NUM_FRAMES_PER_LINE;
        float frameHeight = (float)explosion.Height/NUM_LINES;
        int currentFrame = 0;
        int currentLine = 0;

        Rectangle frameRec = new(0, 0, frameWidth, frameHeight);
        Vector2 position = Vector2.Zero;

        bool active = false;
        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // A click sets off an explosion where none is playing.
            if (IsMouseButtonPressed(MouseButton.Left) && !active)
            {
                position = GetMousePosition();
                active = true;

                position.X -= frameWidth/2.0f;
                position.Y -= frameHeight/2.0f;

                PlaySound(fxBoom);
            }

            // A frame every third frame, line by line through the sheet
            if (active)
            {
                framesCounter++;

                if (framesCounter > 2)
                {
                    currentFrame++;

                    if (currentFrame >= NUM_FRAMES_PER_LINE)
                    {
                        currentFrame = 0;
                        currentLine++;

                        if (currentLine >= NUM_LINES)
                        {
                            currentLine = 0;
                            active = false;
                        }
                    }

                    framesCounter = 0;
                }
            }

            frameRec = frameRec with { X = frameWidth*currentFrame, Y = frameHeight*currentLine };

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (active) DrawTextureRec(explosion, frameRec, position, Color.White);

            EndDrawing();
        }

        UnloadTexture(explosion);
        UnloadSound(fxBoom);

        CloseAudioDevice();

        CloseWindow();
    }
}
