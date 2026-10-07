using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersSceneField
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] scene field");

        // A courtyard under a low sun, its walls, pillars and crates, and a crate pushed to and fro.
        // The scene's distance field darkens the light from all around where things close it off,
        // gives the sun soft contact shadows, and lets the fountain's drops bounce off the crates
        // and the walls, behind them too, where the camera does not see. F turns the field off.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.55f, -0.6f, -0.35f)), new Color(255, 238, 215), 2.2f, castsShadows: true);
        SetAmbientLight(new Color(150, 170, 205), 0.6f);
        SetAmbientOcclusion(1, 1.5f);
        SetSceneField(4);
        var field = true;

        var ground = LoadModelFromMesh(GenMeshPlane(40, 40, 1, 1));
        var wall = LoadModelFromMesh(GenMeshCube(10, 3, 0.5f));
        var pillar = LoadModelFromMesh(GenMeshCylinder(0.35f, 3, 16));
        var crate = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var basin = LoadModelFromMesh(GenMeshCube(2.4f, 0.5f, 2.4f));
        CreateParticleEmitter(new Vector3(0, 0.9f, 0), ParticleEmitter.Default with
        {
            MaxParticles = 1500,
            Rate = 400,
            Life = 3,
            Velocity = new Vector3(0, 5.5f, 0),
            Spread = 28,
            SpeedVariation = 0.25f,
            Gravity = new Vector3(0, -9.8f, 0),
            StartSize = 0.06f,
            EndSize = 0.04f,
            StartColor = new Color(190, 225, 255),
            EndColor = new Color(140, 190, 255, 0),
            Intensity = 1.5f,
            Collision = ParticleCollision.Bounce,
            Bounce = 0.35f,
        });

        var t = 0f;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            t += GetFrameTime();
            if (IsKeyPressed(Key.F)) SetSceneField((field = !field) ? 4 : 0);
            var camera = new Camera3D(new Vector3(MathF.Cos(t * 0.15f) * 11, 6, MathF.Sin(t * 0.15f) * 11), new Vector3(0, 1, 0), Vector3.UnitY, 45);

            BeginDrawing();
            ClearBackground(new Color(155, 185, 220));

            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(196, 190, 178));
            DrawModel(wall, new Vector3(0, 1.5f, -5), 1, new Color(214, 200, 180));
            DrawModelEx(wall, new Vector3(-5, 1.5f, 0), Vector3.UnitY, 90, Vector3.One, new Color(214, 200, 180));
            for (int i = 0; i < 4; i++)
                DrawModel(pillar, new Vector3(3.5f, 0, -3.5f + i * 2.3f), 1, new Color(230, 225, 215));
            DrawModel(basin, new Vector3(0, 0.25f, 0), 1, new Color(170, 175, 185));
            DrawModel(crate, new Vector3(-2.5f, 0.5f, -3.8f), 1, new Color(160, 120, 80));
            DrawModel(crate, new Vector3(-3.6f, 0.5f, -3.6f), 1, new Color(150, 110, 75));
            DrawModel(crate, new Vector3(-3.1f, 1.5f, -3.7f), 1, new Color(170, 128, 86));
            // The pushed crate moves each frame, so the field stamps it as its box.
            DrawModel(crate, new Vector3(1.6f + MathF.Sin(t * 0.8f) * 1.2f, 0.5f, 2.2f), 1, new Color(120, 90, 60));
            EndMode3D();

            DrawText(field ? "The scene's distance field is on. F turns it off." : "The scene's distance field is off. F turns it on.",
                10, 10, 20, Color.DarkGray);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(ground);
        UnloadModel(wall);
        UnloadModel(pillar);
        UnloadModel(crate);
        UnloadModel(basin);
        CloseWindow();
    }
}
