using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// One box of an edit mesh built three ways, as it was shaped, with a bevel modifier, and with the
/// bevel and a subdivision modifier that round it into a pebble, the mesh itself left as shaped,
/// each named with the faces it builds into.
/// </summary>
public static class ModelsEditMeshModifiers
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] edit mesh modifiers");

        (string Name, Action<EditMesh> Modify)[] stacks =
        [
            ("as shaped", _ => { }),
            ("bevel", mesh => EditMeshModifierBevel(mesh, 0.1f)),
            ("bevel and subdivision", mesh =>
            {
                EditMeshModifierBevel(mesh, 0.1f);
                EditMeshModifierSubdivision(mesh, 2);
            }),
        ];
        Color[] colors = [Color.Gray, Color.Gold, Color.Violet];

        var models = new Model[stacks.Length];
        var faces = new int[stacks.Length];
        for (int i = 0; i < stacks.Length; i++)
        {
            var mesh = LoadEditMesh();
            EditMeshBox(mesh, new Vector3(2, 1, 1.5f));
            stacks[i].Modify(mesh);
            faces[i] = GetEditMeshModifiedInfo(mesh).FaceCount;
            models[i] = LoadModelFromEditMesh(mesh);
            models[i].Materials[0] = new ModelMaterial(colors[i]) { Roughness = 0.5f };
            UnloadEditMesh(mesh);
        }
        var ground = LoadModelFromMesh(GenMeshPlane(80, 60, 1, 1));

        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.6f), new Color(255, 244, 225), 1.4f, castsShadows: true);
        SetAmbientLight(new Color(150, 175, 215), 0.35f);
        var camera = new Camera3D(new Vector3(0, 4.5f, 7.5f), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);

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

            // Each name under its shape, with the faces it builds into.
            for (int i = 0; i < models.Length; i++)
            {
                var below = GetWorldToScreen(Slot(i) + new Vector3(1, 0, 2.4f), camera);
                var count = $"{faces[i]} faces";
                DrawText(stacks[i].Name, (int)below.X - MeasureText(stacks[i].Name, 18) / 2, (int)below.Y, 18, Color.Black);
                DrawText(count, (int)below.X - MeasureText(count, 16) / 2, (int)below.Y + 20, 16, Color.DarkGray);
            }
            DrawText("A box built through its modifiers", 10, 10, 20, Color.DarkGray);
            EndDrawing();
        }

        foreach (var model in models) UnloadModel(model);
        UnloadModel(ground);
        CloseWindow();
    }

    // The corner a shape's slot starts at, three slots three meters apart along X.
    private static Vector3 Slot(int i) => new((i - 1) * 3 - 1, 0, -0.75f);
}
