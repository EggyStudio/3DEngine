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
/// <b>Module split (matches <c>Engine.Textures</c>):</b>
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>Engine.Sound</c> (this module) - format-agnostic <see cref="Sound"/> asset,
///     <see cref="ISoundDecoder"/>, registry, loader, the <see cref="AudioServer"/>
///     resource, and a built-in WAV decoder. No native deps.
///   </description></item>
///   <item><description>
///     <c>Engine.Sound.Sdl</c> - SDL3 playback backend
///     (<see cref="IAudioBackend"/>); pulled in here automatically when present.
///   </description></item>
///   <item><description>
///     <c>Engine.Sound.SteamAudio</c> - Steam Audio spatial post-processor
///     (<see cref="ISpatialAudioProcessor"/>); pulled in here automatically when present.
///   </description></item>
/// </list>
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
public sealed class SoundsPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Sound");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("SoundsPlugin: Registering sound model (backend-agnostic)...");

        var registry = new SoundDecoderRegistry();
        registry.RegisterDecoder(new WavSoundDecoder());
        registry.RegisterDecoder(new OggSoundDecoder());
        app.World.InsertResource(registry);

        // Pre-create Assets<Sound> so handle-based PlaySound calls don't race the first
        // load-drain frame (AudioServer.ResolvePending tolerates a missing resource via
        // AudioUpdateSystem's TryGetResource gate, but resource-as-required reads from
        // ctx.PlaySound("...") would otherwise throw on the very first call).
        app.World.InsertResource(new Assets<Sound>());

        // Audio server (NullAudioBackend until a real backend plugin swaps it in).
        var audio = new AudioServer();
        app.World.InsertResource(audio);

        // Bring up the optional native backends. Each is best-effort: missing
        // assemblies / native libs just leave the NullAudioBackend in place.
        TryAddOptionalPlugin(app, "Engine.SdlAudioPlugin");

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
            Logger.Warn("SoundsPlugin: AssetServer not found - SoundAssetLoader was NOT registered. Add AssetPlugin first.");
        }

        // Per-frame systems.
        app.AddSystem(Stage.PreUpdate, AudioListenerSystem.Run);
        app.AddSystem(Stage.PostUpdate, AudioUpdateSystem.Run);

        Logger.Info("SoundsPlugin: Audio pipeline ready.");
    }

    /// <summary>
    /// Best-effort load-and-add of an optional backend plugin by full type name. The
    /// <c>Engine.Sound.Sdl</c> / <c>.SteamAudio</c> modules are co-compiled into the
    /// engine assembly via the <c>Modules\**</c> glob, so the type lookup is a simple
    /// reflection probe against the same assembly. If the symbol is absent (the module
    /// was excluded from the build), we skip silently - <see cref="NullAudioBackend"/>
    /// keeps audio API calls safe.
    /// </summary>
    private static void TryAddOptionalPlugin(App app, string typeName)
    {
        var type = typeof(SoundsPlugin).Assembly.GetType(typeName, throwOnError: false);
        if (type is null)
        {
            Logger.Debug($"SoundsPlugin: optional plugin '{typeName}' not present - skipping.");
            return;
        }
        try
        {
            if (Activator.CreateInstance(type) is IPlugin plugin)
            {
                app.AddPlugin(plugin);
                Logger.Info($"SoundsPlugin: optional plugin '{typeName}' added.");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"SoundsPlugin: failed to instantiate '{typeName}': {ex.Message}");
        }
    }
}