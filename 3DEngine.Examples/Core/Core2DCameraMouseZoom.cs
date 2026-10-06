// raylib's core_2d_camera_mouse_zoom example, Copyright (c) 2022-2025 Jeffery Myers (@JeffM2501), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core2DCameraMouseZoom
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 2d camera mouse zoom");

        Camera2D camera = new(Vector2.Zero, Vector2.Zero, 0.0f, 1.0f);

        int zoomMode = 0;       // 0-Mouse Wheel, 1-Mouse Move

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Alpha1)) zoomMode = 0;
            else if (IsKeyPressed(Key.Alpha2)) zoomMode = 1;

            // Translate based on mouse right click
            if (IsMouseButtonDown(MouseButton.Left))
            {
                Vector2 delta = GetMouseDelta();
                delta *= -1.0f/camera.Zoom;
                camera.Target += delta;
            }

            if (zoomMode == 0)
            {
                // Zoom based on mouse wheel
                float wheel = GetMouseWheelMove();
                if (wheel != 0)
                {
                    // Get the world point that is under the mouse
                    Vector2 mouseWorldPos = GetScreenToWorld2D(GetMousePosition(), camera);

                    // Set the offset to where the mouse is
                    camera.Offset = GetMousePosition();

                    // Set the target to match, so that the camera maps the world space point
                    // under the cursor to the screen space point under the cursor at any zoom
                    camera.Target = mouseWorldPos;

                    // Zoom increment
                    // Uses log scaling to provide consistent zoom speed
                    float scale = 0.2f*wheel;
                    camera.Zoom = Math.Clamp(MathF.Exp(MathF.Log(camera.Zoom) + scale), 0.125f, 64.0f);
                }
            }
            else
            {
                // Zoom based on mouse right click
                if (IsMouseButtonPressed(MouseButton.Right))
                {
                    // Get the world point that is under the mouse
                    Vector2 mouseWorldPos = GetScreenToWorld2D(GetMousePosition(), camera);

                    // Set the offset to where the mouse is
                    camera.Offset = GetMousePosition();

                    // Set the target to match, so that the camera maps the world space point
                    // under the cursor to the screen space point under the cursor at any zoom
                    camera.Target = mouseWorldPos;
                }

                if (IsMouseButtonDown(MouseButton.Right))
                {
                    // Zoom increment
                    // Uses log scaling to provide consistent zoom speed
                    float deltaX = GetMouseDelta().X;
                    float scale = 0.005f*deltaX;
                    camera.Zoom = Math.Clamp(MathF.Exp(MathF.Log(camera.Zoom) + scale), 0.125f, 64.0f);
                }
            }

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                BeginMode2D(camera);
                    // Draw the 3d grid, rotated 90 degrees and centered around 0,0
                    // so there is something in the XY plane
                    rlPushMatrix();
                        rlTranslatef(0, 25*50, 0);
                        rlRotatef(90, 1, 0, 0);
                        DrawGrid(100, 50);
                    rlPopMatrix();

                    // Draw a reference circle
                    DrawCircle(GetScreenWidth()/2, GetScreenHeight()/2, 50, Color.Maroon);
                EndMode2D();

                // Draw mouse reference
                DrawCircleV(GetMousePosition(), 4, Color.DarkGray);
                DrawTextEx(GetFontDefault(), $"[{GetMouseX()}, {GetMouseY()}]",
                    GetMousePosition() + new Vector2(-44, -24), 20, 2, Color.Black);

                DrawText("[1][2] Select mouse zoom mode (Wheel or Move)", 20, 20, 20, Color.DarkGray);
                if (zoomMode == 0) DrawText("Mouse left button drag to move, mouse wheel to zoom", 20, 50, 20, Color.DarkGray);
                else DrawText("Mouse left button drag to move, mouse press and move to zoom", 20, 50, 20, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
