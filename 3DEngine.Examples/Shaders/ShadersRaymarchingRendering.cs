// raylib's shaders_raymarching_rendering example, Copyright (c) 2018-2025 Ramon Santamaria (@raysan5),
// under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersRaymarchingRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.WindowResizable);
        InitWindow(screenWidth, screenHeight, "[shaders] raymarching rendering");

        Camera3D camera = new(new Vector3(2.5f, 2.5f, 3.0f), new Vector3(0.0f, 0.0f, 0.7f), Vector3.UnitY, 65.0f, CameraProjection.Perspective);

        // raylib's raymarching.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/raymarching.slang");

        int viewEyeLoc = GetShaderLocation(shader, "viewEye");
        int viewCenterLoc = GetShaderLocation(shader, "viewCenter");
        int runTimeLoc = GetShaderLocation(shader, "runTime");
        int resolutionLoc = GetShaderLocation(shader, "resolution");

        Vector2 resolution = new((float)screenWidth, (float)screenHeight);
        SetShaderValue(shader, resolutionLoc, resolution);

        float runTime = 0.0f;

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.FirstPerson);

            float deltaTime = GetFrameTime();
            runTime += deltaTime;

            SetShaderValue(shader, viewEyeLoc, camera.Position);
            SetShaderValue(shader, viewCenterLoc, camera.Target);
            SetShaderValue(shader, runTimeLoc, runTime);

            if (IsWindowResized())
            {
                resolution = new Vector2((float)GetScreenWidth(), (float)GetScreenHeight());
                SetShaderValue(shader, resolutionLoc, resolution);
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // A white rectangle over the window, the picture made by the shader's rays
                BeginShaderMode(shader);
                    DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Color.White);
                EndShaderMode();

                DrawText("(c) Raymarching shader by Iñigo Quilez. MIT License.", GetScreenWidth() - 280, GetScreenHeight() - 20, 10, Color.Black);

            EndDrawing();
        }

        UnloadShader(shader);

        CloseWindow();
    }
}
