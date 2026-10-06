namespace Engine;

/// <summary>Plays an XM or MOD module as music, through <see cref="TrackerPlayer"/>, in stereo at 48 kHz.</summary>
/// <remarks>
/// A module is read whole, being small, and played as it is read. Its length is how long it plays
/// before it comes back to a row it has played, as raylib measures it, an hour at most for a module
/// that never does, and music that loops starts it again from its first row at that point, as
/// raylib's does.
/// </remarks>
internal sealed class TrackerMusicDecoder : IMusicDecoder
{
    private const int Rate = 48000;

    private readonly TrackerPlayer _player;
    private long _frame;

    public TrackerMusicDecoder(TrackerModule module)
    {
        _player = new TrackerPlayer(module, Rate);
        TotalFrames = _player.CountFrames(Rate * 3600L);
    }

    /// <summary>Reads a module from a stream by its reader, closing the stream.</summary>
    public static TrackerMusicDecoder Open(Stream stream, Func<byte[], TrackerModule> read)
    {
        byte[] bytes;
        using (stream)
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            bytes = copy.ToArray();
        }
        return new TrackerMusicDecoder(read(bytes));
    }

    public int Channels => 2;

    public int SampleRate => Rate;

    public long TotalFrames { get; }

    public int Read(Span<float> buffer)
    {
        var frames = (int)Math.Min(buffer.Length / 2, TotalFrames - _frame);
        if (frames <= 0) return 0;
        _player.Render(buffer[..(frames * 2)]);
        _frame += frames;
        return frames * 2;
    }

    // A module is played from its start up to the frame, since where each note is depends on all
    // that came before.
    public void Seek(long frame)
    {
        frame = Math.Clamp(frame, 0, TotalFrames);
        _player.Reset();
        _player.Skip(frame);
        _frame = frame;
    }

    public void Dispose()
    {
    }
}
