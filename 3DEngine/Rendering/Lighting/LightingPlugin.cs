namespace Engine;

/// <summary>
/// Wires the lighting subsystem. It schedules <see cref="LightSpawnSystem"/> in
/// <see cref="Stage.PreUpdate"/> (after <c>SceneSpawnSystem</c>) so payloads attached by
/// <see cref="SceneSpawner"/> become first-class <see cref="Light"/> components on the
/// same frame, and registers <see cref="LightExtract"/> with the <see cref="Renderer"/>
/// so those lights are surfaced to the render world each frame.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order:</b> consumer-tier (<see cref="PluginOrder.Default"/>); needs
/// <see cref="ScenesPlugin"/> for the payload contract and the <see cref="Renderer"/>
/// resource (created by <see cref="RenderPlugin"/>) for extract-system registration. Both
/// will exist by the time this plugin builds when launched via <see cref="DefaultPlugins"/>.
/// </para>
/// <para>
/// With no <see cref="Renderer"/>, as in a test of a bare <see cref="App"/>, the extract is not
/// registered, which the debug log says, and the spawn system still runs, so turning payloads into
/// components is tested with no graphics device, as <see cref="MaterialPlugin"/> is.
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
        // Turns SceneLightPayloads into Lights. PreUpdate matches SceneSpawnSystem; ordering inside the stage is
        // registration order, and ScenesPlugin (which registers SceneSpawnSystem) runs
        // earlier because of its lower Order, so this descriptor lands after it.
        // It adds components, which the main thread does, as the scene systems it follows.
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(LightSpawnSystem.Run, "LightSpawnSystem").MainThreadOnly());
        Logger.Debug("LightingPlugin: LightSpawnSystem scheduled in Stage.PreUpdate.");

        // The extract is registered only when the renderer is there, as it is not in tests and
        // headless runs. A renderer inserted after this plugin is not watched for, and would need
        // the extract registered then.
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