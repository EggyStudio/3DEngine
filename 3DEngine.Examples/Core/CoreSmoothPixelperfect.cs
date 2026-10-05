// raylib's core_smooth_pixelperfect example, Copyright (c) 2021-2025 Giancamillo Alessandroni (@NotManyIdeasDev) and Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreSmoothPixelperfect
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        const int virtualScreenWidth = 160;
        const int virtualScreenHeight = 90;

        const float virtualRatio = (float)screenWidth/(float)virtualScreenWidth;

        InitWindow(screenWidth, screenHeight, "[core] smooth pixelperfect");

        Camera2D worldSpaceCamera = default;
        worldSpaceCamera.Zoom = 1.0f;

        Camera2D screenSpaceCamera = default;
        screenSpaceCamera.Zoom = 1.0f;

        RenderTexture2D target = LoadRenderTexture(virtualScreenWidth, virtualScreenHeight);

        Rectangle rec01 = new(70.0f, 35.0f, 20.0f, 20.0f);
        Rectangle rec02 = new(90.0f, 55.0f, 30.0f, 10.0f);
        Rectangle rec03 = new(80.0f, 65.0f, 15.0f, 25.0f);

        // A render texture is upright here, where raylib's is drawn with its height negative to turn it.
        Rectangle sourceRec = new(0.0f, 0.0f, (float)target.Texture.Width, (float)target.Texture.Height);
        Rectangle destRec = new((screenWidth - screenWidth/1.25f)/2.0f, (screenHeight - screenHeight/1.25f)/2.0f, screenWidth/1.25f, screenHeight/1.25f);

        Vector2 origin = new(0.0f, 0.0f);

        float rotation = 0.0f;

        float cameraX = 0.0f;
        float cameraY = 0.0f;

        bool smoothOn = true;
        bool overscan = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            rotation += 60.0f*GetFrameTime();

            cameraX = (MathF.Sin((float)GetTime())*50.0f) - 10.0f;
            cameraY = MathF.Cos((float)GetTime())*30.0f;

            screenSpaceCamera.Target = new Vector2(cameraX, cameraY);

            // The world is drawn at whole pixels of the small target, and what is left of the camera's
            // place moves the target on the screen, scaled up.
            var worldTarget = new Vector2(MathF.Truncate(screenSpaceCamera.Target.X), MathF.Truncate(screenSpaceCamera.Target.Y));
            worldSpaceCamera.Target = worldTarget;
            screenSpaceCamera.Target = (screenSpaceCamera.Target - worldTarget) * virtualRatio;

            if (IsKeyPressed(Key.S)) smoothOn = !smoothOn;
            if (IsKeyPressed(Key.O)) overscan = !overscan;

            if (overscan)
            {
                destRec = new Rectangle(-virtualRatio, -virtualRatio, screenWidth + (virtualRatio*2), screenHeight + (virtualRatio*2));
            }
            else
            {
                destRec = new Rectangle((screenWidth - screenWidth/1.25f)/2.0f, (screenHeight - screenHeight/1.25f)/2.0f, screenWidth/1.25f, screenHeight/1.25f);
            }

            BeginTextureMode(target);
                ClearBackground(Color.RayWhite);

                BeginMode2D(worldSpaceCamera);
                    DrawRectanglePro(rec01, origin, rotation, Color.Black);
                    DrawRectanglePro(rec02, origin, -rotation, Color.Red);
                    DrawRectanglePro(rec03, origin, rotation + 45.0f, Color.Blue);
                EndMode2D();
            EndTextureMode();

            BeginDrawing();
                ClearBackground(Color.LightGray);

                if (smoothOn)
                {
                    BeginMode2D(screenSpaceCamera);
                       DrawTexturePro(target.Texture, sourceRec, destRec, origin, 0.0f, Color.White);
                    EndMode2D();
                }
                else
                {
                    DrawTexturePro(target.Texture, sourceRec, destRec, origin, 0.0f, Color.White);
                }

                DrawText($"Screen resolution: {screenWidth}x{screenHeight}", 10, 10, 20, Color.DarkBlue);
                DrawText($"World resolution: {virtualScreenWidth}x{virtualScreenHeight}", 10, 40, 20, Color.DarkGreen);
                DrawText($"Smooth: {(smoothOn ? "ON" : "OFF")}", 10, screenHeight - 60, 20, Color.Red);
                DrawText($"Overscan: {(overscan ? "ON" : "OFF")}", 10, screenHeight - 30, 20, Color.Red);
                DrawFPS(GetScreenWidth() - 95, 10);
            EndDrawing();
        }

        UnloadRenderTexture(target);

        CloseWindow();
    }
}
