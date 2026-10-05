// raylib's core_render_texture example, Copyright (c) 2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreRenderTexture
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] render texture");

        int renderTextureWidth = 300;
        int renderTextureHeight = 300;
        RenderTexture2D target = LoadRenderTexture(renderTextureWidth, renderTextureHeight);

        Vector2 ballPosition = new(renderTextureWidth/2.0f, renderTextureHeight/2.0f);
        Vector2 ballSpeed = new(5.0f, 4.0f);
        int ballRadius = 20;

        float rotation = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            ballPosition.X += ballSpeed.X;
            ballPosition.Y += ballSpeed.Y;

            if ((ballPosition.X >= (renderTextureWidth - ballRadius)) || (ballPosition.X <= ballRadius)) ballSpeed.X *= -1.0f;
            if ((ballPosition.Y >= (renderTextureHeight - ballRadius)) || (ballPosition.Y <= ballRadius)) ballSpeed.Y *= -1.0f;

            rotation += 0.5f;

            BeginTextureMode(target);

                ClearBackground(Color.SkyBlue);

                DrawRectangle(0, 0, 20, 20, Color.Red);
                DrawCircleV(ballPosition, (float)ballRadius, Color.Maroon);

            EndTextureMode();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // A render texture is upright here, where raylib's is drawn with its height negative to turn it.
                DrawTexturePro(target.Texture,
                    new Rectangle(0, 0, (float)target.Texture.Width, (float)target.Texture.Height),
                    new Rectangle(screenWidth/2.0f, screenHeight/2.0f, (float)target.Texture.Width, (float)target.Texture.Height),
                    new Vector2(target.Texture.Width/2.0f, target.Texture.Height/2.0f), rotation, Color.White);

                DrawText("DRAWING BOUNCING BALL INSIDE RENDER TEXTURE!", 10, screenHeight - 40, 20, Color.Black);

                DrawFPS(10, 10);

            EndDrawing();
        }

        UnloadRenderTexture(target);
        CloseWindow();
    }
}
