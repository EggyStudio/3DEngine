using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersParticles
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] particles");

        // A campfire at night: flames that glow through bloom, smoke that rises and spreads, lit by
        // the fire's lamp and the moon, and sparks that leap from it on Space. Each is an emitter
        // whose particles a compute shader steps.
        SetBloom(0.7f);
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.6f)), new Color(120, 140, 190), 0.35f);
        var lamp = CreatePointLight(new Vector3(0, 1, 0), new Color(255, 150, 70), 5, range: 12);

        var fire = CreateParticleEmitter(new Vector3(0, 0.15f, 0), ParticleEmitter.Default with
        {
            MaxParticles = 600,
            Rate = 220,
            Life = 0.9f,
            LifeVariation = 0.4f,
            Velocity = new Vector3(0, 1.6f, 0),
            Spread = 18,
            Radius = 0.35f,
            Gravity = new Vector3(0, 0.8f, 0),
            StartSize = 0.45f,
            EndSize = 0.1f,
            StartColor = new Color(255, 190, 80),
            EndColor = new Color(200, 40, 10, 0),
            Intensity = 3,
        });
        CreateParticleEmitter(new Vector3(0, 1.2f, 0), ParticleEmitter.Default with
        {
            MaxParticles = 400,
            Rate = 40,
            Life = 4,
            LifeVariation = 0.3f,
            Velocity = new Vector3(0.3f, 1.1f, 0),
            Spread = 15,
            Radius = 0.2f,
            Gravity = new Vector3(0.15f, 0.1f, 0),
            StartSize = 0.5f,
            EndSize = 2.2f,
            StartColor = new Color(150, 150, 155, 150),
            EndColor = new Color(120, 120, 130, 0),
            Lit = true,
            Blend = ParticleBlend.Alpha,
        });
        var sparks = CreateParticleEmitter(new Vector3(0, 0.4f, 0), ParticleEmitter.Default with
        {
            MaxParticles = 300,
            Emitting = false,
            Life = 1.4f,
            LifeVariation = 0.5f,
            Velocity = new Vector3(0, 5, 0),
            Spread = 40,
            SpeedVariation = 0.5f,
            Gravity = new Vector3(0, -6, 0),
            StartSize = 0.07f,
            EndSize = 0.02f,
            StartColor = new Color(255, 220, 140),
            EndColor = new Color(255, 90, 20, 0),
            Intensity = 6,
        });

        var ground = LoadModelFromMesh(GenMeshPlane(30, 30, 1, 1));
        var log = LoadModelFromMesh(GenMeshCylinder(0.12f, 1.4f, 10));
        var stone = LoadModelFromMesh(GenMeshSphere(0.22f, 8, 10));
        var camera = new Camera3D(new Vector3(0, 2.4f, 6.5f), new Vector3(0, 1.3f, 0), Vector3.UnitY, 45);
        var t = 0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            t += GetFrameTime();
            if (IsKeyPressed(Key.Space)) EmitParticles(sparks, 120);
            // The flames flicker, and the lamp with them.
            var flicker = 0.85f + 0.15f * MathF.Sin(t * 13) * MathF.Sin(t * 7.3f);
            SetLightColor(lamp, new Color(255, 150, 70), 5 * flicker);
            SetParticleEmitter(fire, GetParticleEmitter(fire) with { Rate = 220 * flicker });

            BeginDrawing();
            ClearBackground(new Color(8, 10, 18));

            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, new Color(60, 70, 55));
            for (int i = 0; i < 3; i++)
                DrawModelEx(log, new Vector3(0, 0.12f, 0), Vector3.Normalize(new Vector3(MathF.Cos(i * 2.1f), 0, MathF.Sin(i * 2.1f))), 80,
                    Vector3.One, new Color(90, 60, 40));
            for (int i = 0; i < 9; i++)
                DrawModel(stone, new Vector3(MathF.Cos(i * MathF.Tau / 9), 0.1f, MathF.Sin(i * MathF.Tau / 9)) * 0.9f, 1, new Color(120, 118, 115));
            EndMode3D();

            DrawText("Fire, smoke and sparks from three emitters. Space throws sparks.", 10, 10, 20, Color.RayWhite);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(ground);
        UnloadModel(log);
        UnloadModel(stone);
        CloseWindow();
    }
}
