// raylib's models_loading example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsLoading
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] loading");

        Camera3D camera = new(new Vector3(50.0f, 50.0f, 50.0f), new Vector3(0.0f, 12.0f, 0.0f), Vector3.UnitY, 45.0f);

        Model model = LoadModel("resources/models/obj/castle.obj");
        Texture2D texture = LoadTexture("resources/models/obj/castle_diffuse.png");
        model.Materials[0].Texture = texture;

        Vector3 position = Vector3.Zero;

        // The bounds of the model as loaded, which a model drawn scaled would scale too
        BoundingBox bounds = GetMeshBoundingBox(model.Meshes[0]);

        bool selected = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // A model or a texture dropped on the window replaces the one shown
            if (IsFileDropped())
            {
                string[] droppedFiles = LoadDroppedFiles();

                if (droppedFiles.Length == 1)
                {
                    string extension = Path.GetExtension(droppedFiles[0]).ToLowerInvariant();
                    if (extension is ".obj" or ".gltf" or ".glb" or ".vox" or ".iqm" or ".m3d")
                    {
                        UnloadModel(model);
                        model = LoadModel(droppedFiles[0]);
                        model.Materials[0].Texture = texture;

                        bounds = GetMeshBoundingBox(model.Meshes[0]);

                        // Far enough from the model to see all of it
                        camera.Position = bounds.Max + new Vector3(10.0f);
                    }
                    else if (extension == ".png")
                    {
                        UnloadTexture(texture);
                        texture = LoadTexture(droppedFiles[0]);
                        model.Materials[0].Texture = texture;
                    }
                }

                UnloadDroppedFiles();
            }

            if (IsMouseButtonPressed(MouseButton.Left))
            {
                if (GetRayCollisionBox(GetScreenToWorldRay(GetMousePosition(), camera), bounds).Hit) selected = !selected;
                else selected = false;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawModel(model, position, 1.0f, Color.White);

                    DrawGrid(20, 10.0f);

                    if (selected) DrawBoundingBox(bounds, Color.Green);

                EndMode3D();

                DrawText("Drag & drop model to load mesh/texture.", 10, GetScreenHeight() - 20, 10, Color.DarkGray);
                if (selected) DrawText("MODEL SELECTED", GetScreenWidth() - 110, 10, 10, Color.Green);

                DrawText("(c) Castle 3D model by Alberto Cano", screenWidth - 200, screenHeight - 20, 10, Color.Gray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadTexture(texture);
        UnloadModel(model);

        CloseWindow();
    }
}
