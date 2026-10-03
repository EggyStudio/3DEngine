using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsLoading
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] loading");

        var camera = new Camera3D(new Vector3(6, 4, 6), Vector3.Zero, Vector3.UnitY, 45);

        // An OBJ with its material and texture beside it, loaded through Assimp.
        var torus = LoadModel("resources/torus.obj");
        var bounds = GetModelBoundingBox(torus);

        // Models made from generated meshes, one with a texture assigned to its material.
        var checker = LoadTextureFromImage(GenImageChecked(64, 64, 8, 8, Color.White, Color.LightGray));
        var cube = LoadModelFromMesh(GenMeshCube(1.5f, 1.5f, 1.5f));
        cube.Materials[0].Texture = checker;
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 16, 32));
        var floor = LoadModelFromMesh(GenMeshPlane(10, 10, 1, 1));

        var angle = 0f;
        var showBounds = true;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);
            angle += 30 * GetFrameTime();
            if (IsKeyPressed(Key.B)) showBounds = !showBounds;

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            DrawModel(floor, new Vector3(0, -1, 0), 1, Color.LightGray);
            DrawModelEx(torus, Vector3.Zero, Vector3.UnitY, angle, Vector3.One, Color.White);
            DrawModel(cube, new Vector3(-3, 0, 0), 1, Color.White);
            DrawModel(sphere, new Vector3(3, 0, 0), 1, Color.SkyBlue);
            if (showBounds) DrawBoundingBox(bounds, Color.Lime);
            DrawGrid(10, 1);
            EndMode3D();

            DrawText($"torus.obj: {torus.Meshes.Sum(m => m.TriangleCount)} triangles. B shows its bounds.", 10, 10, 20, Color.DarkGray);
            DrawFPS(10, 40);
            EndDrawing();
        }

        UnloadModel(torus);
        UnloadModel(cube);
        UnloadModel(sphere);
        UnloadModel(floor);
        UnloadTexture(checker);
        CloseWindow();
    }
}
