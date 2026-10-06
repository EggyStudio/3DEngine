// raylib's models_loading_vox example, Copyright (c) 2021-2025 Johann Nadalutti (@procfxgen) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;
using static Engine.Examples.RLights;

namespace Engine.Examples;

public static class ModelsLoadingVox
{
    private const int MAX_VOX_FILES = 4;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        string[] voxFileNames =
        [
            "resources/models/vox/chr_knight.vox",
            "resources/models/vox/chr_sword.vox",
            "resources/models/vox/monu9.vox",
            "resources/models/vox/fez.vox",
        ];

        InitWindow(screenWidth, screenHeight, "[models] loading vox");

        // Define the camera to look into our 3d world
        Camera3D camera = default;
        camera.Position = new Vector3(10.0f, 10.0f, 10.0f); // Camera position
        camera.Target = new Vector3(0.0f, 0.0f, 0.0f);      // Camera looking at point
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);          // Camera up vector (rotation towards target)
        camera.FovY = 45.0f;                                // Camera field-of-view Y
        camera.Projection = CameraProjection.Perspective;   // Camera projection type

        // Load MagicaVoxel files
        Model[] models = new Model[MAX_VOX_FILES];

        for (int i = 0; i < MAX_VOX_FILES; i++)
        {
            // Load VOX file and measure time
            double t0 = GetTime()*1000.0;
            models[i] = LoadModel(voxFileNames[i]);
            double t1 = GetTime()*1000.0;

            TraceLog(LogLevel.Info, $"[{voxFileNames[i]}] Model file loaded in {t1 - t0:0.000} ms");

            // Compute model translation matrix to center model on draw position (0, 0 , 0)
            BoundingBox bb = GetModelBoundingBox(models[i]);
            Vector3 center = default;
            center.X = bb.Min.X + (((bb.Max.X - bb.Min.X)/2));
            center.Z = bb.Min.Z + (((bb.Max.Z - bb.Min.Z)/2));

            Matrix4x4 matTranslate = Matrix4x4.CreateTranslation(-center.X, 0, -center.Z);
            models[i].Transform = matTranslate;
        }

        int currentModel = 0;
        Vector3 modelpos = default;
        Vector3 camerarot = default;

        // Load voxel shader
        Shader shader = LoadShader("resources/shaders/slang/voxel_lighting.slang");

        // Get some required shader locations
        int viewLoc = GetShaderLocation(shader, "viewPos");

        // Ambient light level (some basic lighting)
        int ambientLoc = GetShaderLocation(shader, "ambient");
        SetShaderValue(shader, ambientLoc, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));

        // Assign out lighting shader to model
        for (int i = 0; i < MAX_VOX_FILES; i++)
        {
            for (int j = 0; j < models[i].Materials.Length; j++) models[i].Materials[j].Shader = shader;
        }

        // Create lights
        RLights.Light[] lights = new RLights.Light[MAX_LIGHTS];
        lights[0] = CreateLight(LIGHT_POINT, new Vector3(-20, 20, -20), Vector3.Zero, Color.Gray, shader);
        lights[1] = CreateLight(LIGHT_POINT, new Vector3(20, -20, 20), Vector3.Zero, Color.Gray, shader);
        lights[2] = CreateLight(LIGHT_POINT, new Vector3(-20, 20, 20), Vector3.Zero, Color.Gray, shader);
        lights[3] = CreateLight(LIGHT_POINT, new Vector3(20, -20, -20), Vector3.Zero, Color.Gray, shader);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsMouseButtonDown(MouseButton.Middle))
            {
                Vector2 mouseDelta = GetMouseDelta();
                camerarot.X = mouseDelta.X*0.05f;
                camerarot.Y = mouseDelta.Y*0.05f;
            }
            else
            {
                camerarot.X = 0;
                camerarot.Y = 0;
            }

            // Update camera movement, custom controls
            float Down(Key a, Key b) => (IsKeyDown(a) || IsKeyDown(b)) ? 1.0f : 0.0f;
            UpdateCameraPro(ref camera,
                new Vector3(Down(Key.W, Key.Up)*0.1f - Down(Key.S, Key.Down)*0.1f,       // Move forward-backward
                            Down(Key.D, Key.Right)*0.1f - Down(Key.A, Key.Left)*0.1f,    // Move right-left
                            0.0f),                                                       // Move up-down
                camerarot,                                                               // Camera rotation
                GetMouseWheelMove()*-2.0f);                                              // Move to target (zoom)

            // Cycle between models on mouse click
            if (IsMouseButtonPressed(MouseButton.Left)) currentModel = (currentModel + 1)%MAX_VOX_FILES;

            // Update the shader with the camera view vector (points towards { 0.0f, 0.0f, 0.0f })
            SetShaderValue(shader, viewLoc, camera.Position);

            // Update light values (only enable/disable them)
            for (int i = 0; i < MAX_LIGHTS; i++) UpdateLightValues(shader, lights[i]);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // Draw 3D model
                BeginMode3D(camera);
                    DrawModel(models[currentModel], modelpos, 1.0f, Color.White);
                    DrawGrid(10, 1.0f);

                    // Draw spheres to show where the lights are
                    for (int i = 0; i < MAX_LIGHTS; i++)
                    {
                        if (lights[i].enabled) DrawSphereEx(lights[i].position, 0.2f, 8, 8, lights[i].color);
                        else DrawSphereWires(lights[i].position, 0.2f, 8, 8, ColorAlpha(lights[i].color, 0.3f));
                    }
                EndMode3D();

                // Display info
                DrawRectangle(10, 40, 340, 70, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(10, 40, 340, 70, Fade(Color.DarkBlue, 0.5f));
                DrawText("- MOUSE LEFT BUTTON: CYCLE VOX MODELS", 20, 50, 10, Color.Blue);
                DrawText("- MOUSE MIDDLE BUTTON: ZOOM OR ROTATE CAMERA", 20, 70, 10, Color.Blue);
                DrawText("- UP-DOWN-LEFT-RIGHT KEYS: MOVE CAMERA", 20, 90, 10, Color.Blue);
                DrawText($"VOX model file: {Path.GetFileName(voxFileNames[currentModel])}", 10, 10, 20, Color.Gray);

            EndDrawing();
        }

        // Unload models data (GPU VRAM)
        for (int i = 0; i < MAX_VOX_FILES; i++) UnloadModel(models[i]);

        // Unload shader data
        UnloadShader(shader);

        CloseWindow();
    }
}
