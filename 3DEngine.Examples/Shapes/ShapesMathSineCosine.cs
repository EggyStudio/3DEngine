// raylib's shapes_math_sine_cosine example, Copyright (c) 2025 Jopestpe (@jopestpe), under the zlib
// license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesMathSineCosine
{
    private const int WAVE_POINTS = 36;
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] math sine cosine");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Vector2[] sinePoints = new Vector2[WAVE_POINTS];
        Vector2[] cosPoints = new Vector2[WAVE_POINTS];
        Vector2 center = new((screenWidth/2.0f) - 30.0f, screenHeight/2.0f);
        Rectangle start = new(20.0f, screenHeight - 120.0f, 200.0f, 100.0f);
        float radius = 130.0f;
        float angle = 0.0f;
        bool pause = false;

        for (int i = 0; i < WAVE_POINTS; i++)
        {
            float t = i/(float)(WAVE_POINTS - 1);
            float currentAngle = t*360.0f*DEG2RAD;
            sinePoints[i] = new Vector2(start.X + t*start.Width, start.Y + start.Height/2.0f - MathF.Sin(currentAngle)*(start.Height/2.0f));
            cosPoints[i] = new Vector2(start.X + t*start.Width, start.Y + start.Height/2.0f - MathF.Cos(currentAngle)*(start.Height/2.0f));
        }

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float angleRad = angle*DEG2RAD;
            float cosRad = MathF.Cos(angleRad);
            float sinRad = MathF.Sin(angleRad);

            Vector2 point = new(center.X + cosRad*radius, center.Y - sinRad*radius);
            Vector2 limitMin = new(center.X - radius, center.Y - radius);
            Vector2 limitMax = new(center.X + radius, center.Y + radius);

            float complementary = 90.0f - angle;
            float supplementary = 180.0f - angle;
            float explementary = 360.0f - angle;

            float tangent = Math.Clamp(MathF.Tan(angleRad), -10.0f, 10.0f);
            float cotangent = (MathF.Abs(tangent) > 0.001f) ? Math.Clamp(1.0f/tangent, -radius, radius) : 0.0f;
            Vector2 tangentPoint = new(center.X + radius, center.Y - tangent*radius);
            Vector2 cotangentPoint = new(center.X + cotangent*radius, center.Y - radius);

            angle = Wrap(angle + (!pause ? 1.0f : 0.0f), 0.0f, 360.0f);

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                // Cotangent (orange)
                DrawLineEx(new Vector2(center.X, limitMin.Y), new Vector2(cotangentPoint.X, limitMin.Y), 2.0f, Color.Orange);
                DrawLineDashed(center, cotangentPoint, 10, 4, Color.Orange);

                // Side background
                DrawLine(580, 0, 580, GetScreenHeight(), new Color(218, 218, 218, 255));
                DrawRectangle(580, 0, GetScreenWidth(), GetScreenHeight(), new Color(232, 232, 232, 255));

                // Base circle and axes
                DrawCircleLinesV(center, radius, Color.Gray);
                DrawLineEx(new Vector2(center.X, limitMin.Y), new Vector2(center.X, limitMax.Y), 1.0f, Color.Gray);
                DrawLineEx(new Vector2(limitMin.X, center.Y), new Vector2(limitMax.X, center.Y), 1.0f, Color.Gray);

                // Wave graph axes
                DrawLineEx(new Vector2(start.X, start.Y), new Vector2(start.X, start.Y + start.Height), 2.0f, Color.Gray);
                DrawLineEx(new Vector2(start.X + start.Width, start.Y), new Vector2(start.X + start.Width, start.Y + start.Height), 2.0f, Color.Gray);
                DrawLineEx(new Vector2(start.X, start.Y + start.Height/2), new Vector2(start.X + start.Width, start.Y + start.Height/2), 2.0f, Color.Gray);

                // Wave graph axis labels
                DrawText("1", (int)start.X - 8, (int)start.Y, 6, Color.Gray);
                DrawText("0", (int)start.X - 8, (int)start.Y + (int)start.Height/2 - 6, 6, Color.Gray);
                DrawText("-1", (int)start.X - 12, (int)start.Y + (int)start.Height - 8, 6, Color.Gray);
                DrawText("0", (int)start.X - 2, (int)start.Y + (int)start.Height + 4, 6, Color.Gray);
                DrawText("360", (int)start.X + (int)start.Width - 8, (int)start.Y + (int)start.Height + 4, 6, Color.Gray);

                // Sine (red, vertical)
                DrawLineEx(new Vector2(center.X, center.Y), new Vector2(center.X, point.Y), 2.0f, Color.Red);
                DrawLineDashed(new Vector2(point.X, center.Y), new Vector2(point.X, point.Y), 10, 4, Color.Red);
                DrawText($"Sine {sinRad:0.00}", 640, 190, 6, Color.Red);
                DrawCircleV(new Vector2(start.X + (angle/360.0f)*start.Width, start.Y + ((-sinRad + 1)*start.Height/2.0f)), 4.0f, Color.Red);
                DrawSplineLinear(sinePoints, 1.0f, Color.Red);

                // Cosine (blue, horizontal)
                DrawLineEx(new Vector2(center.X, center.Y), new Vector2(point.X, center.Y), 2.0f, Color.Blue);
                DrawLineDashed(new Vector2(center.X, point.Y), new Vector2(point.X, point.Y), 10, 4, Color.Blue);
                DrawText($"Cosine {cosRad:0.00}", 640, 210, 6, Color.Blue);
                DrawCircleV(new Vector2(start.X + (angle/360.0f)*start.Width, start.Y + ((-cosRad + 1)*start.Height/2.0f)), 4.0f, Color.Blue);
                DrawSplineLinear(cosPoints, 1.0f, Color.Blue);

                // Tangent (purple)
                DrawLineEx(new Vector2(limitMax.X, center.Y), new Vector2(limitMax.X, tangentPoint.Y), 2.0f, Color.Purple);
                DrawLineDashed(center, tangentPoint, 10, 4, Color.Purple);
                DrawText($"Tangent {tangent:0.00}", 640, 230, 6, Color.Purple);

                // Cotangent (orange)
                DrawText($"Cotangent {cotangent:0.00}", 640, 250, 6, Color.Orange);

                // Complementary angle (beige)
                DrawCircleSectorLines(center, radius*0.6f, -angle, -90.0f, 36, Color.Beige);
                DrawText($"Complementary  {complementary:0}°", 640, 150, 6, Color.Beige);

                // Supplementary angle (dark blue)
                DrawCircleSectorLines(center, radius*0.5f, -angle, -180.0f, 36, Color.DarkBlue);
                DrawText($"Supplementary  {supplementary:0}°", 640, 130, 6, Color.DarkBlue);

                // Explementary angle (pink)
                DrawCircleSectorLines(center, radius*0.4f, -angle, -360.0f, 36, Color.Pink);
                DrawText($"Explementary  {explementary:0}°", 640, 170, 6, Color.Pink);

                // The current angle's arc (lime), radius (black) and end (black)
                DrawCircleSectorLines(center, radius*0.7f, -angle, 0.0f, 36, Color.Lime);
                DrawLineEx(new Vector2(center.X, center.Y), point, 2.0f, Color.Black);
                DrawCircleV(point, 4.0f, Color.Black);

                // raygui's controls, as ImGui's, where raygui places them. raygui's toggle is a
                // button that stays down, and its group box a bordered child window with its
                // title above it.
                ImGui.SetNextWindowPos(new Vector2(580, 0));
                ImGui.SetNextWindowSize(new Vector2(220, screenHeight));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings);

                ImGui.SetCursorScreenPos(new Vector2(640, 40));
                ImGui.PushStyleColor(ImGuiCol.Text, Color.Lime.ToVector4());
                ImGui.SetNextItemWidth(120);
                ImGui.SliderFloat("Angle", ref angle, 0.0f, 360.0f, "%.0f°");
                ImGui.PopStyleColor();

                ImGui.SetCursorScreenPos(new Vector2(640, 70));
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(pause ? ImGuiCol.ButtonActive : ImGuiCol.Button));
                if (ImGui.Button("Pause", new Vector2(120, 20))) pause = !pause;
                ImGui.PopStyleColor();

                ImGui.SetCursorScreenPos(new Vector2(620, 92));
                ImGui.TextUnformatted("Angle Values");
                ImGui.SetCursorScreenPos(new Vector2(620, 110));
                ImGui.BeginChild("##values", new Vector2(140, 170), ImGuiChildFlags.Borders);
                ImGui.EndChild();

                ImGui.End();

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
