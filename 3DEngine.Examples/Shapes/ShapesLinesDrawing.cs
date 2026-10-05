// raylib's shapes_lines_drawing example, Copyright (c) 2025 Robin (@RobinsAviary), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesLinesDrawing
{
    ﻿

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] lines drawing");

        bool startText = true;

        Vector2 mousePositionPrevious = GetMousePosition();

        RenderTexture2D canvas = LoadRenderTexture(screenWidth, screenHeight);

        float lineThickness = 8.0f;

        float lineHue = 0.0f;

        BeginTextureMode(canvas);
            ClearBackground(Color.RayWhite);
        EndTextureMode();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsMouseButtonPressed(MouseButton.Left) && startText) startText = false;

            if (IsMouseButtonPressed(MouseButton.Middle))
            {
                BeginTextureMode(canvas);
                    ClearBackground(Color.RayWhite);
                EndTextureMode();
            }

            bool leftButtonDown = IsMouseButtonDown(MouseButton.Left);
            bool rightButtonDown = IsMouseButtonDown(MouseButton.Right);

            if (leftButtonDown || rightButtonDown)
            {
                Color drawColor = Color.White;

                if (leftButtonDown)
                {
                    lineHue += Vector2.Distance(mousePositionPrevious, GetMousePosition())/3.0f;

                    while (lineHue >= 360.0f) lineHue -= 360.0f;

                    drawColor = ColorFromHSV(lineHue, 1.0f, 1.0f);
                }
                else if (rightButtonDown) drawColor = Color.RayWhite;

                BeginTextureMode(canvas);

                    DrawCircleV(mousePositionPrevious, lineThickness/2.0f, drawColor);
                    DrawCircleV(GetMousePosition(), lineThickness/2.0f, drawColor);
                    DrawLineEx(mousePositionPrevious, GetMousePosition(), lineThickness, drawColor);
                EndTextureMode();
            }

            lineThickness += GetMouseWheelMove();
            lineThickness = Math.Clamp(lineThickness, 1.0f, 500.0f);

            mousePositionPrevious = GetMousePosition();

            BeginDrawing();

                DrawTextureRec(canvas.Texture, new Rectangle(0.0f, 0.0f, (float)canvas.Texture.Width,(float)canvas.Texture.Height), Vector2.Zero, Color.White);

                if (!leftButtonDown) DrawCircleLinesV(GetMousePosition(), lineThickness/2.0f, new Color(127, 127, 127, 127));

                if (startText) DrawText("try clicking and dragging!", 275, 215, 20, Color.LightGray);

            EndDrawing();
        }

        UnloadRenderTexture(canvas);

        CloseWindow();
    }
}
