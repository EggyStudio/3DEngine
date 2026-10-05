// raylib's models_geometric_shapes example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsGeometricShapes
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] geometric shapes");

        // The camera that looks into the world
        Camera3D camera = new(new Vector3(0.0f, 10.0f, 10.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
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

                    DrawCapsule(new Vector3(-3.0f, 1.5f, -4.0f), new Vector3(-4.0f, -1.0f, -4.0f), 1.2f, 8, 8, Color.Violet);
                    DrawCapsuleWires(new Vector3(-3.0f, 1.5f, -4.0f), new Vector3(-4.0f, -1.0f, -4.0f), 1.2f, 8, 8, Color.Purple);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
