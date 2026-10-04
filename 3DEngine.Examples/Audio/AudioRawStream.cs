using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioRawStream
{
    public static void Run()
    {
        InitWindow(800, 450, "[audio] raw stream");
        InitAudioDevice();

        // A sine wave made as it plays, its pitch following the mouse across the window.
        const int SampleRate = 44100;
        var stream = LoadAudioStream(SampleRate, 32, 1);
        var frequency = 440f;
        var phase = 0.0;
        // The last samples given, for the waveform drawn below.
        var shown = new float[800];
        SetAudioStreamCallback(stream, samples =>
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = 0.3f * MathF.Sin((float)phase);
                phase = (phase + 2 * Math.PI * frequency / SampleRate) % (2 * Math.PI);
            }
            samples[..Math.Min(samples.Length, shown.Length)].CopyTo(shown);
        });
        PlayAudioStream(stream);

        SetTargetFPS(60);
        while (!WindowShouldClose())
        {
            var mouse = GetMousePosition();
            if (IsMouseButtonDown(MouseButton.Left)) frequency = 40 + mouse.X / GetScreenWidth() * 960;
            if (IsKeyPressed(Key.Space))
            {
                if (IsAudioStreamPlaying(stream)) PauseAudioStream(stream);
                else ResumeAudioStream(stream);
            }

            BeginDrawing();
            ClearBackground(Color.RayWhite);
            DrawText($"A sine wave of {frequency:0} Hz. Hold the left button to change it.", 20, 20, 20, Color.DarkGray);
            DrawText(IsAudioStreamPlaying(stream) ? "Space pauses" : "Paused, space plays", 20, 50, 20, Color.Gray);

            // The wave as given to the stream, a sample a pixel.
            for (int x = 1; x < shown.Length; x++)
                DrawLineV(new Vector2(x - 1, 250 + shown[x - 1] * 300), new Vector2(x, 250 + shown[x] * 300), Color.Red);
            EndDrawing();
        }

        UnloadAudioStream(stream);
        CloseAudioDevice();
        CloseWindow();
    }
}
