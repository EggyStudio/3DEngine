// raylib's shapes_logo_raylib_anim example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesLogoRaylibAnim
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] logo raylib anim");

        int logoPositionX = screenWidth/2 - 128;
        int logoPositionY = screenHeight/2 - 128;

        int framesCounter = 0;
        int lettersCount = 0;

        int topSideRecWidth = 16;
        int leftSideRecHeight = 16;

        int bottomSideRecWidth = 16;
        int rightSideRecHeight = 16;

        int state = 0;
        float alpha = 1.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (state == 0)
            {
                framesCounter++;

                if (framesCounter == 120)
                {
                    state = 1;
                    framesCounter = 0;
                }
            }
            else if (state == 1)
            {
                topSideRecWidth += 4;
                leftSideRecHeight += 4;

                if (topSideRecWidth == 256) state = 2;
            }
            else if (state == 2)
            {
                bottomSideRecWidth += 4;
                rightSideRecHeight += 4;

                if (bottomSideRecWidth == 256) state = 3;
            }
            else if (state == 3)
            {
                framesCounter++;

                if (framesCounter/12 != 0)
                {
                    lettersCount++;
                    framesCounter = 0;
                }

                if (lettersCount >= 10)
                {
                    alpha -= 0.02f;

                    if (alpha <= 0.0f)
                    {
                        alpha = 0.0f;
                        state = 4;
                    }
                }
            }
            else if (state == 4)
            {
                if (IsKeyPressed(Key.R))
                {
                    framesCounter = 0;
                    lettersCount = 0;

                    topSideRecWidth = 16;
                    leftSideRecHeight = 16;

                    bottomSideRecWidth = 16;
                    rightSideRecHeight = 16;

                    alpha = 1.0f;
                    state = 0;
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (state == 0)
                {
                    if ((framesCounter/15)%2 != 0) DrawRectangle(logoPositionX, logoPositionY, 16, 16, Color.Black);
                }
                else if (state == 1)
                {
                    DrawRectangle(logoPositionX, logoPositionY, topSideRecWidth, 16, Color.Black);
                    DrawRectangle(logoPositionX, logoPositionY, 16, leftSideRecHeight, Color.Black);
                }
                else if (state == 2)
                {
                    DrawRectangle(logoPositionX, logoPositionY, topSideRecWidth, 16, Color.Black);
                    DrawRectangle(logoPositionX, logoPositionY, 16, leftSideRecHeight, Color.Black);

                    DrawRectangle(logoPositionX + 240, logoPositionY, 16, rightSideRecHeight, Color.Black);
                    DrawRectangle(logoPositionX, logoPositionY + 240, bottomSideRecWidth, 16, Color.Black);
                }
                else if (state == 3)
                {
                    DrawRectangle(logoPositionX, logoPositionY, topSideRecWidth, 16, Fade(Color.Black, alpha));
                    DrawRectangle(logoPositionX, logoPositionY + 16, 16, leftSideRecHeight - 32, Fade(Color.Black, alpha));

                    DrawRectangle(logoPositionX + 240, logoPositionY + 16, 16, rightSideRecHeight - 32, Fade(Color.Black, alpha));
                    DrawRectangle(logoPositionX, logoPositionY + 240, bottomSideRecWidth, 16, Fade(Color.Black, alpha));

                    DrawRectangle(GetScreenWidth()/2 - 112, GetScreenHeight()/2 - 112, 224, 224, Fade(Color.RayWhite, alpha));

                    DrawText("raylib"[..Math.Min(lettersCount, 6)], GetScreenWidth()/2 - 44, GetScreenHeight()/2 + 48, 50, Fade(Color.Black, alpha));
                }
                else if (state == 4)
                {
                    DrawText("[R] REPLAY", 340, 200, 20, Color.Gray);
                }

            EndDrawing();
        }

        CloseWindow();
    }
}
