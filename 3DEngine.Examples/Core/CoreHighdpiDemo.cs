// raylib's core_highdpi_demo example, Copyright (c) 2025 Jonathan Marler (@marler8997), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreHighdpiDemo
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.WindowHighdpi | ConfigFlags.WindowResizable);
        InitWindow(screenWidth, screenHeight, "[core] highdpi demo");
        SetWindowMinSize(450, 450);

        int logicalGridDescY = 120;
        int logicalGridLabelY = logicalGridDescY + 30;
        int logicalGridTop = logicalGridLabelY + 30;
        int logicalGridBottom = logicalGridTop + 80;
        int pixelGridTop = logicalGridBottom - 20;
        int pixelGridBottom = pixelGridTop + 80;
        int pixelGridLabelY = pixelGridBottom + 30;
        int pixelGridDescY = pixelGridLabelY + 30;
        int cellSize = 50;
        float cellSizePx = (float)cellSize;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            int monitorCount = GetMonitorCount();

            if ((monitorCount > 1) && IsKeyPressed(Key.N))
            {
                SetWindowMonitor((GetCurrentMonitor() + 1)%monitorCount);
            }

            int currentMonitor = GetCurrentMonitor();
            Vector2 dpiScale = GetWindowScaleDPI();
            cellSizePx = ((float)cellSize)/dpiScale.X;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                int windowCenter = GetScreenWidth()/2;
                DrawTextCenter($"Dpi Scale: {dpiScale.X:0.000000}", windowCenter, 30, 40, Color.DarkGray);
                DrawTextCenter($"Monitor: {currentMonitor+1}/{monitorCount} ([N] next monitor)", windowCenter, 70, 20, Color.LightGray);
                DrawTextCenter($"Window is {GetScreenWidth()} \"logical points\" wide", windowCenter, logicalGridDescY, 20, Color.Orange);

                bool odd = true;
                for (int i = cellSize; i < GetScreenWidth(); i += cellSize, odd = !odd)
                {
                    if (odd) DrawRectangle(i, logicalGridTop, cellSize, logicalGridBottom-logicalGridTop, Color.Orange);

                    DrawTextCenter($"{i}", i, logicalGridLabelY, 10, Color.LightGray);
                    DrawLine(i, logicalGridLabelY + 10, i, logicalGridBottom, Color.Gray);
                }

                odd = true;
                const int minTextSpace = 30;
                int lastTextX = -minTextSpace;
                for (int i = cellSize; i < GetRenderWidth(); i += cellSize, odd = !odd)
                {
                    int x = (int)(((float)i)/dpiScale.X);
                    if (odd) DrawRectangle(x, pixelGridTop, (int)cellSizePx, pixelGridBottom - pixelGridTop, new Color(0, 121, 241, 100));

                    DrawLine(x, pixelGridTop, (int)(((float)i)/dpiScale.X), pixelGridLabelY - 10, Color.Gray);

                    if ((x - lastTextX) >= minTextSpace)
                    {
                        DrawTextCenter($"{i}", x, pixelGridLabelY, 10, Color.LightGray);
                        lastTextX = x;
                    }
                }

                DrawTextCenter($"Window is {GetRenderWidth()} \"physical pixels\" wide", windowCenter, pixelGridDescY, 20, Color.Blue);

                const string text = "Can you see this?";
                Vector2 size = MeasureTextEx(GetFontDefault(), text, 20, 3);
                Vector2 pos = new(GetScreenWidth() - size.X - 5, GetScreenHeight() - size.Y - 5);
                DrawTextEx(GetFontDefault(), text, pos, 20, 3, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }

    private static void DrawTextCenter(string text, int x, int y, int fontSize, Color color)
    {
        Vector2 size = MeasureTextEx(GetFontDefault(), text, (float)fontSize, 3);
        Vector2 pos = new(x - size.X/2, y - size.Y/2);
        DrawTextEx(GetFontDefault(), text, pos, (float)fontSize, 3, color);
    }
}
