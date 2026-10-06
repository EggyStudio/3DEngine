// raylib's shaders_deferred_rendering example, Copyright (c) 2023-2025 Justin Andreas Lacoste
// (@27justin), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.RLights;

namespace Engine.Examples;

public static class ShadersDeferredRendering
{
    private const int MAX_CUBES = 30;

    // Deferred mode passes
    private enum DeferredMode
    {
       DEFERRED_POSITION,
       DEFERRED_NORMAL,
       DEFERRED_ALBEDO,
       DEFERRED_SHADING
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] deferred rendering");

        Camera3D camera = default;
        camera.Position = new Vector3(5.0f, 4.0f, 5.0f);    // Camera position
        camera.Target = new Vector3(0.0f, 1.0f, 0.0f);      // Camera looking at point
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);          // Camera up vector (rotation towards target)
        camera.FovY = 60.0f;                                // Camera field-of-view Y
        camera.Projection = CameraProjection.Perspective;   // Camera projection type

        // Load plane model from a generated mesh
        Model model = LoadModelFromMesh(GenMeshPlane(10.0f, 10.0f, 3, 3));
        Model cube = LoadModelFromMesh(GenMeshCube(2.0f, 2.0f, 2.0f));

        // Load geometry buffer (G-buffer) shader and deferred shader, raylib's GLSL written in Slang
        Shader gbufferShader = LoadShader("resources/shaders/slang/gbuffer.slang");

        Shader deferredShader = LoadShader("resources/shaders/slang/deferred_shading.slang");
        int viewLoc = GetShaderLocation(deferredShader, "viewPosition");

        // Initialize the G-buffer. raylib builds it from rlgl's framebuffer, a texture of each
        // format attached and a depth renderbuffer, where a render texture here draws into a
        // texture of each format with a depth of its own.

        // 16-bit precision ensures OpenGL ES 3 compatibility, though it may lack precision for real scenarios
        // Vertex positions are stored in the first texture for simplicity, normals in the second,
        // and the color in RGB with the specular strength in the alpha channel in the third
        RenderTexture2D gBuffer = LoadRenderTexture(screenWidth, screenHeight,
            PixelFormat.UncompressedR16G16B16, PixelFormat.UncompressedR16G16B16, PixelFormat.UncompressedR8G8B8A8);

        // Now we initialize the sampler2D uniform's in the deferred shader. The G-buffer's
        // textures are given to them by name, where raylib binds them to texture units
        SetShaderValueTexture(deferredShader, GetShaderLocation(deferredShader, "gPosition"), gBuffer.Textures[0]);
        SetShaderValueTexture(deferredShader, GetShaderLocation(deferredShader, "gNormal"), gBuffer.Textures[1]);
        SetShaderValueTexture(deferredShader, GetShaderLocation(deferredShader, "gAlbedoSpec"), gBuffer.Textures[2]);
        SetShaderValueTexture(deferredShader, GetShaderLocation(deferredShader, "gDepth"), gBuffer.Depth);

        // Assign out lighting shader to model
        model.Materials[0].Shader = gbufferShader;
        cube.Materials[0].Shader = gbufferShader;

        // Create lights
        RLights.Light[] lights = new RLights.Light[MAX_LIGHTS];
        lights[0] = CreateLight(LIGHT_POINT, new Vector3(-2, 1, -2), Vector3.Zero, Color.Yellow, deferredShader);
        lights[1] = CreateLight(LIGHT_POINT, new Vector3(2, 1, 2), Vector3.Zero, Color.Red, deferredShader);
        lights[2] = CreateLight(LIGHT_POINT, new Vector3(-2, 1, 2), Vector3.Zero, Color.Green, deferredShader);
        lights[3] = CreateLight(LIGHT_POINT, new Vector3(2, 1, -2), Vector3.Zero, Color.Blue, deferredShader);

        const float CUBE_SCALE = 0.25f;
        Vector3[] cubePositions = new Vector3[MAX_CUBES];
        float[] cubeRotations = new float[MAX_CUBES];

        for (int i = 0; i < MAX_CUBES; i++)
        {
            cubePositions[i] = new Vector3(
                (float)(rand()%10) - 5,
                (float)(rand()%5),
                (float)(rand()%10) - 5);

            cubeRotations[i] = (float)(rand()%360);
        }

        DeferredMode mode = DeferredMode.DEFERRED_SHADING;

        SetTargetFPS(60);                   // Set our game to run at 60 frames-per-second

        while (!WindowShouldClose())
        {
            // Update
            UpdateCamera(ref camera, CameraMode.Orbital);

            // Update the shader with the camera view vector (points towards { 0.0f, 0.0f, 0.0f })
            SetShaderValue(deferredShader, viewLoc, camera.Position);

            // Check key inputs to enable/disable lights
            if (IsKeyPressed(Key.Y)) { lights[0].enabled = !lights[0].enabled; }
            if (IsKeyPressed(Key.R)) { lights[1].enabled = !lights[1].enabled; }
            if (IsKeyPressed(Key.G)) { lights[2].enabled = !lights[2].enabled; }
            if (IsKeyPressed(Key.B)) { lights[3].enabled = !lights[3].enabled; }

            // Check key inputs to switch between G-buffer textures
            if (IsKeyPressed(Key.One)) mode = DeferredMode.DEFERRED_POSITION;
            if (IsKeyPressed(Key.Two)) mode = DeferredMode.DEFERRED_NORMAL;
            if (IsKeyPressed(Key.Three)) mode = DeferredMode.DEFERRED_ALBEDO;
            if (IsKeyPressed(Key.Four)) mode = DeferredMode.DEFERRED_SHADING;

            // Update light values (actually, only enable/disable them)
            for (int i = 0; i < MAX_LIGHTS; i++) UpdateLightValues(deferredShader, lights[i]);

            // Draw
            BeginDrawing();

                // Draw to the geometry buffer by first activating it
                BeginTextureMode(gBuffer);
                ClearBackground(Color.Blank);   // Clear color and depth buffer
                rlDisableColorBlend();

                BeginMode3D(camera);
                    // When drawing a model here, make sure that the material's shaders are set to the gbuffer shader
                    DrawModel(model, Vector3.Zero, 1.0f, Color.White);
                    DrawModel(cube, new Vector3(0.0f, 1.0f, 0.0f), 1.0f, Color.White);

                    for (int i = 0; i < MAX_CUBES; i++)
                    {
                        Vector3 position = cubePositions[i];
                        DrawModelEx(cube, position, new Vector3(1, 1, 1), cubeRotations[i], new Vector3(CUBE_SCALE, CUBE_SCALE, CUBE_SCALE), Color.White);
                    }
                EndMode3D();

                rlEnableColorBlend();

                // Go back to the default framebuffer and draw our deferred shading
                EndTextureMode();
                ClearBackground(Color.Blank); // Clear color & depth buffer, to the color the G-buffer's clear left set

                switch (mode)
                {
                    case DeferredMode.DEFERRED_SHADING:
                    {
                        // A quad over the screen, shaded by the deferred shader from the G-buffer,
                        // as rlgl's rlLoadDrawQuad draws one. The shader writes the G-buffer's depth
                        // as it goes, where raylib copies it into the window's after, so the depth
                        // test is on for it and the lights below are hidden behind the scene
                        rlEnableDepthTest();
                        rlDisableColorBlend();
                        BeginShaderMode(deferredShader);
                            DrawTextureRec(gBuffer.Texture, new Rectangle(0, 0, screenWidth, screenHeight), Vector2.Zero, Color.White);
                        EndShaderMode();
                        rlEnableColorBlend();
                        rlDisableDepthTest();

                        // Since our shader is now done and disabled, we can draw spheres
                        // that represent light positions in default forward rendering
                        BeginMode3D(camera);
                            for (int i = 0; i < MAX_LIGHTS; i++)
                            {
                                if (lights[i].enabled) DrawSphereEx(lights[i].position, 0.2f, 8, 8, lights[i].color);
                                else DrawSphereWires(lights[i].position, 0.2f, 8, 8, ColorAlpha(lights[i].color, 0.3f));
                            }
                        EndMode3D();

                        DrawText("FINAL RESULT", 10, screenHeight - 30, 20, Color.DarkGreen);
                    } break;
                    // A render texture is upright here, so each is drawn with its height as it is,
                    // where raylib's is turned
                    case DeferredMode.DEFERRED_POSITION:
                    {
                        DrawTextureRec(gBuffer.Textures[0], new Rectangle(0, 0, screenWidth, screenHeight), Vector2.Zero, Color.RayWhite);

                        DrawText("POSITION TEXTURE", 10, screenHeight - 30, 20, Color.DarkGreen);
                    } break;
                    case DeferredMode.DEFERRED_NORMAL:
                    {
                        DrawTextureRec(gBuffer.Textures[1], new Rectangle(0, 0, screenWidth, screenHeight), Vector2.Zero, Color.RayWhite);

                        DrawText("NORMAL TEXTURE", 10, screenHeight - 30, 20, Color.DarkGreen);
                    } break;
                    case DeferredMode.DEFERRED_ALBEDO:
                    {
                        DrawTextureRec(gBuffer.Textures[2], new Rectangle(0, 0, screenWidth, screenHeight), Vector2.Zero, Color.RayWhite);

                        DrawText("ALBEDO TEXTURE", 10, screenHeight - 30, 20, Color.DarkGreen);
                    } break;
                }

                DrawText("Toggle lights keys: [Y][R][G][B]", 10, 40, 20, Color.DarkGray);
                DrawText("Switch G-buffer textures: [1][2][3][4]", 10, 70, 20, Color.DarkGray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        // De-Initialization
        // Unload the models
        UnloadModel(model);
        UnloadModel(cube);

        // Unload shaders
        UnloadShader(deferredShader);
        UnloadShader(gbufferShader);

        // Unload geometry buffer and all attached textures
        UnloadRenderTexture(gBuffer);

        CloseWindow();          // Close window and OpenGL context
    }

    // C's rand, unseeded, as glibc makes it, so the cubes stand where raylib's example built on
    // Linux puts them: 344 values from a seed of 1, each after the first 31 the sum of the ones 31
    // and 3 before it, given without its lowest bit.
    private static readonly List<uint> _rand = Seeded();

    private static List<uint> Seeded()
    {
        var r = new List<uint>(400) { 1 };
        for (int i = 1; i < 31; i++) r.Add((uint)(16807L*r[i - 1]%2147483647));
        for (int i = 31; i < 34; i++) r.Add(r[i - 31]);
        for (int i = 34; i < 344; i++) r.Add(r[i - 31] + r[i - 3]);
        return r;
    }

    private static int rand()
    {
        var next = _rand[^31] + _rand[^3];
        _rand.Add(next);
        return (int)(next >> 1);
    }
}
