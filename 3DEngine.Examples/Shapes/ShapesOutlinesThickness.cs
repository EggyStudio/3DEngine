// raylib's shapes_outlines_thickness example, Copyright (c) 2026 Matthew Roush (@MatthewRoush), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesOutlinesThickness
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] outlines thickness");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        float thick = 5.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // raygui's controls, as ImGui's, where raygui places them.
                ImGui.SetNextWindowPos(new Vector2(290 - 90, 50));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.SetNextItemWidth(220);
                ImGui.SliderFloat("Thickness", ref thick, -30.0f, 30.0f, "%.2f");
                ImGui.End();

                DrawRectangle(35, 180, 220, 220, Color.LightGray);
                DrawRectangleLinesEx(new Rectangle(35, 180, 220, 220), thick, Color.Blue);
                DrawText("DrawRectangleLinesEx()", 35, 160, 10, Color.Black);

                DrawRectangleRounded(new Rectangle(290, 180, 220, 220), 0.2f, 9, Color.LightGray);
                DrawRectangleRoundedLinesEx(new Rectangle(290, 180, 220, 220), 0.2f, 9, thick, Color.Blue);
                DrawText("DrawRectangleRoundedLinesEx()", 290, 160, 10, Color.Black);

                DrawCircle(655, 290, 110, Color.LightGray);
                DrawCircleLinesEx(new Vector2(655, 290), 110, thick, Color.Blue);
                DrawText("DrawCircleLinesEx()", 545, 160, 10, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }
}
