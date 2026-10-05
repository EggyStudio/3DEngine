// raylib's shapes_kaleidoscope example, Copyright (c) 2025 Hugo ARNAL (@hugoarnal) and Ramon Santamaria
// (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesKaleidoscope
{
    private const int MAX_DRAW_LINES = 8192;
    private const float DEG2RAD = MathF.PI/180.0f;

    private struct Line
    {
        public Vector2 start;
        public Vector2 end;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] kaleidoscope");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Line[] lines = new Line[MAX_DRAW_LINES];

        int symmetry = 6;
        float angle = 360.0f/(float)symmetry;
        float thickness = 3.0f;
        Rectangle resetButtonRec = new(screenWidth - 55.0f, 5.0f, 50, 25);
        Rectangle backButtonRec = new(screenWidth - 55.0f, screenHeight - 30.0f, 25, 25);
        Rectangle nextButtonRec = new(screenWidth - 30.0f, screenHeight - 30.0f, 25, 25);
        Vector2 mousePos = Vector2.Zero;
        Vector2 prevMousePos = Vector2.Zero;
        Vector2 scaleVector = new(1.0f, -1.0f);
        Vector2 offset = new((float)screenWidth/2.0f, (float)screenHeight/2.0f);

        Camera2D camera = new(offset, Vector2.Zero, 0.0f, 1.0f);

        int currentLineCounter = 0;
        int totalLineCounter = 0;
        bool resetButtonClicked = false;
        bool backButtonClicked = false;
        bool nextButtonClicked = false;

        SetTargetFPS(20);

        while (!WindowShouldClose())
        {
            prevMousePos = mousePos;
            mousePos = GetMousePosition();

            Vector2 lineStart = mousePos - offset;
            Vector2 lineEnd = prevMousePos - offset;

            if (IsMouseButtonDown(MouseButton.Left)
                && !CheckCollisionPointRec(mousePos, resetButtonRec)
                && !CheckCollisionPointRec(mousePos, backButtonRec)
                && !CheckCollisionPointRec(mousePos, nextButtonRec))
            {
                for (int s = 0; (s < symmetry) && (totalLineCounter < (MAX_DRAW_LINES - 1)); s++)
                {
                    lineStart = Vector2Rotate(lineStart, angle*DEG2RAD);
                    lineEnd = Vector2Rotate(lineEnd, angle*DEG2RAD);

                    // The line under the pointer
                    lines[totalLineCounter].start = lineStart;
                    lines[totalLineCounter].end = lineEnd;

                    // and its reflection.
                    lines[totalLineCounter + 1].start = lineStart*scaleVector;
                    lines[totalLineCounter + 1].end = lineEnd*scaleVector;

                    totalLineCounter += 2;
                    currentLineCounter = totalLineCounter;
                }
            }

            if (resetButtonClicked)
            {
                Array.Clear(lines);
                currentLineCounter = 0;
                totalLineCounter = 0;
            }

            if (backButtonClicked && (currentLineCounter > 0))
            {
                currentLineCounter -= 1;
            }

            if (nextButtonClicked && (currentLineCounter < MAX_DRAW_LINES) && ((currentLineCounter + 1) <= totalLineCounter))
            {
                currentLineCounter += 1;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);
                BeginMode2D(camera);

                    for (int s = 0; s < symmetry; s++)
                    {
                        for (int i = 0; i < currentLineCounter; i += 2)
                        {
                            DrawLineEx(lines[i].start, lines[i].end, thickness, Color.Black);
                            DrawLineEx(lines[i + 1].start, lines[i + 1].end, thickness, Color.Black);
                        }
                    }

                EndMode2D();

                // raygui's buttons, as ImGui's, where raygui places them.
                ImGui.BeginDisabled((currentLineCounter - 1) < 0);
                backButtonClicked = Button(backButtonRec, "<");
                ImGui.EndDisabled();

                ImGui.BeginDisabled((currentLineCounter + 1) > totalLineCounter);
                nextButtonClicked = Button(nextButtonRec, ">");
                ImGui.EndDisabled();
                resetButtonClicked = Button(resetButtonRec, "Reset");

                DrawText($"LINES: {currentLineCounter}/{MAX_DRAW_LINES}", 10, screenHeight - 30, 20, Color.Maroon);
                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }

    // raygui's button: an ImGui window of its own, without padding or a background, holding a button
    // as large as the rectangle.
    private static bool Button(Rectangle bounds, string text)
    {
        ImGui.SetNextWindowPos(new Vector2(bounds.X, bounds.Y));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.Begin("##" + text, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.PopStyleVar();
        bool pressed = ImGui.Button(text, new Vector2(bounds.Width, bounds.Height));
        ImGui.End();
        return pressed;
    }
}
