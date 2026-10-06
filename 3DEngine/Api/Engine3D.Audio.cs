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

/// <summary>Reads a piece of music a piece at a time, for <see cref="Music"/>.</summary>
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

/// <summary>Decodes Ogg Vorbis from its file, or bytes in memory, through NVorbis as it is read.</summary>
internal sealed class OggMusicDecoder(Stream stream) : IMusicDecoder
{
    private readonly NVorbis.VorbisReader _reader = new(stream, closeOnDispose: true);
    private float[] _scratch = [];

    public int Channels => _reader.Channels;

    public int SampleRate => _reader.SampleRate;

    public long TotalFrames => _reader.TotalSamples;

    public int Read(Span<float> buffer)
    {
        if (_scratch.Length < buffer.Length) _scratch = new float[buffer.Length];
        var read = _reader.ReadSamples(_scratch, 0, buffer.Length);
        _scratch.AsSpan(0, read).CopyTo(buffer);
        return read;
    }

    public void Seek(long frame) => _reader.SamplePosition = Math.Clamp(frame, 0, TotalFrames);

    public void Dispose() => _reader.Dispose();
}

/// <summary>Decodes FLAC from its file, or bytes in memory, through <see cref="FlacReader"/> as it is read.</summary>
internal sealed class FlacMusicDecoder(Stream stream) : IMusicDecoder
{
    private readonly FlacReader _reader = new(stream);

    public int Channels => _reader.Channels;

    public int SampleRate => _reader.SampleRate;

    public long TotalFrames => _reader.TotalFrames;

    public int Read(Span<float> buffer) => _reader.Read(buffer);

    public void Seek(long frame) => _reader.Seek(Math.Clamp(frame, 0, TotalFrames));

    public void Dispose() => _reader.Dispose();
}

/// <summary>Decodes MP3 from its file, or bytes in memory, through NLayer as it is read.</summary>
/// <remarks>NLayer measures its stream in bytes of 32-bit samples, which a frame holds one of for each channel.</remarks>
internal sealed class Mp3MusicDecoder : IMusicDecoder
{
    private readonly NLayer.MpegFile _file;
    private float[] _scratch = [];

    public Mp3MusicDecoder(Stream stream)
    {
        _file = new NLayer.MpegFile(stream);
        if (_file.SampleRate <= 0 || _file.Channels <= 0)
        {
            _file.Dispose();
            throw new InvalidDataException("no MPEG audio frames were found");
        }
    }

    public int Channels => _file.Channels;

    public int SampleRate => _file.SampleRate;

    public long TotalFrames => _file.Length / (sizeof(float) * _file.Channels);

    public int Read(Span<float> buffer)
    {
        if (_scratch.Length < buffer.Length) _scratch = new float[buffer.Length];
        var read = _file.ReadSamples(_scratch, 0, buffer.Length);
        _scratch.AsSpan(0, read).CopyTo(buffer);
        return read;
    }

    public void Seek(long frame) => _file.Position = Math.Clamp(frame, 0, TotalFrames) * sizeof(float) * _file.Channels;

    public void Dispose() => _file.Dispose();
}

/// <summary>
/// The music the flat API has open, a world resource, so the app shutting down closes the files of
/// any a program did not unload, and the log names them, as DESIGN.md §6 has it for what a program
/// forgets.
/// </summary>
internal sealed class LoadedMusic : IDisposable
{
    private static readonly ILogger Logger = Log.Category("Engine.Api");
    private readonly HashSet<Music> _open = [];

    public int Count => _open.Count;

    public void Add(Music music) => _open.Add(music);

    public void Remove(Music music) => _open.Remove(music);

    public void Dispose()
    {
        if (_open.Count == 0) return;
        Logger.Warn($"{_open.Count} music stream(s) were still loaded at shutdown, and their files are closed: {string.Join(", ", _open.Select(m => m.Name))}");
        foreach (var music in _open) music.Close();
        _open.Clear();
    }
}

