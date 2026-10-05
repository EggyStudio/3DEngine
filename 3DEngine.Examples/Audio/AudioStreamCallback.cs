// raylib's audio_stream_callback example, Copyright (c) 2026 Dan Hoang (@dan-hoang), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioStreamCallback
{
    private const int BUFFER_SIZE = 4096;
    private const int SAMPLE_RATE = 44100;

    private enum WaveType
    {
        Sine,
        Square,
        Triangle,
        Sawtooth,
    }

    private static int waveFrequency = 440;
    private static int newWaveFrequency = 440;
    private static int waveIndex = 0;

    // The last second of samples handed to the stream, part of which is drawn
    private static readonly float[] buffer = new float[SAMPLE_RATE];

    private static readonly AudioCallback[] waveCallbacks = [SineCallback, SquareCallback, TriangleCallback, SawtoothCallback];
    private static readonly string[] waveTypesAsString = ["sine", "square", "triangle", "sawtooth"];

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] stream callback");

        InitAudioDevice();

        // The stream holds BUFFER_SIZE samples at a time,
        SetAudioStreamBufferSizeDefault(BUFFER_SIZE);

        // 44100 a second, as 32 bit floats, in one channel.
        AudioStream stream = LoadAudioStream(SAMPLE_RATE, 32, 1);

        PlayAudioStream(stream);

        // The wave's callback is called whenever the stream needs samples.
        WaveType waveType = WaveType.Sine;
        SetAudioStreamCallback(stream, waveCallbacks[(int)waveType]);

        SetTargetFPS(30);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Up))
            {
                newWaveFrequency += 10;
                if (newWaveFrequency > 12500) newWaveFrequency = 12500;
            }

            if (IsKeyDown(Key.Down))
            {
                newWaveFrequency -= 10;
                if (newWaveFrequency < 20) newWaveFrequency = 20;
            }

            if (IsKeyPressed(Key.Left))
            {
                if (waveType == WaveType.Sine) waveType = WaveType.Sawtooth;
                else if (waveType == WaveType.Square) waveType = WaveType.Sine;
                else if (waveType == WaveType.Triangle) waveType = WaveType.Square;
                else waveType = WaveType.Triangle;

                SetAudioStreamCallback(stream, waveCallbacks[(int)waveType]);
            }

            if (IsKeyPressed(Key.Right))
            {
                if (waveType == WaveType.Sine) waveType = WaveType.Square;
                else if (waveType == WaveType.Square) waveType = WaveType.Triangle;
                else if (waveType == WaveType.Triangle) waveType = WaveType.Sawtooth;
                else waveType = WaveType.Sine;

                SetAudioStreamCallback(stream, waveCallbacks[(int)waveType]);
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText($"frequency: {newWaveFrequency}", screenWidth - 220, 10, 20, Color.Red);
                DrawText($"wave type: {waveTypesAsString[(int)waveType]}", screenWidth - 220, 30, 20, Color.Red);
                DrawText("Up/down to change frequency", 10, 10, 20, Color.DarkGray);
                DrawText("Left/right to change wave type", 10, 30, 20, Color.DarkGray);

                // The last 10 ms of samples. raylib's last line reads one past its buffer, which C
                // does without complaint, and here the index is held to the last sample.
                for (int i = 0; i < screenWidth; i++)
                {
                    Vector2 startPos = new((float)i, 250 - 50*buffer[SAMPLE_RATE - SAMPLE_RATE/100 + i*SAMPLE_RATE/100/screenWidth]);
                    Vector2 endPos = new((float)(i + 1), 250 - 50*buffer[Math.Min(SAMPLE_RATE - SAMPLE_RATE/100 + (i + 1)*SAMPLE_RATE/100/screenWidth, SAMPLE_RATE - 1)]);

                    DrawLineV(startPos, endPos, Color.Red);
                }

            EndDrawing();
        }

        UnloadAudioStream(stream);

        CloseAudioDevice();

        CloseWindow();
    }

    // Each callback makes its wave a sample at a time, taking a new frequency at the end of a
    // wavelength, and keeps the samples for drawing.
    private static void SineCallback(Span<float> framesOut) =>
        Synthesize(framesOut, (index, wavelength) => MathF.Sin(2*MathF.PI*index/wavelength));

    private static void SquareCallback(Span<float> framesOut) =>
        Synthesize(framesOut, (index, wavelength) => (index < wavelength/2)? 1.0f : -1.0f);

    private static void TriangleCallback(Span<float> framesOut) =>
        Synthesize(framesOut, (index, wavelength) => (index < wavelength/2)? (-1 + 2.0f*index/(wavelength/2)) : (1 - 2.0f*(index - wavelength/2)/(wavelength/2)));

    private static void SawtoothCallback(Span<float> framesOut) =>
        Synthesize(framesOut, (index, wavelength) => -1 + 2.0f*index/wavelength);

    private static void Synthesize(Span<float> framesOut, Func<int, int, float> wave)
    {
        int frameCount = framesOut.Length;
        int wavelength = SAMPLE_RATE/waveFrequency;

        for (int i = 0; i < frameCount; i++)
        {
            framesOut[i] = wave(waveIndex, wavelength);
            waveIndex++;

            if (waveIndex >= wavelength)
            {
                waveFrequency = newWaveFrequency;
                waveIndex = 0;
            }
        }

        // The samples kept for drawing
        for (int i = 0; i < SAMPLE_RATE - frameCount; i++) buffer[i] = buffer[i + frameCount];
        for (int i = 0; i < frameCount; i++) buffer[SAMPLE_RATE - frameCount + i] = framesOut[i];
    }
}
