// raylib's shapes_triangle_strip example, Copyright (c) 2025 Jopestpe (@jopestpe), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesTriangleStrip
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] triangle strip");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Vector2[] points = new Vector2[122];
        Vector2 center = new((screenWidth/2.0f) - 125.0f, screenHeight/2.0f);
        float segments = 6.0f;
        float insideRadius = 100.0f;
        float outsideRadius = 150.0f;
        bool outline = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            int pointCount = (int)(segments);
            float angleStep = (360.0f/pointCount)*(MathF.PI/180);

            for (int i = 0, i2 = 0; i < pointCount; i++, i2 += 2)
            {
                float angle1 = i*angleStep;
                points[i2] = new Vector2(center.X + MathF.Cos(angle1)*insideRadius, center.Y + MathF.Sin(angle1)*insideRadius);
                float angle2 = angle1 + angleStep/2.0f;
                points[i2 + 1] = new Vector2(center.X + MathF.Cos(angle2)*outsideRadius, center.Y + MathF.Sin(angle2)*outsideRadius);
            }

            points[pointCount*2] = points[0];
            points[pointCount*2 + 1] = points[1];

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < pointCount; i++)
                {
                    Vector2 a = points[i*2];
                    Vector2 b = points[i*2 + 1];
                    Vector2 c = points[i*2 + 2];
                    Vector2 d = points[i*2 + 3];

                    float angle1 = i*angleStep;
                    DrawTriangle(c, b, a, ColorFromHSV(angle1*(180/MathF.PI), 1.0f, 1.0f));
                    DrawTriangle(d, b, c, ColorFromHSV((angle1 + angleStep/2)*(180/MathF.PI), 1.0f, 1.0f));

                    if (outline)
                    {
                        DrawTriangleLines(a, b, c, Color.Black);
                        DrawTriangleLines(c, b, d, Color.Black);
                    }
                }

                DrawLine(580, 0, 580, GetScreenHeight(), new Color(218, 218, 218, 255));
                DrawRectangle(580, 0, GetScreenWidth(), GetScreenHeight(), new Color(232, 232, 232, 255));

                // raygui's controls, as ImGui's, where raygui places them.
                ImGui.SetNextWindowPos(new Vector2(640 - 90, 40));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Segments", ref segments, 6.0f, 60.0f, "%.2f");
                ImGui.Checkbox("Outline", ref outline);
                ImGui.End();

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
