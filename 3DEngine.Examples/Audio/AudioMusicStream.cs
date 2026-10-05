// raylib's audio_music_stream example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioMusicStream
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] music stream");

        InitAudioDevice();

        Music music = LoadMusicStream("resources/country.mp3");

        PlayMusicStream(music);

        float timePlayed = 0.0f;        // How much has played, from 0 to 1
        bool pause = false;

        float pan = 0.0f;               // From -1, left, to 1, right
        SetMusicPan(music, pan);

        float volume = 0.8f;
        SetMusicVolume(music, volume);

        SetTargetFPS(30);

        while (!WindowShouldClose())
        {
            UpdateMusicStream(music);

            // Space plays it from the start,
            if (IsKeyPressed(Key.Space))
            {
                StopMusicStream(music);
                PlayMusicStream(music);
            }

            // P pauses and resumes it,
            if (IsKeyPressed(Key.P))
            {
                pause = !pause;

                if (pause) PauseMusicStream(music);
                else ResumeMusicStream(music);
            }

            // the side arrows pan it,
            if (IsKeyDown(Key.Left))
            {
                pan -= 0.05f;
                if (pan < -1.0f) pan = -1.0f;
                SetMusicPan(music, pan);
            }
            else if (IsKeyDown(Key.Right))
            {
                pan += 0.05f;
                if (pan > 1.0f) pan = 1.0f;
                SetMusicPan(music, pan);
            }

            // and the others set its volume.
            if (IsKeyDown(Key.Down))
            {
                volume -= 0.05f;
                if (volume < 0.0f) volume = 0.0f;
                SetMusicVolume(music, volume);
            }
            else if (IsKeyDown(Key.Up))
            {
                volume += 0.05f;
                if (volume > 1.0f) volume = 1.0f;
                SetMusicVolume(music, volume);
            }

            timePlayed = GetMusicTimePlayed(music)/GetMusicTimeLength(music);

            if (timePlayed > 1.0f) timePlayed = 1.0f;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("MUSIC SHOULD BE PLAYING!", 255, 150, 20, Color.LightGray);

                DrawText("LEFT-RIGHT for PAN CONTROL", 320, 74, 10, Color.DarkBlue);
                DrawRectangle(300, 100, 200, 12, Color.LightGray);
                DrawRectangleLines(300, 100, 200, 12, Color.Gray);
                DrawRectangle((int)(300 + (pan + 1.0f)/2.0f*200 - 5), 92, 10, 28, Color.DarkGray);

                DrawRectangle(200, 200, 400, 12, Color.LightGray);
                DrawRectangle(200, 200, (int)(timePlayed*400.0f), 12, Color.Maroon);
                DrawRectangleLines(200, 200, 400, 12, Color.Gray);

                DrawText("PRESS SPACE TO RESTART MUSIC", 215, 250, 20, Color.LightGray);
                DrawText("PRESS P TO PAUSE/RESUME MUSIC", 208, 280, 20, Color.LightGray);

                DrawText("UP-DOWN for VOLUME CONTROL", 320, 334, 10, Color.DarkGreen);
                DrawRectangle(300, 360, 200, 12, Color.LightGray);
                DrawRectangleLines(300, 360, 200, 12, Color.Gray);
                DrawRectangle((int)(300 + volume*200 - 5), 352, 10, 28, Color.DarkGray);

            EndDrawing();
        }

        UnloadMusicStream(music);

        CloseAudioDevice();

        CloseWindow();
    }
}
