// raylib's textures_textured_curve example, Copyright (c) 2022-2025 Jeffery Myers (@JeffM2501) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesTexturedCurve
{
    private static Texture2D texRoad;

    private static bool showCurve = false;

    private static float curveWidth = 50;
    private static int curveSegments = 24;

    // The curve's start, its tangent, its end and the end's tangent, in that order. raylib keeps a
    // pointer to the one selected, and here it is its index, -1 for none.
    private static readonly Vector2[] curvePoints = new Vector2[4];
    private static ref Vector2 curveStartPosition => ref curvePoints[0];
    private static ref Vector2 curveStartPositionTangent => ref curvePoints[1];
    private static ref Vector2 curveEndPosition => ref curvePoints[2];
    private static ref Vector2 curveEndPositionTangent => ref curvePoints[3];

    private static int curveSelectedPoint = -1;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.VsyncHint | ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[textures] textured curve");

        // Load the road texture
        texRoad = LoadTexture("resources/road.png");
        SetTextureFilter(texRoad, TextureFilter.Bilinear);

        // Setup the curve
        curveStartPosition = new Vector2(80, 100);
        curveStartPositionTangent = new Vector2(100, 300);

        curveEndPosition = new Vector2(700, 350);
        curveEndPositionTangent = new Vector2(600, 100);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Curve config options
            if (IsKeyPressed(Key.Space)) showCurve = !showCurve;
            if (IsKeyPressed(Key.Equal)) curveWidth += 2;
            if (IsKeyPressed(Key.Minus)) curveWidth -= 2;
            if (curveWidth < 2) curveWidth = 2;

            // Update segments
            if (IsKeyPressed(Key.Left)) curveSegments -= 2;
            if (IsKeyPressed(Key.Right)) curveSegments += 2;

            if (curveSegments < 2) curveSegments = 2;

            // Update curve logic
            // If the mouse is not down, we are not editing the curve so clear the selection
            if (!IsMouseButtonDown(MouseButton.Left)) curveSelectedPoint = -1;

            // If a point was selected, move it
            if (curveSelectedPoint >= 0) curvePoints[curveSelectedPoint] += GetMouseDelta();

            // The mouse is down, and nothing was selected, so see if anything was picked
            Vector2 mouse = GetMousePosition();
            if (CheckCollisionPointCircle(mouse, curveStartPosition, 6)) curveSelectedPoint = 0;
            else if (CheckCollisionPointCircle(mouse, curveStartPositionTangent, 6)) curveSelectedPoint = 1;
            else if (CheckCollisionPointCircle(mouse, curveEndPosition, 6)) curveSelectedPoint = 2;
            else if (CheckCollisionPointCircle(mouse, curveEndPositionTangent, 6)) curveSelectedPoint = 3;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTexturedCurve();    // Draw a textured Spline Cubic Bezier

                // Draw spline for reference
                if (showCurve) DrawSplineSegmentBezierCubic(curveStartPosition, curveEndPosition, curveStartPositionTangent, curveEndPositionTangent, 2, Color.Blue);

                // Draw the various control points and highlight where the mouse is
                DrawLineV(curveStartPosition, curveStartPositionTangent, Color.SkyBlue);
                DrawLineV(curveStartPositionTangent, curveEndPositionTangent, Fade(Color.LightGray, 0.4f));
                DrawLineV(curveEndPosition, curveEndPositionTangent, Color.Purple);

                if (CheckCollisionPointCircle(mouse, curveStartPosition, 6)) DrawCircleV(curveStartPosition, 7, Color.Yellow);
                DrawCircleV(curveStartPosition, 5, Color.Red);

                if (CheckCollisionPointCircle(mouse, curveStartPositionTangent, 6)) DrawCircleV(curveStartPositionTangent, 7, Color.Yellow);
                DrawCircleV(curveStartPositionTangent, 5, Color.Maroon);

                if (CheckCollisionPointCircle(mouse, curveEndPosition, 6)) DrawCircleV(curveEndPosition, 7, Color.Yellow);
                DrawCircleV(curveEndPosition, 5, Color.Green);

                if (CheckCollisionPointCircle(mouse, curveEndPositionTangent, 6)) DrawCircleV(curveEndPositionTangent, 7, Color.Yellow);
                DrawCircleV(curveEndPositionTangent, 5, Color.DarkGreen);

                // Draw usage info
                DrawText("Drag points to move curve, press SPACE to show/hide base curve", 10, 10, 10, Color.DarkGray);
                DrawText($"Curve width: {curveWidth,2:0} (Use + and - to adjust)", 10, 30, 10, Color.DarkGray);
                DrawText($"Curve segments: {curveSegments} (Use LEFT and RIGHT to adjust)", 10, 50, 10, Color.DarkGray);

            EndDrawing();
        }

        UnloadTexture(texRoad);

        CloseWindow();
    }

    // Draw textured curve using Spline Cubic Bezier
    private static void DrawTexturedCurve()
    {
        float step = 1.0f/curveSegments;

        Vector2 previous = curveStartPosition;
        Vector2 previousTangent = Vector2.Zero;
        float previousV = 0;

        // We can't compute a tangent for the first point, so we need to reuse the tangent from the first segment
        bool tangentSet = false;

        Vector2 current = Vector2.Zero;
        float t = 0.0f;

        for (int i = 1; i <= curveSegments; i++)
        {
            t = step*(float)i;

            float a = MathF.Pow(1.0f - t, 3);
            float b = 3.0f*MathF.Pow(1.0f - t, 2)*t;
            float c = 3.0f*(1.0f - t)*MathF.Pow(t, 2);
            float d = MathF.Pow(t, 3);

            // Compute the endpoint for this segment
            current.Y = a*curveStartPosition.Y + b*curveStartPositionTangent.Y + c*curveEndPositionTangent.Y + d*curveEndPosition.Y;
            current.X = a*curveStartPosition.X + b*curveStartPositionTangent.X + c*curveEndPositionTangent.X + d*curveEndPosition.X;

            // Vector from previous to current
            Vector2 delta = new(current.X - previous.X, current.Y - previous.Y);

            // The right hand normal to the delta vector, which raymath leaves at zero for a delta of none
            Vector2 normal = new(-delta.Y, delta.X);
            if (normal != Vector2.Zero) normal = Vector2.Normalize(normal);

            // The v texture coordinate of the segment (add up the length of all the segments so far)
            float v = previousV + delta.Length()/(float)(texRoad.Height*2);

            // Make sure the start point has a normal
            if (!tangentSet)
            {
                previousTangent = normal;
                tangentSet = true;
            }

            // Extend out the normals from the previous and current points to get the quad for this segment
            Vector2 prevPosNormal = previous + previousTangent*curveWidth;
            Vector2 prevNegNormal = previous + previousTangent*-curveWidth;

            Vector2 currentPosNormal = current + normal*curveWidth;
            Vector2 currentNegNormal = current + normal*-curveWidth;

            // Draw the segment as a quad
            rlSetTexture(texRoad.Id);
            rlBegin(RlDrawMode.Quads);
                rlColor4ub(255, 255, 255, 255);
                rlNormal3f(0.0f, 0.0f, 1.0f);

                rlTexCoord2f(0, previousV);
                rlVertex2f(prevNegNormal.X, prevNegNormal.Y);

                rlTexCoord2f(1, previousV);
                rlVertex2f(prevPosNormal.X, prevPosNormal.Y);

                rlTexCoord2f(1, v);
                rlVertex2f(currentPosNormal.X, currentPosNormal.Y);

                rlTexCoord2f(0, v);
                rlVertex2f(currentNegNormal.X, currentNegNormal.Y);
            rlEnd();

            // The current step is the start of the next step
            previous = current;
            previousTangent = normal;
            previousV = v;
        }
    }
}
