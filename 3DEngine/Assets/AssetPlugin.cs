namespace Engine;

/// <summary>
/// Plugin that sets up the asset pipeline: registers the <see cref="AssetServer"/> resource,
/// default <see cref="FileAssetReader"/> source, built-in loaders, and per-frame processing systems.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registered resources:</b>
/// <list type="bullet">
///   <item><description><see cref="AssetServer"/> - central asset coordinator.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Registered systems:</b>
/// <list type="bullet">
///   <item><description><see cref="Stage.PreUpdate"/> - drains completed loads into <see cref="Assets{T}"/> and fires <see cref="AssetEvent{T}"/>.</description></item>
///   <item><description><see cref="Stage.Last"/> - clears asset events for the frame.</description></item>
///   <item><description><see cref="Stage.Cleanup"/> - disposes the <see cref="AssetServer"/>.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Default: source/ directory, no file watching
/// app.AddPlugin(new AssetPlugin());
///
/// // Custom: different directory, hot-reload enabled
/// app.AddPlugin(new AssetPlugin
/// {
///     AssetDirectory = "/path/to/source",
///     WatchForChanges = true,
///     WorkerThreads = 4,
/// });
/// </code>
/// </example>
/// <seealso cref="AssetServer"/>
/// <seealso cref="Assets{T}"/>
/// <seealso cref="Handle{T}"/>
/// <seealso cref="IAssetLoader{T}"/>
public sealed class AssetPlugin : IPlugin
{
    private static readonly ILogger Logger = Log.Category("Engine.Assets");

    /// <inheritdoc />
    /// <remarks>
    /// Foundational: many other plugins (textures, materials, scenes, models, webview,
    /// renderer-shader-loader) rely on <see cref="AssetServer"/>. Marking this plugin
    /// <see cref="PluginOrder.Foundation"/> guarantees it builds first when registered as
    /// part of an <see cref="IPluginGroup"/> (e.g. <c>DefaultPlugins</c>), so consumers do
    /// not need to declare an explicit <see cref="IPlugin.Dependencies"/> entry on it.
    /// </remarks>
    public int Order => PluginOrder.Foundation;

    /// <summary>
    /// Root directory for the default <see cref="FileAssetReader"/>.
    /// Defaults to <c>{AppContext.BaseDirectory}/source</c>.
    /// Set to <c>null</c> to skip adding a filesystem source.
    /// </summary>
    public string? AssetDirectory { get; init; } = null; // null = default convention

    /// <summary>
    /// When <c>true</c>, enables <see cref="FileWatcher"/>-based hot-reload for filesystem sources.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool WatchForChanges { get; init; }

    /// <summary>
    /// Number of background worker threads for async loading.
    /// <c>null</c> uses <c>Math.Max(2, ProcessorCount / 2)</c>.
    /// </summary>
    public int? WorkerThreads { get; init; }

    /// <summary>
    /// When <c>true</c>, registers built-in <see cref="ByteArrayLoader"/> and <see cref="StringLoader"/>.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool RegisterBuiltInLoaders { get; init; } = true;

    /// <inheritdoc />
    public void Build(App app)
    {
        Logger.Info("AssetPlugin: Building asset pipeline...");

        // Create the AssetServer
        var server = new AssetServer(WorkerThreads);

        // In-memory source: registered first so synthetic paths (embedded glTF textures,
        // future extracted-from-archive entries) shadow any same-named file on disk.
        // The reader is a singleton over InMemoryAssetReader.ProcessWideStore so importers
        // running on background worker threads can publish bytes without needing a World
        // reference. Also exposed as a resource for callers that prefer the world API.
        var inMemory = new InMemoryAssetReader();
        app.World.InsertResource(inMemory);
        server.AddSource(inMemory, "InMemory");

        // Add default filesystem source
        var dir = AssetDirectory ?? Path.Combine(AppContext.BaseDirectory, "source");
        if (Directory.Exists(dir) || AssetDirectory is not null)
        {
            server.AddSource(new FileAssetReader(dir), "FileSystem");
            Logger.Info($"AssetPlugin: Default filesystem source: {dir}");
        }
        else
        {
            Logger.Info($"AssetPlugin: Default source directory not found ({dir}), no filesystem source added. Create 'source/' to enable.");
        }

        // Register built-in loaders
        if (RegisterBuiltInLoaders)
        {
            server.RegisterLoader(new ByteArrayLoader());
            server.RegisterLoader(new StringLoader());
            Logger.Debug("AssetPlugin: Built-in loaders registered (ByteArray, String).");
        }

        // Enable hot-reload
        if (WatchForChanges)
        {
            server.EnableWatching();
            Logger.Info("AssetPlugin: Hot-reload file watching enabled.");
        }

        // Insert into world
        app.World.InsertResource(server);

        // -- Systems --

        // PreUpdate: drain completed loads → Assets<T> + fire AssetEvent<T>
        app.AddSystem(Stage.PreUpdate, new SystemDescriptor(world =>
            {
                world.Resource<AssetServer>().ProcessCompleted(world);
            }, "AssetPlugin.ProcessCompleted")
            .Write<AssetServer>());

        // Last: clear asset events
        app.AddSystem(Stage.Last, new SystemDescriptor(world =>
            {
                world.Resource<AssetServer>().ClearEvents(world);
            }, "AssetPlugin.ClearEvents")
            .Write<AssetServer>());

        // Cleanup: dispose the server (stop workers, watchers)
        app.AddSystem(Stage.Cleanup, new SystemDescriptor(world =>
            {
                if (world.TryGetResource<AssetServer>(out var s))
                    s.Dispose();
            }, "AssetPlugin.Cleanup")
            .Write<AssetServer>());

        Logger.Info("AssetPlugin: Asset pipeline ready.");
    }
}