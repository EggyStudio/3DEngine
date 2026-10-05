// raylib's shapes_easings_ball example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.Easings;

namespace Engine.Examples;

public static class ShapesEasingsBall
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] easings ball");

        int ballPositionX = -100;
        int ballRadius = 20;
        float ballAlpha = 0.0f;

        int state = 0;
        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (state == 0)
            {
                framesCounter++;
                ballPositionX = (int)EaseElasticOut((float)framesCounter, -100, screenWidth/2.0f + 100, 120);

                if (framesCounter >= 120)
                {
                    framesCounter = 0;
                    state = 1;
                }
            }
            else if (state == 1)
            {
                framesCounter++;
                ballRadius = (int)EaseElasticIn((float)framesCounter, 20, 500, 200);

                if (framesCounter >= 200)
                {
                    framesCounter = 0;
                    state = 2;
                }
            }
            else if (state == 2)
            {
                framesCounter++;
                ballAlpha = EaseCubicOut((float)framesCounter, 0.0f, 1.0f, 200);

                if (framesCounter >= 200)
                {
                    framesCounter = 0;
                    state = 3;
                }
            }
            else if (state == 3)
            {
                if (IsKeyPressed(Key.Enter))
                {
                    ballPositionX = -100;
                    ballRadius = 20;
                    ballAlpha = 0.0f;
                    state = 0;
                }
            }

            if (IsKeyPressed(Key.R)) framesCounter = 0;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (state >= 2) DrawRectangle(0, 0, screenWidth, screenHeight, Color.Green);
                DrawCircle(ballPositionX, 200, (float)ballRadius, Fade(Color.Red, 1.0f - ballAlpha));

                if (state == 3) DrawText("PRESS [ENTER] TO PLAY AGAIN!", 240, 200, 20, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }
}
