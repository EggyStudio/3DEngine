// raylib's shapes_vector_angle example, Copyright (c) 2023-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesVectorAngle
{
    public static void Run()
    {
        const int screenWidth = 800;

        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] vector angle");

        Vector2 v0 = new(screenWidth/2.0f, screenHeight/2.0f);
        Vector2 v1 = (v0 + new Vector2(100.0f, 80.0f));
        Vector2 v2 = default;

        float angle = 0.0f;
        int angleMode = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float startangle = 0.0f;

            if (angleMode == 0) startangle = -Vector2LineAngle(v0, v1)*(180/MathF.PI);
            if (angleMode == 1) startangle = 0.0f;

            v2 = GetMousePosition();

            if (IsKeyPressed(Key.Space)) angleMode = angleMode == 0 ? 1 : 0;

            if ((angleMode == 0) && IsMouseButtonDown(MouseButton.Right)) v1 = GetMousePosition();

            if (angleMode == 0)
            {
                Vector2 v1Normal = Vector2.Normalize((v1 - v0));
                Vector2 v2Normal = Vector2.Normalize((v2 - v0));

                angle = Vector2Angle(v1Normal, v2Normal)*(180/MathF.PI);
            }
            else if (angleMode == 1)
            {
                angle = Vector2LineAngle(v0, v2)*(180/MathF.PI);
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (angleMode == 0)
                {
                    DrawText("MODE 0: Angle between V1 and V2", 10, 10, 20, Color.Black);
                    DrawText("Right Click to Move V2", 10, 30, 20, Color.DarkGray);

                    DrawLineEx(v0, v1, 2.0f, Color.Black);
                    DrawLineEx(v0, v2, 2.0f, Color.Red);

                    DrawCircleSector(v0, 40.0f, startangle, startangle + angle, 32, Fade(Color.Green, 0.6f));
                }
                else if (angleMode == 1)
                {
                    DrawText("MODE 1: Angle formed by line V1 to V2", 10, 10, 20, Color.Black);

                    DrawLine(0, screenHeight/2, screenWidth, screenHeight/2, Color.LightGray);
                    DrawLineEx(v0, v2, 2.0f, Color.Red);

                    DrawCircleSector(v0, 40.0f, startangle, startangle - angle, 32, Fade(Color.Green, 0.6f));
                }

                DrawText("v0", (int)v0.X, (int)v0.Y, 10, Color.DarkGray);

                if (angleMode == 0 && (v0 - v1).Y > 0.0f) DrawText("v1", (int)v1.X, (int)v1.Y-10, 10, Color.DarkGray);
                if (angleMode == 0 && (v0 - v1).Y < 0.0f) DrawText("v1", (int)v1.X, (int)v1.Y, 10, Color.DarkGray);

                if (angleMode == 1) DrawText("v1", (int)v0.X + 40, (int)v0.Y, 10, Color.DarkGray);

                DrawText("v2", (int)v2.X-10, (int)v2.Y-10, 10, Color.DarkGray);

                DrawText("Press SPACE to change MODE", 460, 10, 20, Color.DarkGray);
                DrawText($"ANGLE: {angle:0.00}", 10, 70, 20, Color.Lime);

            EndDrawing();
        }

        CloseWindow();
    }
}
