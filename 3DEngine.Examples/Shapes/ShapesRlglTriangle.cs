// raylib's shapes_rlgl_triangle example, Copyright (c) 2025 Robin (@RobinsAviary), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesRlglTriangle
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shapes] rlgl triangle");

        // rlgl culls back faces from the start, and the engine's shapes draw both until culling is
        // turned on, so it is turned on here as raylib's starts.
        rlEnableBackfaceCulling();

        // Starting positions and rendered triangle positions
        Vector2[] startingPositions = [new(400.0f, 150.0f), new(300.0f, 300.0f), new(500.0f, 300.0f)];
        Vector2[] trianglePositions = [startingPositions[0], startingPositions[1], startingPositions[2]];

        // Currently selected vertex, -1 means none
        int triangleIndex = -1;
        bool linesMode = false;
        float handleRadius = 8.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space)) linesMode = !linesMode;

            // Check selected vertex
            for (int i = 0; i < 3; i++)
            {
                // If the mouse is within the handle circle
                if (CheckCollisionPointCircle(GetMousePosition(), trianglePositions[i], handleRadius) &&
                    IsMouseButtonDown(MouseButton.Left))
                {
                    triangleIndex = i;
                    break;
                }
            }

            // If the user has selected a vertex, offset it by the mouse's delta this frame
            if (triangleIndex != -1)
            {
                Vector2 mouseDelta = GetMouseDelta();
                trianglePositions[triangleIndex].X += mouseDelta.X;
                trianglePositions[triangleIndex].Y += mouseDelta.Y;
            }

            // Reset index on release
            if (IsMouseButtonReleased(MouseButton.Left)) triangleIndex = -1;

            // Enable/disable backface culling (2-sided triangles, slower to render)
            if (IsKeyPressed(Key.Left)) rlEnableBackfaceCulling();
            if (IsKeyPressed(Key.Right)) rlDisableBackfaceCulling();

            // Reset triangle vertices to starting positions and reset backface culling
            if (IsKeyPressed(Key.R))
            {
                trianglePositions[0] = startingPositions[0];
                trianglePositions[1] = startingPositions[1];
                trianglePositions[2] = startingPositions[2];

                rlEnableBackfaceCulling();
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (linesMode)
                {
                    // Draw triangle with lines
                    rlBegin(RlDrawMode.Lines);
                        // Three lines, six points
                        // Define color for next vertex
                        rlColor4ub(255, 0, 0, 255);
                        // Define vertex
                        rlVertex2f(trianglePositions[0].X, trianglePositions[0].Y);
                        rlColor4ub(0, 255, 0, 255);
                        rlVertex2f(trianglePositions[1].X, trianglePositions[1].Y);

                        rlColor4ub(0, 255, 0, 255);
                        rlVertex2f(trianglePositions[1].X, trianglePositions[1].Y);
                        rlColor4ub(0, 0, 255, 255);
                        rlVertex2f(trianglePositions[2].X, trianglePositions[2].Y);

                        rlColor4ub(0, 0, 255, 255);
                        rlVertex2f(trianglePositions[2].X, trianglePositions[2].Y);
                        rlColor4ub(255, 0, 0, 255);
                        rlVertex2f(trianglePositions[0].X, trianglePositions[0].Y);
                    rlEnd();
                }
                else
                {
                    // Draw triangle as a triangle
                    rlBegin(RlDrawMode.Triangles);
                        // One triangle, three points
                        // Define color for next vertex
                        rlColor4ub(255, 0, 0, 255);
                        // Define vertex
                        rlVertex2f(trianglePositions[0].X, trianglePositions[0].Y);
                        rlColor4ub(0, 255, 0, 255);
                        rlVertex2f(trianglePositions[1].X, trianglePositions[1].Y);
                        rlColor4ub(0, 0, 255, 255);
                        rlVertex2f(trianglePositions[2].X, trianglePositions[2].Y);
                    rlEnd();
                }

                // Render the vertex handles, reacting to mouse movement/input
                for (int i = 0; i < 3; i++)
                {
                    // Draw handle fill focused by mouse
                    if (CheckCollisionPointCircle(GetMousePosition(), trianglePositions[i], handleRadius))
                        DrawCircleV(trianglePositions[i], handleRadius, ColorAlpha(Color.DarkGray, 0.5f));

                    // Draw handle fill selected
                    if (i == triangleIndex) DrawCircleV(trianglePositions[i], handleRadius, Color.DarkGray);

                    // Draw handle outline
                    DrawCircleLinesV(trianglePositions[i], handleRadius, Color.Black);
                }

                // Draw controls
                DrawText("SPACE: Toggle lines mode", 10, 10, 20, Color.DarkGray);
                DrawText("LEFT-RIGHT: Toggle backface culling", 10, 40, 20, Color.DarkGray);
                DrawText("MOUSE: Click and drag vertex points", 10, 70, 20, Color.DarkGray);
                DrawText("R: Reset triangle to start positions", 10, 100, 20, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
