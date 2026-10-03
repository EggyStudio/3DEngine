namespace Engine;

/// <summary>
/// Standard set of engine plugins as a sortable <see cref="IPluginGroup"/>: window,
/// input, ECS, behaviors, ImGui, renderer, WebView, etc.
/// </summary>
/// <remarks>
/// <para>
/// Plugins listed here are sorted by <see cref="IPlugin.Order"/> before being added to
/// the <see cref="App"/>. Foundational plugins (e.g. <c>AssetPlugin</c> at
/// <see cref="PluginOrder.Foundation"/>) automatically build first regardless of where
/// they appear in the list - so consumer plugins (textures, materials, scenes, models, ...)
/// don't need to declare an explicit dependency on them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var app = new App(Config.GetDefault(title: "My Game", width: 1280, height: 720));
/// app.AddPlugins(new DefaultPlugins());
/// app.AddSystem(Stage.Update, MyGameSystem);
/// app.Run();
/// </code>
/// </example>
/// <seealso cref="App"/>
/// <seealso cref="IPlugin"/>
/// <seealso cref="IPluginGroup"/>
public sealed class DefaultPlugins : IPluginGroup, IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Plugins");

    /// <inheritdoc />
    /// <remarks>
    /// Listed in source-readable order. <see cref="App.AddPlugins(IPluginGroup)"/> sorts
    /// by <see cref="IPlugin.Order"/> (stable sort), so foundational plugins automatically
    /// move to the front.
    /// </remarks>
    public IEnumerable<IPlugin> GetPlugins() =>
    [
        new ExceptionsPlugin(),
        new AppWindowPlugin(),
        new AppExitPlugin(),
        new SdlPlugin(),
        new SdlImGuiPlugin(),
        new AssetPlugin(),
        new ScenesPlugin(),
        new PipelinesPlugin(),
        new MaterialPlugin(),
        new ModelsPlugin(),
        new TexturesPlugin(),
        new LightingPlugin(),
        new TimePlugin(),
        new InputPlugin(),
        new EcsPlugin(),
        new PhysicsPlugin(),
    ];

    /// <summary>
    /// Registers every plugin returned by <see cref="GetPlugins"/> with <paramref name="app"/>,
    /// sorted by <see cref="IPlugin.Order"/> so foundational plugins build first, and then
    /// schedules a <see cref="Stage.First"/> system that calls <see cref="EcsWorld.BeginFrame"/>
    /// at the start of every frame to advance the tick and clear per-frame change tracking.
    /// </summary>
    /// <param name="app">The application to register the plugins and frame-tick system with.</param>
    public void Build(App app)
    {
        Logger.Info("DefaultPlugins: Loading standard engine plugin set...");
        app.AddPlugins(this);
        Logger.Info("DefaultPlugins: All standard plugins loaded successfully.");
    }
}