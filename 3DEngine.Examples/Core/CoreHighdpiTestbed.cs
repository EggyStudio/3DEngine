// raylib's core_highdpi_testbed example, Copyright (c) 2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreHighdpiTestbed
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.WindowResizable | ConfigFlags.WindowHighdpi);
        InitWindow(screenWidth, screenHeight, "[core] highdpi testbed");

        Vector2 scaleDpi = GetWindowScaleDPI();
        Vector2 mousePos = GetMousePosition();
        int currentMonitor = GetCurrentMonitor();
        Vector2 windowPos = GetWindowPosition();

        int gridSpacing = 40;   // Grid spacing in pixels

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            mousePos = GetMousePosition();
            currentMonitor = GetCurrentMonitor();
            scaleDpi = GetWindowScaleDPI();
            windowPos = GetWindowPosition();

            if (IsKeyPressed(Key.Space)) ToggleBorderlessWindowed();
            if (IsKeyPressed(Key.F)) ToggleFullscreen();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // Draw grid
                for (int h = 0; h < GetScreenHeight()/gridSpacing + 1; h++)
                {
                    DrawText($"{h*gridSpacing:00}", 4, h*gridSpacing - 4, 10, Color.Gray);
                    DrawLine(24, h*gridSpacing, GetScreenWidth(), h*gridSpacing, Color.LightGray);
                }
                for (int v = 0; v < GetScreenWidth()/gridSpacing + 1; v++)
                {
                    DrawText($"{v*gridSpacing:00}", v*gridSpacing - 10, 4, 10, Color.Gray);
                    DrawLine(v*gridSpacing, 20, v*gridSpacing, GetScreenHeight(), Color.LightGray);
                }

                // Draw UI info
                DrawText($"CURRENT MONITOR: {currentMonitor + 1}/{GetMonitorCount()} ({GetMonitorWidth(currentMonitor)}x{GetMonitorHeight(currentMonitor)})", 50, 50, 20, Color.DarkGray);
                DrawText($"WINDOW POSITION: {(int)windowPos.X}x{(int)windowPos.Y}", 50, 90, 20, Color.DarkGray);
                DrawText($"SCREEN SIZE: {GetScreenWidth()}x{GetScreenHeight()}", 50, 130, 20, Color.DarkGray);
                DrawText($"RENDER SIZE: {GetRenderWidth()}x{GetRenderHeight()}", 50, 170, 20, Color.DarkGray);
                DrawText($"SCALE FACTOR: {scaleDpi.X:0.00}x{scaleDpi.Y:0.00}", 50, 210, 20, Color.Gray);

                // Draw reference rectangles, top-left and bottom-right corners
                DrawRectangle(0, 0, 30, 60, Color.Red);
                DrawRectangle(GetScreenWidth() - 30, GetScreenHeight() - 60, 30, 60, Color.Blue);

                // Draw mouse position
                DrawCircleV(GetMousePosition(), 20, Color.Maroon);
                DrawRectangleRec(new Rectangle(mousePos.X - 25, mousePos.Y, 50, 2), Color.Black);
                DrawRectangleRec(new Rectangle(mousePos.X, mousePos.Y - 25, 2, 50), Color.Black);
                DrawText($"[{GetMouseX()},{GetMouseY()}]", (int)mousePos.X - 44,
                    (mousePos.Y > GetScreenHeight() - 60)? (int)mousePos.Y - 46 : (int)mousePos.Y + 30, 20, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }
}
