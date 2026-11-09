namespace Engine;

/// <summary>Reads a piece of music a piece at a time, for <c>Music</c>.</summary>
internal interface IMusicDecoder : IDisposable
{
    int Channels { get; }

    int SampleRate { get; }

    long TotalFrames { get; }

    /// <summary>Fills <paramref name="buffer"/> with interleaved samples from where the last read ended.</summary>
    /// <returns>How many samples were written, 0 at the end.</returns>
    int Read(Span<float> buffer);

    /// <summary>Moves to a frame, from which the next read starts.</summary>
    void Seek(long frame);
}
