// raylib's shapes_collision_area example, Copyright (c) 2013-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesCollisionArea
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] collision area");

        Rectangle boxA = new(10, GetScreenHeight()/2.0f - 50, 200, 100);
        int boxASpeedX = 4;

        Rectangle boxB = new(GetScreenWidth()/2.0f - 30, GetScreenHeight()/2.0f - 30, 60, 60);

        Rectangle boxCollision = default;

        int screenUpperLimit = 40;

        bool pause = false;
        bool collision = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (!pause) boxA = boxA with { X = boxA.X + (boxASpeedX) };

            if (((boxA.X + boxA.Width) >= GetScreenWidth()) || (boxA.X <= 0)) boxASpeedX *= -1;

            boxB = boxB with { X = GetMouseX() - boxB.Width/2 };
            boxB = boxB with { Y = GetMouseY() - boxB.Height/2 };

            if ((boxB.X + boxB.Width) >= GetScreenWidth()) boxB = boxB with { X = GetScreenWidth() - boxB.Width };
            else if (boxB.X <= 0) boxB = boxB with { X = 0 };

            if ((boxB.Y + boxB.Height) >= GetScreenHeight()) boxB = boxB with { Y = GetScreenHeight() - boxB.Height };
            else if (boxB.Y <= screenUpperLimit) boxB = boxB with { Y = (float)screenUpperLimit };

            collision = CheckCollisionRecs(boxA, boxB);

            if (collision) boxCollision = GetCollisionRec(boxA, boxB);

            if (IsKeyPressed(Key.Space)) pause = !pause;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawRectangle(0, 0, screenWidth, screenUpperLimit, collision? Color.Red : Color.Black);

                DrawRectangleRec(boxA, Color.Gold);
                DrawRectangleRec(boxB, Color.Blue);

                if (collision)
                {
                    DrawRectangleRec(boxCollision, Color.Lime);

                    DrawText("COLLISION!", GetScreenWidth()/2 - MeasureText("COLLISION!", 20)/2, screenUpperLimit/2 - 10, 20, Color.Black);

                    DrawText($"Collision Area: {(int)boxCollision.Width*(int)boxCollision.Height}", GetScreenWidth()/2 - 100, screenUpperLimit + 10, 20, Color.Black);
                }

                DrawText("Press SPACE to PAUSE/RESUME", 20, screenHeight - 35, 20, Color.LightGray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
