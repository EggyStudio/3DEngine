using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL3;

namespace Engine;

// How a sound's voice is fed to its stream, a block at a time as the device asks, through the
// voice's low-pass and reverb and panned as it goes.
internal sealed partial class SdlAudioBackend
{
    // SDL3-CS takes the callback as a delegate it marshals each call, so it is set here with a
    // function pointer, as the postmix callback is.
    [LibraryImport("SDL3", EntryPoint = "SDL_SetAudioStreamGetCallback")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static unsafe partial bool SetAudioStreamGetCallback(IntPtr stream, delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int, void> callback, IntPtr userdata);

    // The room every voice's reverb sounds.
    private readonly ReverbRoom _room = new();

    /// <inheritdoc />
    public void SetReverb(float size, float decay)
    {
        _room.Size = size;
        _room.Decay = decay;
    }

    /// <inheritdoc />
    public void SetVoiceLowPass(int voiceId, float cutoff)
    {
        lock (_lock)
            if (_voices.TryGetValue(voiceId, out var rec) && rec.Feed is { } feed) feed.Effects.Cutoff = cutoff;
    }

    /// <inheritdoc />
    public void SetVoiceReverb(int voiceId, float share)
    {
        lock (_lock)
            if (_voices.TryGetValue(voiceId, out var rec) && rec.Feed is { } feed) feed.Effects.Share = share;
    }

    /// <summary>
    /// What a sound's voice is fed from, its samples and how far through them it is, its effects
    /// and balance, and the block it hands its stream, read on SDL's audio thread with the stream
    /// locked, its settings written from the program's.
    /// </summary>
    private sealed class VoiceFeed(Sound sound, bool panned, VoiceEffects effects)
    {
        public readonly Sound Sound = sound;
        public readonly bool Panned = panned;
        public readonly VoiceEffects Effects = effects;
        public volatile bool Looping;
        public volatile float Pan;
        // Set once the samples and the room's tail are fed, after which the stream drains.
        public volatile bool Ended;
        public int Position;
        public int TailLeft = -1;
        public float[] Block = new float[2048];
        public GCHandle Handle;

        // Fills frames stereo frames of the block, from the samples, their tail or silence,
        // answering how many it filled, fewer only where the voice ended.
        public int Fill(int frames)
        {
            if (Block.Length < frames * 2) Block = new float[frames * 2];
            var samples = Sound.Samples;
            var channels = Sound.Channels;
            var length = samples.Length / channels;
            var (left, right) = Panned ? (MathF.Sqrt(0.5f * (1 - Pan)), MathF.Sqrt(0.5f * (1 + Pan))) : (1f, 1f);
            Effects.Begin();
            for (int i = 0; i < frames; i++)
            {
                float a = 0, b = 0;
                if (Position < length)
                {
                    var at = Position * channels;
                    a = samples[at];
                    b = channels > 1 ? samples[at + 1] : a;
                    if (++Position == length && Looping) Position = 0;
                }
                else if (TailLeft < 0) TailLeft = Effects.Reverberates ? Effects.TailFrames : 0;
                if (Position >= length && TailLeft == 0)
                {
                    Ended = true;
                    return i;
                }
                if (Position >= length) TailLeft--;
                var room = Effects.Run(ref a, ref b);
                Block[i * 2] = a * left + room;
                Block[i * 2 + 1] = b * right + room;
            }
            return frames;
        }
    }

    // Makes a sound's voice, one stereo stream at the sound's rate fed by the callback below,
    // which the device's own format is converted to as it plays, left for the caller to bind once
    // its gain and rate are set.
    private unsafe (IntPtr Stream, VoiceFeed? Feed) CreateFedStream(Sound sound, in AudioVoiceParams parameters)
    {
        var source = new SDL.AudioSpec { Format = SDL.AudioFormat.AudioF32LE, Channels = 2, Freq = sound.SampleRate };
        var stream = SDL.CreateAudioStream(in source, in _deviceSpec);
        if (stream == IntPtr.Zero)
        {
            Logger.Warn($"SdlAudioBackend: CreateAudioStream failed for '{sound.SourcePath}'. {SDL.GetError()}");
            return (IntPtr.Zero, null);
        }
        var effects = new VoiceEffects(sound.SampleRate, _room) { Cutoff = parameters.LowPass, Share = parameters.Reverb };
        var feed = new VoiceFeed(sound, parameters.Position is not null || parameters.Pannable, effects)
        {
            Looping = parameters.Looping,
            Pan = parameters.Pannable ? Math.Clamp(parameters.Pan, -1f, 1f) : 0f,
        };
        feed.Handle = GCHandle.Alloc(feed);
        if (!SetAudioStreamGetCallback(stream, &Feed, GCHandle.ToIntPtr(feed.Handle)))
        {
            Logger.Warn($"SdlAudioBackend: a stream for '{sound.SourcePath}' could not be fed. {SDL.GetError()}");
            SDL.DestroyAudioStream(stream);
            feed.Handle.Free();
            return (IntPtr.Zero, null);
        }
        return (stream, feed);
    }

    // Lets a fed voice's stream go, which SDL destroys with it locked, so no callback runs on it
    // once its handle is freed.
    private static void Release(IntPtr stream, VoiceFeed feed)
    {
        SDL.DestroyAudioStream(stream);
        if (feed.Handle.IsAllocated) feed.Handle.Free();
    }

    // Feeds a stream what it asks for, in blocks of up to two thousand frames, flushing it once the
    // voice ends so the frames its resampling holds back drain. An exception cannot cross back
    // into SDL, so the first is logged and the voice ends.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void Feed(IntPtr userdata, IntPtr stream, int additional, int total)
    {
        VoiceFeed? feed = null;
        try
        {
            feed = GCHandle.FromIntPtr(userdata).Target as VoiceFeed;
            if (feed is null || feed.Ended || additional <= 0) return;
            var frames = (additional + 7) / 8;
            while (frames > 0)
            {
                var filled = feed.Fill(Math.Min(frames, 2048));
                fixed (float* block = feed.Block)
                    if (filled > 0) SDL.PutAudioStreamData(stream, (IntPtr)block, filled * 2 * sizeof(float));
                if (feed.Ended)
                {
                    SDL.FlushAudioStream(stream);
                    return;
                }
                frames -= filled;
            }
        }
        catch (Exception ex)
        {
            if (feed is not null) feed.Ended = true;
            Logger.Warn($"SdlAudioBackend: feeding a voice threw {ex.GetType().Name}: {ex.Message}");
        }
    }
}
