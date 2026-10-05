// raylib's models_waving_cubes example, Copyright (c) 2019-2025 Codecat (@codecat) and Ramon Santamaria
// (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsWavingCubes
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] waving cubes");

        Camera3D camera = new(new Vector3(30.0f, 20.0f, 30.0f), Vector3.Zero, Vector3.UnitY, 70.0f, CameraProjection.Perspective);

        // The blocks along each axis
        const int numBlocks = 15;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            double time = GetTime();

            // The cubes' spacing and size breathe with time,
            float scale = (2.0f + (float)Math.Sin(time))*0.7f;

            // and the camera circles the scene.
            double cameraTime = time*0.3;
            camera.Position.X = (float)Math.Cos(cameraTime)*40.0f;
            camera.Position.Z = (float)Math.Sin(cameraTime)*40.0f;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawGrid(10, 5.0f);

                    for (int x = 0; x < numBlocks; x++)
                    {
                        for (int y = 0; y < numBlocks; y++)
                        {
                            for (int z = 0; z < numBlocks; z++)
                            {
                                // A block's size by its place
                                float blockScale = (x + y + z)/30.0f;

                                // The wave, which moves each block by its size over time
                                float scatter = MathF.Sin(blockScale*20.0f + (float)(time*4.0f));

                                Vector3 cubePos = new(
                                    (float)(x - (float)numBlocks/2)*(scale*3.0f) + scatter,
                                    (float)(y - (float)numBlocks/2)*(scale*2.0f) + scatter,
                                    (float)(z - (float)numBlocks/2)*(scale*3.0f) + scatter);

                                // A hue by its place, a rainbow across the blocks
                                Color cubeColor = ColorFromHSV((float)(((x + y + z)*18)%360), 0.75f, 0.9f);

                                float cubeSize = (2.4f - scale)*blockScale;

                                DrawCube(cubePos, cubeSize, cubeSize, cubeSize, cubeColor);
                            }
                        }
                    }

                EndMode3D();

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
