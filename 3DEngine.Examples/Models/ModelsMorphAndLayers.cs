using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsMorphAndLayers
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] morph targets and layered clips");

        // Lit from above over a little light from all around, where models are drawn unlit with none.
        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 0.65f);
        SetAmbientLight(Color.White, 0.35f);

        // Summit's hero, its run playing on the whole body and its jump's raised arm on the left
        // arm alone, and a strip whose morph target lifts its top edge as its clip plays.
        var hero = LoadModel("resources/hero.gltf");
        var clips = LoadModelAnimations("resources/hero.gltf");
        var (run, jump) = (clips.First(c => c.Name == "run"), clips.First(c => c.Name == "jump"));
        var strip = LoadModel("resources/morph.gltf");
        var pulse = LoadModelAnimations("resources/morph.gltf")[0];
        var camera = new Camera3D(new Vector3(0, 1.6f, 6), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        var (time, layered) = (0f, true);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            time += GetFrameTime();
            if (IsKeyPressed(Key.Space)) layered = !layered;

            // The arm rises and falls over the run as the layer's weight eases in and out.
            var wave = layered ? 0.5f + 0.5f * MathF.Sin(time * 2) : 0;
            UpdateModelAnimationLayer(hero, run, time, jump, 0, "ArmL", wave);
            UpdateModelAnimationAt(strip, pulse, time);

            BeginDrawing();
            ClearBackground(new Color(30, 34, 46));

            BeginMode3D(camera);
            DrawModel(hero, new Vector3(1.2f, 0, 0), 1, Color.White);
            DrawModel(strip, new Vector3(-1.6f, 0, 0), 1, new Color(240, 200, 80));
            DrawGrid(10, 1);
            EndMode3D();

            DrawText(layered ? "The left arm waves over the run. Space runs alone." : "The run alone. Space waves the left arm.", 10, 10, 20, Color.RayWhite);
            DrawText("The strip's morph target rises and falls with its clip", 10, 40, 20, Color.LightGray);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(hero);
        UnloadModel(strip);
        UnloadModelAnimations(clips);
        CloseWindow();
    }
}
