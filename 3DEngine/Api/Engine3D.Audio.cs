using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// A piece of music: a decoded sound played on one looping voice that the program starts, pauses
/// and stops.
/// </summary>
/// <remarks>
/// raylib streams music from its file while it plays. This decodes the whole file when it loads,
/// so <see cref="Engine3D.UpdateMusicStream"/> does nothing and a long piece costs its full length
/// in memory. Streaming is not written.
/// </remarks>
public sealed class Music
{
    internal Music(Sound sound) => Sound = sound;

    internal Sound Sound { get; }

    internal AudioSource Voice { get; set; }

    internal float Volume { get; set; } = 1f;

    internal float Pitch { get; set; } = 1f;

    /// <summary>Whether the music starts again from the beginning when it ends. Defaults to true.</summary>
    public bool Looping { get; set; } = true;

    /// <summary>Whether the music has samples to play.</summary>
    public bool IsValid => Sound.Samples.Length > 0;
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
    }

    private static readonly ConditionalWeakTable<Sound, SoundState> SoundStates = new();
    private static bool _warnedNoAudio;

    // -- Audio device

    /// <summary>Opens the audio device. Sounds are silent until this is called.</summary>
    public static void InitAudioDevice()
    {
        if (!GetApp().HasPlugin<SoundsPlugin>())
            GetApp().AddPlugin(new SoundsPlugin());
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

    /// <summary>Loads a WAV or Ogg Vorbis file, decoded into memory.</summary>
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
            var bytes = File.ReadAllBytes(path);
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".wav" or ".wave" => WavSoundDecoder.Decode(bytes, fileName),
                ".ogg" => OggSoundDecoder.Decode(bytes, fileName),
                var other => throw new InvalidDataException($"'{other}' is not a sound format the engine reads (WAV, Ogg Vorbis)."),
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            ApiLogger.Warn($"LoadSound: '{fileName}' could not be decoded: {ex.Message}");
            return EmptySound(fileName);
        }
    }

    /// <summary>Whether a sound has samples to play.</summary>
    public static bool IsSoundValid(Sound sound) => sound.Samples.Length > 0;

    /// <summary>Stops a sound. Its samples are managed memory and are collected with it.</summary>
    public static void UnloadSound(Sound sound) => StopSound(sound);

    /// <summary>Plays a sound from its start, stopping it first if it was playing.</summary>
    public static void PlaySound(Sound sound)
    {
        if (!IsSoundValid(sound) || Audio() is not { } audio) return;
        var state = SoundStates.GetOrCreateValue(sound);
        state.Voice.Stop();
        state.Voice = audio.Play(sound, new AudioVoiceParams { Volume = Math.Max(state.Volume, 1e-6f) });
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

    // -- Music

    /// <summary>Loads a WAV or Ogg Vorbis file as music, decoded into memory.</summary>
    /// <returns>The music, or an empty one when the file cannot be read, with the reason in the log.</returns>
    public static Music LoadMusicStream(string fileName) => new(LoadSound(fileName));

    /// <summary>Stops a piece of music. Its samples are collected with it.</summary>
    public static void UnloadMusicStream(Music music) => StopMusicStream(music);

    /// <summary>Whether a piece of music has samples to play.</summary>
    public static bool IsMusicValid(Music music) => music.IsValid;

    /// <summary>Plays a piece of music from its start.</summary>
    public static void PlayMusicStream(Music music)
    {
        if (!music.IsValid || Audio() is not { } audio) return;
        music.Voice.Stop();
        music.Voice = audio.Play(music.Sound, new AudioVoiceParams { Volume = Math.Max(music.Volume, 1e-6f), Looping = music.Looping });
        if (music.Pitch != 1f) music.Voice.SetPlaybackRate(music.Pitch);
    }

    /// <summary>Does nothing, because music is decoded whole when it loads. Kept so raylib programs read the same.</summary>
    public static void UpdateMusicStream(Music music) { }

    /// <summary>Stops a piece of music.</summary>
    public static void StopMusicStream(Music music) => music.Voice.Stop();

    /// <summary>Pauses a piece of music.</summary>
    public static void PauseMusicStream(Music music) => music.Voice.SetPaused(true);

    /// <summary>Resumes a paused piece of music.</summary>
    public static void ResumeMusicStream(Music music) => music.Voice.SetPaused(false);

    /// <summary>Whether a piece of music is playing.</summary>
    public static bool IsMusicStreamPlaying(Music music) => music.Voice.IsValid && music.Voice.IsPlaying;

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

    /// <summary>A piece of music's length in seconds.</summary>
    public static float GetMusicTimeLength(Music music) => (float)music.Sound.DurationSeconds;

    private static AudioServer? Audio()
    {
        if (World.TryGetResource<AudioServer>(out var audio)) return audio;
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
