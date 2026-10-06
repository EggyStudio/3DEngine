namespace Engine;

/// <summary>
/// A voice the program feeds with samples of its own, as a synthesizer, a voice chat or a game that
/// makes its sound as it runs does, raylib's <c>AudioStream</c>.
/// </summary>
/// <remarks>
/// The program asks <see cref="Engine3D.IsAudioStreamProcessed"/> each frame and, when it answers
/// true, gives the next samples to <see cref="Engine3D.UpdateAudioStream(AudioStream, ReadOnlySpan{float})"/>,
/// or hands the stream a callback with <see cref="Engine3D.SetAudioStreamCallback"/>, which the
/// frame calls for as many samples as keep it fed. Samples are interleaved, a frame of one for each
/// channel, from -1 to 1.
/// </remarks>
public sealed class AudioStream
{
    internal AudioStream(int sampleRate, int channels, int bufferFrames)
    {
        SampleRate = sampleRate;
        Channels = channels;
        BufferFrames = bufferFrames;
    }

    /// <summary>The frames a second it plays at.</summary>
    public int SampleRate { get; }

    /// <summary>How many channels a frame has, 1 or 2.</summary>
    public int Channels { get; }

    // How many frames waiting to be heard count as enough, under which the stream asks for more.
    internal int BufferFrames { get; }

    internal AudioSource Voice { get; set; }
    internal bool Playing { get; set; }
    internal bool Unloaded { get; set; }
    internal float Volume { get; set; } = 1f;
    internal float Pitch { get; set; } = 1f;
    internal float Pan { get; set; }
    internal AudioCallback? Callback { get; set; }

    // What runs over each piece of samples the stream queues, in order, replaced as a whole.
    internal AudioCallback[] Processors { get; set; } = [];

    // Runs the processors over samples about to be queued, which they change in place.
    internal void Process(Span<float> samples)
    {
        foreach (var processor in Processors) processor(samples);
    }
}
