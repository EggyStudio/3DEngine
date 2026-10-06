// raylib's shaders_depth_writing example, Copyright (c) 2022-2025 Buğra Alptekin Sarı (@BugraAlptekinSari),
// under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersDepthWriting
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] depth writing");

        // Define the camera to look into our 3d world
        Camera3D camera = new()
        {
            Position = new Vector3(2.0f, 2.0f, 3.0f),    // Camera position
            Target = new Vector3(0.0f, 0.5f, 0.0f),      // Camera looking at point
            Up = new Vector3(0.0f, 1.0f, 0.0f),          // Camera up vector (rotation towards target)
            FovY = 45.0f,                                // Camera field-of-view Y
            Projection = CameraProjection.Perspective,   // Camera projection type
        };

        // Load custom render texture with writable depth texture buffer
        RenderTexture2D target = LoadRenderTextureDepthTex(screenWidth, screenHeight);

        // Load depth writing shader
        // NOTE: The shader inverts the depth buffer by writing into it one less the blue of each color
        Shader shader = LoadShader("resources/shaders/slang/depth_write.slang");
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // Draw into our custom render texture
            BeginTextureMode(target);
                ClearBackground(Color.White);

                BeginMode3D(camera);
                    BeginShaderMode(shader);
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
        UnloadShader(shader);

        CloseWindow();
    }

    // Load custom render texture, create a writable depth texture buffer. A render texture here
    // has a depth texture of its own, which raylib's builds from rlgl's framebuffer calls.
    private static RenderTexture2D LoadRenderTextureDepthTex(int width, int height) => LoadRenderTexture(width, height);

    // Unload render texture from GPU memory (VRAM)
    private static void UnloadRenderTextureDepthTex(RenderTexture2D target) => UnloadRenderTexture(target);
}
