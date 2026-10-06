using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TexturesRenderTarget
{
    public static void Run()
    {
        InitWindow(800, 450, "[textures] render target");

        // Lit from above over a little light from all around, where models are drawn unlit with none.
        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 0.65f);
        SetAmbientLight(Color.White, 0.35f);

        // A 3D scene drawn into an image each frame, then drawn three times as a texture.
        var target = LoadRenderTexture(320, 240);
        var camera = new Camera3D(new Vector3(5, 4, 5), Vector3.Zero, Vector3.UnitY, 45);
        var cube = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        var angle = 0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            angle += 45 * GetFrameTime();

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginTextureMode(target);
            ClearBackground(Color.DarkBlue);
            BeginMode3D(camera);
            DrawModelEx(cube, Vector3.Zero, Vector3.UnitY, angle, Vector3.One, Color.Orange);
            DrawGrid(10, 1);
            EndMode3D();
            DrawRectangleLines(0, 0, 320, 240, Color.Gold);
            EndTextureMode();

            DrawTexture(target.Texture, 20, 60, Color.White);
            DrawTextureEx(target.Texture, new Vector2(360, 60), 0, 0.5f, Color.White);
            DrawTexturePro(target.Texture, new Rectangle(0, 0, 320, 240), new Rectangle(620, 300, 160, 120),
                new Vector2(80, 60), -angle / 4, Color.SkyBlue);

            DrawText("One 3D scene drawn into a render texture, shown three times", 20, 20, 20, Color.DarkGray);
            EndDrawing();
        }

        UnloadModel(cube);
        UnloadRenderTexture(target);
        CloseWindow();
    }
}
