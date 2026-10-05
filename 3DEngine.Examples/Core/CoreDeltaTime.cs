// raylib's core_delta_time example, Copyright (c) 2025 Robin (@RobinsAviary), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreDeltaTime
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] delta time");

        int currentFps = 60;

        Vector2 deltaCircle = new(0, (float)screenHeight/3.0f);
        Vector2 frameCircle = new(0, (float)screenHeight*(2.0f/3.0f));

        const float speed = 10.0f;
        const float circleRadius = 32.0f;

        SetTargetFPS(currentFps);

        while (!WindowShouldClose())
        {
            float mouseWheel = GetMouseWheelMove();
            if (mouseWheel != 0)
            {
                currentFps += (int)mouseWheel;
                if (currentFps < 0) currentFps = 0;
                SetTargetFPS(currentFps);
            }

            deltaCircle.X += GetFrameTime()*6.0f*speed;

            frameCircle.X += 0.1f*speed;

            if (deltaCircle.X > screenWidth) deltaCircle.X = 0;
            if (frameCircle.X > screenWidth) frameCircle.X = 0;

            if (IsKeyPressed(Key.R))
            {
                deltaCircle.X = 0;
                frameCircle.X = 0;
            }

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                DrawCircleV(deltaCircle, circleRadius, Color.Red);
                DrawCircleV(frameCircle, circleRadius, Color.Blue);

                string fpsText;
                if (currentFps <= 0) fpsText = $"FPS: unlimited ({GetFPS()})";
                else fpsText = $"FPS: {GetFPS()} (target: {currentFps})";
                DrawText(fpsText, 10, 10, 20, Color.DarkGray);
                DrawText($"Frame time: {GetFrameTime()*1000.0f:00.00} ms", 10, 30, 20, Color.DarkGray);
                DrawText("Use the scroll wheel to change the fps limit, r to reset", 10, 50, 20, Color.DarkGray);

                DrawText("FUNC: x += GetFrameTime()*speed", 10, 90, 20, Color.Red);
                DrawText("FUNC: x += speed", 10, 240, 20, Color.Blue);

            EndDrawing();
        }

        CloseWindow();
    }
}