public static partial class Engine3D
{
    // The voice each sound last started, with the volume and pitch set on it, so PlaySound can
    // restart a sound the way raylib does and StopSound can find what to stop. Keyed weakly, so a
    // sound the program drops does not stay alive through this table.
    private sealed class SoundState
    {
        public AudioSource Voice;
        public float Volume = 1f;
        public float Pitch = 1f;
        public float Pan = 0.5f;
    }

    private static readonly ConditionalWeakTable<Sound, SoundState> SoundStates = new();
    private static bool _warnedNoAudio;

    // -- Audio device

    /// <summary>Opens the audio device. Sounds are silent until this is called.</summary>
    public static void InitAudioDevice()
    {
        if (!GetApp().HasPlugin<SoundsPlugin>())
            GetApp().AddPlugin(new SoundsPlugin());
        if (MixedProcessors.Count > 0) Audio()?.SetMixedProcessors([.. MixedProcessors]);
    }

    /// <summary>Stops every sound and music the flat API started.</summary>
    public static void CloseAudioDevice()
    {
        foreach (var (_, state) in SoundStates)
            state.Voice.Stop();
    }

    /// <summary>Whether the audio device is open and has a backend that makes sound.</summary>
    public static bool IsAudioDeviceReady() =>
        _app?.World.TryGetResource<AudioServer>(out var audio) == true && audio.Backend is not NullAudioBackend;

    /// <summary>Sets the volume every sound is multiplied by, from 0 to 1.</summary>
    public static void SetMasterVolume(float volume)
    {
        if (Audio() is { } audio) audio.MasterVolume = Math.Clamp(volume, 0f, 1f);
    }

    /// <summary>The volume every sound is multiplied by.</summary>
    public static float GetMasterVolume() => Audio()?.MasterVolume ?? 0f;

    // -- Sounds

