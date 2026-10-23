using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// A room of an edit mesh whose slots are given looks, its walls of brick and its floor of planks,
/// both pictures drawn here with the image calls and written beside the program, each shown at 256 of
/// its pixels a meter by the faces' texture coordinates.
/// </summary>
public static class ModelsEditMeshLooks
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] edit mesh looks");

        // The pictures a slot's look reads by file, drawn once and written beside the program.
        SetRandomSeed(7);
        var folder = Path.Combine(AppContext.BaseDirectory, "resources", "generated");
        Directory.CreateDirectory(folder);
        var bricks = Bricks();
        ExportImage(bricks, Path.Combine(folder, "bricks.png"));
        UnloadImage(bricks);
        var planks = Planks();
        ExportImage(planks, Path.Combine(folder, "planks.png"));
        UnloadImage(planks);

        var room = LoadEditMesh();
        EditMeshRoom(room, new Vector3(6, 3, 5), 0.2f);
        EditMeshSelectAll(room);
        EditMeshMaterial(room, "wall");
        EditMeshSelectFacing(room, Vector3.UnitZ);
        EditMeshCutWindow(room, 1.6f, 1.1f, 3, 1);
        EditMeshSelectInside(room, new Vector3(0.1f, 0.15f, 0.1f), new Vector3(5.9f, 0.25f, 4.9f));
        EditMeshMaterial(room, "floor");
        EditMeshCut(room, "box 6.4 0.6 5.4 at -0.2 2.6 -0.2");

        EditMeshSlotMap(room, "wall", MaterialMapIndex.Albedo, "resources/generated/bricks.png");
        EditMeshSlotMap(room, "floor", MaterialMapIndex.Albedo, "resources/generated/planks.png");
        EditMeshSlotRoughness(room, "floor", 0.6f);
        // Each texture of 512 pixels shown at 256 a meter, so a brick is half a meter long.
        EditMeshSelectSlot(room, "wall");
        EditMeshUvDensity(room, 256, 512);
        EditMeshSelectSlot(room, "floor");
        EditMeshUvDensity(room, 256, 512);
        var model = LoadModelFromEditMesh(room);

        CreateDirectionalLight(new Vector3(-0.4f, -1, 0.35f), new Color(255, 244, 225), 1.4f, castsShadows: true);
        SetAmbientLight(new Color(150, 175, 215), 0.4f);
        var camera = new Camera3D(new Vector3(4, 8, -6), new Vector3(3, 0.4f, 2.8f), Vector3.UnitY, 45);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(new Color(200, 215, 230));
            BeginMode3D(camera);
            DrawModel(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
            DrawText("A room of brick walls on a floor of planks", 10, 10, 20, Color.DarkGray);
            EndDrawing();
        }

        UnloadModel(model);
        UnloadEditMesh(room);
        CloseWindow();
    }

    // Courses of bricks a sixteenth of the picture high, every other one set over by half a brick,
    // each brick a red of its own on a pale mortar.
    private static Image Bricks()
    {
        var image = GenImageColor(512, 512, new Color(196, 188, 176));
        for (int row = 0; row < 16; row++)
            for (int column = -1; column < 4; column++)
            {
                var x = column * 128 + (row % 2) * 64;
                var shade = GetRandomValue(-22, 22);
                var brick = new Color((byte)(150 + shade), (byte)(64 + shade / 2), (byte)(48 + shade / 2));
                ImageDrawRectangle(ref image, x + 3, row * 32 + 3, 122, 26, brick);
            }
        return image;
    }

    // Planks an eighth of the picture wide, each a brown of its own with darker grain along it and a
    // dark seam between it and the next.
    private static Image Planks()
    {
        var image = GenImageColor(512, 512, new Color(70, 45, 25));
        for (int plank = 0; plank < 8; plank++)
        {
            var shade = GetRandomValue(-18, 18);
            var wood = new Color((byte)(168 + shade), (byte)(118 + shade), (byte)(70 + shade / 2));
            ImageDrawRectangle(ref image, plank * 64 + 2, 0, 60, 512, wood);
            var grain = new Color((byte)(140 + shade), (byte)(96 + shade), (byte)(56 + shade / 2));
            for (int line = 0; line < 9; line++)
            {
                var x = plank * 64 + GetRandomValue(6, 58);
                ImageDrawLine(ref image, x, 0, x + GetRandomValue(-2, 2), 511, grain);
            }
        }
        return image;
    }
}
