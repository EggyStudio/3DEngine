namespace Engine;

/// <summary>
/// Registers shader asset loaders with the <see cref="AssetServer"/>. Installs the
/// <see cref="SlangLoader"/>, so any plugin or system can <c>Load&lt;ShaderProgram&gt;</c> a
/// <c>.slang</c> file and receive the SPIR-V of each of its stages.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order:</b> <see cref="PluginOrder.Foundation"/> + 100 - runs after <see cref="AssetPlugin"/>
/// so <see cref="AssetServer"/> is guaranteed to exist, and before any consumer plugin that
/// loads a shader at <see cref="Stage.Startup"/>, such as the renderer's <c>main_pass</c> and
/// <c>VulkanImGuiPlugin</c>.
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
/// <seealso cref="SlangLoader"/>
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
        server.RegisterLoader(new SlangLoader());
        Logger.Info("PipelinesPlugin: SlangLoader registered with AssetServer.");
    }
}