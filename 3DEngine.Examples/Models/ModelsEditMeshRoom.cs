using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// A room blocked out in an edit mesh, its walls given a window and a door and a stair joined to its
/// floor, its roof cut away to look in, and each part put in the slot of a prototype material, the
/// floor, the walls, the stairs, the window and the door each a grid of a meter in its own color.
/// </summary>
public static class ModelsEditMeshRoom
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] edit mesh room");

        var house = LoadEditMesh();
        EditMeshRoom(house, new Vector3(6, 3, 5), 0.2f);
        EditMeshSelectAll(house);
        EditMeshMaterial(house, "wall");
        // The front wall's outside, then its window and its door, each opening's sides in its slot.
        EditMeshSelectFacing(house, Vector3.UnitZ);
        EditMeshCutWindow(house, 1.2f, 1, 4.5f, 1);
        EditMeshMaterial(house, "window");
        EditMeshSelectFacing(house, Vector3.UnitZ);
        EditMeshCutDoor(house, 1, 2.1f, 1.5f);
        EditMeshMaterial(house, "door");
        // A stair up the left wall, joined to the floor and the wall as one solid.
        EditMeshUnion(house, "stairs 6 0.2 0.3 1 at 0.2 0.2 2");
        EditMeshMaterial(house, "stairs");
        // The floor's top, inside a thin slab across the room a little above it.
        EditMeshSelectInside(house, new Vector3(0.1f, 0.15f, 0.1f), new Vector3(5.9f, 0.25f, 4.9f));
        EditMeshMaterial(house, "floor");
        // The roof cut away, so the camera sees in.
        EditMeshCut(house, "box 6.4 0.6 5.4 at -0.2 2.6 -0.2");
        var model = LoadModelFromEditMesh(house);
        var info = GetEditMeshInfo(house);

        CreateDirectionalLight(new Vector3(-0.4f, -1, 0.35f), new Color(255, 244, 225), 1.4f, castsShadows: true);
        SetAmbientLight(new Color(150, 175, 215), 0.4f);
        // From behind the back wall, looking in at the front wall's openings from inside.
        var camera = new Camera3D(new Vector3(4, 8, -6), new Vector3(3, 0.4f, 2.8f), Vector3.UnitY, 45);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(new Color(200, 215, 230));
            BeginMode3D(camera);
            DrawModel(model, Vector3.Zero, 1, Color.White);
            EndMode3D();
            DrawText("A room blocked out in prototype materials", 10, 10, 20, Color.DarkGray);
            DrawText($"{info.FaceCount} faces, {GetEditMeshSlots(house).Length} slots", 10, 34, 18, Color.Gray);
            EndDrawing();
        }

        UnloadModel(model);
        UnloadEditMesh(house);
        CloseWindow();
    }
}
