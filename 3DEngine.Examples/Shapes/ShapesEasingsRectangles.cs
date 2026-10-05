// raylib's shapes_easings_rectangles example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.Easings;

namespace Engine.Examples;

public static class ShapesEasingsRectangles
{
    private const int RECS_WIDTH = 50;

    private const int RECS_HEIGHT = 50;

    private const int MAX_RECS_X = 800/RECS_WIDTH;

    private const int MAX_RECS_Y = 450/RECS_HEIGHT;

    private const int PLAY_TIME_IN_FRAMES = 240;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] easings rectangles");

        Rectangle[] recs = new Rectangle[MAX_RECS_X*MAX_RECS_Y];

        for (int y = 0; y < MAX_RECS_Y; y++)
        {
            for (int x = 0; x < MAX_RECS_X; x++)
            {
                recs[y*MAX_RECS_X + x] = recs[y*MAX_RECS_X + x] with { X = RECS_WIDTH/2.0f + RECS_WIDTH*x };
                recs[y*MAX_RECS_X + x] = recs[y*MAX_RECS_X + x] with { Y = RECS_HEIGHT/2.0f + RECS_HEIGHT*y };
                recs[y*MAX_RECS_X + x] = recs[y*MAX_RECS_X + x] with { Width = RECS_WIDTH };
                recs[y*MAX_RECS_X + x] = recs[y*MAX_RECS_X + x] with { Height = RECS_HEIGHT };
            }
        }

        float rotation = 0.0f;
        int framesCounter = 0;
        int state = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (state == 0)
            {
                framesCounter++;

                for (int i = 0; i < MAX_RECS_X*MAX_RECS_Y; i++)
                {
                    recs[i] = recs[i] with { Height = EaseCircOut((float)framesCounter, RECS_HEIGHT, -RECS_HEIGHT, PLAY_TIME_IN_FRAMES) };
                    recs[i] = recs[i] with { Width = EaseCircOut((float)framesCounter, RECS_WIDTH, -RECS_WIDTH, PLAY_TIME_IN_FRAMES) };

                    if (recs[i].Height < 0) recs[i] = recs[i] with { Height = 0 };
                    if (recs[i].Width < 0) recs[i] = recs[i] with { Width = 0 };

                    if ((recs[i].Height == 0) && (recs[i].Width == 0)) state = 1;

                    rotation = EaseLinearIn((float)framesCounter, 0.0f, 360.0f, PLAY_TIME_IN_FRAMES);
                }
            }
            else if ((state == 1) && IsKeyPressed(Key.Space))
            {
                framesCounter = 0;

                for (int i = 0; i < MAX_RECS_X*MAX_RECS_Y; i++)
                {
                    recs[i] = recs[i] with { Height = RECS_HEIGHT };
                    recs[i] = recs[i] with { Width = RECS_WIDTH };
                }

                state = 0;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (state == 0)
                {
                    for (int i = 0; i < MAX_RECS_X*MAX_RECS_Y; i++)
                    {
                        DrawRectanglePro(recs[i], new Vector2(recs[i].Width/2, recs[i].Height/2), rotation, Color.Red);
                    }
                }
                else if (state == 1) DrawText("PRESS [SPACE] TO PLAY AGAIN!", 240, 200, 20, Color.Gray);

            EndDrawing();
        }

        CloseWindow();
    }
}
