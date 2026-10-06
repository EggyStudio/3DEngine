// raylib's models_loading_iqm example, Copyright (c) 2019-2025 Culacant (@culacant) and Ramon Santamaria
// (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsLoadingIqm
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] loading iqm");

        // Define the camera to look into our 3d world
        Camera3D camera = default;
        camera.Position = new Vector3(10.0f, 10.0f, 10.0f); // Camera position
        camera.Target = new Vector3(0.0f, 4.0f, 0.0f);      // Camera looking at point
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);          // Camera up vector (rotation towards target)
        camera.FovY = 45.0f;                                // Camera field-of-view Y
        camera.Projection = CameraProjection.Perspective;   // Camera mode type

        Model model = LoadModel("resources/models/iqm/guy.iqm");                    // Load the animated model mesh and basic data
        Texture2D texture = LoadTexture("resources/models/iqm/guytex.png");         // Load model texture and set material
        SetMaterialTexture(ref model.Materials[0], MaterialMapIndex.Albedo, texture); // Set model material map texture, raylib's MATERIAL_MAP_DIFFUSE
        Vector3 position = new(0.0f, 0.0f, 0.0f); // Set model position

        // Load animation data
        ModelAnimation[] anims = LoadModelAnimations("resources/models/iqm/guyanim.iqm");
        int animCount = anims.Length;

        // Animation playing variables
        int animIndex = 0;                  // Current animation playing
        float animCurrentFrame = 0.0f;      // Current animation frame (supporting interpolated frames)
        float animSpeed = 1.0f;             // How fast the animation plays

        SetTargetFPS(60);                   // Set our game to run at 60 frames-per-second

        while (!WindowShouldClose())        // Detect window close button or ESC key
        {
            // Update
            UpdateCamera(ref camera, CameraMode.Orbital);

            if (IsKeyPressed(Key.Right))
            {
                animIndex = (animIndex == animCount - 1)? 0 : animIndex + 1;
                animCurrentFrame = 0.0f;
            }
            if (IsKeyPressed(Key.Left))
            {
                animIndex = (animIndex == 0)? animCount - 1 : animIndex - 1;
                animCurrentFrame = 0.0f;
            }

            if (IsKeyPressed(Key.Up))   animSpeed = MathF.Min(5.0f, animSpeed + 0.1f);
            if (IsKeyPressed(Key.Down)) animSpeed = MathF.Max(0.0f, animSpeed - 0.1f);

            animCurrentFrame += animSpeed;
            UpdateModelAnimation(model, anims[animIndex], animCurrentFrame);
            if (animCurrentFrame >= anims[animIndex].FrameCount) animCurrentFrame = 0;

            // Draw
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawModelEx(model, position, new Vector3(1.0f, 0.0f, 0.0f), -90.0f, new Vector3(1.0f, 1.0f, 1.0f), Color.White);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText($"Current animation: {anims[animIndex].Name}", 10, 10, 20, Color.Maroon);
                DrawText($"Animation speed: {animSpeed:0.00}", 10, 40, 20, Color.Maroon);
                DrawText("Use left and right arrow keys to change current animation", 10, screenHeight - 34, 10, Color.Black);
                DrawText("Use up and down arrow keys to change animation speed", 10, screenHeight - 20, 10, Color.Black);
                DrawText("(c) Guy IQM 3D model by @culacant", screenWidth - 200, screenHeight - 20, 10, Color.Gray);

            EndDrawing();
        }

        // De-Initialization
        UnloadTexture(texture);                    // Unload texture
        UnloadModelAnimations(anims);              // Unload model animations data
        UnloadModel(model);                        // Unload model

        CloseWindow();                  // Close window and OpenGL context
    }
}
