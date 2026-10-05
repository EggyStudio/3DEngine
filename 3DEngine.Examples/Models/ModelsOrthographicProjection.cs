// raylib's models_orthographic_projection example, Copyright (c) 2018-2025 Max Danielsson (@autious) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsOrthographicProjection
{
    private const float FOVY_PERSPECTIVE = 45.0f;
    private const float WIDTH_ORTHOGRAPHIC = 10.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] orthographic projection");

        Camera3D camera = new(new Vector3(0.0f, 10.0f, 10.0f), Vector3.Zero, Vector3.UnitY, FOVY_PERSPECTIVE, CameraProjection.Perspective);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space))
            {
                if (camera.Projection == CameraProjection.Perspective)
                {
                    camera.FovY = WIDTH_ORTHOGRAPHIC;
                    camera.Projection = CameraProjection.Orthographic;
                }
                else
                {
                    camera.FovY = FOVY_PERSPECTIVE;
                    camera.Projection = CameraProjection.Perspective;
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawCube(new Vector3(-4.0f, 0.0f, 2.0f), 2.0f, 5.0f, 2.0f, Color.Red);
                    DrawCubeWires(new Vector3(-4.0f, 0.0f, 2.0f), 2.0f, 5.0f, 2.0f, Color.Gold);
                    DrawCubeWires(new Vector3(-4.0f, 0.0f, -2.0f), 3.0f, 6.0f, 2.0f, Color.Maroon);

                    DrawSphere(new Vector3(-1.0f, 0.0f, -2.0f), 1.0f, Color.Green);
                    DrawSphereWires(new Vector3(1.0f, 0.0f, 2.0f), 2.0f, 16, 16, Color.Lime);

                    DrawCylinder(new Vector3(4.0f, 0.0f, -2.0f), 1.0f, 2.0f, 3.0f, 4, Color.SkyBlue);
                    DrawCylinderWires(new Vector3(4.0f, 0.0f, -2.0f), 1.0f, 2.0f, 3.0f, 4, Color.DarkBlue);
                    DrawCylinderWires(new Vector3(4.5f, -1.0f, 2.0f), 1.0f, 1.0f, 2.0f, 6, Color.Brown);

                    DrawCylinder(new Vector3(1.0f, 0.0f, -4.0f), 0.0f, 1.5f, 3.0f, 8, Color.Gold);
                    DrawCylinderWires(new Vector3(1.0f, 0.0f, -4.0f), 0.0f, 1.5f, 3.0f, 8, Color.Pink);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText("Press Spacebar to switch camera type", 10, GetScreenHeight() - 30, 20, Color.DarkGray);

                if (camera.Projection == CameraProjection.Orthographic) DrawText("ORTHOGRAPHIC", 10, 40, 20, Color.Black);
                else if (camera.Projection == CameraProjection.Perspective) DrawText("PERSPECTIVE", 10, 40, 20, Color.Black);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
