// raylib's shaders_model_shader example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersModelShader
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shaders] model shader");

        Camera3D camera = new(new Vector3(4.0f, 4.0f, 4.0f), new Vector3(0.0f, 1.0f, -1.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Model model = LoadModel("resources/models/watermill.obj");
        Texture2D texture = LoadTexture("resources/models/watermill_diffuse.png");

        // raylib's grayscale.fs, written in Slang for the model pass, a model's shader being one
        // that imports it
        Shader shader = LoadShader("resources/shaders/slang/grayscale_model.slang");

        model.Materials[0].Shader = shader;
        model.Materials[0].Texture = texture;

        Vector3 position = Vector3.Zero;

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawModel(model, position, 0.2f, Color.White);
                    DrawGrid(10, 1.0f);
                EndMode3D();

                DrawText("(c) Watermill 3D model by Alberto Cano", screenWidth - 210, screenHeight - 20, 10, Color.Gray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadTexture(texture);
        UnloadModel(model);

        CloseWindow();
    }
}
