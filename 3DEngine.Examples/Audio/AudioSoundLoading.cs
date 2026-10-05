// raylib's audio_sound_loading example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioSoundLoading
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] sound loading");

        InitAudioDevice();

        Sound fxWav = LoadSound("resources/sound.wav");
        Sound fxOgg = LoadSound("resources/target.ogg");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space)) PlaySound(fxWav);
            if (IsKeyPressed(Key.Return)) PlaySound(fxOgg);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("Press SPACE to PLAY the WAV sound!", 200, 180, 20, Color.LightGray);
                DrawText("Press ENTER to PLAY the OGG sound!", 200, 220, 20, Color.LightGray);

            EndDrawing();
        }

        UnloadSound(fxWav);
        UnloadSound(fxOgg);

        CloseAudioDevice();

        CloseWindow();
    }
}
