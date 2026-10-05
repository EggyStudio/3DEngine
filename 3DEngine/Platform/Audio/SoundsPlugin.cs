namespace Engine;

/// <summary>
/// Backend-agnostic sound plugin. Mirrors <see cref="TexturesPlugin"/>:
/// installs the <see cref="SoundDecoderRegistry"/>, registers the built-in
/// <see cref="WavSoundDecoder"/>, wires the shared <see cref="SoundAssetLoader"/>
/// with the <see cref="AssetServer"/>, inserts the <see cref="AudioServer"/>
/// (with a <see cref="NullAudioBackend"/> until a backend plugin replaces it),
/// and schedules the per-frame <see cref="AudioListenerSystem"/> +
/// <see cref="AudioUpdateSystem"/>.
/// </summary>
/// <remarks>
/// <para>
/// The sound model here is the same on every platform: the <see cref="Sound"/> asset, the
/// <see cref="ISoundDecoder"/> registry with its WAV, Ogg Vorbis, MP3 and FLAC decoders, the loader,
/// and the <see cref="AudioServer"/>. Playback is <see cref="SdlAudioPlugin"/>'s, which this plugin
/// adds, and which leaves the server silent rather than failing where there is no audio device.
/// </para>
/// <para>
/// <b>Wiring:</b> add <i>after</i> <see cref="AssetPlugin"/>;
/// <see cref="DefaultPlugins"/> brings this up automatically. The plugin re-syncs the
/// shared <see cref="SoundAssetLoader"/> with the <see cref="AssetServer"/> after
/// every backend has registered its decoders so the loader's
/// <see cref="SoundAssetLoader.Extensions"/> array reflects every supported format.
/// </para>
/// </remarks>
/// <seealso cref="ISoundDecoder"/>
/// <seealso cref="SoundDecoderRegistry"/>
/// <seealso cref="AudioServer"/>
internal sealed class SoundsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Sound");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("SoundsPlugin: Registering sound model (backend-agnostic)...");

        var registry = new SoundDecoderRegistry();
        registry.RegisterDecoder(new WavSoundDecoder());
        registry.RegisterDecoder(new OggSoundDecoder());
        registry.RegisterDecoder(new Mp3SoundDecoder());
        registry.RegisterDecoder(new FlacSoundDecoder());
        app.World.InsertResource(registry);

        // Pre-create Assets<Sound> so handle-based PlaySound calls don't race the first
        // load-drain frame (AudioServer.ResolvePending tolerates a missing resource via
        // AudioUpdateSystem's TryGetResource gate, but resource-as-required reads from
        // ctx.PlaySound("...") would otherwise throw on the very first call).
        app.World.InsertResource(new Assets<Sound>());

        // Audio server (NullAudioBackend until a real backend plugin swaps it in).
        var audio = new AudioServer();
        app.World.InsertResource(audio);

        // Playback through SDL3, named rather than looked up so trimming keeps it. Where the
        // device does not open, the backend stays silent and the server keeps working.
        app.AddPlugin(new SdlAudioPlugin());

        // After backends register their decoders, register one shared loader for all
        // accumulated extensions (mirrors TexturesPlugin's pattern).
        var loader = new SoundAssetLoader(registry);
        app.World.InsertResource(loader);

        if (app.World.TryGetResource<AssetServer>(out var server))
        {
            server.RegisterLoader(loader);
            Logger.Info(
                $"SoundsPlugin: SoundAssetLoader registered with AssetServer for {loader.Extensions.Length} extension(s): " +
                string.Join(", ", loader.Extensions));
        }
        else
        {
            Logger.Warn("SoundsPlugin: There is no AssetServer, so the SoundAssetLoader is not registered. AssetPlugin comes first.");
        }

        // Per-frame systems.
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(AudioListenerSystem.Run, "AudioListenerSystem.Run")
            .Read<EcsWorld>()
            .Write<AudioServer>());
        app.AddSystem(Stage.PostUpdate, new SystemDescriptor(AudioUpdateSystem.Run, "AudioUpdateSystem.Run")
            .Read<Assets<Sound>>()
            .Write<AudioServer>());

        Logger.Info("SoundsPlugin: Audio pipeline ready.");
    }
}
