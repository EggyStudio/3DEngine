// raylib's models_mesh_generation example, Copyright (c) 2017-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsMeshGeneration
{
    private const int NUM_MODELS = 9;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] mesh generation");

        // A checkered image of four texels for the models' texture
        Image @checked = GenImageChecked(2, 2, 1, 1, Color.Red, Color.Green);
        Texture2D texture = LoadTextureFromImage(@checked);
        UnloadImage(@checked);

        // Two texels each way, sampled by the nearest as raylib samples every texture
        SetTextureFilter(texture, TextureFilter.Point);

        Model[] models =
        [
            LoadModelFromMesh(GenMeshPlane(2, 2, 4, 3)),
            LoadModelFromMesh(GenMeshCube(2.0f, 1.0f, 2.0f)),
            LoadModelFromMesh(GenMeshSphere(2, 32, 32)),
            LoadModelFromMesh(GenMeshHemiSphere(2, 16, 16)),
            LoadModelFromMesh(GenMeshCylinder(1, 2, 16)),
            LoadModelFromMesh(GenMeshTorus(0.25f, 4.0f, 16, 32)),
            LoadModelFromMesh(GenMeshKnot(1.0f, 2.0f, 16, 128)),
            LoadModelFromMesh(GenMeshPoly(5, 2.0f)),
            LoadModelFromMesh(GenMeshCustom()),
        ];

        // A generated mesh can be written out with ExportMesh.

        // The checkered texture on every model
        for (int i = 0; i < NUM_MODELS; i++) models[i].Materials[0].Texture = texture;

        Camera3D camera = new(new Vector3(5.0f, 5.0f, 5.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        Vector3 position = Vector3.Zero;

        int currentModel = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            if (IsMouseButtonPressed(MouseButton.Left))
            {
                currentModel = (currentModel + 1)%NUM_MODELS;
            }

            if (IsKeyPressed(Key.Right))
            {
                currentModel++;
                if (currentModel >= NUM_MODELS) currentModel = 0;
            }
            else if (IsKeyPressed(Key.Left))
            {
                currentModel--;
                if (currentModel < 0) currentModel = NUM_MODELS - 1;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                   DrawModel(models[currentModel], position, 1.0f, Color.White);
                   DrawGrid(10, 1.0f);

                EndMode3D();

                DrawRectangle(30, 400, 310, 30, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(30, 400, 310, 30, Fade(Color.DarkBlue, 0.5f));
                DrawText("MOUSE LEFT BUTTON to CYCLE PROCEDURAL MODELS", 40, 410, 10, Color.Blue);

                switch (currentModel)
                {
                    case 0: DrawText("PLANE", 680, 10, 20, Color.DarkBlue); break;
                    case 1: DrawText("CUBE", 680, 10, 20, Color.DarkBlue); break;
                    case 2: DrawText("SPHERE", 680, 10, 20, Color.DarkBlue); break;
                    case 3: DrawText("HEMISPHERE", 640, 10, 20, Color.DarkBlue); break;
                    case 4: DrawText("CYLINDER", 680, 10, 20, Color.DarkBlue); break;
                    case 5: DrawText("TORUS", 680, 10, 20, Color.DarkBlue); break;
                    case 6: DrawText("KNOT", 680, 10, 20, Color.DarkBlue); break;
                    case 7: DrawText("POLY", 680, 10, 20, Color.DarkBlue); break;
                    case 8: DrawText("Custom (triangle)", 580, 10, 20, Color.DarkBlue); break;
                    default: break;
                }

            EndDrawing();
        }

        UnloadTexture(texture);

        for (int i = 0; i < NUM_MODELS; i++) UnloadModel(models[i]);

        CloseWindow();
    }

    // A triangle made in code, its three corners with their normals and texture coordinates. raylib
    // fills a mesh's arrays and uploads it, and here the corners are ModelVertex values and the
    // triangle their indices.
    private static ModelMesh GenMeshCustom()
    {
        ModelVertex[] vertices =
        [
            new(new Vector3(0, 0, 0), Vector3.UnitY, new Vector2(0, 0)),
            new(new Vector3(1, 0, 2), Vector3.UnitY, new Vector2(0.5f, 1.0f)),
            new(new Vector3(2, 0, 0), Vector3.UnitY, new Vector2(1, 0)),
        ];

        return UploadMesh(vertices, [0, 1, 2]);
    }
}
