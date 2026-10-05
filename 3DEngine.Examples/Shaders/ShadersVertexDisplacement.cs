// raylib's shaders_vertex_displacement example, Copyright (c) 2023-2025 Alex ZH (@ZzzhHe), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersVertexDisplacement
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] vertex displacement");

        Camera3D camera = new(new Vector3(20.0f, 5.0f, -20.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY, 60.0f, CameraProjection.Perspective);

        // raylib's vertex_displacement.vs and vertex_displacement.fs, written in Slang for the model pass
        Shader shader = LoadShader("resources/shaders/slang/vertex_displacement.slang");

        // Load perlin noise texture
        Image perlinNoiseImage = GenImagePerlinNoise(512, 512, 0, 0, 1.0f);
        Texture2D perlinNoiseMap = LoadTextureFromImage(perlinNoiseImage);
        UnloadImage(perlinNoiseImage);

        // Sampled by the nearest texel, as raylib samples every texture
        SetTextureFilter(perlinNoiseMap, TextureFilter.Point);

        // The noise for the shader, which raylib binds to a texture slot through rlgl
        int perlinNoiseMapLoc = GetShaderLocation(shader, "perlinNoiseMap");
        SetShaderValueTexture(shader, perlinNoiseMapLoc, perlinNoiseMap);

        // Create a plane mesh and model
        ModelMesh planeMesh = GenMeshPlane(50, 50, 50, 50);
        Model planeModel = LoadModelFromMesh(planeMesh);
        // Set plane model material
        planeModel.Materials[0].Shader = shader;

        float time = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            time += GetFrameTime();
            SetShaderValue(shader, GetShaderLocation(shader, "time"), time);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    BeginShaderMode(shader);
                        // Draw plane model
                        DrawModel(planeModel, new Vector3(0.0f, 0.0f, 0.0f), 1.0f, new Color(255, 255, 255, 255));
                    EndShaderMode();

                EndMode3D();

                DrawText("Vertex displacement", 10, 10, 20, Color.DarkGray);
                DrawFPS(10, 40);

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadModel(planeModel);
        UnloadTexture(perlinNoiseMap);

        CloseWindow();
    }
}
