// raylib's shaders_depth_rendering example, Copyright (c) 2025 Luís Almeida (@luis605), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersDepthRendering
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shaders] depth rendering");

        Camera3D camera = new(new Vector3(4.0f, 1.0f, 5.0f), new Vector3(0.0f, 0.0f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // Load render texture with a depth texture attached
        RenderTexture2D target = LoadRenderTextureDepthTex(screenWidth, screenHeight);

        // raylib's depth_render.fs, written in Slang
        Shader depthShader = LoadShader("resources/shaders/slang/depth_render.slang");
        int depthLoc = GetShaderLocation(depthShader, "depthTexture");
        int flipTextureLoc = GetShaderLocation(depthShader, "flipY");
        // Not flipped, since a target here keeps its rows from the top, where OpenGL keeps them
        // from the bottom and raylib flips them
        SetShaderValue(depthShader, flipTextureLoc, 0);

        // Load scene models
        Model cube = LoadModelFromMesh(GenMeshCube(1.0f, 1.0f, 1.0f));
        Model floor = LoadModelFromMesh(GenMeshPlane(20.0f, 20.0f, 1, 1));

        DisableCursor();  // Limit cursor to relative movement inside the window

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            BeginTextureMode(target);
                ClearBackground(Color.White);

                BeginMode3D(camera);
                    DrawModel(cube, new Vector3(0.0f, 0.0f, 0.0f), 3.0f, Color.Yellow);
                    DrawModel(floor, new Vector3(10.0f, 0.0f, 2.0f), 2.0f, Color.Red);
                EndMode3D();
            EndTextureMode();

            // Draw into screen (main framebuffer)
            BeginDrawing();
                ClearBackground(Color.RayWhite);

                BeginShaderMode(depthShader);
                    SetShaderValueTexture(depthShader, depthLoc, target.Depth);
                    DrawTexture(target.Depth, 0, 0, Color.White);
                EndShaderMode();

                DrawRectangle(10, 10, 320, 93, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(10, 10, 320, 93, Color.Blue);

                DrawText("Camera Controls:", 20, 20, 10, Color.Black);
                DrawText("- WASD to move", 40, 40, 10, Color.DarkGray);
                DrawText("- Mouse Wheel Pressed to Pan", 40, 60, 10, Color.DarkGray);
                DrawText("- Z to zoom to (0, 0, 0)", 40, 80, 10, Color.DarkGray);

            EndDrawing();
        }

        UnloadModel(cube);
        UnloadModel(floor);
        UnloadRenderTextureDepthTex(target);
        UnloadShader(depthShader);

        CloseWindow();
    }

    // A render texture with its depth as a texture. raylib makes one through rlgl, since its own
    // keeps depth where no shader reads it, and a render texture here has its depth in Depth.
    private static RenderTexture2D LoadRenderTextureDepthTex(int width, int height) => LoadRenderTexture(width, height);

    private static void UnloadRenderTextureDepthTex(RenderTexture2D target) => UnloadRenderTexture(target);
}
