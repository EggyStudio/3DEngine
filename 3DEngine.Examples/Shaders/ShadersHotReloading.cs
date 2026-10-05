// raylib's shaders_hot_reloading example, Copyright (c) 2020-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Globalization;
using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersHotReloading
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] hot reloading");

        // raylib's reload.fs, written in Slang. raylib's GetFileModTime is C#'s File here, the
        // file found beside the program as the shader is.
        const string fragShaderFileName = "resources/shaders/slang/reload.slang";
        string fragShaderPath = Path.Combine(AppContext.BaseDirectory, fragShaderFileName);
        DateTime fragShaderFileModTime = File.GetLastWriteTime(fragShaderPath);

        Shader shader = LoadShader(fragShaderFileName);

        int resolutionLoc = GetShaderLocation(shader, "resolution");
        int mouseLoc = GetShaderLocation(shader, "mouse");
        int timeLoc = GetShaderLocation(shader, "time");

        Vector2 resolution = new((float)screenWidth, (float)screenHeight);
        SetShaderValue(shader, resolutionLoc, resolution);

        float totalTime = 0.0f;
        bool shaderAutoReloading = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            totalTime += GetFrameTime();
            Vector2 mouse = GetMousePosition();

            SetShaderValue(shader, timeLoc, totalTime);
            SetShaderValue(shader, mouseLoc, mouse);

            // A click, or every frame with automatic reloading on, loads the shader again when
            // its file has changed.
            if (shaderAutoReloading || IsMouseButtonPressed(MouseButton.Left))
            {
                DateTime currentFragShaderModTime = File.GetLastWriteTime(fragShaderPath);

                if (currentFragShaderModTime != fragShaderFileModTime)
                {
                    Shader updatedShader = LoadShader(fragShaderFileName);

                    // A shader that did not compile is not valid, where raylib's is its default.
                    if (IsShaderValid(updatedShader))
                    {
                        UnloadShader(shader);
                        shader = updatedShader;

                        resolutionLoc = GetShaderLocation(shader, "resolution");
                        mouseLoc = GetShaderLocation(shader, "mouse");
                        timeLoc = GetShaderLocation(shader, "time");

                        SetShaderValue(shader, resolutionLoc, resolution);
                    }

                    fragShaderFileModTime = currentFragShaderModTime;
                }
            }

            if (IsKeyPressed(Key.A)) shaderAutoReloading = !shaderAutoReloading;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // A white rectangle over the window, the picture made by the shader
                BeginShaderMode(shader);
                    DrawRectangle(0, 0, screenWidth, screenHeight, Color.White);
                EndShaderMode();

                DrawText($"PRESS [A] to TOGGLE SHADER AUTOLOADING: {(shaderAutoReloading? "AUTO" : "MANUAL")}", 10, 10, 10, shaderAutoReloading? Color.Red : Color.Black);
                if (!shaderAutoReloading) DrawText("MOUSE CLICK to SHADER RE-LOADING", 10, 30, 10, Color.Black);

                // asctime's form, as raylib prints it
                string modified = string.Create(CultureInfo.InvariantCulture, $"{fragShaderFileModTime:ddd MMM} {fragShaderFileModTime.Day,2} {fragShaderFileModTime:HH:mm:ss yyyy}");
                DrawText($"Shader last modification: {modified}", 10, 430, 10, Color.Black);

            EndDrawing();
        }

        UnloadShader(shader);

        CloseWindow();
    }
}
