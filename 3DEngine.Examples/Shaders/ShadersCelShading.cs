// raylib's shaders_cel_shading example, Copyright (c) 2026 Gleb A (@ggrizzly), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.RLights;

namespace Engine.Examples;

public static class ShadersCelShading
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shaders] cel shading");

        Camera3D camera = new(new Vector3(9.0f, 6.0f, 9.0f), new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f), 45.0f, CameraProjection.Perspective);

        // Load model
        Model model = LoadModel("resources/models/old_car_new.glb");

        // Load cel shader
        Shader celShader = LoadShader("resources/shaders/slang/cel.slang");
        int viewLoc = GetShaderLocation(celShader, "viewPos");

        // Apply cel shader to model, keep copy of default shader
        Shader defaultShader = model.Materials[0].Shader;
        model.Materials[0].Shader = celShader;

        // numBands: controls toon quantization steps (2 = hard binary, 20 = near-smooth)
        float numBands = 10.0f;
        int numBandsLoc = GetShaderLocation(celShader, "numBands");
        SetShaderValue(celShader, numBandsLoc, numBands);

        // Inverted-hull outline shader: draws back faces extruded along normals
        Shader outlineShader = LoadShader("resources/shaders/slang/outline_hull.slang");
        int outlineThicknessLoc = GetShaderLocation(outlineShader, "outlineThickness");

        // Single directional white light, angled so toon bands are visible on the model sides.
        // Spins opposite to CAMERA_ORBITAL (0.5 rad/s) so lighting changes as you watch.
        RLights.Light[] lights = new RLights.Light[MAX_LIGHTS];
        lights[0] = CreateLight(LIGHT_DIRECTIONAL, new Vector3(50.0f, 50.0f, 50.0f), Vector3.Zero, Color.White, celShader);

        bool celEnabled = true;
        bool outlineEnabled = true;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            SetShaderValue(celShader, viewLoc, camera.Position);

            // [Z] Toggle cel shading on/off
            if (IsKeyPressed(Key.Z))
            {
                celEnabled = !celEnabled;
                if (celEnabled) model.Materials[0].Shader = celShader; // Apply cel shader to model
                else model.Materials[0].Shader = defaultShader; // Apply default shader to model
            }

            // [C] Toggle outline on/off
            if (IsKeyPressed(Key.C)) outlineEnabled = !outlineEnabled;

            // [Q/E] Decrease/increase toon band count (press or hold to repeat)
            if (IsKeyPressed(Key.E) || IsKeyPressedRepeat(Key.E)) numBands = Math.Clamp(numBands + 1.0f, 2.0f, 20.0f);
            if (IsKeyPressed(Key.Q) || IsKeyPressedRepeat(Key.Q)) numBands = Math.Clamp(numBands - 1.0f, 2.0f, 20.0f);
            SetShaderValue(celShader, numBandsLoc, numBands);

            // Spin light opposite to CAMERA_ORBITAL (0.5 rad/s), angled 45 degrees off vertical
            float t = (float)GetTime();
            lights[0].position = new Vector3(MathF.Sin(-t*0.3f)*5.0f, 5.0f, MathF.Cos(-t*0.3f)*5.0f);

            for (int i = 0; i < MAX_LIGHTS; i++) UpdateLightValues(celShader, lights[i]);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    if (outlineEnabled)
                    {
                        // Outline pass: cull front faces, draw extruded back faces as silhouette
                        // raylib's normals of this file are as long as one over its scale of
                        // 0.0254, inches to meters, where a normal here is a unit long, so the hull
                        // is pushed by as much more for the outline raylib's picture has.
                        float thickness = 0.005f / 0.0254f;
                        SetShaderValue(outlineShader, outlineThicknessLoc, thickness);

                        rlSetCullFace(RlCullFace.Front);

                        model.Materials[0].Shader = outlineShader;

                        DrawModel(model, Vector3.Zero, 0.75f, Color.White);

                        if (celEnabled) model.Materials[0].Shader = celShader; // Apply cel shader to model
                        else model.Materials[0].Shader = defaultShader; // Apply default shader to model

                        rlSetCullFace(RlCullFace.Back);
                    }

                    DrawModel(model, Vector3.Zero, 0.75f, Color.White);
                    DrawSphereEx(lights[0].position, 0.2f, 50, 50, Color.Yellow);  // Light position indicator
                    DrawGrid(10, 10.0f);

                EndMode3D();

                DrawFPS(10, 10);
                DrawText($"Cel: {(celEnabled ? "ON" : "OFF")}  [Z]", 10, 65, 20, celEnabled ? Color.DarkGreen : Color.DarkGray);
                DrawText($"Outline: {(outlineEnabled ? "ON" : "OFF")}  [C]", 10, 90, 20, outlineEnabled ? Color.DarkGreen : Color.DarkGray);
                DrawText($"Bands: {numBands:0}  [Q/E]", 10, 115, 20, Color.DarkGray);

            EndDrawing();
        }

        UnloadModel(model);
        UnloadShader(celShader);
        UnloadShader(outlineShader);

        CloseWindow();
    }
}
