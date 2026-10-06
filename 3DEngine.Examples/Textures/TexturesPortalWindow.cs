// raylib's textures_portal_window example, Copyright (c) 2025 PanicTitan (@PanicTitan), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesPortalWindow
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[textures] portal window");

        // Camera to navigate the "real world" (Dimension A)
        Camera3D camera = default;
        camera.Position = new Vector3(0.0f, 2.5f, 7.0f);
        camera.Target = new Vector3(0.0f, 1.8f, 0.0f);
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        camera.FovY = 45.0f;
        camera.Projection = CameraProjection.Perspective;

        // The archway opening, in Dimension A world space (used both to draw the frame and
        // as the window rectangle the oblique projection is built from)
        Vector3 archBottomLeft  = new(-1.5f, 0.0f, 0.0f);
        Vector3 archBottomRight = new( 1.5f, 0.0f, 0.0f);
        Vector3 archTopLeft     = new(-1.5f, 4.0f, 0.0f);

        // The archway sits at portalA and looks out onto portalB, far away in world space.
        // Every frame, Dimension B gets rendered to a texture using the same relative eye
        // and window position, shifted by the offset between the two portals
        Vector3 portalA = new(0.0f, 0.0f, 0.0f);
        Vector3 portalB = new(0.0f, 0.0f, -60.0f);
        RenderTexture2D portalView = LoadRenderTexture(480, 640);

        DisableCursor();        // Lock cursor for first-person free camera controls
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);
            float time = (float)GetTime();

            // Eye and window corners, shifted into Dimension B so they match the player's
            // actual position and viewing angle relative to the archway
            Vector3 offset = portalB - portalA;
            Vector3 eyeInB = camera.Position + offset;
            Vector3 blInB = archBottomLeft + offset;
            Vector3 brInB = archBottomRight + offset;
            Vector3 tlInB = archTopLeft + offset;

            // Render Dimension B into an offscreen texture, using an oblique projection so
            // its perspective lines up with the archway exactly as the real camera sees it
            BeginTextureMode(portalView);

                ClearBackground(new Color(10, 5, 20, 255));

                BeginPortalMode3D(eyeInB, blInB, brInB, tlInB, 0.05f, 100.0f);

                    DrawGrid(30, 0.8f);

                    // Floating pulsing core sphere
                    Vector3 corePos = portalB + new Vector3(0.0f, 2.0f + MathF.Sin(time * 2.5f) * 0.4f, -4.0f);
                    DrawSphere(corePos, 1.2f, Color.Purple);
                    DrawSphereWires(corePos, 1.25f, 16, 16, Color.Magenta);

                    // Orbiting cubes
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = time * 1.5f + i * (MathF.PI / 2.0f);
                        Vector3 pos = portalB + new Vector3(
                            MathF.Sin(angle) * 2.5f,
                            2.0f + MathF.Cos(time * 3.0f + i) * 0.5f,
                            -4.0f + MathF.Cos(angle) * 2.5f);

                        DrawCube(pos, 0.5f, 0.5f, 0.5f, Color.Lime);
                        DrawCubeWires(pos, 0.52f, 0.52f, 0.52f, Color.DarkGreen);
                    }

                EndPortalMode3D();

            EndTextureMode();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawGrid(20, 1.0f);

                    // Side pillars
                    DrawCube(new Vector3(-3.5f, 2.0f, 0.0f), 0.8f, 4.0f, 0.8f, Color.DarkGray);
                    DrawCubeWires(new Vector3(-3.5f, 2.0f, 0.0f), 0.8f, 4.0f, 0.8f, Color.Orange);
                    DrawCube(new Vector3(3.5f, 2.0f, 0.0f), 0.8f, 4.0f, 0.8f, Color.DarkGray);
                    DrawCubeWires(new Vector3(3.5f, 2.0f, 0.0f), 0.8f, 4.0f, 0.8f, Color.Orange);

                    // Golden archway frame and solid base
                    DrawCubeWires(new Vector3(0.0f, 2.0f, 0.0f), 3.2f, 4.2f, 0.2f, Color.Gold);
                    DrawCube(new Vector3(0.0f, 0.05f, 0.0f), 3.4f, 0.1f, 0.6f, Color.Maroon);

                    // Solid backing wall, only ever seen if looking at the archway from behind
                    DrawCube(new Vector3(0.0f, 2.0f, -0.05f), 3.1f, 4.1f, 0.05f, Color.Maroon);

                    // The portal opening itself: a plain quad textured with the Dimension B
                    // render, filling the archway exactly, so nothing "leaks" outside its shape.
                    // A render texture is stored the right way up, so its top row is at 0 down
                    // where raylib's is at 1.
                    rlSetTexture(portalView.Texture.Id);
                    rlBegin(RlDrawMode.Quads);
                        rlColor4ub(255, 255, 255, 255);
                        rlNormal3f(0.0f, 0.0f, 1.0f);
                        rlTexCoord2f(0.0f, 1.0f); rlVertex3f(archBottomLeft.X, archBottomLeft.Y, archBottomLeft.Z);
                        rlTexCoord2f(1.0f, 1.0f); rlVertex3f(archBottomRight.X, archBottomRight.Y, archBottomRight.Z);
                        rlTexCoord2f(1.0f, 0.0f); rlVertex3f(archBottomRight.X, 4.0f, archBottomRight.Z);
                        rlTexCoord2f(0.0f, 0.0f); rlVertex3f(archTopLeft.X, archTopLeft.Y, archTopLeft.Z);
                    rlEnd();
                    rlSetTexture(0);

                EndMode3D();

                // HUD overlay
                DrawRectangle(15, 15, 520, 100, Fade(Color.Black, 0.75f));
                DrawRectangleLines(15, 15, 520, 100, Color.Gold);
                DrawText("PORTAL WINDOW", 28, 25, 20, Color.Gold);
                DrawText("Look through the golden arch into Dimension B", 28, 52, 20, Color.RayWhite);
                DrawText("Controls: Mouse to look | WASD to move", 28, 80, 20, Color.Gray);

            EndDrawing();
        }

        UnloadRenderTexture(portalView);

        CloseWindow();
    }

    // Starts a 3D mode using an off-axis ("oblique frustum") projection, built directly from
    // an eye point and 3 corners of a rectangular window, instead of a fovy centered straight
    // ahead of the camera. This is the standard technique for rendering a scene as seen through
    // a window that isn't necessarily faced head-on (also used for multi-monitor and VR
    // rendering), the key difference from a normal Camera3D is that the frustum is allowed
    // to be asymmetric, so perspective lines through the window line up correctly from any
    // eye position, instead of behaving like a flat image pasted onto the window
    private static void BeginPortalMode3D(Vector3 eye, Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, float nearPlane, float farPlane)
    {
        Vector3 right = Vector3.Normalize(bottomRight - bottomLeft);
        Vector3 up = Vector3.Normalize(topLeft - bottomLeft);
        Vector3 normal = Vector3.Normalize(Vector3.Cross(right, up));

        // Vectors from the eye to 3 corners of the window, used to project the window onto
        // the near plane and read off how far it extends left/right/bottom/top of the eye
        Vector3 toBL = bottomLeft - eye;
        Vector3 toBR = bottomRight - eye;
        Vector3 toTL = topLeft - eye;

        float dist = -Vector3.Dot(toBL, normal);
        if (dist < 0.01f) dist = 0.01f; // Keep the eye from crossing the window plane

        float scale = nearPlane/dist;

        rlDrawRenderBatchActive();

        // raymath's MatrixFrustum, as System.Numerics makes it, its depth from 0 to 1
        rlMatrixMode(RlMatrixMode.Projection);
        rlPushMatrix();
        rlSetMatrixProjection(Matrix4x4.CreatePerspectiveOffCenter(
            Vector3.Dot(right, toBL)*scale, Vector3.Dot(right, toBR)*scale,
            Vector3.Dot(up, toBL)*scale, Vector3.Dot(up, toTL)*scale,
            nearPlane, farPlane));

        // View orientation is fixed to the window's own plane (looking straight through it
        // along its normal), NOT aimed at any target, which the asymmetric frustum
        // above is for, and which lets the eye move off to one side without distorting
        rlMatrixMode(RlMatrixMode.Modelview);
        rlLoadIdentity();
        rlMultMatrixf(MatrixToFloat(Matrix4x4.CreateLookAt(eye, eye - normal, up)));

        rlEnableDepthTest();
    }

    // End portal 3D mode and returns to default 2D orthographic mode
    // NOTE: Similar implementation to EndMode3D()
    private static void EndPortalMode3D()
    {
        rlDrawRenderBatchActive();      // Update and draw internal render batch

        rlMatrixMode(RlMatrixMode.Projection);    // Switch to projection matrix
        rlPopMatrix();                  // Restore previous matrix (projection) from matrix stack

        rlMatrixMode(RlMatrixMode.Modelview);     // Switch back to modelview matrix
        rlLoadIdentity();               // Reset current matrix (modelview)

        rlDisableDepthTest();           // Disable DEPTH_TEST for 2D
    }

    // raymath's MatrixToFloat, a matrix's sixteen values in its order
    private static float[] MatrixToFloat(Matrix4x4 m) =>
        [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
}
