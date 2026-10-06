// raylib's audio_module_playing example, Copyright (c) 2016-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioModulePlaying
{
    private const int MaxCircles = 64;

    private struct CircleWave
    {
        public Vector2 Position;
        public float Radius;
        public float Alpha;
        public float Speed;
        public Color Color;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        SetConfigFlags(ConfigFlags.Msaa4xHint);  // Try to enable MSAA 4X

        InitWindow(screenWidth, screenHeight, "[audio] module playing");

        InitAudioDevice();                  // Initialize audio device

        Color[] colors = [ Color.Orange, Color.Red, Color.Gold, Color.Lime, Color.Blue, Color.Violet, Color.Brown, Color.LightGray, Color.Pink,
                           Color.Yellow, Color.Green, Color.SkyBlue, Color.Purple, Color.Beige ];

        // Creates some circles for visual effect
        var circles = new CircleWave[MaxCircles];

        for (int i = MaxCircles - 1; i >= 0; i--)
        {
            circles[i].Alpha = 0.0f;
            circles[i].Radius = GetRandomValue(10, 40);
            circles[i].Position.X = GetRandomValue((int)circles[i].Radius, (int)(screenWidth - circles[i].Radius));
            circles[i].Position.Y = GetRandomValue((int)circles[i].Radius, (int)(screenHeight - circles[i].Radius));
            circles[i].Speed = GetRandomValue(1, 100)/2000.0f;
            circles[i].Color = colors[GetRandomValue(0, 13)];
        }

        Music music = LoadMusicStream("resources/mini1111.xm");
        music.Looping = false;
        float pitch = 1.0f;

        PlayMusicStream(music);

        float timePlayed = 0.0f;
        bool pause = false;

        SetTargetFPS(60);               // Set our game to run at 60 frames-per-second

        while (!WindowShouldClose())    // Detect window close button or ESC key
        {
            UpdateMusicStream(music);      // Update music buffer with new stream data

            // Restart music playing (stop and play)
            if (IsKeyPressed(Key.Space))
            {
                StopMusicStream(music);
                PlayMusicStream(music);
                pause = false;
            }

            // Pause/Resume music playing
            if (IsKeyPressed(Key.P))
            {
                pause = !pause;

                if (pause) PauseMusicStream(music);
                else ResumeMusicStream(music);
            }

            if (IsKeyDown(Key.Down)) pitch -= 0.01f;
            else if (IsKeyDown(Key.Up)) pitch += 0.01f;

            SetMusicPitch(music, pitch);

            // Get timePlayed scaled to bar dimensions
            timePlayed = GetMusicTimePlayed(music)/GetMusicTimeLength(music)*(screenWidth - 40);

            // Color circles animation
            for (int i = MaxCircles - 1; (i >= 0) && !pause; i--)
            {
                circles[i].Alpha += circles[i].Speed;
                circles[i].Radius += circles[i].Speed*10.0f;

                if (circles[i].Alpha > 1.0f) circles[i].Speed *= -1;

                if (circles[i].Alpha <= 0.0f)
                {
                    circles[i].Alpha = 0.0f;
                    circles[i].Radius = GetRandomValue(10, 40);
                    circles[i].Position.X = GetRandomValue((int)circles[i].Radius, (int)(screenWidth - circles[i].Radius));
                    circles[i].Position.Y = GetRandomValue((int)circles[i].Radius, (int)(screenHeight - circles[i].Radius));
                    circles[i].Color = colors[GetRandomValue(0, 13)];
                    circles[i].Speed = GetRandomValue(1, 100)/2000.0f;
                }
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = MaxCircles - 1; i >= 0; i--)
                {
                    DrawCircleV(circles[i].Position, circles[i].Radius, Fade(circles[i].Color, circles[i].Alpha));
                }

                // Draw time bar
                DrawRectangle(20, screenHeight - 20 - 12, screenWidth - 40, 12, Color.LightGray);
                DrawRectangle(20, screenHeight - 20 - 12, (int)timePlayed, 12, Color.Maroon);
                DrawRectangleLines(20, screenHeight - 20 - 12, screenWidth - 40, 12, Color.Gray);

                // Draw help instructions
                DrawRectangle(20, 20, 425, 145, Color.White);
                DrawRectangleLines(20, 20, 425, 145, Color.Gray);
                DrawText("PRESS SPACE TO RESTART MUSIC", 40, 40, 20, Color.Black);
                DrawText("PRESS P TO PAUSE/RESUME", 40, 70, 20, Color.Black);
                DrawText("PRESS UP/DOWN TO CHANGE SPEED", 40, 100, 20, Color.Black);
                DrawText($"SPEED: {pitch:F6}", 40, 130, 20, Color.Maroon);

            EndDrawing();
        }

        UnloadMusicStream(music);          // Unload music stream buffers from RAM

        CloseAudioDevice();     // Close audio device (music streaming is automatically stopped)

        CloseWindow();          // Close window and OpenGL context
    }
}
