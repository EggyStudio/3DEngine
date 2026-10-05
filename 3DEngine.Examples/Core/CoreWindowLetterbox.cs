// raylib's core_window_letterbox example, Copyright (c) 2019-2025 Anata (@anatagawa) and Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreWindowLetterbox
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.WindowResizable | ConfigFlags.VsyncHint);
        InitWindow(screenWidth, screenHeight, "[core] window letterbox");
        SetWindowMinSize(320, 240);

        int gameScreenWidth = 640;
        int gameScreenHeight = 480;

        RenderTexture2D target = LoadRenderTexture(gameScreenWidth, gameScreenHeight);
        SetTextureFilter(target.Texture, TextureFilter.Bilinear);

        Color[] colors = new Color[10];
        for (int i = 0; i < 10; i++) colors[i] = new Color((byte)(GetRandomValue(100, 250)), (byte)(GetRandomValue(50, 150)), (byte)(GetRandomValue(10, 100)), 255);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float scale = Math.Min((float)GetScreenWidth()/gameScreenWidth, (float)GetScreenHeight()/gameScreenHeight);

            if (IsKeyPressed(Key.Space))
            {
                for (int i = 0; i < 10; i++) colors[i] = new Color((byte)(GetRandomValue(100, 250)), (byte)(GetRandomValue(50, 150)), (byte)(GetRandomValue(10, 100)), 255);
            }

            Vector2 mouse = GetMousePosition();
            Vector2 virtualMouse = default;
            virtualMouse.X = (mouse.X - (GetScreenWidth() - (gameScreenWidth*scale))*0.5f)/scale;
            virtualMouse.Y = (mouse.Y - (GetScreenHeight() - (gameScreenHeight*scale))*0.5f)/scale;
            virtualMouse = Vector2.Clamp(virtualMouse, new Vector2(0, 0), new Vector2((float)gameScreenWidth, (float)gameScreenHeight));

            BeginTextureMode(target);
                ClearBackground(Color.RayWhite);

                for (int i = 0; i < 10; i++) DrawRectangle(0, (gameScreenHeight/10)*i, gameScreenWidth, gameScreenHeight/10, colors[i]);

                DrawText("If executed inside a window,\nyou can resize the window,\nand see the screen scaling!", 10, 25, 20, Color.White);
                DrawText($"Default Mouse: [{(int)mouse.X} , {(int)mouse.Y}]", 350, 25, 20, Color.Green);
                DrawText($"Virtual Mouse: [{(int)virtualMouse.X} , {(int)virtualMouse.Y}]", 350, 55, 20, Color.Yellow);
            EndTextureMode();

            BeginDrawing();
                ClearBackground(Color.Black);

                // A render texture is upright here, where raylib's is drawn with its height negative to turn it.
                DrawTexturePro(target.Texture, new Rectangle(0.0f, 0.0f, (float)target.Texture.Width, (float)target.Texture.Height),
                               new Rectangle((GetScreenWidth() - ((float)gameScreenWidth*scale))*0.5f, (GetScreenHeight() - ((float)gameScreenHeight*scale))*0.5f,
                               (float)gameScreenWidth*scale, (float)gameScreenHeight*scale), new Vector2(0, 0), 0.0f, Color.White);
            EndDrawing();
        }

        UnloadRenderTexture(target);

        CloseWindow();
    }
}
