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
    internal float Pan { get; set; } = 0.5f;
    internal AudioCallback? Callback { get; set; }
}

/// <summary>Fills a buffer with an audio stream's next samples, interleaved, from -1 to 1.</summary>
public delegate void AudioCallback(Span<float> samples);

public static partial class Engine3D
{
    private static int _audioStreamBufferFrames = 4096;

    // The streams fed by a callback, which each frame's end tops up.
    private static readonly List<AudioStream> CallbackStreams = [];

    /// <summary>Makes a stream that plays samples the program gives it, at a rate and with one or two channels.</summary>
    /// <remarks>
    /// <paramref name="sampleSize"/> is raylib's bits a sample, kept so its programs read the same.
    /// Samples are given as floats or as 16-bit integers whatever it says.
    /// </remarks>
    public static AudioStream LoadAudioStream(int sampleRate, int sampleSize, int channels) =>
        new(Math.Max(1, sampleRate), Math.Clamp(channels, 1, 2), _audioStreamBufferFrames);

    /// <summary>Whether a stream can be played, which it can until it is unloaded.</summary>
    public static bool IsAudioStreamValid(AudioStream stream) => !stream.Unloaded;

    /// <summary>Stops a stream and lets its voice go.</summary>
    public static void UnloadAudioStream(AudioStream stream)
    {
        StopAudioStream(stream);
        stream.Unloaded = true;
        CallbackStreams.Remove(stream);
    }

    /// <summary>Queues samples after those a stream has yet to play, interleaved, from -1 to 1.</summary>
    public static void UpdateAudioStream(AudioStream stream, ReadOnlySpan<float> samples)
    {
        if (StreamVoice(stream) is { } voice && Audio() is { } audio) audio.QueueSamples(voice, samples);
    }

    /// <summary>Queues 16-bit samples after those a stream has yet to play, interleaved.</summary>
    public static void UpdateAudioStream(AudioStream stream, ReadOnlySpan<short> samples)
    {
        var floats = new float[samples.Length];
        for (int i = 0; i < floats.Length; i++) floats[i] = samples[i] / 32768f;
        UpdateAudioStream(stream, floats);
    }

    /// <summary>Whether a stream has played enough of what it was given to take more, as a loop feeding it asks each frame.</summary>
    public static bool IsAudioStreamProcessed(AudioStream stream) =>
        !stream.Unloaded && Audio() is { } audio && (!stream.Voice.IsValid || audio.QueuedFrames(stream.Voice) < stream.BufferFrames);

    /// <summary>Plays a stream, from what it has been given.</summary>
    public static void PlayAudioStream(AudioStream stream)
    {
        if (StreamVoice(stream) is not { } voice) return;
        voice.SetPaused(false);
        stream.Playing = true;
    }

    /// <summary>Pauses a stream, keeping what it has yet to play.</summary>
    public static void PauseAudioStream(AudioStream stream)
    {
        stream.Voice.SetPaused(true);
        stream.Playing = false;
    }

    /// <summary>Resumes a paused stream.</summary>
    public static void ResumeAudioStream(AudioStream stream) => PlayAudioStream(stream);

    /// <summary>Stops a stream and drops what it had yet to play.</summary>
    public static void StopAudioStream(AudioStream stream)
    {
        stream.Voice.Stop();
        stream.Voice = default;
        stream.Playing = false;
    }

    /// <summary>Whether a stream is playing.</summary>
    public static bool IsAudioStreamPlaying(AudioStream stream) => stream.Playing && stream.Voice.IsValid;

    /// <summary>Sets a stream's volume, from 0 to 1.</summary>
    public static void SetAudioStreamVolume(AudioStream stream, float volume)
    {
        stream.Volume = Math.Clamp(volume, 0f, 1f);
        stream.Voice.SetVolume(stream.Volume);
    }

    /// <summary>Sets a stream's pitch as a speed, where 1 plays it at its sample rate.</summary>
    public static void SetAudioStreamPitch(AudioStream stream, float pitch)
    {
        stream.Pitch = Math.Max(0.01f, pitch);
        stream.Voice.SetPlaybackRate(stream.Pitch);
    }

    /// <summary>Sets a stream's balance, as raylib's 0 (left) to 1 (right) with 0.5 in the middle.</summary>
    public static void SetAudioStreamPan(AudioStream stream, float pan)
    {
        stream.Pan = Math.Clamp(pan, 0f, 1f);
        stream.Voice.SetPan(stream.Pan * 2 - 1);
    }

    /// <summary>How many frames a stream made after this keeps waiting to be heard before it asks for more, 4096 to begin with.</summary>
    /// <remarks>Fewer is quicker to answer a change and more is safer against a slow frame.</remarks>
    public static void SetAudioStreamBufferSizeDefault(int size) => _audioStreamBufferFrames = Math.Max(256, size);

    /// <summary>
    /// Feeds a stream from a callback, which the end of each frame calls for as many samples as
    /// keep the stream fed while it plays. Null stops the calls.
    /// </summary>
    /// <remarks>
    /// raylib calls it on the audio thread. Here it runs on the program's own thread at the end of
    /// <see cref="EndDrawing"/>, so it may read the game's state freely, and a frame slower than the
    /// stream's buffer is heard as a gap.
    /// </remarks>
    public static void SetAudioStreamCallback(AudioStream stream, AudioCallback? callback)
    {
        stream.Callback = callback;
        if (callback is null) CallbackStreams.Remove(stream);
        else if (!CallbackStreams.Contains(stream)) CallbackStreams.Add(stream);
    }

    // Tops up each playing stream that has a callback, a buffer's worth at a time.
    internal static void FeedAudioStreams()
    {
        if (CallbackStreams.Count == 0 || Audio() is not { } audio) return;
        foreach (var stream in CallbackStreams)
        {
            if (!stream.Playing || stream.Callback is not { } callback || !stream.Voice.IsValid) continue;
            var chunk = new float[stream.BufferFrames * stream.Channels];
            while (audio.QueuedFrames(stream.Voice) < stream.BufferFrames)
            {
                callback(chunk);
                audio.QueueSamples(stream.Voice, chunk);
            }
        }
    }

    // The stream's voice, made paused the first time it is needed, once the audio device is open.
    private static AudioSource? StreamVoice(AudioStream stream)
    {
        if (stream.Unloaded) return null;
        if (stream.Voice.IsValid) return stream.Voice;
        if (Audio() is not { } audio) return null;
        var voice = audio.PlayStream(stream.Channels, stream.SampleRate, new AudioVoiceParams
        {
            Volume = Math.Max(stream.Volume, 1e-6f),
            PlaybackRate = stream.Pitch,
            Pannable = true,
            Pan = stream.Pan * 2 - 1,
        });
        if (!voice.IsValid) return null;
        voice.SetPaused(!stream.Playing);
        stream.Voice = voice;
        return voice;
    }
}
