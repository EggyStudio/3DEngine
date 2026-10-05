// raylib's models_basic_voxel example, Copyright (c) 2025 Tim Little (@timlittle), under the zlib license,
// written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsBasicVoxel
{
    private const int WORLD_SIZE = 8;   // The world's cubes along each side

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] basic voxel");

        DisableCursor();

        // A first person camera at the ground
        Camera3D camera = new(new Vector3(-2.0f, 0.0f, -2.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        ModelMesh cubeMesh = GenMeshCube(1.0f, 1.0f, 1.0f);
        Model cubeModel = LoadModelFromMesh(cubeMesh);
        cubeModel.Materials[0].Color = Color.Beige;

        // Every voxel of the world filled
        bool[,,] voxels = new bool[WORLD_SIZE, WORLD_SIZE, WORLD_SIZE];
        for (int x = 0; x < WORLD_SIZE; x++)
            for (int y = 0; y < WORLD_SIZE; y++)
                for (int z = 0; z < WORLD_SIZE; z++)
                    voxels[x, y, z] = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.FirstPerson);

            // A click takes away the voxel under the crosshair, the nearest the ray from the
            // screen's center meets. Marching the ray through the grid would be faster.
            if (IsMouseButtonPressed(MouseButton.Left))
            {
                Vector2 screenCenter = new(GetScreenWidth()/2.0f, GetScreenHeight()/2.0f);
                Ray ray = GetScreenToWorldRay(screenCenter, camera);

                float closestDistance = 99999.0f;
                Vector3 closestVoxelPosition = new(-1, -1, -1);
                bool voxelFound = false;

                for (int x = 0; x < WORLD_SIZE; x++)
                {
                    for (int y = 0; y < WORLD_SIZE; y++)
                    {
                        for (int z = 0; z < WORLD_SIZE; z++)
                        {
                            if (!voxels[x, y, z]) continue;

                            Vector3 position = new((float)x, (float)y, (float)z);
                            BoundingBox box = new(position - new Vector3(0.5f), position + new Vector3(0.5f));

                            RayCollision collision = GetRayCollisionBox(ray, box);
                            if (collision.Hit && (collision.Distance < closestDistance))
                            {
                                closestDistance = collision.Distance;
                                closestVoxelPosition = position;
                                voxelFound = true;
                            }
                        }
                    }
                }

                if (voxelFound)
                {
                    voxels[(int)closestVoxelPosition.X, (int)closestVoxelPosition.Y, (int)closestVoxelPosition.Z] = false;
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawGrid(10, 1.0f);

                    for (int x = 0; x < WORLD_SIZE; x++)
                    {
                        for (int y = 0; y < WORLD_SIZE; y++)
                        {
                            for (int z = 0; z < WORLD_SIZE; z++)
                            {
                                if (!voxels[x, y, z]) continue;

                                Vector3 position = new((float)x, (float)y, (float)z);
                                DrawModel(cubeModel, position, 1.0f, Color.Beige);
                                DrawCubeWires(position, 1.0f, 1.0f, 1.0f, Color.Black);
                            }
                        }
                    }

                EndMode3D();

                // The crosshair the ray is cast from
                DrawCircle(GetScreenWidth()/2, GetScreenHeight()/2, 4, Color.Red);

                DrawText("Left-click a voxel to remove it!", 10, 10, 20, Color.DarkGray);
                DrawText("WASD to move, mouse to look around", 10, 35, 10, Color.Gray);

            EndDrawing();
        }

        UnloadModel(cubeModel);

        CloseWindow();
    }
}
