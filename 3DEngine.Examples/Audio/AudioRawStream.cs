// raylib's audio_raw_stream example, Copyright (c) 2015-2026 Ramon Santamaria (@raysan5) and James
// Hofmann (@triplefox), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioRawStream
{
    private const int BUFFER_SIZE = 4096;
    private const int SAMPLE_RATE = 44100;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] raw stream");

        InitAudioDevice();

        // A stream keeps BUFFER_SIZE samples waiting to be heard at a time
        SetAudioStreamBufferSizeDefault(BUFFER_SIZE);
        float[] buffer = new float[BUFFER_SIZE];

        // A stream of one channel at 44100 samples a second, given as floats
        AudioStream stream = LoadAudioStream(SAMPLE_RATE, 32, 1);
        float pan = 0.0f;
        SetAudioStreamPan(stream, pan);
        PlayAudioStream(stream);

        int sineFrequency = 440;
        int newSineFrequency = 440;
        int sineIndex = 0;
        double sineStartTime = 0.0;

        SetTargetFPS(30);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Up))
            {
                newSineFrequency += 10;
                if (newSineFrequency > 12500) newSineFrequency = 12500;
            }

            if (IsKeyDown(Key.Down))
            {
                newSineFrequency -= 10;
                if (newSineFrequency < 20) newSineFrequency = 20;
            }

            if (IsKeyDown(Key.Left))
            {
                pan -= 0.01f;
                if (pan < -1.0f) pan = -1.0f;
                SetAudioStreamPan(stream, pan);
            }

            if (IsKeyDown(Key.Right))
            {
                pan += 0.01f;
                if (pan > 1.0f) pan = 1.0f;
                SetAudioStreamPan(stream, pan);
            }

            // More of the wave whenever the stream has played enough to take it, the frequency
            // changed only where a wave ends, so the sound never jumps
            if (IsAudioStreamProcessed(stream))
            {
                for (int i = 0; i < BUFFER_SIZE; i++)
                {
                    int wavelength = SAMPLE_RATE/sineFrequency;
                    buffer[i] = MathF.Sin(2*MathF.PI*sineIndex/wavelength);
                    sineIndex++;

                    if (sineIndex >= wavelength)
                    {
                        sineFrequency = newSineFrequency;
                        sineIndex = 0;
                        sineStartTime = GetTime();
                    }
                }

                UpdateAudioStream(stream, buffer);
            }

            BeginDrawing();
                ClearBackground(Color.RayWhite);

                DrawText($"sine frequency: {sineFrequency}", screenWidth - 220, 10, 20, Color.Red);
                DrawText($"pan: {pan:0.00}", screenWidth - 220, 30, 20, Color.Red);
                DrawText("Up/down to change frequency", 10, 10, 20, Color.DarkGray);
                DrawText("Left/right to pan", 10, 30, 20, Color.DarkGray);

                int windowStart = (int)((GetTime() - sineStartTime)*SAMPLE_RATE);
                int windowSize = SAMPLE_RATE/10;
                // Named apart from the loop's own, which C# will not let this one hide
                int shownWavelength = SAMPLE_RATE/sineFrequency;

                // A sine wave of the frequency the stream is being given
                for (int i = 0; i < screenWidth; i++)
                {
                    int t0 = windowStart + i*windowSize/screenWidth;
                    int t1 = windowStart + (i + 1)*windowSize/screenWidth;
                    Vector2 startPos = new(i, 250 + 50*MathF.Sin(2*MathF.PI*t0/shownWavelength));
                    Vector2 endPos = new(i + 1, 250 + 50*MathF.Sin(2*MathF.PI*t1/shownWavelength));
                    DrawLineV(startPos, endPos, Color.Red);
                }

            EndDrawing();
        }

        UnloadAudioStream(stream);
        CloseAudioDevice();

        CloseWindow();
    }
}
