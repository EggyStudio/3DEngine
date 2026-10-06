using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// A piece of music, streamed from its file a little at a time while it plays, on one voice that
/// the program starts, pauses, seeks and stops.
/// </summary>
/// <remarks>
/// As in raylib, <see cref="Engine3D.UpdateMusicStream"/> feeds the voice, so it is called
/// every frame the music plays, and music left without it falls silent after half a second. Ogg
/// Vorbis is decoded from the open file as it plays. A WAV file is read whole, since it is not
/// compressed and reading it in pieces would save nothing.
/// </remarks>
public sealed class Music
{
    internal Music(IMusicDecoder? decoder, string name = "")
    {
        Decoder = decoder;
        Name = name;
        Stream = new AudioStream(decoder?.SampleRate ?? 44100, decoder?.Channels ?? 2, 0);
    }

    /// <summary>
    /// The stream the music plays on, raylib's <c>music.stream</c>, to attach processors to with
    /// <see cref="Engine3D.AttachAudioStreamProcessor"/>.
    /// </summary>
    /// <remarks>The music itself is played with the music functions, which feed this stream's processors.</remarks>
    public AudioStream Stream { get; }

    internal IMusicDecoder? Decoder { get; }

    // The file it was opened from, for the log.
    internal string Name { get; }

    /// <summary>Whether its file has been closed, by unloading it or by the app shutting down.</summary>
    internal bool Closed { get; private set; }

    // Closes the file the music streams from, once.
    internal void Close()
    {
        if (Closed) return;
        Closed = true;
        Decoder?.Dispose();
    }

    internal AudioSource Voice { get; set; }

    internal float Volume { get; set; } = 1f;

    internal float Pitch { get; set; } = 1f;

    internal float Pan { get; set; } = 0.5f;

    internal bool Paused { get; set; }

    // Where in the piece the voice started, how many frames have been queued since, and whether
    // the decoder has run out of a piece that does not loop.
    internal long StartFrame { get; set; }

    internal long FramesQueued { get; set; }

    internal bool Ended { get; set; }

    /// <summary>Whether the music starts again from the beginning when it ends. Defaults to true.</summary>
    public bool Looping { get; set; } = true;

    /// <summary>Whether the music has samples to play and has not been unloaded.</summary>
    public bool IsValid => !Closed && Decoder is { TotalFrames: > 0 };
}
