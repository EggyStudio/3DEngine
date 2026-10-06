// raylib's shaders_hybrid_rendering example, Copyright (c) 2022-2025 Buğra Alptekin Sarı
// (@BugraAlptekinSari), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersHybridRendering
{
    private struct RayLocs
    {
        public int camPos;
        public int camDir;
        public int screenCenter;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] hybrid rendering");

        // This Shader calculates pixel depth and color using raymarch
        Shader shdrRaymarch = LoadShader("resources/shaders/slang/hybrid_raymarch.slang");

        // This Shader is a standard rasterization fragment shader with the addition of depth writing
        // You are required to write depth for all shaders if one shader does it
        Shader shdrRaster = LoadShader("resources/shaders/slang/hybrid_raster.slang");

        // Declare Struct used to store camera locs
        RayLocs marchLocs = default;

        // Fill the struct with shader locs
        marchLocs.camPos = GetShaderLocation(shdrRaymarch, "camPos");
        marchLocs.camDir = GetShaderLocation(shdrRaymarch, "camDir");
        marchLocs.screenCenter = GetShaderLocation(shdrRaymarch, "screenCenter");

        // Transfer screenCenter position to shader. Which is used to calculate ray direction
        Vector2 screenCenter = new(screenWidth/2.0f, screenHeight/2.0f);
        SetShaderValue(shdrRaymarch, marchLocs.screenCenter, screenCenter);

        // Use Customized function to create writable depth texture buffer
        RenderTexture2D target = LoadRenderTextureDepthTex(screenWidth, screenHeight);

        // Define the camera to look into our 3d world
        Camera3D camera = new()
        {
            Position = new Vector3(0.5f, 1.0f, 1.5f),    // Camera position
            Target = new Vector3(0.0f, 0.5f, 0.0f),      // Camera looking at point
            Up = new Vector3(0.0f, 1.0f, 0.0f),          // Camera up vector (rotation towards target)
            FovY = 45.0f,                                // Camera field-of-view Y
            Projection = CameraProjection.Perspective,   // Camera projection type
        };

        // Camera FOV is pre-calculated in the camera distance
        float camDist = 1.0f/(MathF.Tan(camera.FovY*0.5f*(MathF.PI/180.0f)));

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // Update Camera Position in the ray march shader
            SetShaderValue(shdrRaymarch, marchLocs.camPos, camera.Position);

            // Update Camera Looking Vector. Vector length determines FOV
            Vector3 camDir = Vector3.Normalize(camera.Target - camera.Position)*camDist;
            SetShaderValue(shdrRaymarch, marchLocs.camDir, camDir);

            // Draw into our custom render texture (framebuffer)
            BeginTextureMode(target);
                ClearBackground(Color.White);

                // Raymarch Scene
                rlEnableDepthTest(); // Manually enable Depth Test to handle multiple rendering methods
                BeginShaderMode(shdrRaymarch);
                    DrawRectangleRec(new Rectangle(0, 0, (float)screenWidth, (float)screenHeight), Color.White);
                EndShaderMode();

                // Rasterize Scene
                BeginMode3D(camera);
                    BeginShaderMode(shdrRaster);
                        DrawCubeWiresV(new Vector3(0.0f, 0.5f, 1.0f), new Vector3(1.0f, 1.0f, 1.0f), Color.Red);
                        DrawCubeV(new Vector3(0.0f, 0.5f, 1.0f), new Vector3(1.0f, 1.0f, 1.0f), Color.Purple);
                        DrawCubeWiresV(new Vector3(0.0f, 0.5f, -1.0f), new Vector3(1.0f, 1.0f, 1.0f), Color.DarkGreen);
                        DrawCubeV(new Vector3(0.0f, 0.5f, -1.0f), new Vector3(1.0f, 1.0f, 1.0f), Color.Yellow);
                        DrawGrid(10, 1.0f);
                    EndShaderMode();
                EndMode3D();
            EndTextureMode();

            // Draw into screen our custom render texture. A render texture is stored the right way
            // up, so it is drawn with its height as it is, where raylib's is turned.
            BeginDrawing();
                ClearBackground(Color.RayWhite);

                DrawTextureRec(target.Texture, new Rectangle(0, 0, (float)screenWidth, (float)screenHeight), new Vector2(0, 0), Color.White);

                DrawFPS(10, 10);
            EndDrawing();
        }

        UnloadRenderTextureDepthTex(target);
        UnloadShader(shdrRaymarch);
        UnloadShader(shdrRaster);

        CloseWindow();
    }

    // Load custom render texture, create a writable depth texture buffer. A render texture here
    // has a depth texture of its own, which raylib's builds from rlgl's framebuffer calls.
    private static RenderTexture2D LoadRenderTextureDepthTex(int width, int height) => LoadRenderTexture(width, height);

    // Unload render texture from GPU memory (VRAM)
    private static void UnloadRenderTextureDepthTex(RenderTexture2D target) => UnloadRenderTexture(target);
}
