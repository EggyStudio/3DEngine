// raylib's core_screen_recording example, Copyright (c) 2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreScreenRecording
{
    private const int GIF_RECORD_FRAMERATE = 5;     // A frame taken every so many frames
    private const int MAX_SINEWAVE_POINTS = 256;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] screen recording");

        bool gifRecording = false;
        int gifFrameCounter = 0;
        GifWriter gifState = new();

        Vector2 circlePosition = new(0.0f, screenHeight/2.0f);
        float timeCounter = 0.0f;

        // A sine wave's points, worked out for 60 frames a second
        Vector2[] sinePoints = new Vector2[MAX_SINEWAVE_POINTS];
        for (int i = 0; i < MAX_SINEWAVE_POINTS; i++)
        {
            sinePoints[i].X = i*GetScreenWidth()/180.0f;
            sinePoints[i].Y = screenHeight/2.0f + 150*MathF.Sin((2*MathF.PI/1.5f)*(1.0f/60.0f)*i);
        }

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // The circle runs along the wave and starts again past the window's edge
            timeCounter += GetFrameTime();
            circlePosition.X += GetScreenWidth()/180.0f;
            circlePosition.Y = screenHeight/2.0f + 150*MathF.Sin((2*MathF.PI/1.5f)*timeCounter);

            if (circlePosition.X > screenWidth)
            {
                circlePosition.X = 0.0f;
                circlePosition.Y = screenHeight/2.0f;
                timeCounter = 0.0f;
            }

            // Ctrl+R starts recording and stops it, writing screenrecording.gif beside the program
            if (IsKeyDown(Key.LeftControl) && IsKeyPressed(Key.R))
            {
                if (gifRecording)
                {
                    gifRecording = false;

                    byte[] result = gifState.End();
                    SaveFileData($"{GetApplicationDirectory()}/screenrecording.gif", result);

                    TraceLog(LogLevel.Info, "Finish animated GIF recording");
                }
                else
                {
                    gifRecording = true;
                    gifFrameCounter = 0;

                    gifState.Begin(GetRenderWidth(), GetRenderHeight());

                    TraceLog(LogLevel.Info, "Start animated GIF recording");
                }
            }

            if (gifRecording)
            {
                gifFrameCounter++;

                // The window as the last frame left it, every few frames
                if (gifFrameCounter > GIF_RECORD_FRAMERATE)
                {
                    Image imScreen = LoadImageFromScreen();

                    // raylib's frame time works out to 0 in whole centiseconds, which is kept
                    gifState.Frame(imScreen.Data, (int)((1.0f/60.0f)*GIF_RECORD_FRAMERATE)/10, 16, imScreen.Width*4);
                    gifFrameCounter = 0;

                    UnloadImage(imScreen);
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < (MAX_SINEWAVE_POINTS - 1); i++)
                {
                    DrawLineV(sinePoints[i], sinePoints[i + 1], Color.Maroon);
                    DrawCircleV(sinePoints[i], 3, Color.Maroon);
                }

                DrawCircleV(circlePosition, 30, Color.Red);

                DrawFPS(10, 10);

            EndDrawing();
        }

        // A recording still going when the window closes is let go unwritten
        if (gifRecording)
        {
            gifState.End();
            gifRecording = false;
        }

        CloseWindow();
    }
}
