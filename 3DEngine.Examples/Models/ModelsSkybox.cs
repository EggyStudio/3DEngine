using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsSkybox
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] skybox");

        // An equirectangular sky made here, so the example needs no file: the top half a sky
        // with a sun, the bottom half the ground. A photo or a .hdr file loads the same way.
        var sky = GenImageColor(1024, 512, Color.Blank);
        ImageDraw(ref sky, GenImageGradientLinear(1024, 256, 0, new Color(40, 90, 170), new Color(190, 215, 235)),
            new Rectangle(0, 0, 1024, 256), new Rectangle(0, 0, 1024, 256), Color.White);
        ImageDraw(ref sky, GenImageGradientLinear(1024, 256, 0, new Color(95, 105, 80), new Color(45, 50, 40)),
            new Rectangle(0, 0, 1024, 256), new Rectangle(0, 256, 1024, 256), Color.White);
        ImageDrawCircle(ref sky, 300, 150, 14, new Color(255, 250, 225));
        SetEnvironmentMap(sky);

        // Spheres from mirror to chalk, which reflect the sky drawn behind them.
        var sphere = LoadModelFromMesh(GenMeshSphere(0.8f, 32, 32));
        var camera = new Camera3D(new Vector3(0, 1.5f, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 50);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            BeginDrawing();
            ClearBackground(Color.Black);

            BeginMode3D(camera);
            DrawSkybox();
            for (int i = 0; i < 4; i++)
            {
                sphere.Materials[0] = new ModelMaterial(new Color(230, 230, 235)) { Metallic = i < 2 ? 1 : 0, Roughness = 0.05f + i * 0.3f };
                DrawModel(sphere, new Vector3(-3 + i * 2, 0.5f, 0), 1, Color.White);
            }
            EndMode3D();

            DrawText("The environment map drawn as the sky, and reflected", 10, 10, 20, Color.White);
            DrawFPS(10, 420);

            EndDrawing();
        }

        UnloadModel(sphere);
        UnloadEnvironmentMap();
        CloseWindow();
    }
}
