// raylib's shapes_easings_testbed example, Copyright (c) 2019-2025 Juan Miguel López (@flashback-fx) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.Easings;

namespace Engine.Examples;

public static class ShapesEasingsTestbed
{
    private const int FONT_SIZE = 20;

    private const float D_STEP = 20.0f;
    private const float D_STEP_FINE = 2.0f;
    private const float D_MIN = 1.0f;
    private const float D_MAX = 10000.0f;

    private readonly record struct EasingFuncs(string name, Func<float, float, float, float, float> func);

    // In the order of raylib's EasingTypes, the last being EASING_NONE.
    private static readonly EasingFuncs[] easings =
    [
        new("EaseLinearNone", EaseLinearNone),
        new("EaseLinearIn", EaseLinearIn),
        new("EaseLinearOut", EaseLinearOut),
        new("EaseLinearInOut", EaseLinearInOut),
        new("EaseSineIn", EaseSineIn),
        new("EaseSineOut", EaseSineOut),
        new("EaseSineInOut", EaseSineInOut),
        new("EaseCircIn", EaseCircIn),
        new("EaseCircOut", EaseCircOut),
        new("EaseCircInOut", EaseCircInOut),
        new("EaseCubicIn", EaseCubicIn),
        new("EaseCubicOut", EaseCubicOut),
        new("EaseCubicInOut", EaseCubicInOut),
        new("EaseQuadIn", EaseQuadIn),
        new("EaseQuadOut", EaseQuadOut),
        new("EaseQuadInOut", EaseQuadInOut),
        new("EaseExpoIn", EaseExpoIn),
        new("EaseExpoOut", EaseExpoOut),
        new("EaseExpoInOut", EaseExpoInOut),
        new("EaseBackIn", EaseBackIn),
        new("EaseBackOut", EaseBackOut),
        new("EaseBackInOut", EaseBackInOut),
        new("EaseBounceOut", EaseBounceOut),
        new("EaseBounceIn", EaseBounceIn),
        new("EaseBounceInOut", EaseBounceInOut),
        new("EaseElasticIn", EaseElasticIn),
        new("EaseElasticOut", EaseElasticOut),
        new("EaseElasticInOut", EaseElasticInOut),
        new("None", NoEase),
    ];

    private static readonly int EASING_NONE = easings.Length - 1;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] easings testbed");

        Vector2 ballPosition = new(100.0f, 100.0f);

        float t = 0.0f;
        float d = 300.0f;
        bool paused = true;
        bool boundedT = true;

        int easingX = EASING_NONE;
        int easingY = EASING_NONE;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.T)) boundedT = !boundedT;

            if (IsKeyPressed(Key.Right))
            {
                easingX++;

                if (easingX > EASING_NONE) easingX = 0;
            }
            else if (IsKeyPressed(Key.Left))
            {
                if (easingX == 0) easingX = EASING_NONE;
                else easingX--;
            }

            if (IsKeyPressed(Key.Down))
            {
                easingY++;

                if (easingY > EASING_NONE) easingY = 0;
            }
            else if (IsKeyPressed(Key.Up))
            {
                if (easingY == 0) easingY = EASING_NONE;
                else easingY--;
            }

            if (IsKeyPressed(Key.W) && (d < D_MAX - D_STEP)) d += D_STEP;
            else if (IsKeyPressed(Key.Q) && (d > D_MIN + D_STEP)) d -= D_STEP;

            if (IsKeyDown(Key.S) && (d < D_MAX - D_STEP_FINE)) d += D_STEP_FINE;
            else if (IsKeyDown(Key.A) && (d > D_MIN + D_STEP_FINE)) d -= D_STEP_FINE;

            if (IsKeyPressed(Key.Space) || IsKeyPressed(Key.T) ||
                IsKeyPressed(Key.Right) || IsKeyPressed(Key.Left) ||
                IsKeyPressed(Key.Down) || IsKeyPressed(Key.Up) ||
                IsKeyPressed(Key.W) || IsKeyPressed(Key.Q) ||
                IsKeyDown(Key.S) || IsKeyDown(Key.A) ||
                (IsKeyPressed(Key.Enter) && (boundedT == true) && (t >= d)))
            {
                t = 0.0f;
                ballPosition.X = 100.0f;
                ballPosition.Y = 100.0f;
                paused = true;
            }

            if (IsKeyPressed(Key.Enter)) paused = !paused;

            if (!paused && ((boundedT && t < d) || !boundedT))
            {
                ballPosition.X = easings[easingX].func(t, 100.0f, 700.0f - 170.0f, d);
                ballPosition.Y = easings[easingY].func(t, 100.0f, 400.0f - 170.0f, d);
                t += 1.0f;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText($"Easing x: {easings[easingX].name}", 20, FONT_SIZE, FONT_SIZE, Color.LightGray);
                DrawText($"Easing y: {easings[easingY].name}", 20, FONT_SIZE*2, FONT_SIZE, Color.LightGray);
                DrawText($"t ({((boundedT == true) ? 'b' : 'u')}) = {t:0.00} d = {d:0.00}", 20, FONT_SIZE*3, FONT_SIZE, Color.LightGray);

                DrawText("Use ENTER to play or pause movement, use SPACE to restart", 20, GetScreenHeight() - FONT_SIZE*2, FONT_SIZE, Color.LightGray);
                DrawText("Use Q and W or A and S keys to change duration", 20, GetScreenHeight() - FONT_SIZE*3, FONT_SIZE, Color.LightGray);
                DrawText("Use LEFT or RIGHT keys to choose easing for the x axis", 20, GetScreenHeight() - FONT_SIZE*4, FONT_SIZE, Color.LightGray);
                DrawText("Use UP or DOWN keys to choose easing for the y axis", 20, GetScreenHeight() - FONT_SIZE*5, FONT_SIZE, Color.LightGray);

                DrawCircleV(ballPosition, 16.0f, Color.Maroon);

            EndDrawing();
        }

        CloseWindow();
    }

    // When no easing is chosen for an axis, which keeps it where it starts.
    private static float NoEase(float t, float b, float c, float d) => b;
}
