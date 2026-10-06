// raylib's models_animation_blend_custom example, Copyright (c) 2026 dmitrii-brand (@dmitrii-brand),
// under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsAnimationBlendCustom
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] animation blend custom");

        Camera3D camera = new(new Vector3(4.0f, 4.0f, 4.0f), new Vector3(0.0f, 1.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // Load gltf model
        Model model = LoadModel("resources/models/gltf/greenman.glb");
        Vector3 position = new(0.0f, 0.0f, 0.0f); // Set model position

        // raylib's skinning shader, written in Slang for the model pass, on the model's first
        // material from the file, raylib's materials[1], since raylib keeps a default material at 0
        Shader skinningShader = LoadShader("resources/shaders/slang/skinning.slang");
        model.Materials[0].Shader = skinningShader;

        // Load gltf model animations
        ModelAnimation[] anims = LoadModelAnimations("resources/models/gltf/greenman.glb");
        int animCount = anims.Length;

        // Use specific animation indices: 2-walk/move, 3-attack
        int animIndex0 = 2; // Walk/Move animation (index 2)
        int animIndex1 = 3; // Attack animation (index 3)
        int animCurrentFrame0 = 0;
        int animCurrentFrame1 = 0;

        // Validate indices
        if (animIndex0 >= animCount) animIndex0 = 0;
        if (animIndex1 >= animCount) animIndex1 = (animCount > 1) ? 1 : 0;

        bool upperBodyBlend = true;     // Toggle: true = upper/lower body blending, false = uniform blending (50/50)

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // Toggle upper/lower body blending mode (SPACE key)
            if (IsKeyPressed(Key.Space)) upperBodyBlend = !upperBodyBlend;

            // Update animation frames
            ModelAnimation anim0 = anims[animIndex0];
            ModelAnimation anim1 = anims[animIndex1];

            animCurrentFrame0 = (animCurrentFrame0 + 1)%anim0.FrameCount;
            animCurrentFrame1 = (animCurrentFrame1 + 1)%anim1.FrameCount;

            // Blend the two animations
            // When upperBodyBlend is ON: upper body = attack (1.0), lower body = walk (0.0)
            // When upperBodyBlend is OFF: uniform blend at 0.5 (50% walk, 50% attack)
            float blendFactor = (upperBodyBlend? 1.0f : 0.5f);
            UpdateModelAnimationBones(model, anim0, animCurrentFrame0,
                anim1, animCurrentFrame1, blendFactor, upperBodyBlend);

            // raylib provided animation blending function
            //UpdateModelAnimationEx(model, anim0, (float)animCurrentFrame0,
            //    anim1, (float)animCurrentFrame1, blendFactor);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawModel(model, position, 1.0f, Color.White);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                // Draw UI
                DrawText($"ANIM 0: {anim0.Name}", 10, 10, 20, Color.Gray);
                DrawText($"ANIM 1: {anim1.Name}", 10, 40, 20, Color.Gray);
                DrawText($"[SPACE] Toggle blending mode: {(upperBodyBlend? "Upper/Lower Body Blending" : "Uniform Blending")}",
                    10, GetScreenHeight() - 30, 20, Color.DarkGray);

            EndDrawing();
        }

        UnloadModelAnimations(anims);   // Unload model animation
        UnloadModel(model);             // Unload model and meshes/material
        UnloadShader(skinningShader);   // Unload GPU skinning shader

        CloseWindow();
    }

    // Check if a bone is part of upper body (for selective blending)
    private static bool IsUpperBodyBone(string boneName)
    {
        // Common upper body bone names (adjust based on your model)
        if (boneName is "spine" or "spine1" or "spine2" or
            "chest" or "upperChest" or
            "neck" or "head" or
            "shoulder" or "shoulder_L" or "shoulder_R" or
            "upperArm" or "upperArm_L" or "upperArm_R" or
            "lowerArm" or "lowerArm_L" or "lowerArm_R" or
            "hand" or "hand_L" or "hand_R" or
            "clavicle" or "clavicle_L" or "clavicle_R")
        {
            return true;
        }

        // Check if bone name contains upper body keywords
        if (boneName.Contains("spine") || boneName.Contains("chest") ||
            boneName.Contains("neck") || boneName.Contains("head") ||
            boneName.Contains("shoulder") || boneName.Contains("arm") ||
            boneName.Contains("hand") || boneName.Contains("clavicle"))
        {
            return true;
        }

        return false;
    }

    // Blend two animations per-bone with selective upper/lower body blending
    private static void UpdateModelAnimationBones(Model model, ModelAnimation anim0, int frame0,
        ModelAnimation anim1, int frame1, float blend, bool upperBodyBlend)
    {
        // Validate inputs
        if ((anim0.BoneCount != 0) && (anim0.FrameCount != 0) &&
            (anim1.BoneCount != 0) && (anim1.FrameCount != 0) &&
            (model.Bones.Length != 0) && (model.BindPose.Length != 0))
        {
            // Clamp blend factor to [0, 1]
            blend = MathF.Min(1.0f, MathF.Max(0.0f, blend));

            // Ensure frame indices are valid
            if (frame0 >= anim0.FrameCount) frame0 = anim0.FrameCount - 1;
            if (frame1 >= anim1.FrameCount) frame1 = anim1.FrameCount - 1;
            if (frame0 < 0) frame0 = 0;
            if (frame1 < 0) frame1 = 0;

            // Get bone count (use minimum of all to be safe)
            int boneCount = model.Bones.Length;
            if (anim0.BoneCount < boneCount) boneCount = anim0.BoneCount;
            if (anim1.BoneCount < boneCount) boneCount = anim1.BoneCount;

            // Bones past the count are left at their bind pose.
            Transform[] pose = [.. model.BindPose];

            // Blend each bone
            for (int boneIndex = 0; boneIndex < boneCount; boneIndex++)
            {
                // Determine blend factor for this bone
                float boneBlendFactor = blend;

                // If upper body blending is enabled, use different blend factors for upper vs lower body
                if (upperBodyBlend)
                {
                    string boneName = model.Bones[boneIndex].Name;
                    bool isUpperBody = IsUpperBodyBone(boneName);

                    // Upper body: use anim1 (attack), Lower body: use anim0 (walk)
                    // blend = 0.0 means full anim0 (walk), 1.0 means full anim1 (attack)
                    if (isUpperBody) boneBlendFactor = blend; // Upper body: blend towards anim1 (attack)
                    else boneBlendFactor = 1.0f - blend; // Lower body: blend towards anim0 (walk), the blend inverted
                }

                // Get transforms from both animations
                Transform animTransform0 = anim0.FramePoses[frame0][boneIndex];
                Transform animTransform1 = anim1.FramePoses[frame1][boneIndex];

                // Blend the transforms
                pose[boneIndex] = new Transform
                {
                    Position = Vector3.Lerp(animTransform0.Position, animTransform1.Position, boneBlendFactor),
                    Rotation = Quaternion.Slerp(animTransform0.Rotation, animTransform1.Rotation, boneBlendFactor),
                    Scale = Vector3.Lerp(animTransform0.Scale, animTransform1.Scale, boneBlendFactor),
                };
            }

            // raylib turns each bone's blended pose and its bind pose into the bone's matrix and
            // skins the vertices on the CPU itself. Here the blended poses are a clip of one frame,
            // which UpdateModelAnimation skins on the GPU from the same bind pose.
            UpdateModelAnimation(model, new ModelAnimation { Name = "blend", Bones = model.Bones, FramePoses = [pose] }, 0);
        }
    }
}
