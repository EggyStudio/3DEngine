// raylib's core_vr_simulator example, Copyright (c) 2017-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreVrSimulator
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] vr simulator");

        // VR device parameters definition
        VrDeviceInfo device = new()
        {
            HResolution = 2160,                 // Horizontal resolution in pixels
            VResolution = 1200,                 // Vertical resolution in pixels
            HScreenSize = 0.133793f,            // Horizontal size in meters
            VScreenSize = 0.0669f,              // Vertical size in meters
            EyeToScreenDistance = 0.041f,       // Distance between eye and display in meters
            LensSeparationDistance = 0.07f,     // Lens separation distance in meters
            InterpupillaryDistance = 0.07f,     // IPD (distance between pupils) in meters
            // Lens distortion constant parameters
            LensDistortionValues = new Vector4(1.0f, 0.22f, 0.24f, 0.0f),
            // Chromatic aberration correction parameters
            ChromaAbCorrection = new Vector4(0.996f, -0.004f, 1.014f, 0.0f),
        };

        // Load VR stereo config for VR device parameters (Oculus Rift CV1 parameters)
        VrStereoConfig config = LoadVrStereoConfig(device);

        // Distortion shader (uses device lens distortion and chroma)
        Shader distortion = LoadShader("resources/shaders/slang/distortion.slang");

        // Update distortion shader with lens and distortion-scale parameters
        SetShaderValue(distortion, GetShaderLocation(distortion, "leftLensCenter"), config.LeftLensCenter);
        SetShaderValue(distortion, GetShaderLocation(distortion, "rightLensCenter"), config.RightLensCenter);
        SetShaderValue(distortion, GetShaderLocation(distortion, "leftScreenCenter"), config.LeftScreenCenter);
        SetShaderValue(distortion, GetShaderLocation(distortion, "rightScreenCenter"), config.RightScreenCenter);

        SetShaderValue(distortion, GetShaderLocation(distortion, "scale"), config.Scale);
        SetShaderValue(distortion, GetShaderLocation(distortion, "scaleIn"), config.ScaleIn);
        SetShaderValue(distortion, GetShaderLocation(distortion, "deviceWarpParam"), device.LensDistortionValues);
        SetShaderValue(distortion, GetShaderLocation(distortion, "chromaAbParam"), device.ChromaAbCorrection);

        // Initialize framebuffer for stereo rendering
        // NOTE: Screen size should match HMD aspect ratio
        RenderTexture2D target = LoadRenderTexture(device.HResolution, device.VResolution);

        // The target's whole texture, which is stored the right way up here, where raylib's is
        // drawn with its height negative to turn it upright
        Rectangle sourceRec = new(0.0f, 0.0f, target.Texture.Width, target.Texture.Height);
        Rectangle destRec = new(0.0f, 0.0f, GetScreenWidth(), GetScreenHeight());

        // Define the camera to look into our 3d world
        Camera3D camera = default;
        camera.Position = new Vector3(5.0f, 2.0f, 5.0f);    // Camera position
        camera.Target = new Vector3(0.0f, 2.0f, 0.0f);      // Camera looking at point
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);          // Camera up vector
        camera.FovY = 60.0f;                                // Camera field-of-view Y
        camera.Projection = CameraProjection.Perspective;   // Camera projection type

        Vector3 cubePosition = new(0.0f, 0.0f, 0.0f);

        DisableCursor();                    // Limit cursor to relative movement inside the window

        SetTargetFPS(60);                   // Set our game to run at 60 frames-per-second

        // Main game loop
        while (!WindowShouldClose())        // Detect window close button or ESC key
        {
            // Update
            UpdateCamera(ref camera, CameraMode.FirstPerson);

            // Draw
            BeginTextureMode(target);
                ClearBackground(Color.RayWhite);
                BeginVrStereoMode(config);
                    BeginMode3D(camera);

                        DrawCube(cubePosition, 2.0f, 2.0f, 2.0f, Color.Red);
                        DrawCubeWires(cubePosition, 2.0f, 2.0f, 2.0f, Color.Maroon);
                        DrawGrid(40, 1.0f);

                    EndMode3D();
                EndVrStereoMode();
            EndTextureMode();

            BeginDrawing();
                ClearBackground(Color.RayWhite);
                BeginShaderMode(distortion);
                    DrawTexturePro(target.Texture, sourceRec, destRec, new Vector2(0.0f, 0.0f), 0.0f, Color.White);
                EndShaderMode();
                DrawFPS(10, 10);
            EndDrawing();
        }

        // De-Initialization
        UnloadVrStereoConfig(config);   // Unload stereo config

        UnloadRenderTexture(target);    // Unload stereo render fbo
        UnloadShader(distortion);       // Unload distortion shader

        CloseWindow();                  // Close window and OpenGL context
    }
}
