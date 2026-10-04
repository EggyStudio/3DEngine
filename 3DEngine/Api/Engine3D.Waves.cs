namespace Engine;

/// <summary>
/// A sound's samples in memory, to read, cut, convert and write before playing them, raylib's
/// <c>Wave</c>. <see cref="Engine3D.LoadSoundFromWave"/> makes a sound of one.
/// </summary>
/// <remarks>Samples are floats from -1 to 1, interleaved, a frame of one for each channel, whatever the file held.</remarks>
public sealed class Wave
{
    /// <summary>The samples, interleaved.</summary>
    public float[] Samples { get; init; } = [];

    /// <summary>The frames a second it plays at.</summary>
    public int SampleRate { get; init; }

    /// <summary>How many channels a frame has.</summary>
    public int Channels { get; init; }

    /// <summary>How many frames it holds, a sample for each channel each.</summary>
    public int FrameCount => Channels > 0 ? Samples.Length / Channels : 0;
}

public static partial class Engine3D
{
    /// <summary>Reads a WAV, Ogg Vorbis, MP3 or FLAC file into a wave.</summary>
    /// <returns>The wave, or an empty one when the file cannot be read, with the reason in the log.</returns>
    public static Wave LoadWave(string fileName) => WaveOf(LoadSound(fileName));

    /// <summary>Reads a file already in memory into a wave, by its type, as <c>".ogg"</c>.</summary>
    /// <returns>The wave, or an empty one when the bytes cannot be read, with the reason in the log.</returns>
    public static Wave LoadWaveFromMemory(string fileType, byte[] fileData)
    {
        try
        {
            return WaveOf(DecodeSound(fileData, fileType, "memory" + fileType));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            ApiLogger.Warn($"LoadWaveFromMemory: the {fileType} bytes could not be decoded: {ex.Message}");
            return new Wave();
        }
    }

    /// <summary>Whether a wave has samples.</summary>
    public static bool IsWaveValid(Wave wave) => wave.Samples.Length > 0 && wave.SampleRate > 0 && wave.Channels > 0;

    /// <summary>Does nothing, because a wave is managed memory. Kept so raylib programs read the same.</summary>
    public static void UnloadWave(Wave wave) { }

    /// <summary>A sound of a wave's samples, which share its array, so the wave is left as it is afterward.</summary>
    public static Sound LoadSoundFromWave(Wave wave) => new()
    {
        Samples = wave.Samples,
        SampleRate = wave.SampleRate,
        Channels = wave.Channels,
        SourceFormat = "wave",
    };

    /// <summary>A wave of its own with a copy of another's samples.</summary>
    public static Wave WaveCopy(Wave wave) => new() { Samples = [.. wave.Samples], SampleRate = wave.SampleRate, Channels = wave.Channels };

    /// <summary>Keeps the frames of a wave from <paramref name="initFrame"/> up to, not including, <paramref name="finalFrame"/>.</summary>
    public static void WaveCrop(ref Wave wave, int initFrame, int finalFrame)
    {
        initFrame = Math.Clamp(initFrame, 0, wave.FrameCount);
        finalFrame = Math.Clamp(finalFrame, initFrame, wave.FrameCount);
        wave = new Wave
        {
            Samples = wave.Samples[(initFrame * wave.Channels)..(finalFrame * wave.Channels)],
            SampleRate = wave.SampleRate,
            Channels = wave.Channels,
        };
    }

    /// <summary>
    /// Converts a wave to another rate and number of channels, its samples resampled between
    /// neighbors and its channels averaged into one or copied into two.
    /// </summary>
    /// <remarks><paramref name="sampleSize"/> is raylib's bits a sample, kept so its programs read the same, since samples stay floats.</remarks>
    public static void WaveFormat(ref Wave wave, int sampleRate, int sampleSize, int channels)
    {
        if (!IsWaveValid(wave) || sampleRate <= 0 || channels <= 0) return;

        // Each frame of the source mixed to the channels asked for: averaged down to one, the
        // first spread across more, and channel by channel otherwise.
        var frames = wave.FrameCount;
        var mixed = new float[frames * channels];
        for (int f = 0; f < frames; f++)
        for (int c = 0; c < channels; c++)
        {
            float value;
            if (channels == 1 && wave.Channels > 1)
            {
                value = 0;
                for (int s = 0; s < wave.Channels; s++) value += wave.Samples[f * wave.Channels + s];
                value /= wave.Channels;
            }
            else value = wave.Samples[f * wave.Channels + Math.Min(c, wave.Channels - 1)];
            mixed[f * channels + c] = value;
        }

        // Then resampled, each new frame between the two source frames either side of its time.
        var outFrames = Math.Max(1, (int)((long)frames * sampleRate / wave.SampleRate));
        var resampled = new float[outFrames * channels];
        for (int f = 0; f < outFrames; f++)
        {
            var at = (double)f * wave.SampleRate / sampleRate;
            var first = Math.Min((int)at, frames - 1);
            var next = Math.Min(first + 1, frames - 1);
            var t = (float)(at - first);
            for (int c = 0; c < channels; c++)
                resampled[f * channels + c] = mixed[first * channels + c] * (1 - t) + mixed[next * channels + c] * t;
        }

        wave = new Wave { Samples = resampled, SampleRate = sampleRate, Channels = channels };
    }

    /// <summary>A copy of a wave's samples, interleaved, from -1 to 1.</summary>
    public static float[] LoadWaveSamples(Wave wave) => [.. wave.Samples];

    /// <summary>Does nothing, because the samples are managed memory. Kept so raylib programs read the same.</summary>
    public static void UnloadWaveSamples(float[] samples) { }

    /// <summary>Writes a wave to a WAV file of 16-bit samples, beside the program for a relative name.</summary>
    /// <returns>Whether the file was written, with the reason in the log when it was not.</returns>
    public static bool ExportWave(Wave wave, string fileName)
    {
        if (!IsWaveValid(wave)) return false;
        var path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
            using var file = new BinaryWriter(File.Create(path));
            var bytes = wave.Samples.Length * 2;
            file.Write("RIFF"u8);
            file.Write(36 + bytes);
            file.Write("WAVE"u8);
            file.Write("fmt "u8);
            file.Write(16);
            file.Write((short)1);
            file.Write((short)wave.Channels);
            file.Write(wave.SampleRate);
            file.Write(wave.SampleRate * wave.Channels * 2);
            file.Write((short)(wave.Channels * 2));
            file.Write((short)16);
            file.Write("data"u8);
            file.Write(bytes);
            foreach (var sample in wave.Samples) file.Write((short)Math.Clamp(MathF.Round(sample * 32767f), -32768, 32767));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"ExportWave: '{fileName}' could not be written: {ex.Message}");
            return false;
        }
    }

    private static Wave WaveOf(Sound sound) => new() { Samples = sound.Samples, SampleRate = sound.SampleRate, Channels = sound.Channels };
}
