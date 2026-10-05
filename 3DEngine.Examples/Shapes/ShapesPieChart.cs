// raylib's shapes_pie_chart example, Copyright (c) 2025 Gideon Serfontein (@GideonSerf), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesPieChart
{
    private const int MAX_PIE_SLICES = 10;
    private const float DEG2RAD = MathF.PI/180.0f;
    private const float RAD2DEG = 180.0f/MathF.PI;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] pie chart");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        int sliceCount = 7;
        float donutInnerRadius = 25.0f;
        float[] values = [300.0f, 100.0f, 450.0f, 350.0f, 600.0f, 380.0f, 750.0f, 0.0f, 0.0f, 0.0f];
        string[] labels = new string[MAX_PIE_SLICES];

        for (int i = 0; i < MAX_PIE_SLICES; i++) labels[i] = $"Slice {i + 1:00}";

        bool showValues = true;
        bool showPercentages = false;
        bool showDonut = false;
        int hoveredSlice = -1;

        const int panelWidth = 270;
        const int panelMargin = 5;

        Vector2 panelPos = new((float)screenWidth - panelMargin - panelWidth, (float)panelMargin);
        Rectangle panelRect = new(panelPos.X, panelPos.Y, (float)panelWidth, (float)screenHeight - 2.0f*panelMargin);

        Rectangle canvas = new(0, 0, panelPos.X, (float)screenHeight);
        Vector2 center = new(canvas.Width/2.0f, canvas.Height/2.0f);
        const float radius = 205.0f;

        float totalValue = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            totalValue = 0.0f;
            for (int i = 0; i < sliceCount; i++) totalValue += values[i];

            hoveredSlice = -1;
            Vector2 mousePos = GetMousePosition();
            if (CheckCollisionPointRec(mousePos, canvas))
            {
                float dx = mousePos.X - center.X;
                float dy = mousePos.Y - center.Y;
                float distance = MathF.Sqrt(dx*dx + dy*dy);

                if (distance <= radius)
                {
                    float angle = MathF.Atan2(dy, dx)*RAD2DEG;
                    if (angle < 0) angle += 360;

                    float currentAngle = 0.0f;
                    for (int i = 0; i < sliceCount; i++)
                    {
                        float sweep = (totalValue > 0)? (values[i]/totalValue)*360.0f : 0.0f;

                        if ((angle >= currentAngle) && (angle < (currentAngle + sweep)))
                        {
                            hoveredSlice = i;
                            break;
                        }

                        currentAngle += sweep;
                    }
                }
            }

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                float startAngle = 0.0f;
                for (int i = 0; i < sliceCount; i++)
                {
                    float sweepAngle = (totalValue > 0)? (values[i]/totalValue)*360.0f : 0.0f;
                    float midAngle = startAngle + sweepAngle/2.0f;

                    Color color = ColorFromHSV((float)i/sliceCount*360.0f, 0.75f, 0.9f);
                    float currentRadius = radius;

                    // The slice under the pointer pops out.
                    if (i == hoveredSlice) currentRadius += 20.0f;

                    DrawCircleSector(center, currentRadius, startAngle, startAngle + sweepAngle, 120, color);

                    if (values[i] > 0)
                    {
                        string labelText;
                        if (showValues && showPercentages) labelText = $"{values[i]:0.0} ({(values[i]/totalValue)*100.0f:0}%)";
                        else if (showValues) labelText = $"{values[i]:0.0}";
                        else if (showPercentages) labelText = $"{(values[i]/totalValue)*100.0f:0}%";
                        else labelText = "";

                        Vector2 textSize = MeasureTextEx(GetFontDefault(), labelText, 20, 1);
                        float labelRadius = radius*0.7f;
                        Vector2 labelPos = new(center.X + MathF.Cos(midAngle*DEG2RAD)*labelRadius - textSize.X/2.0f,
                            center.Y + MathF.Sin(midAngle*DEG2RAD)*labelRadius - textSize.Y/2.0f);
                        DrawText(labelText, (int)labelPos.X, (int)labelPos.Y, 20, Color.White);
                    }

                    // A circle over the middle makes the donut, as raylib's does.
                    if (showDonut) DrawCircleV(center, donutInnerRadius, Color.RayWhite);

                    startAngle += sweepAngle;
                }

                DrawRectangleRec(panelRect, Fade(Color.LightGray, 0.5f));
                DrawRectangleLinesEx(panelRect, 1.0f, Color.Gray);

                // raygui's controls, as ImGui's, where raygui places them, the slices' editors in a
                // child window that scrolls as raygui's scroll panel does.
                ImGui.SetNextWindowPos(panelPos);
                ImGui.SetNextWindowSize(new Vector2(panelRect.Width, panelRect.Height));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);
                ImGui.Indent(12);
                ImGui.SetNextItemWidth(125);
                if (ImGui.InputInt("Slices", ref sliceCount)) sliceCount = Math.Clamp(sliceCount, 1, MAX_PIE_SLICES);
                ImGui.Checkbox("Show Values", ref showValues);
                ImGui.Checkbox("Show Percentages", ref showPercentages);
                ImGui.Checkbox("Make Donut", ref showDonut);

                // raylib's own disables the inner radius while the donut shows, and so does this.
                ImGui.BeginDisabled(showDonut);
                ImGui.SetNextItemWidth(panelRect.Width - 130);
                ImGui.SliderFloat("Inner Radius", ref donutInnerRadius, 5.0f, radius - 10.0f, "");
                ImGui.EndDisabled();
                ImGui.Unindent(12);

                ImGui.Separator();

                ImGui.BeginChild("##slices", Vector2.Zero, ImGuiChildFlags.Borders);
                for (int i = 0; i < sliceCount; i++)
                {
                    ImGui.PushID(i);
                    Color color = ColorFromHSV((float)i/sliceCount*360.0f, 0.75f, 0.9f);
                    ImGui.ColorButton("##color", color.ToVector4(), ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.NoDragDrop, new Vector2(20, 20));
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(75);
                    ImGui.InputText("##label", ref labels[i], 32);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(110);
                    ImGui.SliderFloat("##value", ref values[i], 0.0f, 1000.0f, "");
                    ImGui.PopID();
                }
                ImGui.EndChild();

                ImGui.End();

            EndDrawing();
        }

        CloseWindow();
    }
}
