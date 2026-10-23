using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// Three boxes of an edit mesh, one beveled round on every edge, one with its top inset and sunk
/// into a tray, and one loop cut round its middle with the loop pushed out, each built into a model
/// and named with the faces its mesh holds.
/// </summary>
public static class ModelsEditMeshCuts
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] edit mesh cuts");

        (string Name, Action<EditMesh> Make)[] boxes =
        [
            ("bevel", mesh =>
            {
                EditMeshBox(mesh, new Vector3(2, 2, 2));
                EditMeshBevel(mesh, 0.3f, 3);
            }),
            ("inset", mesh =>
            {
                EditMeshBox(mesh, new Vector3(2, 2, 2));
                EditMeshSelectFacing(mesh, Vector3.UnitY);
                EditMeshInset(mesh, 0.35f);
                EditMeshExtrude(mesh, -0.6f);
            }),
            ("loop cut", mesh =>
            {
                EditMeshBox(mesh, new Vector3(2, 2, 2));
                // One upright edge, whose ring of faces runs round the box's sides.
                EditMeshSelectInside(mesh, Vector3.Zero, new Vector3(0, 2, 0));
                EditMeshLoopCut(mesh, 0.5f);
                EditMeshScale(mesh, new Vector3(1.35f, 1, 1.35f));
            }),
        ];
        Color[] colors = [Color.Orange, Color.SkyBlue, Color.Lime];

        var models = new Model[boxes.Length];
        var faces = new int[boxes.Length];
        for (int i = 0; i < boxes.Length; i++)
        {
            var mesh = LoadEditMesh();
            boxes[i].Make(mesh);
            faces[i] = GetEditMeshInfo(mesh).FaceCount;
            models[i] = LoadModelFromEditMesh(mesh);
            models[i].Materials[0] = new ModelMaterial(colors[i]) { Roughness = 0.6f };
            UnloadEditMesh(mesh);
        }
        var ground = LoadModelFromMesh(GenMeshPlane(80, 60, 1, 1));

        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.6f), new Color(255, 244, 225), 1.4f, castsShadows: true);
        SetAmbientLight(new Color(150, 175, 215), 0.35f);
        var camera = new Camera3D(new Vector3(0, 5.5f, 9), new Vector3(0, 0.8f, 0), Vector3.UnitY, 45);

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

            // Each name under its box, with the faces its edit mesh held.
            for (int i = 0; i < models.Length; i++)
            {
                var below = GetWorldToScreen(Slot(i) + new Vector3(1, 0, 3), camera);
                var count = $"{faces[i]} faces";
                DrawText(boxes[i].Name, (int)below.X - MeasureText(boxes[i].Name, 18) / 2, (int)below.Y, 18, Color.Black);
                DrawText(count, (int)below.X - MeasureText(count, 16) / 2, (int)below.Y + 20, 16, Color.DarkGray);
            }
            DrawText("A box beveled, inset and loop cut", 10, 10, 20, Color.DarkGray);
            EndDrawing();
        }

        foreach (var model in models) UnloadModel(model);
        UnloadModel(ground);
        CloseWindow();
    }

    // The corner a box's slot starts at, three slots three meters and a half apart along X.
    private static Vector3 Slot(int i) => new((i - 1) * 3.5f - 1, 0, -1);
}
