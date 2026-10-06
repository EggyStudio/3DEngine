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
