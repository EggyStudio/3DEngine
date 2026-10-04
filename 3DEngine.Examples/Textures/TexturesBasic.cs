using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesBasic
{
    public static void Run()
    {
        InitWindow(800, 450, "[textures] basic");

        var logo = LoadTexture("resources/logo.png");
        var checker = LoadTextureFromImage(GenImageChecked(64, 64, 8, 8, Color.DarkGray, Color.LightGray));
        SetTextureFilter(checker, TextureFilter.Point);

        var camera = new Camera3D(new Vector3(4, 3, 4), new Vector3(0, 1, 0), Vector3.UnitY, 45);
        var rotation = 0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);
            rotation += 45 * GetFrameTime();

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            DrawGrid(10, 1);
            DrawBillboard(camera, logo, new Vector3(0, 1, 0), 2, Color.Maroon);
            EndMode3D();

            DrawTextureEx(checker, new Vector2(20, 60), 0, 2, Color.White);
            DrawTexturePro(logo, new Rectangle(0, 0, logo.Width, logo.Height), new Rectangle(690, 340, 128, 128),
                new Vector2(64, 64), rotation, Color.DarkBlue);
            DrawTexturePro(logo, new Rectangle(0, 0, logo.Width / 2f, logo.Height), new Rectangle(170, 60, 64, 128),
                Vector2.Zero, 0, Color.Orange);

            DrawText("A loaded PNG, a generated checkerboard and a billboard", 20, 20, 20, Color.DarkGray);
            EndDrawing();
        }

        UnloadTexture(logo);
        UnloadTexture(checker);
        CloseWindow();
    }
}
