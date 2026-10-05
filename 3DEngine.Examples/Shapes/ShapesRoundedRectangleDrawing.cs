// raylib's shapes_rounded_rectangle_drawing example, Copyright (c) 2018-2025 Vlad Adrian (@demizdor) and Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesRoundedRectangleDrawing
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] rounded rectangle drawing");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        float roundness = 0.2f;
        float width = 200.0f;
        float height = 100.0f;
        float segments = 0.0f;
        float lineThick = 1.0f;

        bool drawRect = false;
        bool drawRoundedRect = true;
        bool drawRoundedLines = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Rectangle rec = new(((float)GetScreenWidth() - width - 250)/2, (GetScreenHeight() - height)/2.0f, (float)width, (float)height);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawLine(560, 0, 560, GetScreenHeight(), Fade(Color.LightGray, 0.6f));
                DrawRectangle(560, 0, GetScreenWidth() - 500, GetScreenHeight(), Fade(Color.LightGray, 0.3f));

                if (drawRect) DrawRectangleRec(rec, Fade(Color.Gold, 0.6f));
                if (drawRoundedRect) DrawRectangleRounded(rec, roundness, (int)segments, Fade(Color.Maroon, 0.2f));
                if (drawRoundedLines) DrawRectangleRoundedLinesEx(rec, roundness, (int)segments, lineThick, Fade(Color.Maroon, 0.4f));

                // raygui's controls, as ImGui's, where raygui places them.
                ImGui.SetNextWindowPos(new Vector2(640 - 90, 40));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.SetNextItemWidth(105);
                ImGui.SliderFloat("Width", ref width, 0, (float)GetScreenWidth() - 300, "%.2f");
                ImGui.SetNextItemWidth(105);
                ImGui.SliderFloat("Height", ref height, 0, (float)GetScreenHeight() - 50, "%.2f");
                ImGui.SetNextItemWidth(105);
                ImGui.SliderFloat("Roundness", ref roundness, 0.0f, 1.0f, "%.2f");
                ImGui.SetNextItemWidth(105);
                ImGui.SliderFloat("Thickness", ref lineThick, 0, 20, "%.2f");
                ImGui.SetNextItemWidth(105);
                ImGui.SliderFloat("Segments", ref segments, 0, 60, "%.2f");

                ImGui.Checkbox("DrawRoundedRect", ref drawRoundedRect);
                ImGui.Checkbox("DrawRoundedLines", ref drawRoundedLines);
                ImGui.Checkbox("DrawRect", ref drawRect);
                ImGui.End();

                DrawText($"MODE: {((segments >= 4)? "MANUAL" : "AUTO")}", 640, 280, 10, (segments >= 4)? Color.Maroon : Color.DarkGray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
