// raylib's textures_framebuffer_rendering example, Copyright (c) 2026 Jack Boakes (@jackboakes), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesFramebufferRendering
{
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;
        const int splitWidth = screenWidth/2;

        InitWindow(screenWidth, screenHeight, "[textures] framebuffer rendering");

        // The camera whose view is shown, and the camera that watches it
        Camera3D subjectCamera = new(new Vector3(5.0f, 5.0f, 5.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);
        Camera3D observerCamera = new(new Vector3(10.0f, 10.0f, 10.0f), Vector3.Zero, Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // A target is stored the right way up here, so each is drawn with its height as it is,
        // where raylib's sources are given negative heights to turn them.
        RenderTexture2D observerTarget = LoadRenderTexture(splitWidth, screenHeight);
        Rectangle observerSource = new(0.0f, 0.0f, (float)observerTarget.Texture.Width, (float)observerTarget.Texture.Height);
        Rectangle observerDest = new(0.0f, 0.0f, (float)splitWidth, (float)screenHeight);

        RenderTexture2D subjectTarget = LoadRenderTexture(splitWidth, screenHeight);
        Rectangle subjectSource = new(0.0f, 0.0f, (float)subjectTarget.Texture.Width, (float)subjectTarget.Texture.Height);
        Rectangle subjectDest = new((float)splitWidth, 0.0f, (float)splitWidth, (float)screenHeight);
        float textureAspectRatio = (float)subjectTarget.Texture.Width/(float)subjectTarget.Texture.Height;

        // The part of the subject's view cut out and shown again small
        const float captureSize = 128.0f;
        Rectangle cropSource = new((subjectTarget.Texture.Width - captureSize)/2.0f, (subjectTarget.Texture.Height - captureSize)/2.0f, captureSize, captureSize);
        Rectangle cropDest = new(splitWidth + 20.0f, 20.0f, captureSize, captureSize);

        SetTargetFPS(60);
        DisableCursor();

        while (!WindowShouldClose())
        {
            UpdateCamera(ref observerCamera, CameraMode.Free);
            UpdateCamera(ref subjectCamera, CameraMode.Orbital);

            if (IsKeyPressed(Key.R)) observerCamera.Target = Vector3.Zero;

            // The observer's view, on the left
            BeginTextureMode(observerTarget);
                ClearBackground(Color.RayWhite);

                BeginMode3D(observerCamera);
                    DrawGrid(10, 1.0f);
                    DrawCube(Vector3.Zero, 2.0f, 2.0f, 2.0f, Color.Gold);
                    DrawCubeWires(Vector3.Zero, 2.0f, 2.0f, 2.0f, Color.Pink);
                    DrawCameraPrism(subjectCamera, textureAspectRatio, Color.Green);
                EndMode3D();

                DrawText("Observer View", 10, observerTarget.Texture.Height - 30, 20, Color.Black);
                DrawText("WASD + Mouse to Move", 10, 10, 20, Color.DarkGray);
                DrawText("Scroll to Zoom", 10, 30, 20, Color.DarkGray);
                DrawText("R to Reset Observer Target", 10, 50, 20, Color.DarkGray);
            EndTextureMode();

            // The subject's view, on the right
            BeginTextureMode(subjectTarget);
                ClearBackground(Color.RayWhite);

                BeginMode3D(subjectCamera);
                    DrawCube(Vector3.Zero, 2.0f, 2.0f, 2.0f, Color.Gold);
                    DrawCubeWires(Vector3.Zero, 2.0f, 2.0f, 2.0f, Color.Pink);
                    DrawGrid(10, 1.0f);
                EndMode3D();

                DrawRectangleLines((int)((subjectTarget.Texture.Width - captureSize)/2.0f), (int)((subjectTarget.Texture.Height - captureSize)/2.0f), (int)captureSize, (int)captureSize, Color.Green);
                DrawText("Subject View", 10, subjectTarget.Texture.Height - 30, 20, Color.Black);
            EndTextureMode();

            BeginDrawing();
                ClearBackground(Color.Black);

                DrawTexturePro(observerTarget.Texture, observerSource, observerDest, Vector2.Zero, 0.0f, Color.White);

                DrawTexturePro(subjectTarget.Texture, subjectSource, subjectDest, Vector2.Zero, 0.0f, Color.White);

                // The small cut over them
                DrawTexturePro(subjectTarget.Texture, cropSource, cropDest, Vector2.Zero, 0.0f, Color.White);
                DrawRectangleLinesEx(cropDest, 2, Color.Black);

                DrawLine(splitWidth, 0, splitWidth, screenHeight, Color.Black);
            EndDrawing();
        }

        UnloadRenderTexture(observerTarget);
        UnloadRenderTexture(subjectTarget);

        CloseWindow();
    }

    // The camera's view as a pyramid, its base the plane through the target.
    private static void DrawCameraPrism(Camera3D camera, float aspect, Color color)
    {
        float length = Vector3.Distance(camera.Position, camera.Target);

        // The base's corners in clip space, at the far plane, which is the target's distance
        Vector3[] planeNDC =
        [
            new(-1.0f, -1.0f, 1.0f),    // Bottom left
            new( 1.0f, -1.0f, 1.0f),    // Bottom right
            new( 1.0f,  1.0f, 1.0f),    // Top right
            new(-1.0f,  1.0f, 1.0f),    // Top left
        ];

        // raymath's MatrixPerspective, MatrixMultiply and MatrixInvert are C#'s own, as the
        // comparison with raylib says, with the far plane at a depth of 1 in both.
        Matrix4x4 view = GetCameraMatrix(camera);
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(camera.FovY*DEG2RAD, aspect, 0.05f, length);
        Matrix4x4.Invert(view*proj, out Matrix4x4 inverseViewProj);

        // Each corner back into the world, divided by its w
        Vector3[] corners = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            Vector4 v = Vector4.Transform(new Vector4(planeNDC[i], 1.0f), inverseViewProj);
            corners[i] = new Vector3(v.X/v.W, v.Y/v.W, v.Z/v.W);
        }

        // The base,
        DrawLine3D(corners[0], corners[1], color);
        DrawLine3D(corners[1], corners[2], color);
        DrawLine3D(corners[2], corners[3], color);
        DrawLine3D(corners[3], corners[0], color);

        // and its edges back to the camera
        for (int i = 0; i < 4; i++)
        {
            DrawLine3D(camera.Position, corners[i], color);
        }
    }
}
