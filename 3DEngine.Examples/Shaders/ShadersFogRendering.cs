// raylib's shaders_fog_rendering example, Copyright (c) 2019-2025 Chris Camacho (@chriscamacho) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.RLights;

namespace Engine.Examples;

public static class ShadersFogRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shaders] fog rendering");

        Camera3D camera = new(new Vector3(2.0f, 2.0f, 6.0f), new Vector3(0.0f, 0.5f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // Load models and texture
        Model modelA = LoadModelFromMesh(GenMeshTorus(0.4f, 1.0f, 16, 32));
        Model modelB = LoadModelFromMesh(GenMeshCube(1.0f, 1.0f, 1.0f));
        Model modelC = LoadModelFromMesh(GenMeshSphere(0.5f, 32, 32));
        Texture2D texture = LoadTexture("resources/texel_checker.png");

        // Assign texture to default model material
        modelA.Materials[0].Texture = texture;
        modelB.Materials[0].Texture = texture;
        modelC.Materials[0].Texture = texture;

        // raylib's lighting.vs and fog.fs, written in Slang for the model pass
        Shader shader = LoadShader("resources/shaders/slang/fog.slang");
        int viewLoc = GetShaderLocation(shader, "viewPos");

        // Ambient light level
        Vector4 ambient = new(0.2f, 0.2f, 0.2f, 1.0f);
        int ambientLoc = GetShaderLocation(shader, "ambient");
        SetShaderValue(shader, ambientLoc, ambient);

        Vector4 fogColor = ColorNormalize(Color.Gray);
        int fogColorLoc = GetShaderLocation(shader, "fogColor");
        SetShaderValue(shader, fogColorLoc, fogColor);

        float fogDensity = 0.15f;
        int fogDensityLoc = GetShaderLocation(shader, "fogDensity");
        SetShaderValue(shader, fogDensityLoc, fogDensity);

        // All models share the same shader
        modelA.Materials[0].Shader = shader;
        modelB.Materials[0].Shader = shader;
        modelC.Materials[0].Shader = shader;

        // One point light
        CreateLight(LIGHT_POINT, new Vector3(0, 2, 6), Vector3.Zero, Color.White, shader);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            if (IsKeyDown(Key.Up))
            {
                fogDensity += 0.001f;
                if (fogDensity > 1.0f) fogDensity = 1.0f;
            }

            if (IsKeyDown(Key.Down))
            {
                fogDensity -= 0.001f;
                if (fogDensity < 0.0f) fogDensity = 0.0f;
            }

            SetShaderValue(shader, fogDensityLoc, fogDensity);

            // Rotate the torus
            modelA.Transform = modelA.Transform*Matrix4x4.CreateRotationX(-0.025f);
            modelA.Transform = modelA.Transform*Matrix4x4.CreateRotationZ(0.012f);

            // Update the light shader with the camera view position
            SetShaderValue(shader, viewLoc, camera.Position);

            BeginDrawing();

                ClearBackground(Color.Gray);

                BeginMode3D(camera);

                    // Draw the three models
                    DrawModel(modelA, Vector3.Zero, 1.0f, Color.White);
                    DrawModel(modelB, new Vector3(-2.6f, 0, 0), 1.0f, Color.White);
                    DrawModel(modelC, new Vector3(2.6f, 0, 0), 1.0f, Color.White);

                    for (int i = -20; i < 20; i += 2) DrawModel(modelA, new Vector3((float)i, 0, 2), 1.0f, Color.White);

                EndMode3D();

                DrawText($"Use KEY_UP/KEY_DOWN to change fog density [{fogDensity:0.00}]", 10, 10, 20, Color.RayWhite);

            EndDrawing();
        }

        UnloadModel(modelA);
        UnloadModel(modelB);
        UnloadModel(modelC);
        UnloadTexture(texture);
        UnloadShader(shader);

        CloseWindow();
    }
}
