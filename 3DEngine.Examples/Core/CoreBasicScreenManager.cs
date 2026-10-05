// raylib's core_basic_screen_manager example, Copyright (c) 2021-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreBasicScreenManager
{
    private const int LOGO = 0;

    private const int TITLE = 1;

    private const int GAMEPLAY = 2;

    private const int ENDING = 3;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] basic screen manager");

        int currentScreen = LOGO;

        int framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            switch (currentScreen)
            {
                case LOGO:
                {
                    framesCounter++;

                    if (framesCounter > 120)
                    {
                        currentScreen = TITLE;
                    }
                } break;
                case TITLE:
                {
                    if (IsKeyPressed(Key.Enter) || IsGestureDetected(Gesture.Tap))
                    {
                        currentScreen = GAMEPLAY;
                    }
                } break;
                case GAMEPLAY:
                {
                    if (IsKeyPressed(Key.Enter) || IsGestureDetected(Gesture.Tap))
                    {
                        currentScreen = ENDING;
                    }
                } break;
                case ENDING:
                {
                    if (IsKeyPressed(Key.Enter) || IsGestureDetected(Gesture.Tap))
                    {
                        currentScreen = TITLE;
                    }
                } break;
                default: break;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                switch(currentScreen)
                {
                    case LOGO:
                    {
                        DrawText("LOGO SCREEN", 20, 20, 40, Color.LightGray);
                        DrawText("WAIT for 2 SECONDS...", 290, 220, 20, Color.Gray);
                    } break;
                    case TITLE:
                    {
                        DrawRectangle(0, 0, screenWidth, screenHeight, Color.Green);
                        DrawText("TITLE SCREEN", 20, 20, 40, Color.DarkGreen);
                        DrawText("PRESS ENTER or TAP to JUMP to GAMEPLAY SCREEN", 120, 220, 20, Color.DarkGreen);
                    } break;
                    case GAMEPLAY:
                    {
                        DrawRectangle(0, 0, screenWidth, screenHeight, Color.Purple);
                        DrawText("GAMEPLAY SCREEN", 20, 20, 40, Color.Maroon);
                        DrawText("PRESS ENTER or TAP to JUMP to ENDING SCREEN", 130, 220, 20, Color.Maroon);
                    } break;
                    case ENDING:
                    {
                        DrawRectangle(0, 0, screenWidth, screenHeight, Color.Blue);
                        DrawText("ENDING SCREEN", 20, 20, 40, Color.DarkBlue);
                        DrawText("PRESS ENTER or TAP to RETURN to TITLE SCREEN", 120, 220, 20, Color.DarkBlue);
                    } break;
                    default: break;
                }

            EndDrawing();
        }

        CloseWindow();
    }
}
