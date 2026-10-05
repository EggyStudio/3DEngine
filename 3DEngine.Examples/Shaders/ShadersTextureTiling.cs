// raylib's shaders_texture_tiling example, Copyright (c) 2023-2025 Luis Almeida (@luis605), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersTextureTiling
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] texture tiling");

        Camera3D camera = new(new Vector3(4.0f, 4.0f, 4.0f), new Vector3(0.0f, 0.5f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        ModelMesh cube = GenMeshCube(1.0f, 1.0f, 1.0f);
        Model model = LoadModelFromMesh(cube);

        Texture2D texture = LoadTexture("resources/cubicmap_atlas.png");
        model.Materials[0].Texture = texture;

        // Pixel art, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(texture, TextureFilter.Point);

        // The texture repeated three times each way by a shader, raylib's tiling.fs written in
        // Slang for the model pass
        Vector2 tiling = new(3.0f, 3.0f);
        Shader shader = LoadShader("resources/shaders/slang/tiling.slang");

        SetTextureWrap(texture, TextureWrap.Repeat);
        SetShaderValue(shader, GetShaderLocation(shader, "tiling"), tiling);
        model.Materials[0].Shader = shader;

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            if (IsKeyPressed(Key.Z)) camera.Target = new Vector3(0.0f, 0.5f, 0.0f);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    BeginShaderMode(shader);
                        DrawModel(model, Vector3.Zero, 2.0f, Color.White);
                    EndShaderMode();

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText("Use mouse to rotate the camera", 10, 10, 20, Color.DarkGray);

            EndDrawing();
        }

        UnloadModel(model);
        UnloadShader(shader);
        UnloadTexture(texture);

        CloseWindow();
    }
}
