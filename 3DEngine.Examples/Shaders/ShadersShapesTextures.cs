// raylib's shaders_shapes_textures example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersShapesTextures
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] shapes textures");

        Texture2D fudesumi = LoadTexture("resources/fudesumi.png");

        // raylib's grayscale.fs, written in Slang. A shader with a fragment stage alone draws with
        // the engine's own vertex stage, as raylib's does given no vertex shader.
        Shader shader = LoadShader("resources/shaders/slang/grayscale.slang");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // The default shader,
                DrawText("USING DEFAULT SHADER", 20, 40, 10, Color.Red);

                DrawCircle(80, 120, 35, Color.DarkBlue);
                DrawCircleGradient(new Vector2(80.0f, 220.0f), 60, Color.Green, Color.SkyBlue);
                DrawCircleLines(80, 340, 80, Color.DarkBlue);

                // the program's own for the shapes and textures after it,
                BeginShaderMode(shader);

                    DrawText("USING CUSTOM SHADER", 190, 40, 10, Color.Red);

                    DrawRectangle(250 - 60, 90, 120, 60, Color.Red);
                    DrawRectangleGradientH(250 - 90, 170, 180, 130, Color.Maroon, Color.Gold);
                    DrawRectangleLines(250 - 40, 320, 80, 60, Color.Orange);

                // and the default again.
                EndShaderMode();

                DrawText("USING DEFAULT SHADER", 370, 40, 10, Color.Red);

                DrawTriangle(new Vector2(430, 80), new Vector2(430 - 60, 150), new Vector2(430 + 60, 150), Color.Violet);

                DrawTriangleLines(new Vector2(430, 160), new Vector2(430 - 20, 230), new Vector2(430 + 20, 230), Color.DarkBlue);

                DrawPoly(new Vector2(430, 320), 6, 80, 0, Color.Brown);

                BeginShaderMode(shader);

                    DrawTexture(fudesumi, 500, -30, Color.White);

                EndShaderMode();

                DrawText("(c) Fudesumi sprite by Eiden Marsal", 380, screenHeight - 20, 10, Color.Gray);

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadTexture(fudesumi);

        CloseWindow();
    }
}
