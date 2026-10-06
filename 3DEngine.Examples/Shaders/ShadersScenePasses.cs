using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersScenePasses
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] scene passes");

        var wave = LoadShader("resources/shaders/wave.slang");
        var grayscale = LoadShader("resources/shaders/grayscale.slang");
        // All the way to gray, which holds for every frame after.
        SetShaderValue(grayscale, 0, 1f);
        var scene = LoadRenderTexture(380, 300);

        var camera = new Camera3D(new Vector3(6, 4, 6), Vector3.Zero, Vector3.UnitY, 45);
        var torus = LoadModel("resources/torus.obj");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);
            var time = (float)GetTime();

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            // The scene, drawn once into an image.
            BeginTextureMode(scene);
            ClearBackground(Color.SkyBlue);
            BeginMode3D(camera);
            DrawModel(torus, Vector3.Zero, 1, Color.White);
            DrawGrid(10, 1);
            EndMode3D();
            EndTextureMode();

            // The same image through two shaders.
            SetShaderValue(wave, 0, time);
            BeginShaderMode(wave);
            DrawTexture(scene.Texture, 10, 60, Color.White);
            EndShaderMode();

            BeginShaderMode(grayscale);
            DrawTexture(scene.Texture, 410, 60, Color.White);
            EndShaderMode();

            DrawText("wave.slang", 10, 370, 20, Color.DarkGray);
            DrawText("grayscale.slang", 410, 370, 20, Color.DarkGray);
            DrawText("One scene in a render texture, drawn through two Slang shaders", 10, 20, 20, Color.DarkGray);
            EndDrawing();
        }

        UnloadShader(wave);
        UnloadShader(grayscale);
        UnloadRenderTexture(scene);
        UnloadModel(torus);
        CloseWindow();
    }
}
