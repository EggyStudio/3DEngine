using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// The seven primitives an edit mesh is shaped from, a box, a plane, a cylinder, a cone, a wedge,
/// stairs and an arch, each made in an edit mesh of its own, built into a model and named with the
/// count of faces it holds before the build cuts them into triangles.
/// </summary>
public static class ModelsEditMesh
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] edit mesh");

        // Each placed by its lowest corner, so each stands on the ground two meters across its slot.
        (string Name, Action<EditMesh> Make)[] primitives =
        [
            ("box", mesh => EditMeshBox(mesh, new Vector3(2, 2, 2))),
            ("plane", mesh => EditMeshPlane(mesh, 2, 2, at: new Vector3(0, 0.02f, 0))),
            ("cylinder", mesh => EditMeshCylinder(mesh, 1, 2, 16, at: new Vector3(1, 0, 1))),
            ("cone", mesh => EditMeshCone(mesh, 1, 2, 16, at: new Vector3(1, 0, 1))),
            // Turned a quarter about their middles, so their slope and their steps face the camera.
            ("wedge", mesh => { EditMeshWedge(mesh, new Vector3(2, 2, 2)); EditMeshRotate(mesh, Vector3.UnitY, 90); }),
            ("stairs", mesh => { EditMeshStairs(mesh, 4, 0.5f, 0.5f, 2); EditMeshRotate(mesh, Vector3.UnitY, 90); }),
            ("arch", mesh => EditMeshArch(mesh, 1, 0.35f, 2, 12)),
        ];
        Color[] colors = [Color.Red, Color.Gold, Color.Lime, Color.SkyBlue, Color.Violet, Color.Orange, Color.Beige];

        var models = new Model[primitives.Length];
        var faces = new int[primitives.Length];
        for (int i = 0; i < primitives.Length; i++)
        {
            var mesh = LoadEditMesh();
            primitives[i].Make(mesh);
            faces[i] = GetEditMeshInfo(mesh).FaceCount;
            models[i] = LoadModelFromEditMesh(mesh);
            models[i].Materials[0] = new ModelMaterial(colors[i]) { Roughness = 0.7f };
            UnloadEditMesh(mesh);
        }
        var ground = LoadModelFromMesh(GenMeshPlane(120, 80, 1, 1));

        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.6f), new Color(255, 244, 225), 1.4f, castsShadows: true);
        SetAmbientLight(new Color(150, 175, 215), 0.35f);
        var camera = new Camera3D(new Vector3(0, 6, 16), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(new Color(200, 215, 230));
            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(120, 125, 130));
            for (int i = 0; i < models.Length; i++)
                DrawModel(models[i], Slot(i), 1, Color.White);
            EndMode3D();

            // Each name under its primitive, with the faces its edit mesh held.
            for (int i = 0; i < models.Length; i++)
            {
                var below = GetWorldToScreen(Slot(i) + new Vector3(1, 0, 3.2f), camera);
                var count = $"{faces[i]} faces";
                DrawText(primitives[i].Name, (int)below.X - MeasureText(primitives[i].Name, 18) / 2, (int)below.Y, 18, Color.Black);
                DrawText(count, (int)below.X - MeasureText(count, 16) / 2, (int)below.Y + 20, 16, Color.DarkGray);
            }
            DrawText("Seven primitives of an edit mesh, each built into a model", 10, 10, 20, Color.DarkGray);
            EndDrawing();
        }

        foreach (var model in models) UnloadModel(model);
        UnloadModel(ground);
        CloseWindow();
    }

    // The corner a primitive's slot starts at, seven slots three meters apart along X.
    private static Vector3 Slot(int i) => new((i - 3) * 3 - 1, 0, -1);
}
