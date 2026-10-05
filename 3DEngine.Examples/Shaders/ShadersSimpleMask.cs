// raylib's shaders_simple_mask example, Copyright (c) 2019-2025 Chris Camacho (@chriscamacho) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersSimpleMask
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] simple mask");

        Camera3D camera = new(new Vector3(0.0f, 1.0f, 2.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // The two models the shader is shown on,
        ModelMesh torus = GenMeshTorus(0.3f, 1, 16, 32);
        Model model1 = LoadModelFromMesh(torus);

        ModelMesh cube = GenMeshCube(0.8f, 0.8f, 0.8f);
        Model model2 = LoadModelFromMesh(cube);

        // and one plain behind, seen through their gaps
        ModelMesh sphere = GenMeshSphere(1, 16, 16);
        Model model3 = LoadModelFromMesh(sphere);

        // raylib's mask.fs, written in Slang for the model pass
        Shader shader = LoadShader("resources/shaders/slang/mask.slang");

        Texture2D texDiffuse = LoadTexture("resources/plasma.png");
        model1.Materials[0].Texture = texDiffuse;
        model2.Materials[0].Texture = texDiffuse;

        // raylib hands the mask to its shader through the material's emission map. Here the
        // shader's own texture is set by name, as any is.
        Texture2D texMask = LoadTexture("resources/mask.png");
        SetShaderValueTexture(shader, GetShaderLocation(shader, "mask"), texMask);

        // A frame count, which moves the mask and the texture
        int shaderFrame = GetShaderLocation(shader, "frame");

        model1.Materials[0].Shader = shader;
        model2.Materials[0].Shader = shader;

        int framesCounter = 0;
        Vector3 rotation = Vector3.Zero;

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.FirstPerson);

            framesCounter++;
            rotation.X += 0.01f;
            rotation.Y += 0.005f;
            rotation.Z -= 0.0025f;

            SetShaderValue(shader, shaderFrame, framesCounter);

            model1.Transform = MatrixRotateXYZ(rotation);

            BeginDrawing();

                ClearBackground(Color.DarkBlue);

                BeginMode3D(camera);

                    DrawModel(model1, new Vector3(0.5f, 0.0f, 0.0f), 1, Color.White);
                    DrawModelEx(model2, new Vector3(-0.5f, 0.0f, 0.0f), new Vector3(1.0f, 1.0f, 0.0f), 50, Vector3.One, Color.White);
                    DrawModel(model3, new Vector3(0.0f, 0.0f, -1.5f), 1, Color.White);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                // raylib draws the frame count at 698, below its 450 window, so it is never seen.
                DrawRectangle(16, 698, MeasureText($"Frame: {framesCounter}", 20) + 8, 42, Color.Blue);
                DrawText($"Frame: {framesCounter}", 20, 700, 20, Color.White);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadModel(model1);
        UnloadModel(model2);
        UnloadModel(model3);

        UnloadTexture(texDiffuse);
        UnloadTexture(texMask);

        UnloadShader(shader);

        CloseWindow();
    }
}
