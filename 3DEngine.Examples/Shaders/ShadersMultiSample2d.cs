// raylib's shaders_multi_sample2d example, Copyright (c) 2020-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersMultiSample2d
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] multi sample2d");

        Image imRed = GenImageColor(800, 450, new Color(255, 0, 0, 255));
        Texture2D texRed = LoadTextureFromImage(imRed);
        UnloadImage(imRed);

        Image imBlue = GenImageColor(800, 450, new Color(0, 0, 255, 255));
        Texture2D texBlue = LoadTextureFromImage(imBlue);
        UnloadImage(imBlue);

        // raylib's color_mix.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/color_mix.slang");

        // The second texture the shader samples, beside the one drawn
        int texBlueLoc = GetShaderLocation(shader, "texture1");

        int dividerLoc = GetShaderLocation(shader, "divider");
        float dividerValue = 0.5f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Right)) dividerValue += 0.01f;
            else if (IsKeyDown(Key.Left)) dividerValue -= 0.01f;

            if (dividerValue < 0.0f) dividerValue = 0.0f;
            else if (dividerValue > 1.0f) dividerValue = 1.0f;

            SetShaderValue(shader, dividerLoc, dividerValue);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginShaderMode(shader);

                    // raylib sets its second texture again each frame, which its EndShaderMode
                    // resets. Here it holds once set, and setting it each frame does no harm.
                    SetShaderValueTexture(shader, texBlueLoc, texBlue);

                    // The red texture drawn, and the blue one sampled beside it
                    DrawTexture(texRed, 0, 0, Color.White);

                EndShaderMode();

                DrawText("Use KEY_LEFT/KEY_RIGHT to move texture mixing in shader!", 80, GetScreenHeight() - 40, 20, Color.RayWhite);

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadTexture(texRed);
        UnloadTexture(texBlue);

        CloseWindow();
    }
}
