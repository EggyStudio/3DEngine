using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersCornellBox
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] cornell box");

        // The Cornell box: a white room open at the front, its left wall red and its right green,
        // two white blocks inside, and a lamp under the ceiling's glowing panel. The light that
        // bounces tints the blocks' sides by the walls beside them and lights the ceiling the lamp
        // does not reach. G steps through the qualities, Off first.
        SetSceneField(3, 0.15f, 2);
        var quality = GlobalIllumination.High;
        SetGlobalIllumination(quality);
        CreatePointLight(new Vector3(0, 4.2f, 0), new Color(255, 236, 210), 9, range: 12);

        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // The panel gives off light of its own, which reaches the room only by bouncing.
        var panel = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        panel.Materials[0].Emissive = new Color(255, 240, 220);
        panel.Materials[0].EmissiveIntensity = 6;
        var camera = new Camera3D(new Vector3(0, 2.5f, 8), new Vector3(0, 2.4f, 0), Vector3.UnitY, 45);
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.G))
            {
                quality = (GlobalIllumination)(((int)quality + 1) % 4);
                SetGlobalIllumination(quality);
            }

            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            var white = new Color(220, 220, 215);
            // The room's slabs, each 0.3 thick, so the field holds three of its cells across each.
            DrawModelEx(slab, new Vector3(0, -0.15f, 0), Vector3.UnitY, 0, new Vector3(5.6f, 0.3f, 5.6f), white);
            DrawModelEx(slab, new Vector3(0, 5.15f, 0), Vector3.UnitY, 0, new Vector3(5.6f, 0.3f, 5.6f), white);
            DrawModelEx(slab, new Vector3(0, 2.5f, -2.65f), Vector3.UnitY, 0, new Vector3(5.6f, 5.6f, 0.3f), white);
            DrawModelEx(slab, new Vector3(-2.65f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5.6f, 5.6f), new Color(200, 30, 30));
            DrawModelEx(slab, new Vector3(2.65f, 2.5f, 0), Vector3.UnitY, 0, new Vector3(0.3f, 5.6f, 5.6f), new Color(30, 170, 40));
            // The blocks, the tall one turned toward the red wall and the short one toward the green.
            DrawModelEx(slab, new Vector3(-0.9f, 1.5f, -0.8f), Vector3.UnitY, 20, new Vector3(1.4f, 3, 1.4f), white);
            DrawModelEx(slab, new Vector3(1, 0.7f, 0.7f), Vector3.UnitY, -18, new Vector3(1.4f, 1.4f, 1.4f), white);
            // The ceiling's panel, which gives off light of its own.
            DrawModelEx(panel, new Vector3(0, 4.97f, 0), Vector3.UnitY, 0, new Vector3(1.6f, 0.06f, 1.6f), Color.White);
            EndMode3D();

            DrawText($"Light that bounces: {quality}. G changes it.", 10, 10, 20, Color.RayWhite);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(slab);
        UnloadModel(panel);
        CloseWindow();
    }
}
