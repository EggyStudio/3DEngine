// raylib's shaders_texture_rendering example, Copyright (c) 2019-2025 Michał Ciesielski (@ciessielski) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersTextureRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] texture rendering");

        // A clear texture the shader paints over
        Image imBlank = GenImageColor(1024, 1024, Color.Blank);
        Texture2D texture = LoadTextureFromImage(imBlank);
        UnloadImage(imBlank);

        // raylib's cubes_panning.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/cubes_panning.slang");

        float time = 0.0f;
        int timeLoc = GetShaderLocation(shader, "uTime");
        SetShaderValue(shader, timeLoc, time);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            time = (float)GetTime();
            SetShaderValue(shader, timeLoc, time);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // The clear texture, which the shader paints and moves
                BeginShaderMode(shader);
                    DrawTexture(texture, 0, 0, Color.White);
                EndShaderMode();

                DrawText("BACKGROUND is PAINTED and ANIMATED on SHADER!", 10, 10, 20, Color.Maroon);

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadTexture(texture);

        CloseWindow();
    }
}
