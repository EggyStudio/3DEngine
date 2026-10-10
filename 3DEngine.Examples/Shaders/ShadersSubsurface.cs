using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersSubsurface
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] subsurface");

        // Light under the surface: in front, spheres of wax, skin and marble lit by a low sun from
        // the right, whose line between lit and shadowed softens and takes the color that travels
        // farthest, beside a plain sphere; behind, a leaf and a slab of wax lit only by a lamp
        // behind them, the light coming through the thin leaf and barely through the thick slab.
        // The lamp's way through each is measured in the scene's distance field. S turns the
        // scattering off and on, and Q steps through its qualities.
        SetSceneField(2, 0.15f, 2);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-1, -0.5f, -0.35f)), new Color(255, 244, 228), 1.6f, castsShadows: true);
        CreatePointLight(new Vector3(0, 1.4f, -3.4f), new Color(255, 210, 150), 6, range: 8);
        SetAmbientLight(new Color(150, 170, 200), 0.12f);

        var sphere = LoadModelFromMesh(GenMeshSphere(0.6f, 48, 48));
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        // Each material's radius in world units and how far each of red, green and blue goes of it.
        var wax = new ModelMaterial(new Color(240, 225, 200)) { SubsurfaceRadius = 0.3f, SubsurfaceColor = new Color(255, 180, 110) };
        var skin = new ModelMaterial(new Color(225, 170, 150)) { SubsurfaceRadius = 0.06f, SubsurfaceColor = new Color(255, 90, 60) };
        var marble = new ModelMaterial(new Color(235, 235, 230)) { SubsurfaceRadius = 0.15f, SubsurfaceColor = new Color(255, 245, 235) };
        var plain = new ModelMaterial(new Color(235, 235, 230));
        var leaf = new ModelMaterial(new Color(110, 170, 60)) { SubsurfaceRadius = 0.25f, SubsurfaceColor = new Color(160, 255, 90), DoubleSided = true };
        var floor = new ModelMaterial(new Color(90, 90, 95));
        var camera = new Camera3D(new Vector3(0, 1.6f, 5.2f), new Vector3(0, 0.7f, -1), Vector3.UnitY, 45);
        var scattering = true;
        var quality = SubsurfaceQuality.High;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.S)) scattering = !scattering;
            if (IsKeyPressed(Key.Q))
            {
                quality = (SubsurfaceQuality)(((int)quality + 1) % 3);
                SetSubsurfaceQuality(quality);
            }
            ModelMaterial Scattering(ModelMaterial material) => scattering ? material : material with { SubsurfaceRadius = 0 };

            BeginDrawing();
            ClearBackground(new Color(20, 22, 28));
            BeginMode3D(camera);
            DrawMesh(slab.Meshes[0], floor, Matrix4x4.CreateScale(10, 0.2f, 8) * Matrix4x4.CreateTranslation(0, -0.1f, -1));
            DrawMesh(sphere.Meshes[0], Scattering(wax), Matrix4x4.CreateTranslation(-2.1f, 0.6f, 0.6f));
            DrawMesh(sphere.Meshes[0], Scattering(skin), Matrix4x4.CreateTranslation(-0.7f, 0.6f, 0.6f));
            DrawMesh(sphere.Meshes[0], Scattering(marble), Matrix4x4.CreateTranslation(0.7f, 0.6f, 0.6f));
            DrawMesh(sphere.Meshes[0], plain, Matrix4x4.CreateTranslation(2.1f, 0.6f, 0.6f));
            // A leaf two centimeters thick and a slab of wax half a unit, the lamp behind both.
            DrawMesh(slab.Meshes[0], Scattering(leaf), Matrix4x4.CreateScale(1.2f, 1.6f, 0.02f) * Matrix4x4.CreateTranslation(-0.8f, 1.0f, -2.2f));
            DrawMesh(slab.Meshes[0], Scattering(wax), Matrix4x4.CreateScale(1.2f, 1.6f, 0.5f) * Matrix4x4.CreateTranslation(0.8f, 1.0f, -2.2f));
            EndMode3D();

            DrawText($"Light under the surface: {(scattering ? quality.ToString() : "off")}. S turns it {(scattering ? "off" : "on")}, Q changes it.", 10, 10, 20, Color.RayWhite);
            DrawText("Wax, skin, marble and a plain sphere; a leaf and a slab of wax lit from behind.", 10, 36, 10, Color.LightGray);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(sphere);
        UnloadModel(slab);
        CloseWindow();
    }
}
