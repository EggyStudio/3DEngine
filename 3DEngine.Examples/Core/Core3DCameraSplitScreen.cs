// raylib's core_3d_camera_split_screen example, Copyright (c) 2021-2025 Jeffery Myers (@JeffM2501), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core3DCameraSplitScreen
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 3d camera split screen");

        Camera3D cameraPlayer1 = default;
        cameraPlayer1.FovY = 45.0f;
        cameraPlayer1.Up.Y = 1.0f;
        cameraPlayer1.Target.Y = 1.0f;
        cameraPlayer1.Position.Z = -3.0f;
        cameraPlayer1.Position.Y = 1.0f;

        RenderTexture2D screenPlayer1 = LoadRenderTexture(screenWidth/2, screenHeight);

        Camera3D cameraPlayer2 = default;
        cameraPlayer2.FovY = 45.0f;
        cameraPlayer2.Up.Y = 1.0f;
        cameraPlayer2.Target.Y = 3.0f;
        cameraPlayer2.Position.X = -3.0f;
        cameraPlayer2.Position.Y = 3.0f;

        RenderTexture2D screenPlayer2 = LoadRenderTexture(screenWidth/2, screenHeight);

        // A render texture is upright here, where raylib's is drawn with its height negative to turn it.
        Rectangle splitScreenRect = new(0.0f, 0.0f, (float)screenPlayer1.Texture.Width, (float)screenPlayer1.Texture.Height);

        int count = 5;
        float spacing = 4;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float offsetThisFrame = 10.0f*GetFrameTime();

            if (IsKeyDown(Key.W))
            {
                cameraPlayer1.Position.Z += offsetThisFrame;
                cameraPlayer1.Target.Z += offsetThisFrame;
            }
            else if (IsKeyDown(Key.S))
            {
                cameraPlayer1.Position.Z -= offsetThisFrame;
                cameraPlayer1.Target.Z -= offsetThisFrame;
            }

            if (IsKeyDown(Key.Up))
            {
                cameraPlayer2.Position.X += offsetThisFrame;
                cameraPlayer2.Target.X += offsetThisFrame;
            }
            else if (IsKeyDown(Key.Down))
            {
                cameraPlayer2.Position.X -= offsetThisFrame;
                cameraPlayer2.Target.X -= offsetThisFrame;
            }

            BeginTextureMode(screenPlayer1);
                ClearBackground(Color.SkyBlue);

                BeginMode3D(cameraPlayer1);

                    DrawPlane(new Vector3(0, 0, 0), new Vector2(50, 50), Color.Beige);

                    for (float x = -count*spacing; x <= count*spacing; x += spacing)
                    {
                        for (float z = -count*spacing; z <= count*spacing; z += spacing)
                        {
                            DrawCube(new Vector3(x, 1.5f, z), 1, 1, 1, Color.Lime);
                            DrawCube(new Vector3(x, 0.5f, z), 0.25f, 1, 0.25f, Color.Brown);
                        }
                    }

                    DrawCube(cameraPlayer1.Position, 1, 1, 1, Color.Red);
                    DrawCube(cameraPlayer2.Position, 1, 1, 1, Color.Blue);

                EndMode3D();

                DrawRectangle(0, 0, GetScreenWidth()/2, 40, Fade(Color.RayWhite, 0.8f));
                DrawText("PLAYER1: W/S to move", 10, 10, 20, Color.Maroon);

            EndTextureMode();

            BeginTextureMode(screenPlayer2);
                ClearBackground(Color.SkyBlue);

                BeginMode3D(cameraPlayer2);

                    DrawPlane(new Vector3(0, 0, 0), new Vector2(50, 50), Color.Beige);

                    for (float x = -count*spacing; x <= count*spacing; x += spacing)
                    {
                        for (float z = -count*spacing; z <= count*spacing; z += spacing)
                        {
                            DrawCube(new Vector3(x, 1.5f, z), 1, 1, 1, Color.Lime);
                            DrawCube(new Vector3(x, 0.5f, z), 0.25f, 1, 0.25f, Color.Brown);
                        }
                    }

                    DrawCube(cameraPlayer1.Position, 1, 1, 1, Color.Red);
                    DrawCube(cameraPlayer2.Position, 1, 1, 1, Color.Blue);

                EndMode3D();

                DrawRectangle(0, 0, GetScreenWidth()/2, 40, Fade(Color.RayWhite, 0.8f));
                DrawText("PLAYER2: UP/DOWN to move", 10, 10, 20, Color.DarkBlue);

            EndTextureMode();

            BeginDrawing();
                ClearBackground(Color.Black);

                DrawTextureRec(screenPlayer1.Texture, splitScreenRect, new Vector2(0, 0), Color.White);
                DrawTextureRec(screenPlayer2.Texture, splitScreenRect, new Vector2(screenWidth/2.0f, 0), Color.White);

                DrawRectangle(GetScreenWidth()/2 - 2, 0, 4, GetScreenHeight(), Color.LightGray);
            EndDrawing();
        }

        UnloadRenderTexture(screenPlayer1);
        UnloadRenderTexture(screenPlayer2);

        CloseWindow();
    }
}
