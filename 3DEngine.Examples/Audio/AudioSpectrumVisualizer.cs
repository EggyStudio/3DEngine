// raylib's audio_spectrum_visualizer example, Copyright (c) 2025 IANN (@meisei4), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class AudioSpectrumVisualizer
{
    private const int MONO = 1;
    private const int SAMPLE_RATE = 44100;
    private const float SAMPLE_RATE_F = 44100.0f;
    private const int FFT_WINDOW_SIZE = 1024;
    private const int BUFFER_SIZE = 512;
    private const int PER_SAMPLE_BIT_DEPTH = 16;
    private const int AUDIO_STREAM_RING_BUFFER_SIZE = FFT_WINDOW_SIZE*2;
    private const float EFFECTIVE_SAMPLE_RATE = SAMPLE_RATE_F*0.5f;
    private const double WINDOW_TIME = (double)FFT_WINDOW_SIZE/(double)EFFECTIVE_SAMPLE_RATE;
    private const float FFT_HISTORICAL_SMOOTHING_DUR = 2.0f;
    private const float MIN_DECIBELS = -100.0f;     // https://developer.mozilla.org/en-US/docs/Web/API/AnalyserNode/minDecibels
    private const float MAX_DECIBELS = -30.0f;      // https://developer.mozilla.org/en-US/docs/Web/API/AnalyserNode/maxDecibels
    private const float INVERSE_DECIBEL_RANGE = 1.0f/(MAX_DECIBELS - MIN_DECIBELS);
    private const float DB_TO_LINEAR_SCALE = 20.0f/2.302585092994046f;
    private const float SMOOTHING_TIME_CONSTANT = 0.8f; // https://developer.mozilla.org/en-US/docs/Web/API/AnalyserNode/smoothingTimeConstant
    private const int TEXTURE_HEIGHT = 1;
    private const int FFT_ROW = 0;
    private const float UNUSED_CHANNEL = 0.0f;

    private struct FFTComplex
    {
        public float real, imaginary;

        public FFTComplex(float real, float imaginary) => (this.real, this.imaginary) = (real, imaginary);
    }

    private sealed class FFTData
    {
        public required FFTComplex[] spectrum;
        public required FFTComplex[] workBuffer;
        public required float[] prevMagnitudes;
        public required float[][] fftHistory;
        public int fftHistoryLen;
        public int historyPos;
        public double lastFftTime;
        public float tapbackPos;
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[audio] spectrum visualizer");

        Image fftImage = GenImageColor(BUFFER_SIZE, TEXTURE_HEIGHT, Color.White);
        Texture2D fftTexture = LoadTextureFromImage(fftImage);
        RenderTexture2D bufferA = LoadRenderTexture(screenWidth, screenHeight);
        Vector2 iResolution = new((float)screenWidth, (float)screenHeight);

        // raylib's fft.fs, written in Slang
        Shader shader = LoadShader("resources/shaders/slang/fft.slang");

        int iResolutionLocation = GetShaderLocation(shader, "iResolution");
        int iChannel0Location = GetShaderLocation(shader, "iChannel0");
        SetShaderValue(shader, iResolutionLocation, iResolution);
        SetShaderValueTexture(shader, iChannel0Location, fftTexture);

        InitAudioDevice();
        SetAudioStreamBufferSizeDefault(AUDIO_STREAM_RING_BUFFER_SIZE);

        Wave wav = LoadWave("resources/country.mp3");
        WaveFormat(ref wav, SAMPLE_RATE, PER_SAMPLE_BIT_DEPTH, MONO);

        AudioStream audioStream = LoadAudioStream(SAMPLE_RATE, PER_SAMPLE_BIT_DEPTH, MONO);
        PlayAudioStream(audioStream);

        int fftHistoryLen = (int)MathF.Ceiling((float)(FFT_HISTORICAL_SMOOTHING_DUR/WINDOW_TIME)) + 1;

        FFTData fft = new()
        {
            spectrum = new FFTComplex[FFT_WINDOW_SIZE],
            workBuffer = new FFTComplex[FFT_WINDOW_SIZE],
            prevMagnitudes = new float[BUFFER_SIZE],
            fftHistory = [.. Enumerable.Range(0, fftHistoryLen).Select(_ => new float[BUFFER_SIZE])],
            fftHistoryLen = fftHistoryLen,
            historyPos = 0,
            lastFftTime = 0.0,
            tapbackPos = 0.01f,
        };

        uint wavCursor = 0;
        // The wave's samples in 16 bits, as WaveFormat leaves raylib's, a wave's being floats here
        short[] wavPCM16 = Array.ConvertAll(wav.Samples, s => (short)Math.Clamp(s*32767.0f, -32768.0f, 32767.0f));

        short[] chunkSamples = new short[AUDIO_STREAM_RING_BUFFER_SIZE];
        float[] audioSamples = new float[FFT_WINDOW_SIZE];

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            while (IsAudioStreamProcessed(audioStream))
            {
                for (int i = 0; i < AUDIO_STREAM_RING_BUFFER_SIZE; i++)
                {
                    int left = (wav.Channels == 2)? wavPCM16[wavCursor*2 + 0] : wavPCM16[wavCursor];
                    int right = (wav.Channels == 2)? wavPCM16[wavCursor*2 + 1] : left;
                    chunkSamples[i] = (short)((left + right)/2);

                    if (++wavCursor >= wav.FrameCount) wavCursor = 0;
                }

                UpdateAudioStream(audioStream, chunkSamples);

                for (int i = 0; i < FFT_WINDOW_SIZE; i++) audioSamples[i] = (chunkSamples[i*2] + chunkSamples[i*2 + 1])*0.5f/32767.0f;
            }

            CaptureFrame(fft, audioSamples);
            RenderFrame(fft, ref fftImage);
            UpdateTexture(fftTexture, fftImage);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // The render texture is blank and stands for the window, so its negative height
                // turns the shader's coordinates as raylib's does, the bars rising from the bottom
                BeginShaderMode(shader);
                    SetShaderValueTexture(shader, iChannel0Location, fftTexture);
                    DrawTextureRec(bufferA.Texture,
                        new Rectangle(0, 0, (float)screenWidth, (float)-screenHeight),
                        new Vector2(0, 0), Color.White);
                EndShaderMode();

            EndDrawing();
        }

        UnloadShader(shader);
        UnloadRenderTexture(bufferA);
        UnloadTexture(fftTexture);
        UnloadImage(fftImage);
        UnloadAudioStream(audioStream);
        UnloadWave(wav);
        CloseAudioDevice();

        CloseWindow();
    }

    // Cooley-Tukey FFT https://en.wikipedia.org/wiki/Cooley%E2%80%93Tukey_FFT_algorithm#Data_reordering,_bit_reversal,_and_in-place_algorithms
    private static void CooleyTukeyFFTSlow(FFTComplex[] spectrum, int n)
    {
        int j = 0;
        for (int i = 1; i < n - 1; i++)
        {
            int bit = n >> 1;
            while (j >= bit)
            {
                j -= bit;
                bit >>= 1;
            }
            j += bit;
            if (i < j)
            {
                (spectrum[i], spectrum[j]) = (spectrum[j], spectrum[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            float angle = -2.0f*MathF.PI/len;
            FFTComplex twiddleUnit = new(MathF.Cos(angle), MathF.Sin(angle));
            for (int i = 0; i < n; i += len)
            {
                FFTComplex twiddleCurrent = new(1.0f, 0.0f);
                for (int k = 0; k < len/2; k++)
                {
                    FFTComplex even = spectrum[i + k];
                    FFTComplex odd = spectrum[i + k + len/2];
                    FFTComplex twiddledOdd = new(
                        odd.real*twiddleCurrent.real - odd.imaginary*twiddleCurrent.imaginary,
                        odd.real*twiddleCurrent.imaginary + odd.imaginary*twiddleCurrent.real);

                    spectrum[i + k].real = even.real + twiddledOdd.real;
                    spectrum[i + k].imaginary = even.imaginary + twiddledOdd.imaginary;
                    spectrum[i + k + len/2].real = even.real - twiddledOdd.real;
                    spectrum[i + k + len/2].imaginary = even.imaginary - twiddledOdd.imaginary;

                    float twiddleRealNext = twiddleCurrent.real*twiddleUnit.real - twiddleCurrent.imaginary*twiddleUnit.imaginary;
                    twiddleCurrent.imaginary = twiddleCurrent.real*twiddleUnit.imaginary + twiddleCurrent.imaginary*twiddleUnit.real;
                    twiddleCurrent.real = twiddleRealNext;
                }
            }
        }
    }

    private static void CaptureFrame(FFTData fftData, float[] audioSamples)
    {
        for (int i = 0; i < FFT_WINDOW_SIZE; i++)
        {
            float x = (2.0f*MathF.PI*i)/(FFT_WINDOW_SIZE - 1.0f);
            float blackmanWeight = 0.42f - 0.5f*MathF.Cos(x) + 0.08f*MathF.Cos(2.0f*x); // https://en.wikipedia.org/wiki/Window_function#Blackman_window
            fftData.workBuffer[i].real = audioSamples[i]*blackmanWeight;
            fftData.workBuffer[i].imaginary = 0.0f;
        }

        CooleyTukeyFFTSlow(fftData.workBuffer, FFT_WINDOW_SIZE);
        Array.Copy(fftData.workBuffer, fftData.spectrum, FFT_WINDOW_SIZE);

        float[] smoothedSpectrum = new float[BUFFER_SIZE];

        for (int bin = 0; bin < BUFFER_SIZE; bin++)
        {
            float re = fftData.workBuffer[bin].real;
            float im = fftData.workBuffer[bin].imaginary;
            float linearMagnitude = MathF.Sqrt(re*re + im*im)/FFT_WINDOW_SIZE;

            float smoothedMagnitude = SMOOTHING_TIME_CONSTANT*fftData.prevMagnitudes[bin] + (1.0f - SMOOTHING_TIME_CONSTANT)*linearMagnitude;
            fftData.prevMagnitudes[bin] = smoothedMagnitude;

            float db = MathF.Log(MathF.Max(smoothedMagnitude, 1e-40f))*DB_TO_LINEAR_SCALE;
            float normalized = (db - MIN_DECIBELS)*INVERSE_DECIBEL_RANGE;
            smoothedSpectrum[bin] = Math.Clamp(normalized, 0.0f, 1.0f);
        }

        fftData.lastFftTime = GetTime();
        Array.Copy(smoothedSpectrum, fftData.fftHistory[fftData.historyPos], BUFFER_SIZE);
        fftData.historyPos = (fftData.historyPos + 1)%fftData.fftHistoryLen;
    }

    private static void RenderFrame(FFTData fftData, ref Image fftImage)
    {
        float framesSinceTapback = MathF.Floor((float)(fftData.tapbackPos/WINDOW_TIME));
        framesSinceTapback = Math.Clamp(framesSinceTapback, 0.0f, (float)(fftData.fftHistoryLen - 1));

        int historyPosition = (fftData.historyPos - 1 - (int)framesSinceTapback)%fftData.fftHistoryLen;
        if (historyPosition < 0) historyPosition += fftData.fftHistoryLen;

        float[] amplitude = fftData.fftHistory[historyPosition];
        for (int bin = 0; bin < BUFFER_SIZE; bin++) ImageDrawPixel(ref fftImage, bin, FFT_ROW, ColorFromNormalized(new Vector4(amplitude[bin], UNUSED_CHANNEL, UNUSED_CHANNEL, UNUSED_CHANNEL)));
    }
}
