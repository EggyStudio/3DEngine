// raylib's textures_sprite_button example, Copyright (c) 2019-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesSpriteButton
{
    // The button's frames in its texture
    private const int NUM_FRAMES = 3;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] sprite button");

        InitAudioDevice();

        Sound fxButton = LoadSound("resources/buttonfx.wav");
        Texture2D button = LoadTexture("resources/button.png");

        float frameHeight = (float)button.Height/NUM_FRAMES;
        Rectangle sourceRec = new(0, 0, (float)button.Width, frameHeight);

        // Where the button is on the screen
        Rectangle btnBounds = new(screenWidth/2.0f - button.Width/2.0f, screenHeight/2.0f - (float)button.Height/NUM_FRAMES/2.0f, (float)button.Width, frameHeight);

        int btnState = 0;               // 0 at rest, 1 under the pointer, 2 pressed
        bool btnAction = false;

        Vector2 mousePoint = Vector2.Zero;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            mousePoint = GetMousePosition();
            btnAction = false;

            if (CheckCollisionPointRec(mousePoint, btnBounds))
            {
                if (IsMouseButtonDown(MouseButton.Left)) btnState = 2;
                else btnState = 1;

                if (IsMouseButtonReleased(MouseButton.Left)) btnAction = true;
            }
            else btnState = 0;

            if (btnAction)
            {
                PlaySound(fxButton);
            }

            // The frame for the button's state
            sourceRec = sourceRec with { Y = btnState*frameHeight };

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTextureRec(button, sourceRec, new Vector2(btnBounds.X, btnBounds.Y), Color.White);

            EndDrawing();
        }

        UnloadTexture(button);
        UnloadSound(fxButton);

        CloseAudioDevice();

        CloseWindow();
    }
}
