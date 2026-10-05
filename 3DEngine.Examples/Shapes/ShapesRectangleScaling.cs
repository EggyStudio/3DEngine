// raylib's shapes_rectangle_scaling example, Copyright (c) 2018-2025 Vlad Adrian (@demizdor) and Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesRectangleScaling
{
    private const int MOUSE_SCALE_MARK_SIZE = 12;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] rectangle scaling");

        Rectangle rec = new(100, 100, 200, 80);

        Vector2 mousePosition = default;

        bool mouseScaleReady = false;
        bool mouseScaleMode = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            mousePosition = GetMousePosition();

            if (CheckCollisionPointRec(mousePosition, new Rectangle(rec.X + rec.Width - MOUSE_SCALE_MARK_SIZE, rec.Y + rec.Height - MOUSE_SCALE_MARK_SIZE, MOUSE_SCALE_MARK_SIZE, MOUSE_SCALE_MARK_SIZE)))
            {
                mouseScaleReady = true;
                if (IsMouseButtonPressed(MouseButton.Left)) mouseScaleMode = true;
            }
            else mouseScaleReady = false;

            if (mouseScaleMode)
            {
                mouseScaleReady = true;

                rec = rec with { Width = (mousePosition.X - rec.X) };
                rec = rec with { Height = (mousePosition.Y - rec.Y) };

                if (rec.Width < MOUSE_SCALE_MARK_SIZE) rec = rec with { Width = MOUSE_SCALE_MARK_SIZE };
                if (rec.Height < MOUSE_SCALE_MARK_SIZE) rec = rec with { Height = MOUSE_SCALE_MARK_SIZE };

                if (rec.Width > (GetScreenWidth() - rec.X)) rec = rec with { Width = GetScreenWidth() - rec.X };
                if (rec.Height > (GetScreenHeight() - rec.Y)) rec = rec with { Height = GetScreenHeight() - rec.Y };

                if (IsMouseButtonReleased(MouseButton.Left)) mouseScaleMode = false;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("Scale rectangle dragging from bottom-right corner!", 10, 10, 20, Color.Gray);

                DrawRectangleRec(rec, Fade(Color.Green, 0.5f));

                if (mouseScaleReady)
                {
                    DrawRectangleLinesEx(rec, 1, Color.Red);
                    DrawTriangle(new Vector2(rec.X + rec.Width - MOUSE_SCALE_MARK_SIZE, rec.Y + rec.Height),
                                 new Vector2(rec.X + rec.Width, rec.Y + rec.Height),
                                 new Vector2(rec.X + rec.Width, rec.Y + rec.Height - MOUSE_SCALE_MARK_SIZE), Color.Red);
                }

            EndDrawing();
        }

        CloseWindow();
    }
}
