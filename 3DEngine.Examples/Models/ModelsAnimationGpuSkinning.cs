// raylib's models_animation_gpu_skinning example, Copyright (c) 2024-2025 Daniel Holden (@orangeduck),
// under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsAnimationGpuSkinning
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] animation gpu skinning");

        Camera3D camera = new(new Vector3(5.0f, 5.0f, 5.0f), new Vector3(0.0f, 1.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // Load gltf model
        Model model = LoadModel("resources/models/gltf/greenman.glb"); // Load character model
        Vector3 position = new(0.0f, 0.0f, 0.0f); // Set model position

        // raylib's skinning shader, which raylib built with SUPPORT_GPU_SKINNING needs, written in
        // Slang for the model pass. A skinned mesh is posed on the GPU here whatever its shader.
        Shader skinningShader = LoadShader("resources/shaders/slang/skinning.slang");

        // The model's first material from the file, raylib's materials[1], since raylib keeps a
        // default material at 0
        model.Materials[0].Shader = skinningShader;

        // Load gltf model animations
        ModelAnimation[] anims = LoadModelAnimations("resources/models/gltf/greenman.glb");
        int animCount = anims.Length;

        // Animation playing variables
        int animIndex = 0;          // Current animation playing
        int animCurrentFrame = 0;   // Current animation frame

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // Select current animation
            if (IsKeyPressed(Key.Right)) animIndex = (animIndex + 1)%animCount;
            else if (IsKeyPressed(Key.Left)) animIndex = (animIndex + animCount - 1)%animCount;

            // Update model animation
            animCurrentFrame = (animCurrentFrame + 1)%anims[animIndex].FrameCount;
            UpdateModelAnimation(model, anims[animIndex], (float)animCurrentFrame);

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

        UnloadModelAnimations(anims);   // Unload model animation
        UnloadModel(model);             // Unload model and meshes/material
        UnloadShader(skinningShader);   // Unload GPU skinning shader

        CloseWindow();
    }
}
