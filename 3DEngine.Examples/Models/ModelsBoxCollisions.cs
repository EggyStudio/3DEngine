// raylib's models_box_collisions example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsBoxCollisions
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] box collisions");

        Camera3D camera = new(new Vector3(0.0f, 10.0f, 10.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Vector3 playerPosition = new(0.0f, 1.0f, 2.0f);
        Vector3 playerSize = new(1.0f, 2.0f, 1.0f);
        Color playerColor = Color.Green;

        Vector3 enemyBoxPos = new(-4.0f, 1.0f, 0.0f);
        Vector3 enemyBoxSize = new(2.0f, 2.0f, 2.0f);

        Vector3 enemySpherePos = new(4.0f, 0.0f, 0.0f);
        float enemySphereSize = 1.5f;

        bool collision = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // One arrow at a time moves the player.
            if (IsKeyDown(Key.Right)) playerPosition.X += 0.2f;
            else if (IsKeyDown(Key.Left)) playerPosition.X -= 0.2f;
            else if (IsKeyDown(Key.Down)) playerPosition.Z += 0.2f;
            else if (IsKeyDown(Key.Up)) playerPosition.Z -= 0.2f;

            collision = false;

            BoundingBox playerBox = new(playerPosition - playerSize/2, playerPosition + playerSize/2);

            // The player against the box,
            if (CheckCollisionBoxes(playerBox, new BoundingBox(enemyBoxPos - enemyBoxSize/2, enemyBoxPos + enemyBoxSize/2))) collision = true;

            // and against the sphere
            if (CheckCollisionBoxSphere(playerBox, enemySpherePos, enemySphereSize)) collision = true;

            if (collision) playerColor = Color.Red;
            else playerColor = Color.Green;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawCube(enemyBoxPos, enemyBoxSize.X, enemyBoxSize.Y, enemyBoxSize.Z, Color.Gray);
                    DrawCubeWires(enemyBoxPos, enemyBoxSize.X, enemyBoxSize.Y, enemyBoxSize.Z, Color.DarkGray);

                    DrawSphere(enemySpherePos, enemySphereSize, Color.Gray);
                    DrawSphereWires(enemySpherePos, enemySphereSize, 16, 16, Color.DarkGray);

                    DrawCubeV(playerPosition, playerSize, playerColor);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText("Move player with arrow keys to collide", 220, 40, 20, Color.Gray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
