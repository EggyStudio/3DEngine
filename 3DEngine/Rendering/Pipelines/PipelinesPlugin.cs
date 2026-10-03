namespace Engine;

/// <summary>
/// Registers shader/pipeline asset loaders with the <see cref="AssetServer"/>. Currently
/// installs the <see cref="GlslLoader"/> so any plugin or system can <c>Load&lt;byte[]&gt;</c>
/// a <c>.glsl</c> file and receive compiled SPIR-V bytecode.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order:</b> <see cref="PluginOrder.Foundation"/> + 100 - runs after <see cref="AssetPlugin"/>
/// so <see cref="AssetServer"/> is guaranteed to exist, and before any consumer plugin that
/// loads a <c>.glsl</c> shader at <see cref="Stage.Startup"/> (e.g. the renderer's
/// <c>main_pass</c>, <c>VulkanImGuiPlugin</c>, <c>VulkanWebViewPlugin</c>).
/// </para>
/// <para>
/// Bundled in <see cref="DefaultPlugins"/>; standalone consumers that opt out of
/// <c>DefaultPlugins</c> should add it explicitly:
/// <code>
/// app.AddPlugin(new AssetPlugin())
///    .AddPlugin(new PipelinesPlugin());
/// </code>
/// </para>
/// </remarks>
/// <seealso cref="GlslLoader"/>
/// <seealso cref="AssetServer"/>
public sealed class PipelinesPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Pipelines");

    /// <inheritdoc />
    /// <remarks>
    /// Foundational consumer of <see cref="AssetServer"/>: registers shader loaders that
    /// downstream plugins implicitly rely on at <see cref="Stage.Startup"/>.
    /// </remarks>
    public int Order => PluginOrder.Foundation + 100;

    /// <inheritdoc />
    public void Build(App app)
    {
        // AssetPlugin is at PluginOrder.Foundation → AssetServer is guaranteed here.
        var server = app.World.Resource<AssetServer>();
        server.RegisterLoader(new GlslLoader());
        Logger.Info("PipelinesPlugin: GlslLoader registered with AssetServer.");
    }
}