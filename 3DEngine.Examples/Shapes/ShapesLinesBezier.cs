// raylib's shapes_lines_bezier example, Copyright (c) 2017-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesLinesBezier
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] lines bezier");

        Vector2 startPoint = new(30, 30);
        Vector2 endPoint = new((float)screenWidth - 30, (float)screenHeight - 30);
        bool moveStartPoint = false;
        bool moveEndPoint = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector2 mouse = GetMousePosition();

            if (CheckCollisionPointCircle(mouse, startPoint, 10.0f) && IsMouseButtonDown(MouseButton.Left)) moveStartPoint = true;
            else if (CheckCollisionPointCircle(mouse, endPoint, 10.0f) && IsMouseButtonDown(MouseButton.Left)) moveEndPoint = true;

            if (moveStartPoint)
            {
                startPoint = mouse;
                if (IsMouseButtonReleased(MouseButton.Left)) moveStartPoint = false;
            }

            if (moveEndPoint)
            {
                endPoint = mouse;
                if (IsMouseButtonReleased(MouseButton.Left)) moveEndPoint = false;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("MOVE START-END POINTS WITH MOUSE", 15, 20, 20, Color.Gray);

                DrawLineBezier(startPoint, endPoint, 4.0f, Color.Blue);

                DrawCircleV(startPoint, CheckCollisionPointCircle(mouse, startPoint, 10.0f)? 14.0f : 8.0f, moveStartPoint? Color.Red : Color.Blue);
                DrawCircleV(endPoint, CheckCollisionPointCircle(mouse, endPoint, 10.0f)? 14.0f : 8.0f, moveEndPoint? Color.Red : Color.Blue);

            EndDrawing();
        }

        CloseWindow();
    }
}
