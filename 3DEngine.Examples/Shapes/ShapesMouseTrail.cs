// raylib's shapes_mouse_trail example, Copyright (c) 2025 Balamurugan R (@Bala050814), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesMouseTrail
{
    private const int MAX_TRAIL_LENGTH = 30;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] mouse trail");

        Vector2[] trailPositions = new Vector2[MAX_TRAIL_LENGTH];

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            Vector2 mousePosition = GetMousePosition();

            for (int i = MAX_TRAIL_LENGTH - 1; i > 0; i--)
            {
                trailPositions[i] = trailPositions[i - 1];
            }

            trailPositions[0] = mousePosition;

            BeginDrawing();

                ClearBackground(Color.Black);

                for (int i = 0; i < MAX_TRAIL_LENGTH; i++)
                {
                    if ((trailPositions[i].X != 0.0f) || (trailPositions[i].Y != 0.0f))
                    {
                        float ratio = (float)(MAX_TRAIL_LENGTH - i)/MAX_TRAIL_LENGTH;

                        Color trailColor = Fade(Color.SkyBlue, ratio*0.5f + 0.5f);

                        float trailRadius = 15.0f*ratio;

                        DrawCircleV(trailPositions[i], trailRadius, trailColor);
                    }
                }

                DrawCircleV(mousePosition, 15.0f, Color.White);

                DrawText("Move the mouse to see the trail effect!", 10, screenHeight - 30, 20, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
