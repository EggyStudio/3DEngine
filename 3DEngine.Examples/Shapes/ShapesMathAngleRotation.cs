// raylib's shapes_math_angle_rotation example, Copyright (c) 2025 Kris (@krispy-snacc), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesMathAngleRotation
{
    public static void Run()
    {
        const int screenWidth = 720;
        const int screenHeight = 400;

        InitWindow(screenWidth, screenHeight, "[shapes] math angle rotation");
        SetTargetFPS(60);

        Vector2 center = new(screenWidth/2.0f, screenHeight/2.0f);
        const float lineLength = 150.0f;

        int[] angles = { 0, 30, 60, 90 };
        int numAngles = angles.Length;

        float totalAngle = 0.0f;

        while (!WindowShouldClose())
        {
            totalAngle += 1.0f;
            if (totalAngle >= 360.0f) totalAngle -= 360.0f;

            BeginDrawing();
                ClearBackground(Color.White);

                DrawText("Fixed angles + rotating line", 10, 10, 20, Color.LightGray);

                for (int i = 0; i < numAngles; i++)
                {
                    float rad = angles[i]*(MathF.PI/180);
                    Vector2 end = new(center.X + MathF.Cos(rad)*lineLength,
                                    center.Y + MathF.Sin(rad)*lineLength);

                    Color col;
                    switch(i)
                    {
                        case 0: col = Color.Green; break;
                        case 1: col = Color.Orange; break;
                        case 2: col = Color.Blue; break;
                        case 3: col = Color.Magenta; break;
                        default: col = Color.White; break;
                    }

                    DrawLineEx(center, end, 5.0f, col);

                    Vector2 textPos = new(center.X + MathF.Cos(rad)*(lineLength + 20),
                                        center.Y + MathF.Sin(rad)*(lineLength + 20));
                    DrawText($"{angles[i]}°", (int)textPos.X, (int)textPos.Y, 20, col);
                }

                float animRad = totalAngle*(MathF.PI/180);
                Vector2 animEnd = new(center.X + MathF.Cos(animRad)*lineLength,
                                    center.Y + MathF.Sin(animRad)*lineLength);

                Color animCol = ColorFromHSV(MathF.IEEERemainder(totalAngle, 360.0f), 0.8f, 0.9f);
                DrawLineEx(center, animEnd, 5.0f, animCol);

            EndDrawing();
        }

        CloseWindow();
    }
}
