// raylib's shapes_splines_drawing example, Copyright (c) 2023-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesSplinesDrawing
{
    private const int MAX_SPLINE_POINTS = 32;

    // Cubic Bezier spline control points. Every segment has two.
    private struct ControlPoint
    {
        public Vector2 start;
        public Vector2 end;
    }

    private enum SplineType
    {
        Linear = 0,
        Basis,
        CatmullRom,
        Bezier,
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] splines drawing");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        Vector2[] points = new Vector2[MAX_SPLINE_POINTS];
        points[0] = new Vector2(50.0f, 400.0f);
        points[1] = new Vector2(160.0f, 220.0f);
        points[2] = new Vector2(340.0f, 380.0f);
        points[3] = new Vector2(520.0f, 60.0f);
        points[4] = new Vector2(710.0f, 260.0f);

        // The cubic Bezier spline takes its control points interleaved with each segment's start and end.
        Vector2[] pointsInterleaved = new Vector2[3*(MAX_SPLINE_POINTS - 1) + 1];

        int pointCount = 5;
        int selectedPoint = -1;
        int focusedPoint = -1;

        // raylib keeps pointers to a control point. Here a control point is named by 2*i for the
        // start of segment i and 2*i + 1 for its end, and -1 for none.
        int selectedControlPoint = -1;
        int focusedControlPoint = -1;

        ControlPoint[] control = new ControlPoint[MAX_SPLINE_POINTS - 1];
        for (int i = 0; i < pointCount - 1; i++)
        {
            control[i].start = new Vector2(points[i].X + 50, points[i].Y);
            control[i].end = new Vector2(points[i + 1].X - 50, points[i + 1].Y);
        }

        ref Vector2 ControlAt(int k) => ref (k%2 == 0) ? ref control[k/2].start : ref control[k/2].end;

        float splineThickness = 8.0f;
        int splineTypeActive = (int)SplineType.Linear;
        bool splineHelpersActive = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Spline points are added at the end of the spline.
            if (IsMouseButtonPressed(MouseButton.Right) && (pointCount < MAX_SPLINE_POINTS))
            {
                points[pointCount] = GetMousePosition();
                int i = pointCount - 1;
                control[i].start = new Vector2(points[i].X + 50, points[i].Y);
                control[i].end = new Vector2(points[i + 1].X - 50, points[i + 1].Y);
                pointCount++;
            }

            if ((selectedPoint == -1) && ((splineTypeActive != (int)SplineType.Bezier) || (selectedControlPoint == -1)))
            {
                focusedPoint = -1;
                for (int i = 0; i < pointCount; i++)
                {
                    if (CheckCollisionPointCircle(GetMousePosition(), points[i], 8.0f))
                    {
                        focusedPoint = i;
                        break;
                    }
                }
                if (IsMouseButtonPressed(MouseButton.Left)) selectedPoint = focusedPoint;
            }

            if (selectedPoint >= 0)
            {
                points[selectedPoint] = GetMousePosition();
                if (IsMouseButtonReleased(MouseButton.Left)) selectedPoint = -1;
            }

            if ((splineTypeActive == (int)SplineType.Bezier) && (focusedPoint == -1))
            {
                if (selectedControlPoint == -1)
                {
                    focusedControlPoint = -1;
                    for (int i = 0; i < pointCount - 1; i++)
                    {
                        if (CheckCollisionPointCircle(GetMousePosition(), control[i].start, 6.0f))
                        {
                            focusedControlPoint = 2*i;
                            break;
                        }
                        else if (CheckCollisionPointCircle(GetMousePosition(), control[i].end, 6.0f))
                        {
                            focusedControlPoint = 2*i + 1;
                            break;
                        }
                    }
                    if (IsMouseButtonPressed(MouseButton.Left)) selectedControlPoint = focusedControlPoint;
                }

                if (selectedControlPoint != -1)
                {
                    ControlAt(selectedControlPoint) = GetMousePosition();
                    if (IsMouseButtonReleased(MouseButton.Left)) selectedControlPoint = -1;
                }
            }

            if (IsKeyPressed(Key.Alpha1)) splineTypeActive = 0;
            else if (IsKeyPressed(Key.Alpha2)) splineTypeActive = 1;
            else if (IsKeyPressed(Key.Alpha3)) splineTypeActive = 2;
            else if (IsKeyPressed(Key.Alpha4)) splineTypeActive = 3;

            // A spline without control points clears their selection.
            if (IsKeyPressed(Key.Alpha1) || IsKeyPressed(Key.Alpha2) || IsKeyPressed(Key.Alpha3)) selectedControlPoint = -1;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (splineTypeActive == (int)SplineType.Linear)
                {
                    DrawSplineLinear(points.AsSpan(0, pointCount), splineThickness, Color.Red);
                }
                else if (splineTypeActive == (int)SplineType.Basis)
                {
                    DrawSplineBasis(points.AsSpan(0, pointCount), splineThickness, Color.Red);
                }
                else if (splineTypeActive == (int)SplineType.CatmullRom)
                {
                    DrawSplineCatmullRom(points.AsSpan(0, pointCount), splineThickness, Color.Red);
                }
                else if (splineTypeActive == (int)SplineType.Bezier)
                {
                    for (int i = 0; i < (pointCount - 1); i++)
                    {
                        pointsInterleaved[3*i] = points[i];
                        pointsInterleaved[3*i + 1] = control[i].start;
                        pointsInterleaved[3*i + 2] = control[i].end;
                    }

                    pointsInterleaved[3*(pointCount - 1)] = points[pointCount - 1];

                    DrawSplineBezierCubic(pointsInterleaved.AsSpan(0, 3*(pointCount - 1) + 1), splineThickness, Color.Red);

                    for (int i = 0; i < pointCount - 1; i++)
                    {
                        DrawCircleV(control[i].start, 6, Color.Gold);
                        DrawCircleV(control[i].end, 6, Color.Gold);
                        if (focusedControlPoint == 2*i) DrawCircleV(control[i].start, 8, Color.Green);
                        else if (focusedControlPoint == 2*i + 1) DrawCircleV(control[i].end, 8, Color.Green);
                        DrawLineEx(points[i], control[i].start, 1.0f, Color.LightGray);
                        DrawLineEx(points[i + 1], control[i].end, 1.0f, Color.LightGray);

                        DrawLineV(points[i], control[i].start, Color.Gray);
                        DrawLineV(control[i].end, points[i + 1], Color.Gray);
                    }
                }

                if (splineHelpersActive)
                {
                    for (int i = 0; i < pointCount; i++)
                    {
                        DrawCircleLinesV(points[i], (focusedPoint == i)? 12.0f : 8.0f, (focusedPoint == i)? Color.Blue : Color.DarkBlue);
                        if ((splineTypeActive != (int)SplineType.Linear) &&
                            (splineTypeActive != (int)SplineType.Bezier) &&
                            (i < pointCount - 1)) DrawLineV(points[i], points[i + 1], Color.Gray);

                        DrawText($"[{points[i].X:0}, {points[i].Y:0}]", (int)points[i].X, (int)points[i].Y + 10, 10, Color.Black);
                    }
                }

                // raygui's controls, as ImGui's, where raygui places them. raygui locks its controls
                // while a point is dragged, and ImGui's move only for a press that began on them, so
                // a point dragged across them leaves them as they are.
                ImGui.SetNextWindowPos(new Vector2(4, 4));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.TextUnformatted("Spline type:");
                ImGui.SetNextItemWidth(140);
                ImGui.Combo("##type", ref splineTypeActive, "LINEAR\0BSPLINE\0CATMULLROM\0BEZIER\0");
                ImGui.Spacing();
                ImGui.TextUnformatted($"Spline thickness: {(int)splineThickness}");
                ImGui.SetNextItemWidth(140);
                ImGui.SliderFloat("##thickness", ref splineThickness, 1.0f, 40.0f, "");
                ImGui.Checkbox("Show point helpers", ref splineHelpersActive);
                ImGui.End();

            EndDrawing();
        }

        CloseWindow();
    }
}
