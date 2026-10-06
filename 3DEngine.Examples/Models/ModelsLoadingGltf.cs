// raylib's models_loading_gltf example, Copyright (c) 2020-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsLoadingGltf
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] loading gltf");

        Camera3D camera = new(new Vector3(6.0f, 6.0f, 6.0f), new Vector3(0.0f, 2.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Model model = LoadModel("resources/models/gltf/robot.glb");
        Vector3 position = Vector3.Zero;

        ModelAnimation[] anims = LoadModelAnimations("resources/models/gltf/robot.glb");
        int animCount = anims.Length;

        int animIndex = 0;
        int animCurrentFrame = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // The arrows pick the clip,
            if (IsKeyPressed(Key.Right)) animIndex = (animIndex + 1)%animCount;
            else if (IsKeyPressed(Key.Left)) animIndex = (animIndex + animCount - 1)%animCount;

            // which plays a frame a frame. raylib's keyframeCount is FrameCount here.
            animCurrentFrame = (animCurrentFrame + 1)%anims[animIndex].KeyframeCount;
            UpdateModelAnimation(model, anims[animIndex], animCurrentFrame);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawModel(model, position, 1.0f, Color.White);
                    DrawGrid(10, 1.0f);
                EndMode3D();

                DrawText($"Current animation: {anims[animIndex].Name}", 10, 40, 20, Color.Maroon);
                DrawText("Use the LEFT/RIGHT keys to switch animation", 10, 10, 20, Color.Gray);

            EndDrawing();
        }

        UnloadModelAnimations(anims);
        UnloadModel(model);

        CloseWindow();
    }
}
