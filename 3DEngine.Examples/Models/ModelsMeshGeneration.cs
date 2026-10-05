using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsMeshGeneration
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] mesh generation");

        var camera = new Camera3D(new Vector3(0, 7, 9), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

        // Every generator, each as a model sharing one checkered texture.
        var checker = LoadTextureFromImage(GenImageChecked(64, 64, 8, 8, Color.White, Color.Gray));
        (string Name, Model Model)[] models =
        [
            ("plane", LoadModelFromMesh(GenMeshPlane(1.6f, 1.6f, 4, 4))),
            ("cube", LoadModelFromMesh(GenMeshCube(1.2f, 1.2f, 1.2f))),
            ("sphere", LoadModelFromMesh(GenMeshSphere(0.7f, 16, 32))),
            ("hemisphere", LoadModelFromMesh(GenMeshHemiSphere(0.8f, 8, 32))),
            ("cylinder", LoadModelFromMesh(GenMeshCylinder(0.6f, 1.4f, 32))),
            ("torus", LoadModelFromMesh(GenMeshTorus(0.36f, 1.1f, 48, 16))),
            ("knot", LoadModelFromMesh(GenMeshKnot(1.2f, 1.0f, 128, 12))),
            ("poly", LoadModelFromMesh(GenMeshPoly(6, 0.8f))),
            ("cone", LoadModelFromMesh(GenMeshCone(0.7f, 1.4f, 32))),
        ];
        foreach (var (_, model) in models) model.Materials[0].Texture = checker;

        var wires = false;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);
            if (IsKeyPressed(Key.Space)) wires = !wires;

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            for (int i = 0; i < models.Length; i++)
            {
                var at = new Vector3((i % 3 - 1) * 3, models[i].Name is "torus" or "knot" or "sphere" ? 0.8f : 0, (i / 3 - 1) * 3);
                if (wires) DrawModelWires(models[i].Model, at, 1, Color.DarkBlue);
                else DrawModel(models[i].Model, at, 1, Color.White);
            }
            DrawGrid(10, 1);
            EndMode3D();

            DrawText("Every GenMesh function. Space shows the wires.", 10, 10, 20, Color.DarkGray);
            EndDrawing();
        }

        foreach (var (_, model) in models) UnloadModel(model);
        UnloadTexture(checker);
        CloseWindow();
    }
}
