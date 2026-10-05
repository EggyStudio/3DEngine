// raylib's core_2d_camera_split_screen example, Copyright (c) 2023-2025 Gabriel dos Santos Sanches (@gabrielssanches), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core2DCameraSplitScreen
{
    private const int PLAYER_SIZE = 40;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 440;

        InitWindow(screenWidth, screenHeight, "[core] 2d camera split screen");

        Rectangle player1 = new(200, 200, PLAYER_SIZE, PLAYER_SIZE);
        Rectangle player2 = new(250, 200, PLAYER_SIZE, PLAYER_SIZE);

        Camera2D camera1 = default;
        camera1.Target = new Vector2(player1.X, player1.Y);
        camera1.Offset = new Vector2(200.0f, 200.0f);
        camera1.Rotation = 0.0f;
        camera1.Zoom = 1.0f;

        Camera2D camera2 = default;
        camera2.Target = new Vector2(player2.X, player2.Y);
        camera2.Offset = new Vector2(200.0f, 200.0f);
        camera2.Rotation = 0.0f;
        camera2.Zoom = 1.0f;

        RenderTexture2D screenCamera1 = LoadRenderTexture(screenWidth/2, screenHeight);
        RenderTexture2D screenCamera2 = LoadRenderTexture(screenWidth/2, screenHeight);

        // A render texture is upright here, where raylib's is drawn with its height negative to turn it.
        Rectangle splitScreenRect = new(0.0f, 0.0f, (float)screenCamera1.Texture.Width, (float)screenCamera1.Texture.Height);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.S)) player1 = player1 with { Y = player1.Y + (3.0f) };
            else if (IsKeyDown(Key.W)) player1 = player1 with { Y = player1.Y - (3.0f) };
            if (IsKeyDown(Key.D)) player1 = player1 with { X = player1.X + (3.0f) };
            else if (IsKeyDown(Key.A)) player1 = player1 with { X = player1.X - (3.0f) };

            if (IsKeyDown(Key.Up)) player2 = player2 with { Y = player2.Y - (3.0f) };
            else if (IsKeyDown(Key.Down)) player2 = player2 with { Y = player2.Y + (3.0f) };
            if (IsKeyDown(Key.Right)) player2 = player2 with { X = player2.X + (3.0f) };
            else if (IsKeyDown(Key.Left)) player2 = player2 with { X = player2.X - (3.0f) };

            camera1.Target = new Vector2(player1.X, player1.Y);
            camera2.Target = new Vector2(player2.X, player2.Y);

            BeginTextureMode(screenCamera1);
                ClearBackground(Color.RayWhite);

                BeginMode2D(camera1);

                    for (int i = 0; i < screenWidth/PLAYER_SIZE + 1; i++)
                    {
                        DrawLineV(new Vector2((float)PLAYER_SIZE*i, 0), new Vector2((float)PLAYER_SIZE*i, (float)screenHeight), Color.LightGray);
                    }

                    for (int i = 0; i < screenHeight/PLAYER_SIZE + 1; i++)
                    {
                        DrawLineV(new Vector2(0, (float)PLAYER_SIZE*i), new Vector2((float)screenWidth, (float)PLAYER_SIZE*i), Color.LightGray);
                    }

                    for (int i = 0; i < screenWidth/PLAYER_SIZE; i++)
                    {
                        for (int j = 0; j < screenHeight/PLAYER_SIZE; j++)
                        {
                            DrawText($"[{i},{j}]", 10 + PLAYER_SIZE*i, 15 + PLAYER_SIZE*j, 10, Color.LightGray);
                        }
                    }

                    DrawRectangleRec(player1, Color.Red);
                    DrawRectangleRec(player2, Color.Blue);
                EndMode2D();

                DrawRectangle(0, 0, GetScreenWidth()/2, 30, Fade(Color.RayWhite, 0.6f));
                DrawText("PLAYER1: W/S/A/D to move", 10, 10, 10, Color.Maroon);

            EndTextureMode();

            BeginTextureMode(screenCamera2);
                ClearBackground(Color.RayWhite);

                BeginMode2D(camera2);

                    for (int i = 0; i < screenWidth/PLAYER_SIZE + 1; i++)
                    {
                        DrawLineV(new Vector2((float)PLAYER_SIZE*i, 0), new Vector2((float)PLAYER_SIZE*i, (float)screenHeight), Color.LightGray);
                    }

                    for (int i = 0; i < screenHeight/PLAYER_SIZE + 1; i++)
                    {
                        DrawLineV(new Vector2(0, (float)PLAYER_SIZE*i), new Vector2((float)screenWidth, (float)PLAYER_SIZE*i), Color.LightGray);
                    }

                    for (int i = 0; i < screenWidth/PLAYER_SIZE; i++)
                    {
                        for (int j = 0; j < screenHeight/PLAYER_SIZE; j++)
                        {
                            DrawText($"[{i},{j}]", 10 + PLAYER_SIZE*i, 15 + PLAYER_SIZE*j, 10, Color.LightGray);
                        }
                    }

                    DrawRectangleRec(player1, Color.Red);
                    DrawRectangleRec(player2, Color.Blue);

                EndMode2D();

                DrawRectangle(0, 0, GetScreenWidth()/2, 30, Fade(Color.RayWhite, 0.6f));
                DrawText("PLAYER2: UP/DOWN/LEFT/RIGHT to move", 10, 10, 10, Color.DarkBlue);

            EndTextureMode();

            BeginDrawing();
                ClearBackground(Color.Black);

                DrawTextureRec(screenCamera1.Texture, splitScreenRect, new Vector2(0, 0), Color.White);
                DrawTextureRec(screenCamera2.Texture, splitScreenRect, new Vector2(screenWidth/2.0f, 0), Color.White);

                DrawRectangle(GetScreenWidth()/2 - 2, 0, 4, GetScreenHeight(), Color.LightGray);
            EndDrawing();
        }

        UnloadRenderTexture(screenCamera1);
        UnloadRenderTexture(screenCamera2);

        CloseWindow();
    }
}
