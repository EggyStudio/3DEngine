// raylib's shaders_texture_waves example, Copyright (c) 2019-2025 Anata (@anatagawa) and Ramon Santamaria
// (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersTextureWaves
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] texture waves");

        Texture2D texture = LoadTexture("resources/space.png");

        // raylib's wave.fs, written in Slang, and the places of its values
        Shader shader = LoadShader("resources/shaders/slang/wave.slang");

        int secondsLoc = GetShaderLocation(shader, "seconds");
        int freqXLoc = GetShaderLocation(shader, "freqX");
        int freqYLoc = GetShaderLocation(shader, "freqY");
        int ampXLoc = GetShaderLocation(shader, "ampX");
        int ampYLoc = GetShaderLocation(shader, "ampY");
        int speedXLoc = GetShaderLocation(shader, "speedX");
        int speedYLoc = GetShaderLocation(shader, "speedY");

        // Values that can change at any time
        float freqX = 25.0f;
        float freqY = 25.0f;
        float ampX = 5.0f;
        float ampY = 5.0f;
        float speedX = 8.0f;
        float speedY = 8.0f;

        Vector2 screenSize = new((float)GetScreenWidth(), (float)GetScreenHeight());
        SetShaderValue(shader, GetShaderLocation(shader, "size"), screenSize);
        SetShaderValue(shader, freqXLoc, freqX);
        SetShaderValue(shader, freqYLoc, freqY);
        SetShaderValue(shader, ampXLoc, ampX);
        SetShaderValue(shader, ampYLoc, ampY);
        SetShaderValue(shader, speedXLoc, speedX);
        SetShaderValue(shader, speedYLoc, speedY);

        float seconds = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            seconds += GetFrameTime();

            SetShaderValue(shader, secondsLoc, seconds);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginShaderMode(shader);

                    DrawTexture(texture, 0, 0, Color.White);
                    DrawTexture(texture, texture.Width, 0, Color.White);

                EndShaderMode();

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadTexture(texture);

        CloseWindow();
    }
}
