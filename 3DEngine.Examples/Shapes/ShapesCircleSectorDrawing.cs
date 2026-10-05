// raylib's shapes_circle_sector_drawing example, Copyright (c) 2018-2025 Vlad Adrian (@demizdor) and Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesCircleSectorDrawing
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] circle sector drawing");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Vector2 center = new((GetScreenWidth() - 300)/2.0f, GetScreenHeight()/2.0f);

        float outerRadius = 180.0f;
        float startAngle = 0.0f;
        float endAngle = 180.0f;
        float segments = 10.0f;
        float minSegments = 4;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawLine(500, 0, 500, GetScreenHeight(), Fade(Color.LightGray, 0.6f));
                DrawRectangle(500, 0, GetScreenWidth() - 500, GetScreenHeight(), Fade(Color.LightGray, 0.3f));

                DrawCircleSector(center, outerRadius, startAngle, endAngle, (int)segments, Fade(Color.Maroon, 0.3f));
                DrawCircleSectorLines(center, outerRadius, startAngle, endAngle, (int)segments, Fade(Color.Maroon, 0.6f));

                // raygui's controls, as ImGui's, where raygui places them.
                ImGui.SetNextWindowPos(new Vector2(600 - 90, 40));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("StartAngle", ref startAngle, 0, 720, "%.2f");
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("EndAngle", ref endAngle, 0, 720, "%.2f");

                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Radius", ref outerRadius, 0, 200, "%.2f");
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Segments", ref segments, 0, 100, "%.2f");
                ImGui.End();

                minSegments = MathF.Truncate(MathF.Ceiling((endAngle - startAngle)/90));
                DrawText($"MODE: {((segments >= minSegments)? "MANUAL" : "AUTO")}", 600, 200, 10, (segments >= minSegments)? Color.Maroon : Color.DarkGray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
