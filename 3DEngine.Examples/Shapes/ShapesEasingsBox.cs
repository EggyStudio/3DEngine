// raylib's shapes_easings_box example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.Easings;

namespace Engine.Examples;

public static class ShapesEasingsBox
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] easings box");

        Rectangle rec = new(GetScreenWidth()/2.0f, -100, 100, 100);
        float rotation = 0.0f;
        float alpha = 1.0f;

        int state = 0;
        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            switch (state)
            {
                case 0:
                {
                    framesCounter++;

                    rec = rec with { Y = EaseElasticOut((float)framesCounter, -100, GetScreenHeight()/2.0f + 100, 120) };

                    if (framesCounter >= 120)
                    {
                        framesCounter = 0;
                        state = 1;
                    }
                } break;
                case 1:
                {
                    framesCounter++;
                    rec = rec with { Height = EaseBounceOut((float)framesCounter, 100, -90, 120) };
                    rec = rec with { Width = EaseBounceOut((float)framesCounter, 100, (float)GetScreenWidth(), 120) };

                    if (framesCounter >= 120)
                    {
                        framesCounter = 0;
                        state = 2;
                    }
                } break;
                case 2:
                {
                    framesCounter++;
                    rotation = EaseQuadOut((float)framesCounter, 0.0f, 270.0f, 240);

                    if (framesCounter >= 240)
                    {
                        framesCounter = 0;
                        state = 3;
                    }
                } break;
                case 3:
                {
                    framesCounter++;
                    rec = rec with { Height = EaseCircOut((float)framesCounter, 10, (float)GetScreenWidth(), 120) };

                    if (framesCounter >= 120)
                    {
                        framesCounter = 0;
                        state = 4;
                    }
                } break;
                case 4:
                {
                    framesCounter++;
                    alpha = EaseSineOut((float)framesCounter, 1.0f, -1.0f, 160);

                    if (framesCounter >= 160)
                    {
                        framesCounter = 0;
                        state = 5;
                    }
                } break;
                default: break;
            }

            if (IsKeyPressed(Key.Space))
            {
                rec = new Rectangle(GetScreenWidth()/2.0f, -100, 100, 100);
                rotation = 0.0f;
                alpha = 1.0f;
                state = 0;
                framesCounter = 0;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawRectanglePro(rec, new Vector2(rec.Width/2, rec.Height/2), rotation, Fade(Color.Black, alpha));

                DrawText("PRESS [SPACE] TO RESET BOX ANIMATION!", 10, GetScreenHeight() - 25, 20, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
