// raylib's core_3d_camera_first_person example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core3DCameraFirstPerson
{
    private const int MAX_COLUMNS = 20;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 3d camera first person");

        Camera3D camera = default;
        camera.Position = new Vector3(0.0f, 2.0f, 4.0f);
        camera.Target = new Vector3(0.0f, 2.0f, 0.0f);
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        camera.FovY = 60.0f;
        camera.Projection = CameraProjection.Perspective;

        CameraMode cameraMode = CameraMode.FirstPerson;

        float[] heights = new float[MAX_COLUMNS];
        Vector3[] positions = new Vector3[MAX_COLUMNS];
        Color[] colors = new Color[MAX_COLUMNS];

        for (int i = 0; i < MAX_COLUMNS; i++)
        {
            heights[i] = (float)GetRandomValue(1, 12);
            positions[i] = new Vector3((float)GetRandomValue(-15, 15), heights[i]/2.0f, (float)GetRandomValue(-15, 15));
            colors[i] = new Color((byte)(GetRandomValue(20, 255)), (byte)(GetRandomValue(10, 55)), 30, 255);
        }

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Alpha1))
            {
                cameraMode = CameraMode.Free;
                camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
            }

            if (IsKeyPressed(Key.Alpha2))
            {
                cameraMode = CameraMode.FirstPerson;
                camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
            }

            if (IsKeyPressed(Key.Alpha3))
            {
                cameraMode = CameraMode.ThirdPerson;
                camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
            }

            if (IsKeyPressed(Key.Alpha4))
            {
                cameraMode = CameraMode.Orbital;
                camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
            }

            if (IsKeyPressed(Key.P))
            {
                if (camera.Projection == CameraProjection.Perspective)
                {
                    cameraMode = CameraMode.ThirdPerson;

                    camera.Position = new Vector3(0.0f, 2.0f, -100.0f);
                    camera.Target = new Vector3(0.0f, 2.0f, 0.0f);
                    camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
                    camera.Projection = CameraProjection.Orthographic;
                    camera.FovY = 20.0f;
                    CameraYaw(ref camera, -135*(MathF.PI/180), true);
                    CameraPitch(ref camera, -45*(MathF.PI/180), true, true, false);
                }
                else if (camera.Projection == CameraProjection.Orthographic)
                {
                    cameraMode = CameraMode.ThirdPerson;
                    camera.Position = new Vector3(0.0f, 2.0f, 10.0f);
                    camera.Target = new Vector3(0.0f, 2.0f, 0.0f);
                    camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
                    camera.Projection = CameraProjection.Perspective;
                    camera.FovY = 60.0f;
                }
            }

            UpdateCamera(ref camera, cameraMode);
    /*

            UpdateCameraPro(ref camera,
                new Vector3((IsKeyDown(Key.W) || IsKeyDown(Key.Up))*0.1f -
                    (IsKeyDown(Key.S) || IsKeyDown(Key.Down))*0.1f,
                    (IsKeyDown(Key.D) || IsKeyDown(Key.Right))*0.1f -
                    (IsKeyDown(Key.A) || IsKeyDown(Key.Left))*0.1f,
                    0.0f
                new Vector3(GetMouseDelta().X*0.05f,
                    GetMouseDelta().Y*0.05f,
                    0.0f
                GetMouseWheelMove()*2.0f);
    */

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawPlane(new Vector3(0.0f, 0.0f, 0.0f), new Vector2(32.0f, 32.0f), Color.LightGray);
                    DrawCube(new Vector3(-16.0f, 2.5f, 0.0f), 1.0f, 5.0f, 32.0f, Color.Blue);
                    DrawCube(new Vector3(16.0f, 2.5f, 0.0f), 1.0f, 5.0f, 32.0f, Color.Lime);
                    DrawCube(new Vector3(0.0f, 2.5f, 16.0f), 32.0f, 5.0f, 1.0f, Color.Gold);

                    for (int i = 0; i < MAX_COLUMNS; i++)
                    {
                        DrawCube(positions[i], 2.0f, heights[i], 2.0f, colors[i]);
                        DrawCubeWires(positions[i], 2.0f, heights[i], 2.0f, Color.Maroon);
                    }

                    if (cameraMode == CameraMode.ThirdPerson)
                    {
                        DrawCube(camera.Target, 0.5f, 0.5f, 0.5f, Color.Purple);
                        DrawCubeWires(camera.Target, 0.5f, 0.5f, 0.5f, Color.DarkPurple);
                    }

                EndMode3D();

                DrawRectangle(5, 5, 330, 100, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(5, 5, 330, 100, Color.Blue);

                DrawText("Camera controls:", 15, 15, 10, Color.Black);
                DrawText("- Move keys: W, A, S, D, Space, Left-Ctrl", 15, 30, 10, Color.Black);
                DrawText("- Look around: arrow keys or mouse", 15, 45, 10, Color.Black);
                DrawText("- Camera mode keys: 1, 2, 3, 4", 15, 60, 10, Color.Black);
                DrawText("- Zoom keys: num-plus, num-minus or mouse scroll", 15, 75, 10, Color.Black);
                DrawText("- Camera projection key: P", 15, 90, 10, Color.Black);

                DrawRectangle(600, 5, 195, 100, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(600, 5, 195, 100, Color.Blue);

                DrawText("Camera status:", 610, 15, 10, Color.Black);
                DrawText($"- Mode: {((cameraMode == CameraMode.Free) ? "FREE" : (cameraMode == CameraMode.FirstPerson) ? "FIRST_PERSON" : (cameraMode == CameraMode.ThirdPerson) ? "THIRD_PERSON" : (cameraMode == CameraMode.Orbital) ? "ORBITAL" : "CUSTOM")}", 610, 30, 10, Color.Black);
                DrawText($"- Projection: {((camera.Projection == CameraProjection.Perspective) ? "PERSPECTIVE" : (camera.Projection == CameraProjection.Orthographic) ? "ORTHOGRAPHIC" : "CUSTOM")}", 610, 45, 10, Color.Black);
                DrawText($"- Position: ({camera.Position.X:00.000}, {camera.Position.Y:00.000}, {camera.Position.Z:00.000})", 610, 60, 10, Color.Black);
                DrawText($"- Target: ({camera.Target.X:00.000}, {camera.Target.Y:00.000}, {camera.Target.Z:00.000})", 610, 75, 10, Color.Black);
                DrawText($"- Up: ({camera.Up.X:00.000}, {camera.Up.Y:00.000}, {camera.Up.Z:00.000})", 610, 90, 10, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }
}
