// raylib's audio_mixed_processor example, Copyright (c) 2023-2025 hkc (@hatkidchan), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioMixedProcessor
{
    private static float exponent = 1.0f;                 // Audio exponentiation value
    private static readonly float[] averageVolume = new float[400];   // Average volume history

    // Audio processing function, on the audio thread, over the device's two channels
    private static void ProcessAudio(Span<float> samples)
    {
        int frames = samples.Length/2;
        float average = 0.0f;               // Temporary average volume

        for (int frame = 0; frame < frames; frame++)
        {
            ref float left = ref samples[frame*2 + 0];
            ref float right = ref samples[frame*2 + 1];

            left = MathF.Pow(MathF.Abs(left), exponent)*( (left < 0.0f)? -1.0f : 1.0f );
            right = MathF.Pow(MathF.Abs(right), exponent)*( (right < 0.0f)? -1.0f : 1.0f );

            average += MathF.Abs(left)/frames;   // accumulating average volume
            average += MathF.Abs(right)/frames;
        }

        // Moving history to the left
        for (int i = 0; i < 399; i++) averageVolume[i] = averageVolume[i + 1];

        averageVolume[399] = average;         // Adding last average value
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] mixed processor");

        InitAudioDevice();              // Initialize audio device

        AttachAudioMixedProcessor(ProcessAudio);

        Music music = LoadMusicStream("resources/country.mp3");
        // The engine's own coin.wav, which shares its name and place with raylib's
        Sound sound = LoadSound("resources/coin.wav");

        PlayMusicStream(music);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateMusicStream(music);   // Update music buffer with new stream data

            // Modify processing variables
            if (IsKeyPressed(Key.Left)) exponent -= 0.05f;
            if (IsKeyPressed(Key.Right)) exponent += 0.05f;

            if (exponent <= 0.5f) exponent = 0.5f;
            if (exponent >= 3.0f) exponent = 3.0f;

            if (IsKeyPressed(Key.Space)) PlaySound(sound);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("MUSIC SHOULD BE PLAYING!", 255, 150, 20, Color.LightGray);

                DrawText($"EXPONENT = {exponent:0.00}", 215, 180, 20, Color.LightGray);

                DrawRectangle(199, 199, 402, 34, Color.LightGray);
                for (int i = 0; i < 400; i++)
                {
                    DrawLine(201 + i, 232 - (int)(averageVolume[i]*32), 201 + i, 232, Color.Maroon);
                }
                DrawRectangleLines(199, 199, 402, 34, Color.Gray);

                DrawText("PRESS SPACE TO PLAY OTHER SOUND", 200, 250, 20, Color.LightGray);
                DrawText("USE LEFT AND RIGHT ARROWS TO ALTER DISTORTION", 140, 280, 20, Color.LightGray);

            EndDrawing();
        }

        UnloadMusicStream(music);   // Unload music stream buffers from RAM
        UnloadSound(sound);

        DetachAudioMixedProcessor(ProcessAudio);  // Disconnect audio processor

        CloseAudioDevice();         // Close audio device (music streaming is automatically stopped)

        CloseWindow();
    }
}