    /// <summary>Loads a WAV, Ogg Vorbis, MP3 or FLAC file, decoded into memory.</summary>
    /// <returns>The sound, or an empty one when the file cannot be read, with the reason in the log.</returns>
    public static Sound LoadSound(string fileName)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadSound: '{fileName}' was not found beside the program or in the working directory.");
            return EmptySound(fileName);
        }

        try
        {
            var sound = DecodeSound(File.ReadAllBytes(path), Path.GetExtension(path), fileName);
            if (!IsSoundValid(sound)) ApiLogger.Warn($"LoadSound: '{fileName}' decoded to no samples.");
            return sound;
        }
        // A decoder given bytes that are not its format throws what it meets, which a file of the
        // right name and the wrong bytes reaches, so any is caught and the frame goes on.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ApiLogger.Warn($"LoadSound: '{fileName}' could not be decoded: {ex.Message}");
            return EmptySound(fileName);
        }
    }

    // A file's bytes decoded by its extension, with or without the dot.
    private static Sound DecodeSound(byte[] bytes, string extension, string name) =>
        ("." + extension.TrimStart('.')).ToLowerInvariant() switch
        {
            ".wav" or ".wave" => WavSoundDecoder.Decode(bytes, name),
            ".ogg" => OggSoundDecoder.Decode(bytes, name),
            ".mp3" => Mp3SoundDecoder.Decode(bytes, name),
            ".flac" => FlacSoundDecoder.Decode(bytes, name),
            var other => throw new InvalidDataException($"'{other}' is not a sound format the engine reads (WAV, Ogg Vorbis, MP3, FLAC)."),
        };

    /// <summary>Whether a sound has samples to play.</summary>
    public static bool IsSoundValid(Sound sound) => sound.Samples.Length > 0;

    /// <summary>Stops a sound. Its samples are managed memory and are collected with it.</summary>
    public static void UnloadSound(Sound sound) => StopSound(sound);

    /// <summary>
    /// A second sound sharing <paramref name="source"/>'s samples and playing apart from it, with a
    /// volume, pitch and pan of its own, so one sound is heard over itself, as rapid shots are.
    /// </summary>
    /// <remarks>The samples are not copied, so an alias costs a handle, and the source may be unloaded first.</remarks>
    public static Sound LoadSoundAlias(Sound source) => new()
    {
        Samples = source.Samples,
        SampleRate = source.SampleRate,
        Channels = source.Channels,
        SourcePath = source.SourcePath,
        SourceFormat = source.SourceFormat,
    };

    /// <summary>Stops an alias, leaving the sound it shares samples with as it is.</summary>
    public static void UnloadSoundAlias(Sound alias) => StopSound(alias);

    /// <summary>Plays a sound from its start, stopping it first if it was playing.</summary>
    public static void PlaySound(Sound sound)
    {
        if (!IsSoundValid(sound) || Audio() is not { } audio) return;
        var state = SoundStates.GetOrCreateValue(sound);
        state.Voice.Stop();
        // Pannable, so SetSoundPan applies to a sound already playing, as raylib's does.
        state.Voice = audio.Play(sound, new AudioVoiceParams
        {
            Volume = Math.Max(state.Volume, 1e-6f),
            Pannable = true,
            Pan = state.Pan * 2 - 1,
        });
        if (state.Pitch != 1f) state.Voice.SetPlaybackRate(state.Pitch);
    }

    /// <summary>Stops a sound.</summary>
    public static void StopSound(Sound sound)
    {
        if (SoundStates.TryGetValue(sound, out var state)) state.Voice.Stop();
    }

    /// <summary>Pauses a playing sound.</summary>
    public static void PauseSound(Sound sound)
    {
        if (SoundStates.TryGetValue(sound, out var state)) state.Voice.SetPaused(true);
    }

    /// <summary>Resumes a paused sound.</summary>
    public static void ResumeSound(Sound sound)
    {
        if (SoundStates.TryGetValue(sound, out var state)) state.Voice.SetPaused(false);
    }

    /// <summary>Whether a sound is playing.</summary>
    public static bool IsSoundPlaying(Sound sound) =>
        SoundStates.TryGetValue(sound, out var state) && state.Voice.IsValid && state.Voice.IsPlaying;

    /// <summary>Sets a sound's volume, from 0 to 1, for this play and the next.</summary>
    public static void SetSoundVolume(Sound sound, float volume)
    {
        var state = SoundStates.GetOrCreateValue(sound);
        state.Volume = Math.Clamp(volume, 0f, 1f);
        state.Voice.SetVolume(state.Volume);
    }

    /// <summary>Sets a sound's pitch as a speed, where 1 is as recorded, for this play and the next.</summary>
    public static void SetSoundPitch(Sound sound, float pitch)
    {
        var state = SoundStates.GetOrCreateValue(sound);
        state.Pitch = Math.Max(0.01f, pitch);
        state.Voice.SetPlaybackRate(state.Pitch);
    }

    /// <summary>
    /// Sets a sound's balance, as raylib's 0 (left) to 1 (right) with 0.5 in the middle, for this
    /// play and the next.
    /// </summary>
    public static void SetSoundPan(Sound sound, float pan)
    {
        var state = SoundStates.GetOrCreateValue(sound);
        state.Pan = Math.Clamp(pan, 0f, 1f);
        state.Voice.SetPan(state.Pan * 2 - 1);
    }

    // -- Music

    // How far ahead of what is heard the voice is kept fed, and the piece read at a time.
    private const float MusicBufferSeconds = 0.5f;
    private const int MusicChunkFrames = 4096;

    /// <summary>Opens a WAV, Ogg Vorbis, MP3 or FLAC file as music, which streams from the file as it plays.</summary>
    /// <returns>The music, or an empty one when the file cannot be read, with the reason in the log.</returns>
    public static Music LoadMusicStream(string fileName)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadMusicStream: '{fileName}' was not found beside the program or in the working directory.");
            return new Music(null);
        }

        try
        {
            var music = new Music(MusicDecoder(Path.GetExtension(path), new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16), fileName), fileName);
            if (!music.IsValid)
            {
                // Its decoder opened the file, as one of a file cut short does, so it is closed
                // here, where nothing will unload it.
                music.Close();
                ApiLogger.Warn($"LoadMusicStream: '{fileName}' holds no sound to stream.");
                return music;
            }
            World.GetOrInsertResource(() => new LoadedMusic()).Add(music);
            return music;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ApiLogger.Warn($"LoadMusicStream: '{fileName}' could not be opened: {ex.Message}");
            return new Music(null);
        }
    }

    /// <summary>
    /// Opens a music file already in memory, by its type, as <c>".ogg"</c>, streamed from the bytes
    /// as it plays as one from a file is, as a game whose music comes from a pack file needs.
    /// </summary>
    /// <returns>The music, or an empty one when the bytes cannot be read, with the reason in the log.</returns>
    public static Music LoadMusicStreamFromMemory(string fileType, byte[] data)
    {
        try
        {
            var name = "memory" + fileType;
            var music = new Music(MusicDecoder(fileType, new MemoryStream(data, writable: false), name), name);
            World.GetOrInsertResource(() => new LoadedMusic()).Add(music);
            return music;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            ApiLogger.Warn($"LoadMusicStreamFromMemory: the {fileType} bytes could not be opened: {ex.Message}");
            return new Music(null);
        }
    }

    // A decoder for a stream by its type, with or without the dot, which closes the stream when
    // it cannot read it, so a file is not left open behind a refusal.
    private static IMusicDecoder MusicDecoder(string extension, Stream stream, string name)
    {
        try
        {
            return ("." + extension.TrimStart('.')).ToLowerInvariant() switch
            {
                ".ogg" => new OggMusicDecoder(stream),
                ".wav" or ".wave" => new WavMusicDecoder(stream, name),
                ".mp3" => new Mp3MusicDecoder(stream),
                ".flac" => new FlacMusicDecoder(stream),
                var other => throw new InvalidDataException($"'{other}' is not a music format the engine reads (WAV, Ogg Vorbis, MP3, FLAC)."),
            };
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>Stops a piece of music and closes its file.</summary>
    /// <remarks>Music still loaded when the app shuts down is closed then, and the log names it.</remarks>
    public static void UnloadMusicStream(Music music)
    {
        StopMusicStream(music);
        music.Close();
        if (_app?.World.TryGetResource<LoadedMusic>(out var loaded) == true) loaded.Remove(music);
    }

    /// <summary>Whether a piece of music has samples to play.</summary>
    public static bool IsMusicValid(Music music) => music.IsValid;

    /// <summary>Plays a piece of music from its start.</summary>
    public static void PlayMusicStream(Music music) => StartMusicAt(music, 0);

    /// <summary>
    /// Feeds a playing piece of music its next samples from the file. Called every frame the
    /// music plays, as in raylib.
    /// </summary>
    public static void UpdateMusicStream(Music music)
    {
        if (music.Decoder is not { } decoder || !music.Voice.IsValid || music.Paused || music.Ended || Audio() is not { } audio) return;

        var wanted = (long)(decoder.SampleRate * MusicBufferSeconds);
        var buffer = new float[MusicChunkFrames * decoder.Channels];
        while (audio.QueuedFrames(music.Voice) < wanted)
        {
            var read = decoder.Read(buffer);
            if (read == 0)
            {
                // The end of the piece: round again when it loops, otherwise let what is queued
                // play out.
                if (!music.Looping)
                {
                    music.Ended = true;
                    return;
                }
                decoder.Seek(0);
                read = decoder.Read(buffer);
                if (read == 0) return;
            }
            music.Stream.Process(buffer.AsSpan(0, read));
            audio.QueueSamples(music.Voice, buffer.AsSpan(0, read));
            music.FramesQueued += read / decoder.Channels;
        }
    }

    /// <summary>Stops a piece of music. Playing it again starts from the beginning.</summary>
    public static void StopMusicStream(Music music)
    {
        music.Voice.Stop();
        music.Voice = default;
        music.Paused = false;
        music.Ended = false;
    }

    /// <summary>Pauses a piece of music where it is.</summary>
    public static void PauseMusicStream(Music music)
    {
        music.Paused = true;
        music.Voice.SetPaused(true);
    }

    /// <summary>Resumes a paused piece of music.</summary>
    public static void ResumeMusicStream(Music music)
    {
        music.Paused = false;
        music.Voice.SetPaused(false);
    }

    /// <summary>Moves a playing or paused piece of music to a time in seconds.</summary>
    public static void SeekMusicStream(Music music, float position)
    {
        if (music.Decoder is not { } decoder) return;
        var paused = music.Paused;
        StartMusicAt(music, (long)(Math.Max(0, position) * decoder.SampleRate));
        if (paused) PauseMusicStream(music);
    }

    /// <summary>Whether a piece of music is playing: started, not paused, and not run out.</summary>
    public static bool IsMusicStreamPlaying(Music music) =>
        music.Voice.IsValid && !music.Paused && !(music.Ended && Audio() is { } audio && audio.QueuedFrames(music.Voice) == 0);

    /// <summary>Sets a piece of music's volume, from 0 to 1.</summary>
    public static void SetMusicVolume(Music music, float volume)
    {
        music.Volume = Math.Clamp(volume, 0f, 1f);
        music.Voice.SetVolume(music.Volume);
    }

    /// <summary>Sets a piece of music's pitch as a speed, where 1 is as recorded.</summary>
    public static void SetMusicPitch(Music music, float pitch)
    {
        music.Pitch = Math.Max(0.01f, pitch);
        music.Voice.SetPlaybackRate(music.Pitch);
    }

    /// <summary>Sets a piece of music's balance, as raylib's 0 (left) to 1 (right) with 0.5 in the middle.</summary>
    public static void SetMusicPan(Music music, float pan)
    {
        music.Pan = Math.Clamp(pan, 0f, 1f);
        music.Voice.SetPan(music.Pan * 2 - 1);
    }

    /// <summary>A piece of music's length in seconds.</summary>
    public static float GetMusicTimeLength(Music music) =>
        music.Decoder is { SampleRate: > 0 } decoder ? (float)decoder.TotalFrames / decoder.SampleRate : 0f;

    /// <summary>How far into the piece the music heard is, in seconds.</summary>
    /// <remarks>Counts what has left the voice, so it trails what has been read from the file by what is queued.</remarks>
    public static float GetMusicTimePlayed(Music music)
    {
        if (music.Decoder is not { TotalFrames: > 0, SampleRate: > 0 } decoder || !music.Voice.IsValid || Audio() is not { } audio) return 0f;
        var heard = music.StartFrame + music.FramesQueued - audio.QueuedFrames(music.Voice);
        return (float)(Math.Max(0, heard) % decoder.TotalFrames) / decoder.SampleRate;
    }

    // Starts the voice afresh at a frame, which drops whatever an earlier voice had queued.
    private static void StartMusicAt(Music music, long frame)
    {
        if (music.Decoder is not { } decoder || !music.IsValid || Audio() is not { } audio) return;
        music.Voice.Stop();
        frame = Math.Clamp(frame, 0, decoder.TotalFrames - 1);
        decoder.Seek(frame);
        music.Voice = audio.PlayStream(decoder.Channels, decoder.SampleRate, new AudioVoiceParams
        {
            Volume = Math.Max(music.Volume, 1e-6f),
            PlaybackRate = music.Pitch,
            Pannable = true,
            Pan = music.Pan * 2 - 1,
        });
        (music.StartFrame, music.FramesQueued, music.Paused, music.Ended) = (frame, 0, false, false);
        UpdateMusicStream(music);
    }

    private static AudioServer? Audio()
    {
        if (TryRes<AudioServer>(out var audio)) return audio;
        if (!_warnedNoAudio)
        {
            _warnedNoAudio = true;
            ApiLogger.Warn("Sounds are silent until InitAudioDevice is called.");
        }
        return null;
    }

    private static Sound EmptySound(string fileName) =>
        new() { Samples = [], SampleRate = 44100, Channels = 1, SourcePath = fileName };
}
