namespace Engine;

/// <summary>
/// Plugin that brings up the SDL3 playback backend for the audio system. Replaces
/// the <see cref="AudioServer"/>'s default <see cref="NullAudioBackend"/> with a
/// <see cref="SdlAudioBackend"/>.
/// </summary>
/// <remarks>
/// <para>
/// Pulled in automatically by <see cref="SoundsPlugin"/> when this module is part of
/// the build (the parent plugin probes for the type by name); standalone consumers
/// can also add it directly.
/// </para>
/// <para>
/// <b>Failure mode:</b> if the SDL audio subsystem cannot be initialized (no audio
/// device, headless CI), the backend stays uninitialized, and <see cref="AudioServer"/>
/// still has a real <see cref="IAudioBackend"/> reference but every method becomes a
/// no-op (matches <see cref="NullAudioBackend"/> semantics). Audio is never a hard
/// dependency for headless / editor scenarios.
/// </para>
/// </remarks>
/// <seealso cref="SoundsPlugin"/>
/// <seealso cref="SdlAudioBackend"/>
internal sealed class SdlAudioPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Sound.Sdl");

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("SdlAudioPlugin: Initializing SDL3 audio backend...");

        if (!app.World.TryGetResource<AudioServer>(out var server))
        {
            Logger.Warn("SdlAudioPlugin: There is no AudioServer, which SoundsPlugin adds, so there is nothing to play through.");
            return;
        }

        var backend = new SdlAudioBackend();
        server.SetBackend(backend);

        Logger.Info(
            backend.IsInitialized
                ? "SdlAudioPlugin: SDL3 audio backend ready."
                : "SdlAudioPlugin: SDL3 audio backend installed but native init failed, so audio is silent.");
    }
}