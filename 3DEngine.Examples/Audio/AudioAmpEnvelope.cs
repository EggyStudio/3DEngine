// raylib's audio_amp_envelope example, Copyright (c) 2026 Arbinda Rizki Muhammad (@arbipink), under the
// zlib license, written again for the flat API.

using System.Numerics;
using ImGuiNET;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioAmpEnvelope
{
    private const int BUFFER_SIZE = 4096;
    private const int SAMPLE_RATE = 44100;

    private enum ADSRState
    {
        Idle,
        Attack,
        Decay,
        Sustain,
        Release,
    }

    // The envelope's settings and where it is
    private sealed class Envelope
    {
        public float attackTime;
        public float decayTime;
        public float sustainLevel;
        public float releaseTime;
        public float currentValue;
        public ADSRState state;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] amp envelope");

        // raygui's default style is light, and ImGui's light one keeps the labels readable on raylib's white.
        ImGui.StyleColorsLight();

        InitAudioDevice();

        // The stream holds BUFFER_SIZE samples at a time,
        SetAudioStreamBufferSizeDefault(BUFFER_SIZE);

        float[] buffer = new float[BUFFER_SIZE];

        // 44100 a second, as 32 bit floats, in one channel.
        AudioStream stream = LoadAudioStream(SAMPLE_RATE, 32, 1);

        // The wave's phase
        float audioTime = 0.0f;

        Envelope env = new()
        {
            attackTime = 1.0f,
            decayTime = 1.0f,
            sustainLevel = 0.5f,
            releaseTime = 1.0f,
            currentValue = 0.0f,
            state = ADSRState.Idle,
        };

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space)) env.state = ADSRState.Attack;

            if (IsKeyReleased(Key.Space) && (env.state != ADSRState.Idle)) env.state = ADSRState.Release;

            if (IsAudioStreamProcessed(stream))
            {
                if ((env.state != ADSRState.Idle) || (env.currentValue > 0.0f))
                {
                    for (int i = 0; i < BUFFER_SIZE; i++)
                    {
                        UpdateEnvelope(env);
                        FillAudioBuffer(i, buffer, env.currentValue, ref audioTime);
                    }
                }
                else
                {
                    // Silence, so the stream does not loop what it last had
                    Array.Clear(buffer);
                    audioTime = 0.0f;
                }

                UpdateAudioStream(stream, buffer);
            }

            if (!IsAudioStreamPlaying(stream)) PlayAudioStream(stream);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // raygui's sliders, as ImGui's, where raygui places them, each named on its left.
                ImGui.SetNextWindowPos(Vector2.Zero);
                ImGui.SetNextWindowSize(new Vector2(screenWidth, 230));
                ImGui.Begin("##controls", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
                Slider(60, "Attack (s)", ref env.attackTime, 0.1f, 3.0f, "%.2fs");
                Slider(100, "Decay (s)", ref env.decayTime, 0.1f, 3.0f, "%.2fs");
                Slider(140, "Sustain", ref env.sustainLevel, 0.0f, 1.0f, "%.2f");
                Slider(180, "Release (s)", ref env.releaseTime, 0.1f, 3.0f, "%.2fs");
                ImGui.End();

                DrawADSRGraph(env, new Rectangle(100, 250, 400, 100));

                DrawCircleV(new Vector2(520, 350 - (env.currentValue*100)), 5, Color.Maroon);
                DrawText($"Current Gain: {env.currentValue:0.00}", 535, (int)(345 - (env.currentValue*100)), 10, Color.Maroon);

                DrawText("Press SPACE to PLAY the sound!", 200, 400, 20, Color.LightGray);

            EndDrawing();
        }

        UnloadAudioStream(stream);

        CloseAudioDevice();

        CloseWindow();
    }

    // A slider at x 100, 400 wide, its name ending 5 pixels left of it, its value inside.
    private static void Slider(float y, string label, ref float value, float min, float max, string format)
    {
        ImGui.SetCursorScreenPos(new Vector2(95 - ImGui.CalcTextSize(label).X, y + 5));
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SetCursorScreenPos(new Vector2(100, y + 5));
        ImGui.SetNextItemWidth(400);
        ImGui.SliderFloat("##" + label, ref value, min, max, format);
    }

    // A sample of a 440 Hz tone at the envelope's gain
    private static void FillAudioBuffer(int i, float[] buffer, float envelopeValue, ref float audioTime)
    {
        int frequency = 440;
        buffer[i] = envelopeValue*MathF.Sin(2.0f*MathF.PI*frequency*audioTime);
        audioTime += (1.0f/SAMPLE_RATE);
    }

    // The envelope a sample on
    private static void UpdateEnvelope(Envelope env)
    {
        float sampleTime = 1.0f/SAMPLE_RATE;

        switch (env.state)
        {
            case ADSRState.Attack:
                env.currentValue += (1.0f/env.attackTime)*sampleTime;
                if (env.currentValue >= 1.0f)
                {
                    env.currentValue = 1.0f;
                    env.state = ADSRState.Decay;
                }
                break;

            case ADSRState.Decay:
                env.currentValue -= ((1.0f - env.sustainLevel)/env.decayTime)*sampleTime;
                if (env.currentValue <= env.sustainLevel)
                {
                    env.currentValue = env.sustainLevel;
                    env.state = ADSRState.Sustain;
                }
                break;

            case ADSRState.Sustain:
                env.currentValue = env.sustainLevel;
                break;

            case ADSRState.Release:
                env.currentValue -= (env.sustainLevel/env.releaseTime)*sampleTime;

                // A small floor, so the tail ends
                if (env.currentValue <= 0.001f)
                {
                    env.currentValue = 0.0f;
                    env.state = ADSRState.Idle;
                }
                break;

            default: break;
        }
    }

    private static void DrawADSRGraph(Envelope env, Rectangle bounds)
    {
        DrawRectangleRec(bounds, Fade(Color.LightGray, 0.3f));
        DrawRectangleLinesEx(bounds, 1, Color.Gray);

        // The sustain is a level rather than a time, so it is drawn a fixed width.
        float sustainWidth = 1.0f;

        // Attack, decay and release, with the sustain's width between
        float totalTime = env.attackTime + env.decayTime + sustainWidth + env.releaseTime;

        float scaleX = bounds.Width/totalTime;
        float scaleY = bounds.Height;

        Vector2 start = new(bounds.X, bounds.Y + bounds.Height);
        Vector2 peak = new(start.X + (env.attackTime*scaleX), bounds.Y);
        Vector2 sustain = new(peak.X + (env.decayTime*scaleX), bounds.Y + (1.0f - env.sustainLevel)*scaleY);
        Vector2 rel = new(sustain.X + (sustainWidth*scaleX), sustain.Y);
        Vector2 end = new(rel.X + (env.releaseTime*scaleX), bounds.Y + bounds.Height);

        DrawLineV(start, peak, Color.SkyBlue);
        DrawLineV(peak, sustain, Color.Blue);
        DrawLineV(sustain, rel, Color.DarkBlue);
        DrawLineV(rel, end, Color.Orange);

        DrawText("ADSR Visualizer", (int)bounds.X, (int)bounds.Y - 20, 10, Color.DarkGray);
    }
}
