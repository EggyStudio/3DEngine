// raylib's shapes_ellipse_collision example, Copyright (c) 2025 Ziya (@Monjaris), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesEllipseCollision
{
    private static bool CheckCollisionPointEllipse(Vector2 point, Vector2 center, float rx, float ry)
    {
        float dx = (point.X - center.X)/rx;
        float dy = (point.Y - center.Y)/ry;
        return (dx*dx + dy*dy) <= 1.0f;
    }

    private static bool CheckCollisionEllipses(Vector2 c1, float rx1, float ry1, Vector2 c2, float rx2, float ry2)
    {
        float dx = c2.X - c1.X;
        float dy = c2.Y - c1.Y;
        float dist = MathF.Sqrt(dx*dx + dy*dy);

        if (dist == 0.0f) return true;

        float theta = MathF.Atan2(dy, dx);
        float cosT = MathF.Cos(theta);
        float sinT = MathF.Sin(theta);

        float r1 = (rx1*ry1)/MathF.Sqrt((ry1*cosT)*(ry1*cosT) + (rx1*sinT)*(rx1*sinT));
        float r2 = (rx2*ry2)/MathF.Sqrt((ry2*cosT)*(ry2*cosT) + (rx2*sinT)*(rx2*sinT));

        return dist <= (r1 + r2);
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] ellipse collision");
        SetTargetFPS(60);

        Vector2 ellipseACenter = new((float)screenWidth/4, (float)screenHeight/2);
        float ellipseARx = 120.0f;
        float ellipseARy = 70.0f;

        Vector2 ellipseBCenter = new((float)screenWidth*3/4, (float)screenHeight/2);
        float ellipseBRx = 90.0f;
        float ellipseBRy = 140.0f;

        int controlled = 0;

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.A)) controlled = 0;
            if (IsKeyPressed(Key.B)) controlled = 1;

            if (controlled == 0) ellipseACenter = GetMousePosition();
            else ellipseBCenter = GetMousePosition();

            bool ellipsesCollide = CheckCollisionEllipses(
                ellipseACenter, ellipseARx, ellipseARy,
                ellipseBCenter, ellipseBRx, ellipseBRy
            );

            bool mouseInA = CheckCollisionPointEllipse(GetMousePosition(), ellipseACenter, ellipseARx, ellipseARy);
            bool mouseInB = CheckCollisionPointEllipse(GetMousePosition(), ellipseBCenter, ellipseBRx, ellipseBRy);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawEllipse((int)ellipseACenter.X, (int)ellipseACenter.Y, ellipseARx, ellipseARy, ellipsesCollide ? Color.Red : Color.Blue);

                DrawEllipse((int)ellipseBCenter.X, (int)ellipseBCenter.Y, ellipseBRx, ellipseBRy, ellipsesCollide ? Color.Red : Color.Green);

                DrawEllipseLines((int)ellipseACenter.X, (int)ellipseACenter.Y, ellipseARx, ellipseARy, Color.White);

                DrawEllipseLines((int)ellipseBCenter.X, (int)ellipseBCenter.Y, ellipseBRx, ellipseBRy, Color.White);

                DrawCircleV(ellipseACenter, 4, Color.White);
                DrawCircleV(ellipseBCenter, 4, Color.White);

                if (ellipsesCollide) DrawText("ELLIPSES COLLIDE", screenWidth/2 - 120, 40, 28, Color.Red);
                else DrawText("NO COLLISION", screenWidth/2 - 80, 40, 28, Color.DarkGray);

                DrawText(controlled == 0 ? "Controlling: A" : "Controlling: B", 20, screenHeight - 40, 20, Color.Yellow);

                if (mouseInA && controlled != 0) DrawText("Mouse inside ellipse A", 20, screenHeight - 70, 20, Color.Blue);
                if (mouseInB && controlled != 1) DrawText("Mouse inside ellipse B", 20, screenHeight - 70, 20, Color.Green);

                DrawText("Press [A] or [B] to switch control", 20, 20, 20, Color.Gray);

            EndDrawing();
        }

        CloseWindow();
    }
}
