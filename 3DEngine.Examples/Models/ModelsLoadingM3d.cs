// raylib's models_loading_m3d example, Copyright (c) 2022-2025 bzt (@bztsrc), under the zlib license,
// written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsLoadingM3d
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] loading m3d");

        // Define the camera to look into our 3d world
        Camera3D camera = default;
        camera.Position = new Vector3(1.5f, 1.5f, 1.5f);    // Camera position
        camera.Target = new Vector3(0.0f, 0.4f, 0.0f);      // Camera looking at point
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);          // Camera up vector (rotation towards target)
        camera.FovY = 45.0f;                                // Camera field-of-view Y
        camera.Projection = CameraProjection.Perspective;   // Camera projection type

        // Load model
        Model model = LoadModel("resources/models/m3d/cesium_man.m3d");             // Load the animated model mesh and basic data
        Vector3 position = new(0.0f, 0.0f, 0.0f); // Set model position

        // Load animation data
        ModelAnimation[] anims = LoadModelAnimations("resources/models/m3d/cesium_man.m3d");
        int animCount = anims.Length;

        // Animation playing variables
        int animIndex = 0;                  // Current animation playing
        float animCurrentFrame = 0.0f;      // Current animation frame (supporting interpolated frames)

        SetTargetFPS(60);                   // Set our game to run at 60 frames-per-second

        while (!WindowShouldClose())        // Detect window close button or ESC key
        {
            // Update
            UpdateCamera(ref camera, CameraMode.Orbital);

            // Select current animation
            if (IsKeyPressed(Key.Right)) animIndex = (animIndex + 1)%animCount;
            else if (IsKeyPressed(Key.Left)) animIndex = (animIndex + animCount - 1)%animCount;

            // Update model animation
            animCurrentFrame += 1.0f;
            if (animCurrentFrame >= anims[animIndex].KeyframeCount) animCurrentFrame = 0.0f;
            UpdateModelAnimation(model, anims[animIndex], animCurrentFrame);

            // Draw
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    // Draw 3d model with texture
                    if (!IsKeyDown(Key.Space)) DrawModel(model, position, 1.0f, Color.White);
                    else
                    {
                        // Draw the animated skeleton
                        DrawModelSkeleton(model.Skeleton.Bones, anims[animIndex].KeyframePoses[(int)animCurrentFrame], 1.0f, Color.Red);
                    }

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText($"Current animation: {anims[animIndex].Name}", 10, 10, 20, Color.LightGray);
                DrawText("Press SPACE to draw skeleton", 10, 40, 20, Color.Maroon);
                DrawText("(c) CesiumMan model by KhronosGroup", GetScreenWidth() - 210, GetScreenHeight() - 20, 10, Color.Gray);

            EndDrawing();
        }

        // De-Initialization
        UnloadModelAnimations(anims);              // Unload model animations data
        UnloadModel(model);                        // Unload model

        CloseWindow();              // Close window and OpenGL context
    }

    // Draw model skeleton
    private static void DrawModelSkeleton(BoneInfo[] bones, Transform[] pose, float scale, Color color)
    {
        // Loop to (boneCount - 1) because the last one is a special "no bone" bone,
        // needed to workaround buggy models without a -1, a cube is always drawn at the origin
        for (int i = 0; i < bones.Length - 1; i++)
        {
            // Display the frame-pose skeleton
            DrawCube(pose[i].Position, scale*0.05f, scale*0.05f, scale*0.05f, color);

            if (bones[i].Parent >= 0)
            {
                DrawLine3D(pose[i].Position, pose[bones[i].Parent].Position, color);
            }
        }
    }
}
