// raylib's models_rotating_cube example, Copyright (c) 2025 Jopestpe (@jopestpe), under the zlib license,
// written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsRotatingCube
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] rotating cube");

        Camera3D camera = new(new Vector3(0.0f, 3.0f, 3.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // A cube textured with a quarter of the maze's atlas
        Model model = LoadModelFromMesh(GenMeshCube(1.0f, 1.0f, 1.0f));

        Image img = LoadImage("resources/cubicmap_atlas.png");
        Image crop = ImageFromImage(img, new Rectangle(0, img.Height/2.0f, img.Width/2.0f, img.Height/2.0f));
        Texture2D texture = LoadTextureFromImage(crop);
        UnloadImage(img);
        UnloadImage(crop);

        // Pixel art, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(texture, TextureFilter.Point);

        model.Materials[0].Texture = texture;

        float rotation = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            rotation += 1.0f;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    // Placed, turned about an axis by degrees, scaled and tinted
                    DrawModelEx(model, Vector3.Zero, new Vector3(0.5f, 1.0f, 0.0f), rotation, Vector3.One, Color.White);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadTexture(texture);
        UnloadModel(model);

        CloseWindow();
    }
}
