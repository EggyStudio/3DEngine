// raylib's models_directional_billboard example, Copyright (c) 2025 Robin (@RobinsAviary), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsDirectionalBillboard
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] directional billboard");

        Camera3D camera = new(new Vector3(2.0f, 1.0f, 2.0f), new Vector3(0.0f, 0.5f, 0.0f), Vector3.UnitY, 45.0f, CameraProjection.Perspective);

        // A sheet of the robot's frames, a row for each of eight directions
        Texture2D skillbot = LoadTexture("resources/skillbot.png");

        // Pixel art drawn far larger than its size, sampled by the nearest texel as raylib samples every texture.
        SetTextureFilter(skillbot, TextureFilter.Point);

        float anim_timer = 0.0f;
        int anim = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // A frame of the walk every half second, four in all
            anim_timer += GetFrameTime();

            if (anim_timer > 0.5f)
            {
                anim_timer = 0.0f;
                anim += 1;
            }

            if (anim >= 4) anim = 0;

            // The row for the direction the camera sees the robot from
            float dir = MathF.Floor(((Vector2Angle(new Vector2(2.0f, 0.0f), new Vector2(camera.Position.X, camera.Position.Z))/MathF.PI)*4.0f) + 0.25f);

            // A negative angle counts back from the last row.
            if (dir < 0.0f)
            {
                dir = 8.0f - (float)Math.Abs((int)dir);
            }

            BeginDrawing();

            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);

                DrawGrid(10, 1.0f);

                // Upright, facing the camera, standing on its bottom edge
                DrawBillboardPro(camera, skillbot, new Rectangle(0.0f + (anim*24.0f), 0.0f + (dir*24.0f), 24.0f, 24.0f), Vector3.Zero, Vector3.UnitY, Vector2.One, new Vector2(0.5f, 0.0f), 0, Color.White);

            EndMode3D();

            DrawText($"animation: {anim}", 10, 10, 20, Color.DarkGray);
            DrawText($"direction frame: {dir:0}", 10, 40, 20, Color.DarkGray);

            EndDrawing();
        }

        UnloadTexture(skillbot);

        CloseWindow();
    }
}
