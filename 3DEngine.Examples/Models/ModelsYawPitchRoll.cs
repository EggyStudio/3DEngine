// raylib's models_yaw_pitch_roll example, Copyright (c) 2017-2025 Berni (@Berni8k) and Ramon Santamaria
// (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsYawPitchRoll
{
    private const float DEG2RAD = MathF.PI/180.0f;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[models] yaw pitch roll");

        Camera3D camera = new(new Vector3(0.0f, 50.0f, -120.0f), Vector3.Zero, Vector3.UnitY, 30.0f, CameraProjection.Perspective);

        Model model = LoadModel("resources/models/obj/plane.obj");
        Texture2D texture = LoadTexture("resources/models/obj/plane_diffuse.png");
        SetTextureWrap(texture, TextureWrap.Repeat);
        model.Materials[0].Texture = texture;

        float pitch = 0.0f;
        float roll = 0.0f;
        float yaw = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // Pitch, about x, eases back to level when let go,
            if (IsKeyDown(Key.Down)) pitch += 0.6f;
            else if (IsKeyDown(Key.Up)) pitch -= 0.6f;
            else
            {
                if (pitch > 0.3f) pitch -= 0.3f;
                else if (pitch < -0.3f) pitch += 0.3f;
            }

            // as yaw does about y,
            if (IsKeyDown(Key.S)) yaw -= 1.0f;
            else if (IsKeyDown(Key.A)) yaw += 1.0f;
            else
            {
                if (yaw > 0.0f) yaw -= 0.5f;
                else if (yaw < 0.0f) yaw += 0.5f;
            }

            // and roll about z.
            if (IsKeyDown(Key.Left)) roll -= 1.0f;
            else if (IsKeyDown(Key.Right)) roll += 1.0f;
            else
            {
                if (roll > 0.0f) roll -= 0.5f;
                else if (roll < 0.0f) roll += 0.5f;
            }

            model.Transform = MatrixRotateXYZ(new Vector3(DEG2RAD*pitch, DEG2RAD*yaw, DEG2RAD*roll));

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);
                    DrawModel(model, new Vector3(0.0f, -8.0f, 0.0f), 1.0f, Color.White);
                    DrawGrid(10, 10.0f);
                EndMode3D();

                DrawRectangle(30, 370, 260, 70, Fade(Color.Green, 0.5f));
                DrawRectangleLines(30, 370, 260, 70, Fade(Color.DarkGreen, 0.5f));
                DrawText("Pitch controlled with: KEY_UP / KEY_DOWN", 40, 380, 10, Color.DarkGray);
                DrawText("Roll controlled with: KEY_LEFT / KEY_RIGHT", 40, 400, 10, Color.DarkGray);
                DrawText("Yaw controlled with: KEY_A / KEY_S", 40, 420, 10, Color.DarkGray);

                DrawText("(c) WWI Plane Model created by GiaHanLam", screenWidth - 240, screenHeight - 20, 10, Color.DarkGray);

            EndDrawing();
        }

        UnloadModel(model);
        UnloadTexture(texture);

        CloseWindow();
    }
}
