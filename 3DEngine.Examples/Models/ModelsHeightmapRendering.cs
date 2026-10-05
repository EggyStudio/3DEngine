// raylib's models_heightmap_rendering example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsHeightmapRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] heightmap rendering");

        Camera3D camera = new(new Vector3(18.0f, 21.0f, 18.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Image image = LoadImage("resources/heightmap.png");
        Texture2D texture = LoadTextureFromImage(image);

        ModelMesh mesh = GenMeshHeightmap(image, new Vector3(16, 8, 16));
        Model model = LoadModelFromMesh(mesh);

        model.Materials[0].Texture = texture;
        Vector3 mapPosition = new(-8.0f, 0.0f, -8.0f);

        UnloadImage(image);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawModel(model, mapPosition, 1.0f, Color.Red);

                    DrawGrid(20, 1.0f);

                EndMode3D();

                DrawTexture(texture, screenWidth - texture.Width - 20, 20, Color.White);
                DrawRectangleLines(screenWidth - texture.Width - 20, 20, texture.Width, texture.Height, Color.Green);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadTexture(texture);
        UnloadModel(model);

        CloseWindow();
    }
}
