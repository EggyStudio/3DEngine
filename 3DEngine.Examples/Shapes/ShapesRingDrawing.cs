// raylib's shapes_ring_drawing example, Copyright (c) 2018-2025 Vlad Adrian (@demizdor) and Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesRingDrawing
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] ring drawing");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Vector2 center = new((GetScreenWidth() - 300)/2.0f, GetScreenHeight()/2.0f);

        float innerRadius = 80.0f;
        float outerRadius = 190.0f;

        float startAngle = 0.0f;
        float endAngle = 360.0f;
        float segments = 0.0f;

        bool drawRing = true;
        bool drawRingLines = false;
        bool drawCircleLines = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawLine(500, 0, 500, GetScreenHeight(), Fade(Color.LightGray, 0.6f));
                DrawRectangle(500, 0, GetScreenWidth() - 500, GetScreenHeight(), Fade(Color.LightGray, 0.3f));

                if (drawRing) DrawRing(center, innerRadius, outerRadius, startAngle, endAngle, (int)segments, Fade(Color.Maroon, 0.3f));
                if (drawRingLines) DrawRingLines(center, innerRadius, outerRadius, startAngle, endAngle, (int)segments, Fade(Color.Black, 0.4f));
                if (drawCircleLines) DrawCircleSectorLines(center, outerRadius, startAngle, endAngle, (int)segments, Fade(Color.Black, 0.4f));

                // raygui's controls, as ImGui's, where raygui places them.
                ImGui.SetNextWindowPos(new Vector2(600 - 90, 40));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("StartAngle", ref startAngle, -450, 450, "%.2f");
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("EndAngle", ref endAngle, -450, 450, "%.2f");

                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("InnerRadius", ref innerRadius, 0, 100, "%.2f");
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("OuterRadius", ref outerRadius, 0, 200, "%.2f");

                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Segments", ref segments, 0, 100, "%.2f");

                ImGui.Checkbox("Draw Ring", ref drawRing);
                ImGui.Checkbox("Draw RingLines", ref drawRingLines);
                ImGui.Checkbox("Draw CircleLines", ref drawCircleLines);
                ImGui.End();

                int minSegments = (int)MathF.Ceiling((endAngle - startAngle)/90);
                DrawText($"MODE: {((segments >= minSegments)? "MANUAL" : "AUTO")}", 600, 270, 10, (segments >= minSegments)? Color.Maroon : Color.DarkGray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
