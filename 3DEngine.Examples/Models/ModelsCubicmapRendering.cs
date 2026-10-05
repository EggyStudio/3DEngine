// raylib's models_cubicmap_rendering example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsCubicmapRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] cubicmap rendering");

        Camera3D camera = new(new Vector3(16.0f, 14.0f, 16.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Image image = LoadImage("resources/cubicmap.png");
        Texture2D cubicmap = LoadTextureFromImage(image);

        // Pixel art drawn at four times its size, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(cubicmap, TextureFilter.Point);

        ModelMesh mesh = GenMeshCubicmap(image, new Vector3(1.0f, 1.0f, 1.0f));
        Model model = LoadModelFromMesh(mesh);

        // Each cube's faces take their part of the atlas.
        Texture2D texture = LoadTexture("resources/cubicmap_atlas.png");
        SetTextureFilter(texture, TextureFilter.Point);
        model.Materials[0].Texture = texture;

        Vector3 mapPosition = new(-16.0f, 0.0f, -8.0f);

        UnloadImage(image);

        bool pause = false;     // Stops the camera's circling

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.P)) pause = !pause;

            if (!pause) UpdateCamera(ref camera, CameraMode.Orbital);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawModel(model, mapPosition, 1.0f, Color.White);

                EndMode3D();

                DrawTextureEx(cubicmap, new Vector2(screenWidth - cubicmap.Width*4.0f - 20, 20.0f), 0.0f, 4.0f, Color.White);
                DrawRectangleLines(screenWidth - cubicmap.Width*4 - 20, 20, cubicmap.Width*4, cubicmap.Height*4, Color.Green);

                DrawText("cubicmap image used to", 658, 90, 10, Color.Gray);
                DrawText("generate map 3d model", 658, 104, 10, Color.Gray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadTexture(cubicmap);
        UnloadTexture(texture);
        UnloadModel(model);

        CloseWindow();
    }
}
