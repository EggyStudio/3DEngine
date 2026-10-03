namespace Engine;

/// <summary>
/// Wires the lighting subsystem: schedules <see cref="LightSpawnSystem"/> in
/// <see cref="Stage.PreUpdate"/> (after <c>SceneSpawnSystem</c>) so payloads attached by
/// <see cref="SceneSpawner"/> become first-class <see cref="Light"/> components on the
/// same frame, and registers <see cref="LightExtract"/> with the <see cref="Renderer"/>
/// so those lights are surfaced to the render world each frame.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order:</b> consumer-tier (<see cref="PluginOrder.Default"/>); needs
/// <see cref="ScenesPlugin"/> for the payload contract and the <see cref="Renderer"/>
/// resource (created by <see cref="SdlPlugin"/>) for extract-system registration. Both
/// will exist by the time this plugin builds when launched via <see cref="DefaultPlugins"/>.
/// </para>
/// <para>
/// <b>Headless / test path:</b> when no <see cref="Renderer"/> is present (e.g. unit
/// tests using a bare <see cref="App"/>), the extract registration is skipped with a
/// debug log and the spawn system still runs — so payload→component translation is
/// testable without a graphics backend, mirroring how <see cref="MaterialPlugin"/> works.
/// </para>
/// </remarks>
/// <seealso cref="Light"/>
/// <seealso cref="LightSpawnSystem"/>
/// <seealso cref="LightExtract"/>
public sealed class LightingPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Lighting");

    /// <inheritdoc />
    public void Build(App app)
    {
        // Spawn driver: turn SceneLightPayloads into Light + (optional) LightShadow /
        // LightShaping. PreUpdate matches SceneSpawnSystem; ordering inside the stage is
        // registration order, and ScenesPlugin (which registers SceneSpawnSystem) runs
        // earlier because of its lower Order, so this descriptor lands after it.
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(LightSpawnSystem.Run, "LightSpawnSystem"));
        Logger.Debug("LightingPlugin: LightSpawnSystem scheduled in Stage.PreUpdate.");

        // Renderer hookup is best-effort: the extract is harmless when the renderer is
        // absent (tests, headless runs). If it shows up later, a follow-up registration
        // would be needed — we don't currently watch for late insertion.
        if (app.World.TryGetResource<Renderer>(out var renderer))
        {
            renderer.AddExtractSystem(new LightExtract());
            // Pack RenderLights into a per-frame UBO and publish it as
            // FrameLightingBinding so material pipelines can wire it into their
            // descriptor sets at draw time.
            renderer.AddPrepareSystem(new LightingUboPrepare());
            Logger.Info("LightingPlugin: LightExtract + LightingUboPrepare registered with the Renderer.");
        }
        else
        {
            Logger.Debug("LightingPlugin: no Renderer resource present; LightExtract / LightingUboPrepare not registered (headless / test path).");
        }
    }
}