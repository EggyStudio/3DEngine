// raylib's shaders_eratosthenes_sieve example, Copyright (c) 2019-2025 ProfJski (@ProfJski) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersEratosthenesSieve
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] eratosthenes sieve");

        RenderTexture2D target = LoadRenderTexture(screenWidth, screenHeight);

        // raylib's eratosthenes.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/eratosthenes.slang");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // The shader does it all, drawn across a blank texture. A rectangle would not do,
            // its texture coordinates being those of the white pixel shapes are drawn with.
            BeginTextureMode(target);
                ClearBackground(Color.Black);
                DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Color.Black);
            EndTextureMode();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginShaderMode(shader);

                    // Drawn turned with raylib's negative height, so the shader's coordinates
                    // count from the bottom as raylib's do. The target is blank, so nothing of
                    // it shows turned.
                    DrawTextureRec(target.Texture, new Rectangle(0, 0, (float)target.Texture.Width, (float)-target.Texture.Height), Vector2.Zero, Color.White);

                EndShaderMode();

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadRenderTexture(target);

        CloseWindow();
    }
}
