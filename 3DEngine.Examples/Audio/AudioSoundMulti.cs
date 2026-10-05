// raylib's audio_sound_multi example, Copyright (c) 2023-2025 Jeffery Myers (@JeffM2501), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioSoundMulti
{
    private const int MAX_SOUNDS = 10;

    private static readonly Sound[] soundArray = new Sound[MAX_SOUNDS];
    private static int currentSound;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] sound multi");

        InitAudioDevice();

        // The first slot holds the sound and its samples,
        soundArray[0] = LoadSound("resources/sound.wav");

        // and the rest aliases of it, which play the same samples without a copy of their own.
        for (int i = 1; i < MAX_SOUNDS; i++) soundArray[i] = LoadSoundAlias(soundArray[0]);

        currentSound = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space))
            {
                // Each press plays the next slot, so ten play over each other before the first is
                // played again. A slot not playing could be looked for instead.
                PlaySound(soundArray[currentSound]);
                currentSound++;

                if (currentSound >= MAX_SOUNDS) currentSound = 0;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("Press SPACE to PLAY a WAV sound!", 200, 180, 20, Color.LightGray);

            EndDrawing();
        }

        for (int i = 1; i < MAX_SOUNDS; i++) UnloadSoundAlias(soundArray[i]);
        UnloadSound(soundArray[0]);

        CloseAudioDevice();

        CloseWindow();
    }
}
