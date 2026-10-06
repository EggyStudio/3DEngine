// raylib's audio_stream_effects example, Copyright (c) 2022-2025 Ramon Santamaria (@raysan5), under
// the zlib license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioStreamEffects
{
    private static float[] delayBuffer = [];
    private static int delayBufferSize = 0;
    private static int delayReadIndex = 2;
    private static int delayWriteIndex = 0;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] stream effects");

        InitAudioDevice();              // Initialize audio device

        Music music = LoadMusicStream("resources/country.mp3");

        // Allocate buffer for the delay effect
        delayBufferSize = 48000*2;      // 1 second delay (device sampleRate*channels)
        delayBuffer = new float[delayBufferSize];

        PlayMusicStream(music);

        float timePlayed = 0.0f;        // Time played normalized [0.0f..1.0f]
        bool pause = false;             // Music playing paused

        bool enableEffectLPF = false;   // Enable effect low-pass-filter
        bool enableEffectDelay = false; // Enable effect delay (1 second)

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateMusicStream(music);   // Update music buffer with new stream data

            // Restart music playing (stop and play)
            if (IsKeyPressed(Key.Space))
            {
                StopMusicStream(music);
                PlayMusicStream(music);
            }

            // Pause/Resume music playing
            if (IsKeyPressed(Key.P))
            {
                pause = !pause;

                if (pause) PauseMusicStream(music);
                else ResumeMusicStream(music);
            }

            // Add/Remove effect: lowpass filter
            if (IsKeyPressed(Key.F))
            {
                enableEffectLPF = !enableEffectLPF;
                if (enableEffectLPF) AttachAudioStreamProcessor(music.Stream, AudioProcessEffectLPF);
                else DetachAudioStreamProcessor(music.Stream, AudioProcessEffectLPF);
            }

            // Add/Remove effect: delay
            if (IsKeyPressed(Key.D))
            {
                enableEffectDelay = !enableEffectDelay;
                if (enableEffectDelay) AttachAudioStreamProcessor(music.Stream, AudioProcessEffectDelay);
                else DetachAudioStreamProcessor(music.Stream, AudioProcessEffectDelay);
            }

            // Get normalized time played for current music stream
            timePlayed = GetMusicTimePlayed(music)/GetMusicTimeLength(music);

            if (timePlayed > 1.0f) timePlayed = 1.0f;   // Make sure time played is no longer than music

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("MUSIC SHOULD BE PLAYING!", 245, 150, 20, Color.LightGray);

                DrawRectangle(200, 180, 400, 12, Color.LightGray);
                DrawRectangle(200, 180, (int)(timePlayed*400.0f), 12, Color.Maroon);
                DrawRectangleLines(200, 180, 400, 12, Color.Gray);

                DrawText("PRESS SPACE TO RESTART MUSIC", 215, 230, 20, Color.LightGray);
                DrawText("PRESS P TO PAUSE/RESUME MUSIC", 208, 260, 20, Color.LightGray);

                DrawText($"PRESS F TO TOGGLE LPF EFFECT: {(enableEffectLPF? "ON" : "OFF")}", 200, 320, 20, Color.Gray);
                DrawText($"PRESS D TO TOGGLE DELAY EFFECT: {(enableEffectDelay? "ON" : "OFF")}", 180, 350, 20, Color.Gray);

            EndDrawing();
        }

        UnloadMusicStream(music);   // Unload music stream buffers from RAM

        CloseAudioDevice();         // Close audio device (music streaming is automatically stopped)

        CloseWindow();
    }

    // Audio effect: lowpass filter, over the music's samples, two channels interleaved
    private static readonly float[] low = [0.0f, 0.0f];
    private static void AudioProcessEffectLPF(Span<float> buffer)
    {
        const float cutoff = 70.0f/44100.0f; // 70 Hz lowpass filter
        const float k = cutoff/(cutoff + 0.1591549431f); // RC filter formula

        for (int i = 0; i + 1 < buffer.Length; i += 2)
        {
            float l = buffer[i];
            float r = buffer[i + 1];

            low[0] += k*(l - low[0]);
            low[1] += k*(r - low[1]);
            buffer[i] = low[0];
            buffer[i + 1] = low[1];
        }
    }

    // Audio effect: delay
    private static void AudioProcessEffectDelay(Span<float> buffer)
    {
        for (int i = 0; i + 1 < buffer.Length; i += 2)
        {
            float leftDelay = delayBuffer[delayReadIndex++];
            float rightDelay = delayBuffer[delayReadIndex++];

            if (delayReadIndex == delayBufferSize) delayReadIndex = 0;

            buffer[i] = 0.5f*buffer[i] + 0.5f*leftDelay;
            buffer[i + 1] = 0.5f*buffer[i + 1] + 0.5f*rightDelay;

            delayBuffer[delayWriteIndex++] = buffer[i];
            delayBuffer[delayWriteIndex++] = buffer[i + 1];
            if (delayWriteIndex == delayBufferSize) delayWriteIndex = 0;
        }
    }
}
