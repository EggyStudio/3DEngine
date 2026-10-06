// raylib's shapes_rectangle_advanced example, Copyright (c) 2024-2025 Everton Jr. (@evertonse) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesRectangleAdvanced
{
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] rectangle advanced");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Update rectangle bounds
            float width = GetScreenWidth()/2.0f, height = GetScreenHeight()/6.0f;
            Rectangle rec = new(
                GetScreenWidth()/2.0f - width/2,
                GetScreenHeight()/2.0f - 5*(height/2),
                width, height);

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                // Draw All Rectangles with different roundess for each side and different gradients
                DrawRectangleRoundedGradientH(rec, 0.8f, 0.8f, 36, Color.Blue, Color.Red);

                rec = rec with { Y = rec.Y + rec.Height + 1 };
                DrawRectangleRoundedGradientH(rec, 0.5f, 1.0f, 36, Color.Red, Color.Pink);

                rec = rec with { Y = rec.Y + rec.Height + 1 };
                DrawRectangleRoundedGradientH(rec, 1.0f, 0.5f, 36, Color.Red, Color.Blue);

                rec = rec with { Y = rec.Y + rec.Height + 1 };
                DrawRectangleRoundedGradientH(rec, 0.0f, 1.0f, 36, Color.Blue, Color.Black);

                rec = rec with { Y = rec.Y + rec.Height + 1 };
                DrawRectangleRoundedGradientH(rec, 1.0f, 0.0f, 36, Color.Blue, Color.Pink);
            EndDrawing();
        }

        CloseWindow();
    }

    // Draw rectangle with rounded edges and horizontal gradient, with options to choose side of roundness
    // Adapted from both DrawRectangleRounded() and DrawRectangleGradientH() of raylib's rshapes.
    // raylib's file has a branch of quads too, for a raylib built with SUPPORT_QUADS_DRAW_MODE,
    // which config.h sets and an example does not include, so it draws triangles, as here.
    private static void DrawRectangleRoundedGradientH(Rectangle rec, float roundnessLeft, float roundnessRight, int segments, Color left, Color right)
    {
        // Neither side is rounded
        if ((roundnessLeft <= 0.0f && roundnessRight <= 0.0f) || (rec.Width < 1) || (rec.Height < 1))
        {
            DrawRectangleGradientEx(rec, left, left, right, right);
            return;
        }

        if (roundnessLeft >= 1.0f) roundnessLeft = 1.0f;
        if (roundnessRight >= 1.0f) roundnessRight = 1.0f;

        // Calculate corner radius both from right and left
        float recSize = rec.Width > rec.Height ? rec.Height : rec.Width;
        float radiusLeft = (recSize*roundnessLeft)/2;
        float radiusRight = (recSize*roundnessRight)/2;

        if (radiusLeft <= 0.0f) radiusLeft = 0.0f;
        if (radiusRight <= 0.0f) radiusRight = 0.0f;

        if (radiusRight <= 0.0f && radiusLeft <= 0.0f) return;

        float stepLength = 90.0f/(float)segments;

        /*
        Diagram Copied here for reference, original at 'DrawRectangleRounded()' source code

              P0____________________P1
              /|                    |\
             /1|          2         |3\
         P7 /__|____________________|__\ P2
           |   |P8                P9|   |
           | 8 |          9         | 4 |
           | __|____________________|__ |
         P6 \  |P11              P10|  / P3
             \7|          6         |5/
              \|____________________|/
              P5                    P4
        */

        // Coordinates of the 12 points also adapted from DrawRectangleRounded
        Vector2[] point =
        [
            // PO, P1, P2
            new((float)rec.X + radiusLeft, rec.Y), new((float)(rec.X + rec.Width) - radiusRight, rec.Y), new(rec.X + rec.Width, (float)rec.Y + radiusRight),
            // P3, P4
            new(rec.X + rec.Width, (float)(rec.Y + rec.Height) - radiusRight), new((float)(rec.X + rec.Width) - radiusRight, rec.Y + rec.Height),
            // P5, P6, P7
            new((float)rec.X + radiusLeft, rec.Y + rec.Height), new(rec.X, (float)(rec.Y + rec.Height) - radiusLeft), new(rec.X, (float)rec.Y + radiusLeft),
            // P8, P9
            new((float)rec.X + radiusLeft, (float)rec.Y + radiusLeft), new((float)(rec.X + rec.Width) - radiusRight, (float)rec.Y + radiusRight),
            // P10, P11
            new((float)(rec.X + rec.Width) - radiusRight, (float)(rec.Y + rec.Height) - radiusRight), new((float)rec.X + radiusLeft, (float)(rec.Y + rec.Height) - radiusLeft),
        ];

        Vector2[] centers = [point[8], point[9], point[10], point[11]];
        float[] angles = [180.0f, 270.0f, 0.0f, 90.0f];

        // Here we use the 'Diagram' to guide ourselves to which point receives what color
        // By choosing the color correctly associated with a pointe the gradient effect
        // will naturally come from OpenGL interpolation
        // But this time instead of Quad, we think in triangles

        rlBegin(RlDrawMode.Triangles);
            // Draw all of the 4 corners: [1] Upper Left Corner, [3] Upper Right Corner, [5] Lower Right Corner, [7] Lower Left Corner
            for (int k = 0; k < 4; ++k)
            {
                Color color = default;
                float radius = 0.0f;
                if (k == 0) (color, radius) = (left, radiusLeft);       // [1] Upper Left Corner
                if (k == 1) (color, radius) = (right, radiusRight);     // [3] Upper Right Corner
                if (k == 2) (color, radius) = (right, radiusRight);     // [5] Lower Right Corner
                if (k == 3) (color, radius) = (left, radiusLeft);       // [7] Lower Left Corner

                float angle = angles[k];
                Vector2 center = centers[k];

                for (int i = 0; i < segments; i++)
                {
                    rlColor4ub(color.R, color.G, color.B, color.A);
                    rlVertex2f(center.X, center.Y);
                    rlVertex2f(center.X + MathF.Cos(DEG2RAD*(angle + stepLength))*radius, center.Y + MathF.Sin(DEG2RAD*(angle + stepLength))*radius);
                    rlVertex2f(center.X + MathF.Cos(DEG2RAD*angle)*radius, center.Y + MathF.Sin(DEG2RAD*angle)*radius);
                    angle += stepLength;
                }
            }

            // [2] Upper Rectangle
            rlColor4ub(left.R, left.G, left.B, left.A);
            rlVertex2f(point[0].X, point[0].Y);
            rlVertex2f(point[8].X, point[8].Y);
            rlColor4ub(right.R, right.G, right.B, right.A);
            rlVertex2f(point[9].X, point[9].Y);
            rlVertex2f(point[1].X, point[1].Y);
            rlColor4ub(left.R, left.G, left.B, left.A);
            rlVertex2f(point[0].X, point[0].Y);
            rlColor4ub(right.R, right.G, right.B, right.A);
            rlVertex2f(point[9].X, point[9].Y);

            // [4] Right Rectangle
            rlColor4ub(right.R, right.G, right.B, right.A);
            rlVertex2f(point[9].X, point[9].Y);
            rlVertex2f(point[10].X, point[10].Y);
            rlVertex2f(point[3].X, point[3].Y);
            rlVertex2f(point[2].X, point[2].Y);
            rlVertex2f(point[9].X, point[9].Y);
            rlVertex2f(point[3].X, point[3].Y);

            // [6] Bottom Rectangle
            rlColor4ub(left.R, left.G, left.B, left.A);
            rlVertex2f(point[11].X, point[11].Y);
            rlVertex2f(point[5].X, point[5].Y);
            rlColor4ub(right.R, right.G, right.B, right.A);
            rlVertex2f(point[4].X, point[4].Y);
            rlVertex2f(point[10].X, point[10].Y);
            rlColor4ub(left.R, left.G, left.B, left.A);
            rlVertex2f(point[11].X, point[11].Y);
            rlColor4ub(right.R, right.G, right.B, right.A);
            rlVertex2f(point[4].X, point[4].Y);

            // [8] Left Rectangle
            rlColor4ub(left.R, left.G, left.B, left.A);
            rlVertex2f(point[7].X, point[7].Y);
            rlVertex2f(point[6].X, point[6].Y);
            rlVertex2f(point[11].X, point[11].Y);
            rlVertex2f(point[8].X, point[8].Y);
            rlVertex2f(point[7].X, point[7].Y);
            rlVertex2f(point[11].X, point[11].Y);

            // [9] Middle Rectangle
            rlColor4ub(left.R, left.G, left.B, left.A);
            rlVertex2f(point[8].X, point[8].Y);
            rlVertex2f(point[11].X, point[11].Y);
            rlColor4ub(right.R, right.G, right.B, right.A);
            rlVertex2f(point[10].X, point[10].Y);
            rlVertex2f(point[9].X, point[9].Y);
            rlColor4ub(left.R, left.G, left.B, left.A);
            rlVertex2f(point[8].X, point[8].Y);
            rlColor4ub(right.R, right.G, right.B, right.A);
            rlVertex2f(point[10].X, point[10].Y);
        rlEnd();
    }
}
