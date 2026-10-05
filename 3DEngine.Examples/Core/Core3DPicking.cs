// raylib's core_3d_picking example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core3DPicking
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 3d picking");

        Camera3D camera = default;
        camera.Position = new Vector3(10.0f, 10.0f, 10.0f);
        camera.Target = new Vector3(0.0f, 0.0f, 0.0f);
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        camera.FovY = 45.0f;
        camera.Projection = CameraProjection.Perspective;

        Vector3 cubePosition = new(0.0f, 1.0f, 0.0f);
        Vector3 cubeSize = new(2.0f, 2.0f, 2.0f);

        Ray ray = default;
        RayCollision collision = default;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsCursorHidden()) UpdateCamera(ref camera, CameraMode.FirstPerson);

            if (IsMouseButtonPressed(MouseButton.Right))
            {
                if (IsCursorHidden()) EnableCursor();
                else DisableCursor();
            }

            if (IsMouseButtonPressed(MouseButton.Left))
            {
                if (!collision.Hit)
                {
                    ray = GetScreenToWorldRay(GetMousePosition(), camera);

                    collision = GetRayCollisionBox(ray,
                                new BoundingBox(new Vector3(cubePosition.X - cubeSize.X/2, cubePosition.Y - cubeSize.Y/2, cubePosition.Z - cubeSize.Z/2),
                                              new Vector3(cubePosition.X + cubeSize.X/2, cubePosition.Y + cubeSize.Y/2, cubePosition.Z + cubeSize.Z/2)));
                }
                else collision = default;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    if (collision.Hit)
                    {
                        DrawCube(cubePosition, cubeSize.X, cubeSize.Y, cubeSize.Z, Color.Red);
                        DrawCubeWires(cubePosition, cubeSize.X, cubeSize.Y, cubeSize.Z, Color.Maroon);

                        DrawCubeWires(cubePosition, cubeSize.X + 0.2f, cubeSize.Y + 0.2f, cubeSize.Z + 0.2f, Color.Green);
                    }
                    else
                    {
                        DrawCube(cubePosition, cubeSize.X, cubeSize.Y, cubeSize.Z, Color.Gray);
                        DrawCubeWires(cubePosition, cubeSize.X, cubeSize.Y, cubeSize.Z, Color.DarkGray);
                    }

                    DrawRay(ray, Color.Maroon);
                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText("Try clicking on the box with your mouse!", 240, 10, 20, Color.DarkGray);

                if (collision.Hit) DrawText("BOX SELECTED", (screenWidth - MeasureText("BOX SELECTED", 30))/2, (int)(screenHeight*0.1f), 30, Color.Green);

                DrawText("Right click mouse to toggle camera controls", 10, 430, 10, Color.Gray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
