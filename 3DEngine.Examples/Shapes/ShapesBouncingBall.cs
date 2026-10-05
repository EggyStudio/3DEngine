// raylib's shapes_bouncing_ball example, Copyright (c) 2013-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesBouncingBall
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] bouncing ball");

        Vector2 ballPosition = new(GetScreenWidth()/2.0f, GetScreenHeight()/2.0f);
        Vector2 ballSpeed = new(5.0f, 4.0f);
        int ballRadius = 20;
        float gravity = 0.2f;

        bool useGravity = true;
        bool pause = false;
        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.G)) useGravity = !useGravity;
            if (IsKeyPressed(Key.Space)) pause = !pause;

            if (!pause)
            {
                ballPosition.X += ballSpeed.X;
                ballPosition.Y += ballSpeed.Y;

                if (useGravity) ballSpeed.Y += gravity;

                if ((ballPosition.X >= (GetScreenWidth() - ballRadius)) || (ballPosition.X <= ballRadius)) ballSpeed.X *= -1.0f;
                if ((ballPosition.Y >= (GetScreenHeight() - ballRadius)) || (ballPosition.Y <= ballRadius)) ballSpeed.Y *= -0.95f;
            }
            else framesCounter++;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawCircleV(ballPosition, (float)ballRadius, Color.Maroon);
                DrawText("PRESS SPACE to PAUSE BALL MOVEMENT", 10, GetScreenHeight() - 25, 20, Color.LightGray);

                if (useGravity) DrawText("GRAVITY: ON (Press G to disable)", 10, GetScreenHeight() - 50, 20, Color.DarkGreen);
                else DrawText("GRAVITY: OFF (Press G to enable)", 10, GetScreenHeight() - 50, 20, Color.Red);

                if (pause && ((framesCounter/30)%2 != 0)) DrawText("PAUSED", 350, 200, 30, Color.Gray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
