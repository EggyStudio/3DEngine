// raylib's shapes_starfield_effect example, Copyright (c) 2025 JP Mortiboys (@themushroompirates), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesStarfieldEffect
{
    private const int STAR_COUNT = 420;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] starfield effect");

        Color bgColor = ColorLerp(Color.DarkBlue, Color.Black, 0.69f);

        float speed = 10.0f/9.0f;

        bool drawLines = true;

        Vector3[] stars = new Vector3[STAR_COUNT];
        Vector2[] starsScreenPos = new Vector2[STAR_COUNT];

        for (int i = 0; i < STAR_COUNT; i++)
        {
            stars[i].X = (float)GetRandomValue(-screenWidth / 2, (int)screenWidth / 2);
            stars[i].Y = (float)GetRandomValue(-screenHeight / 2, (int)screenHeight / 2);
            stars[i].Z = 1.0f;
        }

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float mouseMove = GetMouseWheelMove();
            if ((int)mouseMove != 0) speed += 2.0f*mouseMove/9.0f;
            if (speed < 0.0f) speed = 0.1f;
            else if (speed > 2.0f) speed = 2.0f;

            if (IsKeyPressed(Key.Space)) drawLines = !drawLines;

            float dt = GetFrameTime();
            for (int i = 0; i < STAR_COUNT; i++)
            {
                stars[i].Z -= dt*speed;

                starsScreenPos[i] = new Vector2(screenWidth*0.5f + stars[i].X/stars[i].Z,
                    screenHeight*0.5f + stars[i].Y/stars[i].Z);

                if ((stars[i].Z < 0.0f) || (starsScreenPos[i].X < 0) || (starsScreenPos[i].Y < 0.0f) ||
                    (starsScreenPos[i].X > screenWidth) || (starsScreenPos[i].Y > screenHeight))
                {
                    stars[i].X = (float)GetRandomValue(-screenWidth / 2, screenWidth / 2);
                    stars[i].Y = (float)GetRandomValue(-screenHeight / 2, screenHeight / 2);
                    stars[i].Z = 1.0f;
                }
            }

            BeginDrawing();

                ClearBackground(bgColor);

                for (int i = 0; i < STAR_COUNT; i++)
                {
                    if (drawLines)
                    {
                        float t = Math.Clamp(stars[i].Z + 1.0f/32.0f, 0.0f, 1.0f);

                        if ((t - stars[i].Z) > 1e-3)
                        {
                            Vector2 startPos = new Vector2(screenWidth*0.5f + stars[i].X/t,
                                screenHeight*0.5f + stars[i].Y/t);

                            DrawLineV(startPos, starsScreenPos[i], Color.RayWhite);
                        }
                    }
                    else
                    {
                        float radius = float.Lerp(stars[i].Z, 1.0f, 5.0f);

                        DrawCircleV(starsScreenPos[i], radius, Color.RayWhite);
                    }
                }

                DrawText($"[MOUSE WHEEL] Current Speed: {9.0f*speed/2.0f:0.}", 10, 40, 20, Color.RayWhite);
                DrawText($"[SPACE] Current draw mode: {(drawLines ? "Lines" : "Circles")}", 10, 70, 20, Color.RayWhite);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
