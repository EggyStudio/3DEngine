// raylib's shaders_lights_bloom example, Copyright (c) 2025 PanicTitan (@PanicTitan), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersLightsBloom
{
    private const int MAX_LIGHTS = 8;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint | ConfigFlags.VsyncHint);
        InitWindow(screenWidth, screenHeight, "[shaders] lights bloom");

        Camera3D camera = new(new Vector3(0.0f, 5.0f, 9.0f), new Vector3(0.0f, 0.5f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        RenderTexture2D target = LoadRenderTexture(screenWidth, screenHeight);

        // raylib's forward multi-light shader and bloom post-process shader, written in Slang, the
        // first for the model pass
        Shader lightShader = LoadShader("resources/shaders/slang/lights_bloom.slang");
        Shader bloomShader = LoadShader("resources/shaders/slang/lights_bloom_post.slang");

        // Load models from generated cube mesh and plane
        Model cube = LoadModelFromMesh(GenMeshCube(2.0f, 2.0f, 2.0f));
        Model floor = LoadModelFromMesh(GenMeshPlane(14.0f, 14.0f, 1, 1));
        cube.Materials[0].Shader = lightShader;
        floor.Materials[0].Shader = lightShader;

        int lightPosLoc = GetShaderLocation(lightShader, "lightPositions");
        int lightColLoc = GetShaderLocation(lightShader, "lightColors");
        int viewPosLoc = GetShaderLocation(lightShader, "viewPos");

        Vector3[] lightPositions = new Vector3[MAX_LIGHTS];
        Vector3[] lightColors =
        [
            new(1.0f, 0.2f, 0.2f),   // Red
            new(0.2f, 1.0f, 0.3f),   // Green
            new(0.2f, 0.5f, 1.0f),   // Blue
            new(1.0f, 0.8f, 0.1f),   // Yellow
            new(1.0f, 0.1f, 0.8f),   // Magenta
            new(0.1f, 1.0f, 1.0f),   // Cyan
            new(1.0f, 0.4f, 0.1f),   // Orange
            new(0.7f, 0.2f, 1.0f),   // Purple
        ];

        SetShaderValueV<Vector3>(lightShader, lightColLoc, lightColors);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            float time = (float)GetTime();

            // Orbital path for each point light, its own radius/height offset by index
            for (int i = 0; i < MAX_LIGHTS; i++)
            {
                float angle = (i/(float)MAX_LIGHTS)*2.0f*MathF.PI + time*0.6f;
                float radius = 4.2f + MathF.Sin(time*1.2f + i)*0.4f;
                float height = 1.0f + MathF.Sin(time*1.8f + i)*0.6f;

                lightPositions[i] = new Vector3(MathF.Sin(angle)*radius, height, MathF.Cos(angle)*radius);
            }

            SetShaderValueV<Vector3>(lightShader, lightPosLoc, lightPositions);
            SetShaderValue(lightShader, viewPosLoc, camera.Position);

            // Render the lit scene to an offscreen texture
            BeginTextureMode(target);

                ClearBackground(new Color(12, 12, 18, 255));

                BeginMode3D(camera);

                    DrawModel(cube, new Vector3(0.0f, 1.0f, 0.0f), 1.0f, Color.Gray);
                    DrawModel(floor, new Vector3(0.0f, 0.0f, 0.0f), 1.0f, Color.DarkGray);
                    DrawGrid(10, 1.0f);

                    // Glowing bulbs mark each light's position
                    for (int i = 0; i < MAX_LIGHTS; i++)
                    {
                        Color bulbColor = new(
                            (byte)(lightColors[i].X*255),
                            (byte)(lightColors[i].Y*255),
                            (byte)(lightColors[i].Z*255),
                            255);

                        DrawSphere(lightPositions[i], 0.15f, bulbColor);
                    }

                EndMode3D();

            EndTextureMode();

            // Present the offscreen texture through the bloom + tone mapping shader
            BeginDrawing();

                ClearBackground(Color.Black);

                // Upright with a positive height, as a target here keeps its rows from the top,
                // where raylib's negative height turns OpenGL's from the bottom
                Rectangle srcRec = new(0, 0, (float)target.Texture.Width, (float)target.Texture.Height);

                BeginShaderMode(bloomShader);
                    DrawTextureRec(target.Texture, srcRec, new Vector2(0, 0), Color.White);
                EndShaderMode();

                DrawText("BALANCED MULTI-LIGHT + REINHARD TONE MAPPED BLOOM", 20, 20, 20, Color.Green);

            EndDrawing();
        }

        UnloadModel(cube);
        UnloadModel(floor);
        UnloadShader(lightShader);
        UnloadShader(bloomShader);
        UnloadRenderTexture(target);

        CloseWindow();
    }
}
