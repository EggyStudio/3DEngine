using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersReflections
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] reflections");

        // A hall with a polished floor, a chrome ball and a brushed gold one, three colored pillars
        // and a glowing strip on the back wall, under a low sun. Where light bounces, a glossy
        // surface traces its reflection through the window's depth and the scene's distance field,
        // so the floor reflects the pillars and the balls each other, and a rough one reflects the
        // sky as before. G steps through the qualities, Off first, and Up and Down make the floor
        // smoother or rougher.
        SetSceneField(3, 0.2f, 2);
        var quality = GlobalIllumination.High;
        SetGlobalIllumination(quality);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.5f, -0.6f, -0.4f)), new Color(255, 240, 220), 2.5f, castsShadows: true);
        var sky = GenImageColor(256, 128, Color.Blank);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(50, 95, 170), new Color(190, 210, 235)), 0, 0, Color.White);
        ImageDrawImage(ref sky, GenImageGradientLinear(256, 64, 0, new Color(90, 95, 85), new Color(40, 42, 38)), 0, 64, Color.White);
        SetEnvironmentMap(sky, intensity: 0.5f);

        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        floor.Materials[0] = new ModelMaterial(new Color(40, 40, 46)) { Roughness = 0.08f };
        var wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        wall.Materials[0] = new ModelMaterial(new Color(200, 196, 188));
        var pillar = LoadModelFromMesh(GenMeshCylinder(0.35f, 3, 24));
        var chrome = LoadModelFromMesh(GenMeshSphere(0.7f, 48, 48));
        chrome.Materials[0] = new ModelMaterial(new Color(235, 235, 235)) { Metallic = 1, Roughness = 0.05f };
        var gold = LoadModelFromMesh(GenMeshSphere(0.6f, 48, 48));
        gold.Materials[0] = new ModelMaterial(new Color(255, 200, 90)) { Metallic = 1, Roughness = 0.3f };
        var strip = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        strip.Materials[0] = new ModelMaterial(Color.Black) { Emissive = new Color(120, 200, 255), EmissiveIntensity = 4 };

        var camera = new Camera3D(new Vector3(0, 2.2f, 7.5f), new Vector3(0, 0.9f, 0), Vector3.UnitY, 50);
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.G))
            {
                quality = (GlobalIllumination)(((int)quality + 1) % 4);
                SetGlobalIllumination(quality);
            }
            if (IsKeyPressed(Key.Up)) floor.Materials[0].Roughness = Math.Max(0.02f, floor.Materials[0].Roughness - 0.1f);
            if (IsKeyPressed(Key.Down)) floor.Materials[0].Roughness = Math.Min(1, floor.Materials[0].Roughness + 0.1f);

            BeginDrawing();
            ClearBackground(new Color(50, 95, 170));
            BeginMode3D(camera);
            DrawSkybox();
            DrawModelEx(floor, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(12, 0.2f, 12), Color.White);
            DrawModelEx(wall, new Vector3(0, 2, -4), Vector3.UnitY, 0, new Vector3(12, 4, 0.3f), Color.White);
            DrawModelEx(strip, new Vector3(0, 2.6f, -3.8f), Vector3.UnitY, 0, new Vector3(5, 0.25f, 0.1f), Color.White);
            DrawModel(pillar, new Vector3(-3, 0, -2), 1, new Color(200, 40, 40));
            DrawModel(pillar, new Vector3(0, 0, -2.6f), 1, new Color(40, 170, 60));
            DrawModel(pillar, new Vector3(3, 0, -2), 1, new Color(50, 80, 210));
            DrawModel(chrome, new Vector3(-1.1f, 0.7f, 0.4f), 1, Color.White);
            DrawModel(gold, new Vector3(1.4f, 0.6f, 0.8f), 1, Color.White);
            EndMode3D();

            DrawText($"Light that bounces: {quality}. G changes it.", 10, 10, 20, Color.RayWhite);
            DrawText($"Floor roughness {floor.Materials[0].Roughness:0.00}. Up and Down change it.", 10, 36, 20, Color.RayWhite);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(floor);
        UnloadModel(wall);
        UnloadModel(pillar);
        UnloadModel(chrome);
        UnloadModel(gold);
        UnloadModel(strip);
        UnloadEnvironmentMap();
        CloseWindow();
    }
}
