// raylib's shaders_basic_lighting example, Copyright (c) 2019-2025 Chris Camacho (@chriscamacho) and
// Ramon Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.RLights;

namespace Engine.Examples;

public static class ShadersBasicLighting
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);
        InitWindow(screenWidth, screenHeight, "[shaders] basic lighting");

        Camera3D camera = new(new Vector3(2.0f, 4.0f, 6.0f), new Vector3(0.0f, 0.5f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // raylib's lighting.vs and lighting.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/lighting.slang");
        int viewLoc = GetShaderLocation(shader, "viewPos");

        // Ambient light level (some basic lighting)
        int ambientLoc = GetShaderLocation(shader, "ambient");
        SetShaderValue(shader, ambientLoc, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));

        RLights.Light[] lights = new RLights.Light[MAX_LIGHTS];
        lights[0] = CreateLight(LIGHT_POINT, new Vector3(-2, 1, -2), Vector3.Zero, Color.Yellow, shader);
        lights[1] = CreateLight(LIGHT_POINT, new Vector3(2, 1, 2), Vector3.Zero, Color.Red, shader);
        lights[2] = CreateLight(LIGHT_POINT, new Vector3(-2, 1, 2), Vector3.Zero, Color.Green, shader);
        lights[3] = CreateLight(LIGHT_POINT, new Vector3(2, 1, -2), Vector3.Zero, Color.Blue, shader);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // The camera's position, which the highlights are seen from
            SetShaderValue(shader, viewLoc, camera.Position);

            if (IsKeyPressed(Key.Y)) { lights[0].enabled = !lights[0].enabled; }
            if (IsKeyPressed(Key.R)) { lights[1].enabled = !lights[1].enabled; }
            if (IsKeyPressed(Key.G)) { lights[2].enabled = !lights[2].enabled; }
            if (IsKeyPressed(Key.B)) { lights[3].enabled = !lights[3].enabled; }

            // Update light values (only whether each is on changes)
            for (int i = 0; i < MAX_LIGHTS; i++) UpdateLightValues(shader, lights[i]);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    BeginShaderMode(shader);

                        DrawPlane(Vector3.Zero, new Vector2(10.0f, 10.0f), Color.White);
                        DrawCube(Vector3.Zero, 2.0f, 4.0f, 2.0f, Color.White);

                    EndShaderMode();

                    // Spheres where the lights are
                    for (int i = 0; i < MAX_LIGHTS; i++)
                    {
                        if (lights[i].enabled) DrawSphereEx(lights[i].position, 0.2f, 8, 8, lights[i].color);
                        else DrawSphereWires(lights[i].position, 0.2f, 8, 8, ColorAlpha(lights[i].color, 0.3f));
                    }

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawFPS(10, 10);

                DrawText("Use keys [Y][R][G][B] to toggle lights", 10, 40, 20, Color.DarkGray);

            EndDrawing();
        }

        UnloadShader(shader);

        CloseWindow();
    }
}
