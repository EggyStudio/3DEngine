// raylib's core_2d_camera example, Copyright (c) 2016-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core2DCamera
{
    private const int MAX_BUILDINGS = 100;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 2d camera");

        Rectangle player = new(400, 280, 40, 40);
        Rectangle[] buildings = new Rectangle[MAX_BUILDINGS];
        Color[] buildColors = new Color[MAX_BUILDINGS];

        int spacing = 0;

        for (int i = 0; i < MAX_BUILDINGS; i++)
        {
            buildings[i] = buildings[i] with { Width = (float)GetRandomValue(50, 200) };
            buildings[i] = buildings[i] with { Height = (float)GetRandomValue(100, 800) };
            buildings[i] = buildings[i] with { Y = screenHeight - 130.0f - buildings[i].Height };
            buildings[i] = buildings[i] with { X = -6000.0f + spacing };

            spacing += (int)buildings[i].Width;

            buildColors[i] = new Color((byte)GetRandomValue(200, 240),
                (byte)GetRandomValue(200, 240),
                (byte)GetRandomValue(200, 250),
                255);
        }

        Camera2D camera = default;
        camera.Target = new Vector2(player.X + 20.0f, player.Y + 20.0f);
        camera.Offset = new Vector2(screenWidth/2.0f, screenHeight/2.0f);
        camera.Rotation = 0.0f;
        camera.Zoom = 1.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Right)) player = player with { X = player.X + (2) };
            else if (IsKeyDown(Key.Left)) player = player with { X = player.X - (2) };

            camera.Target = new Vector2(player.X + 20, player.Y + 20);

            if (IsKeyDown(Key.A)) camera.Rotation--;
            else if (IsKeyDown(Key.S)) camera.Rotation++;

            if (camera.Rotation > 40) camera.Rotation = 40;
            else if (camera.Rotation < -40) camera.Rotation = -40;

            camera.Zoom = MathF.Exp(MathF.Log(camera.Zoom) + ((float)GetMouseWheelMove()*0.1f));

            if (camera.Zoom > 3.0f) camera.Zoom = 3.0f;
            else if (camera.Zoom < 0.1f) camera.Zoom = 0.1f;

            if (IsKeyPressed(Key.R))
            {
                camera.Zoom = 1.0f;
                camera.Rotation = 0.0f;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode2D(camera);

                    DrawRectangle(-6000, 320, 13000, 8000, Color.DarkGray);

                    for (int i = 0; i < MAX_BUILDINGS; i++) DrawRectangleRec(buildings[i], buildColors[i]);

                    DrawRectangleRec(player, Color.Red);

                    DrawLine((int)camera.Target.X, -screenHeight*10, (int)camera.Target.X, screenHeight*10, Color.Green);
                    DrawLine(-screenWidth*10, (int)camera.Target.Y, screenWidth*10, (int)camera.Target.Y, Color.Green);

                EndMode2D();

                DrawText("SCREEN AREA", 640, 10, 20, Color.Red);

                DrawRectangle(0, 0, screenWidth, 5, Color.Red);
                DrawRectangle(0, 5, 5, screenHeight - 10, Color.Red);
                DrawRectangle(screenWidth - 5, 5, 5, screenHeight - 10, Color.Red);
                DrawRectangle(0, screenHeight - 5, screenWidth, 5, Color.Red);

                DrawRectangle( 10, 10, 250, 113, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines( 10, 10, 250, 113, Color.Blue);

                DrawText("Free 2D camera controls:", 20, 20, 10, Color.Black);
                DrawText("- Right/Left to move player", 40, 40, 10, Color.DarkGray);
                DrawText("- Mouse Wheel to Zoom in-out", 40, 60, 10, Color.DarkGray);
                DrawText("- A / S to Rotate", 40, 80, 10, Color.DarkGray);
                DrawText("- R to reset Zoom and Rotation", 40, 100, 10, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
