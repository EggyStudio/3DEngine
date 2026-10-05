// raylib's shapes_following_eyes example, Copyright (c) 2013-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesFollowingEyes
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] following eyes");

        Vector2 scleraLeftPosition = new(GetScreenWidth()/2.0f - 100.0f, GetScreenHeight()/2.0f);
        Vector2 scleraRightPosition = new(GetScreenWidth()/2.0f + 100.0f, GetScreenHeight()/2.0f);
        float scleraRadius = 80;

        Vector2 irisLeftPosition = new(GetScreenWidth()/2.0f - 100.0f, GetScreenHeight()/2.0f);
        Vector2 irisRightPosition = new(GetScreenWidth()/2.0f + 100.0f, GetScreenHeight()/2.0f);
        float irisRadius = 24;

        float angle = 0.0f;
        float dx = 0.0f, dy = 0.0f, dxx = 0.0f, dyy = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            irisLeftPosition = GetMousePosition();
            irisRightPosition = GetMousePosition();

            if (!CheckCollisionPointCircle(irisLeftPosition, scleraLeftPosition, scleraRadius - irisRadius))
            {
                dx = irisLeftPosition.X - scleraLeftPosition.X;
                dy = irisLeftPosition.Y - scleraLeftPosition.Y;

                angle = MathF.Atan2(dy, dx);

                dxx = (scleraRadius - irisRadius)*MathF.Cos(angle);
                dyy = (scleraRadius - irisRadius)*MathF.Sin(angle);

                irisLeftPosition.X = scleraLeftPosition.X + dxx;
                irisLeftPosition.Y = scleraLeftPosition.Y + dyy;
            }

            if (!CheckCollisionPointCircle(irisRightPosition, scleraRightPosition, scleraRadius - irisRadius))
            {
                dx = irisRightPosition.X - scleraRightPosition.X;
                dy = irisRightPosition.Y - scleraRightPosition.Y;

                angle = MathF.Atan2(dy, dx);

                dxx = (scleraRadius - irisRadius)*MathF.Cos(angle);
                dyy = (scleraRadius - irisRadius)*MathF.Sin(angle);

                irisRightPosition.X = scleraRightPosition.X + dxx;
                irisRightPosition.Y = scleraRightPosition.Y + dyy;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawCircleV(scleraLeftPosition, scleraRadius, Color.LightGray);
                DrawCircleV(irisLeftPosition, irisRadius, Color.Brown);
                DrawCircleV(irisLeftPosition, 10, Color.Black);

                DrawCircleV(scleraRightPosition, scleraRadius, Color.LightGray);
                DrawCircleV(irisRightPosition, irisRadius, Color.DarkGreen);
                DrawCircleV(irisRightPosition, 10, Color.Black);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
